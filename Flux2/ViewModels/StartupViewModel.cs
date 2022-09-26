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
                Flux.ConnectionProvider.WhenAnyValue(c => c.IsConnecting),
                (e, connecting) => (e.IsHomed, connecting))
                .DistinctUntilChanged();

            startup.Where(t => t.connecting || !t.IsHomed)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t => Flux.Navigator?.Navigate(this));

            startup.Where(t => !t.connecting && t.IsHomed)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => Flux.Navigator?.NavigateHome());

            var can_home = Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation)
                .Select(s => s.CanSafeCycle && !s.IsHomed);

            _ConnectionProgress = Flux.ConnectionProvider.WhenAnyValue(v => v.ConnectionProgress)
                .ToProperty(this, v => v.ConnectionProgress);

            StartupCommand = ReactiveCommand.CreateFromTask(StartupAsync, can_home);
            SettingsCommand = ReactiveCommand.Create(NavigateToSettingsAsync);
            ResetPrinterCommand = ReactiveCommand.Create(ResetPrinter);

            if (Flux.ConnectionProvider.HasToolChange)
                MagazineCommand = ReactiveCommand.Create(NavigateToMagazineAsync);
        }

        private void NavigateToSettingsAsync()
        {
            var functionality = Flux.Functionality;
            Flux.Navigator.NavigateModal(functionality);
        }

        private void NavigateToMagazineAsync()
        {
            var magazine = Flux.Functionality.Magazine.Value;
            Flux.Navigator.NavigateModal(magazine);
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
