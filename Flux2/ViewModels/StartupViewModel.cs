using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
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

        private readonly ObservableAsPropertyHelper<double> _ConnectionProgress;
        [RemoteOutput(true)]
        public double ConnectionProgress => _ConnectionProgress.Value;

        public StartupViewModel(FluxViewModel flux) : base(flux)
        {
            var startup = Observable.CombineLatest(
                Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation),
                Flux.ConnectionProvider.WhenAnyValue(c => c.IsConnecting),
                (status, connecting) => (status, connecting))
                .Throttle(TimeSpan.FromSeconds(0.5))
                .DistinctUntilChanged();

            startup.Where(t => t.connecting || (!t.status.IsHomed && t.status.IsIdle))
                .ObserveOn(RxApp.MainThreadScheduler)
                .SubscribeRC(t => Flux.Navigator?.Navigate(this), this);

            startup
                .PairWithPreviousValue()
                .Where(t => t.OldValue.connecting || !t.OldValue.status.IsHomed)
                .Where(t => !t.NewValue.connecting && t.NewValue.status.IsHomed && t.NewValue.status.IsIdle)
                .ObserveOn(RxApp.MainThreadScheduler)
                .SubscribeRC(_ => { Flux.Navigator?.NavigateHome(); }, this);

            var can_home = Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation)
                .Select(s => s.CanSafeCycle && !s.IsHomed);

            _ConnectionProgress = Flux.ConnectionProvider.WhenAnyValue(v => v.ConnectionProgress)
                .ToPropertyRC(this, v => v.ConnectionProgress);

            StartupCommand = ReactiveCommandRC.CreateFromTask(StartupAsync, this, can_home);
            SettingsCommand = ReactiveCommandRC.Create(NavigateToSettingsAsync, this);
            ResetPrinterCommand = ReactiveCommandRC.Create(ResetPrinter, this);

            if (Flux.ConnectionProvider.VariableStoreBase.HasToolChange)
                MagazineCommand = ReactiveCommandRC.Create(NavigateToMagazineAsync, this);
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
            var home_response = await Flux.ConnectionProvider.HomeAsync(true);
            if (home_response == false)
                return;
        }
    }
}
