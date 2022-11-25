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

        private ObservableAsPropertyHelper<double> _MovePrinterDistance;
        [RemoteOutput(true)]
        public double MovePrinterDistance => _MovePrinterDistance.Value;

        private double _MovePrinterFeedrate = 1000;
        [RemoteInput(step: 100, min: 0)]
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

        private ObservableAsPropertyHelper<string> _AxisPosition;
        [RemoteOutput(true)]
        public string AxisPosition => _AxisPosition.Value;

        public MoveViewModel(FluxViewModel flux) : base(flux)
        {
            _MovePrinterDistance = this.WhenAnyValue(v => v.MovePrinterExponent)
                .Select(e => Math.Pow(10, e))
                .ToProperty(this, v => v.MovePrinterDistance);

            _AxisPosition = Flux.ConnectionProvider.ObserveVariable(m => m.AXIS_POSITION)
                .Convert(c => c.QueryWhenChanged(p => string.Join(" ", p.KeyValues.Select(v => $"{v.Key}{v.Value:0.00}"))))
                .ObservableOr(() => "")
                .ToProperty(this, v => v.AxisPosition);

            ShowRoutinesCommand = ReactiveCommand.Create(() => { Flux.Navigator.NavigateModal(Flux.Functionality); });

            var can_move = Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(e =>
                    e.IsIdle &&
                    e.IsHomed &&
                    e.IsEnabledAxis);

            var variable_store = Flux.ConnectionProvider.VariableStoreBase;
            async Task moveAsync(char axis, double move_distance)
            {
                using var put_move_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await Flux.ConnectionProvider.ExecuteParamacroAsync(c => c.GetMovementGCode((axis, move_distance, MovePrinterFeedrate), variable_store.MoveTransform), put_move_cts.Token, false);
            }

            MovePrinterLeftCommand = ReactiveCommand.CreateFromTask(() => moveAsync('X', -MovePrinterDistance), can_move);
            MovePrinterRightCommand = ReactiveCommand.CreateFromTask(() => moveAsync('X', MovePrinterDistance), can_move);

            MovePrinterBackCommand = ReactiveCommand.CreateFromTask(() => moveAsync('Y', -MovePrinterDistance), can_move);
            MovePrinterFrontCommand = ReactiveCommand.CreateFromTask(() => moveAsync('Y', MovePrinterDistance), can_move);

            MovePrinterUpCommand = ReactiveCommand.CreateFromTask(() => moveAsync('Z', -MovePrinterDistance), can_move);
            MovePrinterDownCommand = ReactiveCommand.CreateFromTask(() => moveAsync('Z', MovePrinterDistance), can_move);

            MovePrinterExtrudeCommand = ReactiveCommand.CreateFromTask(() => moveAsync(variable_store.FeederAxis, MovePrinterDistance), can_move);
            MovePrinterRetractCommand = ReactiveCommand.CreateFromTask(() => moveAsync(variable_store.FeederAxis, -MovePrinterDistance), can_move);

            StopPrinterCommand = ReactiveCommand.CreateFromTask(async () => { await Flux.ConnectionProvider.StopAsync(); });
        }
    }
}
