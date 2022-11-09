using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public enum OSAI_ConnectionPhase
    {
        START_PHASE = 0,
        SELECT_BOOT_MODE = 1,
        WAIT_SYSTEM_UP = 2,
        ENABLE_AUX_ON = 3,
        RESET_PRINTER = 4,
        SELECT_PROCESS_MODE = 5,
        REFERENCE_AXES = 6,
        READ_FULL_MEMORY = 7,
        END_PHASE = 8
    }
    public class OSAI_GCodeGenerator : FLUX_GCodeGenerator<OSAI_Connection>
    {
        public OSAI_GCodeGenerator(OSAI_Connection connection) : base(connection)
        {
        }

        public override GCodeString AppendFile(string folder, string name, GCodeString source)
        {
            return GCodeString.Create(
                $"(OPN, 1, \"{Connection.CombinePaths(folder, name)}\", A, A)",
                source.Select(line => $"(WRT, 1, \"{line}\")").ToArray(),
                "(CLO, 1)");
        }
        public override GCodeString CreateFile(string folder, string name, GCodeString source)
        {
            return GCodeString.Create(
                $"(OPN, 1, \"{Connection.CombinePaths(folder, name)}\", A, W)",
                source.Select(line => $"(WRT, 1, \"{line}\")").ToArray(),
                "(CLO, 1)");
        }
        public GCodeString AppendFile(Union<(string folder, string name), GCodeVariable<string>> path, params Union<string, GCodeVariable<string>>[] source)
        {
            return new GCodeString(append_file());

            IEnumerable<GCodeString> append_file()
            {
                var _path = path.IsType1 ? 
                    $"\"{Connection.CombinePaths(path.Item1.folder, path.Item1.name)}\"" :
                    $"?{path.Item2}";

                yield return $"(OPN, 1, {_path}, A, A)";
                yield return $"(WRT, 1, {string.Join(", ", source.Select(s => s.IsType1 ? $"\"{s.Item}\"" : $"{s.Item2}"))})";
                yield return "(CLO, 1)";
            }
        }
        public GCodeString CreateFile(Union<(string folder, string name), GCodeVariable<string>> path, params Union<GCodeString, GCodeVariable<string>>[] source)
        {
            return new GCodeString(append_file());

            IEnumerable<GCodeString> append_file()
            {
                var _path = path.IsType1 ?
                    $"\"{Connection.CombinePaths(path.Item1.folder, path.Item1.name)}\"" :
                    $"?{path.Item2}";

                yield return $"(OPN, 1, {_path}, A, W)";
                yield return $"(WRT, 1, {string.Join(", ", source.Select(s => s.IsType1 ? $"\"{s.Item}\"" : $"{s.Item2}"))})";
                yield return "(CLO, 1)";
            }
        }

        public override GCodeString DeclareGlobalArray<T>(string[] array_name, out GCodeArray<T> array)
        {
            throw new NotImplementedException();
        }
        public override GCodeString DeclareGlobalVariable<T>(string variable_name, out GCodeVariable<T> variable)
        {
            throw new NotImplementedException();
        }
        public override GCodeString DeclareLocalArray<T>(string[] array_name, Union<GCodeVariable<T>, T> value, out GCodeArray<T> gcode_array)
        {
            gcode_array = new GCodeArray<T>()
            {
                Variables = array_name.Select(n =>
                {
                    DeclareLocalVariable(n, value, out var variable);
                    return variable;
                }).ToArray(),
            };
            return gcode_array.Foreach((v, i) => v.Declare);
        }
        public override GCodeString DeclareLocalVariable<T>(string variable_name, Union<GCodeVariable<T>, T> value, out GCodeVariable<T> gcode_variable)
        {
            gcode_variable = new GCodeVariable<T>()
            {
                Name = variable_name,
                Read = variable_name,
                Write = v => $"{variable_name} = {(typeof(T) == typeof(string) && !$"{v}".Contains('"') ? $"\"{v}\"" : $"{v}")}",
                Declare = $"{variable_name} = {(!value.IsType1 && typeof(T) == typeof(string) && !$"{value.Item2}".Contains('"') ? $"\"{value}\"" : value)}",
            };
            return gcode_variable.Declare;
        }

        public GCodeString DeclareGlobalArray<TRData, TWData>(OSAI_VARCODE varcode, uint address, Func<OSAI_VariableStore, IFLUX_Array<TRData, TWData>> get_array, out GCodeArray<TRData> gcode_array)
        {
            var array = Connection.GetArray(get_array);
            
            gcode_array = new GCodeArray<TRData>()
            {
                Variables = array.Variables.Items.Select((variable, i) =>
                {
                    if (variable is not IOSAI_AddressVariable address_variable)
                        throw new ArgumentException();
                    DeclareGlobalVariable<TRData>(varcode, (uint)(address + i), address_variable, out var gcode_variable);
                    return gcode_variable;
                }).ToArray()
            };

            return gcode_array.Foreach((v, i) => v.Declare);
        }

     
        public GCodeString DeclareGlobalVariable<T>(OSAI_VARCODE varcode, Union<uint, (uint index, uint lenght)> address, IOSAI_AddressVariable address_variable, out GCodeVariable<T> gcode_variable)
        {
            var variable_name = (varcode, address.Item) switch
            {
                (OSAI_VARCODE.E_CODE,  uint index) => $"E{index}",
                (OSAI_VARCODE.LS_CODE, uint index) => $"LS{address}",
                (OSAI_VARCODE.SC_CODE, (uint index, uint lenght)) => $"SC{address}.{lenght}",
                _ => default
            };

            if (string.IsNullOrEmpty(variable_name))
                throw new ArgumentException();

            var update = address_variable.Address switch
            {
                OSAI_IndexAddress index_address => index_address.VarCode switch
                {
                    OSAI_VARCODE.GD_CODE   => $"M4000[0, {index_address.Index}, 0, {address}]",
                    OSAI_VARCODE.MW_CODE   => $"M4000[1, {index_address.Index}, 0, {address}]",
                    OSAI_VARCODE.MD_CODE   => $"M4000[3, {index_address.Index}, 0, {address}]",
                    OSAI_VARCODE.GW_CODE   => $"M4000[4, {index_address.Index}, 0, {address}]",
                    OSAI_VARCODE.L_CODE    => $"M4000[6, {index_address.Index}, 0, {address}]",
                    OSAI_VARCODE.AA_CODE   => $"M4000[7, {index_address.Index}, 0, {address}]",
                    _ => default
                },
                OSAI_BitIndexAddress bit_index_address => bit_index_address.VarCode switch 
                {
                    OSAI_VARCODE.MW_CODE    => $"M4000[2, {bit_index_address.Index}, {bit_index_address.BitIndex}, {address}]",
                    OSAI_VARCODE.GW_CODE    => $"M4000[5, {bit_index_address.Index}, {bit_index_address.BitIndex}, {address}]",
                    _ => default
                },
                OSAI_NamedAddress named_address => $"{variable_name} = {named_address.Name}({named_address.Index})",
                _ => default
            };

            if (string.IsNullOrEmpty(update))
                throw new ArgumentException();

            var write = (Func<string, GCodeString>)(v =>
            {
                var write = address_variable.Address switch
                {
                    OSAI_IndexAddress index_address => index_address.VarCode switch
                    {
                        OSAI_VARCODE.GD_CODE    => $"M4001[0, {address_variable.Address.Index}, 0, {address}]",
                        OSAI_VARCODE.MW_CODE    => $"M4001[1, {address_variable.Address.Index}, 0, {address}]",
                        OSAI_VARCODE.MD_CODE    => $"M4001[3, {address_variable.Address.Index}, 0, {address}]",
                        OSAI_VARCODE.GW_CODE    => $"M4001[4, {address_variable.Address.Index}, 0, {address}]",
                        OSAI_VARCODE.L_CODE     => $"M4001[6, {address_variable.Address.Index}, 0, {address}]",
                        OSAI_VARCODE.AA_CODE    => $"M4001[7, {address_variable.Address.Index}, 0, {address}]",
                        _ => default
                    },
                    OSAI_BitIndexAddress bit_index_address => bit_index_address.VarCode switch
                    {
                        OSAI_VARCODE.MW_CODE => $"M4000[2, {bit_index_address.Index}, {bit_index_address.BitIndex}, {address}]",
                        OSAI_VARCODE.GW_CODE => $"M4000[5, {bit_index_address.Index}, {bit_index_address.BitIndex}, {address}]",
                        _ => default
                    },
                    OSAI_NamedAddress named_address => $"{variable_name} = {named_address.Name}({named_address.Index})",
                    _ => default

                };

                if (string.IsNullOrEmpty(write))
                    throw new ArgumentException();

                return GCodeString.Create($"{variable_name} = {(typeof(T) == typeof(string) && !$"{v}".Contains('"') ? $"\"{v}\"" : $"{v}")}", write);
            });

            gcode_variable = new GCodeVariable<T>()
            {
                Declare = default,
                Write   = write,
                Update  = update,
                Name    = variable_name,
                Read    = variable_name,
            };

            return gcode_variable.Declare;
        }
        public GCodeString DeclareGlobalVariable<TRData, TWData>(OSAI_VARCODE varcode, Union<uint, (uint index, uint lenght)> address, Func<OSAI_VariableStore, IFLUX_Variable<TRData, TWData>> get_variable, out GCodeVariable<TRData> gcode_variable)
        {
            var variable = Connection.GetVariable(get_variable);
            if (variable is not IOSAI_AddressVariable address_variable)
                throw new ArgumentException();
            return DeclareGlobalVariable(varcode, address, address_variable, out gcode_variable);
        }
        public GCodeString DeclareGlobalVariable(uint address, Func<OSAI_VariableStore, IFLUX_Variable<double, double>> get_variable, out GCodeVariable<double> gcode_variable)
        {
            return DeclareGlobalVariable(OSAI_VARCODE.E_CODE, address, get_variable, out gcode_variable);
        }
        public GCodeString DeclareGlobalVariable(uint address, Func<OSAI_VariableStore, IFLUX_Variable<string, string>> get_variable, out GCodeVariable<string> gcode_variable)
        {
            return DeclareGlobalVariable(OSAI_VARCODE.LS_CODE, address, get_variable, out gcode_variable);
        }
        public GCodeString DeclareGlobalVariable(uint address, uint lenght, Func<OSAI_VariableStore, IFLUX_Variable<string, string>> get_variable, out GCodeVariable<string> gcode_variable)
        {
            return DeclareGlobalVariable(OSAI_VARCODE.SC_CODE, (address, lenght), get_variable, out gcode_variable);
        }
        public GCodeString DeclareGlobalVariable(uint address, Func<OSAI_VariableStore, IFLUX_Variable<QueuePosition, QueuePosition>> get_variable, out GCodeVariable<QueuePosition> gcode_variable)
        {
            return DeclareGlobalVariable(OSAI_VARCODE.E_CODE, address, get_variable, out gcode_variable);
        }
        
        public GCodeString DeclareLocalVariable(uint address, Union<GCodeVariable<double>, double> value, out GCodeVariable<double> variable)
        {
            return DeclareLocalVariable($"E{address}", value, out variable);
        }
        public GCodeString DeclareLocalVariable(Union<uint, (uint index, uint lenght)> address, Union<GCodeVariable<string>, string> value, out GCodeVariable<string> variable)
        {
            var variable_name = address.Item switch
            {
                uint index                  => $"LS{index}",
                (uint index, uint lenght)   => $"SC{index}.{lenght}",
                _ => default
            };

            if (string.IsNullOrEmpty(variable_name))
                throw new ArgumentException();

            return DeclareLocalVariable(variable_name, value, out variable);
        }

        public GCodeString DeclareGlobalArray(uint address, Func<OSAI_VariableStore, IFLUX_Array<double, double>> get_variable, out GCodeArray<double> gcode_variable)
        {
            return DeclareGlobalArray(OSAI_VARCODE.E_CODE, address, get_variable, out gcode_variable);
        }
        public GCodeString DeclareGlobalArray(uint address, Func<OSAI_VariableStore, IFLUX_Array<string, string>> get_variable, out GCodeArray<string> gcode_variable)
        {
            return DeclareGlobalArray(OSAI_VARCODE.LS_CODE, address, get_variable, out gcode_variable);
        }

        public override GCodeString DeleteFile(string folder, string name)
        {
            return $"(DEL, {Connection.CombinePaths(folder, name)})";
        }

        public override GCodeString DeleteFile(GCodeVariable<string> path)
        {
            return $"(DEL, ?{path})";
        }

        public override GCodeString ExecuteParamacro(GCodeVariable<string> path)
        {
            return $"(CLS, ?{path})";
        }

        public override GCodeString ExecuteParamacro(string folder, string name)
        {
            return $"(CLS, {Connection.CombinePaths(folder, name)})";
        }

        public override GCodeString LogEvent<T>(Job job, T @event)
        {
            if (job.QueuePosition < 0)
                return default;

            var _event_path = Connection.CombinePaths(Connection.JobEventPath, $"{job.MCodeKey};{job.JobKey}");
            return GCodeString.Create(
                // save event
                ReadDateTime(out var date_time),
                DeclareLocalVariable(0, _event_path, out var event_path),
                AppendFile(event_path, $"{@event.ToEnumString()};", date_time));
        }

        public GCodeString AppendString(GCodeVariable<string> var, params Union<GCodeString, GCodeVariable<string>>[] strings)
        {
            return new GCodeString(append_string());

            IEnumerable<string> append_string()
            {
                foreach (var @string in strings)
                { 
                    var _string = @string.IsType1 ? 
                        @string.Item1.Select(l => $"(CAT, \"{l}\", {var})") :
                        new[] { $"(CAT, {@string.Item2}, {var})" };

                    foreach (var __string in _string)
                        yield return __string;
                }
            }
        }

        public GCodeString ReadDateTime(out GCodeVariable<string> date_time)
        {
            DeclareLocalVariable((0, 22), "", out date_time);
            return GCodeString.Create(
                $"(GDT, D2, T0, SC0.10, SC11.11)",
                "SC10.1 = \";\"");
        }

        public GCodeString ToString<T>(GCodeVariable<T> variable, uint address, out GCodeVariable<string> str_variable)
        {
            str_variable = default;
            if (typeof(T) == typeof(string))
                throw new ArgumentException();

            DeclareLocalVariable(address, "", out str_variable);
            return $"(N2S, \"%d\", {variable}, {str_variable})";
        }

        public override GCodeString LogExtrusion(Job job, ExtrusionKey e, GCodeVariable<double> v)
        {
            throw new NotImplementedException();
        }

        public override GCodeString RenameFile(string old_path, string new_path)
        {
            return $"(REN, {old_path}, {new_path})";
        }

        public override GCodeString RenameFile(GCodeVariable<string> old_path, GCodeVariable<string> new_path)
        {
            return $"(REN, ?{old_path}, ?{new_path})";
        }
    }
    public class OSAI_ConnectionProvider : FLUX_ConnectionProvider<OSAI_ConnectionProvider, OSAI_Connection, OSAI_MemoryBuffer, OSAI_VariableStore, OSAI_ConnectionPhase>
    {
        public FluxViewModel Flux { get; }
        public OSAI_ConnectionProvider(FluxViewModel flux) : base(flux,
            OSAI_ConnectionPhase.START_PHASE, OSAI_ConnectionPhase.END_PHASE, p => (int)p,
            c => new OSAI_VariableStore(c), c => new OSAI_Connection(flux, c), c => new OSAI_MemoryBuffer(c))
        {
            Flux = flux;
            Flux.MCodes.WhenAnyValue(c => c.OperatorUSB)
                .ConvertOr(o => o.AdvancedSettings, () => false)
                .DistinctUntilChanged()
                .Subscribe(debug => WriteVariableAsync(m => m.DEBUG, debug));

            var test = string.Join(Environment.NewLine, GenerateEndMCodeLines(0));
        }
        protected override async Task RollConnectionAsync(CancellationToken ct)
        {
            try
            {
                // PRELIMINARY PHASE
                switch (ConnectionPhase)
                {
                    // CONNECT TO PLC
                    case OSAI_ConnectionPhase.START_PHASE:
                        if (await Connection.ConnectAsync())
                            ConnectionPhase = OSAI_ConnectionPhase.SELECT_BOOT_MODE;
                        break;

                    // INITIALIZE BOOT MODE
                    case OSAI_ConnectionPhase.SELECT_BOOT_MODE:
                        var boot_mode = await WriteVariableAsync(m => m.BOOT_MODE, OSAI_BootMode.RUN);
                        if (boot_mode)
                            ConnectionPhase = OSAI_ConnectionPhase.WAIT_SYSTEM_UP;
                        break;

                    // INITIALIZE BOOT PHASE
                    case OSAI_ConnectionPhase.WAIT_SYSTEM_UP:
                        var boot_phase = await wait_system_up();
                        if (boot_phase)
                            ConnectionPhase = OSAI_ConnectionPhase.ENABLE_AUX_ON;
                        async Task<bool> wait_system_up()
                        {
                            return await Connection.WaitBootPhaseAsync(
                                phase => phase == OSAI_BootPhase.SYSTEM_UP_PHASE,
                                TimeSpan.FromSeconds(0),
                                TimeSpan.FromSeconds(1), 
                                TimeSpan.FromSeconds(0),
                                TimeSpan.FromSeconds(30));
                        }
                        break;

                    // ACTIVATE AUX
                    case OSAI_ConnectionPhase.ENABLE_AUX_ON:

                        var process = await ReadVariableAsync(m => m.PROCESS_STATUS);
                        if (process.HasValue && process.Value == FLUX_ProcessStatus.CYCLE)
                        {
                            ConnectionPhase = OSAI_ConnectionPhase.READ_FULL_MEMORY;
                            return;
                        }

                        var activate_aux = await WriteVariableAsync(m => m.AUX_ON, true);
                        if (activate_aux)
                            ConnectionPhase = OSAI_ConnectionPhase.RESET_PRINTER;
                        else
                            ConnectionPhase = OSAI_ConnectionPhase.ENABLE_AUX_ON;
                        break;

                    // RESETS PLC
                    case OSAI_ConnectionPhase.RESET_PRINTER:
                        var reset_plc = await Connection.StopAsync();
                        if (reset_plc)
                            ConnectionPhase = OSAI_ConnectionPhase.SELECT_PROCESS_MODE;
                        else
                            ConnectionPhase = OSAI_ConnectionPhase.ENABLE_AUX_ON;
                        break;

                    // SET PROCESS MODE TO AUTO
                    case OSAI_ConnectionPhase.SELECT_PROCESS_MODE:
                        var set_auto = await WriteVariableAsync(m => m.PROCESS_MODE, OSAI_ProcessMode.AUTO);
                        if (set_auto)
                            ConnectionPhase = OSAI_ConnectionPhase.REFERENCE_AXES;
                        else
                            ConnectionPhase = OSAI_ConnectionPhase.ENABLE_AUX_ON;
                        break;

                    // REFERENCE ALL AXIS
                    case OSAI_ConnectionPhase.REFERENCE_AXES:
                        var ref_axis = await Connection.AxesRefAsync('X', 'Y', 'Z', 'A');
                        if (ref_axis)
                            ConnectionPhase = OSAI_ConnectionPhase.READ_FULL_MEMORY;
                        else
                            ConnectionPhase = OSAI_ConnectionPhase.ENABLE_AUX_ON;
                        break;

                    case OSAI_ConnectionPhase.READ_FULL_MEMORY:
                        if(MemoryBuffer.HasFullMemoryRead)
                            ConnectionPhase = OSAI_ConnectionPhase.END_PHASE;
                        break;

                    case OSAI_ConnectionPhase.END_PHASE:
                        break;
                }

            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
            }
        }

        public override async Task<bool> ResetClampAsync()
        {
            if (!await WriteVariableAsync(c => c.OPEN_HEAD_CLAMP, true))
                return false; 
            if (!await WriteVariableAsync(c => c.TOOL_CUR, ArrayIndex.FromZeroBase(-1, VariableStoreBase)))
                return false;
            return true;
        }

        public override GCodeString GenerateStartMCodeLines()
        {
            return new[]
            {
                "; preprocessing",
                "(GTO, end_preprocess)",

                "M4140[0, 0]",
                "M4141[0, 0]",
                "M4104[0, 0, 0]",
                "M4999[0, 0, 0, 0]",

                "(CLS, MACRO\\probe_plate)",
                "(CLS, MACRO\\end_print)",
                "(CLS, MACRO\\home_printer)",
                "(CLS, MACRO\\change_tool, 0)",

                "G92 A0",
                "G1 X0 Y0 Z0 F1000",

                "\"end_preprocess\"",
                "(PAS)",
            };
        }
        public override GCodeString GenerateEndMCodeLines(Optional<ushort> queue_size)
        {
            var gcode = new OSAI_GCodeGenerator(Connection);
            return GCodeString.Create(

                gcode.DeclareGlobalVariable(0, c => c.QUEUE_POS, out var queue_pos),
                queue_pos.Update,
                gcode.ToString(queue_pos, 0, out var queue_pos_str),

                gcode.DeclareLocalVariable(1, $"{InnerQueuePath}\\", out var log_end),
                gcode.AppendString(log_end, queue_pos_str, (GCodeString)"\\end.g"),
                
                gcode.ExecuteParamacro(log_end),
                gcode.ExecuteParamacro(MacroPath, "end_print"));
        }
        public override InnerQueueGCodes GenerateInnerQueue(string folder, Job job, MCodePartProgramPreview part_program)
        {
            var gcode = new OSAI_GCodeGenerator(Connection);

            var end             = GCodeFile.Create(folder, "end.g",           gcode.LogEvent(job, FluxEventType.End));
            var pause           = GCodeFile.Create(folder, "pause.g",         gcode.LogEvent(job, FluxEventType.Pause));
            var cancel          = GCodeFile.Create(folder, "cancel.g",        gcode.LogEvent(job, FluxEventType.Cancel));
            var end_filament    = GCodeFile.Create(folder, "end_filament.g",  gcode.LogEvent(job, FluxEventType.EndFilament));

            var start           = GCodeFile.Create(folder, "start.g",        
                gcode.DeclareGlobalVariable(0, c => c.CUR_JOB, out var cur_job),
                gcode.DeclareGlobalArray(1, c => c.EXTR_KEY, out var extr_key),
                
                cur_job.Write($"{job.JobKey}"),
                extr_key.Foreach((v, i) => extrusion_key(i, k => v.Write($"{k}"))),
                gcode.LogEvent(job, part_program.IsRecovery ? FluxEventType.Resume : FluxEventType.Start));

            return new InnerQueueGCodes()
            {
                End = end,
                Start = start,
                Pause = pause,
                Cancel = cancel,
                EndFilament = end_filament,
            };

            Optional<GCodeString> extrusion_key(int position, Func<ExtrusionKey, GCodeString> func)
            {
                return Flux.Feeders.Feeders.Lookup((ushort)position)
                    .Convert(f => f.SelectedMaterial)
                    .Convert(m => m.ExtrusionKey)
                    .Convert(k => func(k));
            }
        }
        public override Optional<FLUX_MCodeRecovery> GetMCodeRecoveryFromSource(MCodeKey mcode, Optional<string> value)
        {
            try
            {
                if (!value.HasValue)
                    return default;

                using var string_reader = new StringReader(value.Value);

                // block nr
                var block_number = new BlockNumber(ulong.Parse(string_reader.ReadLine()), BlockType.Line);

                // tool
                var tool_index = ArrayIndex.FromArrayBase(ushort.Parse(string_reader.ReadLine()), VariableStore);

                // plate temps
                var plate_temperatures = new Dictionary<ArrayIndex, double>();
                var plate_count = GetArrayUnits(c => c.TEMP_PLATE).Count();
                for (ushort i = 0; i < plate_count; i++)
                    plate_temperatures.Add(ArrayIndex.FromZeroBase(i, VariableStore), double.Parse(string_reader.ReadLine()));

                // chamber temps
                var chamber_temperatures = new Dictionary<ArrayIndex, double>();
                var chamber_count = GetArrayUnits(c => c.TEMP_CHAMBER).Count();
                for (ushort i = 0; i < plate_count; i++)
                    chamber_temperatures.Add(ArrayIndex.FromZeroBase(i, VariableStore), double.Parse(string_reader.ReadLine()));

                // tool temps
                var extruders = Flux.SettingsProvider.ExtrudersCount;
                if (!extruders.HasValue)
                    return default;

                var tool_temperatures = new Dictionary<ArrayIndex, double>();
                for (ushort i = 0; i < extruders.Value.machine_extruders; i++)
                    tool_temperatures.Add(ArrayIndex.FromZeroBase(i, VariableStore), double.Parse(string_reader.ReadLine()));

                // pos
                var moves = new Dictionary<char, double>();
                moves.Add('X', double.Parse(string_reader.ReadLine()));
                moves.Add('Y', double.Parse(string_reader.ReadLine()));
                moves.Add('Z', double.Parse(string_reader.ReadLine()));
                moves.Add('A', double.Parse(string_reader.ReadLine()));

                // feedrate
                var feedrate = double.Parse(string_reader.ReadLine());

                return new FLUX_MCodeRecovery()
                {
                    MCodeKey = mcode,
                    AxisMove = new FLUX_AxisMove()
                    {
                        Relative = false,
                        AxisMove = moves,
                        Feedrate = 10000
                    },
                    Feedrate = feedrate,
                    ToolIndex = tool_index,
                    BlockNumber = block_number,
                    ToolTemperatures = tool_temperatures,
                    PlateTemperatures = plate_temperatures,
                    ChamberTemperatures = chamber_temperatures,
                };
            }
            catch (Exception ex)
            {
                return default;
            }
        }
    }
}
