using DynamicData;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public interface IStatusBarItemViewModel : IRemoteControl
    {
        int Notifies { get; }
        FluxViewModel Flux { get; }
        StatusBarState State { get; }
    }

    public class StatusBarItemViewModel : RemoteControl<StatusBarItemViewModel>, IStatusBarItemViewModel
    {
        public FluxViewModel Flux { get; }

        public ObservableAsPropertyHelper<int> _Notifies;
        public int Notifies => _Notifies.Value;

        public ObservableAsPropertyHelper<StatusBarState> _State;
        public StatusBarState State => _State.Value;

        public ObservableAsPropertyHelper<string> _StateBrush;
        [RemoteOutput(true)]
        public string StateBrush => _StateBrush.Value;

        public StatusBarItemViewModel(FluxViewModel flux, string condition, IEnumerable<IConditionViewModel> conditions) 
            : base($"{typeof(StatusBarItemViewModel).GetRemoteElementClass()}.{condition}")
        {
            Flux = flux;
            _Notifies = GetItemNotifies()
                .StartWith(0)
                .ToProperty(this, v => v.Notifies);

            _State = conditions
                .AsObservableChangeSet(t => t.Name)
                .AutoTransform(c => c.State)
                .QueryWhenChanged(s =>
                {
                    if (s.Items.Any(s => s.State == EConditionState.Error))
                        return StatusBarState.Error;
                    if (s.Items.Any(s => s.State == EConditionState.Warning))
                        return StatusBarState.Warning;
                    if (s.Items.All(s => s.State == EConditionState.Stable))
                        return StatusBarState.Stable;
                    if (s.Items.All(s => s.State == EConditionState.Disabled))
                        return StatusBarState.Disabled;
                    if (s.Items.All(s => s.State == EConditionState.Hidden))
                        return StatusBarState.Hidden;
                    if (s.Items.All(s => s.State == EConditionState.Idle))
                        return StatusBarState.Idle;
                    return StatusBarState.Hidden;
                })
                .StartWith(StatusBarState.Disabled)
                .ToProperty(this, v => v.State);

            _StateBrush = this.WhenAnyValue(v => v.State)
                .Select(state => state switch
                {
                    StatusBarState.Idle => FluxColors.Idle,
                    StatusBarState.Error => FluxColors.Error,
                    StatusBarState.Stable => FluxColors.Active,
                    StatusBarState.Warning => FluxColors.Warning,
                    StatusBarState.Disabled => FluxColors.Empty,
                    _ => FluxColors.Error
                })
                .ToProperty(this, v => v.StateBrush);
        }
        protected IObservable<int> GetItemNotifies() => Observable.Return(0);
    }
}
