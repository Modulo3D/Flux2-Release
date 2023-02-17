namespace Flux.ViewModels
{
    /*public class FeedersStatusBarViewModel : StatusBarItemViewModel<FeedersStatusBarViewModel>
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
                .TrueForAny(f => f.WhenAnyValue(f => f.FeederState), s =>
                    s == EFeederState.ERROR)
                .DistinctUntilChanged()
                .StartWith(false);

            var not_found = Flux.Feeders.WhenAnyValue(v => v.SelectedFeeder)
                   .ConvertMany(f => f.WhenAnyValue(f => f.ToolNozzle.NozzleTemperature))
                   .Select(t => t.ConvertOr(t => t.Current > 1000, () => false))
                   .DistinctUntilChanged()
                   .StartWith(false);

            var hot = Flux.Feeders.WhenAnyValue(v => v.SelectedFeeder)
                .ConvertMany(f => f.WhenAnyValue(f => f.ToolNozzle.NozzleTemperature))
                .Select(t => t.ConvertOr(t =>
                t.Current > 50, () => false))
                .DistinctUntilChanged()
                .StartWith(false);

            var on = Flux.Feeders.WhenAnyValue(v => v.SelectedFeeder)
                .ConvertMany(f => f.WhenAnyValue(f => f.ToolNozzle.NozzleTemperature))
                .Select(t => t.ConvertOr(t => t.Target.Convert(t => t > 0), () => false))
                .DistinctUntilChanged()
                .StartWith(false);

            var open = Flux.StatusProvider.LockClosedConditions
                .WatchOptional("main")
                .ConvertMany(c => c.StateChanged)
                .Convert(t => !t.Valid)
                .ValueOr(() => false)
                .StartWith(false);

            var tool = Observable.CombineLatest(connecting, in_mateinance, error, not_found, hot, on, open,
                (connecting, in_mateinance, error, not_found, hot, on, open) => (connecting, in_mateinance, error, not_found, hot, on, open))
                .DistinctUntilChanged();

            tool.Throttle(TimeSpan.FromSeconds(5))
                .Where(t => !t.connecting && !t.in_mateinance && t.not_found)
                .SubscribeRC(_ => Flux.Messages.LogMessage("Utensile", "Sensore di temperatura non trovato", MessageLevel.EMERG, 27001));

            tool.Throttle(TimeSpan.FromSeconds(5))
                .Where(t => !t.connecting && !t.in_mateinance && t.error)
                .SubscribeRC(_ => Flux.Messages.LogMessage("Utensile", "Stato utensile non corretto", MessageLevel.ERROR, 27002));

            tool.Throttle(TimeSpan.FromSeconds(5))
                .Where(t => !t.connecting && !t.in_mateinance && !t.not_found && t.hot && t.open)
                .SubscribeRC(_ => Flux.Messages.LogMessage("Utensile", "Temperatura dell'utensile elevata", MessageLevel.WARNING, 27003));

            return tool.Select(
                tool =>
                {
                    if (tool.connecting)
                        return StatusBarState.Hidden;
                    if (tool.error)
                        return StatusBarState.Error;
                    if (tool.hot)
                        return StatusBarState.Warning;
                    if (tool.on.ValueOr(() => false))
                        return StatusBarState.Stable;
                    return StatusBarState.Disabled;
                });
        }
    }*/
}
