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
    public class PurgeNozzleViewModel : InvalidValueViewModel<PurgeNozzleViewModel>
    {
        private readonly ObservableAsPropertyHelper<string> _InvalidItemBrush;
        public override string InvalidItemBrush => _InvalidItemBrush.Value;

        public PurgeNozzleViewModel(FluxViewModel flux, FeederEvaluator eval) : base(flux, eval)
        {
            var material = eval.Feeder.WhenAnyValue(f => f.SelectedMaterial);
            var target_temp = eval.WhenAnyValue(e => e.TargetTemperature);
            var tool_material = material.Convert(m => m.ToolMaterial);

            _InvalidItemBrush = Observable.CombineLatest(
               eval.Feeder.ToolNozzle.WhenAnyValue(m => m.NozzleTemperature),
               target_temp,
               (temp, target_temp) =>
               {
                   if (!temp.HasValue)
                       return FluxColors.Error;
                   if (!target_temp.HasValue)
                       return FluxColors.Error;
                   if (target_temp.ValueOr(() => 0) < target_temp.Value)
                       return FluxColors.Error;
                   var current_temp = temp.Value.Current;
                   var missing_temp = target_temp.Value - current_temp;
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
            return evaluation.WhenAnyValue(e => e.TargetTemperature)
                .Select(t => $"{t:0}°C")
                .Select(t => t.ToOptional());
        }
    }

    public class PurgeNozzlesViewModel : InvalidValuesViewModel<PurgeNozzlesViewModel>
    {
        public override bool StartWithInvalidValuesEnabled => false;
        public override bool CanStartWithInvalidValues => false;
        public PurgeNozzlesViewModel(FluxViewModel flux) : base(flux)
        {
        }

        public override void Initialize()
        {
            base.Initialize();
            InvalidValues = Flux.StatusProvider.FeederEvaluators.Connect().RemoveKey()
                .AutoRefresh(line => line.HasColdNozzle)
                .Filter(line => line.HasColdNozzle)
                .Transform(line => (IInvalidValueViewModel)new PurgeNozzleViewModel(Flux, line))
                .AsObservableListRC(this);
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
