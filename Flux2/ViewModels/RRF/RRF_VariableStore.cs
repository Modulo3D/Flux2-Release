using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class RRF_VariableStore : FLUX_VariableStore<RRF_VariableStore>
    {
        public RRF_VariableStore(RRF_ConnectionProvider connection_provider)
        {
            try
            {
                var read_timeout = TimeSpan.FromSeconds(5);
                var connection = connection_provider.WhenAnyValue(v => v.Connection);
                var job = RRF_ModelBuilder.Create(connection, m => m.Job, read_timeout);
                var heat = RRF_ModelBuilder.Create(connection, m => m.Heat, read_timeout);
                var move = RRF_ModelBuilder.Create(connection, m => m.Move, read_timeout);
                var state = RRF_ModelBuilder.Create(connection, m => m.State, read_timeout);
                var tools = RRF_ModelBuilder.Create(connection, m => m.Tools, read_timeout);
                var queue = RRF_ModelBuilder.Create(connection, m => m.Queue, read_timeout);
                var input = RRF_ModelBuilder.Create(connection, m => m.Inputs, read_timeout);
                var global = RRF_ModelBuilder.Create(connection, m => m.Global, read_timeout);
                var storage = RRF_ModelBuilder.Create(connection, m => m.Storage, read_timeout);
                var sensors = RRF_ModelBuilder.Create(connection, m => m.Sensors, read_timeout);

                var storage_queue = RRF_ModelBuilder.Create(connection, m => m.Storage, m => m.Queue, read_timeout);
                var job_state_input = RRF_ModelBuilder.Create(connection, m => m.Job, m => m.State, m => m.Inputs, read_timeout);
                var job_global_storage_queue = RRF_ModelBuilder.Create(connection, m => m.Job, m => m.Global, m => m.Storage, m => m.Queue, read_timeout);

                QUEUE = RegisterVariable(queue.CreateVariable<Dictionary<QueuePosition, Guid>, Unit>("QUEUE", (c, m) => m.GetGuidDictionaryFromQueue()));
                PROCESS_STATUS = RegisterVariable(state.CreateVariable<FLUX_ProcessStatus, Unit>("PROCESS STATUS", (c, m) => m.GetProcessStatus()));
                STORAGE = RegisterVariable(storage.CreateVariable<Dictionary<Guid, Dictionary<BlockNumber, MCodePartProgram>>, Unit>("STORAGE", (c, m) => m.GetPartProgramDictionaryFromStorage()));

                PART_PROGRAM = RegisterVariable(job_global_storage_queue.CreateVariable<MCodePartProgram, Unit>("PART PROGRAM", (c, m) => m.GetPartProgram()));
                BLOCK_NUM = RegisterVariable(job_state_input.CreateVariable<LineNumber, Unit>("BLOCK NUM", (c, m) => m.GetBlockNum()));
                IS_HOMED = RegisterVariable(move.CreateVariable<bool, bool>("IS HOMED", (c, m) => m.IsHomed()));

                var axes = move.CreateArray(m => m.Axes.Convert(a => a.ToDictionary(a => a.Letter)));
                AXIS_POSITION = RegisterVariable(axes.Create<double, Unit>("AXIS_POSITION", VariableUnit.Range("X", "Y", "Z", "C"), (c, a) => a.MachinePosition));

                X_USER_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "x_user_offset", VariableUnit.Range(0, 4), false));
                Y_USER_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "y_user_offset", VariableUnit.Range(0, 4), false));
                Z_USER_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "z_user_offset", VariableUnit.Range(0, 4), false));
                X_PROBE_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "x_probe_offset", VariableUnit.Range(0, 4), false));
                Y_PROBE_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "y_probe_offset", VariableUnit.Range(0, 4), false));
                Z_PROBE_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "z_probe_offset", VariableUnit.Range(0, 4), false));
                QUEUE_POS = RegisterVariable(new RRF_VariableGlobalModel<QueuePosition>(connection, "queue_pos", false, new QueuePosition(-1), v => new QueuePosition((short)Convert.ChangeType(v, typeof(short)))));

                TOOL_CUR = RegisterVariable(state.CreateVariable<short, short>("TOOL CUR", (c, s) => s.CurrentTool));
                TOOL_NUM = RegisterVariable(tools.CreateVariable<ushort, ushort>("TOOL NUM", (c, t) => (ushort)t.Count));
                DEBUG = RegisterVariable(new RRF_VariableGlobalModel<bool>(connection, "debug", false));

                MCODE_RECOVERY = RegisterVariable(storage.CreateVariable<IFLUX_MCodeRecovery, Unit>("mcode_recovery", GetMCodeRecoveryAsync));

                var tool_array = state.CreateArray(s =>
                {
                    if (!s.CurrentTool.HasValue)
                        return default;
                    var tool_list = new Dictionary<VariableUnit, bool>();
                    for (ushort i = 0; i < 4; i++)
                        tool_list.Add($"{i}", s.CurrentTool.Value == i);
                    return tool_list.ToOptional();
                });

                MEM_TOOL_ON_TRAILER = RegisterVariable(tool_array.Create<bool, bool>("TOOL ON TRAILER", VariableUnit.Range(0, 4), (c, m) => m));
                MEM_TOOL_IN_MAGAZINE = RegisterVariable(tool_array.Create<bool, bool>("TOOL IN MAGAZINE", VariableUnit.Range(0, 4), (c, m) => !m));

                //MEM_TOOL_ON_TRAILER = RegisterVariable(new RRF_ArrayGlobalModel<bool>(connection, "tool_on_trailer", 4, true));
                //MEM_TOOL_IN_MAGAZINE = RegisterVariable(new RRF_ArrayGlobalModel<bool>(connection, "tool_in_magazine", 4, true));

                PURGE_POSITION = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "purge_position", VariableUnit.Range("X", "Y"), true));
                HOME_OFFSET = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "home_offset", VariableUnit.Range("X", "Y", "Z"), true));
                HOME_BUMP = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "home_bump", VariableUnit.Range("X", "Y", "Z"), true));
                QUEUE_SIZE = RegisterVariable(new RRF_VariableGlobalModel<ushort>(connection, "queue_size", true));

                X_MAGAZINE_POS = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "x_magazine_pos", VariableUnit.Range(0, 4), true));
                Y_MAGAZINE_POS = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "y_magazine_pos", VariableUnit.Range(0, 4), true));

                TEMP_PLATE = RegisterVariable(heat.CreateVariable<FLUX_Temp, double>("TEMP PLATE", (c, m) => m.GetPlateTemperature(), SetPlateTemperature));

                var heaters = heat.CreateArray(h => h.Heaters.Convert(h => h.Skip(1).ToDictionary(h => h.Sensor.Convert(s => s - 1))));
                TEMP_TOOL = RegisterVariable(heaters.Create<FLUX_Temp, double>("TEMP_TOOL", VariableUnit.Range(0, 4), (c, m) => m.GetTemperature(), SetToolTemperatureAsync));

                var endstops = sensors.CreateArray(s =>
                {
                    return new Dictionary<VariableUnit, bool>
                    {
                        { "X", s.Endstops.Value[0].Triggered.Value },
                        { "Y", s.Endstops.Value[1].Triggered.Value },
                        { "Z", s.GetProbeLevels(0).ConvertOr(l => l[0] > 0, () => false)},
                    }.ToOptional();
                });

                AXIS_ENDSTOP = RegisterVariable(endstops.Create<bool, bool>("AXIS ENDSTOP", VariableUnit.Range("X", "Y", "Z"), (c, triggered) => triggered));
                ENABLE_DRIVERS = RegisterVariable(axes.Create<bool, bool>("ENABLE DRIVERS", VariableUnit.Range("X", "Y", "Z", "C"), (c, m) => m.IsEnabledDriver(), EnableDriverAsync));

                var material_mixing = tools.CreateArray(tools =>
                {
                    var material_mixing = new Dictionary<VariableUnit, double>();
                    var ordered_tools = tools.OrderBy(t => t.Number.ValueOr(() => 0));
                    foreach (var tool in ordered_tools)
                    {
                        if (!tool.Mix.HasValue)
                            continue;
                        var tool_number = tool.Number.ValueOr(() => 0);
                        foreach (var mixing in tool.Mix.Value.Select((value, index) => (value, index)))
                            material_mixing.Add($"{tool_number + (mixing.index * tools.Count)}", mixing.value);
                    }
                    return material_mixing.ToOptional();
                });

                MATERIAL_ENABLED = RegisterVariable(material_mixing.Create<bool, bool>("MATERIAL ENABLED", VariableUnit.Range(0, 4), (c, m) => m == 1));

                var extruders = move.CreateArray(m => m.Extruders.Convert(e => e.Select((e, i) => (e, $"{i}")).ToDictionary(e => e.Item2, e => e.e)));
                EXTRUSIONS = RegisterVariable(extruders.Create<double, Unit>("EXTRUSION_SET", VariableUnit.Range(0, 4), (c, e) => e.Position));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private async Task<Optional<IFLUX_MCodeRecovery>> GetMCodeRecoveryAsync(RRF_Connection connection, FLUX_FileList files)
        {
            if (!files.Files.Any(f => f.Name == "resurrect.g"))
                return default;

            var get_resurrect_cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var resurrect_source = await connection.DownloadFileAsync(f => f.StoragePath, "resurrect.g", get_resurrect_cts.Token);
            if (!resurrect_source.HasValue)
                return default;

            var hold_file = resurrect_source.Match("M23 \"(.*)\"")
                .Convert(m => m.Groups.Lookup(1));
            if (!hold_file.HasValue)
                return default;

            var hold_mcode_partprogram = hold_file
                .Convert(m => MCodePartProgram.Parse(m.Value));
            if (!hold_mcode_partprogram.HasValue)
                return default;

            var hold_mcode_vm = connection.Flux.MCodes.AvaiableMCodes.Lookup(hold_mcode_partprogram.Value.MCodeGuid);
            if (!hold_mcode_vm.HasValue)
                return default;

            var hold_byte_offset = resurrect_source.Match("M26 S([+-]?[0-9]+)")
                .Convert(m => m.Groups.Lookup(1))
                .Convert(o => uint.TryParse(o.Value, out var offset) ? offset : default);
            if (!hold_byte_offset.HasValue)
                return default;

            var hold_tool = resurrect_source.Matches("T([+-]?[0-9]+) P([+-]?[0-9]+)")
                .Convert(m => m[1].Groups.Lookup(1))
                .Convert(t => short.TryParse(t.Value, out var tool) ? tool : default);
            if (!hold_tool.HasValue)
                return default;

            var mcode_recovery = new RRF_MCodeRecovery(
                hold_mcode_partprogram.Value.MCodeGuid,
                hold_byte_offset.Value,
                hold_tool.Value);

            if (!files.Files.Any(f => f.Name == mcode_recovery.FileName))
            {
                // upload file

                var hold_plate_temp = resurrect_source.Match("M140 P([+-]?[0-9]+) S([+-]?[0-9]*[.]?[0-9]+)")
                    .Convert(m => m.Groups.Lookup(2))
                    .Convert(m => m.Value);
                if (!hold_plate_temp.HasValue)
                    return default;

                var hold_e_pos = resurrect_source.Match("G92 E([+-]?[0-9]*[.]?[0-9]+)")
                    .Convert(m => m.Groups.Lookup(1))
                    .Convert(e => e.Value);
                if (!hold_e_pos.HasValue)
                    return default;

                var hold_moves = resurrect_source.Matches("G0 ([a-zA-Z0-9. ]*)");
                if (!hold_moves.HasValue)
                    return default;

                var hold_temps = resurrect_source.Matches("G10 P([+-]?[0-9]+) S([+-]?[0-9]*[.]?[0-9]+) R([+-]?[0-9]*[.]?[0-9]+)");
                if (!hold_temps.HasValue)
                    return default;

                var hold_feedrate = resurrect_source.Match("G1 F([+-]?[0-9]*[.]?[0-9]+) P([+-]?[0-9]+)")
                    .Convert(m => m.Groups.Lookup(1))
                    .Convert(m => m.Value);
                if (!hold_feedrate.HasValue)
                    return default;

                var put_resurrect_cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                if (!await connection.PutFileAsync(
                    f => f.StoragePath,
                    mcode_recovery.FileName,
                    put_resurrect_cts.Token,
                    get_recovery_lines().ToOptional()))
                    return default;

                IEnumerable<string> get_recovery_lines()
                {
                    yield return $"M140 S{hold_plate_temp}";
                    foreach (Match hold_temp in hold_temps.Value)
                        yield return hold_temp.Value;

                    yield return $"T{hold_tool}";
                    yield return "M98 P\"resurrect-prologue.g\"";
                    yield return $"G92 E{hold_e_pos}";

                    yield return "M116";

                    yield return $"M23 \"{hold_file}\"";
                    yield return $"M26 S{hold_byte_offset}";

                    foreach (Match hold_move in hold_moves.Value)
                        yield return hold_move.Value;

                    yield return $"G1 F{hold_feedrate} P0";

                    yield return "M24";
                }
            }

            return mcode_recovery;
        }

        public static Task<bool> SetPlateTemperature(RRF_Connection connection, double temperature)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return connection.PostGCodeAsync($"M140 S{temperature}", cts.Token);
        }
        private static Task<bool> EnableDriverAsync(RRF_Connection connection, bool enable, VariableUnit unit)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return connection.PostGCodeAsync($"{(enable ? "M17" : "M18")} {unit}", cts.Token);
        }
        public static Task<bool> SetToolTemperatureAsync(RRF_Connection connection, double temperature, VariableUnit unit)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return connection.PostGCodeAsync($"M104 T{unit} S{temperature}", cts.Token);
        }
    }
}
