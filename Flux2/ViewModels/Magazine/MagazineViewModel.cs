using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class MagazineViewModel : FluxRoutableViewModel<MagazineViewModel>
    {
        [RemoteContent(true)]
        public IObservableCache<MagazineItemViewModel, ushort> Magazine { get; private set; }

        [RemoteCommand]
        public Optional<ReactiveCommandBaseRC<Unit, Unit>> ResetMagazineCommand { get; internal set; }

        public MagazineViewModel(FluxViewModel flux) : base(flux)
        {
            Magazine = Flux.Feeders.Feeders.Connect()
                .Transform(f => new MagazineItemViewModel(Flux, f))
                .AsObservableCacheRC(this);

            var is_idle = Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(s => s.IsIdle);

            if (Flux.ConnectionProvider.VariableStoreBase.HasToolChange)
                ResetMagazineCommand = ReactiveCommandBaseRC.CreateFromTask(async () => { await Flux.ConnectionProvider.ResetMagazineAsync(); }, this, is_idle);

            var top_lock_unit = Flux.ConnectionProvider.GetArrayUnit(m => m.OPEN_LOCK, "top.lock");
            if (Flux.ConnectionProvider.HasVariable(m => m.OPEN_LOCK, top_lock_unit))
                AddCommand("toggleTopLock", ReactiveCommandBaseRC.CreateFromTask(async () => { await Flux.ConnectionProvider.ToggleVariableAsync(c => c.OPEN_LOCK, top_lock_unit); }, this), Unit.Default);
        }
    }
}
