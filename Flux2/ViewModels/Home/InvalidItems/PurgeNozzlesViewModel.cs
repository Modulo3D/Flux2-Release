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
    public class PurgeNozzleViewModel : InvalidValueViewModel<PurgeNozzleViewModel>
    {
        public override string ItemName => "UTENSILE";
        public override string CurrentValueName => "TEMPERATURA CORRENTE";
        public override string ExpectedValueName => "TEMPERATURA RICHIESTA";

        private ObservableAsPropertyHelper<string> _InvalidItemBrush;
        public override string InvalidItemBrush => _InvalidItemBrush.Value;

        public PurgeNozzleViewModel(FeederEvaluator eval) : base($"{typeof(PurgeNozzleViewModel).GetRemoteControlName()}??{eval.Feeder.Position}", eval)
        {
            var tool_material = eval.Feeder.WhenAnyValue(f => f.SelectedToolMaterial);

            _InvalidItemBrush = Observable.CombineLatest(
               eval.Feeder.ToolNozzle.WhenAnyValue(m => m.NozzleTemperature),
               tool_material.ConvertMany(tm => tm.WhenAnyValue(m => m.ExtrusionTemp)).ValueOr(() => 0),
               (temp, expected_temp) =>
               {
                   if (!temp.HasValue)
                       return FluxColors.Error;
                   var target_temp = temp.Value.Target;
                   if (target_temp < expected_temp)
                       return FluxColors.Error;
                   var current_temp = temp.Value.Current;
                   var missing_temp = expected_temp - current_temp;
                   if (missing_temp > 15)
                       return FluxColors.Error;
                   return FluxColors.Warning;
               })
               .ToProperty(this, v => v.InvalidItemBrush)
               .DisposeWith(Disposables);
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
                .WhenAnyValue(t => t.NozzleTemperature)
                .ConvertOr(t => $"{t.Current:0}°C", () => "Err")
                .Select(t => t.ToOptional());
        }
        public override IObservable<Optional<string>> GetExpectedValue(FeederEvaluator evaluation)
        {
            return evaluation.Feeder
                .WhenAnyValue(f => f.SelectedToolMaterial)
                .ConvertMany(tm => tm.WhenAnyValue(t => t.ExtrusionTemp))
                .Select(t => $"{t:0}°C")
                .Select(t => t.ToOptional());
        }
    }

    public class PurgeNozzlesViewModel : InvalidValuesViewModel<PurgeNozzlesViewModel>
    {
        public override bool CanStartWithInvalidValues => false;
        public override string Title => "UTENSILI DA SPURGARE";
        public override string ChangeName => "SPURGA";

        public PurgeNozzlesViewModel(FluxViewModel flux) : base(flux)
        {
        }

        public override void Initialize()
        {
            base.Initialize();
            InvalidValues = Flux.StatusProvider.FeederEvaluators.Connect().RemoveKey()
                .AutoRefresh(line => line.HasColdNozzle)
                .Filter(line => line.HasColdNozzle)
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
