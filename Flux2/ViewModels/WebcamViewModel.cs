using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
