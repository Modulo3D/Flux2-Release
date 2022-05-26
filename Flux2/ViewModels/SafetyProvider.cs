using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Reactive.Linq;
using System.Runtime.Serialization;

namespace Flux.ViewModels
{
    public struct ConditionState
    {
        [JsonProperty("message")]
        public string Message { get; set; }
        [JsonProperty("valid")]
        public bool Valid { get; set; }
        public ConditionState(bool valid, string message)
        {
            Valid = valid;
            Message = message;
        }
    }

    public interface IConditionViewModel : IRemoteControl
    {
        public ConditionState State { get; }
        public IObservable<ConditionState> StateChanged { get; }
    }

    public class ConditionViewModel<T> : RemoteControl<ConditionViewModel<T>>, IConditionViewModel
    {
        private ObservableAsPropertyHelper<ConditionState> _State;
        [RemoteOutput(true)]
        public ConditionState State => _State.Value;

        private ObservableAsPropertyHelper<T> _Value;
        [RemoteOutput(true)]
        public T Value => _Value.Value;

        public IObservable<T> ValueChanged { get; }
        public IObservable<ConditionState> StateChanged { get; }

        public ConditionViewModel(string name, IObservable<T> value_changed, Func<T, ConditionState> get_state, Optional<TimeSpan> throttle = default) : base($"condition??{name}")
        {
            ValueChanged = value_changed;

            _Value = ValueChanged
                .ToProperty(this, v => v.Value);

            var current_state = value_changed.Select(get_state);
            if (throttle.HasValue)
                current_state = current_state.DistinctUntilChanged()
                    .Throttle(throttle.Value, RxApp.MainThreadScheduler);

            _State = current_state
                .DistinctUntilChanged()
                .ToProperty(this, e => e.State);

            StateChanged = this.WhenAnyValue(v => v.State);
        }
    }

    public class ConditionViewModel
    {
        public static ConditionViewModel<TIn> Create<TIn>(
            string name,
            IObservable<TIn> value_changed,
            Func<TIn, ConditionState> get_state,
            Optional<TimeSpan> throttle = default)
            => new ConditionViewModel<TIn>(name, value_changed, get_state, throttle);
        public static Optional<ConditionViewModel<TIn>> Create<TIn>(
            string name,
            OptionalObservable<TIn> value_changed,
            Func<TIn, ConditionState> get_state,
            Optional<TimeSpan> throttle = default)
            => value_changed.Convert(v => new ConditionViewModel<TIn>(name, v, get_state, throttle));
    }
}