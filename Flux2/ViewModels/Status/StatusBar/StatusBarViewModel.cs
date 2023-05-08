using DynamicData;
using DynamicData.Kernel;
using Flux.ViewModels;
using Modulo3DNet;
using ReactiveUI;
using System.Linq;
using System.Reactive;

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
        public ReactiveCommandBaseRC ShowMessagesCommand { get; }

        [RemoteCommand]
        public ReactiveCommandBaseRC ShowWebcamCommand { get; }

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

            var conditions = Flux.ConditionsProvider.GetConditions<StatusBarConditionAttribute>();
            foreach (var kvp in conditions)
                StatusBarItemsSource.AddOrUpdate(new StatusBarItemViewModel(flux, kvp.Key, kvp.Value.Select(v => v.condition)));
        }
    }
}
