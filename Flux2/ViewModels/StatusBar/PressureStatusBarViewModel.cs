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

            var not_found = Flux.StatusProvider.PressurePresence.ValueChanged
                .Convert(v => float.IsNaN(v.pressure.Kpa))
                .DistinctUntilChanged()
                .StartWithEmpty();

            var low = Flux.StatusProvider.PressurePresence.StateChanged
                .Select(s => s.Valid)
                .Convert(v => !v)
                .DistinctUntilChanged()
                .StartWithEmpty();

            var pressure = Observable.CombineLatest(connecting, not_found, low,
                (connecting, not_found, low) => (connecting, not_found, low))
                .DistinctUntilChanged();

            pressure.Throttle(TimeSpan.FromSeconds(5))
                .Where(p => p.connecting.HasValue && !p.connecting.Value && p.not_found.HasValue && p.not_found.Value)
                .Subscribe(_ => Flux.Messages.LogMessage("Pressione", "Sensore della pressione non trovato", MessageLevel.EMERG, 28001));

            pressure.Throttle(TimeSpan.FromSeconds(5))
                .Where(p => p.connecting.HasValue && !p.connecting.Value && p.low.HasValue && p.low.Value)
                .Subscribe(_ => Flux.Messages.LogMessage("Pressione", "Livello di pressione troppo basso", MessageLevel.EMERG, 28002));

            return pressure.Select(
                pressure =>
                {
                    if (!pressure.connecting.HasValue || pressure.connecting.Value || !pressure.not_found.HasValue || !pressure.low.HasValue)
                        return StatusBarState.Hidden;
                    if (pressure.not_found.HasValue && pressure.not_found.Value)
                        return StatusBarState.Error;
                    if (pressure.low.HasValue && pressure.low.Value)
                        return StatusBarState.Error;
                    return StatusBarState.Stable;
                });
        }
    }
}