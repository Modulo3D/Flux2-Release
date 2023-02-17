using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class LowNozzleViewModel : InvalidValueViewModel<LowNozzleViewModel>
    {
        private readonly ObservableAsPropertyHelper<string> _InvalidItemBrush;
        public override string InvalidItemBrush => _InvalidItemBrush.Value;

        public LowNozzleViewModel(FeederEvaluator eval) : base(eval)
        {
            _InvalidItemBrush = Observable.CombineLatest(
                eval.ToolNozzle.WhenAnyValue(m => m.CurrentWeight),
                eval.ToolNozzle.WhenAnyValue(m => m.ExpectedWeight),
                (current_weight, expected_weight) =>
                {
                    if (!current_weight.HasValue || !expected_weight.HasValue)
                        return FluxColors.Error;

                    var missing_weight = expected_weight.Value - current_weight.Value;
                    if (missing_weight <= 0)
                        return FluxColors.Active;

                    if (missing_weight < 1000)
                        return FluxColors.Error;

                    return FluxColors.Warning;
                })
                .ToPropertyRC(this, v => v.InvalidItemBrush);
        }

        public override IObservable<Optional<string>> GetItem(FeederEvaluator eval)
        {
            return eval.ToolNozzle.WhenAnyValue(e => e.CurrentDocument)
                .Convert(m => m.Name);
        }
        public override IObservable<Optional<string>> GetCurrentValue(FeederEvaluator eval)
        {
            return eval.ToolNozzle.WhenAnyValue(e => e.CurrentWeight)
                .Convert(w => $"{w:0.##}g".Replace(",", "."));
        }
        public override IObservable<Optional<string>> GetExpectedValue(FeederEvaluator eval)
        {
            return eval.ToolNozzle.WhenAnyValue(e => e.ExpectedWeight)
                .Convert(w => $"{w:0.##}g".Replace(",", "."));
        }
    }

    public class LowNozzlesViewModel : InvalidValuesViewModel<LowNozzlesViewModel>
    {
        public override bool CanStartWithInvalidValues => true;
        public LowNozzlesViewModel(FluxViewModel flux) : base(flux)
        {
        }

        public override void Initialize()
        {
            InvalidValues = Flux.StatusProvider.FeederEvaluators.Connect().RemoveKey()
                .AutoRefresh(line => line.ToolNozzle.HasLowWeight)
                .Filter(line => line.ToolNozzle.HasLowWeight)
                .Transform(line => (IInvalidValueViewModel)new LowMaterialViewModel(line))
                .Sort(EvaluationComparer)
                .AsObservableListRC(this);
        }

        public override Task ChangeItemsAsync()
        {
            Flux.Navigator.Navigate(Flux.Feeders);
            return Task.CompletedTask;
        }

        public override void StartWithInvalidValues()
        {
            throw new NotImplementedException();
        }
    }
}
