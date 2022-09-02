using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public interface IManualCalibrationPhaseViewModel : IRemoteControl
    {
        FluxViewModel Flux { get; }
        CalibrationViewModel Calibration { get; }
        ReactiveCommand<Unit, Unit> CancelCalibrationCommand { get; }
    }

    public abstract class ManualCalibrationPhaseViewModel<TManualCalibrationPhase> : RemoteControl<TManualCalibrationPhase>, IManualCalibrationPhaseViewModel
        where TManualCalibrationPhase : ManualCalibrationPhaseViewModel<TManualCalibrationPhase>, IManualCalibrationPhaseViewModel
    {
        public FluxViewModel Flux { get; }
        public CalibrationViewModel Calibration { get; }

        [RemoteCommand]
        public abstract ReactiveCommand<Unit, Unit> CancelCalibrationCommand { get; protected set; }

        private ObservableAsPropertyHelper<Optional<ushort>> _SelectedTool;
        [RemoteOutput(true)]
        public Optional<ushort> SelectedTool => _SelectedTool.Value;

        public ManualCalibrationPhaseViewModel(CalibrationViewModel calibration) : base()
        {
            Flux = calibration.Flux;
            Calibration = calibration;

            _SelectedTool = Flux.ConnectionProvider.ObserveVariable(m => m.TOOL_CUR)
                .Convert(o => o.ToOptional(o => o > -1).Convert(o => (ushort)o))
                .ToProperty(this, v => v.SelectedTool)
                .DisposeWith(Disposables);

            var can_probe_plate = Flux.StatusProvider
                .WhenAnyValue(v => v.StatusEvaluation)
                .Select(s => s.CanSafePrint);
        }

        public virtual void Initialize()
        {
            InitializeRemoteView();
        }

        protected async Task ExitAsync()
        {
            using var put_exit_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var wait_exit_cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await Flux.ConnectionProvider.ExecuteParamacroAsync(c =>
            {
                var gcode = new List<string>();

                if (SelectedTool.HasValue)
                {
                    var set_tool_temp_gcode = c.GetSetToolTemperatureGCode(SelectedTool.Value, 0);
                    if (!set_tool_temp_gcode.HasValue)
                        return default;
                    gcode.AddRange(set_tool_temp_gcode.Value);
                }

                var lower_plate_gcode = c.GetLowerPlateGCode();
                if (!lower_plate_gcode.HasValue)
                    return default;
                gcode.AddRange(lower_plate_gcode.Value);

                var park_tool_gcode = c.GetParkToolGCode();
                if (!park_tool_gcode.HasValue)
                    return default;
                gcode.AddRange(park_tool_gcode.Value);

                return gcode;

            }, put_exit_cts.Token, true, wait_exit_cts.Token, false);

            if (Flux.ConnectionProvider.HasVariable(c => c.ENABLE_VACUUM))
                await Flux.ConnectionProvider.WriteVariableAsync(c => c.ENABLE_VACUUM, false);

            Flux.Navigator.NavigateBack();
        }
    }

    public class PrepareManualCalibrationViewModel : ManualCalibrationPhaseViewModel<PrepareManualCalibrationViewModel>
    {
        [RemoteContent(true)]
        public ISourceCache<IConditionViewModel, string> Conditions { get; private set; }

        private ObservableAsPropertyHelper<bool> _HasSafeStart;
        [RemoteOutput(true)]
        public bool HasSafeStart => _HasSafeStart?.Value ?? false;

        [RemoteCommand]
        public override ReactiveCommand<Unit, Unit> CancelCalibrationCommand { get; protected set; }

        public PrepareManualCalibrationViewModel(CalibrationViewModel calibration) : base(calibration)
        {
            Conditions = new SourceCache<IConditionViewModel, string>(c => c.Name);

            _HasSafeStart = Conditions.Connect()
                .AutoRefresh(c => c.State)
                .TrueForAll(line => line.StateChanged, state => state.Valid)
                .StartWith(true)
                .ToProperty(this, e => e.HasSafeStart);

            var can_cancel = Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(s => s.CanSafeStop);

            CancelCalibrationCommand = ReactiveCommand.CreateFromTask(ExitAsync, can_cancel);
        }

        public override void Initialize()
        {
            if (Flux.StatusProvider.VacuumPresence.HasValue)
                Conditions.AddOrUpdate(Flux.StatusProvider.VacuumPresence.Value);

            // TODO
            if (Flux.StatusProvider.TopLockClosed.HasValue)
                Conditions.AddOrUpdate(Flux.StatusProvider.TopLockClosed.Value);
            if (Flux.StatusProvider.ChamberLockClosed.HasValue)
                Conditions.AddOrUpdate(Flux.StatusProvider.ChamberLockClosed.Value);

            if (Flux.StatusProvider.HasZBedHeight.HasValue)
                Conditions.AddOrUpdate(Flux.StatusProvider.HasZBedHeight.Value);

            InitializeRemoteView();
        }
    }

    public class PerformManualCalibrationViewModel : ManualCalibrationPhaseViewModel<PerformManualCalibrationViewModel>
    {
        private ObservableAsPropertyHelper<Optional<FLUX_Temp>> _CurrentTemperature;
        [RemoteOutput(true, typeof(FluxTemperatureConverter))]
        public Optional<FLUX_Temp> CurrentTemperature => _CurrentTemperature.Value;

        private ObservableAsPropertyHelper<double> _TemperaturePercentage;
        [RemoteOutput(true)]
        public double TemperaturePercentage => _TemperaturePercentage.Value;

        [RemoteContent(true)]
        public ISourceList<CmdButton> MoveUpButtons { get; }
        [RemoteContent(true)]
        public ISourceList<CmdButton> MoveDownButtons { get; }

        [RemoteContent(true)]
        public IObservableList<CmdButton> ToolButtons { get; }
        private ObservableAsPropertyHelper<string> _AxisPosition;
        [RemoteOutput(true)]
        public string AxisPosition => _AxisPosition.Value;

        [RemoteCommand]
        public override ReactiveCommand<Unit, Unit> CancelCalibrationCommand { get; protected set; }

        public PerformManualCalibrationViewModel(CalibrationViewModel calibration) : base(calibration)
        {
            _CurrentTemperature = this.WhenAnyValue(v => v.SelectedTool)
               .Select(t =>
               {
                   if (!t.HasValue)
                       return Observable.Return(Optional<FLUX_Temp>.None);

                   var tool_key = Flux.ConnectionProvider.GetArrayUnit(m => m.TEMP_TOOL, t.Value);
                   if (!tool_key.HasValue)
                       return Observable.Return(Optional<FLUX_Temp>.None);

                   return Flux.ConnectionProvider.ObserveVariable(m => m.TEMP_TOOL, tool_key.Value.Alias)
                       .ObservableOrDefault();
               })
               .Switch()
               .ToProperty(this, v => v.CurrentTemperature)
               .DisposeWith(Disposables);

            _TemperaturePercentage = this.WhenAnyValue(v => v.CurrentTemperature)
                .ConvertOr(t => t.Percentage, () => 0)
                .ToProperty(this, v => v.TemperaturePercentage)
                .DisposeWith(Disposables);

            MoveUpButtons = new SourceList<CmdButton>();
            MoveUpButtons.Add(FindMoveButton(-1, false));
            MoveUpButtons.Add(FindMoveButton(-0.1, true));
            MoveUpButtons.Add(FindMoveButton(-0.01, true));
            MoveUpButtons.DisposeWith(Disposables);

            MoveDownButtons = new SourceList<CmdButton>();
            MoveDownButtons.Add(FindMoveButton(1, false));
            MoveDownButtons.Add(FindMoveButton(0.1, true));
            MoveDownButtons.Add(FindMoveButton(0.01, true));
            MoveDownButtons.DisposeWith(Disposables);

            var move_up_not_executing = MoveUpButtons.Connect()
                .AddKey(b => b.Name)
                .Transform(m => m.Command, true)
                .TrueForAll(c => c.IsExecuting, e => !e)
                .StartWith(false);

            var move_down_not_executing = MoveDownButtons.Connect()
                .AddKey(b => b.Name)
                .Transform(m => m.Command, true)
                .TrueForAll(c => c.IsExecuting, e => !e)
                .StartWith(false);

            var not_executing = Observable.CombineLatest(
                move_up_not_executing, move_down_not_executing,
                (up_not_executing, down_not_executing) => up_not_executing && down_not_executing)
                .Delay(ne => Observable.Timer(TimeSpan.FromSeconds(ne ? 1 : 0)));

            ToolButtons = Flux.SettingsProvider
                .WhenAnyValue(v => v.ExtrudersCount)
                .Select(e => FindToolButtons(e, not_executing))
                .ToObservableChangeSet()
                .AsObservableList()
                .DisposeWith(Disposables);

            not_executing.Subscribe(async not_executing =>
                {
                    if (not_executing)
                    {
                        var offset = Calibration.Offsets.LookupOptional(SelectedTool);
                        if (!offset.HasValue)
                            return;

                        var bed_height = 0.0;
                        if (Flux.ConnectionProvider.HasVariable(c => c.Z_BED_HEIGHT))
                        {
                            var z_bed_height = await Flux.ConnectionProvider.ReadVariableAsync(c => c.Z_BED_HEIGHT);
                            if (!z_bed_height.HasValue)
                                return;
                            
                            bed_height = z_bed_height.Value;
                        }

                        var z = await Flux.ConnectionProvider.ReadVariableAsync(m => m.AXIS_POSITION, "Z");
                        if (!z.HasValue)
                            return;

                        offset.Value.ModifyProbeOffset(p => new ProbeOffset(p.Key, p.X, p.Y, z.Value - bed_height - 0.3));
                        Flux.SettingsProvider.UserSettings.PersistLocalSettings();
                    }
                })
                .DisposeWith(Disposables);

            _AxisPosition = Flux.ConnectionProvider.ObserveVariable(m => m.AXIS_POSITION)
                .Convert(c => c.QueryWhenChanged(p => string.Join(" ", p.KeyValues.Select(v => $"{v.Key}{v.Value:0.00}"))))
                .ToOptionalObservable()
                .ObservableOr(() => "")
                .ToProperty(this, v => v.AxisPosition);

            var can_safe_stop = Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(s => s.CanSafeStop);

            var can_cancel = Observable.CombineLatest(
                can_safe_stop,
                not_executing,
                (ss, ne) => ss && ne);

            CancelCalibrationCommand = ReactiveCommand.CreateFromTask(ExitAsync, can_cancel);
        }
        CmdButton FindMoveButton(double distance, bool can_unsafe_cycle)
        {
            var can_safe_cycle = Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(e => e.CanSafePrint);

            var can_execute = Observable.CombineLatest(
                this.WhenAnyValue(v => v.SelectedTool),
                this.WhenAnyValue(v => v.CurrentTemperature),
                can_safe_cycle,
                (tool, temp, can_safe_cycle) =>
                {
                    if (!tool.HasValue)
                        return false;
                    if (!temp.HasValue)
                        return false;
                    if (temp.Value.Target <= 0)
                        return false;
                    if (temp.Value.Percentage < 85)
                        return default;
                    if (!can_safe_cycle)
                        return can_unsafe_cycle;
                    return true;
                })
                .ToOptional();

            var button = new CmdButton($"Z??{(distance > 0 ? $"+{distance:0.00mm}" : $"{distance:0.00mm}")}", () => move_tool(distance), can_execute);
            button.InitializeRemoteView();
            button.DisposeWith(Disposables);
            return button;

            async Task move_tool(double d)
            {
                using var put_relative_movement_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await Flux.ConnectionProvider.ExecuteParamacroAsync(c => c.GetRelativeZMovementGCode(d, 500), put_relative_movement_cts.Token);
            }
        }
        private IEnumerable<CmdButton> FindToolButtons(Optional<(ushort machine_extruders, ushort mixing_extruders)> extruders, IObservable<bool> not_executing)
        {
            if (!extruders.HasValue)
                yield break;

            for (ushort extruder = 0; extruder < extruders.Value.machine_extruders; extruder++)
            {
                var e = extruder;

                var print_temp = Flux.Feeders.Feeders.Connect()
                    .WatchOptional(e)
                    .ConvertMany(f => f.WhenAnyValue(f => f.SelectedToolMaterial))
                    .ConvertMany(tm => tm.WhenAnyValue(tm => tm.Document))
                    .Convert(tm => tm[tm => tm.PrintTemperature, 0.0]);

                var tool_offset = Calibration.Offsets.Connect()
                    .WatchOptional(e)
                    .ConvertMany(o => o.WhenAnyValue(o => o.FluxOffset));

                var can_execute = Observable.CombineLatest(
                    print_temp,
                    tool_offset,
                    not_executing,
                    this.WhenAnyValue(v => v.SelectedTool),
                    Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation).Select(s => s.IsIdle),
                    (temp, offset, ne, tool, idle) => temp.HasValue && offset.HasValue && ne && idle && tool != e)
                    .ToOptional();

                var button = new CmdButton($"selectExtruder??{e + 1}", select_tool, can_execute);
                button.InitializeRemoteView();
                yield return button;

                async Task select_tool()
                {
                    var print_temperature = Flux.Feeders.Feeders
                        .Lookup(e)
                        .Convert(f => f.SelectedToolMaterial)
                        .Convert(tm => tm.Document)
                        .Convert(tm => tm[tm => tm.PrintTemperature, 0.0]);
                    if (!print_temperature.HasValue)
                        return;

                    var offset = Calibration.Offsets.Lookup(e);
                    var tool_offset = offset.Convert(o => o.ToolOffset);
                    if (!tool_offset.HasValue)
                        return;

                    using var put_select_tool_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    using var wait_select_tool_cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    await Flux.ConnectionProvider.ExecuteParamacroAsync(c =>
                    {
                        var gcode = new List<string>();
                        var select_tool_gcode = c.GetSelectToolGCode(e);
                        if (!select_tool_gcode.HasValue)
                            return default;
                        gcode.AddRange(select_tool_gcode.Value);

                        var set_tool_temp_gcode = c.GetSetToolTemperatureGCode(e, print_temperature.Value);
                        if (!set_tool_temp_gcode.HasValue)
                            return default;
                        gcode.AddRange(set_tool_temp_gcode.Value);

                        var set_tool_offset_gcode = c.GetSetToolOffsetGCode(e, tool_offset.Value.X, tool_offset.Value.Y, 0);
                        if (!set_tool_offset_gcode.HasValue)
                            return default;
                        gcode.AddRange(set_tool_offset_gcode.Value);

                        var manual_calibration_position_gcode = c.GetManualCalibrationPositionGCode();
                        if (!manual_calibration_position_gcode.HasValue)
                            return default;
                        gcode.AddRange(manual_calibration_position_gcode.Value);

                        var raise_plate_gcode = c.GetRaisePlateGCode();
                        if (!raise_plate_gcode.HasValue)
                            return default;
                        gcode.AddRange(raise_plate_gcode.Value);

                        return gcode;

                    }, put_select_tool_cts.Token, true, wait_select_tool_cts.Token);
                }
            }
        }
    }

    public class ManualCalibrationViewModel : FluxRoutableViewModel<ManualCalibrationViewModel>
    {
        public PrepareManualCalibrationViewModel PrepareManualCalibration { get; }
        public PerformManualCalibrationViewModel PerformManualCalibration { get; }
        private ObservableAsPropertyHelper<IManualCalibrationPhaseViewModel> _ManualCalibrationPhase;
        [RemoteContent(true)]
        public IManualCalibrationPhaseViewModel ManualCalibrationPhase => _ManualCalibrationPhase.Value;
        public ManualCalibrationViewModel(CalibrationViewModel calibration) : base(calibration.Flux)
        {
            PrepareManualCalibration = new PrepareManualCalibrationViewModel(calibration);
            PerformManualCalibration = new PerformManualCalibrationViewModel(calibration);

            PrepareManualCalibration.Initialize();
            PerformManualCalibration.Initialize();

            _ManualCalibrationPhase = PrepareManualCalibration.WhenAnyValue(v => v.HasSafeStart)
                .Select(GetManualCalibrationViewModel)
                .ToProperty(this, h => h.ManualCalibrationPhase);
        }

        private IManualCalibrationPhaseViewModel GetManualCalibrationViewModel(bool safe_start)
        {
            if (!safe_start)
                return PrepareManualCalibration;
            return PerformManualCalibration;
        }
    }
}
