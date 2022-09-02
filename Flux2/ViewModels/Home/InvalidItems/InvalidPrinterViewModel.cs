using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class InvalidPrinterViewModel : HomePhaseViewModel<InvalidPrinterViewModel>
    {
        public InvalidPrinterViewModel(FluxViewModel flux) : base(flux)
        {
        }
    }
}
