﻿using DynamicData;
using DynamicData.Kernel;
using Microsoft.Extensions.Logging;
using Modulo3DStandard;
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
        public ReactiveCommand<Unit, Unit> UnloadMaterialCommand { get; private set; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> LoadPurgeMaterialCommand { get; private set; }

        private ObservableAsPropertyHelper<string> _MaterialBrush;
        [RemoteOutput(true)]
        public string MaterialBrush => _MaterialBrush.Value;

        private ObservableAsPropertyHelper<Optional<string>> _DocumentLabel;
        [RemoteOutput(true)]
        public override Optional<string> DocumentLabel => _DocumentLabel.Value;

        private ObservableAsPropertyHelper<Optional<bool>> _WirePresenceBeforeGear;
        [RemoteOutput(true)]
        public Optional<bool> FilamentPresenceBeforeGear => _WirePresenceBeforeGear.Value;

        private ObservableAsPropertyHelper<Optional<bool>> _WirePresenceAfterGear;
        [RemoteOutput(true)]
        public Optional<bool> FilamentPresenceAfterGear => _WirePresenceAfterGear.Value;

        private ObservableAsPropertyHelper<Optional<bool>> _WirePresenceOnHead;
        [RemoteOutput(true)]
        public Optional<bool> FilamentPresenceOnHead => _WirePresenceOnHead.Value;

        private ObservableAsPropertyHelper<bool> _MaterialLoaded;
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
                    _ToolMaterial = new ToolMaterialViewModel(Feeder.ToolNozzle, this);
                return _ToolMaterial;
            }
        }

        public ObservableAsPropertyHelper<Optional<FLUX_Humidity>> _Humidity;
        [RemoteOutput(true, typeof(HumidityConverter))]
        public Optional<FLUX_Humidity> Humidity => _Humidity.Value;

        private ObservableAsPropertyHelper<Optional<ExtrusionKey>> _ExtrusionKey;
        public Optional<ExtrusionKey> ExtrusionKey => _ExtrusionKey.Value;

        public MaterialViewModel(FeederViewModel feeder, ushort position) : base(feeder, position, s => s.Materials, (db, m) =>
        {
            return m.GetDocument<Material>(db, m => m.MaterialGuid);
        }, t => t.MaterialGuid)
        {
            _ExtrusionKey = Observable.CombineLatest(
                  Feeder.ToolNozzle.WhenAnyValue(t => t.Nfc),
                  this.WhenAnyValue(m => m.Nfc),
                  (tool_nozzle, material) => Modulo3DStandard.ExtrusionKey.Create(tool_nozzle, material))
                  .ToProperty(this, v => v.ExtrusionKey)
                  .DisposeWith(Disposables);

            var before_gear_key = Flux.ConnectionProvider.GetArrayUnit(m => m.FILAMENT_BEFORE_GEAR, Position);
            _WirePresenceBeforeGear = Flux.ConnectionProvider.ObserveVariable(
                m => m.FILAMENT_BEFORE_GEAR,
                before_gear_key)
                .ObservableOrDefault()
                .ToProperty(this, v => v.FilamentPresenceBeforeGear)
                .DisposeWith(Disposables);

            var after_gear_key = Flux.ConnectionProvider.GetArrayUnit(m => m.FILAMENT_AFTER_GEAR, Position);
            _WirePresenceAfterGear = Flux.ConnectionProvider.ObserveVariable(
                m => m.FILAMENT_AFTER_GEAR,
                after_gear_key)
                .ObservableOrDefault()
                .ToProperty(this, v => v.FilamentPresenceAfterGear)
                .DisposeWith(Disposables);

            var on_head_key = Flux.ConnectionProvider.GetArrayUnit(m => m.FILAMENT_ON_HEAD, Feeder.Position);
            _WirePresenceOnHead = Flux.ConnectionProvider.ObserveVariable(
                m => m.FILAMENT_ON_HEAD,
                on_head_key)
                .ObservableOrDefault()
                .ToProperty(this, v => v.FilamentPresenceOnHead)
                .DisposeWith(Disposables);

            _DocumentLabel = this.WhenAnyValue(v => v.Document)
                .Convert(d => d.Name)
                .ToProperty(this, v => v.DocumentLabel)
                .DisposeWith(Disposables);

            _MaterialLoaded = this.WhenAnyValue(v => v.Nfc)
                .Select(nfc => nfc.Tag)
                .Convert(tag => tag.Loaded)
                .Convert(l => l == Feeder.Position)
                .ValueOr(() => false)
                .ToProperty(this, v => v.MaterialLoaded)
                .DisposeWith(Disposables);

            var humidity_unit = Flux.ConnectionProvider.GetArrayUnit(c => c.FILAMENT_HUMIDITY, Position);
            _Humidity = Flux.ConnectionProvider.ObserveVariable(c => c.FILAMENT_HUMIDITY, humidity_unit)
                .ObservableOrDefault()
                .ToProperty(this, v => v.Humidity);

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
        
        public NFCReading<NFCMaterial> SetMaterialLoaded(Optional<bool> material_loaded)
        {
            if (!material_loaded.HasValue)
                return default;
            if (!Nfc.Tag.HasValue)
                return default;

            if (material_loaded.Value)
                return StoreTag(t => t.SetLoaded(Feeder.Position));
            else
                return StoreTag(t => t.SetLoaded(default));
        }
        public override void Initialize()
        {
            base.Initialize();

            _State = FindMaterialState()
                .ToProperty(this, v => v.State)
                .DisposeWith(Disposables);

            var tool_nozzle = Feeder.ToolNozzle
                .WhenAnyValue(t => t.State);

            var is_selected = Feeder.WhenAnyValue(f => f.SelectedMaterial)
                .Convert(m => m == this)
                .ValueOr(() => false);

            _MaterialBrush = Observable.CombineLatest(
                is_selected,
                tool_nozzle,
                this.WhenAnyValue(v => v.State),
                ToolMaterial.WhenAnyValue(v => v.State),
                (s, tn, m, tm) =>
                {
                    if (m.IsNotLoaded())
                        return FluxColors.Empty;
                    if(!tn.Inserted)
                        return FluxColors.Empty;
                    if (!tn.IsLoaded())
                        return FluxColors.Inactive;
                    if (!m.Known)
                        return FluxColors.Error;
                    if (tm.Compatible.HasValue && !tm.Compatible.Value)
                        return FluxColors.Error;
                    if (!m.Inserted)
                        return FluxColors.Empty;
                    if (!m.Locked)
                        return FluxColors.Warning;
                    if (!s)
                        return FluxColors.Active;
                    return FluxColors.Selected;
                })
                .ToProperty(this, v => v.MaterialBrush)
                .DisposeWith(Disposables);

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

            UnloadMaterialCommand = ReactiveCommand.CreateFromTask(UnloadAsync, material.Select(m => m.can_unload))
                .DisposeWith(Disposables);
            LoadPurgeMaterialCommand = ReactiveCommand.CreateFromTask(LoadPurgeAsync, material.Select(m => m.can_load || m.can_purge))
                .DisposeWith(Disposables);

            Observable.CombineLatest(
                this.WhenAnyValue(m => m.Nfc),
                this.WhenAnyValue(v => v.FilamentPresenceBeforeGear),
                (nfc, material_inserted) => (nfc, material_inserted))
                .SubscribeOn(RxApp.MainThreadScheduler)
                .Subscribe(t =>
                {
                    if (!t.material_inserted.HasValue)
                        return;
                    if (t.material_inserted.Value)
                        return;
                    
                    StoreTag(t => t.SetInserted(default));
                    StoreTag(t => t.SetLoaded(default));
                });

            Observable.CombineLatest(
                this.WhenAnyValue(m => m.Nfc),
                this.WhenAnyValue(v => v.FilamentPresenceAfterGear),
                (nfc, material_preloaded) => (nfc, material_preloaded))
                .SubscribeOn(RxApp.MainThreadScheduler)
                .Subscribe(t =>
                {
                    if (!t.material_preloaded.HasValue)
                        return;
                    if (!t.material_preloaded.Value)
                        return;
                    StoreTag(t => t.SetInserted(Feeder.Position));
                });


            var other_filament_after_gear = Feeder.Materials.Connect()
                .Filter(m => m.Position != Position)
                .TrueForAny(m => m.WhenAnyValue(m => m.FilamentPresenceAfterGear), f => f.ValueOr(() => false))
                .StartWith(false);

            Observable.CombineLatest(
                other_filament_after_gear,
                this.WhenAnyValue(m => m.Nfc),
                this.WhenAnyValue(v => v.FilamentPresenceAfterGear),
                this.WhenAnyValue(v => v.FilamentPresenceOnHead),
                (other_loaded, nfc, material_preloaded, material_loaded) => (other_loaded, nfc, material_preloaded, material_loaded))
                .SubscribeOn(RxApp.MainThreadScheduler)
                .Subscribe(t =>
                {
                    if (t.other_loaded)
                        return;
                    if (!t.material_preloaded.HasValue)
                        return;
                    if (!t.material_loaded.HasValue)
                        return;
                    if (!t.material_preloaded.Value)
                        return;
                    if (!t.material_loaded.Value)
                        return;
                    StoreTag(t => t.SetLoaded(Feeder.Position));
                })
                .DisposeWith(Disposables);
        }
        private IObservable<MaterialState> FindMaterialState()
        {
            var inserted = this.WhenAnyValue(m => m.Nfc)
                .Select(nfc => nfc.Tag.Convert(t => t.Inserted))
                .Convert(i => i == Feeder.Position)
                .ValueOr(() => false);

            var known = this.WhenAnyValue(m => m.Document)
                .Select(document => document.HasValue)
                .DistinctUntilChanged();

            var loaded = this.WhenAnyValue(m => m.Nfc)
                .Select(nfc => nfc.Tag.Convert(t => t.Loaded))
                .ConvertOr(l => l == Feeder.Position, () => false);

            var core_settings = Feeder.Flux.SettingsProvider.CoreSettings.Local;
            var printer_guid = core_settings.WhenAnyValue(s => s.PrinterGuid);

            var tag_printer_guid = this.WhenAnyValue(m => m.Nfc)
                .Select(nfc => nfc.Tag
                .Convert(t => t.PrinterGuid));

            var locked = Observable.CombineLatest(
                printer_guid,
                tag_printer_guid,
                (p_guid, t_guid) => t_guid.ConvertOr(t => t == p_guid, () => false));

            return Observable.CombineLatest(inserted, known, locked, loaded,
                 (inserted, known, locked, loaded) => new MaterialState(inserted, known, locked, loaded));
        }
        private bool CanLoadMaterial(bool can_cycle, ToolNozzleState tool_nozzle, MaterialState material,  Optional<ToolMaterialState> tool_material)
        {
            if (!can_cycle)
                return false;
            if (!tool_nozzle.IsLoaded())
                return false;
            if (material.IsLoaded())
                return false;
            if (!tool_material.HasValue)
                return false;
            if (tool_material.Value.Compatible.HasValue && !tool_material.Value.Compatible.Value)
                return false;
            return true;
        }
        private bool CanPurgeMaterial(bool can_cycle, ToolNozzleState tool_nozzle, MaterialState material, Optional<ToolMaterialState> tool_material)
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
                var recovery = Flux.StatusProvider.PrintingEvaluation.HasRecovery;
                var filament_settings = GCodeFilamentOperation.Create(this, !recovery, recovery);
                if (!filament_settings.HasValue)
                    return;

                await Flux.ConnectionProvider.PurgeAsync(filament_settings.Value);
            }
            else
            {
                Flux.Navigator.Navigate(LoadFilamentOperation.Value);
            }
        }
        public override async Task<ValueResult<NFCMaterial>> CreateTagAsync(Optional<NFCReading<NFCMaterial>> reading)
        {
            var database = Flux.DatabaseProvider.Database;
            if (!database.HasValue)
                return default;

            var printer = Flux.SettingsProvider.Printer;
            if (!printer.HasValue)
                return default;

            var documents = CompositeQuery.Create(database.Value,
                    db => _ => db.Find(printer.Value, Tool.SchemaInstance), db => db.GetTarget,
                    db => t => db.Find(t, Nozzle.SchemaInstance), db => db.GetTarget,
                    db => n => db.Find(n, Modulo3DStandard.ToolMaterial.SchemaInstance), db => db.GetTarget,
                    db => tm => db.Find(Material.SchemaInstance, tm), db => db.GetSource)
                    .Execute()
                    .Convert<Material>();

            var materials = documents.Documents
                .Distinct()
                .OrderBy(d => d[d => d.MaterialType, ""])
                .ThenBy(d => d.Name)
                .AsObservableChangeSet(m => m.Id)
                .AsObservableCache();

            var last_tag = reading.Convert(r => r.Tag);

            var last_loaded = last_tag.Convert(t => t.Loaded);
            var last_cur_weight = last_tag.Convert(t => t.CurWeightG).ValueOr(() => 1000.0);
            var last_max_weight = last_tag.Convert(t => t.MaxWeightG).ValueOr(() => 1000.0);
            var last_printer_guid = last_tag.ConvertOr(t => t.PrinterGuid, () => Guid.Empty);
            var last_material_guid = last_tag.ConvertOr(t => t.MaterialGuid, () => Guid.Empty);

            var material_option = ComboOption.Create("material", "MATERIALE:", materials);
            var cur_weight_option = new NumericOption("curWeight", "PESO CORRENTE:", last_cur_weight, 50.0, converter: typeof(WeightConverter));
            var max_weight_option = new NumericOption("maxWeight", "PESO TOTALE:", last_max_weight, 50.0, value_changed: v =>
            {
                cur_weight_option.Min = 0f;
                cur_weight_option.Max = v;
                cur_weight_option.Value = v;
            }, converter: typeof(WeightConverter));

            var result = await Flux.ShowSelectionAsync(
                $"MATERIALE N.{Feeder.Position + 1}", new IDialogOption[] { material_option, max_weight_option, cur_weight_option });

            if (result != ContentDialogResult.Primary)
                return default;

            var material = material_option.Value;
            if (!material.HasValue)
                return default;

            var max_weight = max_weight_option.Value;
            var cur_weight = cur_weight_option.Value;

            return new NFCMaterial(material.Value, max_weight, cur_weight, last_printer_guid, last_loaded);
        }
    }
}
