﻿using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class InvalidProbeViewModel : InvalidValueViewModel<InvalidProbeViewModel>
    {

        private readonly ObservableAsPropertyHelper<string> _InvalidItemBrush;
        public override string InvalidItemBrush => _InvalidItemBrush.Value;

        public InvalidProbeViewModel(FeederEvaluator eval) : base(eval)
        {
            _InvalidItemBrush = eval.WhenAnyValue(e => e.Offset)
                .ConvertMany(o => o.WhenAnyValue(o => o.ProbeStateBrush))
                .ValueOr(() => FluxColors.Inactive)
                .ToProperty(this, v => v.InvalidItemBrush)
                .DisposeWith(Disposables);
        }

        public override IObservable<Optional<string>> GetItem(FeederEvaluator evaluation)
        {
            return evaluation.Feeder.ToolNozzle
                .WhenAnyValue(t => t.Document)
                .Select(t => t.nozzle)
                .Convert(n => n.Name);
        }
        public override IObservable<Optional<string>> GetCurrentValue(FeederEvaluator eval)
        {
            return Observable.Return("Z OFFSET = 0".ToOptional());
        }
        public override IObservable<Optional<string>> GetExpectedValue(FeederEvaluator eval)
        {
            return Observable.Return("Z OFFSET != 0".ToOptional());
        }
    }

    public class InvalidProbesViewModel : InvalidValuesViewModel<InvalidProbesViewModel>
    {
        public override bool CanStartWithInvalidValues => false;
        public InvalidProbesViewModel(FluxViewModel flux) : base(flux)
        {
        }

        public override void Initialize()
        {
            base.Initialize();
            InvalidValues = Flux.StatusProvider.FeederEvaluators.Connect().RemoveKey()
                .AutoRefresh(line => line.IsInvalidProbe)
                .Filter(line => line.IsInvalidProbe)
                .Transform(line => (IInvalidValueViewModel)new InvalidProbeViewModel(line))
                .Sort(EvaluationComparer)
                .AsObservableListRC(this);
        }

        public override Task ChangeItemsAsync()
        {
            Flux.Navigator.Navigate(Flux.Calibration);
            return Task.CompletedTask;
        }

        public override void StartWithInvalidValues()
        {
            throw new NotImplementedException();
        }
    }
}
