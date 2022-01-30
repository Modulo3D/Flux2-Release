using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Text;

namespace Flux.ViewModels
{
    public class VacuumStatusBarViewModel : StatusBarItemViewModel<VacuumStatusBarViewModel>
    {
        public VacuumStatusBarViewModel(FluxViewModel flux) : base(flux)
        {
        }

        protected override IObservable<StatusBarState> GetItemState()
        {
            var connecting = Flux.ConnectionProvider
                .WhenAnyValue(c => c.IsConnecting)
                .DistinctUntilChanged()
                .StartWith(true);

            var watch = Flux.ConnectionProvider.ObserveVariable(m => m.WATCH_VACUUM)
                .DistinctUntilChanged()
                .StartWithEmpty();

            var not_found = Flux.StatusProvider.VacuumPresence.ValueChanged
                .Convert(v => float.IsNaN(v.@in.pressure.Kpa))
                .DistinctUntilChanged()
                .StartWithEmpty();

            var enabled = Flux.StatusProvider.VacuumPresence.ValueChanged
                .Convert(vacuum => vacuum.@out)
                .DistinctUntilChanged()
                .StartWithEmpty();

            var low = Flux.StatusProvider.VacuumPresence.IsValidChanged
                .Convert(v => !v)
                .DistinctUntilChanged()
                .StartWithEmpty();

            var vacuum = Observable.CombineLatest(connecting, watch, not_found, enabled, low,
                (connecting, watch, not_found, enabled, low) => (connecting, watch, not_found, enabled, low))
                .DistinctUntilChanged();

            vacuum.Throttle(TimeSpan.FromSeconds(1))
                .Where(v => v.connecting.HasValue && !v.connecting.Value && v.not_found.HasValue && v.not_found.Value)
                .Subscribe(_ => Flux.Messages.LogMessage("Vuoto", "Sensore del vuoto non trovato", MessageLevel.EMERG, 32001));

            vacuum.Throttle(TimeSpan.FromSeconds(1))
               .Where(v => v.connecting.HasValue && !v.connecting.Value && v.low.HasValue && v.low.Value && v.watch.HasValue && v.watch.Value)
               .Subscribe(_ => Flux.Messages.LogMessage("Vuoto", "Vuoto perso durante la lavorazione", MessageLevel.EMERG, 32002));

            vacuum.Throttle(TimeSpan.FromSeconds(60))
                .Where(v => v.connecting.HasValue && !v.connecting.Value && v.enabled.HasValue && v.enabled.Value && v.low.HasValue && v.low.Value && v.watch.HasValue && !v.watch.Value)
                .Subscribe(_ => Flux.Messages.LogMessage("Vuoto", "Inserire un foglio", MessageLevel.WARNING, 32003));

            return vacuum.Select(
                vacuum =>
                {
                    if (!vacuum.connecting.HasValue || vacuum.connecting.Value || !vacuum.watch.HasValue)
                        return StatusBarState.Hidden;
                    if (vacuum.not_found.HasValue && vacuum.not_found.Value)
                        return StatusBarState.Error;
                    if (vacuum.watch.HasValue && vacuum.watch.Value)
                        return vacuum.low.HasValue ? (vacuum.low.Value ? StatusBarState.Error : StatusBarState.Stable) : StatusBarState.Hidden;
                    if (vacuum.enabled.HasValue && vacuum.enabled.Value)
                        return vacuum.low.HasValue ? (vacuum.low.Value ? StatusBarState.Warning : StatusBarState.Stable) : StatusBarState.Hidden;
                    return StatusBarState.Disabled;
                });
        }
    }
}