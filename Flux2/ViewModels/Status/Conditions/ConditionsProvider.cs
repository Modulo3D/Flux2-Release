using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class ConditionsProvider : ReactiveObjectRC<ConditionsProvider>
    {
        public FluxViewModel Flux { get; }

        [PrintCondition]
        [CycleCondition]
        [StatusBarCondition]
        public Optional<IConditionViewModel> ClampCondition
        {
            get
            {
                var clamp = Flux.ConnectionProvider.GetVariable(m => m.OPEN_HEAD_CLAMP);
                if (clamp.HasValue && !_ClampCondition.HasValue)
                { 
                    _ClampCondition = new ClampConditionViewModel(this, clamp.Value);
                    _ClampCondition.Value.Initialize();
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
                    SourceCacheRC.Create(this, v => v._LockClosedConditions, c => c.Name);
                    var locks_closed = Flux.ConnectionProvider.GetVariables(c => c.LOCK_CLOSED);
                    if (locks_closed.HasValue)
                    {
                        foreach (var lock_closed in locks_closed.Value.KeyValues)
                        {
                            var open_lock = Flux.ConnectionProvider.GetVariable(c => c.OPEN_LOCK, lock_closed.Key.Alias);
                            if (open_lock.HasValue)
                            {
                                var lock_closed_condition = new LockClosedConditionViewModel(this, lock_closed.Value, open_lock.Value);
                                lock_closed_condition.Initialize();
                                _LockClosedConditions.AddOrUpdate(lock_closed_condition);
                            }
                        }
                    }
                }
                return _LockClosedConditions;
            }
        }
        private SourceCache<IConditionViewModel, string> _LockClosedConditions;

        [FilamentOperationCondition(include_alias: new[] { "spools.lock" })]
        public SourceCache<IConditionViewModel, string> LockToggleConditions
        {
            get
            {
                if (_LockToggleConditions == default)
                {
                    SourceCacheRC.Create(this, v => v._LockToggleConditions, c => c.Name);
                    var locks_closed = Flux.ConnectionProvider.GetVariables(c => c.LOCK_CLOSED);
                    if (locks_closed.HasValue)
                    {
                        foreach (var lock_closed in locks_closed.Value.KeyValues)
                        {
                            var open_lock = Flux.ConnectionProvider.GetVariable(c => c.OPEN_LOCK, lock_closed.Key.Alias);
                            if (open_lock.HasValue)
                            {
                                var lock_closed_condition = new LockToggleConditionViewModel(this, lock_closed.Value, open_lock.Value);
                                lock_closed_condition.Initialize();
                                _LockToggleConditions.AddOrUpdate(lock_closed_condition);
                            }
                        }
                    }
                }
                return _LockToggleConditions;
            }
        }
        private SourceCache<IConditionViewModel, string> _LockToggleConditions;

        [StatusBarCondition]
        public SourceCache<IConditionViewModel, string> ChamberHotConditions
        {
            get
            {
                if (_ChamberHotConditions == default)
                {
                    SourceCacheRC.Create(this, v => v._ChamberHotConditions, c => c.Name);
                    var temp_chamber = Flux.ConnectionProvider.GetArray(c => c.TEMP_CHAMBER);
                    if (!temp_chamber.HasValue)
                        return _ChamberHotConditions;
                    foreach (var chamber in temp_chamber.Value.Variables.KeyValues)
                    {
                        var chamber_condition = new ChamberHotConditionViewModel(this, chamber.Value);
                        chamber_condition.Initialize();
                        _ChamberHotConditions.AddOrUpdate(chamber_condition);
                    }
                }
                return _ChamberHotConditions;
            }
        }
        private SourceCache<IConditionViewModel, string> _ChamberHotConditions;

        [StatusBarCondition]
        public SourceCache<IConditionViewModel, string> PlateHotConditions
        {
            get
            {
                if (_PlateHotConditions == default)
                {
                    SourceCacheRC.Create(this, v => v._PlateHotConditions, c => c.Name);
                    var temp_plate = Flux.ConnectionProvider.GetArray(c => c.TEMP_PLATE);
                    if (!temp_plate.HasValue)
                        return _PlateHotConditions;
                    foreach (var plate in temp_plate.Value.Variables.KeyValues)
                    {
                        var plate_condition = new PlateHotConditionViewModel(this, plate.Value);
                        plate_condition.Initialize();
                        _PlateHotConditions.AddOrUpdate(plate_condition);
                    }
                }
                return _PlateHotConditions;
            }
        }
        private SourceCache<IConditionViewModel, string> _PlateHotConditions;

        [ColdPrinterCondition]
        [ManualCalibrationCondition]
        public SourceCache<IConditionViewModel, string> ChamberColdConditions
        {
            get
            {
                if (_ChamberColdConditions == default)
                {
                    SourceCacheRC.Create(this, v => v._ChamberColdConditions, c => c.Name);
                    var temp_chamber = Flux.ConnectionProvider.GetArray(c => c.TEMP_CHAMBER);
                    if (!temp_chamber.HasValue)
                        return _ChamberColdConditions;
                    foreach (var chamber in temp_chamber.Value.Variables.KeyValues)
                    {
                        var chamber_condition = new ChamberColdConditionViewModel(this, chamber.Value);
                        chamber_condition.Initialize();
                        _ChamberColdConditions.AddOrUpdate(chamber_condition);
                    }
                }
                return _ChamberColdConditions;
            }
        }
        private SourceCache<IConditionViewModel, string> _ChamberColdConditions;

        [ColdPrinterCondition]
        [ManualCalibrationCondition]
        public SourceCache<IConditionViewModel, string> PlateColdConditions
        {
            get
            {
                if (_PlateColdConditions == default)
                {
                    SourceCacheRC.Create(this, v => v._PlateColdConditions, c => c.Name);
                    var temp_plate = Flux.ConnectionProvider.GetArray(c => c.TEMP_PLATE);
                    if (!temp_plate.HasValue)
                        return _PlateColdConditions;
                    foreach (var plate in temp_plate.Value.Variables.KeyValues)
                    {
                        var plate_condition = new PlateColdConditionViewModel(this, plate.Value);
                        plate_condition.Initialize();
                        _PlateColdConditions.AddOrUpdate(plate_condition);
                    }
                }
                return _PlateColdConditions;
            }
        }
        private SourceCache<IConditionViewModel, string> _PlateColdConditions;

        [PrintCondition]
        [CycleCondition]
        [StatusBarCondition]
        public Optional<IConditionViewModel> PressureCondition
        {
            get
            {
                var pressure_presence = Flux.ConnectionProvider.GetVariable(m => m.PRESSURE_PRESENCE);
                var pressure_level = Flux.ConnectionProvider.GetVariable(m => m.PRESSURE_LEVEL);
                if (pressure_presence.HasValue && pressure_level.HasValue && !_PressureCondition.HasValue)
                { 
                    _PressureCondition = new PressureConditionViewModel(this, pressure_presence.Value, pressure_level.Value);
                    _PressureCondition.Value.Initialize();
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
                var vacuum_presence = Flux.ConnectionProvider.GetVariable(m => m.VACUUM_PRESENCE);
                var enable_vacuum = Flux.ConnectionProvider.GetVariable(m => m.ENABLE_VACUUM);
                var vacuum_level = Flux.ConnectionProvider.GetVariable(m => m.VACUUM_LEVEL);
                if (vacuum_presence.HasValue && vacuum_level.HasValue && enable_vacuum.HasValue && !_VacuumCondition.HasValue)
                { 
                    _VacuumCondition = new VacuumConditionViewModel(this, vacuum_presence.Value, vacuum_level.Value, enable_vacuum.Value);
                    _VacuumCondition.Value.Initialize();
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
                var in_change = Flux.ConnectionProvider.GetVariable(m => m.IN_CHANGE);
                if (in_change.HasValue && !_NotInChange.HasValue)
                { 
                    _NotInChange = new NotInChangeConditionViewModel(this, in_change.Value);
                    _NotInChange.Value.Initialize();
                }
                return _NotInChange;
            }
        }
        private Optional<IConditionViewModel> _NotInChange;

        [ManualCalibrationCondition]
        public IConditionViewModel ProbeCondition
        {
            get
            {
                var z_plate_height = Flux.ConnectionProvider.GetVariable(m => m.Z_BED_HEIGHT);
                if (_ProbeCondition == null)
                { 
                    _ProbeCondition = new ProbeConditionViewModel(this, z_plate_height);
                    _ProbeCondition.Initialize();
                }
                return _ProbeCondition;
            }
        }
        private IConditionViewModel _ProbeCondition;

        [ManualCalibrationCondition]
        public IConditionViewModel FeelerGaugeCondition
        {
            get
            {
                if (_FeelerGaugeCondition == null)
                {
                    _FeelerGaugeCondition = new FeelerGaugeConditionViewModel(this);
                    _FeelerGaugeCondition.Initialize();
                }
                return _FeelerGaugeCondition;
            }
        }

        private IConditionViewModel _FeelerGaugeCondition;

        [StatusBarCondition]
        public IConditionViewModel DebugCondition
        {
            get
            {
                if (_DebugCondition == default)
                { 
                    _DebugCondition = new DebugConditionViewModel(this);
                    _DebugCondition.Initialize();
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
                    _MessageCondition = new MessageConditionViewModel(this);
                    _MessageCondition.Initialize();
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
                    _NetworkCondition = new NetworkConditionViewModel(this);
                    _NetworkCondition.Initialize();
                }
                return _NetworkCondition;
            }
        }
        private IConditionViewModel _NetworkCondition;

        /*[FilamentOperationCondition(filter_on_cycle: false, include_alias: new[] { "spools.lock" })]
        public SourceCache<IConditionViewModel, string> LockToggleConditions
        {
            get
            {
                if (_LockToggleConditions == default)
                {
                    SourceCacheRC.Create(this, v => v._LockToggleConditions, c => c.Name);

                    var lock_units = Flux.ConnectionProvider.GetArrayUnits(c => c.LOCK_CLOSED);
                    foreach (var lock_unit in lock_units)
                    {
                        var current_lock = OptionalObservable.CombineLatestOptionalOr(
                            Flux.ConnectionProvider.ObserveVariable(m => m.LOCK_CLOSED, lock_unit.Alias),
                            Flux.ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, lock_unit.Alias),
                            (closed, open) => (closed, open), () => (closed: false, open: false));

                        var lock_toggle = ConditionViewModel.Create(
                            "lock", $"{lock_unit.Alias}",
                            this, current_lock,
                            (state, value) =>
                            {
                                // TODO
                                if (!value.closed || value.open)
                                    return new ConditionState(EConditionState.Stable, new RemoteText($"close; {lock_unit}", true));
                                return new ConditionState(EConditionState.Stable, new RemoteText($"open; {lock_unit}", true));
                            }, state => state.Create(c => c.OPEN_LOCK, lock_unit, "lock"));

                        if (lock_toggle.HasValue)
                            _LockToggleConditions.AddOrUpdate(lock_toggle.Value);
                    }
                }
                return _LockToggleConditions;
            }
        }
        private SourceCache<IConditionViewModel, string> _LockToggleConditions;*/

        public ConditionsProvider(FluxViewModel flux)
        {
            Flux = flux;
        }


        public ConditionDictionary<TConditionAttribute> GetConditions<TConditionAttribute>()
            where TConditionAttribute : FilterConditionAttribute
        {
            var conditions = new ConditionDictionary<TConditionAttribute>();

            var condition_properties = typeof(ConditionsProvider).GetProperties()
                .Where(p => p.IsDefined(typeof(TConditionAttribute), false));

            foreach (var condition_property in condition_properties)
            {
                var condition_attribute = condition_property.GetCustomAttribute<TConditionAttribute>();
                if (!condition_attribute.HasValue)
                    continue;

                var property_name = condition_attribute.Convert(c => c.Name)
                    .ValueOr(condition_property.GetRemoteContentName)
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

        public IObservable<IChangeSet<ConditionState>> ObserveConditions<TConditionAttribute>()
            where TConditionAttribute : FilterConditionAttribute
        {
            return GetConditions<TConditionAttribute>()
                .SelectMany(c => c.Value)
                .Select(c => c.condition.StateChanged)
                .AsObservableChangeSet();
        }
    }
}
