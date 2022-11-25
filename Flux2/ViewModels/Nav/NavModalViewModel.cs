using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System.Reactive;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class NavModalViewModel<TFluxRoutableViewModel> : FluxRoutableViewModel<NavModalViewModel<TFluxRoutableViewModel>>
        where TFluxRoutableViewModel : IFluxRoutableViewModel
    {
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> NavigateBackCommand { get; }

        [RemoteContent(false)]
        public TFluxRoutableViewModel Content { get; }

        public NavModalViewModel(
            FluxViewModel flux,
            TFluxRoutableViewModel route,
            OptionalObservable<bool> can_navigate_back = default,
            OptionalObservable<bool> show_navbar = default)
            : base(flux, show_navbar, $"navModal??{route.Name}")
        {
            Content = route;
            NavigateBackCommand = ReactiveCommand.Create(
                () => { Flux.Navigator.NavigateBack(); },
                can_navigate_back.ObservableOr(() => true));
        }

        public override Task OnNavigatedFromAsync(Optional<IFluxRoutableViewModel> from)
        {
            return Content.OnNavigatedFromAsync(from);
        }
        public override Task OnNavigateToAsync(IFluxRoutableViewModel to)
        {
            return Content.OnNavigateToAsync(to);
        }
    }
}
