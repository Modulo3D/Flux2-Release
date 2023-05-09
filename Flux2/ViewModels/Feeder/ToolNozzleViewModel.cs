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
    public class ToolNozzleViewModel : TagViewModel<ToolNozzleViewModel, NFCToolNozzle, (Optional<Tool> tool, Optional<Nozzle> nozzle), ToolNozzleState>, IFluxToolNozzleViewModel
    {
        public override ushort VirtualTagId => 2;

        private readonly ObservableAsPropertyHelper<Optional<FLUX_Temp>> _NozzleTemperature;
        [RemoteOutput(true, typeof(FluxTemperatureConverter))]
        public Optional<FLUX_Temp> NozzleTemperature => _NozzleTemperature.Value;

        private readonly ObservableAsPropertyHelper<ToolNozzleState> _State;
        public override ToolNozzleState State => _State.Value;

        private ObservableAsPropertyHelper<Optional<IFluxMaterialViewModel>> _MaterialLoaded;
        public Optional<IFluxMaterialViewModel> MaterialLoaded => _MaterialLoaded.Value;

        private bool _InMaintenance;
        public bool InMaintenance
        {
            get => _InMaintenance;
            set => this.RaiseAndSetIfChanged(ref _InMaintenance, value);
        }

        private readonly ObservableAsPropertyHelper<string> _ToolNozzleBrush;
        [RemoteOutput(true)]
        public string ToolNozzleBrush => _ToolNozzleBrush.Value;

        public ReactiveCommand<Unit, Unit> ChangeCommand { get; private set; }

        private readonly ObservableAsPropertyHelper<Optional<string>> _DocumentLabel;
        [RemoteOutput(true)]
        public override Optional<string> DocumentLabel => _DocumentLabel.Value;

        public ToolNozzleViewModel(FluxViewModel flux, FeedersViewModel feeders, FeederViewModel feeder) : base(flux, feeders, feeder, feeder.Position, s => s.NFCToolNozzles, async (db, tn) =>
        {
            return (await tn.GetDocumentAsync<Tool>(db, tn => tn.ToolGuid),
                await tn.GetDocumentAsync<Nozzle>(db, tn => tn.NozzleGuid));
        }, k => k.ToolId, t => t.ToolGuid, watch_odometer_for_pause: false)
        {
            var variable_store = Flux.ConnectionProvider.VariableStoreBase;
            var feeder_index = ArrayIndex.FromZeroBase(Position, variable_store);

            var tool_key = Flux.ConnectionProvider.GetArrayUnit(m => m.TEMP_TOOL, feeder_index);
            _NozzleTemperature = Flux.ConnectionProvider
                .ObserveVariable(m => m.TEMP_TOOL, tool_key)
                .ObservableOrDefault()
                .ToPropertyRC(this, v => v.NozzleTemperature);

            _State = FindToolState()
                .ToPropertyRC(this, v => v.State);

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
                .ToPropertyRC(this, v => v.ToolNozzleBrush);

            _DocumentLabel = this.WhenAnyValue(v => v.Document.nozzle)
                .Convert(d => d.Name)
                .ToPropertyRC(this, v => v.DocumentLabel);
        }

        public override void Initialize()
        {
            base.Initialize();

            _MaterialLoaded = Feeder.Materials.Connect()
                .AutoRefresh(m => m.State)
                .QueryWhenChanged(m => m.Items.FirstOrOptional(m => m.State.IsLoaded()))
                .ToPropertyRC(this, v => v.MaterialLoaded);

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

            ChangeCommand = ReactiveCommandRC.CreateFromTask(ChangeAsync, this, can_load_unload_tool);
        }

        public Task ChangeAsync()
        {
            /*var tool_change = new StartToolChangeViewModel(Feeder);
            Flux.Navigator.Navigate(tool_change);*/
            return Task.CompletedTask;
        }
        private IObservable<ToolNozzleState> FindToolState()
        {
            var has_tool_change = Flux.ConnectionProvider.VariableStoreBase.HasToolChange;

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

            var variable_store = Flux.ConnectionProvider.VariableStoreBase;
            var feeder_index = ArrayIndex.FromZeroBase(Position, variable_store);

            var mem_magazine_key = Flux.ConnectionProvider.GetArrayUnit(m => m.MEM_TOOL_IN_MAGAZINE, feeder_index);
            var mem_magazine = Flux.ConnectionProvider
                .ObserveVariable(m => m.MEM_TOOL_IN_MAGAZINE, mem_magazine_key);

            var mem_trailer_key = Flux.ConnectionProvider.GetArrayUnit(m => m.MEM_TOOL_ON_TRAILER, feeder_index);
            var mem_trailer = Flux.ConnectionProvider
                .ObserveVariable(m => m.MEM_TOOL_ON_TRAILER, mem_trailer_key);

            var input_magazine_key = Flux.ConnectionProvider.GetArrayUnit(m => m.TOOL_IN_MAGAZINE, feeder_index);
            var input_magazine = Flux.ConnectionProvider
                .ObserveVariable(m => m.TOOL_IN_MAGAZINE, input_magazine_key);

            var input_trailer_key = Flux.ConnectionProvider.GetArrayUnit(m => m.TOOL_ON_TRAILER, feeder_index);
            var input_trailer = Flux.ConnectionProvider
                .ObserveVariable(m => m.TOOL_ON_TRAILER, input_trailer_key);

            var known = this.WhenAnyValue(v => v.Document)
                .Select(document => document.tool.HasValue && document.nozzle.HasValue);

            var printer_guid = Flux.SettingsProvider.CoreSettings.Local.PrinterGuid;

            var locked = NFCSlot.WhenAnyValue(v => v.Nfc)
                .Select(nfc => nfc.Tag.ConvertOr(t => t.PrinterGuid == printer_guid, () => false));

            var loaded = NFCSlot.WhenAnyValue(v => v.Nfc)
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
                        if (mem_trailer.HasChange)
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
    }
}
