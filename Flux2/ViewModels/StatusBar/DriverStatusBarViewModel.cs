using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

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

            driver.Throttle(TimeSpan.FromSeconds(1))
                .Where(p => p.connecting.HasValue && !p.connecting.Value && p.emergency)
                .Subscribe(_ => Flux.Messages.LogMessage("Driver assi", "Assi in emergenza", MessageLevel.EMERG, 38001));

            return driver.Select(d =>
            {
                if (!d.connecting.HasValue || d.connecting.Value)
                    return StatusBarState.Hidden;
                if (!d.emergency)
                    return StatusBarState.Hidden;
                return StatusBarState.Error;
            });
        }
    }
}
