using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class LocksStatusBarViewModel : StatusBarItemViewModel<LocksStatusBarViewModel>
    {
        public LocksStatusBarViewModel(FluxViewModel flux) : base(flux)
        {
        }

        protected override IObservable<StatusBarState> GetItemState()
        {
            var connecting = Flux.ConnectionProvider
                .WhenAnyValue(c => c.IsConnecting)
                .DistinctUntilChanged()
                .StartWith(true);

            var in_mateinance = Flux.Feeders.Feeders.Connect()
                .TrueForAny(f => f.ToolNozzle.WhenAnyValue(t => t.InMaintenance), m => m)
                .DistinctUntilChanged()
                .StartWith(false);

            var cycle = Flux.StatusProvider.IsCycle
                .DistinctUntilChanged()
                .StartWith(false);

            var top = Flux.StatusProvider.TopLockClosed.ValueChanged
                .DistinctUntilChanged();

            var chamber = Flux.StatusProvider.ChamberLockClosed.ValueChanged
                .DistinctUntilChanged();

            var debug = Flux.ConnectionProvider.ObserveVariable(m => m.DEBUG)
                .ValueOr(() => false)
                .StartWith(false)
                .DistinctUntilChanged();

            var locks = Observable.CombineLatest(connecting, in_mateinance, cycle, top, chamber, debug,
                (connecting, in_mateinance, cycle, top, chamber, debug) => (connecting, in_mateinance, cycle, top, chamber, debug))
                .DistinctUntilChanged();

            locks.Throttle(TimeSpan.FromSeconds(5))
                .Where(l => l.connecting.HasValue && !l.connecting.Value && l.cycle.HasValue && l.cycle.Value && !l.debug && (l.top.HasValue && !l.top.Value.@in || l.chamber.HasValue && !l.chamber.Value.@in))
                .Subscribe(_ => Flux.Messages.LogMessage("Portella", "Portella aperta durante operazione", MessageLevel.EMERG, 31001));

            locks.Throttle(TimeSpan.FromSeconds(60))
                .Where(l => l.connecting.HasValue && !l.connecting.Value && !l.in_mateinance && l.cycle.HasValue && l.cycle.Value && (l.top.HasValue && !l.top.Value.@in || l.chamber.HasValue && !l.chamber.Value.@in))
                .Subscribe(_ => Flux.Messages.LogMessage("Portella", "Chiudere la portella", MessageLevel.WARNING, 31002));

            return locks.Select(
                locks =>
                {
                    if (!locks.connecting.HasValue || locks.connecting.Value || !locks.cycle.HasValue || (!locks.chamber.HasValue && !locks.chamber.HasValue))
                        return StatusBarState.Hidden;
                    if (locks.top.HasValue && !locks.top.Value.@in || locks.chamber.HasValue && !locks.chamber.Value.@in)
                        return locks.cycle.Value ? StatusBarState.Error : StatusBarState.Warning;
                    if (locks.top.HasValue && locks.top.Value.@out || locks.chamber.HasValue && locks.chamber.Value.@out)
                        return locks.cycle.Value ? StatusBarState.Error : StatusBarState.Disabled;
                    return StatusBarState.Stable;
                });
        }
    }
}
