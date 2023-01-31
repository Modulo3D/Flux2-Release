namespace Flux.ViewModels
{
    /*public class NetworksViewModel : FluxRoutableViewModel<NetworkViewModel>
    {
        private NetworkViewModel _SelectedNetwork;
        public NetworkViewModel SelectedNetwork
        {
            get => _SelectedNetwork;
            private set => this.RaiseAndSetIfChanged(ref _SelectedNetwork, value);
        }

        private ISourceList<NetworkViewModel> _Networks = new SourceList<NetworkViewModel>();
        public IObservableList<NetworkViewModel> Networks { get; }

        public WiFiAdapter WiFiAdapter { get; private set; }

        public WiFiReconnectionKind ReconnectionKind { get; set; }

        public ReactiveCommand<NetworkViewModel, Unit> Connect { get; }

        public NetworksViewModel(FluxViewModel flux)
            : base(flux, "network", "RETE", Files.ConnectionIcon)
        {
            ReconnectionKind = WiFiReconnectionKind.Automatic;

            Networks = _Networks.Connect()
                .AsObservableListRC();

            Connect = ReactiveCommand.CreateFromTask<NetworkViewModel>(ConnectAsync);
            Connect.ThrownExceptions.SubscribeRC(ex => { });

            Task.Run(RefreshAsync);
        }

        public async Task ConnectAsync(NetworkViewModel network)
        {
            WiFiAdapter.Disconnect();
            SelectedNetwork = null;

            var password = new PasswordCredential();
            var result = await WiFiAdapter.ConnectAsync(network.Network, ReconnectionKind, password);

            if (result.ConnectionStatus != WiFiConnectionStatus.Success)
            {
                var dialog = new ContentDialog
                {
                    Content = new PasswordBox() { Height = 33 },
                    Title = "Inserisci una password",
                    IsSecondaryButtonEnabled = true,
                    PrimaryButtonText = "Ok",
                    SecondaryButtonText = "Annulla"
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    var users = await User.FindAllAsync();

                    var current = users.Where(p =>
                        p.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated &&
                        p.Type == UserType.LocalUser)
                    .FirstOrDefault();

                    // user may have username
                    var username = (string)await current.GetPropertyAsync(KnownUserProperties.AccountName);

                    password = new PasswordCredential("Flux", username, ((PasswordBox)dialog.Content).Password);
                    result = await WiFiAdapter.ConnectAsync(network.Network, ReconnectionKind, password);
                }
            }

            if (result.ConnectionStatus == WiFiConnectionStatus.Success)
                SelectedNetwork = network;
        }

        public async Task RefreshAsync()
        {
            if (WiFiAdapter == null)
            {
                var access = await WiFiAdapter.RequestAccessAsync();
                if (access != WiFiAccessStatus.Allowed)
                {
                    throw new Exception("WiFiAccessStatus not allowed");
                }
                else
                {
                    var wifiAdapterResults = await DeviceInformation.FindAllAsync(WiFiAdapter.GetDeviceSelector());
                    if (wifiAdapterResults.Count >= 1)
                        WiFiAdapter = await WiFiAdapter.FromIdAsync(wifiAdapterResults[0].Id);
                    else
                        throw new Exception("WiFi Adapter not found.");
                }
            }

            await WiFiAdapter.ScanAsync();
            var _networks = WiFiAdapter.NetworkReport.AvailableNetworks;
            var networks = _networks.Select(net => new NetworkViewModel(this, net));

            _Networks.Clear();
            _Networks.AddRange(networks);

            SelectedNetwork = null;
            var connectedNetwork = await WiFiAdapter.NetworkAdapter.GetConnectedProfileAsync();
            if (connectedNetwork != null)
            {
                var selectedNetwork = _Networks.Items.FirstOrDefault(net => net.Network.Ssid == connectedNetwork.ProfileName);
                SelectedNetwork = selectedNetwork;
            }
        }
    }*/
}
