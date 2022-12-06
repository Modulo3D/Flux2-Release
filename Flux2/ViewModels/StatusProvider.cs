using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

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

    public class StatusProvider : ReactiveObject, IFluxStatusProvider
    {
        public FluxViewModel Flux { get; }
        public ConditionStateCreator StateCreator { get; }

        private readonly ObservableAsPropertyHelper<Optional<JobQueue>> _JobQueue;
        public Optional<JobQueue> JobQueue => _JobQueue.Value;

        public IObservableCache<FeederEvaluator, ushort> FeederEvaluators { get; private set; }
        public IObservableCache<Optional<DocumentQueue<Material>>, ushort> ExpectedMaterialsQueue { get; private set; }
        public IObservableCache<Optional<DocumentQueue<Nozzle>>, ushort> ExpectedNozzlesQueue { get; private set; }

        private bool _StartWithLowMaterials;
        public bool StartWithLowMaterials
        {
            get => _StartWithLowMaterials;
            set => this.RaiseAndSetIfChanged(ref _StartWithLowMaterials, value);
        }

        private ObservableAsPropertyHelper<FLUX_ProcessStatus> _FluxStatus;
        public FLUX_ProcessStatus FluxStatus => _FluxStatus.Value;

        private readonly ObservableAsPropertyHelper<PrintProgress> _PrintProgress;
        public PrintProgress PrintProgress => _PrintProgress.Value;

        private readonly ObservableAsPropertyHelper<PrintingEvaluation> _PrintingEvaluation;
        public PrintingEvaluation PrintingEvaluation => _PrintingEvaluation.Value;

        private readonly ObservableAsPropertyHelper<StatusEvaluation> _StatusEvaluation;
        public StatusEvaluation StatusEvaluation => _StatusEvaluation.Value;

        private readonly ObservableAsPropertyHelper<StartEvaluation> _StartEvaluation;
        public StartEvaluation StartEvaluation => _StartEvaluation.Value;

        [PrintCondition]
        [CycleCondition]
        [StatusBarCondition]
        public Optional<IConditionViewModel> ClampCondition
        {
            get
            {
                var variable_store = Flux.ConnectionProvider.VariableStoreBase;

                if (!_ClampCondition.HasValue)
                {
                    var clamp_open = OptionalObservable.CombineLatestOptionalOr(
                         Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS).ToOptional(),
                        Flux.ConnectionProvider.ObserveVariable(m => m.IN_CHANGE),
                        Flux.ConnectionProvider.ObserveVariable(m => m.TOOL_CUR).ToOptional(),
                        Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_HEAD_CLAMP),
                        (status, in_change, tool_cur, open) => (status, in_change, tool_cur, open),
                        () => (status: FLUX_ProcessStatus.NONE, in_change: false, tool_cur: ArrayIndex.FromZeroBase(-1, variable_store), open: false));

                    var is_idle = Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS)
                        .Convert(data => data == FLUX_ProcessStatus.IDLE)
                        .ValueOr(() => false);

                    _ClampCondition = ConditionViewModel.Create(this, "clamp", clamp_open,
                        (state, value) =>
                        {
                            var tool_cur = value.tool_cur.GetZeroBaseIndex();
                            var toggle_clamp = state.Create("clamp", c => c.OPEN_HEAD_CLAMP, is_idle);

                            if (value.in_change && value.status == FLUX_ProcessStatus.CYCLE)
                                return state.Create(EConditionState.Idle, "");

                            if (value.open)
                            {
                                if (tool_cur == -1)
                                    return state.Create(EConditionState.Stable, "PINZA OK", toggle_clamp);
                                return state.Create(EConditionState.Error, "PINZA APERTA CON UTENSILE SELEZIONATO");
                            }
                            else
                            {
                                if (tool_cur > -1)
                                    return state.Create(EConditionState.Stable, "PINZA OK", toggle_clamp);
                                return state.Create(EConditionState.Error, "PINZA CHIUSA SENZA UTENSILE SELEZIONATO");
                            }
                        });
                }
                return _ClampCondition;
            }
        }
        private Optional<IConditionViewModel> _ClampCondition;

        [PrintCondition]
        [StatusBarCondition]
        [CycleCondition(exclude_alias: new[] { "spools.lock" })]
        [PreparePrintCondition(exclude_alias: new[] { "spools.lock" })]
        [FilamentOperationCondition(exclude_alias: new[] { "spools.lock" })]
        public SourceCache<IConditionViewModel, string> LockClosedConditions
        {
            get
            {
                if (_LockClosedConditions == default)
                {
                    _LockClosedConditions = new SourceCache<IConditionViewModel, string>(c => c.ConditionName);

                    var is_idle = Flux.ConnectionProvider
                        .ObserveVariable(m => m.PROCESS_STATUS)
                       .Convert(data => data == FLUX_ProcessStatus.IDLE)
                       .ValueOr(() => false);

                    var lock_units = Flux.ConnectionProvider.GetArrayUnits(c => c.LOCK_CLOSED);
                    foreach (var lock_unit in lock_units)
                    {
                        var current_lock = OptionalObservable.CombineLatestOptionalOr(
                            Flux.ConnectionProvider.ObserveVariable(m => m.LOCK_CLOSED, lock_unit.Alias),
                            Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, lock_unit.Alias),
                            is_idle.Select(i => i.ToOptional()).ToOptional(),
                            (closed, open, is_idle) => (closed, open, is_idle), () => default);

                        var lock_closed = ConditionViewModel.Create(this, lock_unit.Alias, current_lock,
                            (state, value) =>
                            {
                                var toggle_lock = state.Create("lock", c => c.OPEN_LOCK, lock_unit, is_idle);

                                if (!value.closed || value.open)
                                {
                                    if (!value.is_idle)
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
                            continue;

                        var temp_unit = Flux.ConnectionProvider.GetArrayUnit(m => m.TEMP_CHAMBER, $"{parent}.chamber");
                        if (!temp_unit.HasValue)
                            continue;

                        var closed_unit = Flux.ConnectionProvider.GetArrayUnit(m => m.LOCK_CLOSED, $"{parent}.lock");
                        var open_unit = Flux.ConnectionProvider.GetArrayUnit(m => m.OPEN_LOCK, $"{parent}.lock");

                        var current_chamber = Observable.CombineLatest(
                            Flux.ConnectionProvider.ObserveVariable(m => m.TEMP_CHAMBER, temp_unit),
                            Flux.ConnectionProvider.ObserveVariable(m => m.LOCK_CLOSED, closed_unit),
                            Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, open_unit),
                            (temperature, closed, open) => (temperature, closed, open));

                        var chamber = ConditionViewModel.Create(this, chamber_unit.Alias, current_chamber,
                            (state, value) =>
                            {
                                var temperature = value.temperature.ValueOrDefault();
                                var closed = value.closed.ValueOr(() => false);
                                var open = value.open.ValueOr(() => true);

                                if (temperature.IsDisconnected)
                                    return state.Create(EConditionState.Error, "SENSORE DELLA TEMPERATURA NON TROVATO");

                                if (temperature.IsHot && (open || !closed))
                                    return state.Create(EConditionState.Warning, $"{chamber_unit} ACCESA, FARE ATTENZIONE");

                                if (temperature.IsOn.ValueOr(() => false))
                                    return state.Create(EConditionState.Stable, $"{chamber_unit} ACCESA");

                                return state.Create(EConditionState.Disabled, $"{chamber_unit} SPENTA");

                            }, TimeSpan.FromSeconds(1));

                        _ChamberConditions.AddOrUpdate(chamber);
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
                            continue;

                        var temp_unit = Flux.ConnectionProvider.GetArrayUnit(m => m.TEMP_PLATE, $"{parent}.plate");
                        if (!temp_unit.HasValue)
                            continue;

                        var closed_unit = Flux.ConnectionProvider.GetArrayUnit(m => m.LOCK_CLOSED, $"{parent}.lock");
                        var open_unit = Flux.ConnectionProvider.GetArrayUnit(m => m.OPEN_LOCK, $"{parent}.lock");

                        var current_plate = Observable.CombineLatest(
                            Flux.ConnectionProvider.ObserveVariable(m => m.TEMP_PLATE, temp_unit),
                            Flux.ConnectionProvider.ObserveVariable(m => m.LOCK_CLOSED, closed_unit),
                            Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, open_unit),
                            (temperature, closed, open) => (temperature, closed, open));

                        var plate = ConditionViewModel.Create(this, plate_unit.Alias, current_plate,
                            (state, value) =>
                            {
                                var temperature = value.temperature.ValueOrDefault();
                                var closed = value.closed.ValueOr(() => false);
                                var open = value.open.ValueOr(() => true);

                                if (temperature.IsDisconnected)
                                    return state.Create(EConditionState.Error, "SENSORE DELLA TEMPERATURA NON TROVATO");

                                if (temperature.IsHot && (open || !closed))
                                    return state.Create(EConditionState.Warning, $"{plate_unit} ACCESA, FARE ATTENZIONE");

                                if (temperature.IsOn.ValueOr(() => false))
                                    return state.Create(EConditionState.Stable, $"{plate_unit} ACCESA");

                                return state.Create(EConditionState.Disabled, $"{plate_unit} SPENTA");

                            }, TimeSpan.FromSeconds(1));

                        _PlateConditions.AddOrUpdate(plate);
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

                    var is_idle = Flux.ConnectionProvider
                        .ObserveVariable(m => m.PROCESS_STATUS)
                        .Convert(data => data == FLUX_ProcessStatus.IDLE)
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

        [ManualCalibrationCondition]
        public Optional<IConditionViewModel> HasZBedHeight
        {
            get
            {
                if (!_HasZBedHeight.HasValue)
                {
                    var z_bed_height = Flux.ConnectionProvider
                        .ObserveVariable(m => m.Z_BED_HEIGHT)
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

                    var is_idle = Flux.ConnectionProvider
                        .ObserveVariable(m => m.PROCESS_STATUS)
                       .Convert(data => data == FLUX_ProcessStatus.IDLE)
                       .ValueOr(() => false);

                    var lock_units = Flux.ConnectionProvider.GetArrayUnits(c => c.LOCK_CLOSED);
                    foreach (var lock_unit in lock_units)
                    {
                        var current_lock = OptionalObservable.CombineLatestOptionalOr(
                            Flux.ConnectionProvider.ObserveVariable(m => m.LOCK_CLOSED, lock_unit.Alias),
                            Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, lock_unit.Alias),
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

                    var lock_units = Flux.ConnectionProvider.GetArrayUnits(c => c.LOCK_CLOSED);
                    foreach (var lock_unit in lock_units)
                    {
                        var current_lock = OptionalObservable.CombineLatestOptionalOr(
                            Flux.ConnectionProvider.ObserveVariable(m => m.LOCK_CLOSED, lock_unit.Alias),
                            Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, lock_unit.Alias),
                            (closed, open) => (closed, open), () => (closed: false, open: false));

                        var lock_toggle = ConditionViewModel.Create(this, lock_unit.Alias, current_lock,
                            (state, value) =>
                            {
                                // TODO
                                var toggle_lock = state.Create("lock", c => c.OPEN_LOCK, lock_unit);
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
                .AsObservableChangeSet(e => e.Feeder.Position)
                .AsObservableCache();

            ExpectedMaterialsQueue = FeederEvaluators.Connect()
                .Transform(f => f.Material, true)
                .AutoTransform(f => f.ExpectedDocumentQueue)
                .AsObservableCache();

            ExpectedNozzlesQueue = FeederEvaluators.Connect()
                .Transform(f => f.ToolNozzle, true)
                .AutoTransform(f => f.ExpectedDocumentQueue)
                .AsObservableCache();

            var core_settings = Flux.SettingsProvider.CoreSettings.Local;

            var queue_pos = Flux.ConnectionProvider
                .ObserveVariable(m => m.QUEUE_POS)
                .StartWithDefault();

            Flux.ConnectionProvider
               .ObserveVariable(m => m.JOB_QUEUE)
               .Subscribe(q => Console.WriteLine(q));

            _JobQueue = Flux.ConnectionProvider
                .ObserveVariable(m => m.JOB_QUEUE)
                .ConvertMany(preview => Observable.FromAsync(() => get_queue(preview)))
                .StartWithDefault()
                .ToProperty(this, v => v.JobQueue);

            Task<Optional<JobQueue>> get_queue(JobQueuePreview preview)
            {
                using var queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                return preview.GetJobQueueAsync(Flux.ConnectionProvider, queue_cts.Token);
            }

            var job_partprograms = Observable.CombineLatest(
                queue_pos, this.WhenAnyValue(s => s.JobQueue), (queue_pos, queue) =>
                {
                    if (!queue.HasValue)
                        return default;
                    if (!queue_pos.HasValue)
                        return default;
                    return queue.Value.Lookup(queue_pos.Value);
                });

            var current_job = job_partprograms
                .Convert(j => j.Job);

            var current_partprogram = job_partprograms
                .Convert(j => j.GetCurrentPartProgram())
                .ConvertMany(preview => Observable.FromAsync(() => get_partprogram(preview)))
                .StartWithDefault();

            Task<Optional<MCodePartProgram>> get_partprogram(MCodePartProgramPreview preview)
            {
                using var queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                return preview.GetMCodePartProgramAsync(Flux.ConnectionProvider, queue_cts.Token);
            }

            var current_mcode_key = current_partprogram
                .Convert(j => j.MCodeKey);

            var current_mcode = Flux.MCodes.AvaiableMCodes.Connect()
                .AutoRefresh(m => m.Analyzer)
                .WatchOptional(current_mcode_key)
                .Convert(m => m.Analyzer)
                .Convert(a => a.MCode)
                .StartWithDefault()
                .DistinctUntilChanged();

            var is_idle = Flux.ConnectionProvider
                .ObserveVariable(m => m.PROCESS_STATUS)
                .Convert(data => data == FLUX_ProcessStatus.IDLE)
                .ValueOr(() => false);

            var is_cycle = Flux.ConnectionProvider
                .ObserveVariable(m => m.PROCESS_STATUS)
                .Convert(data => data == FLUX_ProcessStatus.CYCLE)
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
                current_mcode,
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

            _PrintingEvaluation = Observable.CombineLatest(
                current_job,
                current_mcode,
                current_partprogram,
                PrintingEvaluation.Create)
                .DistinctUntilChanged()
                .ToProperty(this, v => v.PrintingEvaluation);

            var is_homed = Flux.ConnectionProvider
                .ObserveVariable(m => m.IS_HOMED)
                .ValueOr(() => false);

            var has_safe_state = Observable.CombineLatest(
                Flux.ConnectionProvider.WhenAnyValue(v => v.IsConnecting),
                Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS),
                Flux.Feeders.WhenAnyValue(f => f.HasInvalidStates),
                HasSafeState)
                .StartWith(false);

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

            var is_enabled_axis = Flux.ConnectionProvider
                .ObserveVariable(m => m.ENABLE_DRIVERS)
                .QueryWhenChanged(e =>
                {
                    if (e.Items.Any(e => !e.HasValue))
                        return Optional<bool>.None;
                    return e.Items.All(e => e.Value);
                })
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
                current_mcode,
                Observable.Interval(TimeSpan.FromSeconds(5)).StartWith(0),
                (_, _, _) => Unit.Default);

            var print_progress = this.WhenAnyValue(c => c.PrintProgress)
                .Sample(sample_print_progress)
                .DistinctUntilChanged();
        }

        public void Initialize()
        {
            // Status with messages
            var messages = Flux.Messages
                .WhenAnyValue(m => m.MessageCounter);

            _FluxStatus = Observable.CombineLatest(
                messages,
                Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS),
                this.WhenAnyValue(v => v.PrintingEvaluation),
                Flux.Navigator.WhenAnyValue(nav => nav.CurrentViewModel),
                Flux.ConnectionProvider.WhenAnyValue(c => c.IsConnecting),
                FindFluxStatus)
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
            MessageCounter messages,
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

            if (messages.EmergencyMessagesCount > 0)
                return FLUX_ProcessStatus.EMERG;

            if (messages.ErrorMessagesCount > 0)
                return FLUX_ProcessStatus.ERROR;

            switch (status.Value)
            {
                case FLUX_ProcessStatus.IDLE:
                    if (current_vm.HasValue && current_vm.Value is IOperationViewModel)
                        return FLUX_ProcessStatus.WAIT;
                    if (printing_eval.HasRecovery)
                        return FLUX_ProcessStatus.WAIT;
                    if (printing_eval.CurrentMCode.HasValue)
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
            var current_mcode = evaluation.CurrentMCode;
            if (!current_mcode.HasValue)
                return new PrintProgress(0, TimeSpan.Zero);

            var duration = current_mcode.Value.Duration;

            var current_job = evaluation.CurrentJob;
            if (!current_job.HasValue)
                return new PrintProgress(0, duration);

            if (!progress.HasValue)
                return new PrintProgress(0, duration);

            var percentage = progress.Value.GetPercentage(current_mcode.Value.BlockCount).ValueOr(() => 0);
            var remaining_ticks = ((double)duration.Ticks / 100) * (100 - percentage);
            var print_progress = new PrintProgress(percentage, new TimeSpan((long)remaining_ticks));

            var paramacro_name = progress.Value.Paramacro;
            if (!paramacro_name.Contains($"{current_job.Value.MCodeKey}"))
            {
                if (PrintProgress.Percentage == 0)
                    return print_progress;

                return PrintProgress;
            }

            return print_progress;
        }
        private IEnumerable<FeederEvaluator> CreateFeederEvaluator(IQuery<IFluxFeederViewModel, ushort> query)
        {
            foreach (var feeder in query.Items)
            {
                var evaluator = new FeederEvaluator(Flux, feeder);
                evaluator.Initialize();
                yield return evaluator;
            }
        }
    }
}
