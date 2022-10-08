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
    public class ToolNozzleViewModel : TagViewModel<ToolNozzleViewModel, NFCToolNozzle, (Optional<Tool> tool, Optional<Nozzle> nozzle), ToolNozzleState>, IFluxToolNozzleViewModel
    {
        public override ushort VirtualTagId => 2;

        public override OdometerViewModel<NFCToolNozzle> Odometer { get; }

        private ObservableAsPropertyHelper<Optional<FLUX_Temp>> _NozzleTemperature;
        [RemoteOutput(true, typeof(FluxTemperatureConverter))]
        public Optional<FLUX_Temp> NozzleTemperature => _NozzleTemperature.Value;

        private ObservableAsPropertyHelper<ToolNozzleState> _State;
        public override ToolNozzleState State => _State.Value;

        private ObservableAsPropertyHelper<Optional<IFluxMaterialViewModel>> _MaterialLoaded;
        public Optional<IFluxMaterialViewModel> MaterialLoaded => _MaterialLoaded.Value;

        private bool _InMaintenance;
        public bool InMaintenance
        {
            get => _InMaintenance;
            set => this.RaiseAndSetIfChanged(ref _InMaintenance, value);
        }

        private ObservableAsPropertyHelper<string> _ToolNozzleBrush;
        [RemoteOutput(true)]
        public string ToolNozzleBrush => _ToolNozzleBrush.Value;

        public ReactiveCommand<Unit, Unit> ChangeCommand { get; private set; }

        private ObservableAsPropertyHelper<Optional<string>> _DocumentLabel;
        [RemoteOutput(true)]
        public override Optional<string> DocumentLabel => _DocumentLabel.Value;

        public ToolNozzleViewModel(FeederViewModel feeder) : base(feeder, feeder.Position, s => s.ToolNozzles, (db, tn) =>
        {
            return (tn.GetDocument<Tool>(db, tn => tn.ToolGuid),
                tn.GetDocument<Nozzle>(db, tn => tn.NozzleGuid));
        }, t => t.ToolGuid)
        {
            var multiplier = Observable.Return(1.0);
            Odometer = new OdometerViewModel<NFCToolNozzle>(Flux, this, multiplier);

            var tool_key = Flux.ConnectionProvider.GetArrayUnit(m => m.TEMP_TOOL, Position);
            _NozzleTemperature = Flux.ConnectionProvider
                .ObserveVariable(m => m.TEMP_TOOL, tool_key)
                .ObservableOrDefault()
                .ToProperty(this, v => v.NozzleTemperature)
                .DisposeWith(Disposables);

            _State = FindToolState()
                .ToProperty(this, v => v.State)
                .DisposeWith(Disposables);

            _ToolNozzleBrush =
                this.WhenAnyValue(v => v.State)
                .Select(tn =>
                {
                    if (tn.IsNotLoaded())
                        return FluxColors.Empty;
                    if (!tn.Known)
                        return FluxColors.Error;
                    if (!tn.Loaded)
                        return FluxColors.Warning;
                    if (!tn.Locked)
                        return FluxColors.Warning;
                    return FluxColors.Active;
                })
                .ToProperty(this, v => v.ToolNozzleBrush)
                .DisposeWith(Disposables);

            _DocumentLabel = this.WhenAnyValue(v => v.Document.nozzle)
                .Convert(d => d.Name)
                .ToProperty(this, v => v.DocumentLabel)
                .DisposeWith(Disposables);
        }

        public NFCReading<NFCToolNozzle> SetLastBreakTemp(GCodeFilamentOperation filament_settings)
        {
            return StoreTag(c => c.SetLastBreakTemp(filament_settings.CurBreakTemp));
        }
        public override void Initialize()
        {
            base.Initialize();

            _MaterialLoaded = Feeder.Materials.Connect()
                .AutoRefresh(m => m.State)
                .QueryWhenChanged(m => m.Items.FirstOrOptional(m => m.State.IsLoaded()))
                .ToProperty(this, v => v.MaterialLoaded)
                .DisposeWith(Disposables);

            var material = Feeder.WhenAnyValue(f => f.SelectedMaterial);

            var can_load_unload_tool = Observable.CombineLatest(
                Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation).Select(s => s.CanSafeCycle),
                this.WhenAnyValue(v => v.State),
                material.ConvertMany(m => m.WhenAnyValue(v => v.State)),
                (idle, tool, material) =>
                {
                    if (!idle)
                        return false;
                    if (!material.HasValue)
                        return false;
                    if (!material.Value.IsNotLoaded())
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
            var has_tool_change = Flux.ConnectionProvider.HasToolChange;

            var tool_cur = Flux.ConnectionProvider
                .ObserveVariable(m => m.TOOL_CUR);

            var in_idle = Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(s => s.IsIdle)
                .DistinctUntilChanged();

            var in_change = Flux.ConnectionProvider.ObserveVariable(m => m.IN_CHANGE)
                .ObservableOr(() => false);

            var in_change_error = Observable.CombineLatest(
                in_idle,
                in_change,
                (in_idle, in_change) => in_idle && in_change);

            var inserted = this.WhenAnyValue(v => v.NozzleTemperature)
                .ConvertOr(t => t.Current > -100 && t.Current < 1000, () => false);

            var mem_magazine_key = Flux.ConnectionProvider.GetArrayUnit(m => m.MEM_TOOL_IN_MAGAZINE, Position);
            var mem_magazine = Flux.ConnectionProvider
                .ObserveVariable(m => m.MEM_TOOL_IN_MAGAZINE, mem_magazine_key);

            var mem_trailer_key = Flux.ConnectionProvider.GetArrayUnit(m => m.MEM_TOOL_ON_TRAILER, Position);
            var mem_trailer = Flux.ConnectionProvider
                .ObserveVariable(m => m.MEM_TOOL_ON_TRAILER, mem_trailer_key);

            var input_magazine_key = Flux.ConnectionProvider.GetArrayUnit(m => m.TOOL_IN_MAGAZINE, Position);
            var input_magazine = Flux.ConnectionProvider
                .ObserveVariable(m => m.TOOL_IN_MAGAZINE, input_magazine_key);

            var input_trailer_key = Flux.ConnectionProvider.GetArrayUnit(m => m.TOOL_ON_TRAILER, Position);
            var input_trailer = Flux.ConnectionProvider
                .ObserveVariable(m => m.TOOL_ON_TRAILER, input_trailer_key);

            var known = this.WhenAnyValue(v => v.Document)
                .Select(document => document.tool.HasValue && document.nozzle.HasValue);

            var printer_guid = Flux.SettingsProvider.CoreSettings.Local.PrinterGuid;

            var locked = this.WhenAnyValue(v => v.Nfc)
                .Select(nfc => nfc.Tag.ConvertOr(t => t.PrinterGuid == printer_guid, () => false));

            var loaded = this.WhenAnyValue(v => v.Nfc)
                .Select(nfc => nfc.Tag.ConvertOr(t => t.Loaded == Position, () => false));

            var in_mateinance = this.WhenAnyValue(v => v.InMaintenance);

            return Observable.CombineLatest(
                in_change, in_change_error, tool_cur, inserted, mem_trailer, input_trailer, mem_magazine, input_magazine, known, locked, loaded, in_mateinance,
                (in_change, in_change_error, tool_cur, inserted, mem_trailer, input_trailer, mem_magazine, input_magazine, known, locked, loaded, in_mateinance) =>
                {
                    var on_trailer = false;
                    if (input_trailer.HasChange)
                    {
                        on_trailer = input_trailer.Change.ValueOr(() => false);
                        if (mem_trailer.HasChange && mem_trailer.Change != input_trailer.Change)
                            in_change_error = true;
                    }
                    else 
                    {
                        if(mem_trailer.HasChange)
                            on_trailer = mem_trailer.Change.ValueOr(() => false);
                    }

                    var in_magazine = false;
                    if (input_magazine.HasChange)
                    {
                        in_magazine = input_magazine.Change.ValueOr(() => false);
                        if (mem_magazine.HasChange && mem_magazine.Change != input_magazine.Change)
                            in_change_error = true;
                    }
                    else
                    {
                        if (mem_magazine.HasChange)
                            in_magazine = mem_magazine.Change.ValueOr(() => false);
                    }

                    if (has_tool_change && on_trailer == in_magazine)
                        in_change_error = true;

                    var selected = tool_cur.Convert(t => t.GetZeroBaseIndex()).Convert(t => Position == t).ValueOr(() => false);
                    return new ToolNozzleState(has_tool_change, in_change, selected, inserted, known, locked, loaded, on_trailer, in_magazine, in_mateinance, in_change_error);
                });
        }

        public async Task<ValueResult<Tool>> FindNewToolAsync(Optional<NFCReading<NFCToolNozzle>> last_reading)
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

            var last_tag = last_reading.Convert(r => r.Tag);
            var last_tool_guid = last_tag.ConvertOr(t => t.NozzleGuid, () => Guid.Empty);

            var tool_option = ComboOption.Create("tool", "Utensile:", tools);
            var tool_result = await Flux.ShowSelectionAsync(
                $"UTENSILE N.{Position + 1}",
                Observable.Return(true),
                tool_option);

            if (tool_result != ContentDialogResult.Primary)
                return default;

            var tool = tool_option.Value;
            if (!tool.HasValue)
                return new ValueResult<Tool>(default);

            return tool.Value;
        }
        public async Task<(ValueResult<Nozzle> nozzle, double max_weight, double cur_weight)> FindNewNozzleAsync(Tool tool, Optional<NFCReading<NFCToolNozzle>> last_reading)
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

            var last_tag = last_reading.Convert(r => r.Tag);
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
                $"UGELLO N.{Position + 1}",
                Observable.Return(true),
                nozzle_option, max_weight_option, cur_weight_option);

            if (nozzle_result != ContentDialogResult.Primary)
                return (default, default, default);

            var nozzle = nozzle_option.Value;
            if (!nozzle.HasValue)
                return (new ValueResult<Nozzle>(default), default, default);

            var max_weight = max_weight_option.Value;
            var cur_weight = cur_weight_option.Value;

            return (nozzle.Value, max_weight, cur_weight);
        }

        public override async Task<ValueResult<NFCToolNozzle>> CreateTagAsync(Optional<NFCReading<NFCToolNozzle>> last_reading)
        {
            var tool = await FindNewToolAsync(last_reading);
            if (!tool.Result)
                return default;

            if (!tool.HasValue)
                return default;

            var nozzle = await FindNewNozzleAsync(tool.Value, last_reading);
            if (!nozzle.nozzle.Result)
                return default;

            var last_tag = last_reading.Convert(r => r.Tag);
            var last_loaded = last_tag.Convert(t => t.Loaded);
            var last_break_temp = last_tag.Convert(t => t.LastBreakTemperature);
            var last_printer_guid = last_tag.ConvertOr(t => t.PrinterGuid, () => Guid.Empty);

            return new NFCToolNozzle(tool.Value, nozzle.nozzle, nozzle.max_weight, nozzle.cur_weight, last_printer_guid, last_loaded, last_break_temp);
        }
    }
}
