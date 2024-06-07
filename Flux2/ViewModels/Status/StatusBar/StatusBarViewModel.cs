using DynamicData;
using DynamicData.Kernel;
using Flux.ViewModels;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class StatusBarConditionAttribute : FilterConditionAttribute
    {
        public StatusBarConditionAttribute(string name = default, bool filter_on_cycle = true, string[] include_alias = default, string[] exclude_alias = default)
            : base(name, filter_on_cycle, include_alias, exclude_alias) { }
    }
    public class StatusBarViewModel : FluxRoutableViewModel<StatusBarViewModel>
    {
        public SourceCache<IStatusBarItemViewModel, string> StatusBarItemsSource { get; private set; }

        [RemoteContent(true)]
        public IObservableCache<IStatusBarItemViewModel, string> StatusBarItems { get; }

        private Optional<IFluxRoutableViewModel> _Content;
        [RemoteContent(true)]
        public Optional<IFluxRoutableViewModel> Content
        {
            get => _Content;
            set => this.RaiseAndSetIfChanged(ref _Content, value);

        }

        [RemoteCommand]
        public ReactiveCommandBaseRC<Unit, Unit> ShowMessagesCommand { get; }

        [RemoteCommand]
        public ReactiveCommandBaseRC<Unit, Unit> ShowWebcamCommand { get; }

        public StatusBarViewModel(FluxViewModel flux) : base(flux)
        {
            SourceCacheRC.Create(this, v => v.StatusBarItemsSource, v => v.Name);
            StatusBarItems = StatusBarItemsSource.Connect()
                .AutoRefresh(v => v.State)
                .Filter(v => v.State != StatusBarState.Hidden)
                .AsObservableCacheRC(this);

            ShowMessagesCommand = ReactiveCommandBaseRC.Create(() => { Content = Flux.Messages; }, this);
            ShowWebcamCommand = ReactiveCommandBaseRC.Create(() => { Content = Flux.Webcam; }, this);
            Content = Flux.Messages;
        }

        public void Initialize()
        {
            var conditions = Flux.ConditionsProvider.GetConditions<StatusBarConditionAttribute>();
            foreach (var kvp in conditions)
                StatusBarItemsSource.AddOrUpdate(new StatusBarItemViewModel(Flux, kvp.Key, kvp.Value.Select(v => v.condition)));
        }

        public override Task OnNavigatedFromAsync(Optional<IFluxRoutableViewModel> from)
        {
            Flux.Messages.CurrentTimestamp = DateTime.Now;
            return Task.CompletedTask;
        }

    }
}
