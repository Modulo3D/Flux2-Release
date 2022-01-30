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
        private ObservableAsPropertyHelper<bool> _IsHomeX;
        [RemoteOutput(true)]
        public bool IsHomeX => _IsHomeX.Value;

        private ObservableAsPropertyHelper<bool> _IsHomeY;
        [RemoteOutput(true)]
        public bool IsHomeY => _IsHomeY.Value;

        private ObservableAsPropertyHelper<bool> _IsHomeZ;
        [RemoteOutput(true)]
        public bool IsHomeZ => _IsHomeZ.Value;

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ResetPrinterCommand { get; private set; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> StartupCommand { get; private set; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MagazineCommand { get; private set; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> SettingsCommand { get; private set; }

        private ObservableAsPropertyHelper<double> _ConnectionProgress;
        [RemoteOutput(true)]
        public double ConnectionProgress => _ConnectionProgress.Value;

        public StartupViewModel(FluxViewModel flux) : base(flux)
        {
            // TODO

            var endstop_0 = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.AXIS_ENDSTOP, 0);
            var endstop_1 = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.AXIS_ENDSTOP, 1);
            var endstop_2 = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.AXIS_ENDSTOP, 2);
            if (!endstop_0.HasValue || !endstop_1.HasValue || !endstop_2.HasValue)
                return;

            _IsHomeX = Flux.ConnectionProvider.ObserveVariable(m => m.AXIS_ENDSTOP, endstop_0.Value)
                .ConvertOr(v => !v, () => false)
                .ToProperty(this, v => v.IsHomeX);

            _IsHomeY = Flux.ConnectionProvider.ObserveVariable(m => m.AXIS_ENDSTOP, endstop_1.Value)
                .ConvertOr(v => !v, () => false)
                .ToProperty(this, v => v.IsHomeY);

            _IsHomeZ = Flux.ConnectionProvider.ObserveVariable(m => m.AXIS_ENDSTOP, endstop_2.Value)
                .ConvertOr(v => !v, () => false)
                .ToProperty(this, v => v.IsHomeZ);

            var startup = Observable.CombineLatest(
                Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation),
                Flux.ConnectionProvider.WhenAnyValue(c => c.IsInitializing),
                (e, initializing) => (e.IsHomed, initializing))
                .DistinctUntilChanged();

            startup.Where(t =>
                t.initializing.HasValue &&
                (t.initializing.Value ||
                (t.IsHomed.HasValue && !t.IsHomed.Value)))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t => Flux.Navigator.Navigate(this));

            startup.Where(t =>
                t.initializing.HasValue &&
                (!t.initializing.Value &&
                (t.IsHomed.HasValue && t.IsHomed.Value)))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => Flux.Navigator.NavigateHome());

            var is_idle = Flux.StatusProvider.IsIdle
                .ValueOrDefault();

            var can_home = Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation)
                .Select(s => s.CanSafeCycle && s.IsHomed.HasValue && !s.IsHomed.Value);

            _ConnectionProgress = Flux.ConnectionProvider.WhenAnyValue(v => v.ConnectionProgress)
                .ToProperty(this, v => v.ConnectionProgress);

            ResetPrinterCommand = ReactiveCommand.Create(ResetPrinter, is_idle);
            StartupCommand = ReactiveCommand.CreateFromTask(StartupAsync, can_home);
            MagazineCommand = ReactiveCommand.Create(MagazineAsync);
            SettingsCommand = ReactiveCommand.Create(SettingsAsync);
        }

        private void SettingsAsync()
        {
            var functionality = Flux.Functionality;
            Flux.Navigator.NavigateModal(functionality);
        }

        private void MagazineAsync()
        {
            var navigate_back = Flux.StatusProvider.ClampClosed.IsValidChanged
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
