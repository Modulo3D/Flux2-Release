using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class ChamberStatusBarViewModel : StatusBarItemViewModel<ChamberStatusBarViewModel>
    {
        public ChamberStatusBarViewModel(FluxViewModel flux) : base(flux)
        {
        }

        protected override IObservable<StatusBarState> GetItemState()
        {
            var connecting = Flux.ConnectionProvider
                .WhenAnyValue(c => c.IsConnecting)
                .DistinctUntilChanged()
                .StartWith(true);

            var temp = Flux.ConnectionProvider
                .ObserveVariable(f => f.TEMP_CHAMBER)
                .DistinctUntilChanged();

            var open = Flux.StatusProvider.ChamberLockClosed.ValueChanged
                .ConvertOr(t => !t.@in, () => false)
                .DistinctUntilChanged()
                .StartWith(false);

            var chamber = Observable.CombineLatest(connecting, temp, open,
                (connecting, temp, open) => (connecting, temp, open))
                .DistinctUntilChanged();

            chamber.Throttle(TimeSpan.FromSeconds(5))
                .Where(c => c.connecting.HasValue && !c.connecting.Value && c.temp.HasValue && c.temp.Value.IsDisconnected)
                .Subscribe(_ => Flux.Messages.LogMessage("Camera calda", "Sensore di temperatura non trovato", MessageLevel.EMERG, 29001));

            chamber.Throttle(TimeSpan.FromSeconds(5))
                .Where(c => c.connecting.HasValue && !c.connecting.Value && c.temp.HasValue && c.temp.Value.IsHot && c.open)
                .Subscribe(_ => Flux.Messages.LogMessage("Camera calda", "Temperatura della camera elevata", MessageLevel.WARNING, 29002));

            return chamber.Select(
                chamber =>
                {
                    if (!chamber.connecting.HasValue || chamber.connecting.Value || !chamber.temp.HasValue)
                        return StatusBarState.Hidden;
                    if (chamber.temp.Value.IsDisconnected)
                        return StatusBarState.Error;
                    if (chamber.temp.Value.IsHot)
                        return StatusBarState.Warning;
                    if (chamber.temp.Value.IsOn)
                        return StatusBarState.Stable;
                    return StatusBarState.Disabled;
                });
        }
    }

    public class PlateStatusBarViewModel : StatusBarItemViewModel<PlateStatusBarViewModel>
    {
        public PlateStatusBarViewModel(FluxViewModel flux) : base(flux)
        {
        }
        protected override IObservable<StatusBarState> GetItemState()
        {
            var connecting = Flux.ConnectionProvider
                .WhenAnyValue(c => c.IsConnecting)
                .DistinctUntilChanged();

            var temp = Flux.ConnectionProvider
                .ObserveVariable(f => f.TEMP_PLATE)
                .DistinctUntilChanged();

            var open = Flux.StatusProvider.ChamberLockClosed.ValueChanged
                .ConvertOr(t => !t.@in, () => false)
                .DistinctUntilChanged()
                .StartWith(false);

            var plate = Observable.CombineLatest(connecting, temp, open,
               (connecting, temp, open) => (connecting, temp, open))
                .DistinctUntilChanged();

            plate.Throttle(TimeSpan.FromSeconds(1))
                .Where(p => p.connecting.HasValue && !p.connecting.Value && p.temp.HasValue && p.temp.Value.IsDisconnected)
                .Subscribe(_ => Flux.Messages.LogMessage("Piatto caldo", "Sensore di temperatura non trovato", MessageLevel.EMERG, 30001));

            plate.Throttle(TimeSpan.FromSeconds(1))
                .Where(p => p.connecting.HasValue && !p.connecting.Value && p.temp.HasValue && p.temp.Value.IsHot && p.open)
                .Subscribe(_ => Flux.Messages.LogMessage("Piatto caldo", "Temperatura del piatto elevata", MessageLevel.WARNING, 30002));

            return plate.Select(
                plate =>
                {
                    if (!plate.connecting.HasValue || plate.connecting.Value || !plate.temp.HasValue)
                        return StatusBarState.Hidden;
                    if (plate.temp.Value.IsDisconnected)
                        return StatusBarState.Error;
                    if (plate.temp.Value.IsHot)
                        return StatusBarState.Warning;
                    if (plate.temp.Value.IsOn)
                        return StatusBarState.Stable;
                    return StatusBarState.Disabled;
                });
        }
    }
}