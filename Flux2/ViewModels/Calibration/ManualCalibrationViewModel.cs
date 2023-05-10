using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
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
    public class ManualCalibrationConditionAttribute : FilterConditionAttribute
    {
        public ManualCalibrationConditionAttribute(string name = default, bool filter_on_cycle = true, string[] include_alias = default, string[] exclude_alias = default)
            : base(name, filter_on_cycle, include_alias, exclude_alias) { }
    }
    public interface IManualCalibrationPhaseViewModel : IRemoteControl
    {
        FluxViewModel Flux { get; }
        CalibrationViewModel Calibration { get; }
        Optional<ReactiveCommandBaseRC<Unit, Unit>> CancelCalibrationCommand { get; }
    }

    public abstract class ManualCalibrationPhaseViewModel<TManualCalibrationPhase> : RemoteControl<TManualCalibrationPhase>, IManualCalibrationPhaseViewModel
        where TManualCalibrationPhase : ManualCalibrationPhaseViewModel<TManualCalibrationPhase>, IManualCalibrationPhaseViewModel
    {
        public FluxViewModel Flux { get; }
        public CalibrationViewModel Calibration { get; }

        [RemoteCommand]
        public Optional<ReactiveCommandBaseRC<Unit, Unit>> CancelCalibrationCommand { get; protected set; }
    
        private readonly ObservableAsPropertyHelper<Optional<ushort>> _SelectedTool;
        [RemoteOutput(true)]
        public Optional<ushort> SelectedTool => _SelectedTool.Value;

        public ManualCalibrationPhaseViewModel(FluxViewModel flux, CalibrationViewModel calibration)
        {
            Flux = flux;
            Calibration = calibration;

            _SelectedTool = Flux.ConnectionProvider
                .ObserveVariable(m => m.TOOL_CUR)
                .Convert(o => o.GetZeroBaseIndex())
                .Convert(o => o.ToOptional(o => o >= 0))
                .Convert(o => (ushort)o)
                .ToPropertyRC((TManualCalibrationPhase)this, v => v.SelectedTool);
        }

        public virtual void Initialize()
        { 
            var can_cancel = GetCanCancelCalibration();
            CancelCalibrationCommand = ReactiveCommandBaseRC.CreateFromTask(ExitAsync, (TManualCalibrationPhase)this, can_cancel);
        }
        public abstract IObservable<bool> GetCanCancelCalibration();

        protected async Task ExitAsync()
        {
            await Flux.ConnectionProvider.CancelPrintAsync(true);
            Flux.Navigator.NavigateBack();
        }
    }

    public class PrepareManualCalibrationViewModel : ManualCalibrationPhaseViewModel<PrepareManualCalibrationViewModel>
    {
        [RemoteContent(true)]
        public ISourceList<IConditionViewModel> Conditions { get; private set; }

        private readonly ObservableAsPropertyHelper<bool> _HasSafeStart;
        [RemoteOutput(true)]
        public bool HasSafeStart => _HasSafeStart?.Value ?? false;

        public PrepareManualCalibrationViewModel(FluxViewModel flux, CalibrationViewModel calibration) : base(flux, calibration)
        {
            SourceListRC.Create(this, v => v.Conditions);

            _HasSafeStart = Conditions.Connect()
                .AddKey(c => c.Name)
                .TrueForAll(line => line.StateChanged, state => state.Valid)
                .StartWith(true)
                .ToPropertyRC(this, e => e.HasSafeStart);
        }

        public override void Initialize()
        {
            base.Initialize();
            var conditions = Flux.ConditionsProvider.GetConditions<ManualCalibrationConditionAttribute>();
            Conditions.AddRange(conditions.SelectMany(c => c.Value.Select(c => c.condition)));
        }

        public override IObservable<bool> GetCanCancelCalibration()
        {
            return Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(s => s.CanSafeStop);
        }
    }

    public class ManualCalibrationItemViewModel : RemoteControl<ManualCalibrationItemViewModel>
    {
        [RemoteOutput(false)]
        public ushort Position { get; }

        private readonly ObservableAsPropertyHelper<string> _ProbeStateBrush;
        [RemoteOutput(true)]
        public string ProbeStateBrush => _ProbeStateBrush.Value;
        [RemoteCommand]
        public ReactiveCommandBaseRC<Unit, Unit> SelectToolCommand { get; }
        public PerformManualCalibrationViewModel ManualCalibration { get; }

        public ManualCalibrationItemViewModel(FluxViewModel flux, PerformManualCalibrationViewModel calibration, ushort position, IObservable<bool> not_executing) 
            : base($"{typeof(ManualCalibrationItemViewModel).GetRemoteElementClass()};{position}")
        {
            ManualCalibration = calibration;
            Position = position;

            var print_temp = ManualCalibration.Flux.Feeders.Feeders.Connect()
                .WatchOptional(Position)
                .ConvertMany(f => f.WhenAnyValue(f => f.SelectedMaterial))
                .Convert(m => m.ToolMaterial)
                .ConvertMany(tm => tm.WhenAnyValue(tm => tm.ExtrusionTemp));

            var tool_offset = ManualCalibration.Calibration.Offsets.Connect()
                .WatchOptional(Position)
                .ConvertMany(o => o.WhenAnyValue(o => o.FluxOffset));

            _ProbeStateBrush = ManualCalibration.Calibration.Offsets.Connect()
                .WatchOptional(Position)
                .ConvertMany(o => o.WhenAnyValue(o => o.ProbeStateBrush))
                .ValueOr(() => FluxColors.Empty)
                .ToPropertyRC(this, v => v.ProbeStateBrush);

            var status_provider = ManualCalibration.Flux.StatusProvider;
            var can_safe_cycle = status_provider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(s => s.CanSafeCycle);

            var can_execute = Observable.CombineLatest(
                print_temp,
                tool_offset,
                not_executing,
                ManualCalibration.WhenAnyValue(v => v.SelectedTool),
                can_safe_cycle,
                (temp, offset, ne, tool, idle) => temp.HasValue && offset.HasValue && ne && idle && tool != Position);

            SelectToolCommand = ReactiveCommandBaseRC.CreateFromTask(SelectToolAsync, this, can_execute);
        }
        private async Task SelectToolAsync()
        {
            var print_temperature = ManualCalibration.Flux.Feeders.Feeders
                .Lookup(Position)
                .Convert(f => f.SelectedMaterial)
                .Convert(tm => tm.ToolMaterial.ExtrusionTemp);
            if (!print_temperature.HasValue)
                return;

            var offset = ManualCalibration.Calibration.Offsets.Lookup(Position);
            var tool_offset = offset.Convert(o => o.ToolOffset);
            if (!tool_offset.HasValue)
                return;

            var variable_store = ManualCalibration.Flux.ConnectionProvider.VariableStoreBase;
            var tool_index = ArrayIndex.FromZeroBase(Position, variable_store);

            using var put_select_tool_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var wait_select_tool_cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await ManualCalibration.Flux.ConnectionProvider.ExecuteParamacroAsync(c =>
            {
                var gcode = new List<string>();
                var select_tool_gcode = c.GetSelectToolGCode(tool_index);
                if (!select_tool_gcode.HasValue)
                    return default;

                var set_tool_temp_gcode = c.GetSetToolTemperatureGCode(tool_index, print_temperature.Value, false);
                if (!set_tool_temp_gcode.HasValue)
                    return default;

                var set_tool_offset_gcode = c.GetSetToolOffsetGCode(tool_index, tool_offset.Value.X, tool_offset.Value.Y, 0);
                if (!set_tool_offset_gcode.HasValue)
                    return default;

                var manual_calibration_position_gcode = c.GetManualCalibrationPositionGCode();
                if (!manual_calibration_position_gcode.HasValue)
                    return default;

                var raise_plate_gcode = c.GetRaisePlateGCode();
                if (!raise_plate_gcode.HasValue)
                    return default;

                return new[]
                {
                    select_tool_gcode,
                    set_tool_temp_gcode,
                    set_tool_offset_gcode,
                    manual_calibration_position_gcode,
                    raise_plate_gcode
                };

            }, put_select_tool_cts.Token, true, wait_select_tool_cts.Token);
        }
    }

    public class PerformManualCalibrationViewModel : ManualCalibrationPhaseViewModel<PerformManualCalibrationViewModel>
    {
        private readonly ObservableAsPropertyHelper<Optional<FLUX_Temp>> _CurrentTemperature;
        [RemoteOutput(true, typeof(FluxTemperatureConverter))]
        public Optional<FLUX_Temp> CurrentTemperature => _CurrentTemperature.Value;

        private readonly ObservableAsPropertyHelper<double> _TemperaturePercentage;
        [RemoteOutput(true)]
        public double TemperaturePercentage => _TemperaturePercentage.Value;

        [RemoteContent(true)]
        public ISourceList<CmdButton> MoveUpButtons { get; private set; }
        [RemoteContent(true)]
        public ISourceList<CmdButton> MoveDownButtons { get; private set; }

        [RemoteContent(true, comparer:(nameof(ManualCalibrationItemViewModel.Position)))]
        public IObservableList<ManualCalibrationItemViewModel> ToolItems { get; }

        private readonly ObservableAsPropertyHelper<string> _AxisPosition;
        [RemoteOutput(true)]
        public string AxisPosition => _AxisPosition.Value;

        public PerformManualCalibrationViewModel(FluxViewModel flux, CalibrationViewModel calibration) : base(flux, calibration)
        {
            _CurrentTemperature = this.WhenAnyValue(v => v.SelectedTool)
               .Select(t =>
               {
                   if (!t.HasValue)
                       return Observable.Return(Optional<FLUX_Temp>.None);

                   var variable_store = Flux.ConnectionProvider.VariableStoreBase;
                   var tool_index = ArrayIndex.FromZeroBase(t.Value, variable_store);
                   var tool_key = Flux.ConnectionProvider.GetArrayUnit(m => m.TEMP_TOOL, tool_index);
                   if (!tool_key.HasValue)
                       return Observable.Return(Optional<FLUX_Temp>.None);

                   return Flux.ConnectionProvider.ObserveVariable(m => m.TEMP_TOOL, tool_key)
                       .ObservableOrDefault();
               })
               .Switch()
               .ToPropertyRC(this, v => v.CurrentTemperature);

            _TemperaturePercentage = this.WhenAnyValue(v => v.CurrentTemperature)
                .ConvertOr(t => t.Percentage, () => 0)
                .ToPropertyRC(this, v => v.TemperaturePercentage);

            SourceListRC.Create(this, v => v.MoveUpButtons);
            MoveUpButtons.Add(FindMoveButton(-1, false));
            MoveUpButtons.Add(FindMoveButton(-0.1, true));
            MoveUpButtons.Add(FindMoveButton(-0.01, true));
            MoveUpButtons.DisposeWith(Disposables);

            SourceListRC.Create(this, v => v.MoveDownButtons);
            MoveDownButtons.Add(FindMoveButton(1, false));
            MoveDownButtons.Add(FindMoveButton(0.1, true));
            MoveDownButtons.Add(FindMoveButton(0.01, true));
            MoveDownButtons.DisposeWith(Disposables);

            var move_up_not_executing = MoveUpButtons.Connect()
                .AddKey(b => b.Name)
                .Transform(m => m.Command, true)
                .TrueForAll(c => c.WhenAnyValue(v => v.IsExecuting), e => !e)
                .StartWith(false);

            var move_down_not_executing = MoveDownButtons.Connect()
                .AddKey(b => b.Name)
                .Transform(m => m.Command, true)
                .TrueForAll(c => c.WhenAnyValue(v => v.IsExecuting), e => !e)
                .StartWith(false);

            var not_executing = Observable.CombineLatest(
                move_up_not_executing, move_down_not_executing,
                (up_not_executing, down_not_executing) => up_not_executing && down_not_executing)
                .Delay(ne => Observable.Timer(TimeSpan.FromSeconds(ne ? 1 : 0)));

            ToolItems = Flux.SettingsProvider
                .WhenAnyValue(v => v.ExtrudersCount)
                .Select(e => FindCalibrationItems(e, not_executing))
                .AsObservableChangeSet()
                .AsObservableListRC(this);

            not_executing.SubscribeRC(async not_executing =>
                {
                    if (not_executing)
                    {
                        var offset = Calibration.Offsets.LookupOptional(SelectedTool);
                        if (!offset.HasValue)
                            return;

                        var bed_height_mm = 0.0;
                        if (Flux.ConnectionProvider.HasVariable(c => c.Z_BED_HEIGHT))
                        {
                            var z_bed_height = await Flux.ConnectionProvider.ReadVariableAsync(c => c.Z_BED_HEIGHT);
                            if (!z_bed_height.HasValue)
                                return;

                            bed_height_mm = z_bed_height.Value;
                        }

                        var axis_position = await Flux.ConnectionProvider.ReadVariableAsync(m => m.AXIS_POSITION);
                        if (!axis_position.HasValue)
                            return;

                        var z = axis_position.Value.Axes.Dictionary.Lookup('Z');
                        if (!z.HasValue)
                            return;

                        if (Flux.ConditionsProvider.FeelerGaugeCondition is not FeelerGaugeConditionViewModel feeler_gauge_condition)
                            return;

                        if (!feeler_gauge_condition.Value.HasValue)
                            return;

                        var feeler_gauge_mm = feeler_gauge_condition.Value.Value;

                        offset.Value.ModifyProbeOffset(p => new ProbeOffset(p.Key, p.X, p.Y, z.Value - bed_height_mm - feeler_gauge_mm));
                        Flux.SettingsProvider.UserSettings.PersistLocalSettings();
                    }
                }, this);

            var move_transform = Flux.ConnectionProvider.VariableStoreBase.MoveTransform;

            _AxisPosition = Flux.ConnectionProvider.ObserveVariable(m => m.AXIS_POSITION)
                .Convert(c => move_transform.TransformPosition(c, false))
                .ConvertOr(c => c.GetAxisPosition(), () => "")
                .ToPropertyRC(this, v => v.AxisPosition);
        }

        private CmdButton FindMoveButton(double distance, bool can_unsafe_cycle)
        {
            var variable_store = Flux.ConnectionProvider.VariableStoreBase;

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
                    if (temp.Value.Target.ValueOr(() => 0) <= 0)
                        return false;
                    if (temp.Value.Percentage < 85)
                        return default;
                    if (!can_safe_cycle)
                        return can_unsafe_cycle;
                    return true;
                })
                .ToOptional();

            return new CmdButton(Flux, $"moveZ;{(distance > 0 ? $"+{distance:0.00mm}" : $"{distance:0.00mm}")}", () => move_tool(distance), can_execute);

            async Task move_tool(double distance)
            {
                using var put_relative_movement_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await Flux.ConnectionProvider.ExecuteParamacroAsync(c => c.GetMovementGCode(('Z', distance, 500), variable_store.MoveTransform), put_relative_movement_cts.Token);
            }
        }
        private IEnumerable<ManualCalibrationItemViewModel> FindCalibrationItems(Optional<(ushort machine_extruders, ushort mixing_extruders)> extruders, IObservable<bool> not_executing)
        {
            if (!extruders.HasValue)
                yield break;

            for (ushort extruder = 0; extruder < extruders.Value.machine_extruders; extruder++)
                yield return new ManualCalibrationItemViewModel(Flux, this, extruder, not_executing);
        }
        public override IObservable<bool> GetCanCancelCalibration()
        {
            var move_up_not_executing = MoveUpButtons.Connect()
                .AddKey(b => b.Name)
                .Transform(m => m.Command, true)
                .TrueForAll(c => c.WhenAnyValue(v => v.IsExecuting), e => !e)
                .StartWith(false);

            var move_down_not_executing = MoveDownButtons.Connect()
                .AddKey(b => b.Name)
                .Transform(m => m.Command, true)
                .TrueForAll(c => c.WhenAnyValue(v => v.IsExecuting), e => !e)
                .StartWith(false);

            var not_executing = Observable.CombineLatest(
                move_up_not_executing, move_down_not_executing,
                (up_not_executing, down_not_executing) => up_not_executing && down_not_executing)
                .Delay(ne => Observable.Timer(TimeSpan.FromSeconds(ne ? 1 : 0)));

            var can_safe_stop = Flux.StatusProvider
              .WhenAnyValue(s => s.StatusEvaluation)
              .Select(s => s.CanSafeStop);

            return Observable.CombineLatest(can_safe_stop, not_executing, (ss, ne) => ss && ne);
        }
    }

    public class ManualCalibrationViewModel : FluxRoutableViewModel<ManualCalibrationViewModel>
    {
        public PrepareManualCalibrationViewModel PrepareManualCalibration { get; }
        public PerformManualCalibrationViewModel PerformManualCalibration { get; }

        private readonly ObservableAsPropertyHelper<IManualCalibrationPhaseViewModel> _ManualCalibrationPhase;
        [RemoteContent(true)]
        public IManualCalibrationPhaseViewModel ManualCalibrationPhase => _ManualCalibrationPhase.Value;

        public ManualCalibrationViewModel(FluxViewModel flux, CalibrationViewModel calibration) : base(flux)
        {
            PrepareManualCalibration = new PrepareManualCalibrationViewModel(Flux, calibration);
            PerformManualCalibration = new PerformManualCalibrationViewModel(Flux, calibration);

            PrepareManualCalibration.Initialize();
            PerformManualCalibration.Initialize();

            _ManualCalibrationPhase = PrepareManualCalibration
                .WhenAnyValue(v => v.HasSafeStart)
                .Select(GetManualCalibrationViewModel)
                .ToPropertyRC(this, h => h.ManualCalibrationPhase);
        }

        private IManualCalibrationPhaseViewModel GetManualCalibrationViewModel(bool safe_start)
        {
            if (!safe_start)
                return PrepareManualCalibration;
            return PerformManualCalibration;
        }
    }
}
