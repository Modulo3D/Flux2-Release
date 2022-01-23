using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class PrintingViewModel : HomePhaseViewModel<PrintingViewModel>
    {
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> StartCommand { get; private set; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> HoldCommand { get; private set; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ResetCommand { get; private set; }

        private ObservableAsPropertyHelper<Optional<string>> _SelectedMCodeName;
        [RemoteOutput(true)]
        public Optional<string> SelectedMCodeName => _SelectedMCodeName.Value;

        private ObservableAsPropertyHelper<double> _PrintProgress;
        [RemoteOutput(true)]
        public double PrintProgress => _PrintProgress.Value;

        private ObservableAsPropertyHelper<TimeSpan> _RemainingTime;
        [RemoteOutput(true, typeof(TimeSpanConverter))]
        public TimeSpan RemainingTime => _RemainingTime.Value;

        private ObservableAsPropertyHelper<IEnumerable<string>> _ExpectedMaterials;
        [RemoteOutput(true)]
        public IEnumerable<string> ExpectedMaterials => _ExpectedMaterials.Value;

        private ObservableAsPropertyHelper<IEnumerable<string>> _ExpectedNozzles;
        [RemoteOutput(true)]
        public IEnumerable<string> ExpectedNozzles => _ExpectedNozzles.Value;

        public PrintingViewModel(FluxViewModel flux) : base(flux, "printing")
        {
        }

        public override void Initialize()
        {
            var eval = Observable.CombineLatest(
                Flux.StatusProvider.WhenAnyValue(v => v.StatusEvaluation),
                Flux.StatusProvider.WhenAnyValue(v => v.PrintingEvaluation),
                (status, printing) => (status, printing));

            var canStart = eval
                .Select(e => 
                    e.status.IsEnabledAxis.ValueOrDefault() && 
                    e.status.IsHomed.ValueOrDefault() && 
                    e.printing.SelectedPartProgram.HasValue && 
                    e.status.CanSafePrint);

            var canHold = eval
                .Select(e => 
                    e.status.IsEnabledAxis.ValueOrDefault() && 
                    e.status.IsHomed.ValueOrDefault() && 
                    e.printing.SelectedPartProgram.HasValue && 
                    e.status.CanSafeHold);

            var canStop = eval
                .Select(e =>
                    e.status.IsEnabledAxis.ValueOrDefault() && 
                    e.status.IsHomed.ValueOrDefault() && 
                    e.printing.SelectedPartProgram.HasValue &&
                    e.status.CanSafeStop);

            StartCommand = ReactiveCommand.CreateFromTask(StartAsync, canStart, RxApp.MainThreadScheduler);
            HoldCommand = ReactiveCommand.CreateFromTask(HoldAsync, canHold, RxApp.MainThreadScheduler);
            ResetCommand = ReactiveCommand.CreateFromTask(ResetAsync, canStop, RxApp.MainThreadScheduler);

            _SelectedMCodeName = Flux.StatusProvider
                .WhenAnyValue(v => v.PrintingEvaluation)
                .Select(e => e.SelectedMCode)
                .Convert(m => m.Name)
                .ToProperty(this, v => v.SelectedMCodeName);

            _PrintProgress = Flux.StatusProvider
                .WhenAnyValue(v => v.PrintProgress)
                .Select(p => p.Percentage)
                .ToProperty(this, v => v.PrintProgress);

            _RemainingTime = Flux.StatusProvider
                .WhenAnyValue(v => v.PrintProgress)
                .Select(p => p.RemainingTime)
                .ToProperty(this, v => v.RemainingTime);

            _ExpectedMaterials = Flux.StatusProvider.ExpectedMaterials.Connect()
                .QueryWhenChanged()
                .Select(i => i.Select(i => i.Name))
                .ToProperty(this, v => v.ExpectedMaterials);

            _ExpectedNozzles = Flux.StatusProvider.ExpectedNozzles.Connect()
                .QueryWhenChanged()
                .Select(i => i.Select(i => i.Name))
                .ToProperty(this, v => v.ExpectedNozzles);

            InitializeRemoteView();
        }

        public async Task ResetAsync()
        {
            await Flux.ConnectionProvider.CancelPrintAsync(false);
        }

        public async Task HoldAsync()
        {
            await Flux.ConnectionProvider.HoldAsync(false);
        }

        public async Task StartAsync()
        {
            await Flux.ConnectionProvider.StartAsync();
        }

        public Task EnterPhaseAsync()
        {
            return Task.CompletedTask;
        }
    }
}
