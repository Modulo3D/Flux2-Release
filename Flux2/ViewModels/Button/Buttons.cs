using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class CmdButton : RemoteControl<CmdButton>
    {
        public ReactiveCommand<Unit, Unit> Command { get; }

        private ObservableAsPropertyHelper<bool> _Visible;
        public bool Visible => _Visible.Value;

        public CmdButton(
            string name, 
            Action command,
            Optional<IObservable<bool>> can_execute = default,
            Optional<IObservable<bool>> visible = default,
            Optional<IObservable<Optional<bool>>> active = default)
            : base(name)
        {
            _Visible = visible
                .ValueOr(() => Observable.Return(true))
                .ToProperty(this, v => v.Visible);

            Command = ReactiveCommand.Create(command, can_execute.ValueOr(() => Observable.Return(true)));
            AddCommand("command", Command, active.ValueOrDefault());
        }

        public CmdButton(
            string name, 
            Func<Task> command,
            Optional<IObservable<bool>> can_execute = default,
            Optional<IObservable<bool>> visible = default,
            Optional<IObservable<Optional<bool>>> active = default)
            : base(name)
        {
            _Visible = visible
               .ValueOr(() => Observable.Return(true))
               .ToProperty(this, v => v.Visible);

            Command = ReactiveCommand.CreateFromTask(command, can_execute.ValueOr(() => Observable.Return(true)));
            AddCommand("command", Command, active.ValueOrDefault());
        }
    }

    public class ToggleButton : CmdButton
    {
        public ToggleButton(
            string name,
            Action toggle,
            IObservable<Optional<bool>> is_active,
            Optional<IObservable<bool>> can_execute = default,
            Optional<IObservable<bool>> visible = default)
            : base(name, toggle, can_execute, visible, is_active.ToOptional())
        {
        }

        public ToggleButton(
            string name,
            Func<Task> toggle,
            IObservable<Optional<bool>> is_active,
            Optional<IObservable<bool>> can_execute = default,
            Optional<IObservable<bool>> visible = default)
            : base(name, toggle, can_execute, visible, is_active.ToOptional())
        {
        }

        public ToggleButton(
            string name,
            FluxViewModel flux,
            IFLUX_Variable<bool, bool> @bool,
            Optional<IObservable<bool>> can_execute = default,
            Optional<IObservable<bool>> visible = default)
            : this(
                name,
                () => flux.ConnectionProvider.ToggleVariableAsync(@bool),
                @bool.ValueChanged,
                can_execute,
                visible)
        {
        }
    }

    public class NavButton : CmdButton, INavButton
    {
        public IFlux Flux { get; }
        public IFluxRoutableViewModel Route { get; }

        public NavButton(
            IFlux flux,
            IFluxRoutableViewModel route,
            bool reset,
            Optional<IObservable<bool>> can_navigate = default,
            Optional<IObservable<bool>> visible = default) :
            base(route.Name, () => flux.Navigator.Navigate(route, reset), can_navigate, visible)
        {
            Route = route;
            Flux = flux;
        }

        public NavButton(
            IFlux flux,
            NavModalViewModel modal,
            Optional<IObservable<bool>> can_navigate = default,
            Optional<IObservable<bool>> visible = default) :
            base(modal.Content.Name, () => flux.Navigator.Navigate(modal, false), can_navigate, visible)
        {
            Route = modal;
            Flux = flux;
        }
    }
}
