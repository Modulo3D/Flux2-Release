using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System.Reactive;

namespace Flux.ViewModels
{
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

            StatusBarItemsSource.AddOrUpdate(new PlateStatusBarViewModel(Flux));
            StatusBarItemsSource.AddOrUpdate(new ChamberStatusBarViewModel(Flux));
            StatusBarItemsSource.AddOrUpdate(new FeedersStatusBarViewModel(Flux));
            StatusBarItemsSource.AddOrUpdate(new LocksStatusBarViewModel(Flux));
            StatusBarItemsSource.AddOrUpdate(new NetworkStatusBarViewModel(Flux));
            StatusBarItemsSource.AddOrUpdate(new PressureStatusBarViewModel(Flux));
            StatusBarItemsSource.AddOrUpdate(new VacuumStatusBarViewModel(Flux));
            StatusBarItemsSource.AddOrUpdate(new DriverStatusBarViewModel(Flux));
            StatusBarItemsSource.AddOrUpdate(new MessagesStatusBarViewModel(Flux));
            StatusBarItemsSource.AddOrUpdate(new DebugStatusBarViewModel(Flux));
        }
    }
}
