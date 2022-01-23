using DynamicData;
using DynamicData.Kernel;
using ICSharpCode.SharpZipLib.Zip;
using Modulo3DStandard;
using ReactiveUI;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public enum RRF_ConnectionPhase
    {
        START_PHASE = 0,
        DISCONNECTING_CLIENT = 1,
        CONNECTING_CLIENT = 2,
        READING_STATUS = 3,
        CREATING_VARIABLES = 4,
        INITIALIZING_VARIABLES = 5,
        READING_MEMORY = 6,
        END_PHASE = 7,
    }

    public class RRF_ConnectionProvider : FLUX_ConnectionProvider<RRF_Connection, RRF_VariableStore>
    {
        public override OffsetKind OffsetKind => OffsetKind.ToolOrigin;

        public CancellationTokenSource CTS { get; private set; }
        
        private ObservableAsPropertyHelper<Optional<bool>> _IsConnecting;
        public override Optional<bool> IsConnecting => _IsConnecting.Value;

        private ObservableAsPropertyHelper<Optional<bool>> _IsInitializing;
        public override Optional<bool> IsInitializing => _IsInitializing.Value;

        private ObservableAsPropertyHelper<double> _ConnectionProgress;
        public override double ConnectionProgress => _ConnectionProgress.Value;

        private Optional<RRF_ConnectionPhase> _ConnectionPhase;
        protected Optional<RRF_ConnectionPhase> ConnectionPhase
        {
            get => _ConnectionPhase;
            set => this.RaiseAndSetIfChanged(ref _ConnectionPhase, value);
        }

        public FluxViewModel Flux { get; }
        public override IFlux IFlux => Flux;

        public RRF_ConnectionProvider(FluxViewModel flux)
        {
            Flux = flux;
            VariableStore = new RRF_VariableStore(this);

            var connection = this.WhenAnyValue(v => v.Connection);
            var full_memory_read = connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(b => b.WhenAnyValue(v => v.HasFullMemoryRead))
                .ValueOr(() => false);

            _IsInitializing = Observable.CombineLatest(
                this.WhenAnyValue(c => c.ConnectionPhase), full_memory_read,
                (phase, full_read) => phase.Convert(p => p < RRF_ConnectionPhase.END_PHASE || !full_read))
                .ToProperty(this, v => v.IsInitializing);

            _IsConnecting = Observable.CombineLatest(
                this.WhenAnyValue(c => c.ConnectionPhase), full_memory_read,
                (phase, full_read) => phase.Convert(p => p < RRF_ConnectionPhase.END_PHASE || !full_read))
                .ToProperty(this, v => v.IsConnecting);

            var connection_max_value = (double)RRF_ConnectionPhase.END_PHASE;

            _ConnectionProgress =
                this.WhenAnyValue(v => v.ConnectionPhase)
                .Select(p => (double)p.ValueOr(() => RRF_ConnectionPhase.START_PHASE) / connection_max_value * 100)
                .ToProperty(this, v => v.ConnectionProgress);
        }

        public override void Initialize()
        {
            var debug_t = DateTime.Now;
            var status_t = DateTime.Now;
            var network_t = DateTime.Now;
            var memory_buffer_t = DateTime.Now;
            
            StartConnection();
            DisposableTask.Start(async time =>
            {
                try
                {
                    if (DateTime.Now - status_t >= TimeSpan.FromMilliseconds(100))
                    {
                        await RollConnectionAsync();
                        status_t = DateTime.Now;
                    }

                    if (DateTime.Now - network_t >= TimeSpan.FromSeconds(5))
                    {
                        await Flux.NetProvider.UpdateNetworkStateAsync();
                        if (Flux.NetProvider.PLCNetworkConnectivity.ConvertOr(plc => !plc, () => false))
                            StartConnection();
                        network_t = DateTime.Now;
                    }

                    if (DateTime.Now - debug_t >= TimeSpan.FromSeconds(5))
                    {               
                        Flux.MCodes.FindDrive();
                        var debug = Flux.MCodes.OperatorUSB.ConvertOr(o => o.AdvancedSettings, () => false);
                        var debug_plc = await ReadVariableAsync(m => m.DEBUG);
                        if(debug_plc.HasValue && debug != debug_plc)
                            await WriteVariableAsync(m => m.DEBUG, debug);
                        debug_t = DateTime.Now;
                    }

                    if (Connection.HasValue && ConnectionPhase.HasValue && ConnectionPhase.Value > RRF_ConnectionPhase.INITIALIZING_VARIABLES)
                        Connection.Value.MemoryBuffer.UpdateBuffer();
                }
                catch
                { 
                }

            }, TimeSpan.Zero, RxApp.TaskpoolScheduler);
        }
        public override void StartConnection()
        {
            try
            {
                CTS?.Cancel();
            }
            catch
            {
            }
            finally
            {
                try
                {

                    CTS?.Dispose();
                    ConnectionPhase = RRF_ConnectionPhase.START_PHASE;
                }
                catch
                {
                }
            }
        }
        protected override async Task RollConnectionAsync()
        {
            try
            {
                if (!ConnectionPhase.HasValue)
                    ConnectionPhase = RRF_ConnectionPhase.START_PHASE;

                switch (ConnectionPhase.Value)
                {
                    case RRF_ConnectionPhase.START_PHASE:
                        await CreateTimeoutAsync(TimeSpan.FromSeconds(5), async ct =>
                        {
                            if (Connection.HasValue)
                            { 
                                Connection.Value.Dispose();
                                Connection = default;
                            }

                            Connection = new RRF_Connection(Flux, VariableStore);

                            var plc_connected = await connect_plc_async();
                            if (plc_connected)
                                ConnectionPhase = RRF_ConnectionPhase.DISCONNECTING_CLIENT;
                        });
                        async Task<bool> connect_plc_async()
                        {
                            if (Connection == default)
                                return false;

                            var plc_address = Flux.SettingsProvider.CoreSettings.Local.PLCAddress;
                            if (!plc_address.HasValue || string.IsNullOrEmpty(plc_address.Value))
                            {
                                Flux.Messages.LogMessage(OSAI_ConnectResponse.CONNECT_INVALID_ADDRESS);
                                return false;
                            }
                            if(Connection.HasValue)
                                return await Connection.Value.CreateClientAsync($"http://{plc_address.Value}/");
                            return false;
                        }
                        break;

                    case RRF_ConnectionPhase.DISCONNECTING_CLIENT:
                        await CreateTimeoutAsync(TimeSpan.FromSeconds(5), async ct =>
                        {
                            var disconnected = await Connection.Value.PutRequestAsync(new RestRequest("rr_disconnect", Method.Get), IRRF_RequestPriority.Immediate, ct: ct);
                            ConnectionPhase = disconnected ? RRF_ConnectionPhase.CONNECTING_CLIENT : RRF_ConnectionPhase.START_PHASE;
                        });
                        break;

                    case RRF_ConnectionPhase.CONNECTING_CLIENT:
                        await CreateTimeoutAsync(TimeSpan.FromSeconds(5), async ct =>
                        {
                            var connected = await Connection.Value.PutRequestAsync(new RestRequest("rr_connect?password=\"\"", Method.Get), IRRF_RequestPriority.Immediate, ct: ct);
                            ConnectionPhase = connected ? RRF_ConnectionPhase.READING_STATUS : RRF_ConnectionPhase.START_PHASE;
                        });
                        break;

                    case RRF_ConnectionPhase.READING_STATUS:
                        Flux.Messages.Messages.Clear();
                        await CreateTimeoutAsync(TimeSpan.FromSeconds(5), async ct =>
                        {
                            var state = await Connection.Value.MemoryBuffer.GetModelDataAsync<RRF_ObjectModelState>("state", IRRF_RequestPriority.Immediate, ct);
                            if (!state.HasValue)
                                return;
                            var status = state.Value.GetProcessStatus();
                            if (!status.HasValue)
                                return;
                            if (status.Value == FLUX_ProcessStatus.CYCLE)
                                ConnectionPhase = RRF_ConnectionPhase.READING_MEMORY;
                            else
                                ConnectionPhase = RRF_ConnectionPhase.CREATING_VARIABLES;
                        });
                        break;

                    case RRF_ConnectionPhase.CREATING_VARIABLES:
                        await CreateTimeoutAsync(TimeSpan.FromSeconds(10), async ct =>
                        {
                            var result = await Connection.Value.CreateVariablesAsync(ct);
                            if(result)
                                ConnectionPhase = RRF_ConnectionPhase.INITIALIZING_VARIABLES;
                        });
                        break;

                    case RRF_ConnectionPhase.INITIALIZING_VARIABLES:
                        await CreateTimeoutAsync(TimeSpan.FromSeconds(10), async ct =>
                        {
                            var result = await Connection.Value.InitializeVariablesAsync(ct);
                            if (result)
                                ConnectionPhase = RRF_ConnectionPhase.READING_MEMORY;
                        });
                        break;

                    case RRF_ConnectionPhase.READING_MEMORY:
                        if (Connection.Value.MemoryBuffer.HasFullMemoryRead)
                            ConnectionPhase = RRF_ConnectionPhase.END_PHASE;
                        break;

                    case RRF_ConnectionPhase.END_PHASE:
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
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            return await ExecuteParamacroAsync(c => new[]
            {
                "G28 C0",
                "T-1 P0"
            }, true, cts.Token);
        }
        private async Task CreateTimeoutAsync(TimeSpan timeout, Func<CancellationToken, Task> func)
        {
            try
            {
                CTS?.Cancel();
            }
            catch
            {
            }
            finally 
            {
                try
                {
                    CTS?.Dispose();
                    CTS = new CancellationTokenSource(timeout);
                    await func(CTS.Token);
                }
                catch 
                {
                }
            }
        }
        public override Optional<IEnumerable<string>> GenerateStartMCodeLines(MCode mcode)
        {
            return default;
        }
        public override Optional<IEnumerable<string>> GenerateEndMCodeLines(MCode mcode)
        {
            var end_lines = new List<string>
            {
                $"G1 Z290 F1000",
                $"M106 F0 S255",
                $"M190 R65",
                $"M106 F0 S0",
                $"G1 U300 F1500",
                $"G28 U0",
                $"G1 Z280 F1000",
                $"set global.queue_pos = global.queue_pos + 1",
                $"if (!exists(var.next_job))",
                $"    var next_job = \"queue/inner/start_\"^floor(global.queue_pos)^\".g\"",
                $"    M32 {{var.next_job}}",
                $"else",
                $"    set var.next_job = \"queue/inner/start_\"^floor(global.queue_pos)^\".g\"",
                $"    M32 {{var.next_job}}"
            };
            return end_lines;
        }
        public override Optional<IEnumerable<string>> GenerateRecoveryMCodeLines(MCodeRecovery recovery)
        {
            return default;
        }

        public override async Task<bool> ParkToolAsync()
        {
            var position = await ReadVariableAsync(m => m.TOOL_CUR);
            if (!position.HasValue)
                return false;

            if (position.Value == -1)
                return false;

            if (!await MGuard_MagazinePositionAsync((ushort)position.Value))
                return false;

            return await ExecuteParamacroAsync(c => c.GetParkToolGCode());
        }
    }
}
