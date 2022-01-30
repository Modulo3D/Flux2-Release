using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class DebugStatusBarViewModel : StatusBarItemViewModel<DebugStatusBarViewModel>
    {
        public DebugStatusBarViewModel(FluxViewModel flux) : base(flux)
        {
        }

        protected override IObservable<StatusBarState> GetItemState()
        {
            var settings = Flux.MCodes.WhenAnyValue(s => s.OperatorUSB);
            return settings.Select(a => a.ConvertOr(o => o.AdvancedSettings, () => false) ? StatusBarState.Stable : StatusBarState.Hidden);
        }
    }
}