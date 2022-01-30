using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class InvalidToolViewModel : InvalidItemViewModel<InvalidToolViewModel>
    {
        public override string CurrentValueName => "UTENSILE CARICATO";
        public override string ExpectedValueName => "UTENSILE RICHIESTO";

        private ObservableAsPropertyHelper<string> _InvalidItemBrush;
        public override string InvalidItemBrush => _InvalidItemBrush.Value;

        public InvalidToolViewModel(FeederEvaluator eval) : base($"{typeof(InvalidToolViewModel).GetRemoteControlName()}??{eval.Feeder.Position}", eval)
        {
            _InvalidItemBrush = Observable.CombineLatest(
               eval.ToolNozzle.WhenAnyValue(m => m.CurrentDocument),
               eval.ToolNozzle.WhenAnyValue(m => m.ExpectedDocumentQueue),
               (current_document, expected_document_queue) =>
               {
                   if (!expected_document_queue.HasValue)
                       return FluxColors.Error;
                   if (!current_document.HasValue)
                       return FluxColors.Warning;
                   foreach (var expected_document in expected_document_queue.Value)
                       if (expected_document.Value.Id != current_document.Value.Id)
                           return FluxColors.Error;
                   return FluxColors.Warning;
               })
               .ToProperty(this, v => v.InvalidItemBrush)
               .DisposeWith(Disposables);
        }

        public override IObservable<Optional<string>> GetCurrentValue(FeederEvaluator item)
        {
            return item.WhenAnyValue(e => e.ToolNozzle.CurrentDocument)
                .Convert(n => n.Name);
        }
        public override IObservable<Optional<string>> GetExpectedValue(FeederEvaluator item)
        {
            return item.WhenAnyValue(e => e.ToolNozzle.ExpectedDocumentQueue)
                .Convert(f => string.Join(Environment.NewLine, f.Values.Select(f => f.Name).Distinct()));
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
