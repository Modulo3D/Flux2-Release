using DynamicData.Kernel;
using Modulo3DStandard;
using OSAI;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class OSAI_VariableStore : FLUX_VariableStore<OSAI_VariableStore>
    {
        public Optional<IFLUX_Variable<OSAI_ProcessMode, OSAI_ProcessMode>> PROCESS_MODE { get; }
        public Optional<IFLUX_Variable<OSAI_BootPhase, Unit>> BOOT_PHASE { get; }
        public Optional<IFLUX_Variable<OSAI_BootMode, OSAI_BootMode>> BOOT_MODE { get; }
        public Optional<IFLUX_Variable<double, double>> Z_PLATE_HEIGHT { get; }
        public Optional<IFLUX_Variable<double, double>> FEEDRATE { get; }
        public Optional<IFLUX_Variable<bool, bool>> AUX_ON { get; }

        public OSAI_VariableStore(OSAI_ConnectionProvider connection_provider)
        {
            var bump_unit = new VariableUnit[] { "x", "z" };
            var drivers_unit = new VariableUnit[] { "xyz", "e" };
            var lock_unit = new VariableUnit[] { "chamber", "top" };
            var pid_unit = new VariableUnit[] { "kp", "ki", "kd" };
            var axis_unit = new VariableUnit[] { "x", "y", "z", "e" };
            var probe_unit = new VariableUnit[] { "plate_z", "tool_z", "tool_xy" };

            var connection = connection_provider.WhenAnyValue(v => v.Connection);

            QUEUE = RegisterVariable(new FLUX_VariableGP<OSAI_Connection, Dictionary<ushort, Guid>, Unit>(connection, "QUEUE", FluxMemReadPriority.MEDIUM, GetQueueAsync));
            STORAGE = RegisterVariable(new FLUX_VariableGP<OSAI_Connection, Dictionary<Guid, MCodePartProgram>, Unit>(connection, "STORAGE", FluxMemReadPriority.MEDIUM, GetStorageAsync));

            PROCESS_STATUS = RegisterVariable(new FLUX_VariableGP<OSAI_Connection, FLUX_ProcessStatus, Unit>(connection, "PROCESS_STATUS", FluxMemReadPriority.ULTRAHIGH, GetProcessStatusAsync));
            PROCESS_MODE = RegisterVariable(new FLUX_VariableGP<OSAI_Connection, OSAI_ProcessMode, OSAI_ProcessMode>(connection, "PROCESS_MODE", FluxMemReadPriority.ULTRAHIGH, GetProcessModeAsync, SetProcessModeAsync));

            BOOT_PHASE = RegisterVariable(new FLUX_VariableGP<OSAI_Connection, OSAI_BootPhase, Unit>(connection, "BOOT_PHASE", FluxMemReadPriority.ULTRAHIGH, GetBootPhaseAsync));

            BOOT_MODE = RegisterVariable(new FLUX_VariableGP<OSAI_Connection, OSAI_BootMode, OSAI_BootMode>(connection, "BOOT_MODE", FluxMemReadPriority.ULTRAHIGH, write_func: SetBootModeAsync));
            PART_PROGRAM = RegisterVariable(new FLUX_VariableGP<OSAI_Connection, MCodePartProgram, Unit>(connection, "PART_PROGRAM", FluxMemReadPriority.ULTRAHIGH, GetPartProgramAsync));
            BLOCK_NUM = RegisterVariable(new FLUX_VariableGP<OSAI_Connection, uint, Unit>(connection, "BLOCK_NUM", FluxMemReadPriority.ULTRAHIGH, GetBlockNumAsync));

            IS_HOLD = RegisterVariable(new OSAI_VariableNamedBool(connection, "IS HOLD", new OSAI_NamedAddress("!IS_HOLD"), FluxMemReadPriority.ULTRAHIGH));
            REQ_HOLD = RegisterVariable(new OSAI_VariableNamedBool(connection, "REQ HOLD", new OSAI_NamedAddress("!REQ_HOLD"), FluxMemReadPriority.ULTRAHIGH));
            USER_INPUT = RegisterVariable(new OSAI_VariableNamedShort(connection, "USER INPUT", new OSAI_NamedAddress("!USER_INPUT"), FluxMemReadPriority.ULTRAHIGH));

            HOLD_TOOL = RegisterVariable(new OSAI_VariableNamedShort(connection, "HOLD TOOL", new OSAI_NamedAddress("!HOLD_TOOL"), FluxMemReadPriority.MEDIUM));
            HOLD_PP = RegisterVariable(new OSAI_VariableNamedString(connection, "HOLD PART PROGRAM", new OSAI_NamedAddress("!HOLD_PP"), FluxMemReadPriority.MEDIUM, 36)); // max len is 64, skip after guid for now                                                                                                                                           
            HOLD_BLK_NUM = RegisterVariable(new OSAI_VariableNamedDouble(connection, "HOLD BLOCK NUMBER", new OSAI_NamedAddress("!HOLD_BLK"), FluxMemReadPriority.MEDIUM));
            HOLD_POS = RegisterVariable(new OSAI_ArrayNamedDouble(connection, "HOLD POSITION", 4, new OSAI_NamedAddress("!HOLD_POS"), FluxMemReadPriority.MEDIUM, axis_unit));
            HOLD_TEMP = RegisterVariable(new OSAI_ArrayNamedDouble(connection, "HOLD TEMPERATURE", 4, new OSAI_NamedAddress("!HOLD_TEMP"), FluxMemReadPriority.MEDIUM));

            IS_HOMED = RegisterVariable(new OSAI_VariableNamedBool(connection, "IS HOMED", new OSAI_NamedAddress("!IS_HOMED"), FluxMemReadPriority.HIGH));
            IS_HOMING = RegisterVariable(new OSAI_VariableNamedBool(connection, "IS HOMING", new OSAI_NamedAddress("!IS_HOMING"), FluxMemReadPriority.HIGH));
            IN_CHANGE = RegisterVariable(new OSAI_VariableNamedBool(connection, "IN CHANGE", new OSAI_NamedAddress("!IN_CHANGE"), FluxMemReadPriority.HIGH));

            WATCH_VACUUM = RegisterVariable(new OSAI_VariableNamedBool(connection, "WATCH VACUUM", new OSAI_NamedAddress("!WTC_VACUUM"), FluxMemReadPriority.ULTRALOW));
            Z_PLATE_HEIGHT = RegisterVariable(new OSAI_VariableNamedDouble(connection, "PLATE HEIGHT Z", new OSAI_NamedAddress("!Z_PLATE_H"), FluxMemReadPriority.ULTRALOW));

            X_USER_OFFSET_T = RegisterVariable(new OSAI_ArrayNamedDouble(connection, "X USER OFFSET TOOL", 4, new OSAI_NamedAddress("!X_USR_OF_T"), FluxMemReadPriority.LOW));
            Y_USER_OFFSET_T = RegisterVariable(new OSAI_ArrayNamedDouble(connection, "Y USER OFFSET TOOL", 4, new OSAI_NamedAddress("!Y_USR_OF_T"), FluxMemReadPriority.LOW));
            Z_USER_OFFSET_T = RegisterVariable(new OSAI_ArrayNamedDouble(connection, "Z USER OFFSET TOOL", 4, new OSAI_NamedAddress("!Z_USR_OF_T"), FluxMemReadPriority.LOW));

            X_PROBE_OFFSET_T = RegisterVariable(new OSAI_ArrayNamedDouble(connection, "X PROBE OFFSET TOOL", 4, new OSAI_NamedAddress("!X_PRB_OF_T"), FluxMemReadPriority.LOW));
            Y_PROBE_OFFSET_T = RegisterVariable(new OSAI_ArrayNamedDouble(connection, "Y PROBE OFFSET TOOL", 4, new OSAI_NamedAddress("!Y_PRB_OF_T"), FluxMemReadPriority.LOW));
            Z_PROBE_OFFSET_T = RegisterVariable(new OSAI_ArrayNamedDouble(connection, "Z PROBE OFFSET TOOL", 4, new OSAI_NamedAddress("!Z_PRB_OF_T"), FluxMemReadPriority.LOW));

            // GW VARIABLES                                                                                                                                                                                                                                                                              
            TOOL_CUR = RegisterVariable(new OSAI_VariableShort(connection, "TOOL CURRENT", new OSAI_IndexAddress(OSAI_VARCODE.GW_CODE, 0), FluxMemReadPriority.ULTRALOW));
            TOOL_NUM = RegisterVariable(new OSAI_VariableWord(connection, "TOOL NUMBER", new OSAI_IndexAddress(OSAI_VARCODE.GW_CODE, 1), FluxMemReadPriority.ULTRALOW));
            DEBUG = RegisterVariable(new OSAI_VariableBool(connection, "DEBUG", new OSAI_BitIndexAddress(OSAI_VARCODE.GW_CODE, 2, 0), FluxMemReadPriority.HIGH));
            KEEP_CHAMBER = RegisterVariable(new OSAI_VariableBool(connection, "KEEP CHAMBER WARM", new OSAI_BitIndexAddress(OSAI_VARCODE.GW_CODE, 3, 0), FluxMemReadPriority.HIGH));
            KEEP_TOOL = RegisterVariable(new OSAI_VariableBool(connection, "KEEP TOOL WARM", new OSAI_BitIndexAddress(OSAI_VARCODE.GW_CODE, 4, 0), FluxMemReadPriority.HIGH));
            HAS_PLATE = RegisterVariable(new OSAI_VariableBool(connection, "HAS PLATE", new OSAI_BitIndexAddress(OSAI_VARCODE.GW_CODE, 5, 0), FluxMemReadPriority.ULTRALOW));
            AUTO_FAN = RegisterVariable(new OSAI_VariableBool(connection, "AUTO FAN", new OSAI_BitIndexAddress(OSAI_VARCODE.GW_CODE, 6, 0), FluxMemReadPriority.ULTRALOW));
            QUEUE_POS = RegisterVariable(new OSAI_VariableShort(connection, "QUEUE POS", new OSAI_IndexAddress(OSAI_VARCODE.GW_CODE, 7), FluxMemReadPriority.ULTRAHIGH));

            MEM_TOOL_ON_TRAILER = RegisterVariable(new OSAI_ArrayBool(connection, "TOOL ON TRAILER", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.GW_CODE, 100, 0), FluxMemReadPriority.HIGH));
            MEM_TOOL_IN_MAGAZINE = RegisterVariable(new OSAI_ArrayBool(connection, "TOOL IN MAGAZINE", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.GW_CODE, 101, 0), FluxMemReadPriority.HIGH));

            // GD VARIABLES                                                                                                                                                                                                                                                                
            TEMP_WAIT = RegisterVariable(new OSAI_ArrayDouble(connection, "TEMP WAIT", 3, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 0), FluxMemReadPriority.ULTRALOW));
            PID_TOOL = RegisterVariable(new OSAI_ArrayDouble(connection, "PID TOOL", 3, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 3), FluxMemReadPriority.ULTRALOW, custom_unit: pid_unit));
            PID_CHAMBER = RegisterVariable(new OSAI_ArrayDouble(connection, "PID CHAMBER", 3, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 6), FluxMemReadPriority.ULTRALOW, custom_unit: pid_unit));
            PID_PLATE = RegisterVariable(new OSAI_ArrayDouble(connection, "PID PLATE", 3, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 9), FluxMemReadPriority.ULTRALOW, custom_unit: pid_unit));
            PID_RANGE = RegisterVariable(new OSAI_ArrayDouble(connection, "PID RANGE", 3, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 12), FluxMemReadPriority.ULTRALOW));
            TEMP_WINDOW = RegisterVariable(new OSAI_ArrayDouble(connection, "TEMP WINDOW", 3, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 15), FluxMemReadPriority.ULTRALOW));

            FAN_ENABLE = RegisterVariable(new OSAI_ArrayDouble(connection, "FAN ENABLE", 2, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 18), FluxMemReadPriority.ULTRALOW));
            FEEDRATE = RegisterVariable(new OSAI_VariableDouble(connection, "FEEDRATE", new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 20), FluxMemReadPriority.ULTRALOW));
            PURGE_POSITION = RegisterVariable(new OSAI_ArrayDouble(connection, "PURGE POSITION", 2, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 21), FluxMemReadPriority.ULTRALOW, custom_unit: axis_unit));
            Y_SAFE_MAX = RegisterVariable(new OSAI_VariableDouble(connection, "Y SAFE MAX", new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 23), FluxMemReadPriority.ULTRALOW));
            Z_PROBE_MIN = RegisterVariable(new OSAI_VariableDouble(connection, "Z PROBE MIN", new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 24), FluxMemReadPriority.ULTRALOW));
            Z_TOOL_CORRECTION = RegisterVariable(new OSAI_VariableDouble(connection, "Z TOOL COR", new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 25), FluxMemReadPriority.ULTRALOW));
            READER_POSITION = RegisterVariable(new OSAI_ArrayDouble(connection, "READER POSITION", 2, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 26), FluxMemReadPriority.ULTRALOW, custom_unit: axis_unit));
            TOOL_PROBE_POSITION = RegisterVariable(new OSAI_ArrayDouble(connection, "TOOL PROBE POSITION", 2, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 28), FluxMemReadPriority.ULTRALOW, custom_unit: axis_unit));
            HOME_BUMP = RegisterVariable(new OSAI_ArrayDouble(connection, "HOME BUMP", 2, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 30), FluxMemReadPriority.ULTRALOW, custom_unit: bump_unit));
            HOME_OFFSET = RegisterVariable(new OSAI_ArrayDouble(connection, "HOME OFFSET", 2, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 32), FluxMemReadPriority.ULTRALOW, custom_unit: axis_unit));
            PLATE_PROBE_POSITION = RegisterVariable(new OSAI_ArrayDouble(connection, "PLATE PROBE POSITION", 2, new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 34), FluxMemReadPriority.ULTRALOW, custom_unit: axis_unit));

            PRESSURE_LEVEL = RegisterVariable(new OSAI_VariableDouble(connection, "PRESSURE LEVEL", new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 36), FluxMemReadPriority.ULTRALOW));
            VACUUM_LEVEL = RegisterVariable(new OSAI_VariableDouble(connection, "VACUUM LEVEL", new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 37), FluxMemReadPriority.ULTRALOW));

            TOOL_OFF_TIME = RegisterVariable(new OSAI_VariableDouble(connection, "TOOL OFF TIMER", new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 38), FluxMemReadPriority.ULTRALOW));
            CHAMBER_OFF_TIME = RegisterVariable(new OSAI_VariableDouble(connection, "CHAMBER OFF TIMER", new OSAI_IndexAddress(OSAI_VARCODE.GD_CODE, 39), FluxMemReadPriority.ULTRALOW));

            // L VARIABLES                                                                                                                                                                                                                                                                         
            X_MAGAZINE_POS = RegisterVariable(new OSAI_ArrayDouble(connection, "X MAGAZINE POSITION", 4, new OSAI_IndexAddress(OSAI_VARCODE.L_CODE, 1), FluxMemReadPriority.ULTRALOW));
            Y_MAGAZINE_POS = RegisterVariable(new OSAI_ArrayDouble(connection, "Y MAGAZINE POSITION", 4, new OSAI_IndexAddress(OSAI_VARCODE.L_CODE, 18), FluxMemReadPriority.ULTRALOW));

            // STRINGS                                                                                                                                                                                                                                                                               
            TEMP_CHAMBER = RegisterVariable(new OSAI_VariableTemp(connection, "TEMP CHAMBER", new OSAI_IndexAddress(OSAI_VARCODE.AA_CODE, 0), FluxMemReadPriority.ULTRAHIGH, t => $"M4140[{t}, 0]"));
            TEMP_PLATE = RegisterVariable(new OSAI_VariableTemp(connection, "TEMP PLATE", new OSAI_IndexAddress(OSAI_VARCODE.AA_CODE, 1), FluxMemReadPriority.ULTRAHIGH, t => $"M4141[{t}, 0]"));
            TEMP_TOOL = RegisterVariable(new OSAI_ArrayTemp(connection, "TEMP TOOL", 4, new OSAI_IndexAddress(OSAI_VARCODE.AA_CODE, 2), FluxMemReadPriority.ULTRAHIGH, (i, t) => $"M4104[{i + 1}, {t}, 0]"));

            // MW VARIABLES                                                                                                                                                                                                                                                                             
            RUNNING_MACRO = RegisterVariable(new OSAI_VariableMacro(connection, "RUNNING MACRO", new OSAI_IndexAddress(OSAI_VARCODE.MW_CODE, 11021)));
            RUNNING_MCODE = RegisterVariable(new OSAI_VariableMCode(connection, "RUNNING MCODE", new OSAI_IndexAddress(OSAI_VARCODE.MW_CODE, 9999)));
            RUNNING_GCODE = RegisterVariable(new OSAI_VariableGCode(connection, "RUNNING GCODE", new OSAI_IndexAddress(OSAI_VARCODE.MW_CODE, 11022)));

            // INPUTS                                                                                                                                                                                                                                                                             
            LOCK_CLOSED = RegisterVariable(new OSAI_ArrayBool(connection, "LOCK CLOSED", 2, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10201, 0), FluxMemReadPriority.ULTRAHIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 3, 1), lock_unit));
            TOOL_ON_TRAILER = RegisterVariable(new OSAI_ArrayBool(connection, "TOOL ON TRAILER", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10202, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 3, 4)));
            UPS_STATUS = RegisterVariable(new OSAI_ArrayBool(connection, "UPS STATUS", 6, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10203, 0), FluxMemReadPriority.DISABLED));

            WIRE_PRESENCE_BEFORE_GEAR = RegisterVariable(new OSAI_ArrayBool(connection, "WIRE PRESENCE BEFORE GEAR", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10204, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 4, 0)));
            WIRE_PRESENCE_AFTER_GEAR = RegisterVariable(new OSAI_ArrayBool(connection, "WIRE PRESENCE AFTER GEAR", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10205, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 4, 4)));
            WIRE_PRESENCE_ON_HEAD = RegisterVariable(new OSAI_ArrayBool(connection, "WIRE PRESENCE ON HEAD", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10206, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 4, 8)));
            TOOL_IN_MAGAZINE = RegisterVariable(new OSAI_ArrayBool(connection, "TOOL IN MAGAZINE", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10207, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 5, 0)));
            PISTON_LOW = RegisterVariable(new OSAI_ArrayBool(connection, "PISTON LOW", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10208, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 5, 4)));

            AXIS_ENDSTOP = RegisterVariable(new OSAI_ArrayBool(connection, "AXIS ENDSTOP", 3, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10209, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 6, 0), axis_unit));
            AXIS_PROBE = RegisterVariable(new OSAI_ArrayBool(connection, "AXIS PROBE", 3, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10210, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 6, 3), probe_unit));

            DRIVER_EMERGENCY = RegisterVariable(new OSAI_VariableBool(connection, "DRIVER EMERGENCY", new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10211, 0), FluxMemReadPriority.MEDIUM, new OSAI_BitIndexAddress(OSAI_VARCODE.IW_CODE, 6, 6)));
            PRESSURE_PRESENCE = RegisterVariable(new OSAI_VariablePressure<AnalogSensors.PSE540>(connection, "PRESSURE PRESENCE", new OSAI_IndexAddress(OSAI_VARCODE.MW_CODE, 10212), FluxMemReadPriority.MEDIUM, new OSAI_IndexAddress(OSAI_VARCODE.IW_CODE, 7)));
            VACUUM_PRESENCE = RegisterVariable(new OSAI_VariablePressure<AnalogSensors.PSE541>(connection, "VACUUM PRESENCE", new OSAI_IndexAddress(OSAI_VARCODE.MW_CODE, 10213), FluxMemReadPriority.MEDIUM, new OSAI_IndexAddress(OSAI_VARCODE.IW_CODE, 8)));

            // OUTPUTS                                                                                                                                                                                                                                                                                    
            AUX_ON = RegisterVariable(new OSAI_VariableBool(connection, "AUX ON", new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10016, 0), FluxMemReadPriority.LOW));

            ENABLE_DRIVERS = RegisterVariable(new OSAI_ArrayBool(connection, "ENABLE DRIVERS", 2, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10750, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 1, 0), drivers_unit));
            DISABLE_24V = RegisterVariable(new OSAI_VariableBool(connection, "DISABLE 24V", new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10751, 0), FluxMemReadPriority.LOW, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 1, 2)));

            OPEN_LOCK = RegisterVariable(new OSAI_ArrayBool(connection, "OPEN LOCK", 2, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10752, 0), FluxMemReadPriority.ULTRAHIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 1, 3), lock_unit));

            CHAMBER_LIGHT = RegisterVariable(new OSAI_VariableBool(connection, "CHAMBER LIGHT", new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10753, 0), FluxMemReadPriority.ULTRAHIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 1, 5)));
            ENABLE_VACUUM = RegisterVariable(new OSAI_VariableBool(connection, "ENABLE VACUUM", new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10754, 1), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 1, 7)));
            OPEN_HEAD_CLAMP = RegisterVariable(new OSAI_VariableBool(connection, "OPEN HEAD CLAMP", new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10755, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 1, 8)));
            ENABLE_HEAD_FAN = RegisterVariable(new OSAI_VariableBool(connection, "ENABLE HEAD FAN", new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10756, 0), FluxMemReadPriority.HIGH, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 1, 9)));
            ENABLE_CHAMBER_FAN = RegisterVariable(new OSAI_VariableBool(connection, "ENABLE CHAMBER FAN", new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10757, 0), FluxMemReadPriority.LOW, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 1, 10)));
            UPS_REMOTE_SHUTDOWN = RegisterVariable(new OSAI_VariableBool(connection, "UPS REMOTE SHUTDOWN", new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10758, 0), FluxMemReadPriority.LOW, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 1, 15)));

            ENABLE_HOLDING_FAN = RegisterVariable(new OSAI_ArrayBool(connection, "ENABLE HOLDING FAN", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10759, 0), FluxMemReadPriority.LOW, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 2, 0)));
            RAISE_PNEUMATIC_PISTON = RegisterVariable(new OSAI_ArrayBool(connection, "RAISE PNEUMATIC PISTON", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10760, 0), FluxMemReadPriority.LOW, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 2, 4)));
            LOWER_PNEUMATIC_PISTON = RegisterVariable(new OSAI_ArrayBool(connection, "LOWER PNEUMATIC PISTON", 4, new OSAI_BitIndexAddress(OSAI_VARCODE.MW_CODE, 10761, 0), FluxMemReadPriority.LOW, new OSAI_BitIndexAddress(OSAI_VARCODE.OW_CODE, 2, 8)));


        }

        private async Task<Optional<Dictionary<Guid, MCodePartProgram>>> GetStorageAsync(OSAI_Connection connection)
        {
            var qctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var storage = await connection.ListFilesAsync(
                c => c.StoragePath,
                qctk.Token);
            if (!storage.HasValue)
                return default;
            return storage.Value.GetPartProgramDictionaryFromStorage();
        }

        private async Task<Optional<Dictionary<ushort, Guid>>> GetQueueAsync(OSAI_Connection connection)
        {
            var qctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var queue = await connection.ListFilesAsync(
                c => c.QueuePath,
                qctk.Token);
            if (!queue.HasValue)
                return default;
            return queue.Value.GetGuidDictionaryFromQueue();
        }

        private async Task<Optional<uint>> GetBlockNumAsync(OSAI_Connection connection)
        {
            try
            {
                if (!connection.Client.HasValue)
                    return default;

                var get_blk_num_response = await connection.Client.Value.GetBlkNumAsync(connection.ProcessNumber);

                if (!connection.ProcessResponse(
                    get_blk_num_response.Body.retval,
                    get_blk_num_response.Body.ErrClass,
                    get_blk_num_response.Body.ErrNum))
                    return default;

                return get_blk_num_response.Body.GetBlkNum.MainActBlk;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
        }
        private async Task<Optional<OSAI_BootPhase>> GetBootPhaseAsync(OSAI_Connection connection)
        {
            try
            {
                if (!connection.Client.HasValue)
                    return default;

                var boot_phase_request = new BootPhaseEnquiryRequest();
                var boot_phase_response = await connection.Client.Value.BootPhaseEnquiryAsync(boot_phase_request);

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
        private async Task<bool> SetBootModeAsync(OSAI_Connection connection, OSAI_BootMode boot_mode)
        {
            try
            {
                if (!connection.Client.HasValue)
                    return default;

                var boot_mode_request = new BootModeRequest((ushort)boot_mode);
                var boot_mode_response = await connection.Client.Value.BootModeAsync(boot_mode_request);

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
        private async Task<Optional<MCodePartProgram>> GetPartProgramAsync(OSAI_Connection connection)
        {
            try
            {
                var queue_pos = await connection.ReadVariableAsync(c => c.QUEUE_POS);
                if (!queue_pos.HasValue)
                    return default;

                if (queue_pos.Value < 0)
                    return default;

                var queue_dict = await connection.ReadVariableAsync(c => c.QUEUE);
                if (!queue_dict.HasValue || !queue_dict.Value.TryGetValue((ushort)queue_pos.Value, out var current_job))
                    return default;

                var storage_dict = await connection.ReadVariableAsync(c => c.STORAGE);
                if (!storage_dict.HasValue || !storage_dict.Value.TryGetValue(current_job, out var part_program))
                    return default;

                return part_program;
            }
            catch
            {
                return default;
            }
        }
        private async Task<Optional<OSAI_ProcessMode>> GetProcessModeAsync(OSAI_Connection connection)
        {
            try
            {
                if (!connection.Client.HasValue)
                    return default;

                var process_status_response = await connection.Client.Value.GetProcessStatusAsync(connection.ProcessNumber);

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
        private async Task<Optional<FLUX_ProcessStatus>> GetProcessStatusAsync(OSAI_Connection connection)
        {
            try
            {
                if (!connection.Client.HasValue)
                    return default;

                var process_status_response = await connection.Client.Value.GetProcessStatusAsync(connection.ProcessNumber);

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
        private async Task<bool> SetProcessModeAsync(OSAI_Connection connection, OSAI_ProcessMode process_mode)
        {
            try
            {
                if (!connection.Client.HasValue)
                    return false;

                var set_process_mode_request = new SetProcessModeRequest(connection.ProcessNumber, (ushort)process_mode);
                var set_process_mode_response = await connection.Client.Value.SetProcessModeAsync(set_process_mode_request);

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
