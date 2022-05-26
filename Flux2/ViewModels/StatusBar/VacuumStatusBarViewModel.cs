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

            var cycle = Flux.StatusProvider.IsCycle
                .DistinctUntilChanged()
                .StartWith(false);

            var not_found = Flux.StatusProvider.VacuumPresence.Value.ValueChanged
                .Select(v => double.IsNaN(v.@in.Kpa))
                .DistinctUntilChanged();

            var enabled = Flux.StatusProvider.VacuumPresence.Value.ValueChanged
                .Select(vacuum => vacuum.@out)
                .DistinctUntilChanged();

            var low = Flux.StatusProvider.VacuumPresence.Value.StateChanged
                .Select(v => !v.Valid)
                .DistinctUntilChanged()
                .StartWith(false);

            var vacuum = Observable.CombineLatest(connecting, cycle, not_found, enabled, low,
                (connecting, cycle, not_found, enabled, low) => (connecting, cycle, not_found, enabled, low))
                .DistinctUntilChanged();

            vacuum.Throttle(TimeSpan.FromSeconds(5))
                .Where(v => v.connecting.HasValue && !v.connecting.Value && v.not_found)
                .Subscribe(_ => Flux.Messages.LogMessage("Vuoto", "Sensore del vuoto non trovato", MessageLevel.EMERG, 32001));

            vacuum.Throttle(TimeSpan.FromSeconds(5))
               .Where(v => v.connecting.HasValue && !v.connecting.Value && v.enabled && v.low && v.cycle.HasValue && v.cycle.Value)
               .Subscribe(_ => Flux.Messages.LogMessage("Vuoto", "Vuoto perso durante la lavorazione", MessageLevel.EMERG, 32002));

            vacuum.Throttle(TimeSpan.FromSeconds(60))
                .Where(v => v.connecting.HasValue && !v.connecting.Value && v.enabled && v.low)
                .Subscribe(_ => Flux.Messages.LogMessage("Vuoto", "Inserire un foglio", MessageLevel.WARNING, 32003));

            return vacuum.Select(
                vacuum =>
                {
                    if (!vacuum.connecting.HasValue || vacuum.connecting.Value)
                        return StatusBarState.Hidden;
                    if (vacuum.not_found)
                        return StatusBarState.Error;
                    if (vacuum.enabled)
                        return vacuum.low ? StatusBarState.Warning : StatusBarState.Stable;
                    return StatusBarState.Disabled;
                });
        }
    }
}