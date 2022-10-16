using DynamicData;
using DynamicData.Kernel;
using Modulo3DDatabase;
using Modulo3DStandard;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class NavPanelViewModel<TViewModel> : FluxRoutableNavBarViewModel<TViewModel>
        where TViewModel : NavPanelViewModel<TViewModel>
    {
        private SourceList<CmdButton> Buttons { get; }

        [RemoteContent(true)]
        public IObservableList<CmdButton> VisibleButtons { get; }

        public NavPanelViewModel(FluxViewModel flux, string name = default) : base(flux, $"navPanel??{typeof(TViewModel).GetRemoteControlName()}{(string.IsNullOrEmpty(name) ? "" : $"??{name}")}")
        {
            Buttons = new SourceList<CmdButton>();
            Buttons.Connect()
                .DisposeMany()
                .Subscribe()
                .DisposeWith(Disposables);

            VisibleButtons = Buttons.Connect()
                .AutoRefresh(v => v.Visible)
                .Filter(v => v.Visible)
                .AsObservableList();
        }

        public void AddRoute<TFluxRoutableViewModel>(
            TFluxRoutableViewModel route,
            OptionalObservable<bool> can_navigate = default,
            OptionalObservable<bool> visible = default)
            where TFluxRoutableViewModel : IFluxRoutableViewModel
        {
            Buttons.Add(new NavButton<TFluxRoutableViewModel>(Flux, route, can_navigate, visible));
        }

        public void AddModal<TFluxRoutableViewModel>(
            TFluxRoutableViewModel route,
            OptionalObservable<bool> can_navigate = default,
            OptionalObservable<bool> visible = default,
            OptionalObservable<bool> navigate_back = default,
            OptionalObservable<bool> show_navbar = default)
            where TFluxRoutableViewModel : IFluxRoutableViewModel
        {
            var modal = new NavModalViewModel<TFluxRoutableViewModel>(Flux, route, navigate_back, show_navbar);
            Buttons.Add(new NavButtonModal<TFluxRoutableViewModel>(Flux, modal, can_navigate, visible));
        }

        public void AddModal<TFluxRoutableViewModel>(
            Lazy<TFluxRoutableViewModel> route,
            OptionalObservable<bool> can_navigate = default,
            OptionalObservable<bool> visible = default,
            OptionalObservable<bool> navigate_back = default,
            OptionalObservable<bool> show_navbar = default)
            where TFluxRoutableViewModel : IFluxRoutableViewModel
        {
            var modal = new Lazy<NavModalViewModel<TFluxRoutableViewModel>>(() => new NavModalViewModel<TFluxRoutableViewModel>(Flux, route.Value, navigate_back, show_navbar));
            Buttons.Add(new NavButtonModal<TFluxRoutableViewModel>(Flux, modal, can_navigate, visible));
        }

        public void AddModal<TFluxRoutableViewModel>(
            Func<TFluxRoutableViewModel> get_route,
            OptionalObservable<bool> can_navigate = default,
            OptionalObservable<bool> visible = default,
            OptionalObservable<bool> navigate_back = default,
            OptionalObservable<bool> show_navbar = default)
            where TFluxRoutableViewModel : IFluxRoutableViewModel
        {
            var modal = new Lazy<NavModalViewModel<TFluxRoutableViewModel>>(() => new NavModalViewModel<TFluxRoutableViewModel>(Flux, get_route(), navigate_back, show_navbar));
            Buttons.Add(new NavButtonModal<TFluxRoutableViewModel>(Flux, modal, can_navigate, visible));
        }

        public void AddCommand(
            string name,
            Func<Task> task,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default)
        {
            Buttons.Add(new CmdButton(name, task, can_execute, visible));
        }

        public void AddCommand(
            string name,
            Action action,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default)
        {
            Buttons.Add(new CmdButton(name, action, can_execute, visible));
        }

        public void AddCommand(
            string name,
            Func<IFLUX_VariableStore, Optional<IFLUX_Variable<bool, bool>>> get_variable,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default)
        {
            var variable = Flux.ConnectionProvider.GetVariable(get_variable);
            if(variable.HasValue)
                Buttons.Add(new ToggleButton(name, Flux, variable.Value, can_execute, visible));
        }

        public void AddCommand(
            string name,
            Func<IFLUX_VariableStore, IFLUX_Variable<bool, bool>> get_variable,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default)
        {
            var variable = Flux.ConnectionProvider.GetVariable(get_variable);
            Buttons.Add(new ToggleButton(name, Flux, variable, can_execute, visible));
        }

        public void AddCommand(
            string name,
            Func<IFLUX_VariableStore, Optional<IFLUX_Array<bool, bool>>> get_array,
            VariableAlias alias,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default)
        {
            var variable = Flux.ConnectionProvider.GetVariable(get_array, alias);
            if (variable.HasValue)
                Buttons.Add(new ToggleButton(name, Flux, variable.Value, can_execute, visible));
        }

        public void AddCommand(
            string name,
            Func<IFLUX_VariableStore, IFLUX_Array<bool, bool>> get_array,
            VariableAlias alias,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default)
        {
            var variable = Flux.ConnectionProvider.GetVariable(get_array, alias);
            if (variable.HasValue)
                Buttons.Add(new ToggleButton(name, Flux, variable.Value, can_execute, visible));
        }
        public void AddCommand(CmdButton button)
        {
            Buttons.Add(button);
        }

        public void Clear()
        {
            Buttons.Clear();
        }
    }
}
