using DynamicData.Kernel;
using Modulo3DDatabase;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

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
        [RemoteInput(step: 10)]
        public double TargetTemperature
        {
            get => _TargetTemperature;
            set => this.RaiseAndSetIfChanged(ref _TargetTemperature, value);
        }

        private ObservableAsPropertyHelper<Optional<FLUX_Temp>> _Temperature;
        [RemoteOutput(true, typeof(FluxTemperatureConverter))]
        public Optional<FLUX_Temp> Temperature => _Temperature.Value;

        [RemoteOutput(false)]
        public string Label { get; }

        public TemperatureViewModel(TemperaturesViewModel temperatures, IFLUX_Variable<FLUX_Temp, double> temp_var) : base($"temperature??{temp_var.Name}")
        {
            Label = temp_var.Name;
            Flux = temperatures.Flux;

            var is_idle = Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(s => s.IsIdle);

            ShutTargetCommand = ReactiveCommand.CreateFromTask(async () => { await temp_var.WriteAsync(0); }, is_idle);
            SelectTargetCommand = ReactiveCommand.CreateFromTask(async () => { await temp_var.WriteAsync(TargetTemperature); }, is_idle);

            _Temperature = temp_var.ValueChanged
                .ToProperty(this, v => v.Temperature);
        }
    }
}
