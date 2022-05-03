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
    public class ManualCalibrationViewModel : FluxRoutableViewModel<ManualCalibrationViewModel>
    {
        private ObservableAsPropertyHelper<Optional<ushort>> _SelectedTool;
        [RemoteOutput(true)]
        public Optional<ushort> SelectedTool => _SelectedTool.Value;

        private ObservableAsPropertyHelper<Optional<double>> _CurrentTemperature;
        [RemoteOutput(true, typeof(TemperatureConverter))]
        public Optional<double> CurrentTemperature => _CurrentTemperature.Value;

        private ObservableAsPropertyHelper<double> _TemperaturePercentage;
        [RemoteOutput(true)]
        public double TemperaturePercentage => _TemperaturePercentage.Value;

        [RemoteContent(true)]
        public ISourceList<CmdButton> MoveUpButtons { get; }
        [RemoteContent(true)]
        public ISourceList<CmdButton> MoveDownButtons { get; }

        [RemoteContent(true)]
        public IObservableList<CmdButton> ToolButtons { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ExitCommand { get; }

        private ObservableAsPropertyHelper<string> _AxisPosition;
        [RemoteOutput(true)]
        public string AxisPosition => _AxisPosition.Value;

        public CalibrationViewModel Calibration { get; }

        public ManualCalibrationViewModel(CalibrationViewModel calibration) : base(calibration.Flux)
        {
            Calibration = calibration;

            _SelectedTool = Flux.ConnectionProvider.ObserveVariable(m => m.TOOL_CUR)
                .Convert(o => o.ToOptional(o => o > -1).Convert(o => (ushort)o))
                .ToProperty(this, v => v.SelectedTool)
                .DisposeWith(Disposables);

            _CurrentTemperature = this.WhenAnyValue(v => v.SelectedTool)
                .Select(t =>
                {
                    if (!t.HasValue)
                        return Observable.Return(Optional<double>.None);

                    var tool_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.TEMP_TOOL, t.Value);
                    if (!tool_key.HasValue)
                        return Observable.Return(Optional<double>.None);

                    return Flux.ConnectionProvider.ObserveVariable(m => m.TEMP_TOOL, tool_key.Value)
                        .Convert(t => t.Current);
                })
                .Switch()
                .ToProperty(this, v => v.CurrentTemperature)
                .DisposeWith(Disposables);

            _TemperaturePercentage = this.WhenAnyValue(v => v.SelectedTool)
                .Select(t =>
                {
                    if (!t.HasValue)
                        return Observable.Return(0.0);

                    var tool_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.TEMP_TOOL, t.Value);
                    if (!tool_key.HasValue)
                        return Observable.Return(0.0);

                    return Flux.ConnectionProvider.ObserveVariable(m => m.TEMP_TOOL, tool_key.Value)
                        .ConvertOr(t =>
                        {
                            if (t.Current > t.Target)
                                return 100;
                            return Math.Max(0, Math.Min(100, t.Current / t.Target * 100.0));
                        }, () => 0);
                })
                .Switch()
                .ToProperty(this, v => v.TemperaturePercentage)
                .DisposeWith(Disposables);

            ToolButtons = Flux.SettingsProvider
                .WhenAnyValue(v => v.ExtrudersCount)
                .Select(FindToolButtons)
                .ToObservableChangeSet()
                .AsObservableList()
                .DisposeWith(Disposables);

            var can_execute = Flux.StatusProvider.IsIdle
                .ValueOrDefault();

            MoveUpButtons = new SourceList<CmdButton>();
            MoveUpButtons.Add(move_tool_button(-1));
            MoveUpButtons.Add(move_tool_button(-0.1));
            MoveUpButtons.Add(move_tool_button(-0.01));
            MoveUpButtons.DisposeWith(Disposables);

            MoveDownButtons = new SourceList<CmdButton>();
            MoveDownButtons.Add(move_tool_button(1));
            MoveDownButtons.Add(move_tool_button(0.1));
            MoveDownButtons.Add(move_tool_button(0.01));
            MoveDownButtons.DisposeWith(Disposables);

            var move_up_executing = MoveUpButtons.Connect()
                .AddKey(b => b.Name)
                .Transform(m => m.Command, true)
                .TrueForAll(c => c.IsExecuting, e => !e)
                .StartWith(false);

            var move_down_executing = MoveDownButtons.Connect()
                .AddKey(b => b.Name)
                .Transform(m => m.Command, true)
                .TrueForAll(c => c.IsExecuting, e => !e)
                .StartWith(false);

            Observable.CombineLatest(
                move_up_executing, move_down_executing, 
                (up, down) => up || down)
                .Throttle(TimeSpan.FromSeconds(1))
                .Subscribe(async e =>
                {
                    if (e)
                    {

                        var offset = Calibration.Offsets.LookupOptional(SelectedTool);
                        if (!offset.HasValue)
                            return;

                        var z = await Flux.ConnectionProvider.ReadVariableAsync(m => m.AXIS_POSITION, "Z");
                        if (!z.HasValue)
                            return;

                        offset.Value.ModifyProbeOffset(p => new ProbeOffset(p.Key, p.X, p.Y, z.Value - 0.3));
                        Flux.SettingsProvider.UserSettings.PersistLocalSettings();
                    }
                })
                .DisposeWith(Disposables);

            _AxisPosition = Flux.ConnectionProvider.ObserveVariable(m => m.AXIS_POSITION)
                .QueryWhenChanged(p => string.Join(" ", p.KeyValues.Select(v => $"{v.Key}{v.Value:0.00}")))
                .ToProperty(this, v => v.AxisPosition);

            ExitCommand = ReactiveCommand.CreateFromTask(ExitAsync, can_execute)
                .DisposeWith(Disposables);

            CmdButton move_tool_button(double distance)
            {
                var can_execute = Observable.CombineLatest(
                    this.WhenAnyValue(v => v.SelectedTool),
                    this.WhenAnyValue(v => v.TemperaturePercentage),
                    (tool, temp) => tool.HasValue && Math.Abs(temp) > 85)
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
        }

        private async Task ExitAsync()
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
            }, put_exit_cts.Token, true, wait_exit_cts.Token);

            Flux.Navigator.NavigateBack();
        }

        private IEnumerable<CmdButton> FindToolButtons(Optional<ushort> extruders)
        {
            if (!extruders.HasValue)
                yield break;

            for (ushort extruder = 0; extruder < extruders.Value; extruder++)
            {
                var e = extruder;

                var print_temp = Flux.Feeders.Feeders.Connect()
                    .WatchOptional(e)
                    .ConvertMany(f => f.WhenAnyValue(f => f.SelectedToolMaterial))
                    .ConvertMany(tm => tm.WhenAnyValue(tm => tm.Document))
                    .Convert(tm => tm.PrintTemperature);

                var tool_offset = Flux.Calibration.Offsets.Connect()
                    .WatchOptional(e)
                    .ConvertMany(o => o.WhenAnyValue(o => o.FluxOffset));

                var can_execute = Observable.CombineLatest(
                    print_temp,
                    tool_offset,
                    this.WhenAnyValue(v => v.SelectedTool),
                    Flux.StatusProvider.IsIdle.ValueOrDefault(),
                    (temp, offset, tool, idle) => temp.HasValue && offset.HasValue && idle && tool != e)
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
                        .Convert(tm => tm.PrintTemperature);
                    if (!print_temperature.HasValue)
                        return;

                    var offset = Flux.Calibration.Offsets.Lookup(e);
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
}
