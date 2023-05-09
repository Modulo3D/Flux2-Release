using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class MagazineItemViewModel : RemoteControl<MagazineItemViewModel>
    {
        public FluxViewModel Flux { get; }
        public IFluxFeederViewModel Feeder { get; }

        private readonly ObservableAsPropertyHelper<string> _ToolNozzeBrush;
        [RemoteOutput(true)]
        public string ToolNozzeBrush => _ToolNozzeBrush.Value;

        private readonly ObservableAsPropertyHelper<string> _Nozzle;
        [RemoteOutput(true)]
        public string Nozzle => _Nozzle.Value;

        [RemoteOutput(false)]
        public ushort Position => Feeder.Position;

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ResetHeaterFaultCommand { get; }

        [RemoteCommand]
        public Optional<ReactiveCommand<Unit, Unit>> RaisePistonCommand { get; }

        public MagazineItemViewModel(FluxViewModel flux, IFluxFeederViewModel feeder) 
            : base(flux.RemoteContext, $"{typeof(MagazineItemViewModel).GetRemoteElementClass()};{feeder.Position}")
        {
            Flux = flux;
            Feeder = feeder;

            _ToolNozzeBrush = Feeder.ToolNozzle.WhenAnyValue(f => f.State)
                .Select(state =>
                {
                    if (state.IsNotLoaded())
                        return FluxColors.Empty;
                    if (state.InMateinance)
                        return FluxColors.Warning;
                    if (state.IsOnTrailer() || state.IsInMagazine())
                        return FluxColors.Selected;
                    return FluxColors.Error;
                })
                .ToProperty(this, v => v.ToolNozzeBrush);

            _Nozzle = Feeder.ToolNozzle.WhenAnyValue(f => f.Document)
                .Select(d => d.nozzle.ToString())
                .ToProperty(this, v => v.Nozzle);

            // TODO
            ResetHeaterFaultCommand = ReactiveCommandRC.Create(() => { }, this, Observable.Return(false));
        }
    }
}
