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

        public TemperatureViewModel(TemperaturesViewModel temperatures, string label, string key, Func<double, Task<bool>> set_temp, OptionalObservable<Optional<FLUX_Temp>> temperature) : base($"temperature??{key}")
        {
            Label = label;
            Flux = temperatures.Flux;

            var is_idle = Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(s => s.IsIdle);

            ShutTargetCommand = ReactiveCommand.CreateFromTask(async () => { await set_temp(0); }, is_idle);
            SelectTargetCommand = ReactiveCommand.CreateFromTask(async () => { await set_temp(TargetTemperature); }, is_idle);

            _Temperature = temperature
                .ObservableOrDefault()
                .ToProperty(this, v => v.Temperature);
        }
    }
}
