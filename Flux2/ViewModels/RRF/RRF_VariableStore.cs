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
                var connection = connection_provider.WhenAnyValue(v => v.Connection);
                var heat = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.Heat));
                var input = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.Inputs));
                var move = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.Move));
                var sensors = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.Sensors));
                var state = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.State));
                var tools = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.Tools));
                var global = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.Global));
                var storage = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.Storage));
                var queue = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.Queue));
                var job = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.Job));

                var state_input = RRF_StateBuilder.Create(connection,
                    m => Observable.CombineLatest(state.GetState(m), input.GetState(m),
                    (s, i) => s.Convert(s => i.Convert(i => (s, i)))));

                var storage_queue = RRF_StateBuilder.Create(connection,
                    m => Observable.CombineLatest(storage.GetState(m), queue.GetState(m),
                    (s, q) => s.Convert(s => q.Convert(q => (s, q)))));

                var job_global_storage_queue = RRF_StateBuilder.Create(connection,
                    m => Observable.CombineLatest(job.GetState(m), global.GetState(m), storage.GetState(m), queue.GetState(m),
                    (job, global, storage, queue) => job.Convert(job => global.Convert(global => storage.Convert(storage => queue.Convert(queue => (job, global, storage, queue)))))));

                QUEUE = RegisterVariable(queue.CreateVariable<Dictionary<QueuePosition, Guid>, Unit>("QUEUE", (c, m) => m.GetGuidDictionaryFromQueue()));
                PROCESS_STATUS = RegisterVariable(state.CreateVariable<FLUX_ProcessStatus, Unit>("PROCESS STATUS", (c, m) => m.GetProcessStatus()));
                STORAGE = RegisterVariable(storage.CreateVariable<Dictionary<Guid, Dictionary<BlockNumber, MCodePartProgram>>, Unit>("STORAGE", (c, m) => m.GetPartProgramDictionaryFromStorage()));

                PART_PROGRAM = RegisterVariable(job_global_storage_queue.CreateVariable<MCodePartProgram, Unit>("PART PROGRAM", (c, m) => m.GetPartProgram()));
                BLOCK_NUM = RegisterVariable(state_input.CreateVariable<LineNumber, Unit>("BLOCK NUM", (c, m) => m.GetBlockNum()));
                IS_HOMED = RegisterVariable(move.CreateVariable<bool, bool>("IS HOMED", (c, m) => m.IsHomed()));

                var axes = move.CreateArray(m => m.Axes.Convert(a => a.ToDictionary(a => a.Letter)));
                AXIS_POSITION = RegisterVariable(axes.Create<double, Unit>("AXIS_POSITION", new VariableUnit[] { "X", "Y", "Z", "C" }, (c, a) => a.MachinePosition));

                X_USER_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection_provider.Flux, connection, "x_user_offset", VariableUnit.Range(0, 4), false));
                Y_USER_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection_provider.Flux, connection, "y_user_offset", VariableUnit.Range(0, 4), false));
                Z_USER_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection_provider.Flux, connection, "z_user_offset", VariableUnit.Range(0, 4), false));
                X_PROBE_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection_provider.Flux, connection, "x_probe_offset", VariableUnit.Range(0, 4), false));
                Y_PROBE_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection_provider.Flux, connection, "y_probe_offset", VariableUnit.Range(0, 4), false));
                Z_PROBE_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection_provider.Flux, connection, "z_probe_offset", VariableUnit.Range(0, 4), false));
                QUEUE_POS = RegisterVariable(new RRF_VariableGlobalModel<QueuePosition>(connection_provider.Flux, connection, "queue_pos", false, v => new QueuePosition((short)Convert.ChangeType(v, typeof(short)))));

                TOOL_CUR = RegisterVariable(state.CreateVariable<short, short>("TOOL CUR", (c, s) => s.CurrentTool));
                TOOL_NUM = RegisterVariable(tools.CreateVariable<ushort, ushort>("TOOL NUM", (c, t) => (ushort)t.Count));
                DEBUG = RegisterVariable(new RRF_VariableGlobalModel<bool>(connection_provider.Flux, connection, "debug", false));

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

                PURGE_POSITION = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection_provider.Flux, connection, "purge_position", new VariableUnit[] { "X", "Y" }, true));
                HOME_OFFSET = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection_provider.Flux, connection, "home_offset", new VariableUnit[] { "X", "Y", "Z" }, true));
                HOME_BUMP = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection_provider.Flux, connection, "home_bump", new VariableUnit[] { "X", "Y", "Z" }, true));
                QUEUE_SIZE = RegisterVariable(new RRF_VariableGlobalModel<ushort>(connection_provider.Flux, connection, "queue_size", true));

                X_MAGAZINE_POS = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection_provider.Flux, connection, "x_magazine_pos", VariableUnit.Range(0, 4), true));
                Y_MAGAZINE_POS = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection_provider.Flux, connection, "y_magazine_pos", VariableUnit.Range(0, 4), true));

                TEMP_PLATE = RegisterVariable(heat.CreateVariable<FLUX_Temp, double>("TEMP PLATE", (c, m) => m.GetPlateTemperature(), SetPlateTemperature));

                var heaters = heat.CreateArray(h => h.Heaters.Convert(h => h.Skip(1).ToDictionary(h => h.Sensor.Convert(s => s - 1))));
                TEMP_TOOL = RegisterVariable(heaters.Create<FLUX_Temp, double>("TEMP_TOOL", VariableUnit.Range(0, 4), (c, m) => m.GetTemperature(), SetToolTemperatureAsync));

                var endstops = sensors.CreateArray(s =>
                {
                    var endstop_units = new VariableUnit[] { "X", "Y", "Z", "U" };

                    var position = 0;
                    var endstops = new Dictionary<VariableUnit, bool>();
                    if (s.Endstops.HasValue)
                    {
                        foreach (var endstop in s.Endstops.Value)
                        {
                            if (endstop != null && endstop.Triggered.HasValue)
                                endstops.Add(endstop_units[position], endstop.Triggered.Value);
                            position++;
                        }
                    }
                    var z_probe = s.GetProbeLevels(0).Convert(l => l[0] > 0);
                    if (z_probe.HasValue)
                        endstops.Add("Z", z_probe.Value);
                    return endstops.ToOptional();
                });


                AXIS_ENDSTOP = RegisterVariable(endstops.Create<bool, bool>("AXIS ENDSTOP", new VariableUnit[] { "X", "Y", "Z", "U" }, (c, triggered) => triggered));
                ENABLE_DRIVERS = RegisterVariable(axes.Create<bool, bool>("ENABLE DRIVERS", new VariableUnit[] { "X", "Y", "Z", "U", "C" }, (c, m) => m.IsEnabledDriver(), EnableDriverAsync));
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
