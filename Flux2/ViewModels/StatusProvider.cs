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

        public IObservableCache<FeederEvaluator, ushort> FeederEvaluators { get; private set; }
        public IObservableList<Dictionary<ushort, Material>> ExpectedMaterialsQueue { get; private set; }
        public IObservableList<Dictionary<ushort, Nozzle>> ExpectedNozzlesQueue { get; private set; }

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

        public ConditionViewModel<bool> ClampOpen { get; private set; }
        public ConditionViewModel<bool> NotInChange { get; private set; }
        public ConditionViewModel<bool> ClampClosed { get; private set; }
        public ConditionViewModel<bool> TopLockOpen { get; private set; }
        public ConditionViewModel<bool> RaisedPistons { get; private set; }
        public ConditionViewModel<bool> ChamberLockOpen { get; private set; }
        public ConditionsViewModel<bool, bool> TopLockClosed { get; private set; }
        public ConditionsViewModel<bool, bool> ChamberLockClosed { get; private set; }
        public ConditionViewModel<(Pressure pressure, double level)> PressurePresence { get; private set; }
        public ConditionsViewModel<(Pressure pressure, double level), bool> VacuumPresence { get; private set; }

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
            var pressure_in = Observable.CombineLatest(
                Flux.ConnectionProvider.ObserveVariable(m => m.PRESSURE_PRESENCE),
                Flux.ConnectionProvider.ObserveVariable(m => m.PRESSURE_LEVEL),
                (pressure, level) =>
                {
                    if (pressure.HasValue && level.HasValue)
                        return Optional.Some<(Pressure pressure, double level)>((pressure.Value, level.Value));
                    return default;
                });

            PressurePresence = ConditionViewModel.Create("pressure",
               pressure_in,
               v_in => v_in.pressure.Kpa > v_in.level,
               (value, valid) => valid ? "ARIA COMPRESSA ATTIVA" : "ATTIVARE L'ARIA COMPRESSA");

            ClampOpen = ConditionViewModel.Create("clampOpen",
                Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_HEAD_CLAMP),
                (value, valid) => valid ? "PINZA APERTA" : "APRIRE LA PINZA");

            ClampClosed = ConditionViewModel.Create("clampClosed",
                Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_HEAD_CLAMP),
                v => !v,
                (value, valid) => valid ? "PINZA CHIUSA" : "CHIUDERE LA PINZA");

            var vacuum_in = Observable.CombineLatest(
                Flux.ConnectionProvider.ObserveVariable(m => m.VACUUM_PRESENCE),
                Flux.ConnectionProvider.ObserveVariable(m => m.VACUUM_LEVEL),
                (pressure, level) =>
                {
                    if (pressure.HasValue && level.HasValue)
                        return Optional.Some<(Pressure pressure, double level)>((pressure.Value, level.Value));
                    return default;
                });

            VacuumPresence = ConditionViewModel.Create("vacuum",
                vacuum_in,
                Flux.ConnectionProvider.ObserveVariable(m => m.ENABLE_VACUUM),
                (v_in, v_out) => !v_out || v_in.pressure.Kpa < v_in.level,
                (value, valid) => valid ? "FOGLIO INSERITO" : "INSERIRE UN FOGLIO",
                TimeSpan.FromSeconds(1));

            TopLockClosed = ConditionViewModel.Create("topLockClosed",
                Flux.ConnectionProvider.ObserveVariable(m => m.LOCK_CLOSED, "top"),
                Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, "top"),
                (lock_in, lock_out) => lock_in && !lock_out,
                (value, valid) => valid ? "CAPPELLO CHIUSO" : "CHIUDERE IL CAPPELLO");

            ChamberLockClosed = ConditionViewModel.Create("chamberLockClosed",
                Flux.ConnectionProvider.ObserveVariable(m => m.LOCK_CLOSED, "chamber"),
                Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, "chamber"),
                (lock_in, lock_out) => lock_in && !lock_out,
                (value, valid) => valid ? "PORTELLA CHIUSA" : "CHIUDERE LA PORTELLA");

            ChamberLockOpen = ConditionViewModel.Create("chamberLockOpen",
                ChamberLockClosed.IsValidChanged.Convert(c => !c),
                (value, valid) => valid ? "PORTELLA APERTA" : "APRIRE LA PORTELLA");

            TopLockOpen = ConditionViewModel.Create("topLockOpen",
                TopLockClosed.IsValidChanged.Convert(c => !c),
                (value, valid) => valid ? "CAPPELLO APERTO" : "APRIRE IL CAPPELLO");

            RaisedPistons = ConditionViewModel.Create("raisedPiston",
                Flux.ConnectionProvider.ObserveVariable(m => m.PISTON_LOW)
                .QueryWhenChanged(low => low.Items.All(low => low.HasValue && !low.Value))
                .Select(v => Optional.Some(v)),
                (value, valid) => valid ? "STATO PISTONI CORRETTO" : "ALZARE TUTTI I PISTONI");

            NotInChange = ConditionViewModel.Create("notInChange",
                Flux.ConnectionProvider.ObserveVariable(m => m.IN_CHANGE)
                    .Convert(c => !c),
                (value, valid) => valid ? "STAMPANTE NON IN CHANGE" : "STAMPANTE IN CHANGE");

            var has_safe_state = Observable.CombineLatest(
                Flux.ConnectionProvider.WhenAnyValue(v => v.IsInitializing),
                Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS),
                RaisedPistons.IsValidChanged,
                PressurePresence.IsValidChanged,
                TopLockClosed.IsValidChanged,
                ChamberLockClosed.IsValidChanged,
                Flux.Feeders.WhenAnyValue(f => f.HasInvalidStates),
                Flux.Feeders.WhenAnyValue(f => f.SelectedExtruder),
                ClampClosed.IsValidChanged,
                CanPrinterSafeCycle)
                .StartWith(false)
                .DistinctUntilChanged();

            // Status
            IsIdle = Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS)
                .Convert(data => data == FLUX_ProcessStatus.IDLE)
                .StartWithEmpty()
                .DistinctUntilChanged();

            IsCycle = Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS)
                .Convert(data => data == FLUX_ProcessStatus.CYCLE)
                .StartWithEmpty()
                .DistinctUntilChanged();

            var is_homed = Flux.ConnectionProvider.ObserveVariable(m => m.IS_HOMED)
                .StartWithEmpty()
                .DistinctUntilChanged();

            var is_enabled_axis = Flux.ConnectionProvider.ObserveVariable(m => m.ENABLE_DRIVERS)
                .QueryWhenChanged(e =>
                {
                    if (e.Items.Any(e => !e.HasValue))
                        return Optional<bool>.None;
                    return e.Items.All(e => e.Value);
                })
                .StartWithEmpty()
                .DistinctUntilChanged();

            FeederEvaluators = Flux.Feeders.Feeders.Connect()
                .QueryWhenChanged(CreateFeederEvaluator)
                .ToObservableChangeSet(e => e.Feeder.Position)
                .AsObservableCache();

            var material_comparer = SortExpressionComparer<MaterialEvaluator>
                .Descending(p => p.FeederEvaluator.Feeder.Position);

            ExpectedMaterialsQueue = FeederEvaluators.Connect()
                .RemoveKey()
                .Transform(f => f.Material)
                .Sort(material_comparer)
                .AutoRefresh(f => f.ExpectedDocumentQueue)
                .Transform(f => f.ExpectedDocumentQueue, true)
                .Filter(m => m.HasValue)
                .Transform(m => m.Value)
                .AsObservableList();

            var nozzle_comparer = SortExpressionComparer<ToolNozzleEvaluator>
                .Descending(p => p.FeederEvaluator.Feeder.Position);

            ExpectedNozzlesQueue = FeederEvaluators.Connect()
                .RemoveKey()
                .Transform(f => f.ToolNozzle)
                .Sort(nozzle_comparer)
                .AutoRefresh(f => f.ExpectedDocumentQueue)
                .Transform(f => f.ExpectedDocumentQueue, true)
                .Filter(m => m.HasValue)
                .Transform(m => m.Value)
                .AsObservableList();

            var start_with_low_materials = this.WhenAnyValue(s => s.StartWithLowMaterials);

            var has_invalid_materials = FeederEvaluators.Connect()
                .TrueForAny(line => line.WhenAnyValue(l => l.Material.IsValid), valid => !valid)
                .StartWith(false)
                .DistinctUntilChanged();

            var has_invalid_tools = FeederEvaluators.Connect()
                .TrueForAny(line => line.WhenAnyValue(l => l.ToolNozzle.IsValid), valid => !valid)
                .StartWith(false)
                .DistinctUntilChanged();

            var has_invalid_probes = FeederEvaluators.Connect()
                .TrueForAny(line => line.WhenAnyValue(l => l.IsValidProbe), valid => !valid)
                .StartWith(false)
                .DistinctUntilChanged();

            var has_low_materials = FeederEvaluators.Connect()
                .TrueForAny(line => line.WhenAnyValue(l => l.Material.HasEnoughWeight), enough => !enough)
                .StartWith(false)
                .DistinctUntilChanged();

            var has_low_nozzles = FeederEvaluators.Connect()
                .TrueForAny(line => line.WhenAnyValue(l => l.ToolNozzle.HasEnoughWeight), enough => !enough)
                .StartWith(false)
                .DistinctUntilChanged();

            var has_cold_nozzles = FeederEvaluators.Connect()
                .TrueForAny(line => line.WhenAnyValue(l => l.HasHotNozzle), hot => hot.ConvertOr(h => !h, () => false))
                .StartWith(false)
                .DistinctUntilChanged();

            var selected_part_program = Flux.ConnectionProvider.ObserveVariable(m => m.PART_PROGRAM)
                .DistinctUntilChanged()
                .StartWithEmpty();

            var selected_guid = selected_part_program.Convert(pp => pp.MCodeGuid)
                .DistinctUntilChanged()
                .StartWithEmpty();

            var selected_mcode = Observable.CombineLatest(
                Flux.ConnectionProvider.ObserveVariable(m => m.ENABLE_VACUUM),
                Flux.MCodes.AvaiableMCodes.Connect().WatchOptional(selected_guid),
                FindSelectedMCode)
                .StartWithEmpty()
                .DistinctUntilChanged();

            var hold_tool = Flux.ConnectionProvider
                 .ObserveVariable(m => m.HOLD_TOOL)
                 .DistinctUntilChanged()
                 .StartWithEmpty();

            var hold_blk_num = Flux.ConnectionProvider
                 .ObserveVariable(m => m.HOLD_BLK_NUM)
                 .DistinctUntilChanged()
                 .StartWithEmpty();

            var hold_pp = Flux.ConnectionProvider
                 .ObserveVariable(m => m.HOLD_PP)
                 .DistinctUntilChanged()
                 .StartWithEmpty();

            var hold_temperature = Flux.ConnectionProvider
                .ObserveVariable(m => m.HOLD_TEMP)
                .Filter(t => t.HasValue)
                .Transform(t => t.Value)
                .QueryWhenChanged();

            var hold_position = Flux.ConnectionProvider
                .ObserveVariable(m => m.HOLD_TEMP)
                .Filter(t => t.HasValue)
                .Transform(t => t.Value)
                .QueryWhenChanged();

            var recovery = Observable.CombineLatest(
                hold_tool,
                hold_blk_num,
                hold_pp,
                hold_temperature,
                hold_position,
                selected_part_program,
                FindRecovery)
                .StartWithEmpty()
                .DistinctUntilChanged();

            CanSafeCycle = Observable.CombineLatest(
                IsIdle,
                has_safe_state,
                (idle, safe) => idle.ValueOrDefault() && safe)
                .StartWith(false)
                .DistinctUntilChanged();

            CanSafePrint = Observable.CombineLatest(
                CanSafeCycle,
                VacuumPresence.IsValidChanged,
                CanPrinterSafePrint);

            // TODO
            var is_safe_stop = Observable.CombineLatest(
                Flux.ConnectionProvider.ObserveVariable(m => m.RUNNING_MACRO),
                Flux.ConnectionProvider.ObserveVariable(m => m.RUNNING_GCODE),
                Flux.ConnectionProvider.ObserveVariable(m => m.RUNNING_MCODE),
                IsSafeStop);

            CanSafeStop = Observable.CombineLatest(
                IsCycle,
                has_safe_state,
                /*is_safe_stop,*/
                (cycle, safe/*, stop*/) => safe /*&& (!cycle || stop)*/)
                .StartWith(false)
                .DistinctUntilChanged();

            var is_safe_hold = Observable.CombineLatest(
                Flux.ConnectionProvider.ObserveVariable(m => m.RUNNING_MACRO),
                Flux.ConnectionProvider.ObserveVariable(m => m.RUNNING_GCODE),
                Flux.ConnectionProvider.ObserveVariable(m => m.RUNNING_MCODE),
                IsSafePause);

            CanSafeHold = Observable.CombineLatest(
                IsCycle,
                has_safe_state,
                is_safe_hold,
                (cycle, safe, pause) => cycle.ValueOrDefault() && safe && pause)
                .StartWith(false)
                .DistinctUntilChanged();

            var queue_pos = Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE_POS)
                .StartWithEmpty()
                .DistinctUntilChanged();

            var mcode_queue = Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE)
                .StartWithEmpty()
                .DistinctUntilChanged();

            var mcodes = Flux.MCodes.AvaiableMCodes
                .Connect()
                .QueryWhenChanged();

            var extrusion_set_queue = Observable.CombineLatest(
                queue_pos,
                mcode_queue,
                selected_part_program,
                mcodes,
                GetExtrusionSetQueue)
                .StartWithEmpty()
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


            _PrintProgress = Observable.CombineLatest(
                this.WhenAnyValue(v => v.PrintingEvaluation),
                Flux.ConnectionProvider.ObserveVariable(m => m.BLOCK_NUM),
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

        private Optional<MCodeRecovery> FindRecovery(
            Optional<short> hold_tool,
            Optional<double> hold_blk_num,
            Optional<string> hold_part_program, 
            IQuery<double, VariableUnit> hold_temp,
            IQuery<double, VariableUnit> hold_pos,
            Optional<MCodePartProgram> selected_pp)
        {
            try
            {
                if (!hold_tool.HasValue)
                    return default;

                if (!hold_blk_num.HasValue)
                    return default;

                if (hold_temp.Count < 1)
                    return default;

                if (!hold_part_program.HasValue)
                    return default;

                if (string.IsNullOrEmpty(hold_part_program.Value))
                    return default;

                if (!Guid.TryParse(hold_part_program.Value, out var mcode_guid))
                    return default;

                var mcode_lookup = Flux.MCodes.AvaiableMCodes.Lookup(mcode_guid);
                if (!mcode_lookup.HasValue)
                    return default;

                var analyzer = mcode_lookup.Value.Analyzer;
                var is_selected = selected_pp.ConvertOr(pp =>
                {
                    if (!pp.IsRecovery)
                        return false;
                    if (pp.MCodeGuid != analyzer.MCode.MCodeGuid)
                        return false;
                    if (pp.StartBlock != hold_blk_num.Value)
                        return false;
                    return true;
                }, () => false);

                return new MCodeRecovery(
                    analyzer.MCode.MCodeGuid,
                    is_selected,
                    (uint)hold_blk_num.Value,
                    (ushort)hold_tool.Value,
                    hold_temp.KeyValues.ToDictionary(),
                    hold_pos.KeyValues.ToDictionary());
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
                return default;
            }
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
            Optional<bool> raised_pistons,
            Optional<bool> pressure,
            Optional<bool> top_lock,
            Optional<bool> chamber_lock,
            bool has_feeder_error,
            short selected_extruder,
            Optional<bool> clamp_closed)
        {
            if (!is_initializing.HasValue || is_initializing.Value)
                return false;
            if (!status.HasValue)
                return false;
            if (status.Value == FLUX_ProcessStatus.EMERG)
                return false;
            if (status.Value == FLUX_ProcessStatus.ERROR)
                return false;
            if (raised_pistons.HasValue && !raised_pistons.Value)
                return false;
            if (pressure.HasValue && !pressure.Value)
                return false;
            if (top_lock.HasValue && !top_lock.Value)
                return false;
            if (chamber_lock.HasValue && !chamber_lock.Value)
                return false;
            if (selected_extruder > -1 && clamp_closed.HasValue && !clamp_closed.Value)
                return false;
            if (has_feeder_error)
                return false;
            return true;
        }

        private bool CanPrinterSafePrint(bool can_safe_cycle, Optional<bool> vacuum)
        {
            if (!can_safe_cycle)
                return false;
            if (vacuum.HasValue && !vacuum.Value)
                return false;
            return true;
        }

        // Progress and extrusion
        private PrintProgress GetPrintProgress(PrintingEvaluation evaluation, Optional<uint> block_nr)
        {
            if (!block_nr.HasValue)
                return new PrintProgress(0, TimeSpan.Zero);

            var selected_mcode = evaluation.SelectedMCode;

            // update job remaining time
            if (!selected_mcode.HasValue)
                return new PrintProgress(0, TimeSpan.Zero);

            var blocks = selected_mcode.Value.BlockCount;
            var block_number = block_nr.ValueOr(() => 0U);
            var progress = Math.Max(0, Math.Min(1, (float)block_number / blocks));

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
        private Optional<Dictionary<ushort, ExtrusionSet>> GetExtrusionSetQueue(Optional<short> queue_pos, Optional<Dictionary<ushort, Guid>> queue, Optional<MCodePartProgram> part_program, IQuery<IFluxMCodeStorageViewModel, Guid> mcodes)
        {
            if (!queue_pos.HasValue)
                return default;
            if (!queue.HasValue)
                return default;
            if (!part_program.HasValue)
                return default;

            var extrusion_set_queue = new Dictionary<ushort, ExtrusionSet>();
            foreach (var mcode_queue in queue.Value)
            {
                if (mcode_queue.Key < queue_pos.Value)
                    continue;
                var mcode_vm = mcodes.Lookup(mcode_queue.Value);
                if (!mcode_vm.HasValue)
                    continue;

                var start_block = queue_pos.Value == mcode_queue.Key ? part_program.Value.StartBlock : 0;

                var mcode_analyzer = mcode_vm.Value.Analyzer;
                var extrusion_set = mcode_analyzer.MCodeReader.GetFilamentExtrusionSet(start_block, mcode_analyzer.MCode.BlockCount);

                extrusion_set_queue.Add(mcode_queue.Key, extrusion_set);
            }
            return extrusion_set_queue;
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
