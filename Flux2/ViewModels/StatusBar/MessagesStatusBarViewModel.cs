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
    public class MessagesStatusBarViewModel : StatusBarItemViewModel<MessagesStatusBarViewModel>
    {
        public MessagesStatusBarViewModel(FluxViewModel flux) : base(flux)
        {
        }

        protected override IObservable<StatusBarState> GetItemState()
        {
            return Flux.Messages
                .WhenAnyValue(v => v.MessageCounter)
                .Select(counter =>
                {
                    if (counter.EmergencyMessagesCount > 0)
                        return StatusBarState.Error;
                    if (counter.ErrorMessagesCount > 0)
                        return StatusBarState.Warning;
                    if (counter.WarningMessagesCount > 0)
                        return StatusBarState.Warning;
                    if (counter.InfoMessagesCount > 0)
                        return StatusBarState.Stable;
                    return StatusBarState.Disabled;
                });
        }
    }
}