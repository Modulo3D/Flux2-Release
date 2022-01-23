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
    public abstract class BaseButton : RemoteControl<BaseButton>
    {
        public ReactiveCommand<Unit, Unit> Command { get; protected set; }

        private bool _NameVisibility = true;
        public bool NameVisibility
        {
            get => _NameVisibility;
            set => this.RaiseAndSetIfChanged(ref _NameVisibility, value);
        }

        private ObservableAsPropertyHelper<bool> _Visible;
        public bool Visible => _Visible.Value;

        public BaseButton(
            string name,
            Optional<IObservable<bool>> visible = default) : base(name)
        {
            _Visible = visible
                .ValueOr(() => Observable.Return(true))
                .ToProperty(this, v => v.Visible);
        }

        public abstract void Initialize();
    }

    public class CmdButton : BaseButton
    {
        public CmdButton(
         string name,
         ReactiveCommand<Unit, Unit> command,
         Optional<IObservable<bool>> visible = default)
         : base(name, visible)
         => Command = command;

        public CmdButton(
            string name,
            Action action,
            Optional<IObservable<bool>> can_execute = default,
            Optional<IObservable<bool>> visible = default)
            : this(name, ReactiveCommand.Create(action, can_execute.ValueOr(() => Observable.Return(true))), visible)
        {
        }

        public CmdButton(
            string name,
            Func<Task> task,
            Optional<IObservable<bool>> can_execute = default,
            Optional<IObservable<bool>> visible = default)
            : this(name, ReactiveCommand.CreateFromTask(task, can_execute.ValueOr(() => Observable.Return(true))), visible)
        {
        }

        public override void Initialize()
        {
            AddCommand("command", Command);
        }
    }

    public class ToggleButton : BaseButton
    {
        public IFlux Flux { get; }
        public OSAI_VariableBool PLCVariable { get; }

        private ObservableAsPropertyHelper<bool> _IsActive;
        public bool IsActive => _IsActive.Value;

        public ToggleButton(
            string name,
            IFlux flux,
            IObservable<bool> is_active,
            Optional<IObservable<bool>> visible = default)
            : base(name, visible)
        {
            Flux = flux;
            _IsActive = is_active
                .ToProperty(this, v => v.IsActive);
        }

        public ToggleButton(
            string name,
            IFlux flux,
            Action toggle,
            IObservable<bool> is_active,
            Optional<IObservable<bool>> can_execute = default,
            Optional<IObservable<bool>> visible = default)
            : this(name, flux, is_active, visible)
        {
            Command = ReactiveCommand.Create(() => toggle?.Invoke(), can_execute.ValueOr(() => Observable.Return(true)));
        }

        public ToggleButton(
            string name,
            IFlux flux,
            Func<Task> toggle,
            IObservable<bool> is_active,
            Optional<IObservable<bool>> can_execute = default,
            Optional<IObservable<bool>> visible = default)
            : this(name, flux, is_active, visible)
        {
            Command = ReactiveCommand.CreateFromTask(async () => await toggle?.Invoke(), can_execute.ValueOr(() => Observable.Return(true)));
        }

        public ToggleButton(
            string name,
            FluxViewModel flux,
            IFLUX_Variable<bool, bool> @bool,
            Optional<IObservable<bool>> can_execute = default,
            Optional<IObservable<bool>> visible = default)
            : this(
                name,
                flux,
                () => flux.ConnectionProvider.ToggleVariableAsync(@bool),
                @bool.ValueChanged.ValueOr(() => false),
                can_execute,
                visible)
        {
        }

        public override void Initialize()
        {
            var isActive = this.WhenAnyValue(v => v.IsActive)
                .Select(a => a.ToOptional());
            
            AddCommand("command", Command, isActive);
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
            bool nameVisibility,
            Optional<IObservable<bool>> can_navigate = default,
            Optional<IObservable<bool>> visible = default) :
            base(route.Name, () => flux.Navigator.Navigate(route, reset), can_navigate, visible)
        {
            Route = route;
            Flux = flux;
            NameVisibility = nameVisibility;
        }

        public NavButton(
            IFlux flux,
            NavModalViewModel modal,
            bool nameVisibility,
            Optional<IObservable<bool>> can_navigate = default,
            Optional<IObservable<bool>> visible = default) :
            base(modal.Content.Name, () => flux.Navigator.Navigate(modal, false), can_navigate, visible)
        {
            Route = modal;
            Flux = flux;
            NameVisibility = nameVisibility;
        }
    }
}
