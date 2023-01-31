namespace Flux.ViewModels
{
    /*public class NetworkViewModel : ReactiveObjectRC
    {
        static NetworkViewModel()
        {
            Locator.CurrentMutable.Register(() => new NetworkView(), typeof(IViewFor<NetworkViewModel>));
        }

        public WiFiAvailableNetwork Network { get; }
        public ReactiveCommand<Unit, Unit> Connect { get; }

        public NetworkViewModel(NetworksViewModel networks, WiFiAvailableNetwork network)
        {
            Network = network;
            Connect = ReactiveCommand.CreateFromTask(async () => await networks.Connect.Execute(this));
        }
    }*/
}
