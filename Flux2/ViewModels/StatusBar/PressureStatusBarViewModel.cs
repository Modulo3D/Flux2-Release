using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Text;

namespace Flux.ViewModels
{
    public class PressureStatusBarViewModel : StatusBarItemViewModel<PressureStatusBarViewModel>
    {
        public PressureStatusBarViewModel(FluxViewModel flux) : base(flux)
        {
        }

        protected override IObservable<StatusBarState> GetItemState()
        {
            var connecting = Flux.ConnectionProvider
                .WhenAnyValue(c => c.IsConnecting)
                .DistinctUntilChanged()
                .StartWith(true);

            var not_found = Flux.StatusProvider.PressurePresence
                .ConvertToObservable(c => c.ValueChanged)
                .ConvertToObservable(v => double.IsNaN(v.@in.Kpa))
                .DistinctUntilChanged();

            var low = Flux.StatusProvider.PressurePresence
                .ConvertToObservable(c => c.StateChanged)
                .ConvertToObservable(s => !s.Valid)
                .DistinctUntilChanged();

            var pressure = Observable.CombineLatest(connecting, not_found, low,
                (connecting, not_found, low) => (connecting, not_found, low))
                .DistinctUntilChanged();

            pressure.Throttle(TimeSpan.FromSeconds(5))
                .Where(p => p.connecting.HasValue && !p.connecting.Value && p.not_found.HasChange && p.not_found.Change)
                .Subscribe(_ => Flux.Messages.LogMessage("Pressione", "Sensore della pressione non trovato", MessageLevel.EMERG, 28001));

            pressure.Throttle(TimeSpan.FromSeconds(5))
                .Where(p => p.connecting.HasValue && !p.connecting.Value && p.low.HasChange && p.low.Change)
                .Subscribe(_ => Flux.Messages.LogMessage("Pressione", "Livello di pressione troppo basso", MessageLevel.EMERG, 28002));

            return pressure.Select(
                pressure =>
                {
                    if (!pressure.connecting.HasValue || pressure.connecting.Value || !pressure.not_found.HasChange || !pressure.low.HasChange)
                        return StatusBarState.Hidden;
                    if (pressure.not_found.HasChange && pressure.not_found.Change)
                        return StatusBarState.Error;
                    if (pressure.low.HasChange && pressure.low.Change)
                        return StatusBarState.Error;
                    return StatusBarState.Stable;
                });
        }
    }
}