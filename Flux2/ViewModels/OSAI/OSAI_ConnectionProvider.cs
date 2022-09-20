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
        INITIALIZED_CONNECTION = 1,
        INITIALIZED_BOOT_MODE = 2,
        INITIALIZED_BOOT_PHASE = 3,
        INITIALIZED_AXIS_ENABLE = 4,
        INITIALIZED_IS_RESET = 5,
        INITIALIZED_IS_AUTO = 6,
        INITIALIZED_AXIS_REFERENCE = 7,
        END_PHASE = 8
    }
    public class OSAI_ConnectionProvider : FLUX_ConnectionProvider<OSAI_ConnectionProvider, OSAI_Connection, OSAI_MemoryBuffer, OSAI_VariableStore>
    {
        private ObservableAsPropertyHelper<Optional<bool>> _IsInitializing;
        public override Optional<bool> IsInitializing => _IsInitializing.Value;

        private ObservableAsPropertyHelper<double> _ConnectionProgress;
        public override double ConnectionProgress => _ConnectionProgress.Value;

        private ObservableAsPropertyHelper<Optional<bool>> _IsConnecting;
        public override Optional<bool> IsConnecting => _IsConnecting.Value;

        private Optional<OSAI_ConnectionPhase> _ConnectionPhase;
        protected Optional<OSAI_ConnectionPhase> ConnectionPhase
        {
            get => _ConnectionPhase;
            set => this.RaiseAndSetIfChanged(ref _ConnectionPhase, value);
        }

        public FluxViewModel Flux { get; }
        public override IFlux IFlux => Flux;
        public override OSAI_MemoryBuffer MemoryBuffer { get; }
        public override OSAI_VariableStore VariableStore { get; }

        public OSAI_ConnectionProvider(FluxViewModel flux)
        {
            Flux = flux;
            VariableStore = new OSAI_VariableStore(this);

            var full_memory_read = MemoryBuffer.WhenAnyValue(v => v.HasFullMemoryRead);

            _IsInitializing = Observable.CombineLatest(
                this.WhenAnyValue(c => c.ConnectionPhase), full_memory_read,
                (phase, full_read) => phase.Convert(p => p < OSAI_ConnectionPhase.END_PHASE || !full_read))
                .ToProperty(this, v => v.IsInitializing);

            _IsConnecting = Observable.CombineLatest(
                this.WhenAnyValue(c => c.ConnectionPhase), full_memory_read,
                (phase, full_read) => phase.Convert(p => p < OSAI_ConnectionPhase.END_PHASE || !full_read))
                .ToProperty(this, v => v.IsConnecting);

            var connection_max_value = (double)OSAI_ConnectionPhase.END_PHASE;

            _ConnectionProgress =
                this.WhenAnyValue(v => v.ConnectionPhase)
                .Select(p => (double)p.ValueOr(() => OSAI_ConnectionPhase.START_PHASE) / connection_max_value * 100)
                .ToProperty(this, v => v.ConnectionProgress);

            Flux.NetProvider
                .WhenAnyValue(n => n.PLCNetworkConnectivity)
                .Where(c => c == false)
                .Subscribe(_ => StartConnection());

            Flux.MCodes.WhenAnyValue(c => c.OperatorUSB)
                .ConvertOr(o => o.AdvancedSettings, () => false)
                .DistinctUntilChanged()
                .Subscribe(debug => WriteVariableAsync(m => m.DEBUG, debug));
        }

        public override void Initialize()
        {
            var debug_t = DateTime.Now;
            var status_t = DateTime.Now;
            var network_t = DateTime.Now;
            var full_memory_t = DateTime.Now;
            var memory_buffer_t = DateTime.Now;

            var plc_variables = VariableStore.Variables.Values
                .SelectMany(var =>
                {
                    return var switch
                    {
                        IFLUX_Array array => array.Variables.Items,
                        IFLUX_Variable variable => new[] { variable },
                        _ => Array.Empty<IFLUX_Variable>(),
                    };
                })
                .Where(v => v is IOSAI_AsyncVariable)
                .Select(v => v as IOSAI_AsyncVariable)
                .GroupBy(v => v.Priority)
                .ToDictionary(group => group.Key, group => group.ToList());

            var memory_priorities_t = plc_variables
                .ToDictionary(kvp => kvp.Key, kvp => DateTime.Now);

            var memory_times = new Dictionary<OSAI_ReadPriority, TimeSpan>()
            {
                { OSAI_ReadPriority.LOW, OSAI_Connection.LowPriority },
                { OSAI_ReadPriority.HIGH, OSAI_Connection.HighPriority },
                { OSAI_ReadPriority.MEDIUM, OSAI_Connection.MediumPriority },
                { OSAI_ReadPriority.ULTRALOW, OSAI_Connection.UltraLowPriority},
                { OSAI_ReadPriority.ULTRAHIGH, OSAI_Connection.UltraHighPriority },
            };

            DisposableThread.Start(async () =>
            {
                try
                {
                    if (DateTime.Now - status_t >= TimeSpan.FromMilliseconds(100))
                    { 
                        await RollConnectionAsync();
                        status_t = DateTime.Now;
                    }

                    if (DateTime.Now - network_t >= TimeSpan.FromSeconds(10))
                    { 
                        Flux.NetProvider.UpdateNetworkState();
                        network_t = DateTime.Now;
                    }

                    if (DateTime.Now - debug_t >= TimeSpan.FromSeconds(5))
                    {
                        Flux.MCodes.FindDrive();
                        debug_t = DateTime.Now;
                    }

                    if (ConnectionPhase.HasValue && ConnectionPhase.Value >= OSAI_ConnectionPhase.INITIALIZED_BOOT_PHASE)
                    {
                        if (DateTime.Now - memory_buffer_t >= TimeSpan.FromMilliseconds(100))
                            await update_memory_buffers();

                        foreach (var variable_group in plc_variables)
                            await update_variable_group(variable_group);

                        if (DateTime.Now - full_memory_t >= OSAI_Connection.UltraLowPriority)
                            MemoryBuffer.HasFullMemoryRead = true;
                    }
                    else
                    {
                        MemoryBuffer.HasFullMemoryRead = false;
                    }
                }
                catch (Exception ex)
                { 
                }
            }, TimeSpan.Zero);

            async Task update_memory_buffers()
            {
                if (Connection.HasValue)
                    await MemoryBuffer.UpdateBufferAsync();
                memory_buffer_t = DateTime.Now;
            }
            async Task update_variable_group(KeyValuePair<OSAI_ReadPriority, List<IOSAI_AsyncVariable>> variable_group)
            {
                var memory_time = memory_times[variable_group.Key];
                var memory_priority_t = memory_priorities_t[variable_group.Key];
                if (DateTime.Now - memory_priority_t >= memory_time)
                {
                    foreach (var variable in variable_group.Value)
                        await variable.UpdateAsync();
                    memory_priorities_t[variable_group.Key] = DateTime.Now;
                }
            }
        }
        public override void StartConnection()
        {
            ConnectionPhase = OSAI_ConnectionPhase.START_PHASE;
        }
        protected override async Task RollConnectionAsync()
        {
            try
            {
                if (!ConnectionPhase.HasValue)
                    StartConnection();

                // PRELIMINARY PHASE
                switch (ConnectionPhase.Value)
                {
                    // CONNECT TO PLC
                    case OSAI_ConnectionPhase.START_PHASE:
                        var plc_connected = await connect_plc_async();
                        if (plc_connected)
                            ConnectionPhase = OSAI_ConnectionPhase.INITIALIZED_CONNECTION;
                        async Task<bool> connect_plc_async()
                        {
                            if (Connection.HasValue)
                            {
                                await Connection.Value.CloseAsync();
                                Connection.Value.Dispose();
                                Connection = default;
                            }

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
                    case OSAI_ConnectionPhase.INITIALIZED_CONNECTION:
                        var boot_mode = await WriteVariableAsync(m => m.BOOT_MODE, OSAI_BootMode.RUN);
                        if (boot_mode)
                            ConnectionPhase = OSAI_ConnectionPhase.INITIALIZED_BOOT_MODE;
                        else
                            StartConnection();
                        break;

                    // INITIALIZE BOOT PHASE
                    case OSAI_ConnectionPhase.INITIALIZED_BOOT_MODE:
                        var boot_phase = await wait_system_up();
                        if (boot_phase)
                            ConnectionPhase = OSAI_ConnectionPhase.INITIALIZED_BOOT_PHASE;
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

                    // END PHASE
                    case OSAI_ConnectionPhase.END_PHASE:
                        return;
                }

                if (ConnectionPhase.HasValue && ConnectionPhase.Value >= OSAI_ConnectionPhase.INITIALIZED_BOOT_PHASE)
                {
                    var process = await ReadVariableAsync(m => m.PROCESS_STATUS);
                    if (process.HasValue && process.Value == FLUX_ProcessStatus.CYCLE)
                    {
                        ConnectionPhase = OSAI_ConnectionPhase.END_PHASE;
                        Flux.Messages.LogMessage(OSAI_SetupResponse.SETUP_SUCCESS);
                        return;
                    }

                    // Resets variables
                    if (HasVariable(m => m.WATCH_VACUUM) && !await WriteVariableAsync(m => m.WATCH_VACUUM, false))
                    {
                        StartConnection();
                        Flux.Messages.LogMessage(m => m.WATCH_VACUUM, false);
                        return;
                    }
                }

                if (!ConnectionPhase.HasValue)
                {
                    StartConnection();
                    return;
                }

                switch (ConnectionPhase.Value)
                {
                    // ACTIVATE AUX
                    case OSAI_ConnectionPhase.INITIALIZED_BOOT_PHASE:
                        Flux.Messages.Messages.Clear();
                        var activate_aux = await WriteVariableAsync(m => m.AUX_ON, true);
                        if (activate_aux)
                            ConnectionPhase = OSAI_ConnectionPhase.INITIALIZED_AXIS_ENABLE;
                        else
                            ConnectionPhase = OSAI_ConnectionPhase.INITIALIZED_BOOT_PHASE;
                        break;

                    // RESETS PLC
                    case OSAI_ConnectionPhase.INITIALIZED_AXIS_ENABLE:
                        var reset_plc = await ResetAsync();
                        if (reset_plc)
                            ConnectionPhase = OSAI_ConnectionPhase.INITIALIZED_IS_RESET;
                        else
                            ConnectionPhase = OSAI_ConnectionPhase.INITIALIZED_BOOT_PHASE; break;

                    // SET AUTO
                    case OSAI_ConnectionPhase.INITIALIZED_IS_RESET:
                        var set_auto = await WriteVariableAsync(m => m.PROCESS_MODE, OSAI_ProcessMode.AUTO);
                        if (set_auto)
                            ConnectionPhase = OSAI_ConnectionPhase.INITIALIZED_IS_AUTO;
                        else
                            ConnectionPhase = OSAI_ConnectionPhase.INITIALIZED_BOOT_PHASE;
                        break;

                    // REFERENCE ALL AXIS
                    case OSAI_ConnectionPhase.INITIALIZED_IS_AUTO:
                        var ref_axis = await Connection.ConvertOrAsync(c => c.AxesRefAsync('X', 'Y', 'Z', 'A'), () => false);
                        if (ref_axis)
                            ConnectionPhase = OSAI_ConnectionPhase.INITIALIZED_AXIS_REFERENCE;
                        else
                            ConnectionPhase = OSAI_ConnectionPhase.INITIALIZED_BOOT_PHASE;
                        break;

                    case OSAI_ConnectionPhase.INITIALIZED_AXIS_REFERENCE:
                        ConnectionPhase = OSAI_ConnectionPhase.END_PHASE;
                        Flux.Messages.LogMessage(OSAI_SetupResponse.SETUP_SUCCESS);
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
