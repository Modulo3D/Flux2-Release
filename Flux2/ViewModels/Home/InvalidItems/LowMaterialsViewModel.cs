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
    public class LowMaterialViewModel : InvalidValueViewModel<LowMaterialViewModel>
    {

        private readonly ObservableAsPropertyHelper<string> _InvalidItemBrush;
        public override string InvalidItemBrush => _InvalidItemBrush.Value;

        public LowMaterialViewModel(FeederEvaluator eval) : base(eval)
        {
            _InvalidItemBrush = Observable.CombineLatest(
               eval.Material.WhenAnyValue(m => m.CurrentWeight),
               eval.Material.WhenAnyValue(m => m.ExpectedWeight),
               (current_weight, expected_weight) =>
               {
                   if (!current_weight.HasValue || !expected_weight.HasValue)
                       return FluxColors.Error;

                   var missing_weight = expected_weight.Value - current_weight.Value;
                   if (missing_weight <= 0)
                       return FluxColors.Active;

                   if (missing_weight < 10)
                       return FluxColors.Error;

                   return FluxColors.Warning;
               })
               .ToPropertyRC(this, v => v.InvalidItemBrush);
        }

        public override IObservable<Optional<string>> GetItem(FeederEvaluator eval)
        {
            return eval.Material.WhenAnyValue(e => e.CurrentDocument)
                .Convert(m => m.Name);
        }
        public override IObservable<Optional<string>> GetCurrentValue(FeederEvaluator eval)
        {
            return eval.Material.WhenAnyValue(e => e.CurrentWeight)
                .Convert(w => $"{w:0.##}g".Replace(",", "."));
        }
        public override IObservable<Optional<string>> GetExpectedValue(FeederEvaluator eval)
        {
            return eval.Material.WhenAnyValue(e => e.ExpectedWeight)
                .Convert(w => $"{w:0.##}g".Replace(",", "."));
        }
    }

    public class LowMaterialsViewModel : InvalidValuesViewModel<LowMaterialsViewModel>
    {
        private ObservableAsPropertyHelper<bool> _StartWithInvalidValuesEnabled;
        public override bool StartWithInvalidValuesEnabled => _StartWithInvalidValuesEnabled.Value;
        public override bool CanStartWithInvalidValues => true;

        public LowMaterialsViewModel(FluxViewModel flux) : base(flux)
        {
        }

        public override void Initialize()
        {
            InvalidValues = Flux.StatusProvider.FeederEvaluators.Connect().RemoveKey()
                .AutoRefresh(line => line.Material.HasLowWeight)
                .Filter(line => line.Material.HasLowWeight)
                .Transform(line => (IInvalidValueViewModel)new LowMaterialViewModel(line))
                .AsObservableListRC(this);

            _StartWithInvalidValuesEnabled = Flux.StatusProvider.FeederEvaluators.Connect()
                .TrueForAll(line => line.Material.WhenAnyValue(m => m.HasEmptyWeight), e => !e)
                .ToPropertyRC(this, v => v.StartWithInvalidValuesEnabled);

            base.Initialize();
        }

        public override void StartWithInvalidValues()
        {
            Flux.StatusProvider.StartWithLowMaterials = true;
        }
        public override Task ChangeItemsAsync()
        {
            Flux.Navigator.Navigate(Flux.Feeders);
            return Task.CompletedTask;
        }
    }
}
