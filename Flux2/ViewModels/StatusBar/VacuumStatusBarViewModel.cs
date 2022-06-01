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

            var cycle = Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation).Select(s => s.IsCycle)
                .DistinctUntilChanged()
                .StartWith(false);

            var not_found = Flux.StatusProvider.VacuumPresence
                .ConvertToObservable(p => p.ValueChanged)
                .ConvertToObservable(v => double.IsNaN(v.@in.Kpa))
                .DistinctUntilChanged();

            var enabled = Flux.StatusProvider.VacuumPresence
                .ConvertToObservable(c => c.ValueChanged)
                .ConvertToObservable(vacuum => vacuum.@out)
                .DistinctUntilChanged();

            var low = Flux.StatusProvider.VacuumPresence
                .ConvertToObservable(c => c.StateChanged)
                .ConvertToObservable(v => !v.Valid)
                .DistinctUntilChanged();

            var vacuum = Observable.CombineLatest(connecting, cycle, not_found, enabled, low,
                (connecting, cycle, not_found, enabled, low) => (connecting, cycle, not_found, enabled, low))
                .DistinctUntilChanged();

            vacuum.Throttle(TimeSpan.FromSeconds(5))
                .Where(v => v.connecting.HasValue && !v.connecting.Value && v.not_found.HasChange && v.not_found.Change)
                .Subscribe(_ => Flux.Messages.LogMessage("Vuoto", "Sensore del vuoto non trovato", MessageLevel.EMERG, 32001));

            vacuum.Throttle(TimeSpan.FromSeconds(5))
               .Where(v => v.connecting.HasValue && !v.connecting.Value && v.enabled.HasChange && v.enabled.Change && v.low.HasChange && v.low.Change && v.cycle)
               .Subscribe(_ => Flux.Messages.LogMessage("Vuoto", "Vuoto perso durante la lavorazione", MessageLevel.EMERG, 32002));

            vacuum.Throttle(TimeSpan.FromSeconds(60))
                .Where(v => v.connecting.HasValue && !v.connecting.Value && v.enabled.HasChange && v.enabled.Change && v.low.HasChange && v.low.Change)
                .Subscribe(_ => Flux.Messages.LogMessage("Vuoto", "Inserire un foglio", MessageLevel.WARNING, 32003));

            return vacuum.Select(
                vacuum =>
                {
                    if (!vacuum.connecting.HasValue ||
                        vacuum.connecting.Value || 
                        !vacuum.not_found.HasChange ||
                        !vacuum.enabled.HasChange ||
                        !vacuum.low.HasChange)
                        return StatusBarState.Hidden;
                    if (vacuum.not_found.Change)
                        return StatusBarState.Error;
                    if (vacuum.enabled.Change)
                        return vacuum.low.Change ? StatusBarState.Warning : StatusBarState.Stable;
                    return StatusBarState.Disabled;
                });
        }
    }
}