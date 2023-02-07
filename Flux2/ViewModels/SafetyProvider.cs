using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
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
        public string CommandIcon { get; }
        public IObservable<bool> CanExecute { get; }
        public ConditionCommand(Action action, string command_icon, IObservable<bool> can_execute = default)
        {
            Func = () => { action(); return Task.CompletedTask; };
            CommandIcon = command_icon;
            CanExecute = can_execute;
        }
        public ConditionCommand(Func<Task> task, string command_icon, IObservable<bool> can_execute = default)
        {
            CommandIcon = command_icon;
            CanExecute = can_execute;
            Func = task;
        }
    }

    [DataContract]
    public struct ConditionState
    {
        public static ConditionState Default { get; } = new ConditionState(EConditionState.Disabled, "");

        [DataMember(Name = "valid")]
        public bool Valid { get; set; }

        [DataMember(Name = "state")]
        public EConditionState State { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }

        [DataMember(Name = "stateBrush")]
        public string StateBrush { get; set; }

        public ConditionState(EConditionState state, string message)
        {
            State = state;
            Message = message;
            Valid = state switch
            {
                EConditionState.Hidden => true,
                EConditionState.Stable => true,
                _ => false,
            };
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
    }

    public class ConditionStateBuilder
    {
        public FluxViewModel Flux { get; }
        public ConditionStateBuilder(FluxViewModel flux)
        {
            Flux = flux;
        }
        public ConditionCommand Create(Action action, string command_icon, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(action, command_icon, can_execute);
        }
        public ConditionCommand Create(Func<Task> action, string command_icon, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(action, command_icon, can_execute);
        }
        public ConditionCommand Create(Func<IFLUX_ConnectionProvider, Task<bool>> execute_paramacro, string command_icon, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(() => execute_paramacro(Flux.ConnectionProvider), command_icon, can_execute);
        }
        public ConditionCommand Create(Func<IFLUX_VariableStore, IFLUX_Variable<bool, bool>> get_variable, string command_icon, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(() => Flux.ConnectionProvider.ToggleVariableAsync(get_variable), command_icon, can_execute);
        }
        public ConditionCommand Create(Func<IFLUX_VariableStore, IFLUX_Array<bool, bool>> get_variable, VariableAlias unit, string command_icon, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(() => Flux.ConnectionProvider.ToggleVariableAsync(get_variable, unit), command_icon, can_execute);
        }
        public ConditionCommand Create<TRData, TWData>(Func<IFLUX_VariableStore, IFLUX_Variable<TRData, TWData>> get_variable, TWData value, string command_icon, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(() => Flux.ConnectionProvider.WriteVariableAsync(get_variable, value), command_icon, can_execute);
        }
        public ConditionCommand Create<TRData, TWData>(Func<IFLUX_VariableStore, IFLUX_Array<TRData, TWData>> get_variable, VariableAlias unit, TWData value, string command_icon, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(() => Flux.ConnectionProvider.WriteVariableAsync(get_variable, unit, value), command_icon, can_execute);
        }

        public ConditionCommand Create(Func<IFLUX_VariableStore, Optional<IFLUX_Variable<bool, bool>>> get_variable, string command_icon, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(() => Flux.ConnectionProvider.ToggleVariableAsync(get_variable), command_icon, can_execute);
        }
        public ConditionCommand Create(Func<IFLUX_VariableStore, Optional<IFLUX_Array<bool, bool>>> get_variable, VariableAlias unit, string command_icon, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(() => Flux.ConnectionProvider.ToggleVariableAsync(get_variable, unit), command_icon, can_execute);
        }
        public ConditionCommand Create<TRData, TWData>(Func<IFLUX_VariableStore, Optional<IFLUX_Variable<TRData, TWData>>> get_variable, TWData value, string command_icon, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(() => Flux.ConnectionProvider.WriteVariableAsync(get_variable, value), command_icon, can_execute);
        }
        public ConditionCommand Create<TRData, TWData>(Func<IFLUX_VariableStore, Optional<IFLUX_Array<TRData, TWData>>> get_variable, VariableAlias unit, TWData value, string command_icon, IObservable<bool> can_execute = null)
        {
            return new ConditionCommand(() => Flux.ConnectionProvider.WriteVariableAsync(get_variable, unit, value), command_icon, can_execute);
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
        [RemoteOutput(true)]
        public ConditionState State => _State.Value;

        private readonly ObservableAsPropertyHelper<T> _Value;
        [RemoteOutput(true)]
        public T Value => _Value.Value;
        
        [RemoteOutput(false)]
        public string ActionIcon { get; }

        [RemoteCommand]
        public Optional<ReactiveCommand<Unit, Unit>> ActionCommand { get; private set; }

        public IObservable<T> ValueChanged { get; }
        public IObservable<ConditionState> StateChanged { get; }

        public string ConditionName { get; }


        public ConditionViewModel(
            StatusProvider status_provider, 
            string condition_name, 
            IObservable<T> value_changed, 
            Func<ConditionStateBuilder, T, ConditionState> get_state,
            Func<ConditionStateBuilder, Optional<ConditionCommand>> get_command = default,
            TimeSpan sample = default) : base($"condition??{condition_name}")
        {
            ConditionName = condition_name;
            ValueChanged = value_changed;

            _Value = ValueChanged
                .ToPropertyRC(this, v => v.Value);

            if (sample != default)
                value_changed = value_changed.Sample(sample);

            var state_creator = status_provider.StateCreator;
            _State = value_changed
                .DistinctUntilChanged()
                .Select(v => get_state(state_creator, v))
                .StartWith(ConditionState.Default)
                .ToPropertyRC(this, e => e.State);

            var command = get_command.ToOptional()
                .Convert(f => f(state_creator));

            ActionCommand = command
                .Convert(c => ReactiveCommandRC.CreateFromTask(c.Func, this, c.CanExecute));

            ActionIcon = command
                .Convert(c => c.CommandIcon)
                .ValueOr(() => "");

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
            Func<ConditionStateBuilder, Optional<ConditionCommand>> get_command = default,
            TimeSpan sample = default)
            => new ConditionViewModel<TIn>(status_provider, name, value_changed, get_state, get_command, sample);
        public static Optional<IConditionViewModel> Create<TIn>(
            StatusProvider status_provider,
            string name,
            OptionalObservable<TIn> value_changed,
            Func<ConditionStateBuilder, TIn, ConditionState> get_state,
            Func<ConditionStateBuilder, Optional<ConditionCommand>> get_command = default,
            TimeSpan sample = default)
            => value_changed.Convert(v => (IConditionViewModel)new ConditionViewModel<TIn>(status_provider, name, v, get_state, get_command, sample));
    }
}