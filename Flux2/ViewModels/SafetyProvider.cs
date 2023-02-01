using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public enum EConditionState
    {
        Error,
        Warning,
        Stable,
        Disabled,
        Idle,
        Hidden
    }

    public class ConditionCommand
    {
        public Func<Task> Func { get; }
        public string ActionImage { get; }
        public IObservable<bool> CanExecute { get; }
        public ConditionCommand(string action_icon, Action action, IObservable<bool> can_execute = default)
        {
            CanExecute= can_execute;
            ActionImage = action_icon;
            Func = () => { action(); return Task.CompletedTask; };
        }
        public ConditionCommand(string action_icon, Func<Task> task, IObservable<bool> can_execute = default)
        {
            ActionImage = action_icon;
            CanExecute = can_execute;
            Func = task;
        }
    }

    public class ConditionState : RemoteControl<ConditionState>
    {
        [RemoteOutput(false)]
        public string Message { get; }

        [RemoteOutput(false)]
        public EConditionState State { get; }

        [RemoteOutput(false)]
        public string StateBrush { get; }

        [RemoteOutput(false)]
        public Optional<string> ActionImage { get; }

        [RemoteCommand]
        public Optional<ReactiveCommand<Unit, Unit>> ActionCommand { get; }

        [RemoteOutput(false)]
        public bool Valid
        {
            get
            {
                return State switch
                {
                    EConditionState.Hidden => true,
                    EConditionState.Stable => true,
                    _ => false,
                };
            }
        }

        public ConditionState(EConditionState state, string message, Optional<ConditionCommand> command = default) : base()
        {
            State = state;
            Message = message;
            ActionImage = command.Convert(c => c.ActionImage);
            ActionCommand = command.Convert(c => ReactiveCommandRC.CreateFromTask(c.Func, this, c.CanExecute));

            StateBrush = state switch
            {
                EConditionState.Idle => FluxColors.Idle,
                EConditionState.Error => FluxColors.Error,
                EConditionState.Stable => FluxColors.Selected,
                EConditionState.Warning => FluxColors.Warning,
                EConditionState.Disabled => FluxColors.Inactive,
                _ => FluxColors.Error
            };
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }

    public class ConditionStateBuilder
    {
        public static ConditionState Default { get; } = new ConditionState(EConditionState.Disabled, "");
        public static Dictionary<(string file_path, int line_number), ConditionState> ConditionStates { get; } = new Dictionary<(string file_path, int line_number), ConditionState>();
        public static Dictionary<(string file_path, int line_number), ConditionCommand> ConditionCommands { get; } = new Dictionary<(string file_path, int line_number), ConditionCommand>();

        public FluxViewModel Flux { get; }
        public ConditionStateBuilder(FluxViewModel flux)
        {
            Flux = flux;
        }

        public ConditionState Create(EConditionState value, string message = "", Optional<ConditionCommand> command = default, [CallerFilePath] string file_path = "", [CallerLineNumber] int line_number = 0)
        {
            return new ConditionState(value, message, command);
        }
        public ConditionCommand Create(string action_icon, Action action, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(action_icon, action, can_execute);
        }
        public ConditionCommand Create(string action_icon, Func<Task> action, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(action_icon, action, can_execute);
        }
        public ConditionCommand Create(string action_icon, Func<IFLUX_ConnectionProvider, Task<bool>> execute_paramacro, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(action_icon, () => execute_paramacro(Flux.ConnectionProvider), can_execute);
        }
        public ConditionCommand Create(string action_icon, Func<IFLUX_VariableStore, IFLUX_Variable<bool, bool>> get_variable, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(action_icon, () => Flux.ConnectionProvider.ToggleVariableAsync(get_variable), can_execute);
        }
        public ConditionCommand Create(string action_icon, Func<IFLUX_VariableStore, IFLUX_Array<bool, bool>> get_variable, VariableAlias unit, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(action_icon, () => Flux.ConnectionProvider.ToggleVariableAsync(get_variable, unit), can_execute);
        }
        public ConditionCommand Create<TRData, TWData>(string action_icon, Func<IFLUX_VariableStore, IFLUX_Variable<TRData, TWData>> get_variable, TWData value, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(action_icon, () => Flux.ConnectionProvider.WriteVariableAsync(get_variable, value), can_execute);
        }
        public ConditionCommand Create<TRData, TWData>(string action_icon, Func<IFLUX_VariableStore, IFLUX_Array<TRData, TWData>> get_variable, VariableAlias unit, TWData value, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(action_icon, () => Flux.ConnectionProvider.WriteVariableAsync(get_variable, unit, value), can_execute);
        }

        public ConditionCommand Create(string action_icon, Func<IFLUX_VariableStore, Optional<IFLUX_Variable<bool, bool>>> get_variable, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(action_icon, () => Flux.ConnectionProvider.ToggleVariableAsync(get_variable), can_execute);
        }
        public ConditionCommand Create(string action_icon, Func<IFLUX_VariableStore, Optional<IFLUX_Array<bool, bool>>> get_variable, VariableAlias unit, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(action_icon, () => Flux.ConnectionProvider.ToggleVariableAsync(get_variable, unit), can_execute);
        }
        public ConditionCommand Create<TRData, TWData>(string action_icon, Func<IFLUX_VariableStore, Optional<IFLUX_Variable<TRData, TWData>>> get_variable, TWData value, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(action_icon, () => Flux.ConnectionProvider.WriteVariableAsync(get_variable, value), can_execute);
        }
        public ConditionCommand Create<TRData, TWData>(string action_icon, Func<IFLUX_VariableStore, Optional<IFLUX_Array<TRData, TWData>>> get_variable, VariableAlias unit, TWData value, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(action_icon, () => Flux.ConnectionProvider.WriteVariableAsync(get_variable, unit, value), can_execute);
        }
    }

    public interface IConditionViewModel : IRemoteControl
    {
        string ConditionName { get; }
        public ConditionState State { get; }
        public IObservable<ConditionState> StateChanged { get; }
    }

    public class ConditionViewModel<T> : RemoteControl<ConditionViewModel<T>>, IConditionViewModel
    {
        private readonly ObservableAsPropertyHelper<ConditionState> _State;
        [RemoteContent(true)]
        public ConditionState State => _State.Value;

        private readonly ObservableAsPropertyHelper<T> _Value;
        [RemoteOutput(true)]
        public T Value => _Value.Value;

        public IObservable<T> ValueChanged { get; }
        public IObservable<ConditionState> StateChanged { get; }

        public string ConditionName { get; }

        public ConditionViewModel(StatusProvider status_provider, string condition_name, IObservable<T> value_changed, Func<ConditionStateBuilder, T, ConditionState> get_state, TimeSpan sample = default) : base($"condition??{condition_name}")
        {
            ConditionName = condition_name;
            ValueChanged = value_changed;

            _Value = ValueChanged
                .ToPropertyRC(this, v => v.Value);

            if (sample != default)
                value_changed = value_changed.Sample(sample);

            var state_creator = status_provider.StateCreator;
            var current_state = value_changed
                .DistinctUntilChanged()
                .Select(v => get_state(state_creator, v));

            _State = current_state
                .DistinctUntilChanged()
                .StartWith(ConditionStateBuilder.Default)
                .PairWithPreviousValue()
                .Do(t => t.OldValue?.Dispose())
                .Select(t => t.NewValue)
                .ToPropertyRC(this, e => e.State);

            StateChanged = this.WhenAnyValue(v => v.State);
        }
    }

    public class ConditionViewModel
    {
        public static IConditionViewModel Create<TIn>(
            StatusProvider status_provider,
            string name,
            IObservable<TIn> value_changed,
            Func<ConditionStateBuilder, TIn, ConditionState> get_state,
            TimeSpan sample = default)
            => new ConditionViewModel<TIn>(status_provider, name, value_changed, get_state, sample);
        public static Optional<IConditionViewModel> Create<TIn>(
            StatusProvider status_provider,
            string name,
            OptionalObservable<TIn> value_changed,
            Func<ConditionStateBuilder, TIn, ConditionState> get_state,
            TimeSpan sample = default)
            => value_changed.Convert(v => (IConditionViewModel)new ConditionViewModel<TIn>(status_provider, name, v, get_state, sample));
    }
}