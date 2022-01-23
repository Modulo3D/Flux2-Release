using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using Splat;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class PurgeNozzleViewModel : InvalidValueViewModel<PurgeNozzleViewModel>
    {
        public override string ItemName => "UTENSILE";
        public override string CurrentValueName => "TEMPERATURA CORRENTE";
        public override string ExpectedValueName => "TEMPERATURA RICHIESTA";

        public PurgeNozzleViewModel(FeederEvaluator eval) : base(eval)
        {

        }

        public override IObservable<Optional<string>> GetItem(FeederEvaluator evaluation)
        {
            return evaluation.Feeder.ToolNozzle
                .WhenAnyValue(t => t.Document)
                .Select(d => d.nozzle)
                .Convert(t => t.Name);
        }
        public override IObservable<Optional<string>> GetCurrentValue(FeederEvaluator evaluation)
        {
            return evaluation.Feeder.ToolNozzle
                .WhenAnyValue(t => t.Temperature)
                .ConvertOr(t => $"{t.Current:0}°C", () => "Err")
                .Select(t => t.ToOptional());
        }
        public override IObservable<Optional<string>> GetExpectedValue(FeederEvaluator evaluation)
        {
            return evaluation.Feeder.ToolMaterial
                .WhenAnyValue(t => t.ExtrusionTemp)
                .Select(t => $"{t:0}°C")
                .Select(t => t.ToOptional());
        }
    }

    public class PurgeNozzlesViewModel : InvalidValuesViewModel<PurgeNozzlesViewModel>
    {
        public override bool CanStartWithInvalidValues => false;
        public override string Title => "UTENSILI DA SPURGARE";
        public override string ChangeName => "SPURGA";

        public PurgeNozzlesViewModel(FluxViewModel flux) : base(flux, "purge_nozzles")
        {
        }

        public override void Initialize()
        {
            base.Initialize();
            InvalidValues = Flux.StatusProvider.FeederEvaluators.Connect().RemoveKey()
                .AutoRefresh(line => line.HasHotNozzle)
                .Filter(line => line.HasHotNozzle.ConvertOr(h => !h, () => false))
                .Transform(line => (IInvalidValueViewModel)new PurgeNozzleViewModel(line))
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
