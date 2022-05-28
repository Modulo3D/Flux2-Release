using DynamicData;
using Modulo3DStandard;
using ReactiveUI;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class PreparePrintViewModel : HomePhaseViewModel<PreparePrintViewModel>
    {
        [RemoteContent(true)]
        public ISourceCache<IConditionViewModel, string> Conditions { get; private set; }

        private ObservableAsPropertyHelper<bool> _HasSafeStart;
        [RemoteOutput(true)]
        public bool HasSafeStart => _HasSafeStart?.Value ?? false;

        public PreparePrintViewModel(FluxViewModel flux) : base(flux)
        {
            Conditions = new SourceCache<IConditionViewModel, string>(c => c.Name);

            _HasSafeStart = Conditions.Connect()
                .AutoRefresh(c => c.State)
                .Filter(c => c.State.Valid)
                .TrueForAll(line => line.StateChanged, state => state.Valid)
                .StartWith(true)
                .ToProperty(this, e => e.HasSafeStart);
        }

        public override void Initialize()
        {
            if (Flux.StatusProvider.VacuumPresence.HasValue)
                Conditions.AddOrUpdate(Flux.StatusProvider.VacuumPresence.Value);

            // TODO
            if (Flux.StatusProvider.TopLockClosed.HasValue)
                Conditions.AddOrUpdate(Flux.StatusProvider.TopLockClosed.Value);
            if (Flux.StatusProvider.ChamberLockClosed.HasValue)
                Conditions.AddOrUpdate(Flux.StatusProvider.ChamberLockClosed.Value);

            InitializeRemoteView();
        }
    }
}
