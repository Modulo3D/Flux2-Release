using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class NavPanelViewModel<TViewModel> : FluxRoutableNavBarViewModel<TViewModel>
        where TViewModel : NavPanelViewModel<TViewModel>
    {
        private SourceList<BaseButton> Buttons { get; }

        [RemoteContent(true)]
        public IObservableList<BaseButton> VisibleButtons { get; }

        public NavPanelViewModel(FluxViewModel flux, string name = default) : base(flux, $"navPanel??{typeof(TViewModel).GetRemoteControlName()}{(string.IsNullOrEmpty(name) ? "" : $"??{name}")}")
        {
            Buttons = new SourceList<BaseButton>();
            VisibleButtons = Buttons.Connect()
                .AutoRefresh(v => v.Visible)
                .Filter(v => v.Visible)
                .AsObservableList();
        }

        private void AddButton(BaseButton button)
        {
            button.Initialize();
            Buttons.Add(button);
        }

        public void AddRoute(
            IFluxRoutableViewModel route,
            Optional<IObservable<bool>> can_navigate = default,
            Optional<IObservable<bool>> visible = default)
        {
            var nav_button = new NavButton(Flux, route, true, false, can_navigate, visible);
            AddButton(nav_button);
        }

        public void AddModal(
            IFluxRoutableViewModel modal,
            Optional<IObservable<bool>> can_navigate = default,
            Optional<IObservable<bool>> visible = default,
            Optional<IObservable<bool>> navigate_back = default,
            Optional<IObservable<bool>> show_navbar = default)
        {
            var nav_modal = new NavModalViewModel(Flux, modal, navigate_back, show_navbar);
            var nav_button = new NavButton(Flux, nav_modal, true, can_navigate, visible);
            AddButton(nav_button);
        }

        public void AddCommand(
            string name,
            Func<Task> task,
            Optional<IObservable<bool>> can_execute = default,
            Optional<IObservable<bool>> visible = default)
        {
            var cmd_button = new CmdButton(name, task, can_execute, visible);
            AddButton(cmd_button);
        }

        public void AddCommand(
            string name,
            Action action,
            Optional<IObservable<bool>> can_execute = default,
            Optional<IObservable<bool>> visible = default)
        {
            var cmd_button = new CmdButton(name, action, can_execute, visible);
            AddButton(cmd_button);
        }

        public void AddCommand(
            string name,
            Func<IFLUX_ConnectionProvider, Optional<IFLUX_Variable<bool, bool>>> get_variable,
            Optional<IObservable<bool>> can_execute = default,
            Optional<IObservable<bool>> visible = default)
        {
            var memory = Flux.ConnectionProvider;
            var variable = get_variable(memory);
            if(variable.HasValue)
                AddCommand(name, variable.Value, can_execute, visible);
        }

        public void AddCommand(
            string name,
            IFLUX_Variable<bool, bool> variable,
            Optional<IObservable<bool>> can_execute = default,
            Optional<IObservable<bool>> visible = default)
        {
            if (variable.ReadOnly)
                return;

            var toggle_button = new ToggleButton(name, Flux, variable, can_execute, visible);
            AddButton(toggle_button);
        }

        public void AddCommand(
            string name,
            Func<IFLUX_ConnectionProvider, Optional<IFLUX_Array<bool, bool>>> get_array,
            ushort position,
            Optional<IObservable<bool>> can_execute = default,
            Optional<IObservable<bool>> visible = default)
        {
            var memory = Flux.ConnectionProvider;
            var array = get_array(memory);
            if(array.HasValue)
                AddCommand(name, array.Value, position, can_execute, visible);
        }

        public void AddCommand(
            string name,
            IFLUX_Array<bool, bool> array,
            ushort position,
            Optional<IObservable<bool>> can_execute = default,
            Optional<IObservable<bool>> visible = default)
        {
            var variable = array.Variables.Items.ElementAt(position);
            AddCommand(name, variable, can_execute, visible);
        }

        public void AddCommand(BaseButton button)
        {
            AddButton(button);
        }

        public void Clear()
        {
            Buttons.Clear();
            RemoteCommands.Clear();
        }
    }
}
