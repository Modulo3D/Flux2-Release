using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class DriverStatusBarViewModel : StatusBarItemViewModel<DriverStatusBarViewModel>
    {
        public DriverStatusBarViewModel(FluxViewModel flux) : base(flux)
        {
        }

        protected override IObservable<StatusBarState> GetItemState()
        {
            var connecting = Flux.ConnectionProvider
               .WhenAnyValue(c => c.IsConnecting)
               .DistinctUntilChanged();

            var emergency = Flux.ConnectionProvider.ObserveVariable(m => m.DRIVER_EMERGENCY)
                .ValueOr(() => false)
                .DistinctUntilChanged();

            var driver = Observable.CombineLatest(
                connecting, emergency, (connecting, emergency) => (connecting, emergency));

            driver.Throttle(TimeSpan.FromSeconds(5))
                .Where(p => !p.connecting && p.emergency.HasChange && p.emergency.Change)
                .Subscribe(_ => Flux.Messages.LogMessage("Driver assi", "Assi in emergenza", MessageLevel.EMERG, 38001));

            return driver.Select(d =>
            {
                if (d.connecting)
                    return StatusBarState.Hidden;
                if (!d.emergency.HasChange || !d.emergency.Change)
                    return StatusBarState.Hidden;
                return StatusBarState.Error;
            });
        }
    }
}
