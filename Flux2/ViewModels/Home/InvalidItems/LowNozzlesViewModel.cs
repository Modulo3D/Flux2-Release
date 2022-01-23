using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using Splat;
using System;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class LowNozzleViewModel : InvalidValueViewModel<LowNozzleViewModel>
    {
        public override string ItemName => "UGELLO";
        public override string CurrentValueName => "PESO RIMANENTE";
        public override string ExpectedValueName => "PESO RICHIESTO";

        public LowNozzleViewModel(FeederEvaluator eval) : base(eval)
        {
        }

        public override IObservable<Optional<string>> GetItem(FeederEvaluator eval)
        {
            return eval.ToolNozzle.WhenAnyValue(e => e.CurrentDocument)
                .Convert(m => m.Name);
        }
        public override IObservable<Optional<string>> GetCurrentValue(FeederEvaluator eval)
        {
            return eval.ToolNozzle.WhenAnyValue(e => e.CurrentWeight)
                .Convert(w => $"{w:0.##}g");
        }
        public override IObservable<Optional<string>> GetExpectedValue(FeederEvaluator eval)
        {
            return eval.ToolNozzle.WhenAnyValue(e => e.ExpectedWeight)
                .Convert(w => $"{w:0.##}g");
        }
    }

    public class LowNozzlesViewModel : InvalidValuesViewModel<LowNozzlesViewModel>
    {
        public override string Title => "UTENSILI CONSUMATI";
        public override string ChangeName => "CAMBIA UTENSILE";
        public override bool CanStartWithInvalidValues => false;

        public LowNozzlesViewModel(FluxViewModel flux) : base(flux)
        {
        }

        public override void Initialize()
        {
            InvalidValues = Flux.StatusProvider.FeederEvaluators.Connect().RemoveKey()
                .AutoRefresh(line => line.ToolNozzle.HasEnoughWeight)
                .Filter(line => !line.ToolNozzle.HasEnoughWeight)
                .Transform(line => (IInvalidValueViewModel)new LowMaterialViewModel(line))
                .Sort(EvaluationComparer)
                .AsObservableList();
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
