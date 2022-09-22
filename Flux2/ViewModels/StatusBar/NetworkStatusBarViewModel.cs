using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class NetworkStatusBarViewModel : StatusBarItemViewModel<NetworkStatusBarViewModel>
    {
        public NetworkStatusBarViewModel(FluxViewModel flux) : base(flux)
        {
        }

        protected override IObservable<StatusBarState> GetItemState()
        {
            var plc = Flux.NetProvider
                .WhenAnyValue(v => v.PLCNetworkConnectivity)
                .DistinctUntilChanged()
                .StartWith(true);

            var inter = Flux.NetProvider
                .WhenAnyValue(v => v.InterNetworkConnectivity)
                .DistinctUntilChanged()
                .StartWith(true);

            var network = Observable.CombineLatest(plc, inter,
              (plc, inter) => (plc, inter))
              .DistinctUntilChanged();

            network.Throttle(TimeSpan.FromSeconds(5))
                .Where(n => !n.plc)
                .Subscribe(_ => Flux.Messages.LogMessage("Connessione", "Impossibile raggiungere il plc", MessageLevel.INFO, 21001));

            network.Throttle(TimeSpan.FromSeconds(5))
                .Where(n => !n.inter)
                .Subscribe(_ => Flux.Messages.LogMessage("Connessione", "Impossibile connettersi a internet", MessageLevel.INFO, 21002));

            return network.Select(
                network =>
                {
                    if (!network.plc)
                        return StatusBarState.Error;
                    if (!network.inter)
                        return StatusBarState.Warning;
                    return StatusBarState.Stable;
                });
        }
    }
}