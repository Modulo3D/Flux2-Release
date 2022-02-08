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
    public class InvalidMaterialViewModel : InvalidItemViewModel<InvalidMaterialViewModel>
    {
        public override string CurrentValueName => "MATERIALE CARICATO";
        public override string ExpectedValueName => "MATERIALE RICHIESTO";

        private ObservableAsPropertyHelper<string> _InvalidItemBrush;
        public override string InvalidItemBrush => _InvalidItemBrush.Value;

        public InvalidMaterialViewModel(FeederEvaluator eval) : base($"{typeof(InvalidMaterialViewModel).GetRemoteControlName()}??{eval.Feeder.Position}", eval)
        {
            _InvalidItemBrush = Observable.CombineLatest(
                eval.Material.WhenAnyValue(m => m.CurrentDocument),
                eval.Material.WhenAnyValue(m => m.ExpectedDocumentQueue),
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
            return item.WhenAnyValue(e => e.Material.CurrentDocument)
                .Convert(f => f.Name);
        }
        public override IObservable<Optional<string>> GetExpectedValue(FeederEvaluator item)
        {
            return item.WhenAnyValue(e => e.Material.ExpectedDocumentQueue)
                .Convert(f => string.Join(Environment.NewLine, f.Values.Select(f => f.Name).Distinct()));
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
               .AutoRefresh(line => line.Material.IsInvalid)
               .Filter(line => line.Material.IsInvalid)
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
