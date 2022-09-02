using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
using DynamicData.Kernel;
using Microsoft.Extensions.Logging;
using Modulo3DDatabase;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class ArrayComparer<T> : IEqualityComparer<T[]>
    {
        public bool Equals(T[] x, T[] y)
        {
            return Enumerable.SequenceEqual(x, y);
        }
        public int GetHashCode(T[] obj)
        {
            throw new NotImplementedException();
        }
    }

    public struct OdometerExtrusions
    {
        public Optional<Guid> MCodeGuid { get; }
        public Optional<PrintProgress> PrintProgress { get; }
        public Optional<QueuePosition> QueuePosition { get; }
        public Optional<Dictionary<ushort, Extrusion>> Extrusions { get; }

        public OdometerExtrusions(Optional<Guid> mcode_guid, Optional<QueuePosition> queue_pos, Optional<PrintProgress> progress, Dictionary<ushort, Extrusion> extrusions)
        {
            MCodeGuid = mcode_guid;
            Extrusions = extrusions;
            PrintProgress = progress;
            QueuePosition = queue_pos;
        }
    }

    public class StatusProvider : ReactiveObject, IFluxStatusProvider
    {
        public FluxViewModel Flux { get; }

        private ObservableAsPropertyHelper<OdometerExtrusions> _OdometerExtrusions;
        public OdometerExtrusions OdometerExtrusions => _OdometerExtrusions.Value;

        public IObservableCache<FeederEvaluator, ushort> FeederEvaluators { get; private set; }
        public IObservableList<Dictionary<FluxJob, Material>> ExpectedMaterialsQueue { get; private set; }
        public IObservableList<Dictionary<FluxJob, Nozzle>> ExpectedNozzlesQueue { get; private set; }

        private bool _StartWithLowMaterials;
        public bool StartWithLowMaterials
        {
            get => _StartWithLowMaterials;
            set => this.RaiseAndSetIfChanged(ref _StartWithLowMaterials, value);
        }

        private ObservableAsPropertyHelper<FLUX_ProcessStatus> _FluxStatus;
        public FLUX_ProcessStatus FluxStatus => _FluxStatus.Value;

        private ObservableAsPropertyHelper<PrintProgress> _PrintProgress;
        public PrintProgress PrintProgress => _PrintProgress.Value;

        private ObservableAsPropertyHelper<PrintingEvaluation> _PrintingEvaluation;
        public PrintingEvaluation PrintingEvaluation => _PrintingEvaluation.Value;

        private ObservableAsPropertyHelper<StatusEvaluation> _StatusEvaluation;
        public StatusEvaluation StatusEvaluation => _StatusEvaluation.Value;

        private ObservableAsPropertyHelper<StartEvaluation> _StartEvaluation;
        public StartEvaluation StartEvaluation => _StartEvaluation.Value;

        private ObservableAsPropertyHelper<Optional<Dictionary<FluxJob, Dictionary<ushort, Extrusion>>>> _ExtrusionSetQueue;
        public Optional<Dictionary<FluxJob, Dictionary<ushort, Extrusion>>> ExtrusionSetQueue => _ExtrusionSetQueue.Value;

        public Optional<ConditionViewModel<bool>> ClampOpen { get; private set; }
        public Optional<ConditionViewModel<bool>> NotInChange { get; private set; }
        public Optional<ConditionViewModel<bool>> ClampClosed { get; private set; }
        public Optional<ConditionViewModel<bool>> RaisedPistons { get; private set; }
        public Optional<ConditionViewModel<double>> HasZBedHeight { get; private set; }
        public Optional<ConditionViewModel<(bool @in, bool @out)>> SpoolsLock { get; private set; }
        public Optional<ConditionViewModel<(bool @in, bool @out)>> TopLockOpen { get; private set; }
        public Optional<ConditionViewModel<(bool @in, bool @out)>> TopLockClosed { get; private set; }
        public Optional<ConditionViewModel<(bool @in, bool @out)>> ChamberLockOpen { get; private set; }
        public Optional<ConditionViewModel<(bool @in, bool @out)>> ChamberLockClosed { get; private set; }
        public Optional<ConditionViewModel<(Pressure @in, double level)>> PressurePresence { get; private set; }
        public Optional<ConditionViewModel<(Pressure @in, double level, bool @out)>> VacuumPresence { get; private set; }

        public StatusProvider(FluxViewModel flux)
        {
            Flux = flux;

            // Status
            var is_idle = Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS)
                .Convert(data => data == FLUX_ProcessStatus.IDLE)
                .DistinctUntilChanged()
                .ValueOr(() => false);

            // Safety
            var pressure_in = OptionalObservable.CombineLatest(
                Flux.ConnectionProvider.ObserveVariable(m => m.PRESSURE_PRESENCE),
                Flux.ConnectionProvider.ObserveVariable(m => m.PRESSURE_LEVEL),
                (pressure, level) =>
                {
                    if(pressure.HasValue && level.HasValue)
                        return (pressure.Value, level.Value);
                    return (new Pressure(0), 0);
                });

            PressurePresence = ConditionViewModel.Create(flux, "pressure", pressure_in,
                (state, value) =>
                {
                    if (value.Item1.Kpa < value.Item2)
                        return state.Create(false, "ATTIVARE L'ARIA COMPRESSA");
                    return state.Create(true, "ARIA COMPRESSA ATTIVA");
                });

            var clamp_open = Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_HEAD_CLAMP)
                .ValueOr(() => false);

            ClampOpen = ConditionViewModel.Create(flux, "clampOpen", clamp_open,
                (state, value) =>
                {
                    if (!value)
                        return state.Create(false, "APRIRE LA PINZA", "clamp", c => c.OPEN_HEAD_CLAMP, is_idle);
                    return state.Create(true, "PINZA APERTA", "clamp", c => c.OPEN_HEAD_CLAMP, is_idle);
                });

            ClampClosed = ConditionViewModel.Create(flux, "clampClosed", clamp_open,
                (state, value) =>
                {
                    if (value)
                        return state.Create(false, "CHIUDERE LA PINZA", "clamp", c => c.OPEN_HEAD_CLAMP);
                    return state.Create(true, "PINZA CHIUSA", "clamp", c => c.OPEN_HEAD_CLAMP);
                });

            var vacuum = OptionalObservable.CombineLatest(
                Flux.ConnectionProvider.ObserveVariable(m => m.VACUUM_PRESENCE),
                Flux.ConnectionProvider.ObserveVariable(m => m.VACUUM_LEVEL),
                Flux.ConnectionProvider.ObserveVariable(m => m.ENABLE_VACUUM),
                (pressure, level, enable) =>
                {
                    if (pressure.HasValue && level.HasValue && enable.HasValue)
                        return (pressure.Value, level.Value, enable.Value);
                    return (new Pressure(0), 0, false);
                });

            VacuumPresence = ConditionViewModel.Create(flux, "vacuum", vacuum,
                (state, value) =>
                {
                    if (!value.Item3 || value.Item1.Kpa > value.Item2)
                        return state.Create(false, "INSERIRE UN FOGLIO", "vacuum", c => c.ENABLE_VACUUM, is_idle);
                    return state.Create(true, "FOGLIO INSERITO", "vacuum", c => c.ENABLE_VACUUM, is_idle);
                });

            var top_lock = OptionalObservable.CombineLatest(
                Flux.ConnectionProvider.ObserveVariable(m => m.LOCK_CLOSED, "top"),
                Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, "top"),
                (closed, open) =>
                {
                    if (closed.HasValue && open.HasValue)
                        return (closed.Value, open.Value);
                    return (false, false);
                });

            TopLockClosed = ConditionViewModel.Create(flux, "topLockClosed", top_lock,
                (state, value) =>
                {
                    if (!value.Item1 || value.Item2)
                        return state.Create(false, "CHIUDERE IL CAPPELLO", "lock", c => c.OPEN_LOCK, "top", is_idle);
                    return state.Create(true, "CAPPELLO CHIUSO", "lock", c => c.OPEN_LOCK, "top", is_idle);
                });

            TopLockOpen = ConditionViewModel.Create(flux, "topLockOpen", top_lock,
                (state, value) =>
                {
                    if (value.Item1)
                        return state.Create(false, "APRIRE IL CAPPELLO", "lock", c => c.OPEN_LOCK, "top", is_idle);
                    return state.Create(true, "CAPPELLO APERTO", "lock", c => c.OPEN_LOCK, "top", is_idle);
                });


            var spools_lock = OptionalObservable.CombineLatest(
                Flux.ConnectionProvider.ObserveVariable(m => m.LOCK_CLOSED, "spools"),
                Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, "spools"),
                (closed, open) =>
                {
                    if (closed.HasValue && open.HasValue)
                        return (closed.Value, open.Value);
                    return (false, false);
                });

            SpoolsLock = ConditionViewModel.Create(flux, "spoolsLock", spools_lock,
                (state, value) =>
                {
                    if (!value.Item1 || value.Item2)
                        return state.Create(true, "PORTABOBINE APERTO", "lock", c => c.OPEN_LOCK, "spools", is_idle);
                    return state.Create(true, "PORTABOBINE CHIUSO", "lock", c => c.OPEN_LOCK, "spools", is_idle);
                });

            var chamber_lock = OptionalObservable.CombineLatest(
                Flux.ConnectionProvider.ObserveVariable(m => m.LOCK_CLOSED, "chamber"),
                Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, "chamber"),
                (closed, open) =>
                {
                    if (closed.HasValue && open.HasValue)
                        return (closed.Value, open.Value);
                    return (false, false);
                });

            ChamberLockClosed = ConditionViewModel.Create(flux, "chamberLockClosed", chamber_lock,
                (state, value) =>
                {
                    if (!value.Item1 || value.Item2)
                        return state.Create(false, "CHIUDERE LA PORTELLA", "lock", c => c.OPEN_LOCK, "chamber", is_idle);
                    return state.Create(true, "PORTELLA CHIUSA", "lock", c => c.OPEN_LOCK, "chamber", is_idle);
                });

            ChamberLockOpen = ConditionViewModel.Create(flux, "chamberLockOpen", chamber_lock,
                (state, value) =>
                {
                    if (value.Item1)
                        return state.Create(false, "APRIRE LA PORTELLA", "lock", c => c.OPEN_LOCK, "chamber", is_idle);
                    return state.Create(true, "PORTELLA APERTA", "lock", c => c.OPEN_LOCK, "chamber", is_idle);
                });

            // TODO
            /*var raised_pistions = Flux.ConnectionProvider.ObserveVariable(m => m.PISTON_LOW)
                .Convert(c => c.QueryWhenChanged(low => low.Items.All(low => low.HasValue && !low.Value)))
                .ToOptionalObservable();

            RaisedPistons = ConditionViewModel.Create(flux, "raisedPiston", raised_pistions,
                (state, value) =>
                {
                    if (!value)
                        return state.Create(false, "ALZARE TUTTI I PISTONI");
                    return state.Create(true, "STATO PISTONI CORRETTO");
                });*/

            var not_in_change = Flux.ConnectionProvider.ObserveVariable(m => m.IN_CHANGE)
                .ValueOr(() => false);

            NotInChange = ConditionViewModel.Create(flux, "notInChange", not_in_change,
                (state, value) =>
                {
                    if (value)
                        return state.Create(false, "STAMPANTE IN CHANGE");
                    return state.Create(true, "STAMPANTE NON IN CHANGE");
                });

            var has_safe_state = Observable.CombineLatest(
                Flux.ConnectionProvider.WhenAnyValue(v => v.IsInitializing),
                Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS),
                //RaisedPistons.ConvertToObservable(c => c.StateChanged),
                PressurePresence.ConvertToObservable(c => c.StateChanged),
                TopLockClosed.ConvertToObservable(c => c.StateChanged),
                ChamberLockClosed.ConvertToObservable(c => c.StateChanged),
                Flux.Feeders.WhenAnyValue(f => f.HasInvalidStates),
                Flux.Feeders.WhenAnyValue(f => f.SelectedExtruder),
                ClampClosed.ConvertToObservable(c => c.StateChanged),
                CanPrinterSafeCycle)
                .StartWith(false)
                .DistinctUntilChanged();

            var is_cycle = Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS)
                .Convert(data => data == FLUX_ProcessStatus.CYCLE)
                .DistinctUntilChanged()
                .ValueOr(() => false);

            var is_homed = Flux.ConnectionProvider.ObserveVariable(m => m.IS_HOMED)
                .DistinctUntilChanged()
                .ValueOr(() => false);

            var is_enabled_axis = Flux.ConnectionProvider.ObserveVariable(m => m.ENABLE_DRIVERS)
                .QueryWhenChanged(e =>
                {
                    if (e.Items.Any(e => !e.HasValue))
                        return Optional<bool>.None;
                    return e.Items.All(e => e.Value);
                })
                .DistinctUntilChanged()
                .ValueOr(() => false);

            FeederEvaluators = Flux.Feeders.Feeders.Connect()
                .QueryWhenChanged(CreateFeederEvaluator)
                .ToObservableChangeSet(e => e.Feeder.Position)
                .AsObservableCache();

            ExpectedMaterialsQueue = FeederEvaluators.Connect()
                .RemoveKey()
                .Transform(f => f.Material)
                .AutoRefresh(f => f.ExpectedDocumentQueue)
                .Transform(f => f.ExpectedDocumentQueue, true)
                .Filter(m => m.HasValue)
                .Transform(m => m.Value)
                .AsObservableList();

            ExpectedNozzlesQueue = FeederEvaluators.Connect()
                .RemoveKey()
                .Transform(f => f.ToolNozzle)
                .AutoRefresh(f => f.ExpectedDocumentQueue)
                .Transform(f => f.ExpectedDocumentQueue, true)
                .Filter(m => m.HasValue)
                .Transform(m => m.Value)
                .AsObservableList();

            var core_settings = Flux.SettingsProvider.CoreSettings.Local;

            var start_with_low_materials = this.WhenAnyValue(s => s.StartWithLowMaterials);

            var has_invalid_materials = FeederEvaluators.Connect()
                .TrueForAny(line => line.WhenAnyValue(l => l.Material.IsInvalid), invalid => invalid)
                .StartWith(false)
                .DistinctUntilChanged();

            var has_invalid_tools = FeederEvaluators.Connect()
                .TrueForAny(line => line.WhenAnyValue(l => l.ToolNozzle.IsInvalid), invalid => invalid)
                .StartWith(false)
                .DistinctUntilChanged();

            var has_invalid_probes = FeederEvaluators.Connect()
                .TrueForAny(line => line.WhenAnyValue(l => l.IsInvalidProbe), invalid => invalid)
                .StartWith(false)
                .DistinctUntilChanged();

            var has_low_materials = FeederEvaluators.Connect()
                .TrueForAny(line => line.WhenAnyValue(l => l.Material.HasLowWeight), low => low)
                .StartWith(false)
                .DistinctUntilChanged();

            var has_low_nozzles = FeederEvaluators.Connect()
                .TrueForAny(line => line.WhenAnyValue(l => l.ToolNozzle.HasLowWeight), low => low)
                .StartWith(false)
                .DistinctUntilChanged();

            var has_cold_nozzles = FeederEvaluators.Connect()
                .TrueForAny(line => line.WhenAnyValue(l => l.HasColdNozzle), cold => cold)
                .StartWith(false)
                .DistinctUntilChanged();

            var selected_part_program = Flux.ConnectionProvider
                .ObserveVariable(m => m.PART_PROGRAM)
                .DistinctUntilChanged()
                .StartWithDefault();

            selected_part_program.LogObservable(new EventId(0, "selected_part_program"), flux.Logger);

            var selected_guid = selected_part_program
                .Convert(pp => pp.MCodeGuid)
                .DistinctUntilChanged()
                .StartWithDefault();

            var selected_mcode =  Flux.MCodes.AvaiableMCodes.Connect()
                .AutoRefresh(m => m.Analyzer)
                .WatchOptional(selected_guid)
                .Convert(m => m.Analyzer)
                .Convert(a => a.MCode)
                .StartWithDefault()
                .DistinctUntilChanged();

            var has_invalid_printer = Observable.CombineLatest(
                core_settings.WhenAnyValue(v => v.PrinterID),
                selected_mcode,
                (printer_id, selected_mcode) => !selected_mcode.HasValue || selected_mcode.Value.PrinterId != printer_id);

            var can_safe_cycle = Observable.CombineLatest(
                is_idle,
                has_safe_state,
                (idle, safe) => idle && safe)
                .StartWith(false)
                .DistinctUntilChanged();

            var can_safe_print = Observable.CombineLatest(
                can_safe_cycle,
                VacuumPresence.ConvertToObservable(v => v.StateChanged),
                CanPrinterSafePrint);

            // TODO
            var is_safe_stop = Observable.CombineLatest(
                Flux.ConnectionProvider.ObserveVariable(m => m.RUNNING_MACRO).ObservableOrDefault(),
                Flux.ConnectionProvider.ObserveVariable(m => m.RUNNING_GCODE).ObservableOrDefault(),
                Flux.ConnectionProvider.ObserveVariable(m => m.RUNNING_MCODE).ObservableOrDefault(),
                IsSafeStop);

            var can_safe_stop = Observable.CombineLatest(
                is_cycle,
                has_safe_state,
                /*is_safe_stop,*/
                (cycle, safe/*, stop*/) => safe /*&& (!cycle || stop)*/)
                .StartWith(false)
                .DistinctUntilChanged();

            // TODO
            var is_safe_hold = Observable.CombineLatest(
                Flux.ConnectionProvider.ObserveVariable(m => m.RUNNING_MACRO).ObservableOrDefault(),
                Flux.ConnectionProvider.ObserveVariable(m => m.RUNNING_GCODE).ObservableOrDefault(),
                Flux.ConnectionProvider.ObserveVariable(m => m.RUNNING_MCODE).ObservableOrDefault(),
                IsSafePause);

            var can_safe_hold = Observable.CombineLatest(
                is_cycle,
                has_safe_state,
                /*is_safe_hold,*/
                (cycle, safe/*, pause*/) => cycle && safe/* && pause*/)
                .StartWith(false)
                .DistinctUntilChanged();

            var queue_pos = Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE_POS)
                .DistinctUntilChanged();

            bool distinct_queue(Dictionary<QueuePosition, FluxJob> d1, Dictionary<QueuePosition, FluxJob> d2)
            {
                if (d1.Count != d2.Count)
                    return false;
                foreach (var j1 in d1)
                {
                    if (!d2.TryGetValue(j1.Key, out var j2))
                        return false;
                    if (!j1.Value.Equals(j2))
                        return false;
                }
                return true;
            }

            var mcode_queue = Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE)
                .ValueOr(() => new Dictionary<QueuePosition, FluxJob>())
                .StartWith(new Dictionary<QueuePosition, FluxJob>())
                .DistinctUntilChanged(distinct_queue);

            var mcode_analyzers = Flux.MCodes.AvaiableMCodes.Connect()
                .AutoRefresh(m => m.Analyzer)
                .QueryWhenChanged(m => m.KeyValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Analyzer))
                .StartWith(new Dictionary<Guid, Optional<MCodeAnalyzer>>())
                .DistinctUntilChanged();

            var sample_print_progress = Observable.CombineLatest(
                is_cycle,
                selected_mcode,
                Observable.Interval(TimeSpan.FromSeconds(5)).StartWith(0),
                (_, _, _) => Unit.Default);

            var database = Flux.DatabaseProvider
                .WhenAnyValue(v => v.Database);

            var print_progress = this.WhenAnyValue(c => c.PrintProgress)
                .Sample(sample_print_progress)
                .DistinctUntilChanged();

            _StartEvaluation = Observable.CombineLatest(
                has_low_nozzles,
                has_cold_nozzles,
                has_low_materials,
                has_invalid_tools,
                has_invalid_probes,
                has_invalid_printer,
                has_invalid_materials,
                start_with_low_materials,
                StartEvaluation.Create)
                .DistinctUntilChanged()
                .ToProperty(this, v => v.StartEvaluation);

            var recovery = Flux.ConnectionProvider
                .ObserveVariable(c => c.MCODE_RECOVERY)
                .StartWithDefault()
                .DistinctUntilChanged();

            _PrintingEvaluation = Observable.CombineLatest(
                queue_pos,
                selected_mcode,
                recovery,
                selected_part_program,
                PrintingEvaluation.Create)
                .DistinctUntilChanged()
                .ToProperty(this, v => v.PrintingEvaluation);

            _StatusEvaluation = Observable.CombineLatest(
                is_idle,
                is_homed,
                is_cycle,
                can_safe_stop,
                can_safe_hold,
                can_safe_cycle,
                can_safe_print,
                is_enabled_axis,
                StatusEvaluation.Create)
                .DistinctUntilChanged()
                .ToProperty(this, v => v.StatusEvaluation);

            var progress = Flux.ConnectionProvider
                .ObserveVariable(c => c.PROGRESS)
                .DistinctUntilChanged();

            _PrintProgress = Observable.CombineLatest(
                this.WhenAnyValue(v => v.PrintingEvaluation),
                progress,
                GetPrintProgress)
                .DistinctUntilChanged()
                .ToProperty(this, v => v.PrintProgress);

            var z_bed_height = Flux.ConnectionProvider.ObserveVariable(m => m.Z_BED_HEIGHT)
                .ValueOr(() => FluxViewModel.MaxZBedHeight);

            HasZBedHeight = ConditionViewModel.Create(flux, "hasZBedHeight", z_bed_height,
                (state, value) =>
                {
                    if (value >= FluxViewModel.MaxZBedHeight)
                    {
                        var can_probe_plate = this.WhenAnyValue(v => v.StatusEvaluation)
                            .Select(s => s.CanSafePrint);
                        return state.Create(false, "TASTA IL PIATTO", "plate", c => c.ProbePlateAsync(), can_probe_plate);
                    }
                    return state.Create(true, "PIATTO TASTATO");
                });

            _OdometerExtrusions = Observable.CombineLatest(
                database,
                print_progress,
                queue_pos,
                selected_mcode,
                (database, progress, queue_pos, selected_mcode) => Observable.FromAsync(async () =>
                {
                    var extrusion_set = new Dictionary<ushort, Extrusion>();

                    if (!database.HasValue)
                        return new OdometerExtrusions(default, queue_pos, progress, extrusion_set);

                    if (!selected_mcode.HasValue || !queue_pos.HasValue)
                        return new OdometerExtrusions(default, queue_pos, progress, extrusion_set);

                    if (progress.Percentage > 0)
                    {
                        foreach (var feeder_report in selected_mcode.Value.FeederReports)
                        {
                            var extrusion_unit = Flux.ConnectionProvider.GetArrayUnit(c => c.EXTRUSIONS, feeder_report.Key);
                            if (!extrusion_unit.HasValue)
                                continue;

                            var extrusion = await Flux.ConnectionProvider.ReadVariableAsync(c => c.EXTRUSIONS, extrusion_unit.Value.Alias);
                            if (!extrusion.HasValue)
                                continue;

                            var nozzle_result = database.Value.FindById<Nozzle>(feeder_report.Value.NozzleId);
                            if (!nozzle_result.HasDocuments)
                                continue;

                            var material_result = database.Value.FindById<Material>(feeder_report.Value.MaterialId);
                            if (!material_result.HasDocuments)
                                continue;

                            var nozzle = nozzle_result.Documents.FirstOrDefault();
                            var material = material_result.Documents.FirstOrDefault();

                            extrusion_set[extrusion_unit.Value.Index] = Extrusion.CreateExtrusion(nozzle, material, extrusion.Value);
                        }
                    }

                    return new OdometerExtrusions(selected_mcode.Value.MCodeGuid, queue_pos, progress, extrusion_set);
                }))
                .Switch()
                .StartWith(new OdometerExtrusions())
                .ToProperty(this, v => v.OdometerExtrusions);

            var extrusion_set = this.WhenAnyValue(v => v.OdometerExtrusions);

            extrusion_set.PairWithPreviousValue()
                .Subscribe(async extrusions =>
                {
                    if (!extrusions.OldValue.QueuePosition.HasValue)
                        return;
                    if (!extrusions.NewValue.QueuePosition.HasValue)
                        return;
                    if (extrusions.OldValue.QueuePosition != extrusions.NewValue.QueuePosition)
                        return;

                    if (!extrusions.OldValue.MCodeGuid.HasValue)
                        return;
                    if (!extrusions.NewValue.MCodeGuid.HasValue)
                        return;
                    if (extrusions.OldValue.MCodeGuid.Value != extrusions.NewValue.MCodeGuid.Value)
                        return;

                    if (!extrusions.OldValue.PrintProgress.HasValue)
                        return;
                    if (!extrusions.NewValue.PrintProgress.HasValue)
                        return;
                    if (extrusions.NewValue.PrintProgress.Value.Percentage <= extrusions.OldValue.PrintProgress.Value.Percentage)
                        return;

                    if (!extrusions.OldValue.Extrusions.HasValue)
                        return;
                    if (!extrusions.NewValue.Extrusions.HasValue)
                        return;

                    var feeders = Flux.Feeders.Feeders;
                    foreach (var feeder in feeders.Items)
                    {
                        var start_extr = extrusions.OldValue.Extrusions.Value.Lookup(feeder.Position);
                        if (!start_extr.HasValue)
                            continue;

                        var end_extr = extrusions.NewValue.Extrusions.Value.Lookup(feeder.Position);
                        if (!end_extr.HasValue)
                            continue;

                        var extrusion_diff = end_extr.Value - start_extr.Value;
                        if (extrusion_diff.WeightG > 0)
                        {
                            Flux.Logger.LogInformation(new EventId(0, $"extr_{feeder.Position}"), $"{extrusion_diff}");

                            feeder.ToolNozzle.Odometer.AccumulateValue(extrusion_diff);
                            if (feeder.SelectedMaterial.HasValue)
                            { 
                                feeder.SelectedMaterial.Value.Odometer.AccumulateValue(extrusion_diff);

                                if (!feeder.SelectedMaterial.Value.Odometer.Value.HasValue)
                                    continue;

                                if (feeder.SelectedMaterial.Value.Odometer.Percentage > 0)
                                    continue;

                                var user_settings = flux.SettingsProvider.UserSettings.Local;
                                if (!user_settings.SoftwareFilamentSensor.HasValue)
                                    continue;

                                if (!user_settings.SoftwareFilamentSensor.Value)
                                    continue;

                                await Flux.ConnectionProvider.HoldAsync(true);
                            }
                        }
                    }
                });

            _ExtrusionSetQueue = Observable.CombineLatest(
                mcode_queue,
                extrusion_set,
                mcode_analyzers,
                GetExtrusionSetQueue)
                .StartWith(new Dictionary<FluxJob, Dictionary<ushort, Extrusion>>())
                .DistinctUntilChanged()
                .ToProperty(this, v => v.ExtrusionSetQueue);
        }

        public void Initialize()
        {
            // Status with messages
            var messages = Flux.Messages.Messages
                .Connect()
                .StartWithEmpty()
                .QueryWhenChanged();

            _FluxStatus = Observable.CombineLatest(
                messages,
                Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS),
                this.WhenAnyValue(v => v.PrintingEvaluation),
                Flux.Navigator.WhenAnyValue(nav => nav.CurrentViewModel),
                Flux.ConnectionProvider.WhenAnyValue(c => c.IsInitializing),
                FindFluxStatus)
                .DistinctUntilChanged()
                .ToProperty(this, s => s.FluxStatus);
        }

        // Status
        private bool IsSafeStop(
            Optional<OSAI_Macro> macro,
            Optional<OSAI_GCode> gcode,
            Optional<OSAI_MCode> mcode)
        {
            if (!macro.HasValue)
                return false;
            switch (macro.Value)
            {
                case OSAI_Macro.PROGRAM:
                case OSAI_Macro.PROBE_TOOL:
                case OSAI_Macro.LOAD_FILAMENT:
                case OSAI_Macro.GCODE_OR_MCODE:
                case OSAI_Macro.PURGE_FILAMENT:
                case OSAI_Macro.UNLOAD_FILAMENT:
                    if (!mcode.HasValue)
                        return false;
                    switch (mcode.Value)
                    {
                        case OSAI_MCode.CHAMBER_TEMP:
                            return true;
                        case OSAI_MCode.PLATE_TEMP:
                            return true;
                        case OSAI_MCode.TOOL_TEMP:
                            return true;
                        case OSAI_MCode.GCODE:
                            if (!gcode.HasValue)
                                return false;
                            switch (gcode.Value)
                            {
                                case OSAI_GCode.INTERP_MOVE:
                                    return true;
                            }
                            break;
                    }
                    break;
            }
            return false;
        }
        private bool IsSafePause(
            Optional<OSAI_Macro> macro,
            Optional<OSAI_GCode> gcode,
            Optional<OSAI_MCode> mcode)
        {
            if (!macro.HasValue)
                return false;
            switch (macro.Value)
            {
                case OSAI_Macro.PROGRAM:
                    if (!mcode.HasValue)
                        return false;
                    switch (mcode.Value)
                    {
                        case OSAI_MCode.GCODE:
                            if (!gcode.HasValue)
                                return false;
                            switch (gcode.Value)
                            {
                                case OSAI_GCode.INTERP_MOVE:
                                    return true;
                            }
                            break;
                    }
                    break;
            }
            return false;
        }

        private FLUX_ProcessStatus FindFluxStatus(
            IReadOnlyCollection<IFluxMessage> messages,
            Optional<FLUX_ProcessStatus> status,
            PrintingEvaluation printing_eval,
            Optional<IFluxRoutableViewModel> current_vm,
            Optional<bool> initializing_connection)
        {
            if (!status.HasValue)
                return FLUX_ProcessStatus.NONE;

            if (!status.HasValue)
                return FLUX_ProcessStatus.NONE;

            if (!initializing_connection.HasValue || initializing_connection.Value)
                return FLUX_ProcessStatus.NONE;

            var has_emerg = messages.Any(message => message.Level == MessageLevel.EMERG);
            if (has_emerg)
                return FLUX_ProcessStatus.EMERG;

            var has_error = messages.Any(message => message.Level == MessageLevel.ERROR);
            if (has_error)
                return FLUX_ProcessStatus.ERROR;

            switch (status.Value)
            {
                case FLUX_ProcessStatus.IDLE:
                    if (current_vm.HasValue && current_vm.Value is IOperationViewModel)
                        return FLUX_ProcessStatus.WAIT;
                    if (printing_eval.Recovery.HasValue)
                        return FLUX_ProcessStatus.WAIT;
                    if (printing_eval.SelectedMCode.HasValue)
                        return FLUX_ProcessStatus.WAIT;
                    return FLUX_ProcessStatus.IDLE;

                default:
                    return status.Value;
            }
        }

        private bool CanPrinterSafeCycle(
            Optional<bool> is_initializing,
            Optional<FLUX_ProcessStatus> status,
            /*OptionalChange<ConditionState> raised_pistons,*/
            OptionalChange<ConditionState> pressure,
            OptionalChange<ConditionState> top_lock,
            OptionalChange<ConditionState> chamber_lock,
            bool has_feeder_error,
            short selected_extruder,
            OptionalChange<ConditionState> clamp_closed)
        {
            if (!is_initializing.HasValue || is_initializing.Value)
                return false;
            if (!status.HasValue)
                return false;
            if (status.Value == FLUX_ProcessStatus.EMERG)
                return false;
            if (status.Value == FLUX_ProcessStatus.ERROR)
                return false;
            /*if (raised_pistons.HasChange && !raised_pistons.Change.Valid)
                return false;*/
            if (pressure.HasChange && !pressure.Change.Valid)
                return false;
            if (top_lock.HasChange && !top_lock.Change.Valid)
                return false;
            if (chamber_lock.HasChange && !chamber_lock.Change.Valid)
                return false;
            if (selected_extruder > -1 && clamp_closed.HasChange && !clamp_closed.Change.Valid)
                return false;
            if (has_feeder_error)
                return false;
            return true;
        }

        private bool CanPrinterSafePrint(bool can_safe_cycle, OptionalChange<ConditionState> vacuum)
        {
            if (!can_safe_cycle)
                return false;
            if (vacuum.HasChange && !vacuum.Change.Valid)
                return false;
            return true;
        }

        // Progress and extrusion
        private PrintProgress GetPrintProgress(PrintingEvaluation evaluation, Optional<ParamacroProgress> progress)
        {
            var selected_mcode = evaluation.SelectedMCode;
            if (!selected_mcode.HasValue)
                return new PrintProgress(0, TimeSpan.Zero);
            
            var duration = selected_mcode.Value.Duration;
            
            var selected_partprogram = evaluation.SelectedPartProgram;
            if (!selected_partprogram.HasValue)
                return new PrintProgress(0, duration);

            if (!progress.HasValue)
                return PrintProgress;

            if (!MCodePartProgram.TryParse(progress.Value.Paramacro, out var part_program))
                return PrintProgress;

            if (part_program.MCodeGuid != selected_partprogram.Value.MCodeGuid)
                return PrintProgress;

            var remaining_percentage = 100 - progress.Value.Percentage;
            var remaining_ticks = ((double)duration.Ticks / 100) * remaining_percentage;
            return new PrintProgress(progress.Value.Percentage, new TimeSpan((long)remaining_ticks));
        }
        private IEnumerable<FeederEvaluator> CreateFeederEvaluator(IQuery<IFluxFeederViewModel, ushort> query)
        {
            foreach (var feeder in query.Items)
            {
                var evaluator = new FeederEvaluator(this, feeder);
                evaluator.Initialize();
                yield return evaluator;
            }
        }
        private Optional<Dictionary<FluxJob, Dictionary<ushort, Extrusion>>> GetExtrusionSetQueue(Dictionary<QueuePosition, FluxJob> job_queue, OdometerExtrusions odometer_extrusion, Dictionary<Guid, Optional<MCodeAnalyzer>> mcode_analyzers)
        {

            try
            {
                if (!odometer_extrusion.QueuePosition.HasValue)
                    return default;
                if (!odometer_extrusion.MCodeGuid.HasValue)
                    return default;
                if (!odometer_extrusion.Extrusions.HasValue)
                    return default;

                var extrusion_set_queue = new Dictionary<FluxJob, Dictionary<ushort, Extrusion>>();
                foreach (var job in job_queue.Values)
                {
                    if (job.QueuePosition < odometer_extrusion.QueuePosition.Value)
                        continue;

                    var mcode_analyzer = mcode_analyzers.LookupOptional(job.MCodeGuid);
                    if (!mcode_analyzer.HasValue)
                        continue;

                    // copy extrusion set
                    var extrusion_set = new Dictionary<ushort, Extrusion>();
                    foreach (var extrusion in mcode_analyzer.Value.Extrusions)
                        extrusion_set.Add(extrusion.Key, extrusion.Value);

                    // remove odometer extrusion set from extrusion set
                    if (odometer_extrusion.MCodeGuid.Value == job.MCodeGuid &&
                        odometer_extrusion.QueuePosition.Value == job.QueuePosition)
                    {
                        foreach (var extrusion in odometer_extrusion.Extrusions.Value)
                            if (extrusion_set.ContainsKey(extrusion.Key))
                                extrusion_set[extrusion.Key] -= extrusion.Value;
                    }

                    extrusion_set_queue.Add(job, extrusion_set);
                }
                return extrusion_set_queue;
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
                return default;
            }
        }
    }
}
