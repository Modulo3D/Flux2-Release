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

        public PreparePrintViewModel(FluxViewModel flux) : base(flux, "prepare")
        {
            Conditions = new SourceList<IConditionViewModel>();

            _HasSafeStart = Conditions.Connect()
                .AddKey(c => c.Label)
                .TrueForAll(line => line.IsValidChanged, valid => valid.HasValue && valid.Value)
                .StartWith(true)
                .ToProperty(this, e => e.HasSafeStart);
        }

        public override void Initialize()
        {
            // TODO
            if (Flux.ConnectionProvider.VariableStore.HasVariable(m => m.LOCK_CLOSED))
            { 
                Conditions.Add(Flux.StatusProvider.TopLockClosed);
                Conditions.Add(Flux.StatusProvider.ChamberLockClosed);
            }

            if(Flux.ConnectionProvider.VariableStore.HasVariable(m => m.VACUUM_PRESENCE))
                Conditions.Add(Flux.StatusProvider.VacuumPresence);

            InitializeRemoteView();
        }
    }
}
