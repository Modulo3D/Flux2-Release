using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
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

    public class StatusProvider : ReactiveObject, IFluxStatusProvider
    {
        public FluxViewModel Flux { get; }

        private ObservableAsPropertyHelper<(bool has_mcode, Extrusion[] extrusions)> _Extrusions;
        public (bool has_mcode, Extrusion[] extrusions) Extrusions => _Extrusions.Value;

        public IObservableCache<FeederEvaluator, ushort> FeederEvaluators { get; private set; }
        public IObservableList<Dictionary<QueueKey, Material>> ExpectedMaterialsQueue { get; private set; }
        public IObservableList<Dictionary<QueueKey, Nozzle>> ExpectedNozzlesQueue { get; private set; }

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

        public Optional<ConditionViewModel<bool>> ClampOpen { get; private set; }
        public Optional<ConditionViewModel<bool>> NotInChange { get; private set; }
        public Optional<ConditionViewModel<bool>> ClampClosed { get; private set; }
        public Optional<ConditionViewModel<bool>> RaisedPistons { get; private set; }
        public Optional<ConditionViewModel<(bool @in, bool @out)>> TopLockOpen { get; private set; }
        public Optional<ConditionViewModel<(bool @in, bool @out)>> TopLockClosed { get; private set; }
        public Optional<ConditionViewModel<(bool @in, bool @out)>> ChamberLockOpen { get; private set; }
        public Optional<ConditionViewModel<(bool @in, bool @out)>> ChamberLockClosed { get; private set; }
        public Optional<ConditionViewModel<(Pressure @in, double level)>> PressurePresence { get; private set; }
        public Optional<ConditionViewModel<(Pressure @in, double level, bool @out)>> VacuumPresence { get; private set; }

        public IObservable<Optional<bool>> IsIdle { get; }
        public IObservable<Optional<bool>> IsCycle { get; }
        public IObservable<bool> CanSafeCycle { get; }
        public IObservable<bool> CanSafePrint { get; }
        public IObservable<bool> CanSafeStop { get; }
        public IObservable<bool> CanSafeHold { get; }

        public StatusProvider(FluxViewModel flux)
        {
            Flux = flux;

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

            PressurePresence = ConditionViewModel.Create("pressure", pressure_in,
                value =>
                {
                    if (value.Item1.Kpa < value.Item2)
                        return new ConditionState(false, "ATTIVARE L'ARIA COMPRESSA");
                    return new ConditionState(true, "ARIA COMPRESSA ATTIVA");
                });

            var clamp_open = Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_HEAD_CLAMP)
                .ValueOr(() => false);

            ClampOpen = ConditionViewModel.Create("clampOpen", clamp_open,
                value =>
                {
                    if (!value)
                        return new ConditionState(false, "APRIRE LA PINZA");
                    return new ConditionState(true, "PINZA APERTA");
                });

            ClampClosed = ConditionViewModel.Create("clampClosed", clamp_open,
                value =>
                {
                    if (value)
                        return new ConditionState(false, "CHIUDERE LA PINZA");
                    return new ConditionState(true, "PINZA CHIUSA");
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

            VacuumPresence = ConditionViewModel.Create("vacuum", vacuum,
                value =>
                {
                    if (!value.Item3 || value.Item1.Kpa > value.Item2)
                        return new ConditionState(false, "INSERIRE UN FOGLIO");
                    return new ConditionState(true, "FOGLIO INSERITO");
                },
                TimeSpan.FromSeconds(1));

            var top_lock = OptionalObservable.CombineLatest(
                Flux.ConnectionProvider.ObserveVariable(m => m.LOCK_CLOSED, "top"),
                Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, "top"),
                (closed, open) =>
                {
                    if (closed.HasValue && open.HasValue)
                        return (closed.Value, open.Value);
                    return (false, false);
                });

            TopLockClosed = ConditionViewModel.Create("topLockClosed", top_lock,
                value =>
                {
                    if (!value.Item1 || value.Item2)
                        return new ConditionState(false, "CHIUDERE IL CAPPELLO");
                    return new ConditionState(true, "CAPPELLO CHIUSO");
                });

            TopLockOpen = ConditionViewModel.Create("topLockOpen", top_lock,
                value =>
                {
                    if (value.Item1)
                        return new ConditionState(false, "APRIRE IL CAPPELLO");
                    return new ConditionState(true, "CAPPELLO APERTO");
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

            ChamberLockClosed = ConditionViewModel.Create("chamberLockClosed", chamber_lock,
                value =>
                {
                    if (!value.Item1 || value.Item2)
                        return new ConditionState(false, "CHIUDERE LA PORTELLA");
                    return new ConditionState(true, "PORTELLA CHIUSA");
                });

            ChamberLockOpen = ConditionViewModel.Create("chamberLockOpen", chamber_lock,
                value =>
                {
                    if (value.Item1)
                        return new ConditionState(false, "APRIRE LA PORTELLA");
                    return new ConditionState(true, "PORTELLA APERTA");
                });

            var raised_pistions = Flux.ConnectionProvider.ObserveVariable(m => m.PISTON_LOW)
                .ConvertToObservable(c => c.QueryWhenChanged(low => low.Items.All(low => low.HasValue && !low.Value)));

            RaisedPistons = ConditionViewModel.Create("raisedPiston", raised_pistions,
                value =>
                {
                    if (!value)
                        return new ConditionState(false, "ALZARE TUTTI I PISTONI");
                    return new ConditionState(true, "STATO PISTONI CORRETTO");
                });

            var not_in_change = Flux.ConnectionProvider.ObserveVariable(m => m.IN_CHANGE)
                .ValueOr(() => false);

            NotInChange = ConditionViewModel.Create("notInChange", not_in_change,
                value =>
                {
                    if (value)
                        return new ConditionState(false, "STAMPANTE IN CHANGE");
                    return new ConditionState(true, "STAMPANTE NON IN CHANGE");
                });

            var has_safe_state = Observable.CombineLatest(
                Flux.ConnectionProvider.WhenAnyValue(v => v.IsInitializing),
                Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS),
                RaisedPistons.ConvertToObservable(c => c.StateChanged),
                PressurePresence.ConvertToObservable(c => c.StateChanged),
                TopLockClosed.ConvertToObservable(c => c.StateChanged),
                ChamberLockClosed.ConvertToObservable(c => c.StateChanged),
                Flux.Feeders.WhenAnyValue(f => f.HasInvalidStates),
                Flux.Feeders.WhenAnyValue(f => f.SelectedExtruder),
                ClampClosed.ConvertToObservable(c => c.StateChanged),
                CanPrinterSafeCycle)
                .StartWith(false)
                .DistinctUntilChanged();

            // Status
            IsIdle = Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS)
                .Convert(data => data == FLUX_ProcessStatus.IDLE)
                .DistinctUntilChanged();

            IsCycle = Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS)
                .Convert(data => data == FLUX_ProcessStatus.CYCLE)
                .DistinctUntilChanged();

            var is_homed = Flux.ConnectionProvider.ObserveVariable(m => m.IS_HOMED)
                .DistinctUntilChanged();

            var is_enabled_axis = Flux.ConnectionProvider.ObserveVariable(m => m.ENABLE_DRIVERS)
                .QueryWhenChanged(e =>
                {
                    if (e.Items.Any(e => !e.HasValue))
                        return Optional<bool>.None;
                    return e.Items.All(e => e.Value);
                })
                .DistinctUntilChanged();

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

            var selected_guid = selected_part_program
                .Convert(pp => pp.MCodeGuid)
                .DistinctUntilChanged()
                .StartWithDefault();

            var selected_mcode = Observable.CombineLatest(
                Flux.ConnectionProvider.ObserveVariable(m => m.ENABLE_VACUUM).ObservableOrDefault(),
                Flux.MCodes.AvaiableMCodes.Connect().WatchOptional(selected_guid),
                FindSelectedMCode)
                .StartWithDefault()
                .DistinctUntilChanged();

            CanSafeCycle = Observable.CombineLatest(
                IsIdle,
                has_safe_state,
                (idle, safe) => idle.ValueOrDefault() && safe)
                .StartWith(false)
                .DistinctUntilChanged();

            CanSafePrint = Observable.CombineLatest(
                CanSafeCycle,
                VacuumPresence.ConvertToObservable(v => v.StateChanged),
                CanPrinterSafePrint);

            // TODO
            var is_safe_stop = Observable.CombineLatest(
                Flux.ConnectionProvider.ObserveVariable(m => m.RUNNING_MACRO).ObservableOrDefault(),
                Flux.ConnectionProvider.ObserveVariable(m => m.RUNNING_GCODE).ObservableOrDefault(),
                Flux.ConnectionProvider.ObserveVariable(m => m.RUNNING_MCODE).ObservableOrDefault(),
                IsSafeStop);

            CanSafeStop = Observable.CombineLatest(
                IsCycle,
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

            CanSafeHold = Observable.CombineLatest(
                IsCycle,
                has_safe_state,
                /*is_safe_hold,*/
                (cycle, safe/*, pause*/) => cycle.ValueOrDefault() && safe/* && pause*/)
                .StartWith(false)
                .DistinctUntilChanged();

            var queue_pos = Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE_POS)
                .DistinctUntilChanged();

            bool distinct_queue(Dictionary<QueuePosition, Guid> d1, Dictionary<QueuePosition, Guid> d2)
            {
                try
                {
                    return string.Join(";", d1.Select(kvp => $"{kvp.Key}:{kvp.Value}")) ==
                        string.Join(";", d2.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
                }
                catch (Exception ex)
                {
                    return false;
                }
            }

            var mcode_queue = Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE)
                .ValueOr(() => new Dictionary<QueuePosition, Guid>())
                .StartWith(new Dictionary<QueuePosition, Guid>())
                .DistinctUntilChanged(distinct_queue);

            bool distinct_mcodes(Dictionary<Guid, IFluxMCodeStorageViewModel> d1, Dictionary<Guid, IFluxMCodeStorageViewModel> d2)
            {
                try
                {
                    return string.Join(";", d1.Select(kvp => $"{kvp.Key}:{kvp.Value}")) ==
                        string.Join(";", d2.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
                }
                catch (Exception ex)
                {
                    return false;
                }
            }

            var mcodes = Flux.MCodes.AvaiableMCodes.Connect()
                .QueryWhenChanged()
                .Select(q => q.KeyValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
                .StartWith(new Dictionary<Guid, IFluxMCodeStorageViewModel>())
                .DistinctUntilChanged(distinct_mcodes);

            var extrusions = Flux.ConnectionProvider
                .ObserveVariable(c => c.EXTRUSIONS)
                .QueryWhenChanged()
                .ThrottleMax(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));

            _Extrusions = Observable.CombineLatest(extrusions, selected_mcode, (extrusions, selected_mcode) =>
                {
                    var extrusion_set = new Extrusion[16];
                    if (!selected_mcode.HasValue)
                        return (false, extrusion_set);

                    foreach (var extrusion in extrusions.KeyValues)
                    {
                        if (!ushort.TryParse(extrusion.Key, out var extruder))
                            continue;
                        if (!extrusion.Value.HasValue)
                            continue;
                        var report = selected_mcode.Value.FeederReports.Lookup(extruder);
                        if (!report.HasValue)
                            continue;
                        extrusion_set[extruder] = new Extrusion(report.Value, extrusion.Value.Value);
                    }

                    return (true, extrusion_set);
                })
                .ToProperty(this, v => v.Extrusions);

            this.WhenAnyValue(v => v.Extrusions)
                .PairWithPreviousValue()
                .Subscribe(extrusions =>
                {
                    if (!extrusions.OldValue.has_mcode)
                        return;
                    if (!extrusions.NewValue.has_mcode)
                        return;
                    if (extrusions.OldValue.extrusions == null)
                        return;
                    if (extrusions.NewValue.extrusions == null)
                        return;

                    // reset extrusion value
                    for (int e = 0; e < extrusions.OldValue.extrusions.Length; e++)
                        if (extrusions.OldValue.extrusions[e].WeightG > extrusions.NewValue.extrusions[e].WeightG)
                            extrusions.OldValue.extrusions[e] = new Extrusion(0);

                    var feeders = Flux.Feeders.Feeders;
                    foreach (var feeder in feeders.Items)
                    {
                        var extrusion_diff = extrusions.NewValue.extrusions[feeder.Position] - extrusions.OldValue.extrusions[feeder.Position];
                        if (extrusion_diff.WeightG > 0)
                        { 
                            feeder.ToolNozzle.Odometer.AccumulateValue(extrusion_diff);
                            if(feeder.SelectedMaterial.HasValue)
                                feeder.SelectedMaterial.Value.Odometer.AccumulateValue(extrusion_diff);
                        }
                    }
                });

            var extrusion_set = this.WhenAnyValue(v => v.Extrusions);

            var extrusion_set_queue = Observable.CombineLatest(
                queue_pos,
                mcode_queue,
                extrusion_set,
                mcodes,
                GetExtrusionSetQueue)
                .StartWithDefault()
                .DistinctUntilChanged();

            _StartEvaluation = Observable.CombineLatest(
                has_low_nozzles,
                has_cold_nozzles,
                has_low_materials,
                has_invalid_tools,
                has_invalid_probes,
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
                selected_mcode,
                recovery,
                selected_part_program,
                extrusion_set_queue,
                PrintingEvaluation.Create)
                .DistinctUntilChanged()
                .ToProperty(this, v => v.PrintingEvaluation);

            _StatusEvaluation = Observable.CombineLatest(
                IsIdle,
                is_homed,
                IsCycle,
                CanSafeStop,
                CanSafeHold,
                CanSafeCycle,
                CanSafePrint,
                is_enabled_axis,
                StatusEvaluation.Create)
                .DistinctUntilChanged()
                .ToProperty(this, v => v.StatusEvaluation);

            var block_nr = Flux.ConnectionProvider.ObserveVariable(m => m.BLOCK_NUM)
                .DistinctUntilChanged();

            _PrintProgress = Observable.CombineLatest(
                this.WhenAnyValue(v => v.PrintingEvaluation),
                block_nr,
                GetPrintProgress)
                .DistinctUntilChanged()
                .ToProperty(this, v => v.PrintProgress);
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
            OptionalChange<ConditionState> raised_pistons,
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
            if (raised_pistons.HasChange && raised_pistons.Change.Valid)
                return false;
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
        private PrintProgress GetPrintProgress(PrintingEvaluation evaluation, Optional<LineNumber> line_nr)
        {
            var selected_mcode = evaluation.SelectedMCode;
            // update job remaining time
            if (!selected_mcode.HasValue)
                return new PrintProgress(0, TimeSpan.Zero);

            // return last line_nr
            if ((!line_nr.HasValue || line_nr.Value == 0) && PrintProgress.Percentage > 0)
                return PrintProgress;
            
            var blocks = selected_mcode.Value.BlockCount;
            var progress = Math.Max(0, Math.Min(1, (float)line_nr.ValueOr(() => new LineNumber(0)) / blocks));
            if (float.IsNaN(progress))
                progress = 0;

            progress = Math.Clamp(progress, 0, 1);
            var duration = selected_mcode.Value.Duration;
            var remaining_ticks = duration.Ticks * (1 - progress);
            return new PrintProgress(progress * 100, new TimeSpan((long)remaining_ticks));
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
        private Optional<Dictionary<QueueKey, Extrusion[]>> GetExtrusionSetQueue(Optional<QueuePosition> queue_pos, Dictionary<QueuePosition, Guid> queue, (bool has_mcode, Extrusion[] extrusions) odometer_extrusion_set, Dictionary<Guid, IFluxMCodeStorageViewModel> mcodes)
        {
            try
            {
                if (!queue_pos.HasValue)
                    return default;

                var extrusion_set_queue = new Dictionary<QueueKey, Extrusion[]>();
                foreach (var mcode_queue in queue)
                {
                    var queue_key = new QueueKey(mcode_queue.Value, mcode_queue.Key);
                    if (mcode_queue.Key < queue_pos.Value)
                        continue;

                    var mcode_vm = mcodes.Lookup(mcode_queue.Value);
                    if (!mcode_vm.HasValue)
                        continue;

                    var mcode_analyzer = mcode_vm.Value.Analyzer;

                    var extrusion_set = new Extrusion[16];
                    if (queue_pos.Value == mcode_queue.Key && odometer_extrusion_set.has_mcode && odometer_extrusion_set.extrusions != null)
                        for (int e = 0; e < extrusion_set.Length; e++)
                            extrusion_set[e] = mcode_analyzer.Extrusions[e] - odometer_extrusion_set.extrusions[e];

                    extrusion_set_queue.Add(queue_key, extrusion_set);
                }
                return extrusion_set_queue;
            }
            catch(Exception ex)
            {
                Flux.Messages.LogException(this, ex);
                return default;
            }
        }
        private Optional<MCode> FindSelectedMCode(Optional<bool> enabled_vacuum, Optional<IFluxMCodeStorageViewModel> mcode_vm)
        {
            if (enabled_vacuum.HasValue && !enabled_vacuum.Value)
                return default;
            if (!mcode_vm.HasValue)
                return default;
            return mcode_vm.Value.Analyzer.MCode;
        }
    }
}
