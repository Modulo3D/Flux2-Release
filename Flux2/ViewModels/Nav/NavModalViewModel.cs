using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System.Reactive;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Flux.ViewModels
{
    public sealed class NavModalViewModel<TFluxRoutableViewModel> : FluxRoutableViewModel<NavModalViewModel<TFluxRoutableViewModel>>
        where TFluxRoutableViewModel : IFluxRoutableViewModel
    {
        [RemoteCommand]
        public ReactiveCommandBaseRC NavigateBackCommand { get; }

        [RemoteContent(false)]
        public TFluxRoutableViewModel Content { get; }

        public NavModalViewModel(
            FluxViewModel flux,
            TFluxRoutableViewModel route,
            OptionalObservable<bool> can_navigate_back = default)
            : base(flux, string.IsNullOrEmpty(route.Name) ? $"{typeof(TFluxRoutableViewModel).GetRemoteElementClass()}" : route.Name)
        {
            Content = route;
            NavigateBackCommand = ReactiveCommandBaseRC.Create(
                () => Flux.Navigator.NavigateBack(), this,
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
