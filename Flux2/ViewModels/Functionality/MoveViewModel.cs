using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using Splat;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class MoveViewModel : FluxRoutableViewModel<MoveViewModel>
    {
        private double _MoveExponent = 1;
        [RemoteInput(1, -2, 3)]
        public double MoveExponent
        {
            get => _MoveExponent;
            set => this.RaiseAndSetIfChanged(ref _MoveExponent, value);
        }

        private ObservableAsPropertyHelper<double> _MoveDistance;
        [RemoteOutput(true)]
        public double MoveDistance => _MoveDistance.Value;

        private double _Feedrate = 1000;
        [RemoteInput(100, 0)]
        public double Feedrate
        {
            get => _Feedrate;
            set => this.RaiseAndSetIfChanged(ref _Feedrate, value);
        }

        public ReactiveCommand<Unit, Unit> IncreaseExponent { get; }
        public ReactiveCommand<Unit, Unit> DecreaseExponent { get; }

        public ReactiveCommand<Unit, Unit> IncreaseFeedrate { get; }
        public ReactiveCommand<Unit, Unit> DecreaseFeedrate { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MoveLeft { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MoveRight { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MoveBack { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MoveFront { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MoveUp { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MoveDown { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> Extrude { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> Retract { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> Stop { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ShowRoutinesCommand { get; }

        private ObservableAsPropertyHelper<string> _AxisPosition;
        [RemoteOutput(true)]
        public string AxisPosition => _AxisPosition.Value;

        public MoveViewModel(FluxViewModel flux) : base(flux)
        {
            _MoveDistance = this.WhenAnyValue(v => v.MoveExponent)
                .Select(e => Math.Pow(10, e))
                .ToProperty(this, v => v.MoveDistance);

            _AxisPosition = Flux.ConnectionProvider.ObserveVariable(m => m.AXIS_POSITION)
                .QueryWhenChanged(p => string.Join(" ", p.KeyValues.Select(v => $"{v.Key}{v.Value:0.00}")))
                .ToProperty(this, v => v.AxisPosition);

            IncreaseExponent = ReactiveCommand.Create(() => { MoveExponent++; });
            DecreaseExponent = ReactiveCommand.Create(() => { MoveExponent--; });

            IncreaseFeedrate = ReactiveCommand.Create(() => { Feedrate += 200; });
            DecreaseFeedrate = ReactiveCommand.Create(() => { Feedrate -= 200; });

            ShowRoutinesCommand = ReactiveCommand.Create(() => { Flux.Navigator.NavigateModal(Flux.Functionality.Routines); });

            var can_move = Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(e => 
                    e.IsIdle.ValueOrDefault() && 
                    e.IsHomed.ValueOrDefault() && 
                    e.IsEnabledAxis.ValueOrDefault());

            async Task moveAsync(Func<IFLUX_Connection, string[]> func)
            {
                await Flux.ConnectionProvider.ExecuteParamacroAsync(func);
            }

            MoveLeft    = ReactiveCommand.CreateFromTask(() => moveAsync(c => c.GetRelativeXMovementGCode(-MoveDistance, Feedrate)), can_move);
            MoveRight   = ReactiveCommand.CreateFromTask(() => moveAsync(c => c.GetRelativeXMovementGCode(MoveDistance, Feedrate)), can_move);
            MoveBack    = ReactiveCommand.CreateFromTask(() => moveAsync(c => c.GetRelativeYMovementGCode(-MoveDistance, Feedrate)), can_move);
            MoveFront   = ReactiveCommand.CreateFromTask(() => moveAsync(c => c.GetRelativeYMovementGCode(MoveDistance, Feedrate)), can_move);
                                                          
            MoveUp      = ReactiveCommand.CreateFromTask(() => moveAsync(c => c.GetRelativeZMovementGCode(-MoveDistance, Feedrate)), can_move);
            MoveDown    = ReactiveCommand.CreateFromTask(() => moveAsync(c => c.GetRelativeZMovementGCode(MoveDistance, Feedrate)), can_move);
                                                               
            Extrude     = ReactiveCommand.CreateFromTask(() => moveAsync(c => c.GetRelativeEMovementGCode(MoveDistance, Feedrate)), can_move);
            Retract     = ReactiveCommand.CreateFromTask(() => moveAsync(c => c.GetRelativeEMovementGCode(-MoveDistance, Feedrate)), can_move);

            Stop = ReactiveCommand.CreateFromTask(async () => { await Flux.ConnectionProvider.ResetAsync(); });
        }
    }
}
