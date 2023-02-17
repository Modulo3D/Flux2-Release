using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
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

        public PrintingViewModel(FluxViewModel flux) : base(flux)
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
                    e.status.IsEnabledAxis &&
                    e.status.IsHomed &&
                    e.printing.MCode.HasValue &&
                    e.status.CanSafePrint);

            var canHold = eval
                .Select(e =>
                    e.status.IsEnabledAxis &&
                    e.status.IsHomed &&
                    e.status.CanSafeHold &&
                    e.printing.MCode.HasValue &&
                    !e.printing.Recovery.HasValue);

            var canStop = eval
                .Select(e =>
                    e.status.IsEnabledAxis &&
                    e.status.IsHomed &&
                    e.printing.MCode.HasValue &&
                    e.status.CanSafeStop);

            StartCommand = ReactiveCommandRC.CreateFromTask(StartPrintAsync, this, canStart);
            HoldCommand = ReactiveCommandRC.CreateFromTask(PausePrintAsync, this, canHold);
            ResetCommand = ReactiveCommandRC.CreateFromTask(CancelPrintAsync, this, canStop);

            _SelectedMCodeName = Flux.StatusProvider
                .WhenAnyValue(v => v.PrintingEvaluation)
                .Select(e => e.MCode)
                .Convert(m => m.Name)
                .Throttle(TimeSpan.FromSeconds(0.25))
                .ToPropertyRC(this, v => v.SelectedMCodeName);

            _PrintProgress = Flux.StatusProvider
                .WhenAnyValue(v => v.PrintProgress)
                .Select(p => p.Percentage)
                .ToPropertyRC(this, v => v.PrintProgress);

            _RemainingTime = Flux.StatusProvider
                .WhenAnyValue(v => v.PrintProgress)
                .Select(p => p.RemainingTime)
                .ToPropertyRC(this, v => v.RemainingTime);

            var current_job = Flux.StatusProvider
                .WhenAnyValue(s => s.PrintingEvaluation)
                .Select(s => s.FluxJob);

            var material_queue = Flux.StatusProvider.ExpectedMaterialsQueue.Connect()
                .QueryWhenChanged();

            var materials = Observable.CombineLatest(
                current_job, material_queue, (current_job, material_queue) =>
                material_queue.Items.Select(m => current_job.Convert(j => m.Convert(m => m.Lookup(j.JobKey)))));

            _ExpectedMaterials = materials
                .Select(i => i.Select(i => i.ConvertOr(i => i.Name, () => "---")))
                .Throttle(TimeSpan.FromSeconds(0.25))
                .ToPropertyRC(this, v => v.ExpectedMaterials);

            var nozzle_queue = Flux.StatusProvider.ExpectedNozzlesQueue.Connect()
                .QueryWhenChanged();

            var nozzles = Observable.CombineLatest(
                current_job, nozzle_queue, (current_job, nozzle_queue) =>
                nozzle_queue.Items.Select(m => current_job.Convert(j => m.Convert(m => m.Lookup(j.JobKey)))));

            _ExpectedNozzles = nozzles
                .Select(i => i.Select(i => i.ConvertOr(i => i.Name, () => "---")))
                .Throttle(TimeSpan.FromSeconds(0.25))
                .ToPropertyRC(this, v => v.ExpectedNozzles);
        }

        public async Task CancelPrintAsync()
        {
            await Flux.ConnectionProvider.CancelPrintAsync(false);
        }

        public async Task PausePrintAsync()
        {
            await Flux.ConnectionProvider.PausePrintAsync(false);
        }

        public async Task StartPrintAsync()
        {
            await Flux.ConnectionProvider.StartPrintAsync();
        }

        public Task EnterPhaseAsync()
        {
            return Task.CompletedTask;
        }
    }
}
