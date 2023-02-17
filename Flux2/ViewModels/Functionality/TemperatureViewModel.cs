﻿using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public struct TimestampedFloat
    {
        public float Value { get; set; }
        public DateTime DateTime { get; set; }
        public TimestampedFloat(DateTime datetime, float value)
        {
            Value = value;
            DateTime = datetime;
        }
    }

    public class TemperatureViewModel : RemoteControl<TemperatureViewModel>
    {
        public FluxViewModel Flux { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ShutTargetCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> SelectTargetCommand { get; }

        private double _TargetTemperature = 0;
        [RemoteInput(step: 10, converter: typeof(TemperatureConverter))]
        public double TargetTemperature
        {
            get => _TargetTemperature;
            set => this.RaiseAndSetIfChanged(ref _TargetTemperature, value);
        }

        private readonly ObservableAsPropertyHelper<Optional<FLUX_Temp>> _Temperature;
        [RemoteOutput(true, typeof(FluxTemperatureConverter))]
        public Optional<FLUX_Temp> Temperature => _Temperature.Value;

        [RemoteOutput(false)]
        public RemoteText Label { get; }

        public TemperatureViewModel(TemperaturesViewModel temperatures, IFLUX_Variable<FLUX_Temp, double> temp_var) : base($"{temp_var.Name}")
        {
            Flux = temperatures.Flux;
            Label = new RemoteText($"temp;{temp_var.Unit.Alias}", true);

            var can_safe_cycle = Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(s => s.CanSafeCycle);

            ShutTargetCommand = ReactiveCommandRC.CreateFromTask(async () => { await temp_var.WriteAsync(0); }, this, can_safe_cycle);
            SelectTargetCommand = ReactiveCommandRC.CreateFromTask(async () => { await temp_var.WriteAsync(TargetTemperature); }, this, can_safe_cycle);

            _Temperature = temp_var.ValueChanged
                .ToPropertyRC(this, v => v.Temperature);
        }
    }
}
