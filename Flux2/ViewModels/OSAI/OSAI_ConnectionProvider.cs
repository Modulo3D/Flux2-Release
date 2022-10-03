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
            c => new OSAI_VariableStore(c), c => new OSAI_Connection(flux, c), c => new OSAI_MemoryBuffer(c))
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
                        if (await Connection.ConnectAsync())
                            ConnectionPhase = OSAI_ConnectionPhase.SELECT_BOOT_MODE;
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
                            return await Connection.WaitBootPhaseAsync(
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
            if (!await WriteVariableAsync(c => c.TOOL_CUR, new ArrayIndex(-1)))
                return false;
            return true;
        }
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
