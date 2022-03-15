using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class FeederViewModel : RemoteControl<FeederViewModel>, IFluxFeederViewModel
    {
        IFlux IFluxFeederViewModel.Flux => Flux;
        IFluxFeedersViewModel IFluxFeederViewModel.Feeders => Feeders;

        public FluxViewModel Flux { get; }
        public FeedersViewModel Feeders { get; }

        // FEEDER
        private ObservableAsPropertyHelper<EFeederState> _FeederState;
        public EFeederState FeederState => _FeederState.Value;

        public MaterialViewModel Material { get; }
        public ToolNozzleViewModel ToolNozzle { get; }
        public ToolMaterialViewModel ToolMaterial { get; }

        IFluxMaterialViewModel IFluxFeederViewModel.Material => Material;
        IFluxToolNozzleViewModel IFluxFeederViewModel.ToolNozzle => ToolNozzle;
        IFluxToolMaterialViewModel IFluxFeederViewModel.ToolMaterial => ToolMaterial;

        public ReactiveCommand<Unit, Unit> UpdateMaterialTagCommand { get; internal set; }
        public ReactiveCommand<Unit, Unit> UpdateToolNozzleTagCommand { get; internal set; }

        private ObservableAsPropertyHelper<bool> _HasInvalidState;
        public bool HasInvalidState => _HasInvalidState.Value;

        public IObservable<bool> HasInvalidStateChanged { get; }
        public IObservable<EFeederState> FeederStateChanged { get; }

        [RemoteOutput(false)]
        public ushort Position { get; }

        private ObservableAsPropertyHelper<string> _FeederStateStr;
        [RemoteOutput(true)]
        public string FeederStateStr => _FeederStateStr.Value;

        private ObservableAsPropertyHelper<string> _FeederBrush;
        [RemoteOutput(true)]
        public string FeederBrush => _FeederBrush.Value;

        private ObservableAsPropertyHelper<string> _ToolNozzleBrush;
        [RemoteOutput(true)]
        public string ToolNozzleBrush => _ToolNozzleBrush.Value;

        private ObservableAsPropertyHelper<string> _MaterialBrush;
        [RemoteOutput(true)]
        public string MaterialBrush => _MaterialBrush.Value;

        private ObservableAsPropertyHelper<bool> _MaterialLoaded;
        [RemoteOutput(true)]
        public bool MaterialLoaded => _MaterialLoaded.Value;

        // CONSTRUCTOR
        public FeederViewModel(FeedersViewModel feeders, ushort position) : base($"{typeof(FeederViewModel).GetRemoteControlName()}??{position}")
        {
            Feeders = feeders;
            Flux = feeders.Flux;
            Position = position;

            Material = new MaterialViewModel(this);
            ToolNozzle = new ToolNozzleViewModel(this);
            ToolMaterial = new ToolMaterialViewModel(this);

            ToolMaterial.Initialize();
            ToolNozzle.Initialize();
            Material.Initialize();

            _FeederState = ToolNozzle.WhenAnyValue(v => v.State)
                .Select(FindFeederState)
                .ToProperty(this, v => v.FeederState);

            _HasInvalidState = this.WhenAnyValue(f => f.FeederState)
                .Select(f => f == EFeederState.ERROR)
                .ToProperty(this, v => v.HasInvalidState);

            _MaterialLoaded = Material.WhenAnyValue(v => v.State)
                .Select(s => s.IsLoaded())
                .ToProperty(this, v => v.MaterialLoaded);

            FeederStateChanged = this.WhenAnyValue(f => f.FeederState);
            HasInvalidStateChanged = this.WhenAnyValue(f => f.HasInvalidState);

            UpdateMaterialTagCommand = ReactiveCommand.CreateFromTask(async () =>
                {
                    var operator_usb = Flux.MCodes.OperatorUSB;
                    var reading = await Material.ReadTagAsync(default, true, operator_usb.ConvertOr(o => o.RewriteNFC, () => false));
                    if (reading.HasValue)
                        await Material.StoreTagAsync(reading.Value);
                },
                Flux.StatusProvider.CanSafeCycle,
                RxApp.MainThreadScheduler);

            UpdateToolNozzleTagCommand = ReactiveCommand.CreateFromTask(async () =>
                {
                    var operator_usb = Flux.MCodes.OperatorUSB;
                    var reading = await ToolNozzle.ReadTagAsync(default, true, operator_usb.ConvertOr(o => o.RewriteNFC, () => false));
                    if (reading.HasValue)
                        await ToolNozzle.StoreTagAsync(reading.Value);
                },
                Flux.StatusProvider.CanSafeCycle,
                RxApp.MainThreadScheduler);

            AddCommand("unloadMaterial", Material.UnloadCommand);
            AddCommand("updateMaterial", UpdateMaterialTagCommand);
            AddCommand("loadPurgeMaterial", Material.LoadPurgeCommand);
            AddOutput("materialPercentage", Material.Odometer.WhenAnyValue(v => v.Percentage));
            AddOutput("materialName", Material.WhenAnyValue(v => v.Document).Convert(n => n.Name));
            AddOutput("materialWeight", Material.WhenAnyValue(v => v.Nfc).Select(d => d.Tag).Convert(n => n.CurWeightG), typeof(WeightConverter));

            AddCommand("changeToolNozzle", ToolNozzle.ChangeCommand);
            AddCommand("updateToolNozzle", UpdateToolNozzleTagCommand);
            AddOutput("nozzlePercentage", ToolNozzle.Odometer.WhenAnyValue(v => v.Percentage));
            AddOutput("nozzleName", ToolNozzle.WhenAnyValue(v => v.Document).Select(d => d.nozzle).Convert(n => n.Name));
            AddOutput("nozzleTemperature", ToolNozzle.WhenAnyValue(v => v.Temperature), typeof(FluxTemperatureConverter));
            AddOutput("nozzleWeight", ToolNozzle.WhenAnyValue(v => v.Nfc).Select(d => d.Tag).Convert(n => n.CurWeightG), typeof(WeightConverter));

            _FeederStateStr = this.WhenAnyValue(v => v.FeederState).Select(state => state switch
            {
                EFeederState.FEEDER_SELECTED => "ATTIVA",
                EFeederState.FEEDER_WAIT => "ATTESA",
                EFeederState.FEEDER_EMPTY => "VUOTA",
                _ => "ERRORE",
            }).ToProperty(this, v => v.FeederStateStr);

            _FeederBrush = this.WhenAnyValue(v => v.FeederState).Select(state => state switch
            {
                EFeederState.FEEDER_SELECTED => FluxColors.Selected,
                EFeederState.FEEDER_WAIT => FluxColors.Inactive,
                EFeederState.FEEDER_EMPTY => FluxColors.Empty,
                _ => FluxColors.Error,
            }).ToProperty(this, v => v.FeederBrush);

            _ToolNozzleBrush = ToolNozzle.WhenAnyValue(v => v.State).Select(state =>
            {
                if (state.IsNotLoaded())
                    return FluxColors.Empty;
                if (!state.IsLoaded() || state.InMateinance)
                    return FluxColors.Warning;
                if (state.IsOnTrailer())
                    return FluxColors.Active;
                if (state.IsInMagazine())
                    return FluxColors.Inactive;
                return FluxColors.Error;
            }).ToProperty(this, v => v.ToolNozzleBrush);

            _MaterialBrush = Observable.CombineLatest(
                Material.WhenAnyValue(v => v.State),
                ToolMaterial.WhenAnyValue(v => v.State),
                (m, tm) =>
                {
                    if (!tm.KnownNozzle)
                        return FluxColors.Empty;
                    if (tm.KnownMaterial && !tm.Compatible)
                        return FluxColors.Error;
                    if (m.IsNotLoaded())
                        return FluxColors.Empty;
                    if (!m.Known)
                        return FluxColors.Warning;
                    if (!m.IsLoaded())
                        return FluxColors.Inactive;
                    if (!m.Locked)
                        return FluxColors.Error;
                    return FluxColors.Active;
                }).ToProperty(this, v => v.MaterialBrush);
        }

        // FEEDER
        private EFeederState FindFeederState(ToolNozzleState tool)
        {
            if (tool.ChangeError)
                return EFeederState.ERROR;
            if (!tool.Inserted)
                return EFeederState.FEEDER_EMPTY;
            if (tool.InMateinance)
                return EFeederState.FEEDER_WAIT;
            if (tool.InMagazine && !tool.OnTrailer)
                return EFeederState.FEEDER_WAIT;
            if (!tool.InMagazine && tool.OnTrailer)
                return EFeederState.FEEDER_SELECTED;
            return EFeederState.ERROR;
        }
    }
}
