using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class MaterialViewModel : TagViewModel<MaterialViewModel, NFCMaterial, Optional<Material>, MaterialState>, IFluxMaterialViewModel
    {
        public override ushort VirtualTagId => 1;

        private ObservableAsPropertyHelper<MaterialState> _State;
        public override MaterialState State => _State.Value;

        [RemoteCommand]
        public ReactiveCommandBaseRC<Unit, Unit> UnloadMaterialCommand { get; private set; }

        [RemoteCommand]
        public ReactiveCommandBaseRC<Unit, Unit> LoadPurgeMaterialCommand { get; private set; }

        private ObservableAsPropertyHelper<string> _MaterialBrush;
        [RemoteOutput(true)]
        public string MaterialBrush => _MaterialBrush.Value;

        private readonly ObservableAsPropertyHelper<Optional<string>> _DocumentLabel;
        [RemoteOutput(true)]
        public override Optional<string> DocumentLabel => _DocumentLabel.Value;

        private readonly ObservableAsPropertyHelper<Optional<bool>> _WirePresenceBeforeGear;
        [RemoteOutput(true)]
        public Optional<bool> FilamentPresenceBeforeGear => _WirePresenceBeforeGear.Value;

        private readonly ObservableAsPropertyHelper<Optional<bool>> _WirePresenceAfterGear;
        [RemoteOutput(true)]
        public Optional<bool> FilamentPresenceAfterGear => _WirePresenceAfterGear.Value;

        private readonly ObservableAsPropertyHelper<Optional<bool>> _WirePresenceOnHead;
        [RemoteOutput(true)]
        public Optional<bool> FilamentPresenceOnHead => _WirePresenceOnHead.Value;

        private readonly ObservableAsPropertyHelper<bool> _MaterialLoaded;
        [RemoteOutput(true)]
        public bool MaterialLoaded => _MaterialLoaded.Value;

        public Lazy<LoadFilamentOperationViewModel> LoadFilamentOperation { get; }

        private Lazy<UnloadFilamentOperationViewModel> UnloadFilamentOperation { get; }

        IFluxToolMaterialViewModel IFluxMaterialViewModel.ToolMaterial => ToolMaterial;
        private ToolMaterialViewModel _ToolMaterial;
        public ToolMaterialViewModel ToolMaterial
        {
            get
            {
                if (_ToolMaterial == default)
                {
                    _ToolMaterial = new ToolMaterialViewModel(Feeder.ToolNozzle, this);
                    _ToolMaterial.DisposeWith(Disposables);
                }
                return _ToolMaterial;
            }
        }

        public ObservableAsPropertyHelper<Optional<FLUX_Humidity>> _Humidity;
        [RemoteOutput(true, typeof(HumidityConverter))]
        public Optional<FLUX_Humidity> Humidity => _Humidity.Value;

        private readonly ObservableAsPropertyHelper<Optional<ExtrusionKey>> _ExtrusionKey;
        public Optional<ExtrusionKey> ExtrusionKey => _ExtrusionKey.Value;

        public MaterialViewModel(FluxViewModel flux, FeedersViewModel feeders, FeederViewModel feeder, ushort position) : base(flux, feeders, feeder, position, s => s.NFCMaterials, (db, m) =>
        {
            return m.GetDocumentAsync<Material>(db, m => m.MaterialGuid);
        }, k => k.MaterialId, t => t.MaterialGuid, watch_odometer_for_pause: true)
        {
            _ExtrusionKey = Observable.CombineLatest(
                  Feeder.ToolNozzle.NFCSlot.WhenAnyValue(t => t.Nfc),
                  NFCSlot.WhenAnyValue(m => m.Nfc),
                  (tool_nozzle, material) => Modulo3DNet.ExtrusionKey.Create(tool_nozzle, material))
                  .ToPropertyRC(this, v => v.ExtrusionKey);

            var varable_store = Flux.ConnectionProvider.VariableStoreBase;
            var material_index = ArrayIndex.FromZeroBase(Position, varable_store);
            var feeder_index = ArrayIndex.FromZeroBase(Feeder.Position, varable_store);

            var before_gear_key = Flux.ConnectionProvider.GetArrayUnit(m => m.FILAMENT_BEFORE_GEAR, material_index);
            _WirePresenceBeforeGear = Flux.ConnectionProvider.ObserveVariable(
                m => m.FILAMENT_BEFORE_GEAR,
                before_gear_key)
                .ObservableOrDefault()
                .ToPropertyRC(this, v => v.FilamentPresenceBeforeGear);

            var after_gear_key = Flux.ConnectionProvider.GetArrayUnit(m => m.FILAMENT_AFTER_GEAR, material_index);
            _WirePresenceAfterGear = Flux.ConnectionProvider.ObserveVariable(
                m => m.FILAMENT_AFTER_GEAR,
                after_gear_key)
                .ObservableOrDefault()
                .ToPropertyRC(this, v => v.FilamentPresenceAfterGear);

            var on_head_key = Flux.ConnectionProvider.GetArrayUnit(m => m.FILAMENT_ON_HEAD, feeder_index);
            _WirePresenceOnHead = Flux.ConnectionProvider.ObserveVariable(
                m => m.FILAMENT_ON_HEAD,
                on_head_key)
                .ObservableOrDefault()
                .ToPropertyRC(this, v => v.FilamentPresenceOnHead);

            _DocumentLabel = this.WhenAnyValue(v => v.Document)
                .Convert(d => d.Name)
                .ToPropertyRC(this, v => v.DocumentLabel);

            _MaterialLoaded = NFCSlot.WhenAnyValue(v => v.Nfc)
                .Select(nfc => nfc.Tag)
                .Convert(tag => tag.Loaded)
                .Convert(l => l == Feeder.Position)
                .ValueOr(() => false)
                .ToPropertyRC(this, v => v.MaterialLoaded);

            var humidity_unit = Flux.ConnectionProvider.GetArrayUnit(c => c.FILAMENT_HUMIDITY, material_index);
            _Humidity = Flux.ConnectionProvider.ObserveVariable(c => c.FILAMENT_HUMIDITY, humidity_unit)
                .ObservableOrDefault()
                .ToPropertyRC(this, v => v.Humidity);

            LoadFilamentOperation = new Lazy<LoadFilamentOperationViewModel>(() =>
            {
                var load = new LoadFilamentOperationViewModel(this);
                load.Initialize();
                return load;
            });
            UnloadFilamentOperation = new Lazy<UnloadFilamentOperationViewModel>(() =>
            {
                var unload = new UnloadFilamentOperationViewModel(this);
                unload.Initialize();
                return unload;
            });
        }

        public override void Initialize()
        {
            base.Initialize();

            _State = FindMaterialState()
                .ToPropertyRC(this, v => v.State);

            var tool_nozzle = Feeder.ToolNozzle
                .WhenAnyValue(t => t.State);

            _MaterialBrush = this.WhenAnyValue(v => v.State)
                .Select(s => 
                {
                    if (!s.Known)
                        return FluxColors.Empty;
                    if (!s.Inserted)
                        return FluxColors.Empty;
                    if (!s.Loaded)
                        return FluxColors.Inactive;
                    if (!s.Locked)
                        return FluxColors.Warning;
                    if (!s.Selected)
                        return FluxColors.Active;
                    return FluxColors.Selected;
                })
                .ToPropertyRC(this, v => v.MaterialBrush);

            var material = Observable.CombineLatest(
                tool_nozzle,
                this.WhenAnyValue(v => v.State),
                ToolMaterial.WhenAnyValue(v => v.State),
                Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation).Select(s => s.CanSafeCycle),
                (tool_nozzle, material, tool_material, can_cycle) => new
                {
                    can_unload = can_cycle,
                    can_purge = CanPurgeMaterial(can_cycle, tool_nozzle, material, tool_material),
                    can_load = CanLoadMaterial(can_cycle, tool_nozzle, material, tool_material),
                });

            UnloadMaterialCommand = ReactiveCommandBaseRC.CreateFromTask(UnloadAsync, this, material.Select(m => m.can_unload));
            LoadPurgeMaterialCommand = ReactiveCommandBaseRC.CreateFromTask(LoadPurgeAsync, this, material.Select(m => m.can_load || m.can_purge));

            var other_filament_after_gear = Feeder.Materials.Connect()
                .Filter(m => m.Position != Position)
                .TrueForAny(m => m.WhenAnyValue(m => m.FilamentPresenceAfterGear), f => f.ValueOr(() => false))
                .StartWith(false);

            // extract filament if before gear is open
            Observable.CombineLatest(
                NFCSlot.WhenAnyValue(m => m.Nfc),
                this.WhenAnyValue(v => v.FilamentPresenceBeforeGear),
                (nfc, before_gear) => (nfc, before_gear))
                .SubscribeOn(RxApp.MainThreadScheduler)
                .SubscribeRC(t =>
                {
                    var core_setting = Flux.SettingsProvider.CoreSettings.Local;
                    if (!t.before_gear.HasValue)
                        return;
                    if (t.before_gear.Value)
                        return;
                    NFCSlot.StoreTag(t => t.SetInserted(core_setting.PrinterGuid, default));
                }, this);

            // insert filament if it's the only one after gear and on after gear is closed
            Observable.CombineLatest(
                other_filament_after_gear,
                NFCSlot.WhenAnyValue(m => m.Nfc),
                this.WhenAnyValue(v => v.FilamentPresenceAfterGear),
                (other_after_gear, nfc, after_gear) => (other_after_gear, nfc, after_gear))
                .SubscribeOn(RxApp.MainThreadScheduler)
                .SubscribeRC(t =>
                {
                    if (t.other_after_gear)
                        return;
                    if (!t.after_gear.HasValue)
                        return;
                    if (!t.after_gear.Value)
                        return;

                    var core_setting = Flux.SettingsProvider.CoreSettings.Local;
                    NFCSlot.StoreTag(t => t.SetInserted(core_setting.PrinterGuid, Feeder.Position));
                }, this);

            // unload filament if on head is open
            Observable.CombineLatest(
               NFCSlot.WhenAnyValue(m => m.Nfc),
               this.WhenAnyValue(v => v.FilamentPresenceOnHead),
               (nfc, on_head) => (nfc, on_head))
               .SubscribeOn(RxApp.MainThreadScheduler)
               .SubscribeRC(t =>
               {
                   var core_setting = Flux.SettingsProvider.CoreSettings.Local;
                   if (!t.on_head.HasValue)
                       return;
                   if (t.on_head.Value)
                       return;
                   NFCSlot.StoreTag(t => t.SetLoaded(core_setting.PrinterGuid, default));
               }, this);

            // load filament if it's the only one after gear and on head is closed
            Observable.CombineLatest(
                other_filament_after_gear,
                NFCSlot.WhenAnyValue(m => m.Nfc),
                this.WhenAnyValue(v => v.FilamentPresenceOnHead),
                (other_after_gear, nfc, on_head) => (other_after_gear, nfc, on_head))
                .SubscribeOn(RxApp.MainThreadScheduler)
                .SubscribeRC(t =>
                {
                    if (t.other_after_gear)
                        return;
                    if (!t.on_head.HasValue)
                        return;
                    if (!t.on_head.Value)
                        return;

                    var core_setting = Flux.SettingsProvider.CoreSettings.Local;
                    NFCSlot.StoreTag(t => t.SetLoaded(core_setting.PrinterGuid, Feeder.Position));
                }, this);
        }
        private IObservable<MaterialState> FindMaterialState()
        {
            var inserted = NFCSlot.WhenAnyValue(m => m.Nfc)
                .Select(nfc => nfc.Tag.Convert(t => t.Inserted))
                .Convert(i => i == Feeder.Position)
                .ValueOr(() => false);

            var known = this.WhenAnyValue(m => m.Document)
                .Select(document => document.HasValue)
                .DistinctUntilChanged();

            var loaded = NFCSlot.WhenAnyValue(m => m.Nfc)
                .Select(nfc => nfc.Tag.Convert(t => t.Loaded))
                .ConvertOr(l => l == Feeder.Position, () => false);

            var core_settings = Flux.SettingsProvider.CoreSettings.Local;
            var printer_guid = core_settings.WhenAnyValue(s => s.PrinterGuid);

            var tag_printer_guid = NFCSlot.WhenAnyValue(m => m.Nfc)
                .Select(nfc => nfc.Tag
                .Convert(t => t.PrinterGuid));

            var locked = Observable.CombineLatest(
                printer_guid,
                tag_printer_guid,
                (p_guid, t_guid) => t_guid.ConvertOr(t => t == p_guid, () => false));

            var selected = Feeder.WhenAnyValue(f => f.SelectedMaterial)
                .Convert(m => m == this)
                .ValueOr(() => false);

            return Observable.CombineLatest(inserted, known, locked, loaded, selected,
                 (inserted, known, locked, loaded, selected) => new MaterialState(inserted, known, locked, loaded, selected));
        }
        private static bool CanLoadMaterial(bool can_cycle, ToolNozzleState tool_nozzle, MaterialState material, Optional<ToolMaterialState> tool_material)
        {
            if (!can_cycle)
                return false;
            if (!tool_nozzle.IsLoaded())
                return false;
            if (material.IsLoaded())
                return false;
            if (material.Locked)
                return false;
            if (!tool_material.HasValue)
                return false;
            if (tool_material.Value.Compatible.HasValue && !tool_material.Value.Compatible.Value)
                return false;
            return true;
        }
        private static bool CanPurgeMaterial(bool can_cycle, ToolNozzleState tool_nozzle, MaterialState material, Optional<ToolMaterialState> tool_material)
        {
            if (!can_cycle)
                return false;
            if (!tool_nozzle.IsLoaded())
                return false;
            if (!material.IsLoaded())
                return false;
            if (!tool_material.HasValue)
                return false;
            if (tool_material.Value.Compatible.HasValue && !tool_material.Value.Compatible.Value)
                return false;
            return true;
        }

        public Task UnloadAsync()
        {
            Flux.Navigator.Navigate(UnloadFilamentOperation.Value);
            return Task.CompletedTask;
        }
        public async Task LoadPurgeAsync()
        {
            if (State.Loaded && State.Locked)
            {
                var recovery = Flux.StatusProvider.PrintingEvaluation.Recovery;
                var filament_settings = GCodeFilamentOperation.Create(this, recovery);
                if (!filament_settings.HasValue)
                    return;

                await Flux.ConnectionProvider.PurgeAsync(filament_settings.Value);
            }
            else
            {
                Flux.Navigator.Navigate(LoadFilamentOperation.Value);
            }
        }
    }
}
