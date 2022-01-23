using Modulo3DDatabase;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Drawing;
using System.IO;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public interface IStatusBarItemViewModel : IRemoteControl
    {
        int Notifies { get; }
        FluxViewModel Flux { get; }
        StatusBarState State { get; }
    }

    public abstract class StatusBarItemViewModel<TViewModel> : RemoteControl<StatusBarItemViewModel<TViewModel>>, IStatusBarItemViewModel
        where TViewModel : StatusBarItemViewModel<TViewModel>
    {
        public FluxViewModel Flux { get; }

        public ObservableAsPropertyHelper<int> _Notifies;
        public int Notifies => _Notifies.Value;

        public ObservableAsPropertyHelper<StatusBarState> _State;
        public StatusBarState State => _State.Value;

        public ObservableAsPropertyHelper<string> _StateBrush;
        [RemoteOutput(true)]
        public string StateBrush => _StateBrush.Value;

        public StatusBarItemViewModel(FluxViewModel flux) : base($"statusBarItem??{typeof(TViewModel).GetRemoteControlName().Replace("StatusBar", "")}")
        {
            Flux = flux;
            _Notifies = GetItemNotifies()
                .StartWith(0)
                .ToProperty(this, v => v.Notifies);

            _State = GetItemState()
                .StartWith(StatusBarState.Disabled)
                .ToProperty(this, v => v.State);

            _StateBrush = this.WhenAnyValue(v => v.State)
                .Select(state => state switch
                {
                    StatusBarState.Error => FluxColors.Error,
                    StatusBarState.Stable => FluxColors.Active,
                    StatusBarState.Warning => FluxColors.Warning,
                    StatusBarState.Disabled => FluxColors.Empty,
                    _ => FluxColors.Error
                })
                .ToProperty(this, v => v.StateBrush);
        }

        protected abstract IObservable<StatusBarState> GetItemState();
        protected IObservable<int> GetItemNotifies() => Observable.Return(0);
    }
}
