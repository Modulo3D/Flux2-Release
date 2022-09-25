using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class MagazineItemViewModel : RemoteControl<MagazineItemViewModel>
    {
        public FluxViewModel Flux { get; }
        public IFluxFeederViewModel Feeder { get; }

        private ObservableAsPropertyHelper<string> _ToolNozzeBrush;
        [RemoteOutput(true)]
        public string ToolNozzeBrush => _ToolNozzeBrush.Value;

        private ObservableAsPropertyHelper<string> _Nozzle;
        [RemoteOutput(true)]
        public string Nozzle => _Nozzle.Value;

        [RemoteOutput(false)]
        public ushort Position => Feeder.Position;

        public MagazineItemViewModel(FluxViewModel flux, IFluxFeederViewModel feeder) : base($"{typeof(MagazineItemViewModel).GetRemoteControlName()}??{feeder.Position}")
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
        }
    }
}
