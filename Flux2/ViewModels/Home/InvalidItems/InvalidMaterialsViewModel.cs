using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using Splat;
using System;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class InvalidMaterialViewModel : InvalidItemViewModel<InvalidMaterialViewModel>
    {
        public override string CurrentValueName => "MATERIALE CARICATO";
        public override string ExpectedValueName => "MATERIALE RICHIESTO";

        public InvalidMaterialViewModel(FeederEvaluator eval) : base(eval)
        {
        }

        public override IObservable<Optional<string>> GetCurrentValue(FeederEvaluator item)
        {
            return item.WhenAnyValue(e => e.Material.CurrentDocument)
                .Convert(f => f.Name);
        }
        public override IObservable<Optional<string>> GetExpectedValue(FeederEvaluator item)
        {
            return item.WhenAnyValue(e => e.Material.ExpectedDocument)
                .Convert(f => f.Name);
        }
    }

    [RemoteControl]
    public class InvalidMaterialsViewModel : InvalidItemsViewModel<InvalidMaterialsViewModel>
    {
        public override string Title => "MATERIALI NON VALIDI";
        public override string ChangeName => "CAMBIA MATERIALE";

        public InvalidMaterialsViewModel(FluxViewModel flux) : base(flux)
        {
        }

        public override void Initialize()
        {
            InvalidItems = Flux.StatusProvider.FeederEvaluators.Connect().RemoveKey()
               .AutoRefresh(line => line.Material.IsValid)
               .Filter(line => !line.Material.IsValid)
               .Transform(line => (IInvalidItemViewModel)new InvalidMaterialViewModel(line))
               .Sort(EvaluationComparer)
               .AsObservableList();
        }

        public override Task ChangeItemsAsync()
        {
            Flux.Navigator.Navigate(Flux.Feeders);
            return Task.CompletedTask;
        }
    }
}
