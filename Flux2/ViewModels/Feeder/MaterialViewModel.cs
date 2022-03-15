using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class MaterialViewModel : TagViewModel<NFCMaterial, Optional<Material>, MaterialState>, IFluxMaterialViewModel
    {
        public override int VirtualTagId => 1;

        private ObservableAsPropertyHelper<MaterialState> _State;
        public override MaterialState State => _State.Value;

        private ObservableAsPropertyHelper<Optional<bool>> _WirePresence1;
        public Optional<bool> WirePresence1 => _WirePresence1.Value;

        private ObservableAsPropertyHelper<Optional<bool>> _WirePresence2;
        public Optional<bool> WirePresence2 => _WirePresence2.Value;

        private ObservableAsPropertyHelper<Optional<bool>> _WirePresence3;
        public Optional<bool> WirePresence3 => _WirePresence3.Value;

        public override OdometerViewModel<NFCMaterial> Odometer { get; }

        public ReactiveCommand<Unit, Unit> UnloadCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> LoadPurgeCommand { get; private set; }

        private LoadMaterialViewModel _LoadMaterialViewModel;
        public LoadMaterialViewModel LoadMaterialViewModel
        {
            get
            {
                if (_LoadMaterialViewModel == null)
                {
                    _LoadMaterialViewModel = new LoadMaterialViewModel(Feeder);
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
                    _UnloadMaterialViewModel = new UnloadMaterialViewModel(Feeder);
                    _UnloadMaterialViewModel.InitializeRemoteView();
                }
                return _UnloadMaterialViewModel;
            }
        }

        public MaterialViewModel(FeederViewModel feeder) : base(feeder, s => s.Materials, (db, m) =>
        {
            return m.GetDocument<Material>(db, m => m.MaterialGuid);
        }, t => t.MaterialGuid)
        {
            var multiplier = Observable.Return(1.0);
            Odometer = new OdometerViewModel<NFCMaterial>(this, multiplier);

            var before_gear_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.WIRE_PRESENCE_BEFORE_GEAR, Feeder.Position);
            _WirePresence1 = Flux.ConnectionProvider.ObserveVariable(
                m => m.WIRE_PRESENCE_BEFORE_GEAR,
                before_gear_key.ValueOr(() => ""))
                .ToProperty(this, v => v.WirePresence1);

            var after_gear_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.WIRE_PRESENCE_AFTER_GEAR, Feeder.Position);
            _WirePresence2 = Flux.ConnectionProvider.ObserveVariable(
                m => m.WIRE_PRESENCE_AFTER_GEAR,
                after_gear_key.ValueOr(() => ""))
                .ToProperty(this, v => v.WirePresence2);

            var on_head_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.WIRE_PRESENCE_ON_HEAD, Feeder.Position);
            _WirePresence3 = Flux.ConnectionProvider.ObserveVariable(
                m => m.WIRE_PRESENCE_ON_HEAD,
                on_head_key.ValueOr(() => ""))
                .ToProperty(this, v => v.WirePresence3);

            _State = FindMaterialState()
                .ToProperty(this, v => v.State);
        }

        public override void Initialize()
        {
            var material = Observable.CombineLatest(
                this.WhenAnyValue(v => v.State),
                Feeder.ToolNozzle.WhenAnyValue(v => v.State),
                Feeder.ToolMaterial.WhenAnyValue(v => v.State),
                Flux.StatusProvider.CanSafeCycle,
                (ms, tn, tm, si) => new
                {
                    can_purge = CanPurgeMaterial(ms, tn, tm, si),
                    can_load = CanLoadMaterial(ms, tn, tm, si),
                    can_unload = CanUnloadMaterial(ms, tn, tm, si),
                });

            UnloadCommand = ReactiveCommand.CreateFromTask(UnloadAsync, material.Select(m => m.can_unload));
            LoadPurgeCommand = ReactiveCommand.CreateFromTask(LoadPurgeAsync, material.Select(m => m.can_load || m.can_purge));
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

            var compatible = Feeder.ToolMaterial
                .WhenAnyValue(tm => tm.Document)
                .Select(d => d.HasValue);

            return Observable.CombineLatest(inserted, known, locked, loaded,
                 (inserted, known, locked, loaded) => new MaterialState(inserted, known, locked, loaded));
        }
        private bool CanUnloadMaterial(MaterialState material, ToolNozzleState tool_nozzle, ToolMaterialState tool_material, bool safe_idle)
        {
            if (!tool_nozzle.IsLoaded())
                return false;
            if (material.IsNotLoaded())
                return false;
            if (!tool_material.KnownNozzle)
                return false;
            if (!tool_material.KnownMaterial)
                return safe_idle;
            return safe_idle && tool_material.Compatible;
        }
        private bool CanLoadMaterial(MaterialState material, ToolNozzleState tool_nozzle, ToolMaterialState tool_material, bool safe_idle)
        {
            if (!tool_nozzle.IsLoaded())
                return false;
            if (material.IsLoaded())
                return false;
            if (!tool_material.KnownNozzle)
                return false;
            if (!tool_material.KnownMaterial)
                return safe_idle;
            return safe_idle && tool_material.Compatible;
        }
        private bool CanPurgeMaterial(MaterialState material, ToolNozzleState tool_nozzle, ToolMaterialState tool_material, bool safe_idle)
        {
            if (!tool_nozzle.IsLoaded())
                return false;
            if (!material.IsLoaded())
                return false;
            if (!tool_material.KnownNozzle)
                return false;
            if (!tool_material.KnownMaterial)
                return false;
            return safe_idle && tool_material.Compatible;
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
                var extrusion_temp = Feeder.ToolMaterial.ExtrusionTemp;
                if (!extrusion_temp.HasValue)
                {
                    Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_INVALID_BREAK_TEMP, Feeder.Material.State);
                    return;
                }
                await Flux.ConnectionProvider.PurgeAsync(Feeder.Position, extrusion_temp.Value);
            }
            else
            {
                Flux.Navigator.Navigate(LoadMaterialViewModel);
            }
        }
        protected override IObservable<Optional<INFCReader>> GetReader()
        {
            return Flux.NFCProvider.GetMaterialReader(Feeder.Position);
        }
        protected override async Task<(bool result, Optional<NFCMaterial> tag)> CreateTagAsync(string card_id, bool virtual_tag)
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
                $"MATERIALE N.{Feeder.Position + 1}{(virtual_tag ? " (VIRTUALE)" : "")}, ID: {card_id}", 
                Observable.Return(true),
                material_option, max_weight_option, cur_weight_option);

            if (result != ContentDialogResult.Primary)
                return (false, default);

            var material = material_option.Value;
            if (!material.HasValue)
                return (true, default);

            var max_weight = max_weight_option.Value;
            if (!max_weight.HasValue)
                return (true, default);

            var cur_weight = cur_weight_option.Value;

            var nfc_material = new NFCMaterial(material.Value, max_weight.Value, cur_weight);
            return (true, nfc_material);
        }
    }
}
