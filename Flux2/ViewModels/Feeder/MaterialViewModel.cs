﻿using DynamicData;
using DynamicData.Kernel;
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

        private ObservableAsPropertyHelper<Optional<bool>> _WirePresence1;
        public Optional<bool> WirePresence1 => _WirePresence1.Value;

        private ObservableAsPropertyHelper<Optional<bool>> _WirePresence2;
        public Optional<bool> WirePresence2 => _WirePresence2.Value;

        private ObservableAsPropertyHelper<Optional<bool>> _WirePresence3;
        public Optional<bool> WirePresence3 => _WirePresence3.Value;

        public override OdometerViewModel<NFCMaterial> Odometer { get; }

        public ReactiveCommand<Unit, Unit> UnloadMaterialCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> LoadPurgeMaterialCommand { get; private set; }

        private LoadMaterialViewModel _LoadMaterialViewModel;
        public LoadMaterialViewModel LoadMaterialViewModel
        {
            get
            {
                if (_LoadMaterialViewModel == null)
                {
                    _LoadMaterialViewModel = new LoadMaterialViewModel(this);
                    _LoadMaterialViewModel.Initialize();
                    _LoadMaterialViewModel.InitializeRemoteView();
                }
                return _LoadMaterialViewModel;
            }
        }

        private UnloadMaterialViewModel _UnloadMaterialViewModel;
        public UnloadMaterialViewModel UnloadMaterialViewModel
        {
            get
            {
                if (_UnloadMaterialViewModel == null)
                {
                    _UnloadMaterialViewModel = new UnloadMaterialViewModel(this);
                    _UnloadMaterialViewModel.Initialize();
                    _UnloadMaterialViewModel.InitializeRemoteView();
                }
                return _UnloadMaterialViewModel;
            }
        }

        private ObservableAsPropertyHelper<string> _MaterialBrush;
        [RemoteOutput(true)]
        public string MaterialBrush => _MaterialBrush.Value;

        public MaterialViewModel(FeederViewModel feeder, ushort position) : base(feeder, position, s => s.Materials, (db, m) =>
        {
            return m.GetDocument<Material>(db, m => m.MaterialGuid);
        }, t => t.MaterialGuid, $"{typeof(MaterialViewModel).GetRemoteControlName()}??{position}")
        {
            var multiplier = Observable.Return(1.0);
            Odometer = new OdometerViewModel<NFCMaterial>(this, multiplier);

            var before_gear_key = Flux.ConnectionProvider.GetArrayUnit(m => m.FILAMENT_BEFORE_GEAR, Feeder.Position);
            _WirePresence1 = Flux.ConnectionProvider.ObserveVariable(
                m => m.FILAMENT_BEFORE_GEAR,
                before_gear_key.ConvertOr(u => u.Alias, () => ""))
                .ObservableOrDefault()
                .ToProperty(this, v => v.WirePresence1)
                .DisposeWith(Disposables);

            var after_gear_key = Flux.ConnectionProvider.GetArrayUnit(m => m.FILAMENT_AFTER_GEAR, Feeder.Position);
            _WirePresence2 = Flux.ConnectionProvider.ObserveVariable(
                m => m.FILAMENT_AFTER_GEAR,
                after_gear_key.ConvertOr(u => u.Alias, () => ""))
                .ObservableOrDefault()
                .ToProperty(this, v => v.WirePresence2)
                .DisposeWith(Disposables);

            var on_head_key = Flux.ConnectionProvider.GetArrayUnit(m => m.FILAMENT_ON_HEAD, Feeder.Position);
            _WirePresence3 = Flux.ConnectionProvider.ObserveVariable(
                m => m.FILAMENT_ON_HEAD,
                on_head_key.ConvertOr(u => u.Alias, () => ""))
                .ObservableOrDefault()
                .ToProperty(this, v => v.WirePresence3)
                .DisposeWith(Disposables);
        }

        public override void Initialize()
        {
            base.Initialize();

            _State = FindMaterialState()
                .ToProperty(this, v => v.State)
                .DisposeWith(Disposables);

            var tool_nozzle = Feeder.ToolNozzle
                .WhenAnyValue(t => t.State);

            var tool_material = Feeder.ToolMaterials.Connect()
                .WatchOptional(Position);

            var is_selected = Feeder.WhenAnyValue(f => f.SelectedMaterial)
                .Convert(m => m == this)
                .ValueOr(() => false);

            _MaterialBrush = Observable.CombineLatest(
                is_selected,
                tool_nozzle,
                this.WhenAnyValue(v => v.State),
                tool_material.ConvertMany(tm => tm.WhenAnyValue(v => v.State)),
                (s, tn, m, tm) =>
                {
                    if (!tn.IsLoaded())
                        return FluxColors.Empty;
                    if (m.IsNotLoaded())
                        return FluxColors.Empty;
                    if (!m.Known)
                        return FluxColors.Error;
                    if (!tm.HasValue)
                        return FluxColors.Error;
                    if (tm.Value.Compatible.HasValue && !tm.Value.Compatible.Value)
                        return FluxColors.Error;
                    if (!m.Inserted)
                        return FluxColors.Empty;
                    if (!m.Loaded)
                        return FluxColors.Inactive;
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
                tool_material.ConvertMany(tm => tm.WhenAnyValue(v => v.State)),
                Flux.StatusProvider.CanSafeCycle,
                (tool_nozzle, material, tool_material, idle) => new
                {
                    can_purge = idle && CanPurgeMaterial(tool_nozzle, material, tool_material),
                    can_load = idle && CanLoadMaterial(tool_nozzle, material, tool_material),
                    can_unload = idle && CanUnloadMaterial(tool_nozzle, material, tool_material),
                });

            UnloadMaterialCommand = ReactiveCommand.CreateFromTask(UnloadAsync, material.Select(m => m.can_unload))
                .DisposeWith(Disposables);
            LoadPurgeMaterialCommand = ReactiveCommand.CreateFromTask(LoadPurgeAsync, material.Select(m => m.can_load || m.can_purge))
                .DisposeWith(Disposables);

            AddCommand("unloadMaterial", UnloadMaterialCommand);
            AddCommand("loadPurgeMaterial", LoadPurgeMaterialCommand);
        }
        private IObservable<MaterialState> FindMaterialState()
        {
            var inserted = this.WhenAnyValue(m => m.Nfc)
                .Select(nfc => nfc.CardId.HasValue && nfc.Tag.HasValue);

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

            var selected = Feeder.SelectedMaterial
                .ConvertOr(m => m.Position == Position, () => false);

            var compatible = Feeder.ToolMaterials.Connect()
                .WatchOptional(Position)
                .ConvertMany(tm => tm.WhenAnyValue(tm => tm.Document))
                .Select(d => d.HasValue);

            return Observable.CombineLatest(inserted, known, locked, loaded,
                 (inserted, known, locked, loaded) => new MaterialState(inserted, known, selected, locked, loaded));
        }
        private bool CanUnloadMaterial(ToolNozzleState tool_nozzle, MaterialState material,  Optional<ToolMaterialState> tool_material)
        {
            if (!tool_nozzle.IsLoaded())
                return false;
            if (material.IsNotLoaded())
                return false;
            if (!tool_material.HasValue)
                return false;
            if (tool_material.Value.Compatible.HasValue && !tool_material.Value.Compatible.Value)
                return false;
            return true;
        }
        private bool CanLoadMaterial(ToolNozzleState tool_nozzle, MaterialState material,  Optional<ToolMaterialState> tool_material)
        {
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
        private bool CanPurgeMaterial(ToolNozzleState tool_nozzle, MaterialState material, Optional<ToolMaterialState> tool_material)
        {
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
            Flux.Navigator.Navigate(UnloadMaterialViewModel);
            return Task.CompletedTask;
        }
        public async Task LoadPurgeAsync()
        {
            if (State.Loaded && State.Locked)
            {
                var tool_material = Feeder.ToolMaterials.Lookup(Position);
                if (!tool_material.HasValue)
                    return;

                var extrusion_temp = tool_material.Value.ExtrusionTemp;
                if (!extrusion_temp.HasValue)
                    return;

                var break_temp = tool_material.Value.BreakTemp;
                if (!break_temp.HasValue)
                    return;

                var filament_settings = new FilamentOperationSettings()
                {
                    Mixing = Position,
                    Extruder = Feeder.Position,
                    BreakTemperature = break_temp.Value,
                    ExtrusionTemperature = extrusion_temp.Value,
                    FilamentOnHead = Flux.ConnectionProvider.GetArrayUnit(c => c.FILAMENT_ON_HEAD, Feeder.Position),
                    FilamentAfterGear = Flux.ConnectionProvider.GetArrayUnit(c => c.FILAMENT_AFTER_GEAR, Position),
                    FilamentBeforeGear = Flux.ConnectionProvider.GetArrayUnit(c => c.FILAMENT_BEFORE_GEAR, Position),
                };

                await Flux.ConnectionProvider.PurgeAsync(filament_settings);
            }
            else
            {
                Flux.Navigator.Navigate(LoadMaterialViewModel);
            }
        }
        public override async Task<Optional<NFCMaterial>> CreateTagAsync()
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
                    db => n => db.Find(n, ToolMaterial.SchemaInstance), db => db.GetTarget,
                    db => tm => db.Find(Material.SchemaInstance, tm), db => db.GetSource)
                    .Execute()
                    .Convert<Material>();

            var materials = documents.Documents
                .Distinct()
                .OrderBy(d => d.MaterialType.ValueOr(() => ""))
                .ThenBy(d => d.Name)
                .AsObservableChangeSet(m => m.Id)
                .AsObservableCache();

            var material_weights = new[] { 2000.0, 1000.0, 750.0 }
                .AsObservableChangeSet(w => (int)w)
                .AsObservableCache();

            var material_option = ComboOption.Create("material", "MATERIALE:", materials);
            var cur_weight_option = new NumericOption("curWeight", "PESO CORRENTE:", 1000.0, 50.0, converter: typeof(WeightConverter));
            var max_weight_option = ComboOption.Create("maxWeight", "PESO TOTALE:", material_weights, selection_changed:
            v =>
            {
                cur_weight_option.Min = 0f;
                cur_weight_option.Max = v.ValueOr(() => 0);
                cur_weight_option.Value = v.ValueOr(() => 0);
            }, converter: typeof(WeightConverter));

            var result = await Flux.ShowSelectionAsync(
                $"MATERIALE N.{Feeder.Position + 1}", 
                Observable.Return(true),
                material_option, max_weight_option, cur_weight_option);

            if (result != ContentDialogResult.Primary)
                return default;

            var material = material_option.Value;
            if (!material.HasValue)
                return default;

            var max_weight = max_weight_option.Value;
            if (!max_weight.HasValue)
                return default;

            var cur_weight = cur_weight_option.Value;

            return new NFCMaterial(material.Value, max_weight.Value, cur_weight);
        }
    }
}
