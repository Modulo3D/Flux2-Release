using DynamicData;
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
    public class FeedersStatusBarViewModel : StatusBarItemViewModel<FeedersStatusBarViewModel>
    {
        public FeedersStatusBarViewModel(FluxViewModel flux) : base(flux)
        {
        }

        protected override IObservable<StatusBarState> GetItemState()
        {
            var connecting = Flux.ConnectionProvider
                .WhenAnyValue(c => c.IsConnecting)
                .StartWith(true)
                .DistinctUntilChanged();

            var in_mateinance = Flux.Feeders.Feeders.Connect()
                .TrueForAny(f => f.ToolNozzle.WhenAnyValue(t => t.InMaintenance), m => m)
                .DistinctUntilChanged()
                .StartWith(false);

            var error = Flux.Feeders.Feeders.Connect()
                .TrueForAny(f => f.WhenAnyValue(f => f.ToolNozzle.State), s =>
                    s.IsNotLoaded() ? false : !s.IsInMagazine() && !s.IsOnTrailer())
                .DistinctUntilChanged()
                .StartWith(false);

            var not_found = Flux.Feeders.WhenAnyValue(v => v.SelectedFeeder)
                   .ConvertMany(f => f.WhenAnyValue(f => f.ToolNozzle.Temperature))
                   .Select(t =>  t.ConvertOr(t => t.Current > 1000, () => false))
                   .DistinctUntilChanged()
                   .StartWith(false);

            var hot = Flux.Feeders.WhenAnyValue(v => v.SelectedFeeder)
                .ConvertMany(f => f.WhenAnyValue(f => f.ToolNozzle.Temperature))
                .Select(t => t.ConvertOr(t =>
                t.Current > 50, () => false))
                .DistinctUntilChanged()
                .StartWith(false);

            var on = Flux.Feeders.WhenAnyValue(v => v.SelectedFeeder)
                .ConvertMany(f => f.WhenAnyValue(f => f.ToolNozzle.Temperature))
                .Select(t => t.ConvertOr(t => t.Target > 0, () => false))
                .DistinctUntilChanged()
                .StartWith(false);

            var open = Flux.StatusProvider.ChamberLockClosed.ValueChanged
                .ConvertOr(t => !t.@in, () => false)
                .DistinctUntilChanged()
                .StartWith(false);

            var tool = Observable.CombineLatest(connecting, in_mateinance, error, not_found, hot, on, open,
                (connecting, in_mateinance, error, not_found, hot, on, open) => (connecting, in_mateinance, error, not_found, hot, on, open))
                .DistinctUntilChanged();

            tool.Throttle(TimeSpan.FromSeconds(1))
                .Where(t => t.connecting.HasValue && !t.connecting.Value && !t.in_mateinance && t.not_found)
                .Subscribe(_ => Flux.Messages.LogMessage("Utensile", "Sensore di temperatura non trovato", MessageLevel.EMERG, 27001));

            tool.Throttle(TimeSpan.FromSeconds(1))
                .Where(t => t.connecting.HasValue && !t.connecting.Value && !t.in_mateinance && t.error)
                .Subscribe(_ => Flux.Messages.LogMessage("Utensile", "Stato utensile non corretto", MessageLevel.ERROR, 27002));

            tool.Throttle(TimeSpan.FromSeconds(1))
                .Where(t => t.connecting.HasValue && !t.connecting.Value && !t.in_mateinance && !t.not_found && t.hot && t.open)
                .Subscribe(_ => Flux.Messages.LogMessage("Utensile", "Temperatura dell'utensile elevata", MessageLevel.WARNING, 27003));

            return tool.Select(
                tool =>
                {
                    if (!tool.connecting.HasValue || tool.connecting.Value)
                        return StatusBarState.Hidden;
                    if (tool.error)
                        return StatusBarState.Error;
                    if (tool.hot)
                        return StatusBarState.Warning;
                    if (tool.on)
                        return StatusBarState.Stable;
                    return StatusBarState.Disabled;
                });
        }
    }
}
