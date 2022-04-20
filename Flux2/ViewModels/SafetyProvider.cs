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
        [JsonConverter(typeof(JsonConverters.OptionalConverter<bool>))]
        public Optional<bool> Valid { get; set; }
        public ConditionState(Optional<bool> valid, string message)
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

        private ObservableAsPropertyHelper<Optional<T>> _Value;
        [RemoteOutput(true)]
        public Optional<T> Value => _Value.Value;

        public IObservable<Optional<T>> ValueChanged { get; }
        public IObservable<ConditionState> StateChanged { get; }

        public ConditionViewModel(string name, IObservable<Optional<T>> value_changed, Func<Optional<T>, ConditionState> get_state, Optional<TimeSpan> throttle = default) : base($"condition??{name}")
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

    public class ConditionsViewModel<TIn, TOut> : ConditionViewModel<(TIn @in, TOut @out)>
    {
        public ConditionsViewModel(string name, IObservable<Optional<(TIn @in, TOut @out)>> value_changed, Func<Optional<(TIn @in, TOut @out)>, ConditionState> get_state, Optional<TimeSpan> throttle = default)
            : base(name, value_changed, get_state, throttle)
        {
        }
    }

    public class ConditionViewModel
    {
        // Single
        public static ConditionViewModel<TIn> Create<TIn>(
            string name,
            IObservable<TIn> value_changed,
            Func<TIn, ConditionState> get_state,
            Optional<TimeSpan> throttle = default)
            => new ConditionViewModel<TIn>(name, value_changed.Select(v => v.ToOptional()), v => get_state(v.Value), throttle);

        public static ConditionViewModel<TIn> Create<TIn>(
            string name,
            IObservable<Optional<TIn>> value_changed,
            Func<Optional<TIn>, ConditionState> get_state,
            Optional<TimeSpan> throttle = default)
            => new ConditionViewModel<TIn>(name, value_changed, get_state, throttle);

        public static ConditionViewModel<bool> Create(
            string name,
            IObservable<Optional<bool>> value_changed,
            Func<Optional<bool>, ConditionState> get_state,
            Optional<TimeSpan> throttle = default)
            => new ConditionViewModel<bool>(name, value_changed, get_state, throttle);

        // double
        public static ConditionsViewModel<TIn, TOut> Create<TIn, TOut>(
            string name,
            IObservable<Optional<TIn>> in_value,
            IObservable<Optional<TOut>> out_value,
            Func<Optional<(TIn @in, TOut @out)>, ConditionState> get_state,
            Optional<TimeSpan> throttle = default)
        {
            var value_changed = Observable.CombineLatest(in_value, out_value, (@in, @out) =>
            {
                if (@in.HasValue && @out.HasValue)
                    return Optional.Some<(TIn @in, TOut @out)>((@in.Value, @out.Value));
                return default;
            });

            return new ConditionsViewModel<TIn, TOut>(name, value_changed, get_state, throttle);
        }
    }
}