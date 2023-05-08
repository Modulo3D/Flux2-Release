using DynamicData.Kernel;
using EmbedIO.Routing;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class CmdButton : RemoteControl<CmdButton>
    {
        public ReactiveCommandBaseRC Command { get; }

        private readonly ObservableAsPropertyHelper<bool> _Visible;
        public bool Visible => _Visible.Value;

        private CmdButton(
            string name,
            Func<CmdButton, ReactiveCommandBaseRC> command,
            OptionalObservable<bool> visible = default,
            OptionalObservable<Optional<bool>> active = default)
            : base($"{typeof(CmdButton).GetRemoteElementClass()}.{name}")
        {
            Command = command(this);
            _Visible = visible
                .ObservableOr(() => true)
                .ToPropertyRC(this, v => v.Visible);
            AddCommand("command", Command, active.ObservableOrDefault());
        }

        public CmdButton(
            string name,
            Action command,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default,
            OptionalObservable<Optional<bool>> active = default)
            : this(name, d => ReactiveCommandBaseRC.Create(command, d, can_execute.ObservableOr(() => true)), visible, active)
        {
        }

        public CmdButton(
            string name,
            Func<Task> command,
            OptionalObservable<bool> can_execute = default,
            OptionalObservable<bool> visible = default,
            OptionalObservable<Optional<bool>> active = default)
            : this(name, d => ReactiveCommandBaseRC.CreateFromTask(command, d, can_execute.ObservableOr(() => true)), visible, active)
        {
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

    public sealed class NavButton<TFluxRoutableViewModel> : CmdButton, INavButton
        where TFluxRoutableViewModel : IFluxRoutableViewModel
    {
        public IFlux Flux { get; }

        public NavButton(
            IFlux flux,
            TFluxRoutableViewModel route,
            OptionalObservable<bool> can_navigate = default,
            OptionalObservable<bool> visible = default,
            bool reset = false)
            : base(route.Name, () => flux.Navigator.Navigate(route, reset), can_navigate, visible)
        {
            Flux = flux;
        }

        public NavButton(
            IFlux flux,
            Lazy<TFluxRoutableViewModel> modal,
            bool reset,
            OptionalObservable<bool> can_navigate = default,
            OptionalObservable<bool> visible = default) :
            base(typeof(TFluxRoutableViewModel).GetRemoteElementClass(), () => flux.Navigator.Navigate(modal.Value, reset), can_navigate, visible)
        {
            Flux = flux;
        }
    }
    public sealed class NavButtonModal<TFluxRoutableViewModel> : CmdButton, INavButton
        where TFluxRoutableViewModel : IFluxRoutableViewModel
    {
        public IFlux Flux { get; }

        public NavButtonModal(
            IFlux flux,
            NavModalViewModel<TFluxRoutableViewModel> route,
            OptionalObservable<bool> can_navigate = default,
            OptionalObservable<bool> visible = default) :
            base(route.Name, () => flux.Navigator.Navigate(route, false), can_navigate, visible)
        {
            Flux = flux;
        }

        public NavButtonModal(
            IFlux flux,
            Lazy<NavModalViewModel<TFluxRoutableViewModel>> modal,
            OptionalObservable<bool> can_navigate = default,
            OptionalObservable<bool> visible = default) :
            base(typeof(TFluxRoutableViewModel).GetRemoteElementClass(), () => flux.Navigator.Navigate(modal.Value, false), can_navigate, visible)
        {
            Flux = flux;
        }
    }
}
