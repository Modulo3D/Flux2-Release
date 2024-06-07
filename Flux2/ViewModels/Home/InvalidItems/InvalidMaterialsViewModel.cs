using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
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
        private readonly ObservableAsPropertyHelper<string> _InvalidItemBrush;
        public override string InvalidItemBrush => _InvalidItemBrush.Value;

        public InvalidMaterialViewModel(FluxViewModel flux, FeederEvaluator eval) : base(flux, eval)
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
                .ToPropertyRC(this, v => v.InvalidItemBrush);
        }

        public override IObservable<Optional<string>> GetCurrentValue(FeederEvaluator item)
        {
            return item.WhenAnyValue(e => e.Material.CurrentDocument)
                .Convert(f => f.Name);
        }
        public override IObservable<Optional<string>> GetExpectedValue(FeederEvaluator item)
        {
            return item.WhenAnyValue(e => e.Material.ExpectedDocumentQueue)
                .Convert(d => d.Values.DistinctBy(f => f.Name))
                .Convert(f => string.Join(Environment.NewLine, f.Select(f => f.Name)));
        }
    }

    public class InvalidMaterialsViewModel : InvalidItemsViewModel<InvalidMaterialsViewModel>
    {
        public InvalidMaterialsViewModel(FluxViewModel flux) : base(flux)
        {
        }

        public override void Initialize()
        {
            InvalidItems = Flux.StatusProvider.FeederEvaluators.Connect().RemoveKey()
               .AutoRefresh(line => line.Material.IsInvalid)
               .Filter(line => line.Material.IsInvalid)
               .Transform(line => (IInvalidItemViewModel)new InvalidMaterialViewModel(Flux, line))
               .AsObservableListRC(this);
        }

        public override Task ChangeItemsAsync()
        {
            Flux.Navigator.Navigate(Flux.Feeders);
            return Task.CompletedTask;
        }
    }
}
