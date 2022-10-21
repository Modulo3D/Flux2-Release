using DynamicData;
using DynamicData.Binding;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class FeedersViewModel : FluxRoutableNavBarViewModel<FeedersViewModel>, IFluxFeedersViewModel
    {
        [RemoteContent(true)]
        public IObservableCache<IFluxFeederViewModel, ushort> Feeders { get; }
        public IObservableCache<IFluxMaterialViewModel, ushort> ToolMaterials { get; }
        public IObservableCache<IFluxToolNozzleViewModel, ushort> ToolNozzles { get; }

        public ObservableAsPropertyHelper<short> _SelectedExtruder;
        public short SelectedExtruder => _SelectedExtruder.Value;

        public ObservableAsPropertyHelper<Optional<IFluxFeederViewModel>> _SelectedFeeder;
        public Optional<IFluxFeederViewModel> SelectedFeeder => _SelectedFeeder.Value;

        public ObservableAsPropertyHelper<bool> _HasInvalidStates;
        public bool HasInvalidStates => _HasInvalidStates.Value;

        public IObservableCache<Optional<Material>, ushort> Materials { get; }
        public IObservableCache<Optional<Nozzle>, ushort> Nozzles { get; }
        public IObservableCache<Optional<Tool>, ushort> Tools { get; }

        public OdometerManager OdometerManager { get; }
        IOdometerManager IFluxFeedersViewModel.OdometerManager => OdometerManager;

        public NFCStorage<FluxUserSettings, NFCMaterial> NFCMaterials { get; }
        public NFCStorage<FluxUserSettings, NFCToolNozzle> NFCToolNozzles { get; }

        public FeedersViewModel(FluxViewModel flux) : base(flux)
        {
            OdometerManager = new OdometerManager(flux);

            Feeders = Flux.SettingsProvider
                .WhenAnyValue(v => v.ExtrudersCount)
                .Select(CreateFeeders)
                .ToObservableChangeSet(f => f.Position)
                .DisposeMany()
                .AsObservableCache()
                .DisposeWith(Disposables);

            ToolNozzles = Feeders.Connect()
                .TransformMany(CreateToolNozzles, tn => tn.Position)
                .DisposeMany()
                .AsObservableCache()
                .DisposeWith(Disposables);

            ToolMaterials = Feeders.Connect()
                .TransformMany(CreateToolMaterials, tn => tn.Position)
                .DisposeMany()
                .AsObservableCache()
                .DisposeWith(Disposables);

            var user_settings = Flux.SettingsProvider.CoreSettings;
            var lock_guid = user_settings.Local.PrinterGuid;

            NFCMaterials = new NFCStorage<FluxUserSettings, NFCMaterial>(
                this, Flux.SettingsProvider.UserSettings, lock_guid,
                Directories.NFCBackup, s => s.Materials);

            NFCToolNozzles = new NFCStorage<FluxUserSettings, NFCToolNozzle>(
                this, Flux.SettingsProvider.UserSettings, lock_guid,
                Directories.NFCBackup, s => s.ToolNozzles);

            _HasInvalidStates = Feeders.Connect()
                .TrueForAny(f => f.HasInvalidStateChanged, i => i)
                .ToProperty(this, f => f.HasInvalidStates)
                .DisposeWith(Disposables);

            // TODO
            Materials = Feeders.Connect()
                .AutoRefresh(f => f.SelectedMaterial)
                .Transform(f => f.SelectedMaterial, true)
                .AutoRefresh(m => m.Document)
                .Transform(f => f.Convert(f => f.Document), true)
                .AsObservableCache()
                .DisposeWith(Disposables);

            Tools = Feeders.Connect()
                .AutoRefresh(f => f.ToolNozzle.Document)
                .Transform(f => f.ToolNozzle.Document, true)
                .Transform(tn => tn.tool)
                .AsObservableCache()
                .DisposeWith(Disposables);

            Nozzles = Feeders.Connect()
              .AutoRefresh(f => f.ToolNozzle.Document)
              .Transform(f => f.ToolNozzle.Document, true)
              .Transform(tn => tn.nozzle)
              .AsObservableCache()
              .DisposeWith(Disposables);

            var selected_extruder = Flux.ConnectionProvider
                .ObserveVariable(m => m.TOOL_ON_TRAILER)
                .Convert(c => c.QueryWhenChanged(q => (short)q.Items.IndexOf(true)))
                .ToOptionalObservable();

            _SelectedExtruder = selected_extruder
                .ObservableOr(() => (short)-1)
                .ToProperty(this, v => v.SelectedExtruder)
                .DisposeWith(Disposables);

            _SelectedFeeder = selected_extruder
                .ConvertToObservable(FindSelectedFeeder)
                .ObservableOrDefault()
                .ToProperty(this, v => v.SelectedFeeder)
                .DisposeWith(Disposables);
        }

        private IObservable<Optional<IFluxFeederViewModel>> FindSelectedFeeder(short selected_extruder)
        {
            if (selected_extruder < 0)
                return Observable.Return(Optional<IFluxFeederViewModel>.None);

            return Feeders.Connect().WatchValue((ushort)selected_extruder)
                .Select(f => Optional<IFluxFeederViewModel>.Create(f));
        }

        private IEnumerable<IFluxFeederViewModel> CreateFeeders(Optional<(ushort machine_extruders, ushort mixing_extruders)> extruders)
        {
            if (!extruders.HasValue)
                yield break;
            for (ushort position = 0; position < extruders.Value.machine_extruders; position++)
                yield return new FeederViewModel(this, position, extruders.Value.mixing_extruders);
        }
        private IEnumerable<IFluxToolNozzleViewModel> CreateToolNozzles(IFluxFeederViewModel feeder)
        {
            yield return new ToolNozzleViewModel(this, (FeederViewModel)feeder);
        }
        private IEnumerable<IFluxMaterialViewModel> CreateToolMaterials(IFluxFeederViewModel feeder)
        {
            for (ushort position = 0; position < feeder.MixingCount; position++)
            { 
                var material = new MaterialViewModel(this, (FeederViewModel)feeder, (ushort)(position + (feeder.Position * feeder.MixingCount)));
                material.Initialize();
                yield return material;
            }
        }

        async Task<NFCReading<NFCMaterial>> INFCStorageProvider<NFCMaterial>.CreateTagAsync(ushort position, Optional<NFCMaterial> last_tag, CardId virtual_card_id)
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
                .OrderBy(d => d[d => d.MaterialType, ""])
                .ThenBy(d => d.Name)
                .AsObservableChangeSet(m => m.Id)
                .AsObservableCache();

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
                $"MATERIALE N.{position + 1}", new IDialogOption[] { material_option, max_weight_option, cur_weight_option });

            if (result != ContentDialogResult.Primary)
                return default;

            var material = material_option.Value;
            if (!material.HasValue)
                return default;

            var max_weight = max_weight_option.Value;
            var cur_weight = cur_weight_option.Value;

            var tag = new NFCMaterial(material.Value, max_weight, cur_weight, last_printer_guid, last_loaded);
            return new NFCReading<NFCMaterial>(tag, virtual_card_id);
        }

        async Task<ValueResult<Tool>> FindNewToolAsync(ushort position, Optional<NFCToolNozzle> last_tag)
        {
            var database = Flux.DatabaseProvider.Database;
            if (!database.HasValue)
                return default;

            var printer = Flux.SettingsProvider.Printer;
            if (!printer.HasValue)
                return default;

            var tool_documents = CompositeQuery.Create(database.Value,
               db => _ => db.Find(printer.Value, Tool.SchemaInstance), db => db.GetTarget)
               .Execute()
               .Convert<Tool>();

            var tools = tool_documents.Documents
                .OrderBy(d => d.Name)
                .AsObservableChangeSet(t => t.Id)
                .AsObservableCache();

            var last_tool_guid = last_tag.ConvertOr(t => t.NozzleGuid, () => Guid.Empty);

            var tool_option = ComboOption.Create("tool", "Utensile:", tools);
            var tool_result = await Flux.ShowSelectionAsync(
                $"UTENSILE N.{position + 1}", new[] { tool_option });

            if (tool_result != ContentDialogResult.Primary)
                return default;

            var tool = tool_option.Value;
            if (!tool.HasValue)
                return new ValueResult<Tool>(default);

            return tool.Value;
        }
        async Task<(ValueResult<Nozzle> nozzle, double max_weight, double cur_weight)> FindNewNozzleAsync(ushort position, Tool tool, Optional<NFCToolNozzle> last_tag)
        {
            var database = Flux.DatabaseProvider.Database;
            if (!database.HasValue)
                return default;

            var nozzle_documents = CompositeQuery.Create(database.Value,
               db => _ => db.Find(tool, Nozzle.SchemaInstance), db => db.GetTarget)
               .Execute()
               .Convert<Nozzle>();

            if (!nozzle_documents.HasDocuments)
                return default;

            var nozzles = nozzle_documents.Documents
                .OrderBy(d => d.Name)
                .AsObservableChangeSet(n => n.Id)
                .AsObservableCache();

            var nozzle_weights = new[] { 20000.0, 10000.0 }
                .AsObservableChangeSet(w => (int)w)
                .AsObservableCache();

            var last_nozzle_guid = last_tag.ConvertOr(t => t.NozzleGuid, () => Guid.Empty);
            var last_cur_weight = last_tag.Convert(t => t.CurWeightG).ValueOr(() => 10000.0);
            var last_max_weight = last_tag.Convert(t => t.MaxWeightG).ValueOr(() => 10000.0);

            var nozzle_option = ComboOption.Create("nozzle", "UGELLO:", nozzles);
            var cur_weight_option = new NumericOption("curWeight", "PESO CORRENTE:", last_cur_weight, 1000.0, converter: typeof(WeightConverter));
            var max_weight_option = new NumericOption("maxWeight", "PESO TOTALE:", last_max_weight, 1000.0, value_changed:
            v =>
            {
                cur_weight_option.Min = 0f;
                cur_weight_option.Max = v;
                cur_weight_option.Value = v;
            }, converter: typeof(WeightConverter));

            var nozzle_result = await Flux.ShowSelectionAsync(
                $"UGELLO N.{position + 1}", new IDialogOption[] { nozzle_option, max_weight_option, cur_weight_option });

            if (nozzle_result != ContentDialogResult.Primary)
                return (default, default, default);

            var nozzle = nozzle_option.Value;
            if (!nozzle.HasValue)
                return (new ValueResult<Nozzle>(default), default, default);

            var max_weight = max_weight_option.Value;
            var cur_weight = cur_weight_option.Value;

            return (nozzle.Value, max_weight, cur_weight);
        }
        async Task<NFCReading<NFCToolNozzle>> INFCStorageProvider<NFCToolNozzle>.CreateTagAsync(ushort position, Optional<NFCToolNozzle> last_tag, CardId virtual_card_id)
        {
            var tool = await FindNewToolAsync(position, last_tag);
            if (!tool.Result)
                return default;

            if (!tool.HasValue)
                return default;

            var nozzle = await FindNewNozzleAsync(position, tool.Value, last_tag);
            if (!nozzle.nozzle.Result)
                return default;

            var last_loaded = last_tag.Convert(t => t.Loaded);
            var last_break_temp = last_tag.Convert(t => t.LastBreakTemperature);
            var last_printer_guid = last_tag.ConvertOr(t => t.PrinterGuid, () => Guid.Empty);

            var tag = new NFCToolNozzle(tool.Value, nozzle.nozzle, nozzle.max_weight, nozzle.cur_weight, last_printer_guid, last_loaded, last_break_temp);
            return new NFCReading<NFCToolNozzle>(tag, virtual_card_id);
        }

        IObservable<IChangeSet<NFCSlot<NFCMaterial>, ushort>> INFCStorageProvider<NFCMaterial>.GetNFCSlots() => ToolMaterials.Connect().Transform(m => m.NFCSlot);
        IObservable<IChangeSet<NFCSlot<NFCToolNozzle>, ushort>> INFCStorageProvider<NFCToolNozzle>.GetNFCSlots() => ToolNozzles.Connect().Transform(m => m.NFCSlot);
    }
}
