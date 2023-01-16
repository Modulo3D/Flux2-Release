using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
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

        public override async Task<bool> ResetClampAsync()
        {
            if (!await WriteVariableAsync(c => c.OPEN_HEAD_CLAMP, true))
                return false;
            if (!await WriteVariableAsync(c => c.TOOL_CUR, ArrayIndex.FromZeroBase(-1, VariableStoreBase)))
                return false;
            return true;
        }
        public override Optional<DateTime> ParseDateTime(string date_time)
        {
            var parts = date_time.Split(new[] { ';' }, 2);
            if (parts.Length != 2)
                return default;

            if (!DateOnly.TryParse(parts[0], out var date_only))
                return default;
            if (!TimeOnly.TryParse(parts[1], out var time_only))
                return default;

            return date_only.ToDateTime(time_only);
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
                        if (MemoryBuffer.HasFullMemoryRead)
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
        protected override (GCodeString start_compare, GCodeString end_compare) CompareQueuePosGCode(int queue_pos)
        {
            return ($"(IF, !QUEUE_POS = {queue_pos})", "(ENDIF)");
        }
        public override Optional<FluxJobRecovery> GetFluxJobRecoveryFromSource(FluxJob current_job, string source)
        {
            try
            {
                using var string_reader = new StringReader(source);

                // block nr
                var block_number = new BlockNumber(ulong.Parse(string_reader.ReadLine()), BlockType.Line);

                // tool
                var tool_index = ArrayIndex.FromArrayBase(ushort.Parse(string_reader.ReadLine()), VariableStore);

                // plate temps
                var plate_temperatures = new Dictionary<ArrayIndex, double>();
                var plate_count = GetArrayUnits(c => c.TEMP_PLATE).Count();
                for (ushort i = 0; i < plate_count; i++)
                    plate_temperatures.Add(ArrayIndex.FromZeroBase(i, VariableStore), double.Parse(string_reader.ReadLine(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture));

                // chamber temps
                var chamber_temperatures = new Dictionary<ArrayIndex, double>();
                var chamber_count = GetArrayUnits(c => c.TEMP_CHAMBER).Count();
                for (ushort i = 0; i < plate_count; i++)
                    chamber_temperatures.Add(ArrayIndex.FromZeroBase(i, VariableStore), double.Parse(string_reader.ReadLine(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture));

                // tool temps
                var extruders = Flux.SettingsProvider.ExtrudersCount;
                if (!extruders.HasValue)
                    return default;

                var tool_temperatures = new Dictionary<ArrayIndex, double>();
                for (ushort i = 0; i < extruders.Value.machine_extruders; i++)
                    tool_temperatures.Add(ArrayIndex.FromZeroBase(i, VariableStore), double.Parse(string_reader.ReadLine(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture));

                // pos
                var position = ImmutableDictionary<char, double>.Empty
                    .AddRange(new KeyValuePair<char, double>[] 
                    {
                        new ('X', double.Parse(string_reader.ReadLine(),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture)),
                        new ('Y', double.Parse(string_reader.ReadLine(),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture)),
                        new ('Z', double.Parse(string_reader.ReadLine(),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture)),
                        new ('A', double.Parse(string_reader.ReadLine(),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture)),
                    });

                // feedrate
                var feedrate = double.Parse(string_reader.ReadLine(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture);

                return new FluxJobRecovery()
                {
                    Feedrate = feedrate,
                    FluxJob = current_job,
                    ToolIndex = tool_index,
                    AxisPosition = position,
                    BlockNumber = block_number,
                    ToolTemperatures = tool_temperatures,
                    PlateTemperatures = plate_temperatures,
                    ChamberTemperatures = chamber_temperatures,
                };
            }
            catch (Exception)
            {
                return default;
            }
        }
    }
}
