﻿using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    [RemoteControl(baseClass: typeof(NavPanelViewModel<>))]
    public class NavPanelViewModel<TNavPanelViewModel> : FluxRoutableNavBarViewModel<TNavPanelViewModel>
        where TNavPanelViewModel : NavPanelViewModel<TNavPanelViewModel>
    {
        private SourceList<CmdButton> Buttons { get; set; }

        [RemoteContent(true)]
        public IObservableList<CmdButton> VisibleButtons { get; }

        public NavPanelViewModel(FluxViewModel flux, string name = "") : base(flux, name)
        {
            SourceListRC.Create(this, v => v.Buttons);
            VisibleButtons = Buttons.Connect()
                .AutoRefresh(v => v.Visible)
                .Filter(v => v.Visible)
                .AsObservableListRC(this);
        }

        public void AddRoute<TFluxRoutableViewModel>(
            TFluxRoutableViewModel route,
            OptionalObservable<bool> can_navigate = default,
            OptionalObservable<bool> visible = default)
            where TFluxRoutableViewModel : IFluxRoutableViewModel
        {
            Buttons.Add(new NavButton<TFluxRoutableViewModel>(route.Flux, route, can_navigate, visible));
        }

        public void AddModal<TFluxRoutableViewModel>(
            TFluxRoutableViewModel route,
            OptionalObservable<bool> can_navigate = default,
            OptionalObservable<bool> visible = default,
            OptionalObservable<bool> navigate_back = default)
            where TFluxRoutableViewModel : IFluxRoutableViewModel
        {
            var modal = new NavModalViewModel<TFluxRoutableViewModel>(Flux, route, navigate_back);
            Buttons.Add(new NavButtonModal<TFluxRoutableViewModel>(route.Flux, modal, can_navigate, visible));
        }


        public Lazy<TFluxRoutableViewModel> AddModal<TFluxRoutableViewModel>(
            Func<TFluxRoutableViewModel> get_route,
            OptionalObservable<bool> can_navigate = default,
            OptionalObservable<bool> visible = default,
            OptionalObservable<bool> navigate_back = default)
            where TFluxRoutableViewModel : IFluxRoutableViewModel
        {
            var lazy_route = new Lazy<TFluxRoutableViewModel>(get_route);
            var modal = new Lazy<NavModalViewModel<TFluxRoutableViewModel>>(() => new NavModalViewModel<TFluxRoutableViewModel>(Flux, lazy_route.Value, navigate_back));
            Buttons.Add(new NavButtonModal<TFluxRoutableViewModel>(Flux, modal, can_navigate, visible));
            return lazy_route;
        }
        public void AddModal<TFluxRoutableViewModel>(
            Lazy<TFluxRoutableViewModel> lazy_route,
            OptionalObservable<bool> can_navigate = default,
            OptionalObservable<bool> visible = default,
            OptionalObservable<bool> navigate_back = default)
            where TFluxRoutableViewModel : IFluxRoutableViewModel
        {
            var modal = new Lazy<NavModalViewModel<TFluxRoutableViewModel>>(() => new NavModalViewModel<TFluxRoutableViewModel>(Flux, lazy_route.Value, navigate_back));
            Buttons.Add(new NavButtonModal<TFluxRoutableViewModel>(Flux, modal, can_navigate, visible));
        }

        public void AddCommand(
            string name,
            Func<Task> task,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default)
        {
            Buttons.Add(new CmdButton(Flux, name, task, can_execute, visible));
        }

        public void AddCommand(
            string name,
            Action action,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default)
        {
            Buttons.Add(new CmdButton(Flux, name, action, can_execute, visible));
        }

        public void AddCommand(
            string name,
            Func<IFLUX_VariableStore, Optional<IFLUX_Variable<bool, bool>>> get_variable,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default)
        {
            var variable = Flux.ConnectionProvider.GetVariable(get_variable);
            if (variable.HasValue)
                Buttons.Add(new ToggleButton(Flux, name, variable.Value, can_execute, visible));
        }

        public void AddCommand(
            string name,
            Func<IFLUX_VariableStore, IFLUX_Variable<bool, bool>> get_variable,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default)
        {
            var variable = Flux.ConnectionProvider.GetVariable(get_variable);
            Buttons.Add(new ToggleButton(Flux, name, variable, can_execute, visible));
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
                Buttons.Add(new ToggleButton(Flux, name, variable.Value, can_execute, visible));
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
                Buttons.Add(new ToggleButton(Flux, name, variable.Value, can_execute, visible));
        }


        public void AddCommand<TSettings>(
            string name,
            LocalSettingsProvider<TSettings> settings,
            Expression<Func<TSettings, bool>> setting_expr,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default)
            where TSettings : new()
        {
            var setting_getter = setting_expr.GetCachedGetterDelegate();
            var setting_setter = setting_expr.GetCachedSetterDelegate();
            var is_active = settings.Local.WhenAnyValue(setting_expr)
                .Select(v => v.ToOptional());

            Buttons.Add(new ToggleButton(Flux, name, toggle_setting, is_active, can_execute, visible));

            void toggle_setting()
            {
                var value = setting_getter(settings.Local);
                setting_setter.Invoke(settings.Local, !value);
                settings.PersistLocalSettings();
            }
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
