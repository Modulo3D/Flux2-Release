using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class RecoveryViewModel : HomePhaseViewModel<RecoveryViewModel>
    {
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> LoadRecoveryCommand { get; }

        private double _RecoveryProgress = 0;
        [RemoteOutput(true)]
        public double RecoveryProgress
        {
            get => _RecoveryProgress;
            set => this.RaiseAndSetIfChanged(ref _RecoveryProgress, value);
        }

        public RecoveryViewModel(FluxViewModel flux) : base(flux, "recovery")
        {
            var is_idle = Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(s => s.IsIdle);

            var has_recovery = Flux.StatusProvider
                .WhenAnyValue(v => v.PrintingEvaluation)
                .Select(e => e.Recovery.HasValue);

            var can_recovery = Observable.CombineLatest(has_recovery, is_idle, (r, i) => r && i);

            LoadRecoveryCommand = ReactiveCommand.CreateFromTask(LoadRecoveryAsync, can_recovery);
        }

        public async Task LoadRecoveryAsync()
        {
            var recovery = Flux.StatusProvider.PrintingEvaluation.Recovery;
            if (!recovery.HasValue)
                return;

            var mcode_vm = Flux.MCodes.AvaiableMCodes.Lookup(recovery.Value.MCodeGuid);
            if (!mcode_vm.HasValue)
                return;

            await Flux.MCodes.PrepareMCodeAsync(mcode_vm.Value, true, p => RecoveryProgress = p);
        }
    }
}
