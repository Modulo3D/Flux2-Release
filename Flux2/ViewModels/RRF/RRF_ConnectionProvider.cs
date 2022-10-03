using DynamicData;
using DynamicData.Kernel;
using GreenSuperGreen.Queues;
using Modulo3DStandard;
using ReactiveUI;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Reflection;
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

    public class RRF_ConnectionProvider : FLUX_ConnectionProvider<RRF_ConnectionProvider, RRF_Connection, RRF_MemoryBuffer, RRF_VariableStoreBase, RRF_ConnectionPhase>
    {
        public FluxViewModel Flux { get; }

        public RRF_ConnectionProvider(FluxViewModel flux, Func<RRF_ConnectionProvider, RRF_VariableStoreBase> get_variable_store) : base(flux,
            RRF_ConnectionPhase.START_PHASE, RRF_ConnectionPhase.END_PHASE, p => (int)p,
            get_variable_store, c => new RRF_Connection(flux, c), c => new RRF_MemoryBuffer(c))
        {
            Flux = flux;
        }
        protected override async Task RollConnectionAsync()
        {
            try
            {
                switch (ConnectionPhase)
                {
                    case RRF_ConnectionPhase.START_PHASE:
                        await CreateTimeoutAsync(TimeSpan.FromSeconds(5), async ct =>
                        {
                            if(await Connection.ConnectAsync())
                                ConnectionPhase = RRF_ConnectionPhase.DISCONNECTING_CLIENT;
                        });
                        break;

                    case RRF_ConnectionPhase.DISCONNECTING_CLIENT:
                        await CreateTimeoutAsync(TimeSpan.FromSeconds(1), async ct =>
                        {
                            var request = new RRF_Request("rr_disconnect", Method.Get, RRF_RequestPriority.Immediate, ct);
                            var disconnected = await Connection.ExecuteAsync(request);
                            ConnectionPhase = disconnected.Ok ? RRF_ConnectionPhase.CONNECTING_CLIENT : RRF_ConnectionPhase.START_PHASE;
                        });
                        break;

                    case RRF_ConnectionPhase.CONNECTING_CLIENT:
                        await CreateTimeoutAsync(TimeSpan.FromSeconds(1), async ct =>
                        {
                            var request = new RRF_Request($"rr_connect?password=\"\"&time={DateTime.UtcNow:O}", Method.Get, RRF_RequestPriority.Immediate, ct);
                            var connected = await Connection.ExecuteAsync(request);
                            ConnectionPhase = connected.Ok ? RRF_ConnectionPhase.READING_STATUS : RRF_ConnectionPhase.START_PHASE;
                        });
                        break;

                    case RRF_ConnectionPhase.READING_STATUS:
                        Flux.Messages.Messages.Clear();
                        await CreateTimeoutAsync(TimeSpan.FromSeconds(1), async ct =>
                        {
                            var state = await MemoryBuffer.GetModelDataAsync<RRF_ObjectModelState>(ct);
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
                        await CreateTimeoutAsync(TimeSpan.FromSeconds(5), async ct =>
                        {
                            var result = await Connection.CreateVariablesAsync(ct);
                            if (result)
                                ConnectionPhase = RRF_ConnectionPhase.INITIALIZING_VARIABLES;
                        });
                        break;

                    case RRF_ConnectionPhase.INITIALIZING_VARIABLES:
                        await CreateTimeoutAsync(TimeSpan.FromSeconds(5), async ct =>
                        {
                            var result = await Connection.InitializeVariablesAsync(ct);
                            if (result)
                                ConnectionPhase = RRF_ConnectionPhase.READING_MEMORY;
                        });
                        break;

                    case RRF_ConnectionPhase.READING_MEMORY:
                        if (MemoryBuffer.HasFullMemoryRead)
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
            using var put_reset_clamp_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var wait_reset_clamp_cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            return await ExecuteParamacroAsync(c => new[]
            {
                "G28 C0",
                "T-1 P0"
            }, put_reset_clamp_cts.Token, true, wait_reset_clamp_cts.Token);
        }
        private static async Task CreateTimeoutAsync(TimeSpan timeout, Func<CancellationToken, Task> func)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeout);
                await func(cts.Token);
            }
            catch
            {
            }
        }

        public override Optional<IEnumerable<string>> GenerateEndMCodeLines(MCode mcode, Optional<ushort> queue_size)
        {
            if (!queue_size.HasValue || queue_size.Value <= 1)
                return new[] { "M98 P\"/macros/cancel_print\"" };
            return new[] { "M98 P\"/macros/unload_print\"" };
        }

        public override Optional<IEnumerable<string>> GenerateStartMCodeLines(MCode mcode)
        {
            return default;
        }
    }
}
