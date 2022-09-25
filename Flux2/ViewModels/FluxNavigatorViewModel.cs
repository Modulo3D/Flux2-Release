﻿using DynamicData;
using DynamicData.Kernel;
using Modulo3DDatabase;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class FluxNavigatorViewModel : RemoteControl<FluxNavigatorViewModel>, IFluxNavigatorViewModel
    {
        private ObservableAsPropertyHelper<bool> _ShowNavBar;
        [RemoteOutput(true)]
        public bool ShowNavBar => _ShowNavBar.Value;

        public FluxViewModel Flux { get; }
        public SourceCache<INavButton, IFluxRoutableViewModel> Routes { get; }

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

        public FluxNavigatorViewModel(FluxViewModel flux) : base("navigator")
        {
            Flux = flux;
            PreviousViewModels = new Stack<IFluxRoutableViewModel>();
            Routes = new SourceCache<INavButton, IFluxRoutableViewModel>(n => n.Route);

            var home = new NavButton(Flux, Flux.Home, true);
            var storage = new NavButton(Flux, Flux.MCodes, true);
            var feeders = new NavButton(Flux, Flux.Feeders, true);
            var calibration = new NavButton(Flux, Flux.Calibration, true);
            var functionality = new NavButton(Flux, Flux.Functionality, true);


            _ShowNavBar = this.WhenAnyValue(v => v.CurrentViewModel)
                .ConvertMany(vm => vm.ShowNavBar)
                .ValueOr(() => false)
                .ToProperty(this, v => v.ShowNavBar);

            Routes.AddOrUpdate(home);
            Routes.AddOrUpdate(storage);
            Routes.AddOrUpdate(feeders);
            Routes.AddOrUpdate(calibration);
            Routes.AddOrUpdate(functionality);

            HomeCommand = home.Command;
            MCodesCommand = storage.Command;
            FeedersCommand = feeders.Command;
            CalibrationCommand = calibration.Command;
            FunctionalityCommand = functionality.Command;
        }

        public void Navigate(IFluxRoutableViewModel route, bool reset = false)
        {
            try
            {
                if (reset)
                {
                    PreviousViewModels.Clear();
                }
                else
                {
                    if (CurrentViewModel.HasValue && (!PreviousViewModels.TryPeek(out var previous) || previous != route))
                        PreviousViewModels.Push(CurrentViewModel.Value);
                }

                CurrentViewModel = route.ToOptional();
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
            }
        }

        public void NavigateModal(
            IFluxRoutableViewModel route,
            OptionalObservable<bool> navigate_back = default,
            OptionalObservable<bool> show_navbar = default)
        {
            Navigate(new NavModalViewModel(Flux, route, navigate_back, show_navbar), false);
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

    public abstract class FluxRoutableViewModel<TViewModel> : RemoteControl<TViewModel>, IFluxRoutableViewModel
        where TViewModel : FluxRoutableViewModel<TViewModel>
    {
        public FluxViewModel Flux { get; }
        public string UrlPathSegment { get; }
        IFlux IFluxRoutableViewModel.Flux => Flux;
        public IObservable<bool> ShowNavBar { get; }

        public FluxRoutableViewModel(
            FluxViewModel flux,
            OptionalObservable<bool> show_navbar = default,
            Optional<string> name = default) : base(name)
        {
            Flux = flux;
            UrlPathSegment = this.GetRemoteControlName();
            ShowNavBar = show_navbar.ObservableOr(() => false);
        }

        public virtual Task OnNavigatedFromAsync()
        {
            return Task.CompletedTask;
        }
        public virtual Task OnNavigateToAsync()
        {
            return Task.CompletedTask;
        }
    }

    public abstract class FluxRoutableNavBarViewModel<TViewModel> : FluxRoutableViewModel<TViewModel>
        where TViewModel : FluxRoutableNavBarViewModel<TViewModel>
    {
        public FluxRoutableNavBarViewModel(
            FluxViewModel flux,
            Optional<string> name = default)
            : base(flux, OptionalObservable.Some(true), name)
        {
        }
    }
}
