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
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;

namespace Flux.ViewModels
{

    public class ConditionDictionary<TConditionAttribute> : Dictionary<string, List<(IConditionViewModel condition, TConditionAttribute condition_attribute)>>
    {
    }
    public abstract class ConditionAttribute : Attribute
    {
        public ConditionAttribute()
        {
        }
    }
    public class FilterConditionAttribute : ConditionAttribute
    {
        public bool FilterOnCycle { get; }
        public Optional<string> Name { get; }
        public Optional<string[]> IncludeAlias { get; }
        public Optional<string[]> ExcludeAlias { get; }
        public FilterConditionAttribute(string name = default, bool filter_on_cycle = true, string[] include_alias = default, string[] exclude_alias = default)
        {
            Name = name;
            IncludeAlias = include_alias;
            ExcludeAlias = exclude_alias; 
            FilterOnCycle = filter_on_cycle;
            if (include_alias != null && exclude_alias != null)
                throw new ArgumentException("non è possibile aggiungere alias di inclusione ed esclusione allo stesso momento");
        }
        public bool Filter(IConditionViewModel condition)
        {
            if (IncludeAlias.HasValue)
                return IncludeAlias.Value.Contains(condition.ConditionName);
            if (ExcludeAlias.HasValue)
                return !ExcludeAlias.Value.Contains(condition.ConditionName);
            return true;
        }
    }
    public class CycleConditionAttribute : FilterConditionAttribute
    {
        public CycleConditionAttribute(string name = default, bool filter_on_cycle = true, string[] include_alias = default, string[] exclude_alias = default)
            : base(name, filter_on_cycle, include_alias, exclude_alias)
        { 
        }
    }
    public class PrintConditionAttribute : FilterConditionAttribute
    {
        public PrintConditionAttribute(string name = default, bool filter_on_cycle = true, string[] include_alias = default, string[] exclude_alias = default)
             : base(name, filter_on_cycle, include_alias, exclude_alias)
        {
        }
    }
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
        public ConditionStateCreator StateCreator { get; }

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

        [PrintCondition]
        [CycleCondition]
        public Optional<IConditionViewModel> ClampClosedCondition
        {
            get 
            {
                if (!_ClampClosedCondition.HasValue)
                {
                    var clamp_open = OptionalObservable.CombineLatestOptionalOr(
                        Flux.ConnectionProvider.ObserveVariable(m => m.TOOL_CUR).ToOptional(),
                        Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_HEAD_CLAMP),
                        (tool_cur, open) => (tool_cur, open), () => (tool_cur: -1, open: false));

                    var is_idle = Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS)
                        .Convert(data => data == FLUX_ProcessStatus.IDLE)
                        .DistinctUntilChanged()
                        .ValueOr(() => false);

                    _ClampClosedCondition = ConditionViewModel.Create(this, "clampClosed", clamp_open,
                        (state, value) =>
                        {
                            var toggle_clamp = state.Create("clamp", c => c.OPEN_HEAD_CLAMP, is_idle);
                            if (value.open)
                                return state.Create(EConditionState.Error, "CHIUDERE LA PINZA", toggle_clamp);
                            return state.Create(EConditionState.Stable, "PINZA CHIUSA", toggle_clamp);
                        });
                }
                return _ClampClosedCondition;
            }
        }
        private Optional<IConditionViewModel> _ClampClosedCondition;

        [PrintCondition]
        [StatusBarCondition]
        [CycleCondition(exclude_alias: new[] { "spools.lock" })]
        [PreparePrintCondition(exclude_alias: new[] { "spools.lock" })]
        [ManualCalibrationCondition(exclude_alias: new[] { "spools.lock" })]
        [FilamentOperationCondition(exclude_alias: new[] { "spools.lock" })]
        public SourceCache<IConditionViewModel, string> LockClosedConditions
        {
            get
            {
                if (_LockClosedConditions == default)
                {
                    _LockClosedConditions = new SourceCache<IConditionViewModel, string>(c => c.ConditionName);

                    var is_idle = Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS)
                       .Convert(data => data == FLUX_ProcessStatus.IDLE)
                       .DistinctUntilChanged()
                       .ValueOr(() => false);

                    var lock_units = Flux.ConnectionProvider.GetArrayUnits(c => c.LOCK_CLOSED);
                    foreach (var lock_unit in lock_units)
                    {
                        var current_lock = OptionalObservable.CombineLatestOptionalOr(
                            Flux.ConnectionProvider.ObserveVariable(m => m.LOCK_CLOSED, lock_unit),
                            Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, lock_unit),
                            is_idle.Select(i => i.ToOptional()).ToOptional(),
                            (closed, open, is_idle) => (closed, open, is_idle), () => default);

                        var lock_closed = ConditionViewModel.Create(this, lock_unit.Alias, current_lock,
                            (state, value) =>
                            {
                                var toggle_lock = state.Create("lock", c => c.OPEN_LOCK, lock_unit, is_idle);

                                if (!value.closed || value.open)
                                { 
                                    if(!value.is_idle)
                                        return state.Create(EConditionState.Error, $"{lock_unit} APERTA DURANTE LA LAVORAZIONE");

                                    return state.Create(EConditionState.Warning, $"CHIUDERE {lock_unit}", toggle_lock);
                                }

                                return state.Create(EConditionState.Stable, $"{lock_unit} CHIUSO", toggle_lock);
                            });

                        if (lock_closed.HasValue)
                            _LockClosedConditions.AddOrUpdate(lock_closed.Value);
                    }
                }
                return _LockClosedConditions;
            }
        }
        private SourceCache<IConditionViewModel, string> _LockClosedConditions;

        [StatusBarCondition]
        public SourceCache<IConditionViewModel, string> ChamberConditions
        {
            get 
            {
                if (_ChamberConditions == default)
                {
                    _ChamberConditions = new SourceCache<IConditionViewModel, string>(c => c.ConditionName);
                    var chamber_units = Flux.ConnectionProvider.GetArrayUnits(c => c.TEMP_CHAMBER);
                    foreach (var chamber_unit in chamber_units)
                    {
                        var parent = chamber_unit.Alias.Alias.Split(new[] { "." },
                            StringSplitOptions.RemoveEmptyEntries)
                            .FirstOrOptional(s => !string.IsNullOrEmpty(s));

                        if (!parent.HasValue)
                            return _ChamberConditions;

                        var temp_unit = Flux.ConnectionProvider.GetArrayUnit(m => m.TEMP_CHAMBER, $"{parent}.chamber");
                        var closed_unit = Flux.ConnectionProvider.GetArrayUnit(m => m.LOCK_CLOSED, $"{parent}.lock");
                        var open_unit = Flux.ConnectionProvider.GetArrayUnit(m => m.OPEN_LOCK, $"{parent}.lock");

                        var current_chamber = OptionalObservable.CombineLatestOptionalOr(
                            Flux.ConnectionProvider.ObserveVariable(m => m.TEMP_CHAMBER, temp_unit),
                            Flux.ConnectionProvider.ObserveVariable(m => m.LOCK_CLOSED, closed_unit),
                            Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, open_unit),
                            (temperature, closed, open) => (temperature, closed, open), () => (default, true, false));

                        var chamber = ConditionViewModel.Create(this, chamber_unit.Alias, current_chamber,
                            (state, value) =>
                            {
                                if (value.temperature.IsDisconnected)
                                    return state.Create(EConditionState.Error, "SENSORE DELLA TEMPERATURA NON TROVATO");

                                if (value.temperature.IsHot || value.temperature.IsOn.ValueOr(() => false))
                                {
                                    if (value.open || !value.closed)
                                        return state.Create(EConditionState.Warning, $"{chamber_unit} ACCESA, FARE ATTENZIONE");
                                    else
                                        return state.Create(EConditionState.Stable, $"{chamber_unit} ACCESA");
                                }

                                return state.Create(EConditionState.Disabled, $"{chamber_unit} SPENTA");

                            }, TimeSpan.FromSeconds(1));

                        if (chamber.HasValue)
                            _ChamberConditions.AddOrUpdate(chamber.Value);
                    }
                }
                return _ChamberConditions;
            }
        }
        private SourceCache<IConditionViewModel, string> _ChamberConditions;

        [StatusBarCondition]
        public SourceCache<IConditionViewModel, string> PlateConditions
        {
            get
            {
                if (_PlateConditions == default)
                {
                    _PlateConditions = new SourceCache<IConditionViewModel, string>(c => c.ConditionName);
                    var plate_units = Flux.ConnectionProvider.GetArrayUnits(c => c.TEMP_PLATE);
                    foreach (var plate_unit in plate_units)
                    {
                        var parent = plate_unit.Alias.Alias.Split(new[] { "." },
                           StringSplitOptions.RemoveEmptyEntries)
                           .FirstOrOptional(s => !string.IsNullOrEmpty(s));

                        if (!parent.HasValue)
                            return _ChamberConditions;

                        var temp_unit = Flux.ConnectionProvider.GetArrayUnit(m => m.TEMP_PLATE, $"{parent}.plate");
                        var closed_unit = Flux.ConnectionProvider.GetArrayUnit(m => m.LOCK_CLOSED, $"{parent}.lock");
                        var open_unit = Flux.ConnectionProvider.GetArrayUnit(m => m.OPEN_LOCK, $"{parent}.lock");

                        var current_plate = OptionalObservable.CombineLatestOptionalOr(
                            Flux.ConnectionProvider.ObserveVariable(m => m.TEMP_PLATE, temp_unit),
                            Flux.ConnectionProvider.ObserveVariable(m => m.LOCK_CLOSED, closed_unit),
                            Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, open_unit),
                            (temperature, closed, open) => (temperature, closed, open), () => (default, true, false));

                        var plate = ConditionViewModel.Create(this, plate_unit.Alias, current_plate,
                            (state, value) =>
                            {
                                if (value.temperature.IsDisconnected)
                                    return state.Create(EConditionState.Error, "SENSORE DELLA TEMPERATURA NON TROVATO");

                                if (value.temperature.IsHot || value.temperature.IsOn.ValueOr(() => false))
                                { 
                                    if (value.open || !value.closed)
                                        return state.Create(EConditionState.Warning, $"{plate_unit} ACCESA, FARE ATTENZIONE");
                                    else
                                        return state.Create(EConditionState.Stable, $"{plate_unit} ACCESA");
                                }
          
                                return state.Create(EConditionState.Disabled, $"{plate_unit} SPENTA");

                            }, TimeSpan.FromSeconds(1));

                        if (plate.HasValue)
                            _PlateConditions.AddOrUpdate(plate.Value);
                    }
                }
                return _PlateConditions;
            }
        }
        private SourceCache<IConditionViewModel, string> _PlateConditions;

        [PrintCondition]
        [CycleCondition]
        [StatusBarCondition]
        public Optional<IConditionViewModel> PressureCondition
        {
            get 
            {
                if (!_PressureCondition.HasValue)
                {
                    var pressure_in = OptionalObservable.CombineLatestOptionalOr(
                       Flux.ConnectionProvider.ObserveVariable(m => m.PRESSURE_PRESENCE),
                       Flux.ConnectionProvider.ObserveVariable(m => m.PRESSURE_LEVEL),
                       (pressure, level) => (pressure, level), () => (new Pressure(0), 0));

                    _PressureCondition = ConditionViewModel.Create(this, "pressure", pressure_in,
                        (state, value) =>
                        {
                            if (value.pressure.Kpa < value.level)
                                return (EConditionState.Error, "ATTIVARE L'ARIA COMPRESSA");
                            return (EConditionState.Stable, "ARIA COMPRESSA ATTIVA");
                        }, TimeSpan.FromSeconds(1));
                }
                return _PressureCondition;
            }
        }
        private Optional<IConditionViewModel> _PressureCondition;

        [PrintCondition]
        [StatusBarCondition]
        [PreparePrintCondition]
        [ManualCalibrationCondition]
        public Optional<IConditionViewModel> VacuumCondition
        {
            get
            {
                if (!_VacuumCondition.HasValue)
                {
                    var vacuum = OptionalObservable.CombineLatestOptionalOr(
                        Flux.ConnectionProvider.ObserveVariable(m => m.VACUUM_PRESENCE),
                        Flux.ConnectionProvider.ObserveVariable(m => m.VACUUM_LEVEL),
                        Flux.ConnectionProvider.ObserveVariable(m => m.ENABLE_VACUUM),
                        (pressure, level, enable) => (pressure, level, enable), () => (new Pressure(0), 0, false));

                    var is_idle = Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS)
                        .Convert(data => data == FLUX_ProcessStatus.IDLE)
                        .DistinctUntilChanged()
                        .ValueOr(() => false);

                    _VacuumCondition = ConditionViewModel.Create(this, "vacuum", vacuum,
                        (state, value) =>
                        {
                            var toggle_vacuum = state.Create("vacuum", c => c.ENABLE_VACUUM, is_idle);
                            if (!value.enable)
                                return state.Create(EConditionState.Disabled, "ATTIVA LA POMPA A VUOTO", toggle_vacuum);
                            if (value.pressure.Kpa > value.level)
                                return state.Create(EConditionState.Warning, "INSERIRE UN FOGLIO", toggle_vacuum);
                            return state.Create(EConditionState.Stable, "FOGLIO INSERITO", toggle_vacuum);
                        }, TimeSpan.FromSeconds(1));
                }
                return _VacuumCondition;
            }
        }
        private Optional<IConditionViewModel> _VacuumCondition;

        [PrintCondition]
        [CycleCondition]
        public Optional<IConditionViewModel> NotInChange
        {
            get
            {
                if (!_NotInChange.HasValue)
                {
                    var not_in_change = Flux.ConnectionProvider
                        .ObserveVariable(m => m.IN_CHANGE)
                        .ValueOr(() => false);

                    _NotInChange = ConditionViewModel.Create(this, "notInChange", not_in_change,
                        (state, value) =>
                        {
                            if (value)
                                return state.Create(EConditionState.Error, "STAMPANTE IN CHANGE");
                            return state.Create(EConditionState.Stable, "STAMPANTE NON IN CHANGE");
                        });
                }
                return _NotInChange;
            }
        }
        private Optional<IConditionViewModel> _NotInChange;

        public Optional<IConditionViewModel> ClampOpen
        {
            get
            {
                if (!_ClampOpen.HasValue)
                {
                    var clamp_open = OptionalObservable.CombineLatestOptionalOr(
                         Flux.ConnectionProvider.ObserveVariable(m => m.TOOL_CUR).ToOptional(),
                         Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_HEAD_CLAMP),
                         (tool_cur, open) => (tool_cur, open), () => (tool_cur: -1, open: false));

                    var is_idle = Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS)
                        .Convert(data => data == FLUX_ProcessStatus.IDLE)
                        .DistinctUntilChanged()
                        .ValueOr(() => false);

                    _ClampOpen = ConditionViewModel.Create(this, "clampOpen", clamp_open,
                        (state, value) =>
                        {
                            var toggle_clamp = state.Create("clamp", c => c.OPEN_HEAD_CLAMP, is_idle);
                            if (!value.open)
                                return state.Create(EConditionState.Error, "APRIRE LA PINZA", toggle_clamp);
                            return state.Create(EConditionState.Stable, "PINZA APERTA", toggle_clamp);
                        });
                }
                return _ClampOpen;
            }
        }
        private Optional<IConditionViewModel> _ClampOpen;

        [ManualCalibrationCondition]
        public Optional<IConditionViewModel> HasZBedHeight
        {
            get
            {
                if (!_HasZBedHeight.HasValue)
                {
                    var z_bed_height = Flux.ConnectionProvider.ObserveVariable(m => m.Z_BED_HEIGHT)
                        .ValueOr(() => FluxViewModel.MaxZBedHeight);

                    _HasZBedHeight = ConditionViewModel.Create(this, "hasZBedHeight", z_bed_height,
                        (state, value) =>
                        {
                            if (value >= FluxViewModel.MaxZBedHeight)
                            {
                                var can_probe_plate = this.WhenAnyValue(v => v.StatusEvaluation)
                                    .Select(s => s.CanSafePrint);
                                var probe_plate = state.Create("plate", c => c.ProbePlateAsync(), can_probe_plate);
                                return state.Create(EConditionState.Warning, "TASTA IL PIATTO", probe_plate);
                            }
                            return state.Create(EConditionState.Stable, "PIATTO TASTATO");
                        });
                }
                return _HasZBedHeight;
            }
        }
        private Optional<IConditionViewModel> _HasZBedHeight;

        public SourceCache<IConditionViewModel, string> LockOpenCondition
        {
            get
            {
                if (_LockOpenCondition == default)
                {
                    _LockOpenCondition = new SourceCache<IConditionViewModel, string>(c => c.ConditionName);

                    var is_idle = Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS)
                       .Convert(data => data == FLUX_ProcessStatus.IDLE)
                       .DistinctUntilChanged()
                       .ValueOr(() => false);

                    var lock_units = Flux.ConnectionProvider.GetArrayUnits(c => c.LOCK_CLOSED);
                    foreach (var lock_unit in lock_units)
                    {
                        var current_lock = OptionalObservable.CombineLatestOptionalOr(
                            Flux.ConnectionProvider.ObserveVariable(m => m.LOCK_CLOSED, lock_unit),
                            Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, lock_unit),
                            (closed, open) => (closed, open), () => (closed: false, open: false));

                        var lock_open = ConditionViewModel.Create(this, lock_unit.Alias, current_lock,
                            (state, value) =>
                            {
                                var toggle_lock = state.Create("lock", c => c.OPEN_LOCK, lock_unit, is_idle);
                                if (value.closed)
                                    return state.Create(EConditionState.Error, $"APRIRE {lock_unit}", toggle_lock);
                                return state.Create(EConditionState.Stable, $"{lock_unit} APERTO", toggle_lock);
                            });

                        if (lock_open.HasValue)
                            _LockOpenCondition.AddOrUpdate(lock_open.Value);
                    }
                }
                return _LockOpenCondition;
            }
        }
        private SourceCache<IConditionViewModel, string> _LockOpenCondition;

        [StatusBarCondition]
        public IConditionViewModel DebugCondition
        {
            get
            {
                if (_DebugCondition == default)
                {
                    var debug = Flux.MCodes.WhenAnyValue(s => s.OperatorUSB).ValueOrDefault();
                    _DebugCondition = ConditionViewModel.Create(this, "debug", debug,
                        (state, debug) =>
                        {
                            if (!debug.AdvancedSettings)
                                return EConditionState.Hidden;
                            return EConditionState.Stable;
                        });
                }
                return _DebugCondition;
            }
        }
        private IConditionViewModel _DebugCondition;

        [StatusBarCondition]
        public IConditionViewModel MessageCondition
        {
            get
            {
                if (_MessageCondition == default)
                {
                    var message_counter = Flux.Messages.WhenAnyValue(v => v.MessageCounter);
                    _MessageCondition = ConditionViewModel.Create(this, "message", message_counter,
                        (state, message_counter) =>
                        {
                            if (message_counter.EmergencyMessagesCount > 0)
                                return EConditionState.Error;
                            if (message_counter.ErrorMessagesCount > 0)
                                return EConditionState.Warning;
                            if (message_counter.WarningMessagesCount > 0)
                                return EConditionState.Warning;
                            if (message_counter.InfoMessagesCount > 0)
                                return EConditionState.Stable;
                            return EConditionState.Disabled;
                        });
                }
                return _MessageCondition;
            }
        }
        private IConditionViewModel _MessageCondition;

        [StatusBarCondition]
        public IConditionViewModel NetworkCondition 
        {
            get
            {
                if (_NetworkCondition == default)
                {
                    var network = Observable.CombineLatest(
                        Flux.NetProvider.WhenAnyValue(v => v.PLCNetworkConnectivity),
                        Flux.NetProvider.WhenAnyValue(v => v.InterNetworkConnectivity),
                        (plc, inter) => (plc, inter));

                    _NetworkCondition = ConditionViewModel.Create(this, "message", network,
                        (state, network) =>
                        {
                            if (!network.plc)
                                return EConditionState.Error;
                            if (!network.inter)
                                return EConditionState.Warning;
                            return EConditionState.Stable;
                        });
                }
                return _NetworkCondition;
            }
        }
        private IConditionViewModel _NetworkCondition;

        [FilamentOperationCondition(filter_on_cycle: false, include_alias: new[] { "spools.lock" })]
        public SourceCache<IConditionViewModel, string> LockToggleConditions
        {
            get
            {
                if (_LockToggleConditions == default)
                {
                    _LockToggleConditions = new SourceCache<IConditionViewModel, string>(c => c.ConditionName);

                    var is_idle = Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS)
                       .Convert(data => data == FLUX_ProcessStatus.IDLE)
                       .DistinctUntilChanged()
                       .ValueOr(() => false);

                    var lock_units = Flux.ConnectionProvider.GetArrayUnits(c => c.LOCK_CLOSED);
                    foreach (var lock_unit in lock_units)
                    {
                        var current_lock = OptionalObservable.CombineLatestOptionalOr(
                            Flux.ConnectionProvider.ObserveVariable(m => m.LOCK_CLOSED, lock_unit),
                            Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, lock_unit),
                            (closed, open) => (closed, open), () => (closed: false, open: false));

                        var lock_toggle = ConditionViewModel.Create(this, lock_unit.Alias, current_lock,
                            (state, value) =>
                            {
                                var toggle_lock = state.Create("lock", c => c.OPEN_LOCK, lock_unit, is_idle);
                                if (!value.closed || value.open)
                                    return state.Create(EConditionState.Stable, $"CHIUDI {lock_unit}", toggle_lock);
                                return state.Create(EConditionState.Stable, $"APRI {lock_unit}", toggle_lock);
                            });

                        if (lock_toggle.HasValue)
                            _LockToggleConditions.AddOrUpdate(lock_toggle.Value);
                    }
                }
                return _LockToggleConditions;
            }
        }
        private SourceCache<IConditionViewModel, string> _LockToggleConditions;

        public StatusProvider(FluxViewModel flux)
        {
            Flux = flux;
            StateCreator = new ConditionStateCreator(flux);

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

            var selected_part_program = Flux.ConnectionProvider
                .ObserveVariable(m => m.PART_PROGRAM)
                .DistinctUntilChanged()
                .StartWithDefault();

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

            var is_idle = Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS)
                .Convert(data => data == FLUX_ProcessStatus.IDLE)
                .DistinctUntilChanged()
                .ValueOr(() => false);

            var is_cycle = Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS)
                .Convert(data => data == FLUX_ProcessStatus.CYCLE)
                .DistinctUntilChanged()
                .ValueOr(() => false);

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
            
            var has_invalid_printer = Observable.CombineLatest(
                core_settings.WhenAnyValue(v => v.PrinterID),
                selected_mcode,
                (printer_id, selected_mcode) => !selected_mcode.HasValue || selected_mcode.Value.PrinterId != printer_id);
            
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

            _PrintingEvaluation = Observable.CombineLatest(
                queue_pos,
                selected_mcode,
                recovery,
                selected_part_program,
                PrintingEvaluation.Create)
                .DistinctUntilChanged()
                .ToProperty(this, v => v.PrintingEvaluation);

            var is_homed = Flux.ConnectionProvider.ObserveVariable(m => m.IS_HOMED)
                .DistinctUntilChanged()
                .ValueOr(() => false);

            var has_safe_state = Observable.CombineLatest(
                Flux.ConnectionProvider.WhenAnyValue(v => v.IsConnecting),
                Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS),
                Flux.Feeders.WhenAnyValue(f => f.HasInvalidStates),
                HasSafeState)
                .StartWith(false)
                .DistinctUntilChanged();

            var can_safe_cycle = Observable.CombineLatest(
                is_idle,
                has_safe_state,
                ObserveConditions<CycleConditionAttribute>(),
                (idle, state, safe_cycle) => idle && state && safe_cycle.All(s => s.Valid))
                .StartWith(false)
                .DistinctUntilChanged();

            var can_safe_print = Observable.CombineLatest(
                is_idle,
                has_safe_state,
                ObserveConditions<PrintConditionAttribute>(),
                (idle, state, safe_print) => idle && state && safe_print.All(s => s.Valid));

            var can_safe_stop = Observable.CombineLatest(
                has_safe_state,
                ObserveConditions<CycleConditionAttribute>(),
                (state, safe_cycle) => state && safe_cycle.All(s => s.Valid))
                .StartWith(false)
                .DistinctUntilChanged();

            var can_safe_hold = Observable.CombineLatest(
                is_cycle,
                has_safe_state,
                ObserveConditions<CycleConditionAttribute>(),
                (cycle, state, safe_cycle) => cycle && state && safe_cycle.All(s => s.Valid))
                .StartWith(false)
                .DistinctUntilChanged();

            var is_enabled_axis = Flux.ConnectionProvider.ObserveVariable(m => m.ENABLE_DRIVERS)
                .QueryWhenChanged(e =>
                {
                    if (e.Items.Any(e => !e.HasValue))
                        return Optional<bool>.None;
                    return e.Items.All(e => e.Value);
                })
                .DistinctUntilChanged()
                .ValueOr(() => false);

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

            var database = Flux.DatabaseProvider
                .WhenAnyValue(v => v.Database);

            var sample_print_progress = Observable.CombineLatest(
                is_cycle,
                selected_mcode,
                Observable.Interval(TimeSpan.FromSeconds(5)).StartWith(0),
                (_, _, _) => Unit.Default);

            var print_progress = this.WhenAnyValue(c => c.PrintProgress)
                .Sample(sample_print_progress)
                .DistinctUntilChanged();

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
                            var extrusion = await Flux.ConnectionProvider.ReadVariableAsync(c => c.EXTRUSIONS, extrusion_unit);
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
                Flux.ConnectionProvider.WhenAnyValue(c => c.IsConnecting),
                FindFluxStatus)
                .DistinctUntilChanged()
                .ToProperty(this, s => s.FluxStatus);
        }

        public ConditionDictionary<TConditionAttribute> GetConditions<TConditionAttribute>()
            where TConditionAttribute : FilterConditionAttribute
        {
            var conditions = new ConditionDictionary<TConditionAttribute>();

            var condition_properties = typeof(StatusProvider).GetProperties()
                .Where(p => p.IsDefined(typeof(TConditionAttribute), false));

            foreach (var condition_property in condition_properties)
            {
                var condition_attribute = condition_property.GetCustomAttribute<TConditionAttribute>();
                if (!condition_attribute.HasValue)
                    continue;

                var property_name = condition_attribute.Convert(c => c.Name)
                    .ValueOr(() => condition_property.GetRemoteContentName())
                    .Replace("Conditions", "")
                    .Replace("Condition", "");

                var base_condition = condition_property.GetValue(this);
                switch (base_condition)
                {
                    case IConditionViewModel condition:
                        add_condition(condition);
                        break;
                    case Optional<IConditionViewModel> optional_condition:
                        if (optional_condition.HasValue)
                            add_condition(optional_condition.Value);
                        break;
                    case SourceCache<IConditionViewModel, string> condition_cache:
                        foreach (var condition in condition_cache.Items)
                            add_condition(condition);
                        break;
                }

                void add_condition(IConditionViewModel condition)
                {
                    if (!condition_attribute.Value.Filter(condition))
                        return;
                    if (!conditions.ContainsKey(property_name))
                        conditions.Add(property_name, new List<(IConditionViewModel condition, TConditionAttribute condition_attribute)>());
                    conditions[property_name].Add((condition, condition_attribute.Value));
                }
            }

            return conditions;
        }

        public IObservable<IList<ConditionState>> ObserveConditions<TConditionAttribute>()
            where TConditionAttribute : FilterConditionAttribute
        {
            var conditions = GetConditions<TConditionAttribute>()
                .SelectMany(c => c.Value)
                .Select(c => c.condition.StateChanged);
            return Observable.CombineLatest(conditions);
        }

        private FLUX_ProcessStatus FindFluxStatus(
            IReadOnlyCollection<IFluxMessage> messages,
            Optional<FLUX_ProcessStatus> status,
            PrintingEvaluation printing_eval,
            Optional<IFluxRoutableViewModel> current_vm,
            bool is_connecting)
        {
            if (!status.HasValue)
                return FLUX_ProcessStatus.NONE;

            if (!status.HasValue)
                return FLUX_ProcessStatus.NONE;

            if (is_connecting)
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

        private bool HasSafeState(
            bool is_connecting,
            Optional<FLUX_ProcessStatus> status,
            bool has_feeder_error)
        {
            if (is_connecting)
                return false;
            if (!status.HasValue)
                return false;
            if (status.Value == FLUX_ProcessStatus.EMERG)
                return false;
            if (status.Value == FLUX_ProcessStatus.ERROR)
                return false;
            if (has_feeder_error)
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
