using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public interface IConditionViewModel : IRemoteControl
    {
        string Label { get; }
        public Optional<bool> IsValid { get; }
        public IObservable<Optional<bool>> IsValidChanged { get; }
    }

    public class ConditionViewModel<T> : RemoteControl<ConditionViewModel<T>>, IConditionViewModel
    {
        private ObservableAsPropertyHelper<string> _Label;
        [RemoteOutput(true)]
        public string Label => _Label.Value;

        protected ObservableAsPropertyHelper<Optional<bool>> _IsValid;
        [RemoteOutput(true)]
        public Optional<bool> IsValid => _IsValid.Value;

        private ObservableAsPropertyHelper<Optional<T>> _Value;
        [RemoteOutput(true)]
        public Optional<T> Value => _Value.Value;

        public IObservable<Optional<T>> ValueChanged { get; }
        public IObservable<Optional<bool>> IsValidChanged { get; }

        public ConditionViewModel(string name, IObservable<Optional<T>> value_changed, Func<T, bool> isValid, Func<Optional<T>, bool, string> label, Optional<TimeSpan> throttle = default) : base($"condition??{name}")
        {
            ValueChanged = value_changed;

            _Value = ValueChanged
                .ToProperty(this, v => v.Value);

            var is_valid = value_changed.Convert(isValid);
            if (throttle.HasValue)
                is_valid = is_valid.DistinctUntilChanged()
                    .Throttle(throttle.Value, RxApp.MainThreadScheduler);

            _IsValid = is_valid
                .DistinctUntilChanged()
                .ToProperty(this, e => e.IsValid);

            _Label = Observable.CombineLatest(
                this.WhenAnyValue(v => v.Value),
                this.WhenAnyValue(v => v.IsValid),
                (value, valid) => valid.ConvertOr(v => label(value, v), () => "ERROR"))
                .ToProperty(this, v => v.Label);

            IsValidChanged = this.WhenAnyValue(v => v.IsValid);
        }
    }

    public class ConditionsViewModel<TIn, TOut> : ConditionViewModel<(TIn @in, TOut @out)>
    {
        public ConditionsViewModel(string name, IObservable<Optional<(TIn @in, TOut @out)>> value_changed, Func<TIn, TOut, bool> isValid, Func<Optional<(TIn @in, TOut @out)>, bool, string> label, Optional<TimeSpan> throttle = default)
            : base(name, value_changed, tuple => isValid(tuple.@in, tuple.@out), label, throttle)
        {
        }
    }

    public class ConditionViewModel
    {
        // Single
        public static ConditionViewModel<TIn> Create<TIn>(
            string name,
            IObservable<Optional<TIn>> value_changed,
            Func<TIn, bool> valid_func,
            Func<Optional<TIn>, bool, string> name_func,
            Optional<TimeSpan> throttle = default)
            => new ConditionViewModel<TIn>(name, value_changed, valid_func, name_func, throttle);

        public static ConditionViewModel<bool> Create(
            string name,
            IObservable<Optional<bool>> value_changed,
            Func<Optional<bool>, bool, string> name_func,
            Optional<TimeSpan> throttle = default)
            => new ConditionViewModel<bool>(name, value_changed, v => v, name_func, throttle);

        // double
        public static ConditionsViewModel<TIn, TOut> Create<TIn, TOut>(
            string name,
            IObservable<Optional<TIn>> in_value,
            IObservable<Optional<TOut>> out_value,
            Func<TIn, TOut, bool> valid_func,
            Func<Optional<(TIn @in, TOut @out)>, bool, string> name_func,
            Optional<TimeSpan> throttle = default)
            => new ConditionsViewModel<TIn, TOut>(name, Observable.CombineLatest(in_value, out_value, (@in, @out) =>
            {
                if (@in.HasValue && @out.HasValue)
                    return Optional.Some<(TIn @in, TOut @out)>((@in.Value, @out.Value));
                return default;
            }), valid_func, name_func, throttle);
    }
}