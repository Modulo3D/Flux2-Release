using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
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
                .QueryWhenChanged(p => string.Join(" ", p.KeyValues.Select(v => $"{v.Key}{v.Value:0.00}")))
                .ToProperty(this, v => v.AxisPosition);

            ShowRoutinesCommand = ReactiveCommand.Create(() => { Flux.Navigator.NavigateModal(Flux.Functionality.Routines); });

            var can_move = Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(e =>
                    e.IsIdle.ValueOrDefault() &&
                    e.IsHomed.ValueOrDefault() &&
                    e.IsEnabledAxis.ValueOrDefault());

            async Task moveAsync(Func<IFLUX_Connection, Optional<IEnumerable<string>>> func)
            {
                await Flux.ConnectionProvider.ExecuteParamacroAsync(func, false);
            }

            MovePrinterLeftCommand = ReactiveCommand.CreateFromTask(() => moveAsync(c => c.GetRelativeXMovementGCode(-MovePrinterDistance, MovePrinterFeedrate)), can_move);
            MovePrinterRightCommand = ReactiveCommand.CreateFromTask(() => moveAsync(c => c.GetRelativeXMovementGCode(MovePrinterDistance, MovePrinterFeedrate)), can_move);

            MovePrinterBackCommand = ReactiveCommand.CreateFromTask(() => moveAsync(c => c.GetRelativeYMovementGCode(-MovePrinterDistance, MovePrinterFeedrate)), can_move);
            MovePrinterFrontCommand = ReactiveCommand.CreateFromTask(() => moveAsync(c => c.GetRelativeYMovementGCode(MovePrinterDistance, MovePrinterFeedrate)), can_move);

            MovePrinterUpCommand = ReactiveCommand.CreateFromTask(() => moveAsync(c => c.GetRelativeZMovementGCode(-MovePrinterDistance, MovePrinterFeedrate)), can_move);
            MovePrinterDownCommand = ReactiveCommand.CreateFromTask(() => moveAsync(c => c.GetRelativeZMovementGCode(MovePrinterDistance, MovePrinterFeedrate)), can_move);

            MovePrinterExtrudeCommand = ReactiveCommand.CreateFromTask(() => moveAsync(c => c.GetRelativeEMovementGCode(MovePrinterDistance, MovePrinterFeedrate)), can_move);
            MovePrinterRetractCommand = ReactiveCommand.CreateFromTask(() => moveAsync(c => c.GetRelativeEMovementGCode(-MovePrinterDistance, MovePrinterFeedrate)), can_move);

            StopPrinterCommand = ReactiveCommand.CreateFromTask(async () => { await Flux.ConnectionProvider.ResetAsync(); });
        }
    }
}
