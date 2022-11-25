using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;

namespace Flux.ViewModels
{
    public class WebcamViewModel : FluxRoutableViewModel<WebcamViewModel>
    {
        private ObservableAsPropertyHelper<Optional<string>> _WebcamAddress;
        [RemoteOutput(true)]
        public Optional<string> WebcamAddress => _WebcamAddress.Value;

        public WebcamViewModel(FluxViewModel flux) : base(flux)
        {
            var settings = flux.SettingsProvider.CoreSettings.Local;
            _WebcamAddress = settings.WhenAnyValue(v => v.WebcamAddress)
                .ToProperty(this, v => v.WebcamAddress);
        }
    }
}
