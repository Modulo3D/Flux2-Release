using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Linq;
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
        private readonly ObservableAsPropertyHelper<EFeederState> _FeederState;
        public EFeederState FeederState => _FeederState.Value;

        [RemoteContent(false)]
        public ToolNozzleViewModel ToolNozzle { get; }
        private readonly ObservableAsPropertyHelper<Optional<IFluxMaterialViewModel>> _SelectedMaterial;
        public Optional<IFluxMaterialViewModel> SelectedMaterial => _SelectedMaterial.Value;

        [RemoteContent(true)]
        public IObservableCache<IFluxMaterialViewModel, ushort> Materials { get; }

        IFluxToolNozzleViewModel IFluxFeederViewModel.ToolNozzle => ToolNozzle;

        private readonly ObservableAsPropertyHelper<bool> _HasInvalidState;
        public bool HasInvalidState => _HasInvalidState.Value;

        public IObservable<bool> HasInvalidStateChanged { get; }
        public IObservable<EFeederState> FeederStateChanged { get; }

        [RemoteOutput(false)]
        public ushort Position { get; }

        private readonly ObservableAsPropertyHelper<RemoteText> _FeederStateStr;
        [RemoteOutput(true)]
        public RemoteText FeederStateStr => _FeederStateStr.Value;

        private readonly ObservableAsPropertyHelper<string> _FeederBrush;
        [RemoteOutput(true)]
        public string FeederBrush => _FeederBrush.Value;

        private readonly ObservableAsPropertyHelper<string> _ToolNozzleBrush;
        [RemoteOutput(true)]
        public string ToolNozzleBrush => _ToolNozzleBrush.Value;

        public ushort MixingCount { get; }

        // CONSTRUCTOR
        public FeederViewModel(FeedersViewModel feeders, ushort position, ushort mixing_count) : base($"{position}")
        {
            Feeders = feeders;
            Flux = feeders.Flux;
            Position = position;
            MixingCount = mixing_count;
            ToolNozzle = new ToolNozzleViewModel(feeders, this);

            var extruders = Flux.SettingsProvider
                .WhenAnyValue(v => v.ExtrudersCount);

            // TODO
            Materials = Feeders.ToolMaterials.Connect()
                .Filter(m => m.Feeder == this)
                .AsObservableCacheRC(this);

            var selected_positions = Flux.ConnectionProvider
                .ObserveVariable(c => c.FILAMENT_AFTER_GEAR)
                .Convert(c => c.QueryWhenChanged());

            var materials = Materials.Connect()
                .QueryWhenChanged();

            _SelectedMaterial = Observable.CombineLatest(
               materials, extruders, selected_positions, FindSelectedViewModel)
                .ToPropertyRC(this, v => v.SelectedMaterial);

            foreach (var material in Materials.Items.Cast<MaterialViewModel>())
                material.Initialize();

            ToolNozzle.Initialize();

            _FeederState = ToolNozzle.WhenAnyValue(v => v.State)
                .Select(FindFeederState)
                .ToPropertyRC(this, v => v.FeederState);

            _HasInvalidState = this.WhenAnyValue(f => f.FeederState)
                .Select(f => f == EFeederState.ERROR)
                .ToPropertyRC(this, v => v.HasInvalidState);

            FeederStateChanged = this.WhenAnyValue(f => f.FeederState);
            HasInvalidStateChanged = this.WhenAnyValue(f => f.HasInvalidState);

            _FeederStateStr = this.WhenAnyValue(v => v.FeederState)
                .Select(state => state switch
                {
                    EFeederState.FEEDER_SELECTED => new RemoteText("selected", true),
                    EFeederState.FEEDER_WAIT => new RemoteText("wait", true),
                    EFeederState.FEEDER_EMPTY => new RemoteText("empty", true),
                    EFeederState.IN_CHANGE => new RemoteText("inChange", true),
                    _ => new RemoteText("error", true),
                })
                .ToPropertyRC(this, v => v.FeederStateStr);

            _FeederBrush = this.WhenAnyValue(v => v.FeederState)
                .Select(state => state switch
                {
                    EFeederState.FEEDER_SELECTED => FluxColors.Selected,
                    EFeederState.FEEDER_WAIT => FluxColors.Inactive,
                    EFeederState.FEEDER_EMPTY => FluxColors.Empty,
                    EFeederState.IN_CHANGE => FluxColors.Idle,
                    _ => FluxColors.Error,
                })
                .ToPropertyRC(this, v => v.FeederBrush);

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
                .ToPropertyRC(this, v => v.ToolNozzleBrush);
        }

        private Optional<TViewModel> FindSelectedViewModel<TViewModel>(
            IQuery<TViewModel, ushort> viewmodels,
            Optional<(ushort machine_extruders, ushort mixing_extruders)> extruders,
            OptionalChange<IQuery<Optional<bool>, VariableAlias>> wire_presence)
        {
            if (!extruders.HasValue)
                return default;
            if (!wire_presence.HasChange)
            {
                if (extruders.Value.mixing_extruders == 1)
                    return viewmodels.Lookup(Position);
                return default;
            }

            var wire_range_start = Position * extruders.Value.mixing_extruders;
            var wire_range_end = wire_range_start + extruders.Value.mixing_extruders;

            var selected_wire = wire_presence.Change.Items
                .Select((wire, index) => (wire, index: (short)index))
                .FirstOrOptional(t => t.index >= wire_range_start && t.index < wire_range_end && t.wire == true);

            if (!selected_wire.HasValue)
                return default;

            var variable_store = Flux.ConnectionProvider.VariableStoreBase;
            return viewmodels.Lookup((ushort)selected_wire.Value.index);
        }

        // FEEDER
        private EFeederState FindFeederState(ToolNozzleState tool)
        {
            if (tool.InChange)
                return EFeederState.IN_CHANGE;
            if (tool.ChangeError)
                return EFeederState.ERROR;
            if (!tool.Inserted)
                return EFeederState.FEEDER_EMPTY;
            if (tool.InMateinance)
                return EFeederState.FEEDER_WAIT;
            if (!tool.Selected || (tool.IsInMagazine() && !tool.IsOnTrailer()))
                return EFeederState.FEEDER_WAIT;
            if (tool.Selected && tool.IsOnTrailer() && !tool.IsInMagazine())
                return EFeederState.FEEDER_SELECTED;
            return EFeederState.ERROR;
        }
    }
}
