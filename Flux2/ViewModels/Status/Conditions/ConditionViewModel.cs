using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.FSharp.Core.ByRefKinds;

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

        public FluxViewModel Flux { get; }
        public ConditionsProvider Conditions { get; }

        public ConditionViewModel(FluxViewModel flux, ConditionsProvider conditions, string name = "") 
            : base(flux.RemoteContext, string.IsNullOrEmpty(name) ? name : $"{typeof(TConditionViewModel).GetRemoteElementClass()};{name}")
        {
            Flux = flux;
            Conditions = conditions;
        }

        public void Initialize()
        { 
            var is_idle = Flux.ConnectionProvider
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

    [RemoteControl(baseClass: typeof(ConditionViewModel<>))]
    public abstract class InputConditionViewModel<TInputConditionViewModel, TInput> : ConditionViewModel<TInputConditionViewModel>
         where TInputConditionViewModel : InputConditionViewModel<TInputConditionViewModel, TInput>
    {
        [RemoteInput]
        public abstract TInput Input { get; set; }

        private Optional<TInput> _Value;
        public Optional<TInput> Value 
        {
            get => _Value;
            set => this.RaiseAndSetIfChanged(ref _Value, value);
        }
        protected InputConditionViewModel(FluxViewModel flux, ConditionsProvider conditions, string name = "") : base(flux, conditions, name)
        {
        }
        protected sealed override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            return GetState(is_idle, this.WhenAnyValue(v => v.Value));
        }
        protected abstract IObservable<ConditionState> GetState(IObservable<bool> is_idle, IObservable<Optional<TInput>> value);
    }

    public class FeelerGaugeConditionViewModel : InputConditionViewModel<FeelerGaugeConditionViewModel, double>
    {
        public FeelerGaugeConditionViewModel(FluxViewModel flux, ConditionsProvider conditions) : base(flux, conditions)
        {
        }

        private double _Input;
        [RemoteInput(step:0.05, min:0, max:1, converter:typeof(MillimeterConverter))]
        public override double Input
        {
            get => _Input; 
            set => this.RaiseAndSetIfChanged(ref _Input, value); 
        }

        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle, IObservable<Optional<double>> value)
        {
            return Observable.CombineLatest(is_idle, value, (i, v) => 
            {
                if(!v.HasValue)
                    return new ConditionState(EConditionState.Warning, new RemoteText($"noOffset", true));
                if(v.Value < 0.05)
                    return new ConditionState(EConditionState.Warning, new RemoteText($"noOffset", true));
                return new ConditionState(EConditionState.Stable, new RemoteText($"okOffset;{$"{v.Value:0.00}".Replace(",", ".")}mm", true));
            });
        }

        protected override Optional<(Func<Task> action, IObservable<bool> can_execute)> GetExecuteAction(IObservable<bool> is_idle)
        {
            return (() => { Value = Input; return Task.CompletedTask; }, Observable.Return(true));
        }
    }

    public class LockClosedConditionViewModel : ConditionViewModel<LockClosedConditionViewModel>
    {
        public IFLUX_Variable<bool, bool> LockClosed { get; }
        public IFLUX_Variable<bool, bool> OpenLock { get; }
        public LockClosedConditionViewModel(FluxViewModel flux, ConditionsProvider conditions, IFLUX_Variable<bool, bool> lock_closed, IFLUX_Variable<bool, bool> open_lock) 
            : base(flux, conditions, open_lock.Unit.Alias)
        {
            OpenLock = open_lock;
            LockClosed = lock_closed;
        }

        protected override Optional<(Func<Task> action, IObservable<bool> can_execute)> GetExecuteAction(IObservable<bool> is_idle)
        {
            return (() => Flux.ConnectionProvider.ToggleVariableAsync(_ => OpenLock), is_idle);
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
                            return new ConditionState(EConditionState.Warning, new RemoteText($"openDuringCycle;{OpenLock.Unit}", true));
                        return new ConditionState(EConditionState.Warning, new RemoteText($"close;{OpenLock.Unit}", true));
                    }
                    return new ConditionState(EConditionState.Stable, new RemoteText($"closed;{OpenLock.Unit}", true));
                });
        }
    }
    public class LockToggleConditionViewModel : ConditionViewModel<LockToggleConditionViewModel>
    {
        public IFLUX_Variable<bool, bool> LockClosed { get; }
        public IFLUX_Variable<bool, bool> OpenLock { get; }
        public LockToggleConditionViewModel(FluxViewModel flux, ConditionsProvider conditions, IFLUX_Variable<bool, bool> lock_closed, IFLUX_Variable<bool, bool> open_lock)
            : base(flux, conditions, open_lock.Unit.Alias)
        {
            OpenLock = open_lock;
            LockClosed = lock_closed;
        }

        protected override Optional<(Func<Task> action, IObservable<bool> can_execute)> GetExecuteAction(IObservable<bool> is_idle)
        {
            return (() => Flux.ConnectionProvider.ToggleVariableAsync(_ => OpenLock), is_idle);
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
                            return new ConditionState(EConditionState.Warning, new RemoteText($"openDuringCycle;{OpenLock.Unit}", true));
                        return new ConditionState(EConditionState.Stable, new RemoteText($"close;{OpenLock.Unit}", true));
                    }
                    return new ConditionState(EConditionState.Stable, new RemoteText($"open;{OpenLock.Unit}", true));
                });
        }
    }
    public class ClampConditionViewModel : ConditionViewModel<ClampConditionViewModel>
    {
        public IFLUX_Variable<bool, bool> OpenClamp { get; }
        public ClampConditionViewModel(FluxViewModel flux, ConditionsProvider conditions, IFLUX_Variable<bool, bool> clamp) : base(flux, conditions, clamp.Unit.Alias)
        {
            OpenClamp = clamp;
        }

        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            return Observable.CombineLatest(
                Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS).ValueOr(() => FLUX_ProcessStatus.NONE),
                Flux.ConnectionProvider.ObserveVariable(m => m.IN_CHANGE).ObservableOr(() => false),
                Flux.ConnectionProvider.ObserveVariable(m => m.TOOL_CUR).ValueOr(() => ArrayIndex.FromZeroBase(0, Flux.ConnectionProvider.VariableStoreBase)),
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
            return (() => Flux.ConnectionProvider.ToggleVariableAsync(_ => OpenClamp), is_idle);
        }
    }

    public abstract class HeaterHotConditionViewModel<THeaterConditionViewModel> : ConditionViewModel<THeaterConditionViewModel>
        where THeaterConditionViewModel : HeaterHotConditionViewModel<THeaterConditionViewModel>
    {
        IFLUX_Variable<FLUX_Temp, double> HeaterTemp { get; }
        Optional<IFLUX_Variable<bool, bool>> LockClosed { get; }
        Optional<IFLUX_Variable<bool, bool>> OpenLock { get; }
        public HeaterHotConditionViewModel(FluxViewModel flux, ConditionsProvider conditions, IFLUX_Variable<FLUX_Temp, double> chamber_temp) : base(flux, conditions, chamber_temp.Unit.Alias)
        {
            HeaterTemp = chamber_temp;

            var alias = (string)chamber_temp.Unit.Alias;
            var parent = alias.Split(new[] { "." },
                StringSplitOptions.RemoveEmptyEntries)
                .FirstOrOptional(s => !string.IsNullOrEmpty(s));

            LockClosed = Flux.ConnectionProvider.GetVariable(m => m.LOCK_CLOSED, $"{parent}.lock");
            OpenLock = Flux.ConnectionProvider.GetVariable(m => m.OPEN_LOCK, $"{parent}.lock");
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
                        return new ConditionState(EConditionState.Error, new RemoteText($"notFound;{HeaterTemp.Unit}", true));

                    if (temperature.IsHot && (open || !closed))
                        return new ConditionState(EConditionState.Warning, new RemoteText($"hot;{HeaterTemp.Unit}", true));

                    if (temperature.IsOn)
                        return new ConditionState(EConditionState.Stable, new RemoteText($"on;{HeaterTemp.Unit}", true));

                    return new ConditionState(EConditionState.Disabled, new RemoteText($"off;{HeaterTemp.Unit}", true));
                });
        }
    }
    public class ChamberHotConditionViewModel : HeaterHotConditionViewModel<ChamberHotConditionViewModel>
    {
        public ChamberHotConditionViewModel(FluxViewModel flux, ConditionsProvider conditions, IFLUX_Variable<FLUX_Temp, double> chamber_temp) : base(flux, conditions, chamber_temp)
        {
        }
    }
    public class PlateHotConditionViewModel : HeaterHotConditionViewModel<PlateHotConditionViewModel>
    {
        public PlateHotConditionViewModel(FluxViewModel flux, ConditionsProvider conditions, IFLUX_Variable<FLUX_Temp, double> plate_temp) : base(flux, conditions, plate_temp)
        {
        }
    }
    public abstract class HeaterColdConditionViewModel<THeaterConditionViewModel> : ConditionViewModel<THeaterConditionViewModel>
        where THeaterConditionViewModel : HeaterColdConditionViewModel<THeaterConditionViewModel>
    {
        IFLUX_Variable<FLUX_Temp, double> HeaterTemp { get; }
        public HeaterColdConditionViewModel(FluxViewModel flux, ConditionsProvider conditions, IFLUX_Variable<FLUX_Temp, double> chamber_temp) : base(flux, conditions, chamber_temp.Unit.Alias)
        {
            HeaterTemp = chamber_temp;
        }

        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            return HeaterTemp.ValueChanged.ValueOrDefault()
                .Select(temperature =>
                {
                    if (temperature.IsDisconnected)
                        return new ConditionState(EConditionState.Error, new RemoteText($"notFound;{HeaterTemp.Unit}", true));

                    if (temperature.IsHot)
                        return new ConditionState(EConditionState.Warning, new RemoteText($"hot;{HeaterTemp.Unit}", true));

                    if (temperature.IsOn)
                        return new ConditionState(EConditionState.Warning, new RemoteText($"on;{HeaterTemp.Unit}", true));

                    return new ConditionState(EConditionState.Stable, new RemoteText($"off;{HeaterTemp.Unit}", true));
                });
        }
        protected override Optional<(Func<Task> action, IObservable<bool> can_execute)> GetExecuteAction(IObservable<bool> is_idle)
        {
            return (() => Flux.ConnectionProvider.WriteVariableAsync(_ => HeaterTemp, 0.0), is_idle);
        }
    }
    public class ChamberColdConditionViewModel : HeaterColdConditionViewModel<ChamberColdConditionViewModel>
    {
        public ChamberColdConditionViewModel(FluxViewModel flux, ConditionsProvider conditions, IFLUX_Variable<FLUX_Temp, double> chamber_temp) : base(flux, conditions, chamber_temp)
        {
        }
    }
    public class PlateColdConditionViewModel : HeaterColdConditionViewModel<PlateColdConditionViewModel>
    {
        public PlateColdConditionViewModel(FluxViewModel flux, ConditionsProvider conditions, IFLUX_Variable<FLUX_Temp, double> plate_temp) : base(flux, conditions, plate_temp)
        {
        }
    }
    public class PressureConditionViewModel : ConditionViewModel<PressureConditionViewModel>
    {
        public IFLUX_Variable<Pressure, Unit> PressurePresence { get; }
        public IFLUX_Variable<double, double> PressureLevel { get; }
        public PressureConditionViewModel(FluxViewModel flux, ConditionsProvider conditions, IFLUX_Variable<Pressure, Unit> pressure_presence, IFLUX_Variable<double, double> pressure_level) : base(flux, conditions)
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
        public VacuumConditionViewModel(FluxViewModel flux, ConditionsProvider conditions, IFLUX_Variable<Pressure, Unit> vacuum_presence, IFLUX_Variable<double, double> vacuum_level, IFLUX_Variable<bool, bool> enable_vacuum) : base(flux, conditions)
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
            return (() => Flux.ConnectionProvider.ToggleVariableAsync(_ => EnableVacuum), is_idle);
        }
    }
    
    public class NotInChangeConditionViewModel : ConditionViewModel<NotInChangeConditionViewModel>
    {
        public IFLUX_Variable<bool, bool> InChange { get; }
        public NotInChangeConditionViewModel(FluxViewModel flux, ConditionsProvider conditions, IFLUX_Variable<bool, bool> in_change) : base(flux, conditions)
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
    
    public class ProbeConditionViewModel : ConditionViewModel<ProbeConditionViewModel>
    {
        private bool _IsHomed;
        public bool IsProbed 
        {
            get => _IsHomed;
            set => this.RaiseAndSetIfChanged(ref _IsHomed, value);
        }
        public Optional<IFLUX_Variable<double, double>> ZPlateHeight { get; }
        public ProbeConditionViewModel(FluxViewModel flux, ConditionsProvider conditions, Optional<IFLUX_Variable<double, double>> z_plate_height) : base(flux, conditions)
        {
            ZPlateHeight = z_plate_height;
        }
        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            var cold_conditions = Conditions.ObserveConditions<ColdPrinterConditionAttribute>();
            var z_plate_height = ZPlateHeight.ConvertToObservable(z => z.ValueChanged)
                .ObservableOr(() => FluxViewModel.MaxZBedHeight);

            return Observable.CombineLatest(
                z_plate_height,
                this.WhenAnyValue(v => v.IsProbed),
                cold_conditions.QueryWhenChanged(v => v.All(v => v.Valid)),
                (z_plate_height, is_probed, cold_printer) =>
                {
                    if (!cold_printer)
                        return new ConditionState(EConditionState.Warning, new RemoteText("notCold", true));
                    if (z_plate_height >= FluxViewModel.MaxZBedHeight && !is_probed)
                        return new ConditionState(EConditionState.Warning, new RemoteText("noValue", true));
                    return new ConditionState(EConditionState.Stable, new RemoteText("hasValue", true));
                });
        }
        protected override Optional<(Func<Task> action, IObservable<bool> can_execute)> GetExecuteAction(IObservable<bool> is_idle)
        {
            var cold_conditions = Conditions.ObserveConditions<ColdPrinterConditionAttribute>();
            var print_conditions = Conditions.ObserveConditions<PrintConditionAttribute>();
            var safe_state = Flux.ConnectionProvider.WhenAnyValue(v => v.IsConnecting).CombineLatest(
                Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS),
                Flux.Feeders.WhenAnyValue(f => f.HasInvalidStates),
                has_safe_state)
                .StartWith(false);
            var can_safe_cycle = Observable.CombineLatest(
                is_idle,
                safe_state,
                cold_conditions.QueryWhenChanged(),
                print_conditions.QueryWhenChanged(),
                (idle, state, cold_printer, safe_cycle) => idle && state && safe_cycle.All(s => s.Valid) && cold_printer.All(s => s.Valid))
                .StartWith(false)
                .DistinctUntilChanged();

            var probe_plate = async () =>
            {
                if (ZPlateHeight.HasValue)
                {
                    await Flux.ConnectionProvider.ProbePlateAsync();
                }
                else
                { 
                    await Flux.ConnectionProvider.HomeAsync(false);
                    IsProbed = true;
                }
            };

            return (probe_plate, can_safe_cycle);

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
        public DebugConditionViewModel(FluxViewModel flux, ConditionsProvider conditions) : base(flux, conditions)
        {
        }

        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            return Flux.MCodes.WhenAnyValue(s => s.OperatorUSB).ValueOrDefault()
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
        public MessageConditionViewModel(FluxViewModel flux, ConditionsProvider conditions) : base(flux, conditions)
        {
        }

        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            return Flux.Messages.WhenAnyValue(v => v.MessageCounter)
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
        public NetworkConditionViewModel(FluxViewModel flux, ConditionsProvider conditions) : base(flux, conditions)
        {
        }

        protected override IObservable<ConditionState> GetState(IObservable<bool> is_idle)
        {
            return Observable.CombineLatest(
                Flux.NetProvider.WhenAnyValue(v => v.PLCNetworkConnectivity),
                Flux.NetProvider.WhenAnyValue(v => v.InterNetworkConnectivity),
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
        public LoadFilamentConditionViewModel(FluxViewModel flux, LoadFilamentOperationViewModel load_filament) : base(flux, load_filament.Flux.ConditionsProvider)
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
        public UnloadFilamentConditionViewModel(FluxViewModel flux, UnloadFilamentOperationViewModel unload_filament) : base(flux, unload_filament.Flux.ConditionsProvider)
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
