using DynamicData;
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

            var before_gear_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.WIRE_PRESENCE_BEFORE_GEAR, Feeder.Position);
            _WirePresence1 = Flux.ConnectionProvider.ObserveVariable(
                m => m.WIRE_PRESENCE_BEFORE_GEAR,
                before_gear_key.ValueOr(() => ""))
                .ToProperty(this, v => v.WirePresence1)
                .DisposeWith(Disposables);

            var after_gear_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.WIRE_PRESENCE_AFTER_GEAR, Feeder.Position);
            _WirePresence2 = Flux.ConnectionProvider.ObserveVariable(
                m => m.WIRE_PRESENCE_AFTER_GEAR,
                after_gear_key.ValueOr(() => ""))
                .ToProperty(this, v => v.WirePresence2)
                .DisposeWith(Disposables);

            var on_head_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.WIRE_PRESENCE_ON_HEAD, Feeder.Position);
            _WirePresence3 = Flux.ConnectionProvider.ObserveVariable(
                m => m.WIRE_PRESENCE_ON_HEAD,
                on_head_key.ValueOr(() => ""))
                .ToProperty(this, v => v.WirePresence3)
                .DisposeWith(Disposables);

            _State = FindMaterialState()
                .ToProperty(this, v => v.State)
                .DisposeWith(Disposables); 
        }

        public override void Initialize()
        {
            base.Initialize();

            var tool_material = Feeder.WhenAnyValue(f => f.SelectedToolMaterial);

            _MaterialBrush = Observable.CombineLatest(
                this.WhenAnyValue(v => v.State),
                tool_material.ConvertMany(tm => tm.WhenAnyValue(v => v.State)),
                (m, tm) =>
                {
                    if (!tm.HasValue || !tm.Value.KnownNozzle)
                        return FluxColors.Empty;
                    if (tm.Value.KnownMaterial && !tm.Value.Compatible)
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
                })
                .ToProperty(this, v => v.MaterialBrush)
                .DisposeWith(Disposables);

            var material = Observable.CombineLatest(
                this.WhenAnyValue(v => v.State),
                Feeder.ToolNozzle.WhenAnyValue(v => v.State),
                tool_material.ConvertMany(tm => tm.WhenAnyValue(v => v.State)),
                Flux.StatusProvider.CanSafeCycle,
                (ms, tn, tm, si) => new
                {
                    can_purge = CanPurgeMaterial(ms, tn, tm, si),
                    can_load = CanLoadMaterial(ms, tn, tm, si),
                    can_unload = CanUnloadMaterial(ms, tn, tm, si),
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

            var compatible = Feeder.WhenAnyValue(f => f.SelectedToolMaterial)
                .ConvertMany(tm => tm.WhenAnyValue(tm => tm.Document))
                .Select(d => d.HasValue);

            return Observable.CombineLatest(inserted, known, locked, loaded,
                 (inserted, known, locked, loaded) => new MaterialState(inserted, known, locked, loaded));
        }
        private bool CanUnloadMaterial(MaterialState material, ToolNozzleState tool_nozzle, Optional<ToolMaterialState> tool_material, bool safe_idle)
        {
            if (!tool_nozzle.IsLoaded())
                return false;
            if (material.IsNotLoaded())
                return false;
            if (!tool_material.HasValue)
                return false;
            if (!tool_material.Value.KnownNozzle)
                return false;
            if (!tool_material.Value.KnownMaterial)
                return safe_idle;
            return safe_idle && tool_material.Value.Compatible;
        }
        private bool CanLoadMaterial(MaterialState material, ToolNozzleState tool_nozzle, Optional<ToolMaterialState> tool_material, bool safe_idle)
        {
            if (!tool_nozzle.IsLoaded())
                return false;
            if (material.IsLoaded())
                return false;
            if (!tool_material.HasValue)
                return false;
            if (!tool_material.Value.KnownNozzle)
                return false;
            if (!tool_material.Value.KnownMaterial)
                return safe_idle;
            return safe_idle && tool_material.Value.Compatible;
        }
        private bool CanPurgeMaterial(MaterialState material, ToolNozzleState tool_nozzle, Optional<ToolMaterialState> tool_material, bool safe_idle)
        {
            if (!tool_nozzle.IsLoaded())
                return false;
            if (!material.IsLoaded())
                return false;
            if (!tool_material.HasValue)
                return false;
            if (!tool_material.Value.KnownNozzle)
                return false;
            if (!tool_material.Value.KnownMaterial)
                return false;
            return safe_idle && tool_material.Value.Compatible;
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
                var tool_material = Feeder.SelectedToolMaterial;
                if (!tool_material.HasValue)
                {
                    Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_INVALID_BREAK_TEMP, default);
                    return;
                }

                var extrusion_temp = tool_material.Value.ExtrusionTemp;
                if (!extrusion_temp.HasValue)
                {
                    Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_INVALID_BREAK_TEMP, default);
                    return;
                }
                await Flux.ConnectionProvider.PurgeAsync(Feeder.Position, extrusion_temp.Value);
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
