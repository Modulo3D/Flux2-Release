namespace Flux.ViewModels
{
    /*public class NetworkViewModel : ReactiveObjectRC
    {
        static NetworkViewModel()
        {
            Locator.CurrentMutable.Register(() => new NetworkView(), typeof(IViewFor<NetworkViewModel>));
        }

        public WiFiAvailableNetwork Network { get; }
        public ReactiveCommandBaseRC<Unit, Unit> Connect { get; }

        public NetworkViewModel(NetworksViewModel networks, WiFiAvailableNetwork network)
        {
            Network = network;
            Connect = ReactiveCommandBaseRC.CreateFromTask(async () => await networks.Connect.Execute(this));
        }
    }*/
}
