using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using Splat;
using System;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class LowMaterialViewModel : InvalidValueViewModel<LowMaterialViewModel>
    {
        public override string ItemName => "MATERIALE";
        public override string CurrentValueName => "PESO RIMANENTE";
        public override string ExpectedValueName => "PESO RICHIESTO";

        public LowMaterialViewModel(FeederEvaluator eval) : base(eval)
        {
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
                .AutoRefresh(line => line.Material.HasEnoughWeight)
                .Filter(line => !line.Material.HasEnoughWeight)
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
