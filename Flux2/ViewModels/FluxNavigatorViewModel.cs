using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class FluxNavigatorViewModel : RemoteControl<FluxNavigatorViewModel>, IFluxNavigatorViewModel
    {
        private readonly ObservableAsPropertyHelper<bool> _ShowNavBar;
        [RemoteOutput(true)]
        public bool ShowNavBar => _ShowNavBar.Value;

        public FluxViewModel Flux { get; }
        public SourceList<INavButton> Routes { get; private set; }

        private Optional<IFluxRoutableViewModel> _CurrentViewModel;
        [RemoteContent(true)]
        public Optional<IFluxRoutableViewModel> CurrentViewModel
        {
            get => _CurrentViewModel;
            set => this.RaiseAndSetIfChanged(ref _CurrentViewModel, value);
        }

        private Stack<IFluxRoutableViewModel> PreviousViewModels { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> HomeCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MCodesCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> FeedersCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> CalibrationCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> FunctionalityCommand { get; }

        public FluxNavigatorViewModel(FluxViewModel flux)
        {
            Flux = flux;
            SourceListRC.Create(this, v => v.Routes);
            PreviousViewModels = new Stack<IFluxRoutableViewModel>();

            var home = new NavButton<HomeViewModel>(flux, Flux.Home, reset: true);
            var storage = new NavButton<MCodesViewModel>(flux, Flux.MCodes, reset: true);
            var feeders = new NavButton<FeedersViewModel>(flux, Flux.Feeders, reset: true);
            var calibration = new NavButton<CalibrationViewModel>(flux, Flux.Calibration, reset: true);
            var functionality = new NavButton<FunctionalityViewModel>(flux, Flux.Functionality, reset: true);


            _ShowNavBar = this.WhenAnyValue(v => v.CurrentViewModel)
                .ConvertMany(vm => vm.ShowNavBar)
                .ValueOr(() => false)
                .ToPropertyRC(this, v => v.ShowNavBar);

            Routes.Add(home);
            Routes.Add(storage);
            Routes.Add(feeders);
            Routes.Add(calibration);
            Routes.Add(functionality);

            HomeCommand = home.Command;
            MCodesCommand = storage.Command;
            FeedersCommand = feeders.Command;
            CalibrationCommand = calibration.Command;
            FunctionalityCommand = functionality.Command;

            this.WhenAnyValue(v => v.CurrentViewModel)
                .PairWithPreviousValue()
                .SubscribeRC(async v =>
                {
                    if (v.NewValue.HasValue)
                    {
                        await v.NewValue.Value.OnNavigatedFromAsync(v.OldValue);
                        if (v.OldValue.HasValue)
                            await v.OldValue.Value.OnNavigateToAsync(v.NewValue.Value);
                    }
                }, this);
        }

        public void Navigate<TFluxRoutableViewModel>(TFluxRoutableViewModel route, bool reset = false)
            where TFluxRoutableViewModel : IFluxRoutableViewModel
        {
            try
            {
                if (reset)
                {
                    PreviousViewModels.Clear();
                }
                else
                {
                    if (CurrentViewModel.HasValue && (!PreviousViewModels.TryPeek(out var previous) || !ReferenceEquals(previous, route)))
                        PreviousViewModels.Push(CurrentViewModel.Value);
                }

                CurrentViewModel = ((IFluxRoutableViewModel)route).ToOptional();
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
            }
        }

        public void NavigateModal<TFluxRoutableViewModel>(
            TFluxRoutableViewModel route,
            OptionalObservable<bool> navigate_back = default,
            OptionalObservable<bool> show_navbar = default)
            where TFluxRoutableViewModel : IFluxRoutableViewModel
        {
            Navigate(new NavModalViewModel<TFluxRoutableViewModel>(Flux, route, navigate_back, show_navbar), false);
        }
        public void NavigateModal<TFluxRoutableViewModel>(
            Lazy<TFluxRoutableViewModel> lazy_route,
            OptionalObservable<bool> navigate_back = default,
            OptionalObservable<bool> show_navbar = default)
            where TFluxRoutableViewModel : IFluxRoutableViewModel
        {
            Navigate(new NavModalViewModel<TFluxRoutableViewModel>(Flux, lazy_route.Value, navigate_back, show_navbar), false);
        }

        public void NavigateBack()
        {
            try
            {
                if (PreviousViewModels.TryPop(out var previous))
                    CurrentViewModel = previous.ToOptional();
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
            }
        }

        public void NavigateHome()
        {
            try
            {
                CurrentViewModel = Flux.Home;
                PreviousViewModels.Clear();
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
            }
        }
    }

    public abstract class FluxRoutableViewModel<TFluxRoutableViewModel> : RemoteControl<TFluxRoutableViewModel>, IFluxRoutableViewModel
        where TFluxRoutableViewModel : FluxRoutableViewModel<TFluxRoutableViewModel>
    {
        public FluxViewModel Flux { get; }
        public string UrlPathSegment { get; }
        IFlux IFluxRoutableViewModel.Flux => Flux;
        public IObservable<bool> ShowNavBar { get; }

        public FluxRoutableViewModel(
            FluxViewModel flux,
            OptionalObservable<bool> show_navbar = default,
            string name = "") : base(name)
        {
            Flux = flux;
            ShowNavBar = show_navbar.ObservableOr(() => false);
            UrlPathSegment = typeof(TFluxRoutableViewModel).GetRemoteElementClass();
        }

        public virtual Task OnNavigatedFromAsync(Optional<IFluxRoutableViewModel> from)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnNavigateToAsync(IFluxRoutableViewModel to)
        {
            return Task.CompletedTask;
        }
    }

    public abstract class FluxRoutableNavBarViewModel<TViewModel> : FluxRoutableViewModel<TViewModel>
        where TViewModel : FluxRoutableNavBarViewModel<TViewModel>
    {
        public FluxRoutableNavBarViewModel(
            FluxViewModel flux,
            string name = "")
            : base(flux, OptionalObservable.Some(true), name)
        {
        }
    }
}
