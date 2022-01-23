using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using Splat;
using System;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class InvalidToolViewModel : InvalidItemViewModel<InvalidToolViewModel>
    {
        public override string CurrentValueName => "UTENSILE CARICATO";
        public override string ExpectedValueName => "UTENSILE RICHIESTO";

        public InvalidToolViewModel(FeederEvaluator eval) : base(eval)
        {
        }

        public override IObservable<Optional<string>> GetCurrentValue(FeederEvaluator item)
        {
            return item.WhenAnyValue(e => e.ToolNozzle.CurrentDocument)
                .Convert(n => n.Name);
        }
        public override IObservable<Optional<string>> GetExpectedValue(FeederEvaluator item)
        {
            return item.WhenAnyValue(e => e.ToolNozzle.ExpectedDocument)
                .Convert(n => n.Name);
        }
    }

    public class InvalidToolsViewModel : InvalidItemsViewModel<InvalidToolsViewModel>
    {
        public override string Title => "UTENSILI NON VALIDI";
        public override string ChangeName => "CAMBIA UTENSILE";

        public InvalidToolsViewModel(FluxViewModel flux) : base(flux)
        {
        }

        public override void Initialize()
        {
            InvalidItems = Flux.StatusProvider.FeederEvaluators.Connect().RemoveKey()
                .AutoRefresh(line => line.ToolNozzle.IsValid)
                .Filter(line => !line.ToolNozzle.IsValid)
                .Transform(line => (IInvalidItemViewModel)new InvalidToolViewModel(line))
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
