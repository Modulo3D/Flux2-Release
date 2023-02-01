using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
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
                .AsObservableChangeSet(f => f.Position)
                .DisposeMany()
                .AsObservableCacheRC(this);

            ToolNozzles = Feeders.Connect()
                .TransformMany(CreateToolNozzles, tn => tn.Position)
                .DisposeMany()
                .AsObservableCacheRC(this);

            ToolMaterials = Feeders.Connect()
                .TransformMany(CreateToolMaterials, tn => tn.Position)
                .DisposeMany()
                .AsObservableCacheRC(this);

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
                .ToPropertyRC(this, f => f.HasInvalidStates);

            // TODO
            Materials = Feeders.Connect()
                .AutoRefresh(f => f.SelectedMaterial)
                .Transform(f => f.SelectedMaterial, true)
                .AutoRefresh(m => m.Document)
                .Transform(f => f.Convert(f => f.Document), true)
                .AsObservableCacheRC(this);

            Tools = Feeders.Connect()
                .AutoRefresh(f => f.ToolNozzle.Document)
                .Transform(f => f.ToolNozzle.Document, true)
                .Transform(tn => tn.tool)
                .AsObservableCacheRC(this);

            Nozzles = Feeders.Connect()
              .AutoRefresh(f => f.ToolNozzle.Document)
              .Transform(f => f.ToolNozzle.Document, true)
              .Transform(tn => tn.nozzle)
              .AsObservableCacheRC(this);

            var selected_extruder = Flux.ConnectionProvider
                .ObserveVariable(m => m.TOOL_ON_TRAILER)
                .Convert(c => c.QueryWhenChanged(q => (short)q.Items.IndexOf(true)));

            _SelectedExtruder = selected_extruder
                .ObservableOr(() => (short)-1)
                .ToPropertyRC(this, v => v.SelectedExtruder);

            _SelectedFeeder = selected_extruder
                .ConvertToObservable(FindSelectedFeeder)
                .ObservableOrDefault()
                .ToPropertyRC(this, v => v.SelectedFeeder);
        }

        private IObservable<Optional<IFluxFeederViewModel>> FindSelectedFeeder(short selected_extruder)
        {
            return Feeders.Connect().WatchOptional((ushort)selected_extruder);
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

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var documents = await CompositeQuery.Create(database.Value,
                    db => (_, ct) => db.FindAsync(printer.Value, Tool.SchemaInstance, ct), db => db.GetTargetAsync,
                    db => (t, ct) => db.FindAsync(t, Nozzle.SchemaInstance, ct), db => db.GetTargetAsync,
                    db => (n, ct) => db.FindAsync(n, ToolMaterial.SchemaInstance, ct), db => db.GetTargetAsync,
                    db => (tm, ct) => db.FindAsync(Material.SchemaInstance, tm, ct), db => db.GetSourceAsync)
                    .ExecuteAsync<Material>(cts.Token);

            var materials = documents.Distinct()
                .OrderBy(d => d[d => d.MaterialType, ""])
                .ThenBy(d => d.Name)
                .AsObservableChangeSet(m => m.Id);

            var last_loaded = last_tag.Convert(t => t.Loaded);
            var last_inserted = last_tag.Convert(t => t.Inserted);
            var last_cur_weight = last_tag.Convert(t => t.CurWeightG).ValueOr(() => 1000.0);
            var last_max_weight = last_tag.Convert(t => t.MaxWeightG).ValueOr(() => 1000.0);
            var last_printer_guid = last_tag.ConvertOr(t => t.PrinterGuid, () => Guid.Empty);
            var last_material_guid = last_tag.ConvertOr(t => t.MaterialGuid, () => Guid.Empty);

            var material_option = ComboOption.Create("material", "MATERIALE:", d => materials.AsObservableCache().DisposeWith(d));
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

            var tag = new NFCMaterial(material.Value, max_weight, cur_weight, last_printer_guid, last_inserted, last_loaded);
            return new NFCReading<NFCMaterial>(virtual_card_id, tag);
        }

        private async Task<ValueResult<Tool>> FindNewToolAsync(ushort position, Optional<NFCToolNozzle> last_tag)
        {
            var database = Flux.DatabaseProvider.Database;
            if (!database.HasValue)
                return default;

            var printer = Flux.SettingsProvider.Printer;
            if (!printer.HasValue)
                return default;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var tool_documents = await CompositeQuery.Create(database.Value,
               db => (_, ct) => db.FindAsync(printer.Value, Tool.SchemaInstance, ct), db => db.GetTargetAsync)
               .ExecuteAsync<Tool>(cts.Token);

            var tools = tool_documents.OrderBy(d => d.Name)
                .AsObservableChangeSet(t => t.Id);

            var last_tool_guid = last_tag.ConvertOr(t => t.NozzleGuid, () => Guid.Empty);

            var tool_option = ComboOption.Create("tool", "Utensile:", d => tools.AsObservableCache().DisposeWith(d));
            var tool_result = await Flux.ShowSelectionAsync(
                $"UTENSILE N.{position + 1}", new[] { tool_option });

            if (tool_result != ContentDialogResult.Primary)
                return default;

            var tool = tool_option.Value;
            if (!tool.HasValue)
                return new ValueResult<Tool>(default);

            return tool.Value;
        }

        private async Task<(ValueResult<Nozzle> nozzle, double max_weight, double cur_weight)> FindNewNozzleAsync(ushort position, Tool tool, Optional<NFCToolNozzle> last_tag)
        {
            var database = Flux.DatabaseProvider.Database;
            if (!database.HasValue)
                return default;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var nozzle_documents = await CompositeQuery.Create(database.Value,
               db => (_, ct) => db.FindAsync(tool, Nozzle.SchemaInstance, ct), db => db.GetTargetAsync)
               .ExecuteAsync<Nozzle>(cts.Token);

            if (!nozzle_documents.HasDocuments)
                return default;

            var nozzles = nozzle_documents.OrderBy(d => d.Name)
                .AsObservableChangeSet(n => n.Id);

            var last_nozzle_guid = last_tag.ConvertOr(t => t.NozzleGuid, () => Guid.Empty);
            var last_cur_weight = last_tag.Convert(t => t.CurWeightG).ValueOr(() => 10000.0);
            var last_max_weight = last_tag.Convert(t => t.MaxWeightG).ValueOr(() => 10000.0);

            var nozzle_option = ComboOption.Create("nozzle", "UGELLO:", d => nozzles.AsObservableCache().DisposeWith(d));
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
            return new NFCReading<NFCToolNozzle>(virtual_card_id, tag);
        }

        IObservable<IChangeSet<NFCSlot<NFCMaterial>, ushort>> INFCStorageProvider<NFCMaterial>.GetNFCSlots() => ToolMaterials.Connect().Transform(m => m.NFCSlot);
        IObservable<IChangeSet<NFCSlot<NFCToolNozzle>, ushort>> INFCStorageProvider<NFCToolNozzle>.GetNFCSlots() => ToolNozzles.Connect().Transform(m => m.NFCSlot);
    }
}
