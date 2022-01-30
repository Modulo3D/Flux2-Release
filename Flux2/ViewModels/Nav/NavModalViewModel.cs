using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class NavModalViewModel : FluxRoutableViewModel<NavModalViewModel>
    {
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> NavigateBackCommand { get; }

        [RemoteContent(false)]
        public IFluxRoutableViewModel Content { get; }

        public NavModalViewModel(
            FluxViewModel flux,
            IFluxRoutableViewModel route,
            Optional<IObservable<bool>> can_navigate_back = default,
            Optional<IObservable<bool>> show_navbar = default)
            : base(flux, show_navbar, $"navModal??{route.Name}")
        {
            Content = route;
            NavigateBackCommand = ReactiveCommand.Create(
                () => { Flux.Navigator.NavigateBack(); },
                can_navigate_back.ValueOr(() => Observable.Return(true)));
        }
    }
}
