using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class LowMaterialViewModel : InvalidValueViewModel<LowMaterialViewModel>
    {
        public override string ItemName => "MATERIALE";
        public override string CurrentValueName => "PESO RIMANENTE";
        public override string ExpectedValueName => "PESO RICHIESTO";

        private ObservableAsPropertyHelper<string> _InvalidItemBrush;
        public override string InvalidItemBrush => _InvalidItemBrush.Value;

        public LowMaterialViewModel(FeederEvaluator eval) : base($"{typeof(LowMaterialViewModel).GetRemoteControlName()}??{eval.Feeder.Position}", eval)
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
               .ToProperty(this, v => v.InvalidItemBrush)
               .DisposeWith(Disposables);
        }

        public override IObservable<Optional<string>> GetItem(FeederEvaluator eval)
        {
            return eval.Material.WhenAnyValue(e => e.CurrentDocument)
                .Convert(m => m.Name);
        }
        public override IObservable<Optional<string>> GetCurrentValue(FeederEvaluator eval)
        {
            return eval.Material.WhenAnyValue(e => e.CurrentWeight)
                .Convert(w => $"{w:0.##}g");
        }
        public override IObservable<Optional<string>> GetExpectedValue(FeederEvaluator eval)
        {
            return eval.Material.WhenAnyValue(e => e.ExpectedWeight)
                .Convert(w => $"{w:0.##}g");
        }
    }

    public class LowMaterialsViewModel : InvalidValuesViewModel<LowMaterialsViewModel>
    {
        public override string Title => "MATERIALI NON SUFFICIENTI";
        public override string ChangeName => "CAMBIA MATERIALE";
        public override bool CanStartWithInvalidValues => true;

        public LowMaterialsViewModel(FluxViewModel flux) : base(flux)
        {
        }

        public override void Initialize()
        {
            base.Initialize();
            InvalidValues = Flux.StatusProvider.FeederEvaluators.Connect().RemoveKey()
                .AutoRefresh(line => line.Material.HasLowWeight)
                .Filter(line => line.Material.HasLowWeight)
                .Transform(line => (IInvalidValueViewModel)new LowMaterialViewModel(line))
                .Sort(EvaluationComparer)
                .AsObservableList();
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
