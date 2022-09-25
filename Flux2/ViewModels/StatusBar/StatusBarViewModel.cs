using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
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
        public SourceCache<IStatusBarItemViewModel, string> StatusBarItemsSource { get; }

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
        public ReactiveCommand<Unit, Unit> ShowMessagesCommand { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ShowWebcamCommand { get; }

        public StatusBarViewModel(FluxViewModel flux) : base(flux)
        {
            StatusBarItemsSource = new SourceCache<IStatusBarItemViewModel, string>(v => v.Name);
            StatusBarItems = StatusBarItemsSource.Connect()
                .AutoRefresh(v => v.State)
                .Filter(v => v.State != StatusBarState.Hidden)
                .AsObservableCache();

            ShowMessagesCommand = ReactiveCommand.Create(() => { Content = Flux.Messages; });
            ShowWebcamCommand = ReactiveCommand.Create(() => { Content = Flux.Webcam; });
            Content = Flux.Messages;

            var conditions = Flux.StatusProvider.GetConditions<StatusBarConditionAttribute>();
            foreach (var kvp in conditions)
                StatusBarItemsSource.AddOrUpdate(new StatusBarItemViewModel(flux, kvp.Key, kvp.Value.Select(v => v.condition)));
        }
    }
}
