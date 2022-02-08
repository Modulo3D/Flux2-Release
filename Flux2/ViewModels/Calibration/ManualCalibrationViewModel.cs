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
        public ISourceList<CmdButton> MoveButtons { get; }

        [RemoteContent(true)]
        public IObservableList<CmdButton> ToolButtons { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ExitCommand { get; }

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

            MoveButtons = new SourceList<CmdButton>();
            MoveButtons.Add(move_tool_button(1));
            MoveButtons.Add(move_tool_button(0.05));
            MoveButtons.Add(move_tool_button(-0.05));
            MoveButtons.Add(move_tool_button(-1));
            MoveButtons.DisposeWith(Disposables);

            MoveButtons.Connect()
                .AddKey(b => b.Name)
                .Transform(m => m.Command, true)
                .TrueForAll(c => c.IsExecuting, e => !e)
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

            ExitCommand = ReactiveCommand.CreateFromTask(ExitAsync, can_execute)
                .DisposeWith(Disposables);

            CmdButton move_tool_button(double distance)
            {
                var can_execute = Observable.CombineLatest(
                    this.WhenAnyValue(v => v.SelectedTool),
                    this.WhenAnyValue(v => v.TemperaturePercentage),
                    Flux.StatusProvider.IsIdle.ValueOrDefault(),
                    (tool, temp, idle) => tool.HasValue && Math.Abs(temp) > 95 && idle)
                    .ToOptional();

                var button = new CmdButton($"Z??{(distance > 0 ? $"+{distance:0.00mm}" : $"{distance:0.00}")}", () => move_tool(distance), can_execute);
                button.InitializeRemoteView();
                button.DisposeWith(Disposables);
                return button;

                async Task move_tool(double d)
                {
                    await Flux.ConnectionProvider.ExecuteParamacroAsync(c => c.GetRelativeZMovementGCode(d, 500), false);
                }
            }
        }

        private async Task ExitAsync()
        {
            var exit_ctk = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await Flux.ConnectionProvider.ExecuteParamacroAsync(c =>
            {
                var gcode = new List<string>();
                if (SelectedTool.HasValue)
                    gcode.AddRange(c.GetSetToolTemperatureGCode(SelectedTool.Value, 0));
                gcode.AddRange(c.GetLowerPlateGCode());
                gcode.AddRange(c.GetParkToolGCode());
                return gcode;
            }, true, exit_ctk.Token);

            Flux.Navigator.NavigateBack();
        }

        private IEnumerable<CmdButton> FindToolButtons(Optional<ushort> extruders)
        {
            if (!extruders.HasValue)
                yield break;

            for (ushort extruder = 0; extruder < extruders.Value; extruder++)
            {
                var e = extruder;

                var print_temp = Flux.Feeders.Feeders.Connect().WatchOptional(e)
                    .ConvertMany(f => f.ToolMaterial.WhenAnyValue(tm => tm.Document))
                    .Convert(tm => tm.PrintTemperature);

                var can_execute = Observable.CombineLatest(
                    print_temp,
                    this.WhenAnyValue(v => v.SelectedTool),
                    Flux.StatusProvider.IsIdle.ValueOrDefault(),
                    (temp, tool, idle) => temp.HasValue && idle && tool != e)
                    .ToOptional();

                var button = new CmdButton($"selectExtruder??{e + 1}", select_tool, can_execute);
                button.InitializeRemoteView();
                yield return button;

                async Task select_tool()
                {
                    var select_tool_ctk = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    await Flux.ConnectionProvider.ExecuteParamacroAsync(c =>
                    {
                        var gcode = new List<string>();
                        gcode.AddRange(c.GetSelectToolGCode(e));

                        var feeder = Flux.Feeders.Feeders.Lookup(e);
                        var tool_material = feeder.Convert(f => f.ToolMaterial.Document);
                        var print_temperature = tool_material.Convert(tm => tm.PrintTemperature);
                        if (print_temperature.HasValue)
                            gcode.AddRange(c.GetSetToolTemperatureGCode(e, print_temperature.Value));

                        gcode.AddRange(c.GetRaisePlateGCode());
                        return gcode;
                    }, true, select_tool_ctk.Token);
                }
            }
        }
    }
}
