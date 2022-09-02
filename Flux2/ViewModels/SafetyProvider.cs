using DynamicData;
using DynamicData.Kernel;
using Modulo3DDatabase;
using Modulo3DStandard;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class ConditionState : RemoteControl<ConditionState>
    {
        [RemoteOutput(false)]
        public bool Valid { get; }
        [RemoteOutput(false)]
        public string Message { get; }
        [RemoteOutput(false)]
        public string ActionImage { get; }
        [RemoteCommand]
        public Optional<ReactiveCommand<Unit, Unit>> ActionCommand { get; }
        public ConditionState(bool valid, string message) : base()
        {
            Valid = valid;
            Message = message;
        }
        public ConditionState(bool valid, string message, string action_icon, Action action, IObservable<bool> can_execute = null) : this(valid, message)
        {
            ActionImage = action_icon;
            ActionCommand = ReactiveCommand.Create(action, can_execute)
                .DisposeWith(Disposables);
        }
        public ConditionState(bool valid, string message, string action_icon, Func<Task> action, IObservable<bool> can_execute = null) : this(valid, message)
        {
            ActionImage = action_icon;
            ActionCommand = ReactiveCommand.CreateFromTask(action, can_execute)
                .DisposeWith(Disposables);
        }
    }

    public class ConditionStateCreator
    {
        public FluxViewModel Flux { get; }
        public ConditionStateCreator(FluxViewModel flux)
        {
            Flux = flux;
        }

        public ConditionState Create(bool valid, string message)
        {
            var state = new ConditionState(valid, message);
            state.InitializeRemoteView();
            return state;
        }
        public ConditionState Create(bool valid, string message, string action_icon, Action action, IObservable<bool> can_execute = null)
        {
            var state = new ConditionState(valid, message, action_icon, action, can_execute);
            state.InitializeRemoteView();
            return state;
        }
        public ConditionState Create(bool valid, string message, string action_icon, Func<Task> action, IObservable<bool> can_execute = null)
        {
            var state = new ConditionState(valid, message, action_icon, action, can_execute);
            state.InitializeRemoteView();
            return state;
        } 
        public ConditionState Create(bool valid, string message, string action_icon, Func<IFLUX_ConnectionProvider, Task<bool>> execute_paramacro, IObservable<bool> can_execute = null)
        {
            var state = new ConditionState(valid, message, action_icon, () => execute_paramacro(Flux.ConnectionProvider), can_execute);
            state.InitializeRemoteView();
            return state;
        }

        public ConditionState Create(bool valid, string message, string action_icon, Func<IFLUX_VariableStore, IFLUX_Variable<bool, bool>> get_variable, IObservable<bool> can_execute = null)
        {
            var state = new ConditionState(valid, message, action_icon, () => Flux.ConnectionProvider.ToggleVariableAsync(get_variable), can_execute);
            state.InitializeRemoteView();
            return state;
        }
        public ConditionState Create(bool valid, string message, string action_icon, Func<IFLUX_VariableStore, IFLUX_Array<bool, bool>> get_variable, VariableAlias alias, IObservable<bool> can_execute = null)
        {
            var state = new ConditionState(valid, message, action_icon, () => Flux.ConnectionProvider.ToggleVariableAsync(get_variable, alias), can_execute);
            state.InitializeRemoteView();
            return state;
        }
        public ConditionState Create<TRData, TWData>(bool valid, string message, string action_icon, Func<IFLUX_VariableStore, IFLUX_Variable<TRData, TWData>> get_variable, TWData value, IObservable<bool> can_execute = null)
        {
            var state = new ConditionState(valid, message, action_icon, () => Flux.ConnectionProvider.WriteVariableAsync(get_variable, value), can_execute);
            state.InitializeRemoteView();
            return state;
        }
        public ConditionState Create<TRData, TWData>(bool valid, string message, string action_icon, Func<IFLUX_VariableStore, IFLUX_Array<TRData, TWData>> get_variable, VariableAlias alias, TWData value, IObservable<bool> can_execute = null)
        {
            var state = new ConditionState(valid, message, action_icon, () => Flux.ConnectionProvider.WriteVariableAsync(get_variable, alias, value), can_execute);
            state.InitializeRemoteView();
            return state;
        }

        public ConditionState Create(bool valid, string message, string action_icon, Func<IFLUX_VariableStore, Optional<IFLUX_Variable<bool, bool>>> get_variable, IObservable<bool> can_execute = null)
        {
            var state = new ConditionState(valid, message, action_icon, () => Flux.ConnectionProvider.ToggleVariableAsync(get_variable), can_execute);
            state.InitializeRemoteView();
            return state;
        }
        public ConditionState Create(bool valid, string message, string action_icon, Func<IFLUX_VariableStore, Optional<IFLUX_Array<bool, bool>>> get_variable, VariableAlias alias, IObservable<bool> can_execute = null)
        {
            var state = new ConditionState(valid, message, action_icon, () => Flux.ConnectionProvider.ToggleVariableAsync(get_variable, alias), can_execute);
            state.InitializeRemoteView();
            return state;
        }
        public ConditionState Create<TRData, TWData>(bool valid, string message, string action_icon, Func<IFLUX_VariableStore, Optional<IFLUX_Variable<TRData, TWData>>> get_variable, TWData value, IObservable<bool> can_execute = null)
        {
            var state = new ConditionState(valid, message, action_icon, () => Flux.ConnectionProvider.WriteVariableAsync(get_variable, value), can_execute);
            state.InitializeRemoteView();
            return state;
        }
        public ConditionState Create<TRData, TWData>(bool valid, string message, string action_icon, Func<IFLUX_VariableStore, Optional<IFLUX_Array<TRData, TWData>>> get_variable, VariableAlias alias, TWData value, IObservable<bool> can_execute = null)
        {
            var state = new ConditionState(valid, message, action_icon, () => Flux.ConnectionProvider.WriteVariableAsync(get_variable, alias, value), can_execute);
            state.InitializeRemoteView();
            return state;
        }
    }

    public interface IConditionViewModel : IRemoteControl
    {
        public ConditionState State { get; }
        public IObservable<ConditionState> StateChanged { get; }
    }

    public class ConditionViewModel<T> : RemoteControl<ConditionViewModel<T>>, IConditionViewModel
    {
        public FluxViewModel Flux { get; }

        private ObservableAsPropertyHelper<ConditionState> _State;
        [RemoteContent(true)]
        public ConditionState State => _State.Value;

        private ObservableAsPropertyHelper<T> _Value;
        [RemoteOutput(true)]
        public T Value => _Value.Value;

        public IObservable<T> ValueChanged { get; }
        public IObservable<ConditionState> StateChanged { get; }

        public ConditionViewModel(FluxViewModel flux, string name, IObservable<T> value_changed, Func<ConditionStateCreator, T, ConditionState> get_state, Optional<TimeSpan> throttle = default) : base($"condition??{name}")
        {
            Flux = flux;

            ValueChanged = value_changed;

            _Value = ValueChanged
                .ToProperty(this, v => v.Value);

            var state_creator = new ConditionStateCreator(flux);
            
            var current_state = value_changed
                .DistinctUntilChanged()
                .Select(v => get_state(state_creator, v));
            
            if (throttle.HasValue)
                current_state = current_state
                    .Throttle(throttle.Value, RxApp.MainThreadScheduler);

            _State = current_state
                .DistinctUntilChanged()
                .StartWith(new ConditionState(false, ""))
                .ToProperty(this, e => e.State);

            StateChanged = this.WhenAnyValue(v => v.State);
        }
    }

    public class ConditionViewModel
    {
        public static ConditionViewModel<TIn> Create<TIn>(
            FluxViewModel flux,
            string name,
            IObservable<TIn> value_changed,
            Func<ConditionStateCreator, TIn, ConditionState> get_state,
            Optional<TimeSpan> throttle = default)
            => new ConditionViewModel<TIn>(flux, name, value_changed, get_state, throttle);
        public static Optional<ConditionViewModel<TIn>> Create<TIn>(
            FluxViewModel flux,
            string name,
            OptionalObservable<TIn> value_changed,
            Func<ConditionStateCreator, TIn, ConditionState> get_state,
            Optional<TimeSpan> throttle = default)
            => value_changed.Convert(v => new ConditionViewModel<TIn>(flux, name, v, get_state, throttle));
    }
}