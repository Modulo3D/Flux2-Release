using DynamicData.Kernel;
using Modulo3DNet;
using OSAI;
using System;
using System.IO;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class OSAI_VariableStore : FLUX_VariableStore<OSAI_VariableStore, OSAI_ConnectionProvider>
    {
        public override ushort ArrayBase => 1;
        public override char FeederAxis => 'A';
        public override bool HasPrintUnloader => false;
        public override bool CanProbeMagazine => false;
        public override bool HasMovementLimits => false;
        public override bool CanMeshProbePlate => false;
        public override bool ParkToolAfterOperation => false;
        public override FLUX_AxisTransform MoveTransform { get; }

        public IFLUX_Variable<OSAI_ProcessMode, OSAI_ProcessMode> PROCESS_MODE { get; set; }
        public IFLUX_Variable<Unit, OSAI_BootMode> BOOT_MODE { get; set; }
        public IFLUX_Variable<OSAI_BootPhase, Unit> BOOT_PHASE { get; set; }
        public IFLUX_Variable<bool, bool> AUX_ON { get; set; }

        public IFLUX_Array<double, double> HOME_OFFSET { get; set; }
        public IFLUX_Variable<double, double> Y_SAFE_MAX { get; set; }
        public IFLUX_Array<double, double> PURGE_POSITION { get; set; }
        public IFLUX_Array<double, double> TOOL_PROBE_POSITION { get; set; }
        public IFLUX_Variable<double, double> Z_TOOL_CORRECTION { get; set; }
        public IFLUX_Array<double, double> PLATE_PROBE_POSITION { get; set; }

        public OSAI_VariableStore(OSAI_ConnectionProvider connection_provider) : base(connection_provider)
        {
            MoveTransform = new FLUX_AxisTransform((m, r) =>
            {
                var x = m.Axes.Dictionary.Lookup('X');
                var y = m.Axes.Dictionary.Lookup('Y');

                if (!r && x.HasValue != y.HasValue)
                    return default;

                if (x.HasValue || y.HasValue)
                {
                    var core_x = x.ValueOr(() => 0) + y.ValueOr(() => 0);
                    var core_y = x.ValueOr(() => 0) - y.ValueOr(() => 0);

                    m = m with { Axes = m.Axes.Dictionary.SetItem('X', core_x) };
                    m = m with { Axes = m.Axes.Dictionary.SetItem('Y', core_y) };
                }

                return m;
            }, (m, r) =>
            {
                var core_x = m.Axes.Dictionary.Lookup('X');
                var core_y = m.Axes.Dictionary.Lookup('Y');

                if (!r && core_x.HasValue != core_y.HasValue)
                    return default;

                if (core_x.HasValue || core_y.HasValue)
                {
                    var x = (core_x.ValueOr(() => 0) + core_y.ValueOr(() => 0)) / 2;
                    var y = (core_x.ValueOr(() => 0) - core_y.ValueOr(() => 0)) / 2;

                    m = m with { Axes = m.Axes.Dictionary.SetItem('X', x) };
                    m = m with { Axes = m.Axes.Dictionary.SetItem('Y', y) };
                }

                return m;
            });

            try
            {
                var chamber_unit = new VariableUnits("main.chamber");
                var plate_unit = new VariableUnits("main.plate");
                var bump_unit = new VariableUnits("x", "y");
                var drivers_unit = new VariableUnits("xyz", "e");
                var lock_unit = new VariableUnits("main.lock", "top.lock");
                var pid_unit = new VariableUnits("kp", "ki", "kd");
                var axis_unit = new VariableUnits("x", "y", "z", "e");
                var probe_unit = new VariableUnits("plate_z", "tool_z", "tool_xy");

                var model = new OSAI_ModelBuilder(this);

                model.CreateVariable(c => c.IS_HOMED, "!IS_HOMED", OSAI_ReadPriority.HIGH);
                model.CreateVariable(c => c.IS_HOMING, "!IS_HOMING", OSAI_ReadPriority.HIGH);
                model.CreateVariable(c => c.QUEUE_POS, "!QUEUE_POS", OSAI_ReadPriority.ULTRAHIGH);

                model.CreateVariable(c => c.WATCH_VACUUM, "!WTC_VACUUM", OSAI_ReadPriority.ULTRALOW);
                model.CreateVariable(c => c.Z_BED_HEIGHT, "!Z_PLATE_H", OSAI_ReadPriority.ULTRALOW);
                model.CreateVariable(c => c.IN_CHANGE, "!IN_CHANGE", OSAI_ReadPriority.HIGH);

                model.CreateArray(c => c.X_USER_OFFSET, 4, "!X_USR_OF_T", OSAI_ReadPriority.LOW);
                model.CreateArray(c => c.Y_USER_OFFSET, 4, "!Y_USR_OF_T", OSAI_ReadPriority.LOW);
                model.CreateArray(c => c.Z_USER_OFFSET, 4, "!Z_USR_OF_T", OSAI_ReadPriority.LOW);

                model.CreateArray(c => c.X_PROBE_OFFSET, 4, "!X_PRB_OF_T", OSAI_ReadPriority.LOW);
                model.CreateArray(c => c.Y_PROBE_OFFSET, 4, "!Y_PRB_OF_T", OSAI_ReadPriority.LOW);
                model.CreateArray(c => c.Z_PROBE_OFFSET, 4, "!Z_PRB_OF_T", OSAI_ReadPriority.LOW);
                model.CreateArray(c => c.EXTR_MM,        4, "!EXTR_MM",    OSAI_ReadPriority.LOW);


                model.CreateVariable(c => c.AXIS_POSITION, OSAI_ReadPriority.HIGH, GetAxisPositionAsync);
                
                model.CreateVariable(c => c.MESSAGES, OSAI_ReadPriority.HIGH, GetMessagesAsync);
                model.CreateVariable(c => c.QUEUE, OSAI_ReadPriority.HIGH, GetJobQueuePreviewAsync);
                model.CreateVariable(c => c.STORAGE, OSAI_ReadPriority.MEDIUM, GetMCodeStorageAsync);
                model.CreateVariable(c => c.BOOT_PHASE, OSAI_ReadPriority.ULTRAHIGH, GetBootPhaseAsync);
                model.CreateVariable(c => c.RECOVERY, OSAI_ReadPriority.MEDIUM, GetJobRecoveryPreviewAsync);
                model.CreateVariable(c => c.PROCESS_STATUS, OSAI_ReadPriority.ULTRAHIGH, GetProcessStatusAsync);
                model.CreateVariable(c => c.MCODE_EVENT, OSAI_ReadPriority.HIGH, GetMCodeEventsAsync);
                model.CreateVariable(c => c.EXTRUSIONS, OSAI_ReadPriority.HIGH, GetExtrusionSetQueueAsync);
                model.CreateVariable(c => c.PROCESS_MODE, OSAI_ReadPriority.ULTRAHIGH, GetProcessModeAsync, SetProcessModeAsync);
                model.CreateVariable(c => c.BOOT_MODE, OSAI_ReadPriority.ULTRAHIGH, (_, _) => Task.FromResult(Optional.Some(Unit.Default)), SetBootModeAsync);
                model.CreateVariable(c => c.PROGRESS, OSAI_ReadPriority.ULTRAHIGH, GetProgressAsync);

                // GW VARIABLES                                                                                                                                                                                                                                                                              
                model.CreateVariable(c => c.TOOL_CUR, (OSAI_VARCODE.GW_CODE, 0));
                model.CreateVariable(c => c.TOOL_NUM, (OSAI_VARCODE.GW_CODE, 1));
                model.CreateVariable(c => c.DEBUG, (OSAI_VARCODE.GW_CODE, 2));
                model.CreateVariable(c => c.KEEP_CHAMBER, (OSAI_VARCODE.GW_CODE, 3));
                model.CreateVariable(c => c.KEEP_TOOL, (OSAI_VARCODE.GW_CODE, 4));

                model.CreateArray(c => c.MEM_TOOL_ON_TRAILER, 4, (OSAI_VARCODE.GW_CODE, 100));
                model.CreateArray(c => c.MEM_TOOL_IN_MAGAZINE, 4, (OSAI_VARCODE.GW_CODE, 101));

                // GD VARIABLES                                                                                                                                                                                                                                                                
                model.CreateArray(c => c.TEMP_WAIT, 3, (OSAI_VARCODE.GD_CODE, 0));
                model.CreateArray(c => c.PID_TOOL, 3, (OSAI_VARCODE.GD_CODE, 3), pid_unit);
                model.CreateArray(c => c.PID_CHAMBER, 3, (OSAI_VARCODE.GD_CODE, 6), pid_unit);
                model.CreateArray(c => c.PID_PLATE, 3, (OSAI_VARCODE.GD_CODE, 9), pid_unit);
                model.CreateArray(c => c.PID_RANGE, 3, (OSAI_VARCODE.GD_CODE, 12));
                model.CreateArray(c => c.TEMP_WINDOW, 3, (OSAI_VARCODE.GD_CODE, 15));


                model.CreateArray(c => c.FAN_ENABLE, 2, (OSAI_VARCODE.GD_CODE, 18));
                model.CreateArray(c => c.PURGE_POSITION, 2, (OSAI_VARCODE.GD_CODE, 21), axis_unit);
                model.CreateVariable(c => c.Y_SAFE_MAX, (OSAI_VARCODE.GD_CODE, 23));
                model.CreateVariable(c => c.Z_PROBE_MIN, (OSAI_VARCODE.GD_CODE, 24));
                model.CreateVariable(c => c.Z_TOOL_CORRECTION, (OSAI_VARCODE.GD_CODE, 25));
                model.CreateArray(c => c.TOOL_PROBE_POSITION, 2, (OSAI_VARCODE.GD_CODE, 28), axis_unit);
                model.CreateArray(c => c.HOME_OFFSET, 2, (OSAI_VARCODE.GD_CODE, 32), axis_unit);
                model.CreateArray(c => c.PLATE_PROBE_POSITION, 2, (OSAI_VARCODE.GD_CODE, 34), axis_unit);

                model.CreateVariable(c => c.PRESSURE_LEVEL, (OSAI_VARCODE.GD_CODE, 36));
                model.CreateVariable(c => c.VACUUM_LEVEL, (OSAI_VARCODE.GD_CODE, 37));

                model.CreateVariable(c => c.TOOL_OFF_TIME, (OSAI_VARCODE.GD_CODE, 38));
                model.CreateVariable(c => c.CHAMBER_OFF_TIME, (OSAI_VARCODE.GD_CODE, 39));

                // L VARIABLES                                                                                                                                                                                                                                                                         
                model.CreateArray(c => c.X_MAGAZINE_POS, 4, (OSAI_VARCODE.L_CODE, 1));
                model.CreateArray(c => c.Y_MAGAZINE_POS, 4, (OSAI_VARCODE.L_CODE, 18));

                // STRINGS
                model.CreateVariable(c => c.CUR_JOB, (OSAI_VARCODE.AA_CODE, 0), OSAI_ReadPriority.MEDIUM);
                model.CreateArray(c => c.TEMP_CHAMBER, 1, (OSAI_VARCODE.AA_CODE, 1), OSAI_ReadPriority.MEDIUM, (i, t) => $"M4140[{t}, 0]", chamber_unit);
                model.CreateArray(c => c.TEMP_PLATE, 1, (OSAI_VARCODE.AA_CODE, 2), OSAI_ReadPriority.MEDIUM, (i, t) => $"M4141[{t}, 0]", plate_unit);
                model.CreateArray(c => c.EXTR_KEY, 4, (OSAI_VARCODE.AA_CODE, 3), OSAI_ReadPriority.MEDIUM);
                model.CreateArray(c => c.TEMP_TOOL, 4, (OSAI_VARCODE.AA_CODE, 7), OSAI_ReadPriority.MEDIUM, (i, t) => $"M4104[{i + 1}, {t}, 0]");

                // INPUTS                                                                                                                                                                                                                                                                             
                model.CreateArray(c => c.LOCK_CLOSED, 2, (OSAI_VARCODE.MW_CODE, 10201), lock_unit);
                model.CreateArray(c => c.TOOL_ON_TRAILER, 4, (OSAI_VARCODE.MW_CODE, 10202));

                model.CreateArray(c => c.FILAMENT_BEFORE_GEAR, 4, (OSAI_VARCODE.MW_CODE, 10204));
                model.CreateArray(c => c.FILAMENT_AFTER_GEAR, 4, (OSAI_VARCODE.MW_CODE, 10205));
                model.CreateArray(c => c.FILAMENT_ON_HEAD, 4, (OSAI_VARCODE.MW_CODE, 10206));
                model.CreateArray(c => c.TOOL_IN_MAGAZINE, 4, (OSAI_VARCODE.MW_CODE, 10207));

                model.CreateArray(c => c.AXIS_ENDSTOP, 3, (OSAI_VARCODE.MW_CODE, 10209), axis_unit);
                model.CreateArray(c => c.AXIS_PROBE, 3, (OSAI_VARCODE.MW_CODE, 10210), probe_unit);

                model.CreateVariable(c => c.DRIVER_EMERGENCY, (OSAI_VARCODE.MW_CODE, 10211));
                model.CreateVariable<AnalogSensors.PSE540>(c => c.PRESSURE_PRESENCE, (OSAI_VARCODE.MW_CODE, 10212));
                model.CreateVariable<AnalogSensors.PSE541>(c => c.VACUUM_PRESENCE, (OSAI_VARCODE.MW_CODE, 10213));

                // OUTPUTS                                                                                                                                                                                                                                                                                    
                model.CreateVariable(c => c.AUX_ON, (OSAI_VARCODE.MW_CODE, 10016));

                model.CreateArray(c => c.ENABLE_DRIVERS, 2, (OSAI_VARCODE.MW_CODE, 10750), drivers_unit);
                model.CreateVariable(c => c.DISABLE_24V, (OSAI_VARCODE.MW_CODE, 10751));

                model.CreateArray(c => c.OPEN_LOCK, 2, (OSAI_VARCODE.MW_CODE, 10752), lock_unit);

                model.CreateVariable(c => c.CHAMBER_LIGHT, (OSAI_VARCODE.MW_CODE, 10753));
                model.CreateVariable(c => c.ENABLE_VACUUM, (OSAI_VARCODE.MW_CODE, 10754, 1));
                model.CreateVariable(c => c.OPEN_HEAD_CLAMP, (OSAI_VARCODE.MW_CODE, 10755));
                model.CreateVariable(c => c.ENABLE_HEAD_FAN, (OSAI_VARCODE.MW_CODE, 10756));
                model.CreateVariable(c => c.ENABLE_CHAMBER_FAN, (OSAI_VARCODE.MW_CODE, 10757));

                model.CreateArray(c => c.ENABLE_HOLDING_FAN, 4, (OSAI_VARCODE.MW_CODE, 10759));
                model.CreateArray(c => c.RAISE_MAGAZINE_PISTON, 4, (OSAI_VARCODE.MW_CODE, 10760));
                model.CreateArray(c => c.LOWER_MAGAZINE_PISTON, 4, (OSAI_VARCODE.MW_CODE, 10761));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Environment.Exit(0);
            }
        }

        private static async Task<Optional<FLUX_AxisPosition>> GetAxisPositionAsync(OSAI_ConnectionProvider connection_provider, CancellationToken ct)
        {
            var connection = connection_provider.Connection;
            var axis_position = await connection.GetAxesPositionAsync(OSAI_AxisPositionSelect.Absolute, ct);
            if (!axis_position.HasValue)
                return default;

            return axis_position;
        }
        private static async Task<Optional<FluxJobRecoveryPreview>> GetJobRecoveryPreviewAsync(OSAI_ConnectionProvider connection_provider, CancellationToken ct)
        {
            var connection = connection_provider.Connection;
            var queue_files = await connection.ListFilesAsync(c => c.QueuePath, ct);
            if (!queue_files.HasValue)
                return default;

            return queue_files.Value.GetJobRecoveryPreview();
        }
        private static async Task<Optional<MCodeStorage>> GetMCodeStorageAsync(OSAI_ConnectionProvider connection_provider, CancellationToken ct)
        {
            var connection = connection_provider.Connection;
            var queue_files = await connection.ListFilesAsync(c => c.StoragePath, ct);
            if (!queue_files.HasValue)
                return default;

            return queue_files.Value.GetMCodeStorage();
        }
        private static async Task<Optional<FluxJobQueuePreview>> GetJobQueuePreviewAsync(OSAI_ConnectionProvider connection_provider, CancellationToken ct)
        {
            var connection = connection_provider.Connection;
            var queue_files = await connection.ListFilesAsync(c => c.QueuePath, ct);
            if (!queue_files.HasValue)
                return default;

            return queue_files.Value.GetJobQueuePreview();
        }
        private static async Task<Optional<FLUX_FileList>> GetMessagesAsync(OSAI_ConnectionProvider connection_provider, CancellationToken ct)
        {
            var connection = connection_provider.Connection;
            return await connection.ListFilesAsync(c => c.MessageEventPath, ct);
        }
        private static async Task<Optional<ExtrusionSetQueuePreview<ExtrusionMM>>> GetExtrusionSetQueueAsync(OSAI_ConnectionProvider connection_provider, CancellationToken ct)
        {
            var connection = connection_provider.Connection;
            var queue_files = await connection.ListFilesAsync(c => c.ExtrusionEventPath, ct);
            if (!queue_files.HasValue)
                return default;

            return queue_files.Value.GetExtrusionSetQueue();
        }
        private static async Task<Optional<MCodeEventStoragePreview>> GetMCodeEventsAsync(OSAI_ConnectionProvider connection_provider, CancellationToken ct)
        {
            var connection = connection_provider.Connection;
            var queue_files = await connection.ListFilesAsync(c => c.JobEventPath, ct);
            if (!queue_files.HasValue)
                return default;

            return queue_files.Value.GetMCodeEvents();
        }
        private static async Task<Optional<MCodeProgress>> GetProgressAsync(OSAI_ConnectionProvider connection_provider, CancellationToken ct)
        {
            var get_pp_response = await connection_provider.Connection.TryEnqueueRequestAsync(
                (c, ct) => c.GetActivePartProgramAsync(new GetActivePartProgramRequest(OSAI_Connection.ProcessNumber)),
                r => r.Main, r => new(r.retval, r.ErrClass, r.ErrNum), FLUX_RequestPriority.Immediate, ct);

            if (!get_pp_response.HasValue)
                return default;

            var get_blk_num_response = await connection_provider.Connection.TryEnqueueRequestAsync(
                (c, ct) => c.GetBlkNumAsync(OSAI_Connection.ProcessNumber),
                r => r.Body, r => new(r.Body.retval, r.Body.ErrClass, r.Body.ErrNum), FLUX_RequestPriority.Immediate, ct);

            if (!get_blk_num_response.HasValue)
                return default;

            var filename = Path.GetFileNameWithoutExtension(get_pp_response.Value);
            if (!MCodeKey.TryParse(filename, out var mcode_key))
                return default(MCodeProgress);

            var block_nr = get_blk_num_response.Value.GetBlkNum.MainActBlk;
            var block_number = new BlockNumber(block_nr, BlockType.Line);
            
            return new MCodeProgress(mcode_key, block_number);
        }

        private static async Task<Optional<OSAI_BootPhase>> GetBootPhaseAsync(OSAI_ConnectionProvider connection_provider, CancellationToken ct)
        {
            var boot_phase_response = await connection_provider.Connection.TryEnqueueRequestAsync(
                (c, ct) => c.BootPhaseEnquiryAsync(new BootPhaseEnquiryRequest()),
                r => r.Phase, r => new(r.retval, r.ErrClass, r.ErrNum), FLUX_RequestPriority.Immediate, ct);

            if (!boot_phase_response.HasValue)
                return default;

            return (OSAI_BootPhase)boot_phase_response.Value;
        }
        private static async Task<bool> SetBootModeAsync(OSAI_ConnectionProvider connection_provider, OSAI_BootMode boot_mode, CancellationToken ct)
        {
            var boot_mode_response = await connection_provider.Connection.TryEnqueueRequestAsync(
                (c, ct) => c.BootModeAsync(new BootModeRequest((ushort)boot_mode)),
                r => new(r.retval, r.ErrClass, r.ErrNum), FLUX_RequestPriority.Immediate, ct);

            return boot_mode_response.Ok;
        }
        private static async Task<Optional<OSAI_ProcessMode>> GetProcessModeAsync(OSAI_ConnectionProvider connection_provider, CancellationToken ct)
        {
            var process_status_response = await connection_provider.Connection.TryEnqueueRequestAsync(
                (c, ct) => c.GetProcessStatusAsync(OSAI_Connection.ProcessNumber),
                r => r.Body, r => new(r.Body.retval, r.Body.ErrClass, r.Body.ErrNum), FLUX_RequestPriority.Immediate, ct);

            if (!process_status_response.HasValue)
                return default;

            return (OSAI_ProcessMode)process_status_response.Value.ProcStat.Mode;
        }
        private static async Task<Optional<FLUX_ProcessStatus>> GetProcessStatusAsync(OSAI_ConnectionProvider connection_provider, CancellationToken ct)
        {
            var process_status_response = await connection_provider.Connection.TryEnqueueRequestAsync(
                (c, ct) => c.GetProcessStatusAsync(OSAI_Connection.ProcessNumber),
                r => r.Body, r => new(r.Body.retval, r.Body.ErrClass, r.Body.ErrNum), FLUX_RequestPriority.Immediate, ct);

            if (!process_status_response.HasValue)
                return default;

            var status = (OSAI_ProcessStatus)process_status_response.Value.ProcStat.Status;
            return status switch
            {
                OSAI_ProcessStatus.NONE => FLUX_ProcessStatus.NONE,
                OSAI_ProcessStatus.IDLE => FLUX_ProcessStatus.IDLE,
                OSAI_ProcessStatus.CYCLE or 
                OSAI_ProcessStatus.HOLDA or
                OSAI_ProcessStatus.RUNH or
                OSAI_ProcessStatus.HRUN or 
                OSAI_ProcessStatus.RESET or 
                OSAI_ProcessStatus.WAIT or 
                OSAI_ProcessStatus.INPUT => FLUX_ProcessStatus.CYCLE,
                OSAI_ProcessStatus.ERRO => FLUX_ProcessStatus.ERROR,
                OSAI_ProcessStatus.EMERG => FLUX_ProcessStatus.EMERG,
                _ => FLUX_ProcessStatus.NONE,
            };
        }
        private static async Task<bool> SetProcessModeAsync(OSAI_ConnectionProvider connection_provider, OSAI_ProcessMode process_mode, CancellationToken ct)
        {
            var set_process_mode_response = await connection_provider.Connection.TryEnqueueRequestAsync(
                (c, ct) => c.SetProcessModeAsync(new SetProcessModeRequest(OSAI_Connection.ProcessNumber, (ushort)process_mode)),
                r => new(r.retval, r.ErrClass, r.ErrNum), FLUX_RequestPriority.Immediate, ct);

            if (!set_process_mode_response.Ok)
                return false;

            return await connection_provider.Connection.WaitProcessModeAsync(
                m => m == process_mode,
                TimeSpan.FromSeconds(0),
                TimeSpan.FromSeconds(0.1),
                TimeSpan.FromSeconds(0.2),
                TimeSpan.FromSeconds(1));
        }
    }
}
