﻿using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public interface IConditionViewModel : IRemoteControl
    {
        public ConditionState State { get; }
        public IObservable<ConditionState> StateChanged { get; }

        void Initialize();
    }

    [RemoteControl(baseClass: typeof(ConditionViewModel<>))]
    public abstract class ConditionViewModel<TConditionViewModel> : RemoteControl<TConditionViewModel>, IConditionViewModel
        where TConditionViewModel : ConditionViewModel<TConditionViewModel>
    {
        private ObservableAsPropertyHelper<ConditionState> _State;
        [RemoteOutput(true)]
        public ConditionState State => _State.Value;

        [RemoteCommand]
        public Optional<ReactiveCommand<Unit, Unit>> ActionCommand { get; private set; }

        public IObservable<ConditionState> StateChanged { get; private set; }

        public ConditionsProvider Conditions { get; }

        public ConditionViewModel(ConditionsProvider conditions, string name) : base(name)
        {
            Conditions = conditions;
        }

        public void Initialize()
        { 
            var is_idle = Conditions.Flux.ConnectionProvider
                .ObserveVariable(m => m.PROCESS_STATUS)
                .Convert(data => data == FLUX_ProcessStatus.IDLE)
                .ValueOr(() => false);

            ActionCommand = GetExecuteAction(is_idle).Convert(e =>
                ReactiveCommandRC.CreateFromTask(e.action, (TConditionViewModel)this, e.can_execute));

            _State = GetState(is_idle)
                .StartWith(ConditionState.Default)
                .ToPropertyRC((TConditionViewModel)this, e => e.State);

            StateChanged = this.WhenAnyValue(v => v.State);
        }

        protected abstract IObservable<ConditionState> GetState(IObservable<bool> is_idle);
        protected virtual Optional<(Func<Task> action, IObservable<bool> can_execute)> GetExecuteAction(IObservable<bool> is_idle) => default;
    }

    public class LockClosedConditionViewModel : ConditionViewModel<LockClosedConditionViewModel>
    {
        public IFLUX_Variable<bool, bool> LockClosed { get; }
        public IFLUX_Variable<bool, bool> OpenLock { get; }
        public LockClosedConditionViewModel(ConditionsProvider conditions, IFLUX_Variable<bool, bool> lock_closed, IFLUX_Variable<bool, bool> open_lock)
            : base(conditions, open_lock.Unit.Alias)
        {
            OpenLock = open_lock;
            LockClosed = lock_closed;
        }

        protected override Optional<(Func<Task> action, IObservable<bool> can_execute)> GetExecuteAction(IObservable<bool> is_idle)
        {
            var toggle_variable = async () => { await Conditions.Flux.ConnectionProvider.ToggleVariableAsync(_ => OpenLock); };
            return (toggle_variable, is_idle);
        }
        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            return Observable.CombineLatest(
                LockClosed.ValueChanged.ValueOrDefault(),
                OpenLock.ValueChanged.ValueOrDefault(),
                is_idle,
                (closed, open, is_idle) =>
                {
                    if (!closed || open)
                    {
                        if (!is_idle)
                            return new ConditionState(EConditionState.Error, new RemoteText($"openDuringCycle;{Name}", true));
                        return new ConditionState(EConditionState.Warning, new RemoteText($"close;{Name}", true));
                    }
                    return new ConditionState(EConditionState.Stable, new RemoteText($"closed;{Name}", true));
                });
        }
    }
    public class ClampConditionViewModel : ConditionViewModel<ClampConditionViewModel>
    {
        public IFLUX_Variable<bool, bool> OpenClamp { get; }
        public ClampConditionViewModel(ConditionsProvider conditions, IFLUX_Variable<bool, bool> clamp) : base(conditions, clamp.Unit.Alias)
        {
            OpenClamp = clamp;
        }

        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            return Observable.CombineLatest(
                Conditions.Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS).ValueOr(() => FLUX_ProcessStatus.NONE),
                Conditions.Flux.ConnectionProvider.ObserveVariable(m => m.IN_CHANGE).ObservableOr(() => false),
                Conditions.Flux.ConnectionProvider.ObserveVariable(m => m.TOOL_CUR).ValueOr(() => ArrayIndex.FromZeroBase(0, Conditions.Flux.ConnectionProvider.VariableStoreBase)),
                OpenClamp.ValueChanged.ValueOr(() => false),
                (status, in_change, tool_cur, open) =>
                {
                    var tool_cur_zero_base = tool_cur.GetZeroBaseIndex();

                    if (in_change && status == FLUX_ProcessStatus.CYCLE)
                        return new ConditionState(EConditionState.Idle, new RemoteText("", true));

                    if (open)
                    {
                        if (tool_cur_zero_base == -1)
                            return new ConditionState(EConditionState.Stable, new RemoteText("open", true));
                        return new ConditionState(EConditionState.Error, new RemoteText("openWithTool", true));
                    }
                    else
                    {
                        if (tool_cur_zero_base > -1)
                            return new ConditionState(EConditionState.Stable, new RemoteText("closed", true));
                        return new ConditionState(EConditionState.Error, new RemoteText("closedWithoutTool", true));
                    }
                });
        }

        protected override Optional<(Func<Task> action, IObservable<bool> can_execute)> GetExecuteAction(IObservable<bool> is_idle)
        {
            var toggle_variable = async () => { await Conditions.Flux.ConnectionProvider.ToggleVariableAsync(_ => OpenClamp); };
            return (toggle_variable, is_idle);
        }
    }

    public abstract class HeaterConditionViewModel<THeaterConditionViewModel> : ConditionViewModel<THeaterConditionViewModel>
        where THeaterConditionViewModel : HeaterConditionViewModel<THeaterConditionViewModel>
    {
        IFLUX_Variable<FLUX_Temp, double> HeaterTemp { get; }
        Optional<IFLUX_Variable<bool, bool>> LockClosed { get; }
        Optional<IFLUX_Variable<bool, bool>> OpenLock { get; }
        public HeaterConditionViewModel(ConditionsProvider conditions,
            IFLUX_Variable<FLUX_Temp, double> chamber_temp) : base(conditions, chamber_temp.Unit.Alias)
        {
            HeaterTemp = chamber_temp;

            var alias = (string)chamber_temp.Unit.Alias;
            var parent = alias.Split(new[] { "." },
                StringSplitOptions.RemoveEmptyEntries)
                .FirstOrOptional(s => !string.IsNullOrEmpty(s));

            LockClosed = Conditions.Flux.ConnectionProvider.GetVariable(m => m.LOCK_CLOSED, $"{parent}.lock");
            OpenLock = Conditions.Flux.ConnectionProvider.GetVariable(m => m.OPEN_LOCK, $"{parent}.lock");
        }

        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            return Observable.CombineLatest(
                HeaterTemp.ValueChanged.ValueOrDefault(),
                LockClosed.ConvertToObservable(l => l.ValueChanged).ObservableOr(() => false),
                OpenLock.ConvertToObservable(l => l.ValueChanged).ObservableOr(() => true),
                (temperature, closed, open) =>
                {
                    if (temperature.IsDisconnected)
                        return new ConditionState(EConditionState.Error, new RemoteText($"notFound;{Name}", true));

                    if (temperature.IsHot && (open || !closed))
                        return new ConditionState(EConditionState.Warning, new RemoteText($"hot;{Name}", true));

                    if (temperature.IsOn.ValueOr(() => false))
                        return new ConditionState(EConditionState.Stable, new RemoteText($"on;{Name}", true));

                    return new ConditionState(EConditionState.Disabled, new RemoteText($"off;{Name}", true));
                });
        }
    }
    public class ChamberConditionViewModel : HeaterConditionViewModel<ChamberConditionViewModel>
    {
        public ChamberConditionViewModel(ConditionsProvider conditions,
            IFLUX_Variable<FLUX_Temp, double> chamber_temp)
            : base(conditions, chamber_temp)
        {
        }
    }
    public class PlateConditionViewModel : HeaterConditionViewModel<PlateConditionViewModel>
    {
        public PlateConditionViewModel(ConditionsProvider conditions,
            IFLUX_Variable<FLUX_Temp, double> plate_temp)
            : base(conditions, plate_temp)
        {
        }
    }
    
    public class PressureConditionViewModel : ConditionViewModel<PressureConditionViewModel>
    {
        public IFLUX_Variable<Pressure, Unit> PressurePresence { get; }
        public IFLUX_Variable<double, double> PressureLevel { get; }
        public PressureConditionViewModel(ConditionsProvider conditions,
            IFLUX_Variable<Pressure, Unit> pressure_presence,
            IFLUX_Variable<double, double> pressure_level) : base(conditions, "")
        {
            PressureLevel = pressure_level;
            PressurePresence = pressure_presence;
        }

        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            return Observable.CombineLatest(
                PressurePresence.ValueChanged.ValueOrDefault(),
                PressureLevel.ValueChanged.ValueOrDefault(),
                (pressure_presence, pressure_level) =>
                {
                    if (pressure_presence.Kpa < pressure_level)
                        return new ConditionState(EConditionState.Error, new RemoteText("belowLevel", true));
                    return new ConditionState(EConditionState.Stable, new RemoteText("aboveLevel", true));
                });
        }
    }
    public class VacuumConditionViewModel : ConditionViewModel<VacuumConditionViewModel>
    {
        public IFLUX_Variable<Pressure, Unit> VacuumPresence { get; }
        public IFLUX_Variable<double, double> VacuumLevel { get; }
        public IFLUX_Variable<bool, bool> EnableVacuum { get; }
        public VacuumConditionViewModel(ConditionsProvider conditions,
            IFLUX_Variable<Pressure, Unit> vacuum_presence,
            IFLUX_Variable<double, double> vacuum_level,
            IFLUX_Variable<bool, bool> enable_vacuum) : base(conditions, "")
        {
            VacuumLevel = vacuum_level;
            EnableVacuum = enable_vacuum;
            VacuumPresence = vacuum_presence;
        }

        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            return Observable.CombineLatest(
                VacuumPresence.ValueChanged.ValueOrDefault(),
                VacuumLevel.ValueChanged.ValueOrDefault(),
                EnableVacuum.ValueChanged.ValueOrDefault(),
                (vacuum_presence, vacuum_level, enable_vacuum) =>
                {
                    if (!enable_vacuum)
                        return new ConditionState(EConditionState.Disabled, new RemoteText("disabled", true));
                    if (vacuum_presence.Kpa > vacuum_level)
                        return new ConditionState(EConditionState.Warning, new RemoteText("aboveLevel", true));
                    return new ConditionState(EConditionState.Stable, new RemoteText("belowLevel", true));
                });
        }
        protected override Optional<(Func<Task> action, IObservable<bool> can_execute)> GetExecuteAction(IObservable<bool> is_idle)
        {
            var toggle_variable = async () => { await Conditions.Flux.ConnectionProvider.ToggleVariableAsync(_ => EnableVacuum); };
            return (toggle_variable, is_idle);
        }
    }
    
    public class NotInChangeConditionViewModel : ConditionViewModel<NotInChangeConditionViewModel>
    {
        public IFLUX_Variable<bool, bool> InChange { get; }
        public NotInChangeConditionViewModel(ConditionsProvider conditions, IFLUX_Variable<bool, bool> in_change) : base(conditions, "")
        {
            InChange = in_change;
        }
        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            return InChange.ValueChanged.ValueOr(() => false)
                .Select(change =>
                {
                    if (change)
                        return new ConditionState(EConditionState.Error, new RemoteText("inChange", true));
                    return new ConditionState(EConditionState.Stable, new RemoteText("notInChange", true));
                });
        }
    }
    
    public class HasZPlateHeightConditionViewModel : ConditionViewModel<HasZPlateHeightConditionViewModel>
    {
        public IFLUX_Variable<double, double> ZPlateHeight { get; }
        public HasZPlateHeightConditionViewModel(ConditionsProvider conditions, IFLUX_Variable<double, double> z_plate_height) : base(conditions, "")
        {
            ZPlateHeight = z_plate_height;
        }
        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            return ZPlateHeight.ValueChanged.ValueOr(() => FluxViewModel.MaxZBedHeight)
                .Select(value =>
                {
                    if (value >= FluxViewModel.MaxZBedHeight)
                        return new ConditionState(EConditionState.Warning, new RemoteText("noValue", true));
                    return new ConditionState(EConditionState.Stable, new RemoteText("hasValue", true));
                });
        }
        protected override Optional<(Func<Task> action, IObservable<bool> can_execute)> GetExecuteAction(IObservable<bool> is_idle)
        {
            var toggle_variable = async () => { await Conditions.Flux.ConnectionProvider.ProbePlateAsync(); };
            var cycle_conditions = Conditions.ObserveConditions<PrintConditionAttribute>();
            var safe_state = Conditions.Flux.ConnectionProvider.WhenAnyValue(v => v.IsConnecting).CombineLatest(
                Conditions.Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS),
                Conditions.Flux.Feeders.WhenAnyValue(f => f.HasInvalidStates),
                has_safe_state)
                .StartWith(false);
            var can_safe_cycle = Observable.CombineLatest(
                is_idle,
                safe_state,
                cycle_conditions.QueryWhenChanged(),
                (idle, state, safe_cycle) => idle && state && safe_cycle.All(s => s.Valid))
                .StartWith(false)
                .DistinctUntilChanged();
            return (toggle_variable, can_safe_cycle);

            bool has_safe_state(
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
        }
    }

    public class DebugConditionViewModel : ConditionViewModel<DebugConditionViewModel>
    {
        public DebugConditionViewModel(ConditionsProvider conditions) : base(conditions, "")
        {
        }

        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            return Conditions.Flux.MCodes.WhenAnyValue(s => s.OperatorUSB).ValueOrDefault()
                .Select(debug =>
                {
                    if (!debug.AdvancedSettings)
                        return new ConditionState(EConditionState.Hidden, new RemoteText("", true));
                    return new ConditionState(EConditionState.Stable, new RemoteText("debug", true));
                });
        }
    }

    public class MessageConditionViewModel : ConditionViewModel<MessageConditionViewModel>
    {
        public MessageConditionViewModel(ConditionsProvider conditions) : base(conditions, "")
        {
        }

        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            return Conditions.Flux.Messages.WhenAnyValue(v => v.MessageCounter)
                .Select(message_counter =>
                {
                    if (message_counter.EmergencyMessagesCount > 0)
                        return new ConditionState(EConditionState.Error, new RemoteText("emerg", true));
                    if (message_counter.ErrorMessagesCount > 0)
                        return new ConditionState(EConditionState.Warning, new RemoteText("error", true));
                    if (message_counter.WarningMessagesCount > 0)
                        return new ConditionState(EConditionState.Warning, new RemoteText("warning", true));
                    if (message_counter.InfoMessagesCount > 0)
                        return new ConditionState(EConditionState.Stable, new RemoteText("info", true));
                    return new ConditionState(EConditionState.Disabled, new RemoteText("none", true));
                });
        }
    }

    public class NetworkConditionViewModel : ConditionViewModel<NetworkConditionViewModel>
    {
        public NetworkConditionViewModel(ConditionsProvider conditions) : base(conditions, "")
        {
        }

        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            return Observable.CombineLatest(
                Conditions.Flux.NetProvider.WhenAnyValue(v => v.PLCNetworkConnectivity),
                Conditions.Flux.NetProvider.WhenAnyValue(v => v.InterNetworkConnectivity),
                (plc, inter) =>
                {
                    if (!plc)
                        return new ConditionState(EConditionState.Error, new RemoteText("disconnected", true));
                    if (!inter)
                        return new ConditionState(EConditionState.Warning, new RemoteText("noInternet", true));
                    return new ConditionState(EConditionState.Stable, new RemoteText("stable", true));
                });
        }
    }

    public class LoadFilamentConditionViewModel : ConditionViewModel<LoadFilamentConditionViewModel>
    {
        public LoadFilamentOperationViewModel LoadFilament { get; }
        public LoadFilamentConditionViewModel(LoadFilamentOperationViewModel load_filament) : base(load_filament.Flux.ConditionsProvider, "")
        {
            LoadFilament = load_filament;
        }

        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            return Observable.CombineLatest(
                LoadFilament.Material.WhenAnyValue(f => f.State),
                LoadFilament.Material.WhenAnyValue(m => m.Document),
                LoadFilament.Material.ToolMaterial.WhenAnyValue(m => m.State),
                (state, material, tool_material) =>
                {
                    if (state.Loaded)
                        return new ConditionState(EConditionState.Disabled, new RemoteText($"loaded;{material}", true));

                    if (!material.HasValue)
                        return new ConditionState(EConditionState.Disabled, new RemoteText("read", true));

                    if (!tool_material.Compatible.ValueOr(() => false))
                        return new ConditionState(EConditionState.Error, new RemoteText($"incompatible;{material}", true));

                    if (!state.Locked)
                        return new ConditionState(EConditionState.Warning, new RemoteText($"lock;{material}", true));

                    return new ConditionState(EConditionState.Stable, new RemoteText($"readyToLoad;{material}", true));
                });
        }

        protected override Optional<(Func<Task> action, IObservable<bool> can_execute)> GetExecuteAction(IObservable<bool> is_idle)
        {
            return (LoadFilament.UpdateNFCAsync, Observable.Return(true));
        }
    }

    public class UnloadFilamentConditionViewModel : ConditionViewModel<UnloadFilamentConditionViewModel>
    {
        public UnloadFilamentOperationViewModel UnloadFilament { get; }
        public UnloadFilamentConditionViewModel(UnloadFilamentOperationViewModel unload_filament) : base(unload_filament.Flux.ConditionsProvider, "")
        {
            UnloadFilament = unload_filament;
        }

        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            return Observable.CombineLatest(
                UnloadFilament.Material.WhenAnyValue(f => f.State),
                UnloadFilament.Material.WhenAnyValue(m => m.Document),
                UnloadFilament.Material.ToolMaterial.WhenAnyValue(m => m.State),
                (state, material, tool_material) =>
                {
                    if (!state.Loaded && !state.Inserted && state.Locked)
                        return new ConditionState(EConditionState.Warning, new RemoteText("unlock", true));

                    if (!state.Loaded && !state.Inserted && !state.Locked)
                        return new ConditionState(EConditionState.Disabled, new RemoteText($"unloaded;{material}", true));

                    if (!material.HasValue)
                        return new ConditionState(EConditionState.Warning, new RemoteText("read", true));

                    if (!tool_material.Compatible.ValueOr(() => false))
                        return new ConditionState(EConditionState.Error, new RemoteText($"incompatible;{material}", true));

                    return new ConditionState(EConditionState.Stable, new RemoteText($"readyToUnload;{material}", true));
                });
        }

        protected override Optional<(Func<Task> action, IObservable<bool> can_execute)> GetExecuteAction(IObservable<bool> is_idle)
        {
            return (UnloadFilament.UpdateNFCAsync, Observable.Return(true));
        }
    }
}
