using DynamicData;
using Modulo3DStandard;
using ReactiveUI;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class PreparePrintViewModel : HomePhaseViewModel<PreparePrintViewModel>
    {
        [RemoteContent(true)]
        public ISourceList<IConditionViewModel> Conditions { get; private set; }

        private ObservableAsPropertyHelper<bool> _HasSafeStart;
        [RemoteOutput(true)]
        public bool HasSafeStart => _HasSafeStart?.Value ?? false;

        public PreparePrintViewModel(FluxViewModel flux) : base(flux)
        {
            Conditions = new SourceList<IConditionViewModel>();

            _HasSafeStart = Conditions.Connect()
                .AddKey(c => c.Name)
                .AutoRefresh(c => c.State)
                .Filter(c => c.State.Valid)
                .TrueForAll(line => line.StateChanged, state => state.Valid)
                .StartWith(true)
                .ToProperty(this, e => e.HasSafeStart);
        }

        public override void Initialize()
        {
            if (Flux.StatusProvider.VacuumPresence.HasValue)
                Conditions.Add(Flux.StatusProvider.VacuumPresence.Value);
            // TODO
            if (Flux.StatusProvider.TopLockClosed.HasValue)
                Conditions.Add(Flux.StatusProvider.TopLockClosed.Value);
            if (Flux.StatusProvider.ChamberLockClosed.HasValue)
                Conditions.Add(Flux.StatusProvider.ChamberLockClosed.Value);

            InitializeRemoteView();
        }
    }
}
