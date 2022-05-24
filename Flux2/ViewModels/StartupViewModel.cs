using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class StartupViewModel : FluxRoutableViewModel<StartupViewModel>
    {
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ResetPrinterCommand { get; private set; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> StartupCommand { get; private set; }
        [RemoteCommand]
        public Optional<ReactiveCommand<Unit, Unit>> MagazineCommand { get; private set; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> SettingsCommand { get; private set; }

        private ObservableAsPropertyHelper<double> _ConnectionProgress;
        [RemoteOutput(true)]
        public double ConnectionProgress => _ConnectionProgress.Value;

        public StartupViewModel(FluxViewModel flux) : base(flux)
        {
            var startup = Observable.CombineLatest(
                Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation),
                Flux.ConnectionProvider.WhenAnyValue(c => c.IsInitializing),
                (e, initializing) => (e.IsHomed, initializing))
                .DistinctUntilChanged();

            startup.Where(t =>
                t.initializing.HasValue && 
                (t.initializing.Value || !t.IsHomed.HasValue || !t.IsHomed.Value))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t => Flux.Navigator.Navigate(this));

            startup.Where(t =>
                t.initializing.HasValue &&
                (!t.initializing.Value && t.IsHomed.HasValue && t.IsHomed.Value))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => Flux.Navigator.NavigateHome());

            var can_reset = Flux.StatusProvider.IsIdle
                .ValueOr(() => true);

            var can_home = Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation)
                .Select(s => s.CanSafeCycle && s.IsHomed.HasValue && !s.IsHomed.Value);

            _ConnectionProgress = Flux.ConnectionProvider.WhenAnyValue(v => v.ConnectionProgress)
                .ToProperty(this, v => v.ConnectionProgress);

            ResetPrinterCommand = ReactiveCommand.Create(ResetPrinter, can_reset);
            StartupCommand = ReactiveCommand.CreateFromTask(StartupAsync, can_home);
            SettingsCommand = ReactiveCommand.Create(SettingsAsync);

            if (Flux.ConnectionProvider.HasToolChange)
                MagazineCommand = ReactiveCommand.Create(MagazineAsync);
        }

        private void SettingsAsync()
        {
            var functionality = Flux.Functionality;
            Flux.Navigator.NavigateModal(functionality);
        }

        private void MagazineAsync()
        {
            var navigate_back = Flux.StatusProvider.ClampClosed.StateChanged
                .Select(s => s.Valid)
                .ValueOr(() => true)
                .ToOptional();

            Flux.Navigator.NavigateModal(Flux.Magazine, navigate_back: navigate_back);
        }

        public void ResetPrinter()
        {
            Flux.ConnectionProvider.StartConnection();
        }

        private async Task StartupAsync()
        {
            var home_response = await Flux.ConnectionProvider.HomeAsync();
            if (home_response == false)
                return;
        }
    }
}
