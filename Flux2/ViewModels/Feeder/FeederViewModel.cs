using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
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

        public ToolNozzleViewModel ToolNozzle { get; }
        public ToolMaterialViewModel ToolMaterial { get; }
        public SelectableCache<IFluxMaterialViewModel, ushort> Materials { get; }

        IFluxToolNozzleViewModel IFluxFeederViewModel.ToolNozzle => ToolNozzle;
        IFluxToolMaterialViewModel IFluxFeederViewModel.ToolMaterial => ToolMaterial;

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

        private ObservableAsPropertyHelper<Optional<ushort>> _MaterialLoaded;
        [RemoteOutput(true)]
        public Optional<ushort> MaterialLoaded => _MaterialLoaded.Value;


        // CONSTRUCTOR
        public FeederViewModel(FeedersViewModel feeders, ushort position) : base($"{typeof(FeederViewModel).GetRemoteControlName()}??{position}")
        {
            Feeders = feeders;
            Flux = feeders.Flux;
            Position = position;

            ToolNozzle = new ToolNozzleViewModel(this);
            ToolMaterial = new ToolMaterialViewModel(this);
            Materials = new SelectableCache<IFluxMaterialViewModel, ushort>(default);

            ToolMaterial.Initialize();
            ToolNozzle.Initialize();

            _FeederState = ToolNozzle.WhenAnyValue(v => v.State)
                .Select(FindFeederState)
                .ToProperty(this, v => v.FeederState)
                .DisposeWith(Disposables);

            _HasInvalidState = this.WhenAnyValue(f => f.FeederState)
                .Select(f => f == EFeederState.ERROR)
                .ToProperty(this, v => v.HasInvalidState)
                .DisposeWith(Disposables);

            _MaterialLoaded = Materials.ItemsSource.Connect()
                .AutoRefreshOnObservable(m => m.ConvertOr(m => m.WhenAnyValue(m => m.State), () => Observable.Return(new MaterialState())))
                .QueryWhenChanged(m => m.Items.SingleOrDefault(m => m.HasValue && m.Value.State.IsLoaded()))
                .Convert(m => m.Position)
                .ToProperty(this, v => v.MaterialLoaded)
                .DisposeWith(Disposables);

            FeederStateChanged = this.WhenAnyValue(f => f.FeederState);
            HasInvalidStateChanged = this.WhenAnyValue(f => f.HasInvalidState);     

            _FeederStateStr = this.WhenAnyValue(v => v.FeederState)
                .Select(state => state switch
                {
                    EFeederState.FEEDER_SELECTED => "ATTIVA",
                    EFeederState.FEEDER_WAIT => "ATTESA",
                    EFeederState.FEEDER_EMPTY => "VUOTA",
                    _ => "ERRORE",
                })
                .ToProperty(this, v => v.FeederStateStr)
                .DisposeWith(Disposables);

            _FeederBrush = this.WhenAnyValue(v => v.FeederState)
                .Select(state => state switch
                {
                    EFeederState.FEEDER_SELECTED => FluxColors.Selected,
                    EFeederState.FEEDER_WAIT => FluxColors.Inactive,
                    EFeederState.FEEDER_EMPTY => FluxColors.Empty,
                    _ => FluxColors.Error,
                })
                .ToProperty(this, v => v.FeederBrush)
                .DisposeWith(Disposables);

            _ToolNozzleBrush = ToolNozzle.WhenAnyValue(v => v.State)
                .Select(state =>
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
                })
                .ToProperty(this, v => v.ToolNozzleBrush)
                .DisposeWith(Disposables);

            var material = Materials.SelectedValueChanged;

            _MaterialBrush = Observable.CombineLatest(
                material.ConvertMany(m => m.WhenAnyValue(v => v.State)),
                ToolMaterial.WhenAnyValue(v => v.State),
                (m, tm) =>
                {
                    if (!m.HasValue || !tm.KnownNozzle)
                        return FluxColors.Empty;
                    if (tm.KnownMaterial && !tm.Compatible)
                        return FluxColors.Error;
                    if (m.Value.IsNotLoaded())
                        return FluxColors.Empty;
                    if (!m.Value.Known)
                        return FluxColors.Warning;
                    if (!m.Value.IsLoaded())
                        return FluxColors.Inactive;
                    if (!m.Value.Locked)
                        return FluxColors.Error;
                    return FluxColors.Active;
                }).ToProperty(this, v => v.MaterialBrush)
                .DisposeWith(Disposables);
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
