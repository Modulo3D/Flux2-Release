using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class FeederViewModel : RemoteControl<FeederViewModel>, IFluxFeederViewModel
    {
        IFluxFeedersViewModel IFluxFeederViewModel.Feeders => Feeders;

        public FluxViewModel Flux { get; }
        public FeedersViewModel Feeders { get; }

        // FEEDER
        private ObservableAsPropertyHelper<EFeederState> _FeederState;
        public EFeederState FeederState => _FeederState.Value;

        [RemoteContent(false)]
        public ToolNozzleViewModel ToolNozzle { get; }
        private ObservableAsPropertyHelper<Optional<IFluxMaterialViewModel>> _SelectedMaterial;
        public Optional<IFluxMaterialViewModel> SelectedMaterial => _SelectedMaterial.Value;
        private ObservableAsPropertyHelper<Optional<IFluxToolMaterialViewModel>> _SelectedToolMaterial;
        public Optional<IFluxToolMaterialViewModel> SelectedToolMaterial => _SelectedToolMaterial.Value;

        [RemoteContent(true)]
        public IObservableCache<IFluxMaterialViewModel, ushort> Materials { get; }
        public IObservableCache<IFluxToolMaterialViewModel, ushort> ToolMaterials { get; }

        IFluxToolNozzleViewModel IFluxFeederViewModel.ToolNozzle => ToolNozzle;

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


        // CONSTRUCTOR
        public FeederViewModel(FeedersViewModel feeders, ushort position) : base($"{typeof(FeederViewModel).GetRemoteControlName()}??{position}")
        {
            Feeders = feeders;
            Flux = feeders.Flux;
            Position = position;

            ToolNozzle = new ToolNozzleViewModel(this);

            // TODO
            Materials = Flux.SettingsProvider
                .WhenAnyValue(v => v.ExtrudersCount)
                .Select(CreateMaterials)
                .ToObservableChangeSet(f => f.Position)
                .DisposeMany()
                .AsObservableCache()
                .DisposeWith(Disposables);

            ToolMaterials = Materials.Connect()
                .Transform(m => (MaterialViewModel)m)
                .Transform(m => new ToolMaterialViewModel(ToolNozzle, m))
                .Transform(tm => (IFluxToolMaterialViewModel)tm)
                .AsObservableCache();


            var selected_positions = Flux.ConnectionProvider
                .ObserveVariable(c => c.FILAMENT_AFTER_GEAR)
                .ConvertToObservable(c => c.QueryWhenChanged());

            var tool_materials = ToolMaterials.Connect()
                .QueryWhenChanged();

            _SelectedToolMaterial = Observable.CombineLatest(
               tool_materials, selected_positions, FindSelectedViewModel)
                .ToProperty(this, v => v.SelectedToolMaterial);

            var materials = Materials.Connect()
                .QueryWhenChanged();

            _SelectedMaterial = Observable.CombineLatest(
               materials, selected_positions, FindSelectedViewModel)
                .ToProperty(this, v => v.SelectedMaterial);


            foreach (ToolMaterialViewModel tm in ToolMaterials.Items)
                tm.Initialize();

            foreach (MaterialViewModel m in Materials.Items)
                m.Initialize();
            
            ToolNozzle.Initialize();

            _FeederState = ToolNozzle.WhenAnyValue(v => v.State)
                .Select(FindFeederState)
                .ToProperty(this, v => v.FeederState)
                .DisposeWith(Disposables);

            _HasInvalidState = this.WhenAnyValue(f => f.FeederState)
                .Select(f => f == EFeederState.ERROR)
                .ToProperty(this, v => v.HasInvalidState)
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
        }

        private IEnumerable<IFluxMaterialViewModel> CreateMaterials(Optional<(ushort machine_extruders, ushort mixing_extruders)> extruders)
        {
            if (!extruders.HasValue)
                yield break;
            for (ushort position = 0; position < extruders.Value.mixing_extruders; position++)
                yield return new MaterialViewModel(this, (ushort)((Position * extruders.Value.mixing_extruders) + position));
        }

        private Optional<TViewModel> FindSelectedViewModel<TViewModel>(IQuery<TViewModel, ushort> viewmodels, OptionalChange<IQuery<Optional<bool>, string>> wire_presence)
        {
            if (!wire_presence.HasChange)
                return default;
            var selected_wire = wire_presence.Change.Items.IndexOfOptional(true);
            if (!selected_wire.HasValue)
                return default;
            return viewmodels.Lookup((ushort)selected_wire.Value.Index);
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
            if (!tool.Selected || tool.IsInMagazine())
                return EFeederState.FEEDER_WAIT;
            if (tool.Selected && tool.IsOnTrailer())
                return EFeederState.FEEDER_SELECTED;
            return EFeederState.ERROR;
        }
    }
}
