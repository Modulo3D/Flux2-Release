using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using OSAI;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class OSAI_VariableStore : FLUX_VariableStore<OSAI_VariableStore, OSAI_ConnectionProvider>
    {
        public IFLUX_Variable<OSAI_ProcessMode, OSAI_ProcessMode> PROCESS_MODE { get; }
        public IFLUX_Variable<OSAI_BootMode, OSAI_BootMode> BOOT_MODE { get; }
        public IFLUX_Variable<OSAI_BootPhase, Unit> BOOT_PHASE { get; }
        public IFLUX_Variable<bool, bool> AUX_ON { get; }

        public IFLUX_Array<double, double> HOME_OFFSET { get; }
        public IFLUX_Variable<double, double> Y_SAFE_MAX { get; }
        public IFLUX_Array<double, double> PURGE_POSITION { get; }
        public IFLUX_Array<double, double> TOOL_PROBE_POSITION { get; }
        public IFLUX_Variable<double, double> Z_TOOL_CORRECTION { get; }
        public IFLUX_Array<double, double> PLATE_PROBE_POSITION { get; }

        public OSAI_VariableStore(OSAI_ConnectionProvider connection_provider) : base(connection_provider)
        {
            var bump_unit = VariableUnit.Range(0, "x", "y");
            var drivers_unit = VariableUnit.Range(0, "xyz", "e");
            var lock_unit = VariableUnit.Range(0, "chamber", "top");
            var pid_unit = VariableUnit.Range(0, "kp", "ki", "kd");
            var axis_unit = VariableUnit.Range(0, "x", "y", "z", "e");
            var probe_unit = VariableUnit.Range(0, "plate_z", "tool_z", "tool_xy");

            EXTRUSIONS = RegisterArray(new OSAI_ArrayNamedDouble(connection_provider, "EXTRUSIONS", 4, new OSAI_NamedAddress("!EXTR"), FluxMemReadPriority.MEDIUM));


            QUEUE_POS = RegisterVariable(new OSAI_VariableNamedQueuePosition(connection_provider, "QUEUE_POS", new OSAI_NamedAddress("!Q_POS"), FluxMemReadPriority.MEDIUM));
            
            QUEUE = RegisterVariable(new OSAI_VariableGP<Dictionary<QueuePosition, FluxJob>, Unit>(connection_provider, "QUEUE", FluxMemReadPriority.MEDIUM, GetQueueAsync));
            STORAGE = RegisterVariable(new OSAI_VariableGP<Dictionary<Guid, Dictionary<BlockNumber, MCodePartProgram>>, Unit>(connection_provider, "STORAGE", FluxMemReadPriority.MEDIUM, GetStorageAsync));

            PROCESS_STATUS  = RegisterVariable(new OSAI_VariableGP<FLUX_ProcessStatus, Unit>(connection_provider, "PROCESS_STATUS", FluxMemReadPriority.ULTRAHIGH, GetProcessStatusAsync));
            PROCESS_MODE    = RegisterVariable(new OSAI_VariableGP<OSAI_ProcessMode, OSAI_ProcessMode>(connection_provider, "PROCESS_MODE", FluxMemReadPriority.ULTRAHIGH, GetProcessModeAsync, SetProcessModeAsync));

            BOOT_PHASE      = RegisterVariable(new OSAI_VariableGP<OSAI_BootPhase, Unit>(connection_provider, "BOOT_PHASE", FluxMemReadPriority.ULTRAHIGH, GetBootPhaseAsync));

            BOOT_MODE       = RegisterVariable(new OSAI_VariableGP<OSAI_BootMode, OSAI_BootMode>(connection_provider, "BOOT_MODE", FluxMemReadPriority.ULTRAHIGH, write_func: SetBootModeAsync));
            PART_PROGRAM    = RegisterVariable(new OSAI_VariableGP<MCodePartProgram, Unit>(connection_provider, "PART_PROGRAM", FluxMemReadPriority.HIGH, GetPartProgramAsync));
        
            PROGRESS = RegisterVariable(new OSAI_VariableGP<ParamacroProgress, Unit>(connection_provider, "PROGRESS", FluxMemReadPriority.ULTRAHIGH, c => Task.FromResult(new ParamacroProgress("", 70).ToOptional())));

            MCODE_RECOVERY = RegisterVariable(new OSAI_VariableGP<IFLUX_MCodeRecovery, Unit>(connection_provider, "MCODE_RECOVERY", FluxMemReadPriority.MEDIUM, GetMCodeRecoveryAsync));

            IS_HOMED = RegisterVariable(new OSAI_VariableNamedBool(connection_provider, "IS HOMED", new OSAI_NamedAddress("!IS_HOMED"), FluxMemReadPriority.HIGH));
            IS_HOMING = RegisterVariable(new OSAI_VariableNamedBool(connection_provider, "IS HOMING", new OSAI_NamedAddress("!IS_HOMING"), FluxMemReadPriority.HIGH));
            IN_CHANGE = RegisterVariable(new OSAI_VariableNamedBool(connection_provider, "IN CHANGE", new OSAI_NamedAddress("!IN_CHANGE"), FluxMemReadPriority.HIGH));

            WATCH_VACUUM = RegisterVariable(new OSAI_VariableNamedBool(connection_provider, "WATCH VACUUM", new OSAI_NamedAddress("!WTC_VACUUM"), FluxMemReadPriority.ULTRALOW));
            Z_BED_HEIGHT = RegisterVariable(new OSAI_VariableNamedDouble(connection_provider, "PLATE HEIGHT Z", new OSAI_NamedAddress("!Z_PLATE_H"), FluxMemReadPriority.ULTRALOW));

            X_USER_OFFSET_T = RegisterArray(new OSAI_ArrayNamedDouble(connection_provider, "X USER OFFSET TOOL", 4, new OSAI_NamedAddress("!X_USR_OF_T"), FluxMemReadPriority.LOW));
            Y_USER_OFFSET_T = RegisterArray(new OSAI_ArrayNamedDouble(connection_provider, "Y USER OFFSET TOOL", 4, new OSAI_NamedAddress("!Y_USR_OF_T"), FluxMemReadPriority.LOW));
            Z_USER_OFFSET_T = RegisterArray(new OSAI_ArrayNamedDouble(connection_provider, "Z USER OFFSET TOOL", 4, new OSAI_NamedAddress("!Z_USR_OF_T"), FluxMemReadPriority.LOW));

            X_PROBE_OFFSET_T = RegisterArray(new OSAI_ArrayNamedDouble(connection_provider, "X PROBE OFFSET TOOL", 4, new OSAI_NamedAddress("!X_PRB_OF_T"), FluxMemReadPriority.LOW));
            Y_PROBE_OFFSET_T = RegisterArray(new OSAI_ArrayNamedDouble(connection_provider, "Y PROBE OFFSET TOOL", 4, new OSAI_NamedAddress("!Y_PRB_OF_T"), FluxMemReadPriority.LOW));
            Z_PROBE_OFFSET_T = RegisterArray(new OSAI_ArrayNamedDouble(connection_provider, "Z PROBE OFFSET TOOL", 4, new OSAI_NamedAddress("!Z_PRB_OF_T"), FluxMemReadPriority.LOW));

            // GW VARIABLES                                                                                                                                                                                                                                                                              
            TOOL_CUR = RegisterVariable(new OSAI_VariableShort(connection_provider, "TOOL CURRENT", new OSAI_IndexAddress(OSAI_VARCODE.GW_CODE, 0), FluxMemReadPriority.ULTRALOW));
            TOOL_NUM = RegisterVariable(new OSAI_VariableWord(connection_provider, "TOOL NUMBER", new OSAI_IndexAddress(OSAI_VARCODE.GW_CODE, 1), FluxMemReadPriority.ULTRALOW));
            DEBUG = RegisterVariable(new OSAI_VariableBool(connection_provider, "DEBUG", new OSAI_BitIndexAddress(OSAI_VARCODE.GW_CODE, 2, 0), FluxMemReadPriority.HIGH));
            KEEP_CHAMBER = RegisterVariable(new OSAI_VariableBool(connection_provider, "KEEP CHAMBER WARM", new OSAI_BitIndexAddress(OSAI_VARCODE.GW_CODE, 3, 0), FluxMemReadPriority.HIGH));
            KEEP_TOOL = RegisterVariable(new OSAI_VariableBool(connection_provider, "KEEP TOOL WARM", new OSAI_BitIndexAddress(OSAI_VARCODE.GW_CODE, 4, 0), FluxMemReadPriority.HIGH));
            AUTO_FAN = RegisterVariable(new OSAI_VariableBool(connection_provider, "AUTO FAN", new OSAI_BitIndexAddress(OSAI_VARCODE.GW_CODE, 6, 0), FluxMemReadPriority.ULTRALOW));
            QUEUE_SIZE = RegisterVariable(new OSAI_VariableWord(connection_provider, "QUEUE SIZE", new OSAI_IndexAddress(OSAI_VARCODE.GW_CODE, 7), FluxMemReadPriority.ULTRALOW));

            MEM_TOOL_ON_TRAILER = RegisterArray(new OSAI_ArrayBool(connection_provider, "TOOL ON TRAILER", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.GW_CODE, 100, 0), FluxMemReadPriority.HIGH));
            MEM_TOOL_IN_MAGAZINE = RegisterArray(new OSAI_ArrayBool(connection_provider, "TOOL IN MAGAZINE", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.GW_CODE, 101, 0), FluxMemReadPriority.HIGH));

            // GD VARIABLES                                                                                                                                                                                                                                                                
            TEMP_WAIT = RegisterArray(new OSAI_ArrayDouble(connection_provider, "TEMP WAIT", 3, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 0), FluxMemReadPriority.ULTRALOW));
            PID_TOOL = RegisterArray(new OSAI_ArrayDouble(connection_provider, "PID TOOL", 3, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 3), FluxMemReadPriority.ULTRALOW, custom_unit: pid_unit));
            PID_CHAMBER = RegisterArray(new OSAI_ArrayDouble(connection_provider, "PID CHAMBER", 3, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 6), FluxMemReadPriority.ULTRALOW, custom_unit: pid_unit));
            PID_PLATE = RegisterArray(new OSAI_ArrayDouble(connection_provider, "PID PLATE", 3, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 9), FluxMemReadPriority.ULTRALOW, custom_unit: pid_unit));
            PID_RANGE = RegisterArray(new OSAI_ArrayDouble(connection_provider, "PID RANGE", 3, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 12), FluxMemReadPriority.ULTRALOW));
            TEMP_WINDOW = RegisterArray(new OSAI_ArrayDouble(connection_provider, "TEMP WINDOW", 3, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 15), FluxMemReadPriority.ULTRALOW));

            FAN_ENABLE = RegisterArray(new OSAI_ArrayDouble(connection_provider, "FAN ENABLE", 2, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 18), FluxMemReadPriority.ULTRALOW));
            PURGE_POSITION = RegisterArray(new OSAI_ArrayDouble(connection_provider, "PURGE POSITION", 2, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 21), FluxMemReadPriority.ULTRALOW, custom_unit: axis_unit));
            Y_SAFE_MAX = RegisterVariable(new OSAI_VariableDouble(connection_provider, "Y SAFE MAX", new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 23), FluxMemReadPriority.ULTRALOW));
            Z_PROBE_MIN = RegisterVariable(new OSAI_VariableDouble(connection_provider, "Z PROBE MIN", new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 24), FluxMemReadPriority.ULTRALOW));
            Z_TOOL_CORRECTION = RegisterVariable(new OSAI_VariableDouble(connection_provider, "Z TOOL COR", new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 25), FluxMemReadPriority.ULTRALOW));
            TOOL_PROBE_POSITION = RegisterArray(new OSAI_ArrayDouble(connection_provider, "TOOL PROBE POSITION", 2, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 28), FluxMemReadPriority.ULTRALOW, custom_unit: axis_unit));
            HOME_OFFSET = RegisterArray(new OSAI_ArrayDouble(connection_provider, "HOME OFFSET", 2, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 32), FluxMemReadPriority.ULTRALOW, custom_unit: axis_unit));
            PLATE_PROBE_POSITION = RegisterArray(new OSAI_ArrayDouble(connection_provider, "PLATE PROBE POSITION", 2, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 34), FluxMemReadPriority.ULTRALOW, custom_unit: axis_unit));

            PRESSURE_LEVEL = RegisterVariable(new OSAI_VariableDouble(connection_provider, "PRESSURE LEVEL", new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 36), FluxMemReadPriority.ULTRALOW));
            VACUUM_LEVEL = RegisterVariable(new OSAI_VariableDouble(connection_provider, "VACUUM LEVEL", new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 37), FluxMemReadPriority.ULTRALOW));

            TOOL_OFF_TIME = RegisterVariable(new OSAI_VariableDouble(connection_provider, "TOOL OFF TIMER", new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 38), FluxMemReadPriority.ULTRALOW));
            CHAMBER_OFF_TIME = RegisterVariable(new OSAI_VariableDouble(connection_provider, "CHAMBER OFF TIMER", new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 39), FluxMemReadPriority.ULTRALOW));

            // L VARIABLES                                                                                                                                                                                                                                                                         
            X_MAGAZINE_POS = RegisterArray(new OSAI_ArrayDouble(connection_provider, "X MAGAZINE POSITION", 4, new OSAI_IndexAddress(OSAI_VARCODE.L_CODE, 1), FluxMemReadPriority.ULTRALOW));
            Y_MAGAZINE_POS = RegisterArray(new OSAI_ArrayDouble(connection_provider, "Y MAGAZINE POSITION", 4, new OSAI_IndexAddress(OSAI_VARCODE.L_CODE, 18), FluxMemReadPriority.ULTRALOW));

            // STRINGS                                                                                                                                                                                                                                                                               
            TEMP_CHAMBER = RegisterArray(new OSAI_ArrayTemp(connection_provider, "TEMP CHAMBER", 1, new OSAI_IndexAddress(OSAI_VARCODE.AA_CODE, 0), FluxMemReadPriority.ULTRAHIGH, (i, t) => $"M4140[{t}, 0]"));
            TEMP_PLATE = RegisterVariable(new OSAI_VariableTemp(connection_provider, "TEMP PLATE", new OSAI_IndexAddress(OSAI_VARCODE.AA_CODE, 1), FluxMemReadPriority.ULTRAHIGH, t => $"M4141[{t}, 0]"));
            TEMP_TOOL = RegisterArray(new OSAI_ArrayTemp(connection_provider, "TEMP TOOL", 4, new OSAI_IndexAddress(OSAI_VARCODE.AA_CODE, 2), FluxMemReadPriority.ULTRAHIGH, (i, t) => $"M4104[{i + 1}, {t}, 0]"));

            // MW VARIABLES                                                                                                                                                                                                                                                                             
            RUNNING_MACRO = RegisterVariable(new OSAI_VariableMacro(connection_provider, "RUNNING MACRO", new OSAI_IndexAddress(OSAI_VARCODE.MW_CODE, 11021)));
            RUNNING_MCODE = RegisterVariable(new OSAI_VariableMCode(connection_provider, "RUNNING MCODE", new OSAI_IndexAddress(OSAI_VARCODE.MW_CODE, 9999)));
            RUNNING_GCODE = RegisterVariable(new OSAI_VariableGCode(connection_provider, "RUNNING GCODE", new OSAI_IndexAddress(OSAI_VARCODE.MW_CODE, 11022)));

            // INPUTS                                                                                                                                                                                                                                                                             
            LOCK_CLOSED = RegisterArray(new OSAI_ArrayBool(connection_provider, "LOCK CLOSED", 2, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10201, 0), FluxMemReadPriority.ULTRAHIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 3, 1), lock_unit));
            TOOL_ON_TRAILER = RegisterArray(new OSAI_ArrayBool(connection_provider, "TOOL ON TRAILER", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10202, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 3, 4)));

            FILAMENT_BEFORE_GEAR = RegisterArray(new OSAI_ArrayBool(connection_provider, "WIRE PRESENCE BEFORE GEAR", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10204, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 4, 0)));
            FILAMENT_AFTER_GEAR = RegisterArray(new OSAI_ArrayBool(connection_provider, "WIRE PRESENCE AFTER GEAR", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10205, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 4, 4)));
            FILAMENT_ON_HEAD = RegisterArray(new OSAI_ArrayBool(connection_provider, "WIRE PRESENCE ON HEAD", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10206, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 4, 8)));
            TOOL_IN_MAGAZINE = RegisterArray(new OSAI_ArrayBool(connection_provider, "TOOL IN MAGAZINE", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10207, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 5, 0)));

            AXIS_ENDSTOP = RegisterArray(new OSAI_ArrayBool(connection_provider, "AXIS ENDSTOP", 3, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10209, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 6, 0), axis_unit));
            AXIS_PROBE = RegisterArray(new OSAI_ArrayBool(connection_provider, "AXIS PROBE", 3, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10210, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 6, 3), probe_unit));

            DRIVER_EMERGENCY = RegisterVariable(new OSAI_VariableBool(connection_provider, "DRIVER EMERGENCY", new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10211, 0), FluxMemReadPriority.MEDIUM, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 6, 6)));
            PRESSURE_PRESENCE = RegisterVariable(new OSAI_VariablePressure<AnalogSensors.PSE540>(connection_provider, "PRESSURE PRESENCE", new OSAI_IndexAddress(OSAI_VARCODE.MW_CODE, 10212), FluxMemReadPriority.MEDIUM, new OSAI_IndexAddress(OSAI_VARCODE.IW_CODE, 7)));
            VACUUM_PRESENCE = RegisterVariable(new OSAI_VariablePressure<AnalogSensors.PSE541>(connection_provider, "VACUUM PRESENCE", new OSAI_IndexAddress(OSAI_VARCODE.MW_CODE, 10213), FluxMemReadPriority.MEDIUM, new OSAI_IndexAddress(OSAI_VARCODE.IW_CODE, 8)));

            // OUTPUTS                                                                                                                                                                                                                                                                                    
            AUX_ON = RegisterVariable(new OSAI_VariableBool(connection_provider, "AUX ON", new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10016, 0), FluxMemReadPriority.LOW));

            ENABLE_DRIVERS = RegisterArray(new OSAI_ArrayBool(connection_provider, "ENABLE DRIVERS", 2, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10750, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 1, 0), drivers_unit));
            DISABLE_24V = RegisterVariable(new OSAI_VariableBool(connection_provider, "DISABLE 24V", new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10751, 0), FluxMemReadPriority.LOW, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 1, 2)));

            OPEN_LOCK = RegisterArray(new OSAI_ArrayBool(connection_provider, "OPEN LOCK", 2, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10752, 0), FluxMemReadPriority.ULTRAHIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 1, 3), lock_unit));

            CHAMBER_LIGHT = RegisterVariable(new OSAI_VariableBool(connection_provider, "CHAMBER LIGHT", new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10753, 0), FluxMemReadPriority.ULTRAHIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 1, 5)));
            ENABLE_VACUUM = RegisterVariable(new OSAI_VariableBool(connection_provider, "ENABLE VACUUM", new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10754, 1), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 1, 7)));
            OPEN_HEAD_CLAMP = RegisterVariable(new OSAI_VariableBool(connection_provider, "OPEN HEAD CLAMP", new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10755, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 1, 8)));
            ENABLE_HEAD_FAN = RegisterVariable(new OSAI_VariableBool(connection_provider, "ENABLE HEAD FAN", new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10756, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 1, 9)));
            ENABLE_CHAMBER_FAN = RegisterVariable(new OSAI_VariableBool(connection_provider, "ENABLE CHAMBER FAN", new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10757, 0), FluxMemReadPriority.LOW, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 1, 10)));

            ENABLE_HOLDING_FAN = RegisterArray(new OSAI_ArrayBool(connection_provider, "ENABLE HOLDING FAN", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10759, 0), FluxMemReadPriority.LOW, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 2, 0)));    
        }

        private static async Task<Optional<IFLUX_MCodeRecovery>> GetMCodeRecoveryAsync(OSAI_Connection connection)
        {
            try
            {
                var hold_tool = await connection.ReadNamedShortAsync("!HOLD_TOOL");
                if (!hold_tool.HasValue)
                    return default;

                var hold_blk_num = await connection.ReadNamedDoubleAsync("!HOLD_BLK");
                if (!hold_blk_num.HasValue)
                    return default;

                var req_hold = await connection.ReadNamedBoolAsync("!REQ_HOLD");
                if (!req_hold.HasValue)
                    return default;

                var is_hold = await connection.ReadNamedBoolAsync("!IS_HOLD");
                if (!is_hold.HasValue)
                    return default;

                if (!req_hold.Value && !is_hold.Value)
                    return default;

                var hold_temperatures = new Dictionary<ushort, double>();
                for (ushort position = 0; position < 4; position++)
                {
                    var hold_temperature = await connection.ReadNamedDoubleAsync(new OSAI_NamedAddress("!HOLD_TEMP", position));
                    if (!hold_temperature.HasValue)
                        continue;
                    hold_temperatures.Add(position, hold_temperature.Value);
                }

                if (hold_temperatures.Count < 1)
                    return default;

                var hold_positions = new Dictionary<ushort, double>();
                for (ushort position = 0; position < 4; position++)
                {
                    var hold_position = await connection.ReadNamedDoubleAsync(new OSAI_NamedAddress("!HOLD_POS", position));
                    if (!hold_position.HasValue)
                        continue;
                    hold_positions.Add(position, hold_position.Value);
                }

                if (hold_positions.Count < 1)
                    return default;

                var hold_pp_str = await connection.ReadNamedStringAsync("!HOLD_PP", 36);
                if (!hold_pp_str.HasValue)
                    return default;

                var hold_pp = MCodePartProgram.Parse(hold_pp_str.Value);
                if (!hold_pp.HasValue)
                    return default;

                var hold_mcode_lookup = connection.Flux.MCodes.AvaiableMCodes.Lookup(hold_pp.Value.MCodeGuid);
                if (!hold_mcode_lookup.HasValue)
                    return default;

                var hold_analyzer = hold_mcode_lookup.Value.Analyzer;
                if (!hold_analyzer.HasValue)
                    return default;

                var selected_pp = await connection.ReadVariableAsync(m => m.PART_PROGRAM);
                var is_selected = selected_pp.ConvertOr(pp =>
                {
                    if (!is_hold.Value)
                        return false;
                    if (!pp.IsRecovery)
                        return false;
                    if (pp.MCodeGuid != hold_analyzer.Value.MCode.MCodeGuid)
                        return false;
                    if (pp.StartBlock != hold_blk_num.Value)
                        return false;
                    return true;
                }, () => false);

                return new OSAI_MCodeRecovery(
                    hold_pp.Value.MCodeGuid,
                    hold_pp.Value.StartBlock,
                    is_selected,
                    (uint)hold_blk_num.Value,
                    hold_tool.Value,
                    hold_temperatures,
                    hold_positions);
            }
            catch (Exception ex)
            {
                return default;
            }
        }

        private static async Task<Optional<Dictionary<Guid, Dictionary<BlockNumber, MCodePartProgram>>>> GetStorageAsync(OSAI_Connection connection)
        {
            var qctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var storage = await connection.ListFilesAsync(
                c => c.StoragePath,
                qctk.Token);
            if (!storage.HasValue)
                return default;
            return storage.Value.GetPartProgramDictionaryFromStorage();
        }

        private static async Task<Optional<Dictionary<QueuePosition, FluxJob>>> GetQueueAsync(OSAI_Connection connection)
        {
            var qctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var queue = await connection.ListFilesAsync(
                c => c.QueuePath,
                qctk.Token);
            if (!queue.HasValue)
                return default;
            return queue.Value.GetJobDictionaryFromQueue();
        }

        private static async Task<Optional<LineNumber>> GetBlockNumAsync(OSAI_Connection connection)
        {
            try
            {
                var get_blk_num_response = await connection.Client.GetBlkNumAsync(connection.ProcessNumber);

                if (!connection.ProcessResponse(
                    get_blk_num_response.Body.retval,
                    get_blk_num_response.Body.ErrClass,
                    get_blk_num_response.Body.ErrNum))
                    return default;

                return (LineNumber)get_blk_num_response.Body.GetBlkNum.MainActBlk;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
        }
        private static async Task<Optional<OSAI_BootPhase>> GetBootPhaseAsync(OSAI_Connection connection)
        {
            try
            {
                var boot_phase_request = new BootPhaseEnquiryRequest();
                var boot_phase_response = await connection.Client.BootPhaseEnquiryAsync(boot_phase_request);

                if (!connection.ProcessResponse(
                    boot_phase_response.retval,
                    boot_phase_response.ErrClass,
                    boot_phase_response.ErrNum))
                    return default;

                return (OSAI_BootPhase)boot_phase_response.Phase;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
        }
        private static async Task<bool> SetBootModeAsync(OSAI_Connection connection, OSAI_BootMode boot_mode)
        {
            try
            {
                var boot_mode_request = new BootModeRequest((ushort)boot_mode);
                var boot_mode_response = await connection.Client.BootModeAsync(boot_mode_request);

                if (!connection.ProcessResponse(
                    boot_mode_response.retval,
                    boot_mode_response.ErrClass,
                    boot_mode_response.ErrNum))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
        private static async Task<Optional<MCodePartProgram>> GetPartProgramAsync(OSAI_Connection connection)
        {
            try
            {
                var queue_pos = await connection.ReadVariableAsync(c => c.QUEUE_POS);
                if (queue_pos.Value < 0)
                    return default;
 
                var job_queue = await connection.ReadVariableAsync(c => c.QUEUE);
                if (!job_queue.HasValue)
                    return default;

                if (!job_queue.Value.TryGetValue(queue_pos.Value, out var current_job))
                    return default;

                var storage_dict = await connection.ReadVariableAsync(c => c.STORAGE);
                if (!storage_dict.HasValue)
                    return default;

                if (!storage_dict.Value.ContainsKey(current_job.MCodeGuid))
                    return default;

                // Full part program from filename
                var get_active_pp_request = new GetActivePartProgramRequest(connection.ProcessNumber);
                var get_active_pp_response = await connection.Client.GetActivePartProgramAsync(get_active_pp_request);

                if (!connection.ProcessResponse(
                    get_active_pp_response.retval,
                    get_active_pp_response.ErrClass,
                    get_active_pp_response.ErrNum))
                    return default;

                var partprogram_filename = get_active_pp_response.Main;

                if (MCodePartProgram.TryParse(partprogram_filename, out var full_part_program) &&
                    full_part_program.MCodeGuid == current_job.MCodeGuid)
                {
                    if (storage_dict.Value.TryGetValue(full_part_program.MCodeGuid, out var part_programs) &&
                        part_programs.TryGetValue(full_part_program.StartBlock, out var part_program))
                        return part_program;
                }

                return storage_dict.Value.FirstOrOptional(kvp => kvp.Key == current_job.MCodeGuid)
                    .Convert(p => p.Value.Values.FirstOrDefault());
            }
            catch
            {
                return default;
            }
        }
        private static async Task<Optional<OSAI_ProcessMode>> GetProcessModeAsync(OSAI_Connection connection)
        {
            try
            {
                var process_status_response = await connection.Client.GetProcessStatusAsync(connection.ProcessNumber);

                if (!connection.ProcessResponse(
                    process_status_response.Body.retval,
                    process_status_response.Body.ErrClass,
                    process_status_response.Body.ErrNum))
                    return default;

                return (OSAI_ProcessMode)process_status_response.Body.ProcStat.Mode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
        }
        private static async Task<Optional<FLUX_ProcessStatus>> GetProcessStatusAsync(OSAI_Connection connection)
        {
            try
            {
                var process_status_response = await connection.Client.GetProcessStatusAsync(connection.ProcessNumber);

                if (!connection.ProcessResponse(
                    process_status_response.Body.retval,
                    process_status_response.Body.ErrClass,
                    process_status_response.Body.ErrNum))
                    return default;

                var status = (OSAI_ProcessStatus)process_status_response.Body.ProcStat.Status;
                switch (status)
                {
                    case OSAI_ProcessStatus.NONE:
                        return FLUX_ProcessStatus.NONE;

                    case OSAI_ProcessStatus.IDLE:
                        return FLUX_ProcessStatus.IDLE;

                    case OSAI_ProcessStatus.CYCLE:
                    case OSAI_ProcessStatus.HOLDA:
                    case OSAI_ProcessStatus.RUNH:
                    case OSAI_ProcessStatus.HRUN:
                    case OSAI_ProcessStatus.RESET:
                    case OSAI_ProcessStatus.WAIT:
                    case OSAI_ProcessStatus.INPUT:
                        return FLUX_ProcessStatus.CYCLE;

                    case OSAI_ProcessStatus.ERRO:
                        return FLUX_ProcessStatus.ERROR;

                    case OSAI_ProcessStatus.EMERG:
                        return FLUX_ProcessStatus.EMERG;

                    default:
                        return FLUX_ProcessStatus.NONE;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
        }
        private static async Task<bool> SetProcessModeAsync(OSAI_Connection connection, OSAI_ProcessMode process_mode)
        {
            try
            {
                var set_process_mode_request = new SetProcessModeRequest(connection.ProcessNumber, (ushort)process_mode);
                var set_process_mode_response = await connection.Client.SetProcessModeAsync(set_process_mode_request);

                if (!connection.ProcessResponse(
                    set_process_mode_response.retval,
                    set_process_mode_response.ErrClass,
                    set_process_mode_response.ErrNum))
                    return false;

                return await connection.WaitProcessModeAsync(
                    m => m == process_mode,
                    TimeSpan.FromSeconds(0),
                    TimeSpan.FromSeconds(0.1),
                    TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
    }
}
