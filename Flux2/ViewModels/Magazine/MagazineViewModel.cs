using DynamicData;
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
        public ReactiveCommand<Unit, Unit> ResetMagazineCommand { get; internal set; }

        public MagazineViewModel(FluxViewModel flux) : base(flux, Observable.Return(true).ToOptional())
        {
            Magazine = Flux.Feeders.Feeders.Connect()
                .Transform(f => new MagazineItemViewModel(Flux, f))
                .AsObservableCacheRC(this);

            var is_idle = Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(s => s.IsIdle);

            ResetMagazineCommand = ReactiveCommandRC.CreateFromTask(async () => { await Flux.ConnectionProvider.ResetMagazineAsync(); }, this, is_idle);

            var top_lock_unit = Flux.ConnectionProvider.GetArrayUnit(m => m.LOCK_CLOSED, "top");
            if (Flux.ConnectionProvider.HasVariable(m => m.LOCK_CLOSED, top_lock_unit))
                AddCommand("toggleTopLock", ReactiveCommandRC.CreateFromTask(async () => { await Flux.ConnectionProvider.ToggleVariableAsync(c => c.OPEN_LOCK, top_lock_unit); }, this));
        }
    }
}
