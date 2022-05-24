using DynamicData;
using DynamicData.Binding;
using Modulo3DStandard;
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
                .AsObservableCache();

            var is_idle = Flux.StatusProvider.IsIdle
                .ValueOrDefault();

            ResetMagazineCommand = ReactiveCommand.CreateFromTask(async () => { await Flux.SettingsProvider.ResetMagazineAsync(); }, is_idle);

            if (Flux.ConnectionProvider.HasVariable(m => m.OPEN_HEAD_CLAMP))
                AddCommand("toggleClamp", ReactiveCommand.CreateFromTask(async () => { await Flux.ConnectionProvider.ToggleVariableAsync(c => c.OPEN_HEAD_CLAMP); }));

            if (Flux.ConnectionProvider.HasVariable(m => m.LOCK_CLOSED, "top"))
                AddCommand("toggleTopLock", ReactiveCommand.CreateFromTask(async () => { await Flux.ConnectionProvider.ToggleVariableAsync(c => c.OPEN_LOCK, "top"); }));
        }
    }
}
