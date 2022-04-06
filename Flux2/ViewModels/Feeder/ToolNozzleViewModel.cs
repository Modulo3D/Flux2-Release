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
    public class ToolNozzleViewModel : TagViewModel<NFCToolNozzle, (Optional<Tool> tool, Optional<Nozzle> nozzle), ToolNozzleState>, IFluxToolNozzleViewModel
    {
        public override int VirtualTagId => 2;

        public override OdometerViewModel<NFCToolNozzle> Odometer { get; }

        private ObservableAsPropertyHelper<Optional<FLUX_Temp>> _Temperature;
        public Optional<FLUX_Temp> Temperature => _Temperature.Value;

        private ObservableAsPropertyHelper<ToolNozzleState> _State;
        public override ToolNozzleState State => _State.Value;

        private bool _InMaintenance;
        public bool InMaintenance
        {
            get => _InMaintenance;
            set => this.RaiseAndSetIfChanged(ref _InMaintenance, value);
        }

        public ReactiveCommand<Unit, Unit> ChangeCommand { get; private set; }

        public ToolNozzleViewModel(FeederViewModel feeder) : base(feeder, s => s.ToolNozzles, (db, tn) =>
        {
            return (tn.GetDocument<Tool>(db, tn => tn.ToolGuid),
                tn.GetDocument<Nozzle>(db, tn => tn.NozzleGuid));
        }, t => t.ToolGuid)
        {
            var multiplier = feeder.Material.WhenAnyValue(v => v.Document)
                .Convert(m => m.GetPropertyRW("odometer_multiplier").TryGetValue<double>())
                .ValueOr(() => 1);

            Odometer = new OdometerViewModel<NFCToolNozzle>(this, multiplier);

            var tool_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.TEMP_TOOL, Feeder.Position);
            _Temperature = Flux.ConnectionProvider
                .ObserveVariable(m => m.TEMP_TOOL, tool_key.ValueOr(() => ""))
                .ToProperty(this, v => v.Temperature);
        }

        public override void Initialize()
        {
            _State = FindToolState()
                .ToProperty(this, v => v.State);

            var can_load_unload_tool = Observable.CombineLatest(
                Flux.StatusProvider.CanSafeCycle,
                this.WhenAnyValue(v => v.State),
                Feeder.Material.WhenAnyValue(v => v.State),
                (idle, tool, material) =>
                {
                    if (!idle)
                        return false;
                    if (!material.IsNotLoaded())
                        return false;
                    return true;
                });

            ChangeCommand = ReactiveCommand.CreateFromTask(ChangeAsync, can_load_unload_tool);
        }

        public Task ChangeAsync()
        {
            /*var tool_change = new StartToolChangeViewModel(Feeder);
            Flux.Navigator.Navigate(tool_change);*/
            return Task.CompletedTask;
        }
        private IObservable<ToolNozzleState> FindToolState()
        {
            var tool_cur = Flux.ConnectionProvider.ObserveVariable(m => m.TOOL_CUR)
                .DistinctUntilChanged();

            var in_idle = Flux.StatusProvider.IsIdle
                .ValueOrDefault()
                .DistinctUntilChanged();

            var in_change = Flux.ConnectionProvider.ObserveVariable(m => m.IN_CHANGE)
                .DistinctUntilChanged();

            var in_change_error = Observable.CombineLatest(
                in_idle,
                in_change,
                (in_idle, in_change) => in_change.Convert(in_change => in_idle && in_change))
                .Throttle(TimeSpan.FromSeconds(1));

            var inserted = this.WhenAnyValue(v => v.Temperature)
                .ConvertOr(t => t.Current > -100 && t.Current < 1000, () => false)
                .DistinctUntilChanged();

            var mem_magazine_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.MEM_TOOL_IN_MAGAZINE, Feeder.Position).ValueOr(() => "");
            var mem_magazine = Flux.ConnectionProvider.ObserveVariable(m => m.MEM_TOOL_IN_MAGAZINE, mem_magazine_key)
                .DistinctUntilChanged();

            var mem_trailer_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.MEM_TOOL_ON_TRAILER, Feeder.Position).ValueOr(() => "");
            var mem_trailer = Flux.ConnectionProvider.ObserveVariable(m => m.MEM_TOOL_ON_TRAILER, mem_trailer_key)
                .DistinctUntilChanged();

            var input_magazine_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.TOOL_IN_MAGAZINE, Feeder.Position).ValueOr(() => "");
            var input_magazine = Flux.ConnectionProvider.ObserveVariable(m => m.TOOL_IN_MAGAZINE, input_magazine_key)
                .DistinctUntilChanged();

            var input_trailer_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.TOOL_ON_TRAILER, Feeder.Position).ValueOr(() => "");
            var input_trailer = Flux.ConnectionProvider.ObserveVariable(m => m.TOOL_ON_TRAILER, input_trailer_key)
                .DistinctUntilChanged();

            var known = this.WhenAnyValue(v => v.Document)
                .Select(document => document.tool.HasValue && document.nozzle.HasValue)
                .DistinctUntilChanged();

            var printer_guid = Flux.SettingsProvider.CoreSettings.Local.PrinterGuid;

            var locked = this.WhenAnyValue(v => v.Nfc)
                .Select(nfc => nfc.Tag.ConvertOr(t => t.PrinterGuid == printer_guid, () => false));

            var loaded = this.WhenAnyValue(v => v.Nfc)
                .Select(nfc => nfc.Tag.ConvertOr(t => t.Loaded == Feeder.Position, () => false));

            var in_mateinance = this.WhenAnyValue(v => v.InMaintenance);

            return Observable.CombineLatest(
                in_change_error, inserted, mem_trailer, input_trailer, mem_magazine, input_magazine, known, locked, loaded, in_mateinance,
                (in_change_error, inserted, mem_trailer, input_trailer, mem_magazine, input_magazine, known, locked, loaded, in_mateinance) =>
                {
                    var on_trailer = mem_trailer.Convert(t => t && t == input_trailer.ValueOr(() => t)).ValueOr(() => false);
                    var in_magazine = mem_magazine.Convert(t => t && t == input_magazine.ValueOr(() => t)).ValueOr(() => false);
                    return new ToolNozzleState(inserted, known, locked, loaded, on_trailer, in_magazine, in_mateinance, in_change_error.ValueOr(() => false));
                });
        }

        public async Task<(bool result, Optional<Tool> tool)> FindNewToolAsync()
        {
            var database = Flux.DatabaseProvider.Database;
            if (!database.HasValue)
                return default;

            var printer = Flux.SettingsProvider.Printer;
            if (!printer.HasValue)
                return (false, default);

            var tool_documents = CompositeQuery.Create(database.Value,
               db => _ => db.Find(printer.Value, Tool.SchemaInstance), db => db.GetTarget)
               .Execute()
               .Convert<Tool>();

            var tools = tool_documents.Documents
                .OrderBy(d => d.Name)
                .AsObservableChangeSet(t => t.Id)
                .AsObservableCache();

            var tool_option = ComboOption.Create("tool", "Utensile:", tools);
            var tool_result = await Flux.ShowSelectionAsync(
                $"UTENSILE N.{Feeder.Position + 1}",
                Observable.Return(true),
                tool_option);

            if (tool_result != ContentDialogResult.Primary)
                return default;

            var tool = tool_option.Value;
            if (!tool.HasValue)
                return (true, default);

            return (true, tool.Value);
        }
        public async Task<(bool result, Optional<Nozzle> nozzle, double max_weight, double cur_weight)> FindNewNozzleAsync(Tool tool)
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

            var nozzle_option = ComboOption.Create("nozzle", "UGELLO:", nozzles);
            var cur_weight_option = new NumericOption("curWeight", "PESO CORRENTE:", 10000.0, 500.0, converter: typeof(WeightConverter));
            var max_weight_option = ComboOption.Create("maxWeight", "PESO TOTALE:", nozzle_weights, selection_changed:
            v =>
            {
                cur_weight_option.Min = 0f;
                cur_weight_option.Max = v.ValueOr(() => 0);
                cur_weight_option.Value = v.ValueOr(() => 0);
            }, converter: typeof(WeightConverter));

            var nozzle_result = await Flux.ShowSelectionAsync(
                $"UGELLO N.{Feeder.Position + 1}",
                Observable.Return(true),
                nozzle_option, max_weight_option, cur_weight_option);

            if (nozzle_result != ContentDialogResult.Primary)
                return (false, default, default, default);

            var nozzle = nozzle_option.Value;
            if (!nozzle.HasValue)
                return (true, default, default, default);

            var max_weight = max_weight_option.Value;
            if (!max_weight.HasValue)
                return (true, default, default, default);

            var cur_weight = cur_weight_option.Value;

            return (true, nozzle.Value, max_weight.Value, cur_weight);
        }

        public override async Task<Optional<NFCToolNozzle>> CreateTagAsync()
        {
            var tool = await FindNewToolAsync();
            if (!tool.result)
                return default;

            if (!tool.tool.HasValue)
                return default;

            var nozzle = await FindNewNozzleAsync(tool.tool.Value);
            if (!nozzle.result)
                return default;

            return new NFCToolNozzle(tool.tool.Value, nozzle.nozzle, nozzle.max_weight, nozzle.cur_weight);
        }
    }
}
