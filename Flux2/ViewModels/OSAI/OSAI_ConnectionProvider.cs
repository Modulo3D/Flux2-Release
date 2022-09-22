using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
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
    public class OSAI_ConnectionProvider : FLUX_ConnectionProvider<OSAI_ConnectionProvider, OSAI_Connection, OSAI_MemoryBuffer, OSAI_VariableStore, OSAI_ConnectionPhase>
    {
        public FluxViewModel Flux { get; }

        public OSAI_ConnectionProvider(FluxViewModel flux) : base(flux,
            OSAI_ConnectionPhase.START_PHASE, OSAI_ConnectionPhase.END_PHASE, p => (int)p,
            c => new OSAI_MemoryBuffer(c), c => new OSAI_VariableStore(c))
        {
            Flux = flux;
            Flux.MCodes.WhenAnyValue(c => c.OperatorUSB)
                .ConvertOr(o => o.AdvancedSettings, () => false)
                .DistinctUntilChanged()
                .Subscribe(debug => WriteVariableAsync(m => m.DEBUG, debug));
        }
        protected override async Task RollConnectionAsync()
        {
            try
            {
                // PRELIMINARY PHASE
                switch (ConnectionPhase)
                {
                    // CONNECT TO PLC
                    case OSAI_ConnectionPhase.START_PHASE:
                        var plc_connected = await connect_plc_async();
                        if (plc_connected)
                            ConnectionPhase = OSAI_ConnectionPhase.SELECT_BOOT_MODE;
                        async Task<bool> connect_plc_async()
                        {
                            if (Connection.HasValue)
                            {
                                await Connection.Value.CloseAsync();
                                Connection.Value.Dispose();
                                Connection = default;
                            }

                            if (!Flux.NetProvider.PLCNetworkConnectivity)
                                return false;

                            var plc_address = Flux.SettingsProvider.CoreSettings.Local.PLCAddress;
                            if (!plc_address.HasValue || string.IsNullOrEmpty(plc_address.Value))
                            {
                                Flux.Messages.LogMessage(OSAI_ConnectResponse.CONNECT_INVALID_ADDRESS);
                                return false;
                            }
                            Connection = new OSAI_Connection(this, plc_address.Value);
                            return true;
                        }

                        break;

                    // INITIALIZE BOOT MODE
                    case OSAI_ConnectionPhase.SELECT_BOOT_MODE:
                        var boot_mode = await WriteVariableAsync(m => m.BOOT_MODE, OSAI_BootMode.RUN);
                        if (boot_mode)
                            ConnectionPhase = OSAI_ConnectionPhase.WAIT_SYSTEM_UP;
                        else
                            StartConnection();
                        break;

                    // INITIALIZE BOOT PHASE
                    case OSAI_ConnectionPhase.WAIT_SYSTEM_UP:
                        var boot_phase = await wait_system_up();
                        if (boot_phase)
                            ConnectionPhase = OSAI_ConnectionPhase.ENABLE_AUX_ON;
                        else
                            StartConnection();
                        async Task<bool> wait_system_up()
                        {
                            if (!Connection.HasValue)
                                return false;
                            return await Connection.Value.WaitBootPhaseAsync(
                                phase => phase == OSAI_BootPhase.SYSTEM_UP_PHASE,
                                TimeSpan.FromSeconds(0),
                                TimeSpan.FromSeconds(1),
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
                        var reset_plc = await ResetAsync();
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
                        var ref_axis = await Connection.ConvertOrAsync(c => c.AxesRefAsync('X', 'Y', 'Z', 'A'), () => false);
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

        public override async Task<bool> ParkToolAsync()
        {
            var position = await ReadVariableAsync(m => m.TOOL_CUR);
            if (!position.HasValue)
                return false;

            if (position.Value == 0)
                return false;

            using var put_park_tool_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var wait_park_tool_cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            return await ExecuteParamacroAsync(c => c.GetParkToolGCode(), put_park_tool_cts.Token, true, wait_park_tool_cts.Token);
        }
        public override async Task<bool> ResetClampAsync()
        {
            if (!await WriteVariableAsync(c => c.OPEN_HEAD_CLAMP, true))
                return false;
            if (!await WriteVariableAsync(c => c.TOOL_CUR, (short)0))
                return false;
            return true;
        }

        // TODO
        /*public override Optional<IEnumerable<string>> GenerateStartMCodeLines(MCode mcode)
        {
            return generate_start_mcode().ToOptional();
            IEnumerable<string> generate_start_mcode()
            {
                // Write preprocess gcode
                yield return "(GTO, end_preprocess)";

                var def_recovery = new OSAI_MCodeRecovery(mcode.MCodeGuid, false, 0, 0,
                    new Dictionary<VariableUnit, double>()
                    {
                        { "0", 0 },
                        { "1", 0 },
                        { "2", 0 },
                        { "3", 0 },
                    },
                    new Dictionary<VariableUnit, double>() 
                    {
                        { "X", 0 },
                        { "Y", 0 },
                        { "Z", 0 },
                        { "E", 0 },
                    });

                var recovery_mcode = Connection.Convert(c => c.GenerateRecoveryLines(def_recovery));
                if (recovery_mcode.HasValue)
                {
                    foreach (var recovery_move in recovery_mcode.Value)
                    {
                        var gcode_move = new GCodeMove(mcode, Optional<GCodeMove>.None, recovery_move);
                        var move = gcode_move.GetMove();
                        if (!move.HasValue)
                            continue;

                        if (!mcode.GCodeMoves.ContainsKey(move.Value))
                            mcode.GCodeMoves.Add(move.Value, gcode_move);
                    }
                }

                foreach (var gcode_move in mcode.GCodeMoves.Values)
                    yield return gcode_move.Line;

                yield return "\"end_preprocess\"";
                yield return "(PAS)";
            }
        }*/
        public override Optional<IEnumerable<string>> GenerateEndMCodeLines(MCode mcode, Optional<ushort> queue_size)
        {
            return default;
        }

        public override Optional<IEnumerable<string>> GenerateStartMCodeLines(MCode mcode)
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
                "(CLS, MACRO\\cancel_print)",
                "(CLS, MACRO\\home_printer)",
                "(CLS, MACRO\\change_tool, 0)",

                "G92 A0",
                "G1 X0 Y0 Z0 F1000",

                "\"end_preprocess\"",
                "(PAS)",
            };
        }
    }
}
