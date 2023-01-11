using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Net.Http;
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
        INITIALIZING_VARIABLES = 4,
        READING_MEMORY = 6,
        END_PHASE = 7,
    }

    public class RRF_ConnectionProvider : FLUX_ConnectionProvider<RRF_ConnectionProvider, RRF_Connection, RRF_MemoryBuffer, RRF_VariableStoreBase, RRF_ConnectionPhase>
    {
        public FluxViewModel Flux { get; }
        public static string SystemPath => RRF_Connection.SystemPath;

        public RRF_ConnectionProvider(FluxViewModel flux, Func<RRF_ConnectionProvider, RRF_VariableStoreBase> get_variable_store) : base(flux,
            RRF_ConnectionPhase.START_PHASE, RRF_ConnectionPhase.END_PHASE, p => (int)p,
            get_variable_store, c => new RRF_Connection(flux, c), c => new RRF_MemoryBuffer(c))
        {
            Flux = flux;
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
        public override Optional<DateTime> ParseDateTime(string date_time_str)
        {
            if (!DateTime.TryParse(date_time_str, out var date_time))
                return default;
            return date_time;
        }
        protected override async Task RollConnectionAsync(CancellationToken ct)
        {
            try
            {
                switch (ConnectionPhase)
                {
                    case RRF_ConnectionPhase.START_PHASE:
                        if (await Connection.ConnectAsync())
                            ConnectionPhase = RRF_ConnectionPhase.DISCONNECTING_CLIENT;
                        break;

                    case RRF_ConnectionPhase.DISCONNECTING_CLIENT:
                        var request = new RRF_Request<string>("rr_disconnect", HttpMethod.Get, RRF_RequestPriority.Immediate, ct);
                        var disconnected = await Connection.ExecuteAsync(request);
                        ConnectionPhase = disconnected.Ok ? RRF_ConnectionPhase.CONNECTING_CLIENT : RRF_ConnectionPhase.START_PHASE;
                        break;

                    case RRF_ConnectionPhase.CONNECTING_CLIENT:
                        request = new RRF_Request<string>($"rr_connect?password=\"\"&time={DateTime.Now}", HttpMethod.Get, RRF_RequestPriority.Immediate, ct);
                        var connected = await Connection.ExecuteAsync(request);
                        ConnectionPhase = connected.Ok ? RRF_ConnectionPhase.READING_STATUS : RRF_ConnectionPhase.START_PHASE;
                        break;

                    case RRF_ConnectionPhase.READING_STATUS:
                        Flux.Messages.Messages.Clear();
                        var state = await MemoryBuffer.GetModelDataAsync(m => m.State, ct);
                        if (!state.HasValue)
                            return;
                        var status = state.Value.GetProcessStatus();
                        if (!status.HasValue)
                            return;
                        if (status.Value == FLUX_ProcessStatus.CYCLE)
                            ConnectionPhase = RRF_ConnectionPhase.READING_MEMORY;
                        else
                            ConnectionPhase = RRF_ConnectionPhase.INITIALIZING_VARIABLES;
                        break;

                    case RRF_ConnectionPhase.INITIALIZING_VARIABLES:
                        var init_var_result = await Connection.InitializeVariablesAsync(ct);
                        if (init_var_result)
                            ConnectionPhase = RRF_ConnectionPhase.READING_MEMORY;
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
        public override Optional<FluxJobRecovery> GetFluxJobRecoveryFromSource(MCodeKey mcode, string source)
        {
            try
            {
                var tool_index = source
                     .Match(/* language = regex */ "(?m)^T(?<tool_nr>\\S*)")
                     .Convert(m => m.Lookup("tool_nr", nr => ArrayIndex.FromArrayBase(short.Parse(nr), VariableStore)));
                if (!tool_index.HasValue)
                    return default;

                var block_number = source
                    .Match(/* language = regex */ "(?m)^M26 S(?<block_number>\\S*)")
                    .Convert(m => m.Lookup("block_number", nr => BlockNumber.Parse(nr, BlockType.Byte)));
                if (!block_number.HasValue)
                    return default;

                var tools_temp = source
                    .Matches(/* language = regex */ "(?m)^G10 P(?<tool_nr>\\S*) S(?<tool_temp>\\S*)")
                    .Convert(m => m.ToDictionary(
                        "tool_nr", nr => ArrayIndex.FromArrayBase(short.Parse(nr), VariableStore),
                        "tool_temp", t => double.Parse(t, CultureInfo.InvariantCulture)));
                if (!tools_temp.HasValue)
                    return default;

                var plates_temp = source
                    .Matches(/* language = regex */ "(?m)^M140 P(?<plate_nr>\\S*) S(?<plate_temp>\\S*)")
                    .Convert(m => m.ToDictionary(
                        "plate_nr", nr => ArrayIndex.FromArrayBase(short.Parse(nr), VariableStore),
                        "plate_temp", t => double.Parse(t, CultureInfo.InvariantCulture)))
                    .ValueOr(() => new Dictionary<ArrayIndex, double>());

                var chamber_temp = source
                    .Matches(/* language = regex */ "(?m)^M141 P(?<chamber_nr>\\S*) S(?<chamber_temp>\\S*)")
                    .Convert(m => m.ToDictionary(
                        "chamber_nr", nr => ArrayIndex.FromArrayBase(short.Parse(nr), VariableStore),
                        "chamber_temp", t => double.Parse(t, CultureInfo.InvariantCulture)))
                    .ValueOr(() => new Dictionary<ArrayIndex, double>());

                var feedrate = source
                    .Match(/* language = regex */ "(?m)^G1 F(?<feedrate>\\S*)")
                    .Convert(m => m.Lookup("feedrate", f => double.Parse(f, CultureInfo.InvariantCulture)));
                if (!feedrate.HasValue)
                    return default;

                var z_pos = source
                    .Matches(/* language = regex */ "(?m)^G0 F(?<feedrate>\\S*) Z(?<z_pos>\\S*)")
                    .Convert(m => m.LastOrDefault().Lookup("z_pos", x => double.Parse(x, CultureInfo.InvariantCulture)));

                if (!z_pos.HasValue)
                    return default;

                var xy_pos = source
                    .Match(/* language = regex */ "(?m)^G0 F(?<feedrate>\\S*) X(?<x_pos>\\S*) Y(?<y_pos>\\S*)")
                    .Convert(m =>
                        (x_pos: m.Lookup("x_pos", x => double.Parse(x, CultureInfo.InvariantCulture)),
                        y_pos: m.Lookup("y_pos", y => double.Parse(y, CultureInfo.InvariantCulture))
                    ));

                if (!xy_pos.HasValue)
                    return default;
                if (!xy_pos.Value.x_pos.HasValue)
                    return default;
                if (!xy_pos.Value.y_pos.HasValue)
                    return default;

                var e_pos = source
                    .Match(/* language = regex */ "(?m)^G92 E(?<e_pos>\\S*)")
                    .Convert(m => m.Lookup("e_pos", e => double.Parse(e, CultureInfo.InvariantCulture)));
                if (!e_pos.HasValue)
                    return default;

                var axis_position = ImmutableDictionary<char, double>.Empty
                    .AddRange(new KeyValuePair<char, double>[]
                    {
                        new('X', xy_pos.Value.x_pos.Value),
                        new('Y', xy_pos.Value.y_pos.Value),
                        new('Z', z_pos.Value),
                        new('E', e_pos.Value),
                    });

                return new FluxJobRecovery()
                {
                    MCodeKey = mcode,
                    Feedrate = feedrate.Value,
                    ToolIndex = tool_index.Value,
                    AxisPosition = axis_position,
                    PlateTemperatures = plates_temp,
                    BlockNumber = block_number.Value,
                    ChamberTemperatures = chamber_temp,
                    ToolTemperatures = tools_temp.Value,
                };
            }
            catch (Exception)
            {
                return default;
            }
        }
        protected override (GCodeString start_compare, GCodeString end_compare) CompareQueuePosGCode(int queue_pos)
        {
            return ($"{(queue_pos == 0 ? "if" : "elif")} global.queue_pos = {queue_pos}", default);
        }
    }
}
