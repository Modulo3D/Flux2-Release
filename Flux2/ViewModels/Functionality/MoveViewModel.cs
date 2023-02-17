using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class MoveViewModel : FluxRoutableViewModel<MoveViewModel>
    {
        private double _MovePrinterExponent = 1;
        [RemoteInput(step: 1, min: -2, max: 3)]
        public double MovePrinterExponent
        {
            get => _MovePrinterExponent;
            set => this.RaiseAndSetIfChanged(ref _MovePrinterExponent, value);
        }

        private readonly ObservableAsPropertyHelper<double> _MovePrinterDistance;
        [RemoteOutput(true, converter:typeof(MillimeterConverter))]
        public double MovePrinterDistance => _MovePrinterDistance.Value;

        private double _MovePrinterFeedrate = 1000;
        [RemoteInput(step: 100, min: 0, converter:typeof(FeedrateConverter))]
        public double MovePrinterFeedrate
        {
            get => _MovePrinterFeedrate;
            set => this.RaiseAndSetIfChanged(ref _MovePrinterFeedrate, value);
        }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MovePrinterLeftCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MovePrinterRightCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MovePrinterBackCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MovePrinterFrontCommand { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MovePrinterUpCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MovePrinterDownCommand { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MovePrinterExtrudeCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MovePrinterRetractCommand { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> StopPrinterCommand { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ShowRoutinesCommand { get; }

        private readonly ObservableAsPropertyHelper<string> _AxisPosition;
        [RemoteOutput(true)]
        public string AxisPosition => _AxisPosition.Value;
        
        public MoveViewModel(FluxViewModel flux) : base(flux)
        {
            var variable_store = Flux.ConnectionProvider.VariableStoreBase;

            _MovePrinterDistance = this.WhenAnyValue(v => v.MovePrinterExponent)
                .Select(e => Math.Pow(10, e))
                .ToPropertyRC(this, v => v.MovePrinterDistance);

            var move_transform = variable_store.MoveTransform;
            _AxisPosition = Flux.ConnectionProvider.ObserveVariable(m => m.AXIS_POSITION)
                .Convert(c => move_transform.TransformPosition(c, false))
                .ConvertOr(c => c.GetAxisPosition(), () => "")
                .ToPropertyRC(this, v => v.AxisPosition);

            ShowRoutinesCommand = ReactiveCommandRC.CreateFromTask(async () => { await Flux.ShowModalDialogAsync(f => f.Functionality.Routines); }, this);

            var has_limits = variable_store.HasMovementLimits;
            var can_move_xyz = Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(e => e.CanSafeCycle && has_limits);
            
            var can_move_e = Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(e => e.CanSafeCycle);

            async Task moveAsync(char axis, double move_distance)
            {
                using var put_move_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await Flux.ConnectionProvider.ExecuteParamacroAsync(c => c.GetMovementGCode((axis, move_distance, MovePrinterFeedrate), variable_store.MoveTransform), put_move_cts.Token, false);
            }

            MovePrinterLeftCommand = ReactiveCommandRC.CreateFromTask(() => moveAsync('X', -MovePrinterDistance), this, can_move_xyz);
            MovePrinterRightCommand = ReactiveCommandRC.CreateFromTask(() => moveAsync('X', MovePrinterDistance), this, can_move_xyz);

            MovePrinterBackCommand = ReactiveCommandRC.CreateFromTask(() => moveAsync('Y', -MovePrinterDistance), this, can_move_xyz);
            MovePrinterFrontCommand = ReactiveCommandRC.CreateFromTask(() => moveAsync('Y', MovePrinterDistance), this, can_move_xyz);

            MovePrinterUpCommand = ReactiveCommandRC.CreateFromTask(() => moveAsync('Z', -MovePrinterDistance), this, can_move_xyz);
            MovePrinterDownCommand = ReactiveCommandRC.CreateFromTask(() => moveAsync('Z', MovePrinterDistance), this, can_move_xyz);

            MovePrinterExtrudeCommand = ReactiveCommandRC.CreateFromTask(() => moveAsync(variable_store.FeederAxis, MovePrinterDistance), this, can_move_e);
            MovePrinterRetractCommand = ReactiveCommandRC.CreateFromTask(() => moveAsync(variable_store.FeederAxis, -MovePrinterDistance), this, can_move_e);

            StopPrinterCommand = ReactiveCommandRC.CreateFromTask(Flux.ConnectionProvider.StopAsync, this);
        }
    }
}
