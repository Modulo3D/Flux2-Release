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

        public void AddRoute(
            IFluxRoutableViewModel route,
            OptionalObservable<bool> can_navigate = default,
            OptionalObservable<bool> visible = default)
        {
            Buttons.Add(new NavButton(Flux, route, false, can_navigate, visible));
        }

        public void AddModal(
            IFluxRoutableViewModel modal,
            OptionalObservable<bool> can_navigate = default,
            OptionalObservable<bool> visible = default,
            OptionalObservable<bool> navigate_back = default,
            OptionalObservable<bool> show_navbar = default)
        {
            Buttons.Add(new NavButton(Flux, new NavModalViewModel(Flux, modal, navigate_back, show_navbar), false, can_navigate, visible));
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
            VariableUnit unit,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default)
        {
            var variable = Flux.ConnectionProvider.GetVariable(get_array, unit);
            if (variable.HasValue)
                Buttons.Add(new ToggleButton(name, Flux, variable.Value, can_execute, visible));
        }

        public void AddCommand(
            string name,
            Func<IFLUX_VariableStore, IFLUX_Array<bool, bool>> get_array,
            VariableUnit unit,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default)
        {
            var variable = Flux.ConnectionProvider.GetVariable(get_array, unit);
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
