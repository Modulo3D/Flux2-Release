using DynamicData.Kernel;
using Modulo3DDatabase;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class CmdButton : RemoteControl<CmdButton>
    {
        public ReactiveCommand<Unit, Unit> Command { get; }

        private ObservableAsPropertyHelper<bool> _Visible;
        public bool Visible => _Visible.Value;

        private CmdButton(
            string name,
            OptionalObservable<bool> visible = default,
            OptionalObservable<Optional<bool>> active = default)
            : base(name)
        {
            _Visible = visible
                .ObservableOr(() => true)
                .ToProperty(this, v => v.Visible)
                .DisposeWith(Disposables);
        }

        public CmdButton(
            string name,
            Action command,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default,
            OptionalObservable<Optional<bool>> active = default)
            : this(name, visible, active)
        {
            Command = ReactiveCommand.Create(command, can_execute.ObservableOr(() => true))
                .DisposeWith(Disposables);
            AddCommand("command", Command, active.ObservableOrDefault());
        }

        public CmdButton(
            string name,
            Func<Task> command,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default,
            OptionalObservable<Optional<bool>> active = default)
            : this(name, visible, active)
        {
            Command = ReactiveCommand.CreateFromTask(command, can_execute.ObservableOr(() => true))
                .DisposeWith(Disposables);
            AddCommand("command", Command, active.ObservableOrDefault());
        }
    }

    public class ToggleButton : CmdButton
    {
        public ToggleButton(
            string name,
            Action toggle,
            IObservable<Optional<bool>> is_active,
            OptionalObservable<bool> can_execute = default, 
            OptionalObservable<bool> visible = default)
            : base(name, toggle, can_execute, visible, is_active.ToOptional())
        {
        }

        public ToggleButton(
            string name,
            Func<Task> toggle,
            IObservable<Optional<bool>> is_active,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default)
            : base(name, toggle, can_execute, visible, is_active.ToOptional())
        {
        }

        public ToggleButton(
            string name,
            FluxViewModel flux,
            IFLUX_Variable<bool, bool> @bool,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default)
            : this(
                name,
                () => @bool.WriteAsync(!@bool.Value.ValueOr(() => false)),
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
            OptionalObservable<bool> can_navigate = default,
            OptionalObservable<bool> visible = default) :
            base(route.Name, () => flux.Navigator.Navigate(route, reset), can_navigate, visible)
        {
            Route = route;
            Flux = flux;
        }

        public NavButton(
            IFlux flux,
            NavModalViewModel modal,
            OptionalObservable<bool> can_navigate = default,
            OptionalObservable<bool> visible = default) :
            base(modal.Content.Name, () => flux.Navigator.Navigate(modal, false), can_navigate, visible)
        {
            Route = modal;
            Flux = flux;
        }
    }
}
