using DynamicData.Kernel;
using Modulo3DNet;
using OSAI;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using GreenSuperGreen.Queues;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Reflection.Metadata;
using System.Reactive.Disposables;

namespace Flux.ViewModels
{
    public interface IOSAI_Request
    {
        TimeSpan Timeout { get; }
        OSAI_RequestPriority Priority { get; }
        CancellationToken Cancellation { get; }

        bool TrySetCanceled();
        Task<bool> TrySendAsync(OPENcontrolPortTypeClient client, CancellationToken ct);
    }
    public enum OSAI_RequestPriority : ushort
    {
        Immediate = 3,
        High = 2,
        Medium = 1,
        Low = 0
    }

    public readonly struct OSAI_Request<TData> : IOSAI_Request
    {
        public TimeSpan Timeout { get; }
        public OSAI_RequestPriority Priority { get; }
        public CancellationToken Cancellation { get; }
        public TaskCompletionSource<OSAI_Response<TData>> Response { get; }
        public Func<TData, OSAI_Err> Retval { get; }
        public Func<OPENcontrolPortTypeClient, CancellationToken, Task<TData>> Request { get; }
        public OSAI_Request(
            Func<OPENcontrolPortTypeClient, CancellationToken, Task<TData>> request,
            Func<TData, OSAI_Err> retval,
            OSAI_RequestPriority priority,
            CancellationToken ct, 
            TimeSpan timeout = default)
        {
            Retval = retval;
            Timeout = timeout;
            Request = request;
            Cancellation = ct;
            Priority = priority;
            Response = new TaskCompletionSource<OSAI_Response<TData>>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        public override string ToString() => $"{nameof(OSAI_Request<TData>)} Method:{Request.Method}";
        public async Task<bool> TrySendAsync(OPENcontrolPortTypeClient client, CancellationToken ct)
        {
            try
            {
                var response    = await Request(client, ct);
                var retval      = Retval(response);
                return Response.TrySetResult(new OSAI_Response<TData>(response, retval));
            }
            catch (Exception ex)
            {
                Response.TrySetResult(default);
                return false;
            }
        }

        public bool TrySetCanceled()
        {
            return Response.TrySetResult(default);
        }
    }

    public interface IOSAI_Response
    {
        bool Ok { get; }
    }
    public record struct OSAI_Err(ushort Err, uint ErrClass, uint ErrNum);
    public record struct OSAI_Response<TData>(Optional<TData> Content, OSAI_Err Err) : IOSAI_Response
    {
        public bool Ok
        {
            get
            {
                if (Err.Err == 0 || !Content.HasValue)
                {
                    Console.WriteLine(this);
                    return false;
                }
                return true;
            }
        }

        public override string ToString()
        {
            if (!Content.HasValue)
                return $"{nameof(OSAI_Response<TData>)} Task cancellata";
            if (Err.Err == 0)
                return $"Errore {Err.ErrClass}, {Err.ErrNum}";
            return "OK";
        }
    }

    public class OSAI_Connection : FLUX_Connection<OSAI_Connection, OSAI_ConnectionProvider, OSAI_VariableStore, OPENcontrolPortTypeClient>
    {
        public const ushort AxisNum = 4;
        public const ushort ProcessNumber = 1;

        public override string RootPath => "DEVICE";
        public override string MacroPath => "MACRO";
        public override string QueuePath => "PROGRAMS\\QUEUE";
        public override string EventPath => "PROGRAMS\\EVENTS";
        public override string StoragePath => "PROGRAMS\\STORAGE";
        public override string JobEventPath => CombinePaths(EventPath, "JOB");
        public override string InnerQueuePath => CombinePaths(QueuePath, "INNER");
        public override string ExtrusionEventPath => CombinePaths(EventPath, "EXTR");
        public override string CombinePaths(params string[] paths) => string.Join("\\", paths);

        public FluxViewModel Flux { get; }
        public PriorityQueueNotifierUC<OSAI_RequestPriority, IOSAI_Request> Requests { get; }

        // MEMORY VARIABLES
        public OSAI_Connection(FluxViewModel flux, OSAI_ConnectionProvider connection_provider) : base(connection_provider)
        {
            Flux = flux;

            var values = ((OSAI_RequestPriority[])Enum.GetValues(typeof(OSAI_RequestPriority)))
                .OrderByDescending(e => (ushort)e);
            Requests = new PriorityQueueNotifierUC<OSAI_RequestPriority, IOSAI_Request>(values);

            DisposableThread.Start(TryDequeueAsync, TimeSpan.Zero)
                .DisposeWith(Disposables);
        }
        private async Task TryDequeueAsync()
        {
            if (!Client.HasValue)
                return;
            await Requests.EnqueuedItemsAsync();
            while (Requests.TryDequeu(out var rrf_request))
            {
                try
                {
                    using var request_cts = CancellationTokenSource.CreateLinkedTokenSource(rrf_request.Cancellation);
                    if (rrf_request.Timeout > TimeSpan.Zero)
                        request_cts.CancelAfter(rrf_request.Timeout);

                    await rrf_request.TrySendAsync(Client.Value, request_cts.Token);
                }
                catch
                {
                    rrf_request.TrySetCanceled();
                }
            }
        }
        public async Task<OSAI_Response<TData>> TryEnqueueRequestAsync<TData>(
            Func<OPENcontrolPortTypeClient, CancellationToken, Task<TData>> request, 
            Func<TData, OSAI_Err> retval, 
            OSAI_RequestPriority priority,
            CancellationToken ct,
            TimeSpan timeout = default)
        {
            if (!Client.HasValue)
                return default;
            var osai_request = new OSAI_Request<TData>((c, ct) => request(c, ct), retval, priority, ct, timeout);
            using var ctr = osai_request.Cancellation.Register(() => osai_request.TrySetCanceled());
            Requests.Enqueue(osai_request.Priority, osai_request);
            var response = await osai_request.Response.Task;
            await ctr.DisposeAsync();
            return response;
        }
        public async Task<Optional<TData>> TryEnqueueRequestAsync<TResponse, TData>(
            Func<OPENcontrolPortTypeClient, CancellationToken, Task<TResponse>> request,
            Func<TResponse, TData> get_value,
            Func<TResponse, OSAI_Err> retval,
            OSAI_RequestPriority priority,
            CancellationToken ct,
            TimeSpan timeout = default)
        {
            try
            {
                if (!Client.HasValue)
                    return default;
                var osai_request = new OSAI_Request<TResponse>((c, ct) => request(c, ct), retval, priority, ct, timeout);
                using var ctr = osai_request.Cancellation.Register(() => osai_request.TrySetCanceled());
                Requests.Enqueue(osai_request.Priority, osai_request);
                var response = await osai_request.Response.Task;
                await ctr.DisposeAsync();

                if (!response.Ok)
                    return default;
                if (!response.Content.HasValue)
                    return default;

                return get_value(response.Content.Value);
            }
            catch(Exception ex)
            {
                return default;
            }
        }

        // CONNECT
        public override async Task<bool> CloseAsync()
        {
            try
            {
                if (!Client.HasValue)
                    return true;
                await Client.Value.CloseAsync();
                Client = null;
                return true;
            }
            catch
            {
                return false;
            }
        }
        public override async Task<bool> ConnectAsync()
        {
            try
            {
                if (!await CloseAsync())
                    return false;

                if (!Flux.NetProvider.PLCNetworkConnectivity)
                    return false;

                var core_settings = Flux.SettingsProvider.CoreSettings.Local;
                var plc_address = core_settings.PLCAddress;
                if (!plc_address.HasValue)
                    return false;

                Client = new OPENcontrolPortTypeClient(OPENcontrolPortTypeClient.EndpointConfiguration.OPENcontrol, plc_address.Value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // MEMORY R/W
        private async Task<bool> WriteVariableAsync<TOSAI_Address, TData, TResponse>(
            TOSAI_Address address, TData value, CancellationToken ct, 
            Func<OPENcontrolPortTypeClient, TOSAI_Address, TData, Task<TResponse>> write,
            Func<TResponse, OSAI_Err> retval)
        {
            var response = await TryEnqueueRequestAsync((c, ct) => write(c, address, value), retval, OSAI_RequestPriority.Immediate, ct);
            return response.Ok;
        }
        public Task<bool> WriteVariableAsync(OSAI_BitIndexAddress address, bool value, CancellationToken ct)
        {
            return WriteVariableAsync(address, value, ct,
                (c, a, v) => c.WriteVarWordBitAsync(new WriteVarWordBitRequest((ushort)a.VarCode, ProcessNumber, a.Index, a.BitIndex, v ? (ushort)1 : (ushort)0)),
                r => new (r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<bool> WriteVariableAsync(OSAI_IndexAddress address, short value, CancellationToken ct)
        {
            return WriteVariableAsync(address, value, ct,
                (c, a, v) => c.WriteVarWordAsync(new WriteVarWordRequest((ushort)a.VarCode, ProcessNumber, a.Index, 1, new unsignedshortarray { ShortConverter.Convert(v) })),
                r => new (r.retval, r.ErrClass, r.ErrNum));
        }
        public async Task<bool> WriteVariableAsync(OSAI_IndexAddress address, string value, CancellationToken ct)
        {
            if (value.Length > 128)
                return false;

            return await WriteVariableAsync(address, value, ct,
                (c, a, v) => c.WriteVarTextAsync(new WriteVarTextRequest((ushort)a.VarCode, ProcessNumber, a.Index, (ushort)v.Length, v)),
                r => new (r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<bool> WriteVariableAsync(OSAI_IndexAddress address, ushort value, CancellationToken ct)
        {
            return WriteVariableAsync(address, value, ct,
                (c, a, v) => c.WriteVarWordAsync(new WriteVarWordRequest((ushort)a.VarCode, ProcessNumber, a.Index, 1, new unsignedshortarray { v })),
                r => new (r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<bool> WriteVariableAsync(OSAI_IndexAddress address, double value, CancellationToken ct)
        {
            return WriteVariableAsync(address, value, ct,
                (c, a, v) => c.WriteVarDoubleAsync(new WriteVarDoubleRequest((ushort)a.VarCode, ProcessNumber, a.Index, 1, new doublearray { v })),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<bool> WriteVariableAsync(OSAI_IndexAddress address, ushort bit_index, bool value, CancellationToken ct)
        {
            return WriteVariableAsync(address, value, ct,
                (c, a, v) => c.WriteVarWordBitAsync(new WriteVarWordBitRequest((ushort)a.VarCode, ProcessNumber, a.Index, bit_index, v ? (ushort)1 : (ushort)0)),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }

        public Task<bool> WriteVariableAsync(OSAI_NamedAddress address, bool value, CancellationToken ct)
        {
            return WriteVariableAsync(address, value, ct,
                (c, a, v) => c.WriteNamedVarDoubleAsync(new WriteNamedVarDoubleRequest(ProcessNumber, a.Name, 1, a.Index, -1, -1, new doublearray() { v ? 1.0 : 0.0 })),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<bool> WriteVariableAsync(OSAI_NamedAddress address, short value, CancellationToken ct)
        {
            return WriteVariableAsync(address, value, ct,
                (c, a, v) => c.WriteNamedVarDoubleAsync(new WriteNamedVarDoubleRequest(ProcessNumber, a.Name, 1, a.Index, -1, -1, new doublearray() { v })),
                r => new(r.retval, r.ErrClass, r.ErrNum));
          
        }
        public Task<bool> WriteVariableAsync(OSAI_NamedAddress address, ushort value, CancellationToken ct)
        {
            return WriteVariableAsync(address, value, ct,
                (c, a, v) => c.WriteNamedVarDoubleAsync(new WriteNamedVarDoubleRequest(ProcessNumber, a.Name, 1, a.Index, -1, -1, new doublearray() { v })),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<bool> WriteVariableAsync(OSAI_NamedAddress address, double value, CancellationToken ct)
        {
            return WriteVariableAsync(address, value, ct,
                (c, a, v) => c.WriteNamedVarDoubleAsync(new WriteNamedVarDoubleRequest(ProcessNumber, a.Name, 1, a.Index, -1, -1, new doublearray() { v })),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public async Task<bool> WriteVariableAsync(OSAI_NamedAddress address, string value, ushort lenght, CancellationToken ct)
        {
            if (value.Length > lenght)
                return false;

            var bytes_array = Encoding.ASCII.GetBytes(value);

            return await WriteVariableAsync(address, bytes_array, ct,
                (c, a, v) => c.WriteNamedVarByteArrayAsync(new WriteNamedVarByteArrayRequest(ProcessNumber, a.Name, (ushort)v.Length, 0, -1, -1, v)),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }

        private Task<Optional<TData>> ReadVariableAsync<TOSAI_Address, TData, TResponse>(
            TOSAI_Address address, CancellationToken ct,
            Func<OPENcontrolPortTypeClient, TOSAI_Address, Task<TResponse>> read,
            Func<TResponse, TOSAI_Address, TData> get_value,
            Func<TResponse, OSAI_Err> retval)
        {
            return TryEnqueueRequestAsync((c, ct) => read(c, address), v => get_value(v, address), retval, OSAI_RequestPriority.Immediate, ct);
        }
        public Task<Optional<bool>> ReadBoolAsync(OSAI_BitIndexAddress address, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadVarWordAsync(new ReadVarWordRequest((ushort)a.VarCode, ProcessNumber, a.Index, 1)),
                (r, a) => r.Value[0].IsBitSet(a.BitIndex),
                r => new (r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<Optional<ushort>> ReadUShortAsync(OSAI_IndexAddress address, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadVarWordAsync(new ReadVarWordRequest((ushort)a.VarCode, ProcessNumber, a.Index, 1)),
                (r, a) => r.Value[0],
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<Optional<short>> ReadShortAsync(OSAI_IndexAddress address, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadVarWordAsync(new ReadVarWordRequest((ushort)a.VarCode, ProcessNumber, a.Index, 1)),
                (r, a) => ShortConverter.Convert(r.Value[0]),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<Optional<string>> ReadTextAsync(OSAI_IndexAddress address, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadVarTextAsync(new ReadVarTextRequest((ushort)a.VarCode, ProcessNumber, a.Index, 128)),
                (r, a) => r.Text,
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<Optional<double>> ReadDoubleAsync(OSAI_IndexAddress address, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadVarDoubleAsync(new ReadVarDoubleRequest((ushort)a.VarCode, ProcessNumber, a.Index, 1)),
                (r, a) => r.Value[0],
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<Optional<bool>> ReadBoolAsync(OSAI_IndexAddress address, ushort bit_index, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadVarWordAsync(new ReadVarWordRequest((ushort)a.VarCode, ProcessNumber, a.Index, 1)),
                (r, a) => r.Value[0].IsBitSet(bit_index),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }

        public Task<Optional<bool>> ReadNamedBoolAsync(OSAI_NamedAddress address, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadNamedVarDoubleAsync(new ReadNamedVarDoubleRequest(ProcessNumber, a.Name, 1, a.Index, -1, -1)),
                (r, a) => r.Value[0] == 1.0,
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<Optional<short>> ReadNamedShortAsync(OSAI_NamedAddress address, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadNamedVarDoubleAsync(new ReadNamedVarDoubleRequest(ProcessNumber, a.Name, 1, a.Index, -1, -1)),
                (r, a) => (short)r.Value[0],
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<Optional<ushort>> ReadNamedUShortAsync(OSAI_NamedAddress address, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadNamedVarDoubleAsync(new ReadNamedVarDoubleRequest(ProcessNumber, a.Name, 1, a.Index, -1, -1)),
                (r, a) => (ushort)r.Value[0],
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<Optional<double>> ReadNamedDoubleAsync(OSAI_NamedAddress address, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadNamedVarDoubleAsync(new ReadNamedVarDoubleRequest(ProcessNumber, a.Name, 1, a.Index, -1, -1)),
                (r, a) => r.Value[0],
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<Optional<string>> ReadNamedStringAsync(OSAI_NamedAddress address, ushort lenght, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadNamedVarByteArrayAsync(new ReadNamedVarByteArrayRequest(ProcessNumber, a.Name, lenght, a.Index, -1, -1)),
                (r, a) => Encoding.ASCII.GetString(r.Value),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }

        public Task<Optional<TOut>> ReadBoolAsync<TOut>(OSAI_BitIndexAddress address, Func<bool, TOut> convert_func, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadVarWordAsync(new ReadVarWordRequest((ushort)a.VarCode, ProcessNumber, a.Index, 1)),
                (r, a) => convert_func(r.Value[0].IsBitSet(a.BitIndex)),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<Optional<TOut>> ReadUShortAsync<TOut>(OSAI_IndexAddress address, Func<ushort, TOut> convert_func, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadVarWordAsync(new ReadVarWordRequest((ushort)a.VarCode, ProcessNumber, a.Index, 1)),
                (r, a) => convert_func(r.Value[0]),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<Optional<TOut>> ReadShortAsync<TOut>(OSAI_IndexAddress address, Func<short, TOut> convert_func, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadVarWordAsync(new ReadVarWordRequest((ushort)a.VarCode, ProcessNumber, a.Index, 1)),
                (r, a) => convert_func(ShortConverter.Convert(r.Value[0])),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<Optional<TOut>> ReadTextAsync<TOut>(OSAI_IndexAddress address, Func<string, TOut> convert_func, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadVarTextAsync(new ReadVarTextRequest((ushort)a.VarCode, ProcessNumber, a.Index, 1)),
                (r, a) => convert_func(r.Text),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<Optional<TOut>> ReadDoubleAsync<TOut>(OSAI_IndexAddress address, Func<double, TOut> convert_func, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadVarDoubleAsync(new ReadVarDoubleRequest((ushort)a.VarCode, ProcessNumber, a.Index, 1)),
                (r, a) => convert_func(r.Value[0]),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<Optional<TOut>> ReadBoolAsync<TOut>(OSAI_IndexAddress address, ushort bit_index, Func<bool, TOut> convert_func, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadVarWordAsync(new ReadVarWordRequest((ushort)a.VarCode, ProcessNumber, a.Index, 1)),
                (r, a) => convert_func(r.Value[0].IsBitSet(bit_index)),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }

        public Task<Optional<TOut>> ReadNamedBoolAsync<TOut>(OSAI_NamedAddress address, Func<bool, TOut> convert_func, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadNamedVarDoubleAsync(new ReadNamedVarDoubleRequest(ProcessNumber, a.Name, 1, a.Index, -1, -1)),
                (r, a) => convert_func(r.Value[0] == 1.0),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<Optional<TOut>> ReadNamedShortAsync<TOut>(OSAI_NamedAddress address, Func<short, TOut> convert_func, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadNamedVarDoubleAsync(new ReadNamedVarDoubleRequest(ProcessNumber, a.Name, 1, a.Index, -1, -1)),
                (r, a) => convert_func((short)r.Value[0]),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<Optional<TOut>> ReadNamedUShortAsync<TOut>(OSAI_NamedAddress address, Func<ushort, TOut> convert_func, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadNamedVarDoubleAsync(new ReadNamedVarDoubleRequest(ProcessNumber, a.Name, 1, a.Index, -1, -1)),
                (r, a) => convert_func((ushort)r.Value[0]),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<Optional<TOut>> ReadNamedDoubleAsync<TOut>(OSAI_NamedAddress address, Func<double, TOut> convert_func, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadNamedVarDoubleAsync(new ReadNamedVarDoubleRequest(ProcessNumber, a.Name, 1, a.Index, -1, -1)),
                (r, a) => convert_func(r.Value[0]),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }
        public Task<Optional<TOut>> ReadNamedStringAsync<TOut>(OSAI_NamedAddress address, ushort lenght, Func<string, TOut> convert_func, CancellationToken ct)
        {
            return ReadVariableAsync(address, ct,
                (c, a) => c.ReadNamedVarByteArrayAsync(new ReadNamedVarByteArrayRequest(ProcessNumber, a.Name, lenght, a.Index, -1, -1)),
                (r, a) => convert_func(Encoding.ASCII.GetString(r.Value)),
                r => new(r.retval, r.ErrClass, r.ErrNum));
        }

        // BASIC FUNCTIONS
        public override Task<bool> InitializeVariablesAsync(CancellationToken ct)
        {
            return Task.FromResult(true);
        }
        // WAIT MEMORY
        public async Task<bool> WaitBootPhaseAsync(Func<OSAI_BootPhase, bool> phase_func, TimeSpan dueTime, TimeSpan sample, TimeSpan throttle, TimeSpan timeout)
        {
            var read_boot_phase = Observable.Timer(dueTime, sample)
                .SelectMany(t => ReadVariableAsync(c => c.BOOT_PHASE));

            return await WaitUtils.WaitForOptionalAsync(read_boot_phase,
                throttle, phase => phase_func(phase), timeout);
        }
        public async Task<bool> WaitProcessModeAsync(Func<OSAI_ProcessMode, bool> status_func, TimeSpan dueTime, TimeSpan sample, TimeSpan throttle, TimeSpan timeout)
        {
            var read_boot_mode = Observable.Timer(dueTime, sample)
                .SelectMany(t => ReadVariableAsync(c => c.PROCESS_MODE));

            return await WaitUtils.WaitForOptionalAsync(read_boot_mode,
                throttle, process => status_func(process), timeout);
        }

        // BASIC OPERATIONS
        public Task<Optional<FLUX_AxisPosition>> GetAxesPositionAsync(OSAI_AxisPositionSelect select, CancellationToken ct)
        {
            return TryEnqueueRequestAsync(
                (c, ct) => c.GetAxesPositionAsync(new GetAxesPositionRequest(ProcessNumber, 0, (ushort)select, AxisNum)),
                r => (FLUX_AxisPosition)r.IntPos.ToImmutableDictionary(p => (char)p.AxisName, p => p.position),
                r => new(r.retval, r.ErrClass, r.ErrNum), OSAI_RequestPriority.Immediate, ct);
        }
        public async Task<bool> AxesRefAsync(CancellationToken ct, params char[] axes)
        {
            var response = await TryEnqueueRequestAsync(
                (c, ct) => c.AxesRefAsync(new AxesRefRequest(ProcessNumber, (ushort)axes.Length, string.Join("", axes))),
                r => new(r.retval, r.ErrClass, r.ErrNum), OSAI_RequestPriority.Immediate, ct);

            return response.Ok;
        }

        // CONTROL
        public override async Task<bool> StopAsync(CancellationToken ct)
        {
            var response = await TryEnqueueRequestAsync(
                (c, ct) => c.ResetAsync(new ResetRequest(ProcessNumber)),
                r => new(r.retval, r.ErrClass, r.ErrNum), OSAI_RequestPriority.Immediate, ct);

            return response.Ok;
        }
        public override async Task<bool> CancelAsync(CancellationToken ct)
        {
            var reset_response = await StopAsync(ct);
            if (!reset_response)
                return false;

            var wait_idle = await ConnectionProvider.WaitProcessStatusAsync(
                s => s == FLUX_ProcessStatus.IDLE,
                TimeSpan.FromSeconds(0.1),
                TimeSpan.FromSeconds(0.1),
                TimeSpan.FromSeconds(0.2),
                TimeSpan.FromSeconds(5));

            if (!wait_idle)
                return false;

            using var paramacro_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await ExecuteParamacroAsync(new[]
            {
                GetExecuteMacroGCode(InnerQueuePath, "cancel.g"),
                GetExecuteMacroGCode(MacroPath, "end_print")
            }, paramacro_cts.Token);
        }
        public override async Task<bool> PauseAsync(CancellationToken ct)
        {
            return await WriteVariableAsync("!REQ_HOLD", true, ct);
        }
        public override async Task<bool> ExecuteParamacroAsync(GCodeString paramacro, CancellationToken put_ct, bool can_cancel = false)
        {
            try
            {
                if (!paramacro.HasValue)
                    return false;

                var deselect_part_program_response = await TryEnqueueRequestAsync(
                    (c, ct) => Client.Value.SelectPartProgramFromDriveAsync(new SelectPartProgramFromDriveRequest(ProcessNumber, $"")),
                    r => new (r.retval, r.ErrClass, r.ErrNum), OSAI_RequestPriority.Immediate, put_ct);

                if (!deselect_part_program_response.Ok)
                    return false;

                using var delete_paramacro_ctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var remove_file_response = await DeleteAsync(StoragePath, "paramacro.mcode", delete_paramacro_ctk.Token);
                if (!remove_file_response)
                    return false;

                // put file
                var put_paramacro_response = await PutFileAsync(
                    StoragePath,
                    "paramacro.mcode", true,
                    put_ct, get_paramacro_gcode().ToOptional());

                if (put_paramacro_response == false)
                    return false;

                // Set PLC to Cycle

                var select_part_program_response = await TryEnqueueRequestAsync(
                    (c, ct) => Client.Value.SelectPartProgramFromDriveAsync(new SelectPartProgramFromDriveRequest(ProcessNumber, CombinePaths(StoragePath, "paramacro.mcode"))),
                    r => new(r.retval, r.ErrClass, r.ErrNum), OSAI_RequestPriority.Immediate, put_ct);

                if (!select_part_program_response.Ok)
                    return false;

                var cycle_response = await TryEnqueueRequestAsync(
                    (c, ct) => Client.Value.CycleAsync(new CycleRequest(ProcessNumber, 1)),
                    r => new(r.retval, r.ErrClass, r.ErrNum), OSAI_RequestPriority.Immediate, put_ct);

                if (!cycle_response.Ok)
                    return false;

                return true;

                IEnumerable<string> get_paramacro_gcode()
                {
                    foreach (var line in paramacro)
                        yield return line.TrimEnd();
                }
            }
            catch
            {
                return false;
            }
        }
        // FILES
        public override async Task<bool> PutFileAsync(
            string folder,
            string filename,
            bool is_paramacro,
            CancellationToken ct,
            GCodeString source = default,
            GCodeString start = default,
            GCodeString end = default,
            Optional<BlockNumber> source_blocks = default,
            Action<double> report_progress = default)
        {
            ushort file_id;
            uint block_number = 0;
            ushort transaction = 0;

            try
            {
                if (!Client.HasValue)
                    return false;

                // upload a paramacro
                if (is_paramacro)
                {
                    var path_name = CombinePaths(folder, filename);
                    var gcode = GCodeString.Create(start, source, end, Environment.NewLine).ToString();
                    var put_file_request = new PutFileRequest(gcode, (uint)gcode.Length, path_name);
                    var put_file_response = await Client.Value.PutFileAsync(put_file_request);

                    if (put_file_response.retval == 0)
                        return false;

                    return true;
                }

                // upload a partprogram
                using var delete_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var remove_file_response = await DeleteAsync(folder, "file_upload.tmp", delete_cts.Token);
                if (!remove_file_response)
                    return false;

                var create_file_request = new LogFSCreateFileRequest($"{folder}\\file_upload.tmp");
                var create_file_response = await Client.Value.LogFSCreateFileAsync(create_file_request);

                if (create_file_response.retval == 0)
                    return false;

                var open_file_request = new LogFSOpenFileRequest($"{folder}\\file_upload.tmp", true, 0, 0);
                var open_file_response = await Client.Value.LogFSOpenFileAsync(open_file_request);

                if (open_file_response.retval == 0)
                    return false;
                file_id = open_file_response.FileID;


                // Write actual gcode
                var chunk_size = 10000;
                if (source.HasValue)
                {
                    foreach (var chunk in get_full_source().AsChunks(chunk_size))
                    {
                        using var gcode_writer = new StringWriter();
                        foreach (var line in chunk)
                            gcode_writer.WriteLine(line);
                        gcode_writer.WriteLine("");

                        var byte_data = Encoding.UTF8.GetBytes(gcode_writer.ToString());
                        var write_record_request = new LogFSWriteRecordRequest(file_id, transaction++, (uint)byte_data.Length, byte_data);
                        var write_record_response = await Client.Value.LogFSWriteRecordAsync(write_record_request);

                        if (write_record_response.retval == 0)
                        {
                            var close_file_request = new LogFSCloseFileRequest(file_id, (ushort)(transaction - 1));
                            var close_file_response = await Client.Value.LogFSCloseFileAsync(close_file_request);

                            return false;
                        }
                    }
                }

                var byte_data_newline = Encoding.UTF8.GetBytes(Environment.NewLine);
                var write_record_request_newline = new LogFSWriteRecordRequest(file_id, transaction++, (uint)byte_data_newline.Length, byte_data_newline);
                var write_record_response_newline = await Client.Value.LogFSWriteRecordAsync(write_record_request_newline);

                if (write_record_response_newline.retval == 0)
                {
                    var close_file_request = new LogFSCloseFileRequest(file_id, (ushort)(transaction - 1));
                    var close_file_response = await Client.Value.LogFSCloseFileAsync(close_file_request);
                    return false;
                }

                IEnumerable<string> get_full_source()
                {
                    if (start.HasValue)
                    {
                        foreach (var line in start)
                            yield return line;
                    }

                    long current_block = 0;
                    if (source.HasValue)
                    {
                        foreach (var line in source)
                        {
                            yield return $"N{block_number++} {line}";

                            if (!source_blocks.HasValue)
                                continue;

                            if (source_blocks.Value == 0)
                                continue;

                            var progress = (double)current_block++ / source_blocks.Value * 100;
                            if (progress - (int)progress < 0.001)
                                report_progress?.Invoke((int)progress);
                        }
                    }

                    if (end.HasValue)
                    {
                        foreach (var line in end)
                            yield return line;
                    }

                    yield return "";
                }
            }
            catch (Exception)
            {
                return false;
            }

            try
            {
                if (file_id == 0)
                    return false;

                var close_file_request = new LogFSCloseFileRequest(file_id, transaction++);
                var close_file_response = await Client.Value.LogFSCloseFileAsync(close_file_request);
                if (close_file_response.retval == 0)
                    return false;

                using var delete_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var remove_file_response = await DeleteAsync(folder, filename, delete_cts.Token);
                if (!remove_file_response)
                    return false;

                using var rename_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var rename_response = await RenameAsync(folder, "file_upload.tmp", filename, rename_cts.Token); ;
                if (!rename_response)
                    return false;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        public override Task<bool> ClearFolderAsync(string folder, CancellationToken ct)
        {
            return DeleteAsync(folder, "*", ct);
        }
        public override Task<Stream> GetFileStreamAsync(string folder, string name, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
        public override async Task<bool> DeleteAsync(string folder, string filename, CancellationToken ct)
        {
            var remove_file_response = await TryEnqueueRequestAsync(
                (c, ct) => c.LogFSRemoveFileAsync(new LogFSRemoveFileRequest($"{folder}\\", filename)),
                r => new(r.retval, r.ErrClass, r.ErrNum), OSAI_RequestPriority.Immediate, ct);
            return remove_file_response.Ok || (remove_file_response.Err.ErrClass == 5 && remove_file_response.Err.ErrNum == 17);
        }
        public override async Task<bool> CreateFolderAsync(string folder, string name, CancellationToken ct)
        {
            var create_dir_response = await TryEnqueueRequestAsync(
                (c, ct) => c.LogFSCreateDirAsync(new LogFSCreateDirRequest($"{folder}\\{name}")),
                r => new(r.retval, r.ErrClass, r.ErrNum), OSAI_RequestPriority.Immediate, ct);
            return create_dir_response.Ok;
        }
        public override async Task<Optional<FLUX_FileList>> ListFilesAsync(string folder, CancellationToken ct)
        {
            var files_data = new FLUX_FileList(folder);
            Optional<LogFSFindFirstResponseBody> find_first_result = default;

            try
            {
                if (!Client.HasValue)
                    return default;

                find_first_result = await TryEnqueueRequestAsync(
                    (c, ct) => c.LogFSFindFirstOrDefaultAsync($"{folder}\\*"),
                    r => r.Body, r => new(r.Body.retval, r.Body.ErrClass, r.Body.ErrNum), OSAI_RequestPriority.Immediate, ct);

                if (!find_first_result.HasValue)
                    return files_data;

                // empty 
                if (find_first_result.Value.Finder == 0xFFFFFFFF)
                    return files_data;

                var file = parse_file(find_first_result.Value.FindData);
                if (file.HasValue)
                    files_data.Files.Add(file.Value);

                var handle = find_first_result.Value.Finder;

                Optional<LogFSFindNextResponseBody> find_next_result;
                do
                {
                    find_next_result = await TryEnqueueRequestAsync(
                        (c, ct) => c.LogFSFindNextAsync(handle),
                        r => r.Body, r => new(r.Body.retval, r.Body.ErrClass, r.Body.ErrNum), OSAI_RequestPriority.Immediate, ct); 
                    
                    if (!find_next_result.HasValue)
                        return files_data;

                    file = parse_file(find_next_result.Value.FindData);
                    if (file.HasValue)
                        files_data.Files.Add(file.Value);
                }
                while (find_next_result.ConvertOr(r => r.Found, () => false));
            }
            catch 
            { 
                return default;
            }
            finally
            {
                if (find_first_result.HasValue)
                {
                    var close_response = await TryEnqueueRequestAsync(
                        (c, ct) => c.LogFSFindCloseAsync(new LogFSFindCloseRequest(find_first_result.Value.Finder)),
                        r => new(r.retval, r.ErrClass, r.ErrNum), OSAI_RequestPriority.Immediate, ct); 
                }
            }

            return files_data;

            static Optional<FLUX_File> parse_file(FILEFINDDATA data)
            {
                if (string.IsNullOrEmpty(data.FileName))
                    return default;

                var file_name = data.FileName;
                var file_attributes = (OSAI_FileAttributes)data.FileAttributes;
                var file_size = (ulong)data.FileSizeHigh << 32 | data.FileSizeLow;
                var has_directory_flag = file_attributes.HasFlag(OSAI_FileAttributes.FILE_ATTRIBUTE_DIRECTORY);
                var file_type = has_directory_flag ? FLUX_FileType.Directory : FLUX_FileType.File;

                var file_date = DateTime.FromFileTime((long)data.HighDateLastWriteTime << 32 | data.LowDateLastWriteTime);

                return new FLUX_File()
                {
                    Name = file_name,
                    Type = file_type,
                    Date = file_date,
                    Size = file_size,
                };
            }
        }
        public override Task<bool> PutFileStreamAsync(string folder, string name, Stream data, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
        public override async Task<Optional<string>> GetFileAsync(string folder, string filename, CancellationToken ct)
        {
            if (!Client.HasValue)
                return default;

            var path_name = CombinePaths(folder, filename);

            var file_size_request = new LogFSGetFileSizeRequest(path_name);
            var file_size_response = await Client.Value.LogFSGetFileSizeAsync(file_size_request);

            if (file_size_response.retval == 0)
                return default;

            var get_file_request = new GetFileRequest(path_name, file_size_response.Size);
            var get_file_response = await Client.Value.GetFileAsync(get_file_request);

            if (get_file_response.retval == 0)
                return default;

            return get_file_response.Data;
        }
        public override async Task<bool> RenameAsync(string folder, string old_filename, string new_filename, CancellationToken ct)
        {
            var rename_response = await TryEnqueueRequestAsync(
                (c, ct) => Client.Value.LogFSRenameAsync(new LogFSRenameRequest($"{folder}\\{old_filename}", $"{folder}\\{new_filename}")),
                r => new(r.retval, r.ErrClass, r.ErrNum), OSAI_RequestPriority.Immediate, ct);
            return rename_response.Ok;
        }

        // GENERIC GCODE
        public override GCodeString GetParkToolGCode()
            {
                return "(CLS, MACRO\\change_tool, 0)";
            }
        public override GCodeString GetProbePlateGCode()
        {
            return "(CLS, MACRO\\probe_plate)";
        }
        public override GCodeString GetLowerPlateGCode()
        {
            return "(CLS, MACRO\\lower_plate)";
        }
        public override GCodeString GetRaisePlateGCode()
        {
            return "(CLS, MACRO\\raise_plate)";
        }
        public override GCodeString GetSetLowCurrentGCode()
        {
            throw new NotImplementedException();
        }
        public override GCodeString GetProbeMagazineGCode()
        {
            throw new NotImplementedException();
        }
        public override GCodeString GetCenterPositionGCode()
        {
            return new[] { "(CLS, MACRO\\center_position)" };
        }
        public override GCodeString GetExitPartProgramGCode()
        {
            return new[] { "(REL)" };
        }
        public override GCodeString GetBeginPartProgramGCode()
        {
            return GCodeString.Create(
                base.GetBeginPartProgramGCode(),
                 "; preprocessing",
                "(GTO, end_preprocess)",

                "M4140[0, 0]",
                "M4141[0, 0]",
                "M4104[0, 0, 0]",
                "M4999[0, 0, 0, 0]",

                "(CLS, MACRO\\probe_plate)",
                "(CLS, MACRO\\end_print)",
                "(CLS, MACRO\\home_printer)",
                "(CLS, MACRO\\change_tool, 0)",

                "G92 A0",
                "G1 X0 Y0 Z0 F1000",

                "\"end_preprocess\"",
                "(PAS)");
        }
        public override GCodeString GetHomingGCode(params char[] axis)
        {
            return "(CLS, MACRO\\home_printer)";
        }
        public override GCodeString GetManualCalibrationPositionGCode()
        {
            throw new NotImplementedException();
        }
        public override GCodeString GetSelectToolGCode(ArrayIndex position)
        {
            return $"(CLS, MACRO\\change_tool, {position.GetArrayBaseIndex()})";
        }
        public override GCodeString GetCancelLoadFilamentGCode(ArrayIndex position)
        {
            return new[] { $"M4104 [{position.GetArrayBaseIndex()}, 0, 0]" };
        }
        public override GCodeString GetWriteCurrentJobGCode(Optional<JobKey> job_key)
        {
            var job_key_str = job_key.ConvertOr(k => k.ToString(), () => "");
            return GCodeString.Create(
                $"LS0 = \"{job_key_str}\"",
                "M4001[7, 0, 0, 0]");
        }
        public override GCodeString GetCancelUnloadFilamentGCode(ArrayIndex position)
        {
            return new[] { $"M4104 [{position.GetArrayBaseIndex()}, 0, 0]" };
        }
        public override GCodeString GetDeleteFileGCode(string folder, string filename)
        {
            return GCodeString.Create(
                "ERR = 1",
                $"(DEL, \"{CombinePaths(folder, filename)}\")",
                "ERR = 0");
        }
        public override GCodeString GetExecuteMacroGCode(string folder, string filename)
        {
            return new[] { $"(CLS, {folder}\\{filename})" };
        }
        public override GCodeString GetLogEventGCode(FluxJob job, FluxEventType event_type)
        {
            var event_path = CombinePaths(JobEventPath, $"{job.MCodeKey};{job.JobKey}");
            var event_type_str = event_type.ToEnumString();

            return GCodeString.Create(

                // get event path
                $"LS0 = \"{event_path}\"",

                // get current date
                $"(GDT, D2, T0, SC0.10, SC11.11)",
                "SC10.1 = \";\"",

                // append file
                $"(OPN, 1, ?LS0, A, A)",
                $"(WRT, 1, \"{event_type_str};\", SC0.22)",
                "(CLO, 1)");
        }
        public override GCodeString GetProbeToolGCode(ArrayIndex position, double temperature)
        {
            return $"(CLS, MACRO\\probe_tool, {position.GetArrayBaseIndex()}, {temperature})";
        }
        public override GCodeString GetFilamentSensorSettingsGCode(ArrayIndex position, bool enabled)
        {
            return $"M4999 [{{FILAMENT_ENDSTOP_1_RESET}}, {position.GetArrayBaseIndex()}, 0, {(enabled ? 1 : 0)}]";
        }
        public override GCodeString GetMovementGCode(FLUX_AxisMove axis_move, FLUX_AxisTransform transform)
        {
            var inverse_transform_move = transform.InverseTransformMove(axis_move);
            if (!inverse_transform_move.HasValue)
                return default;

            return $"G1 {inverse_transform_move.Value.GetAxisPosition(m => m.Relative ? ">>" : "")} {axis_move.GetFeedrate('F')}";
        }
        public override GCodeString GetSetToolOffsetGCode(ArrayIndex position, double x, double y, double z)
        {
            var x_offset = (x + y) / 2;
            var y_offset = (x - y) / 2;
            return new[] { $"(UTO, 0, X({x_offset}), Y({y_offset}), Z({z}))" };
        }
        public override GCodeString GetWriteExtrusionKeyGCode(ArrayIndex position, Optional<ExtrusionKey> extr_key)
        {
            var extr_key_str = extr_key.ConvertOr(k => k.ToString(), () => "");
            return GCodeString.Create(
                $"E0 = 3 + {position.GetZeroBaseIndex()}",
                $"LS0 = \"{extr_key_str}\"",
                $"M4001[7, E0, 0, 0]");
        }
        public override GCodeString GetWriteExtrusionMMGCode(ArrayIndex position, double distance_mm)
        {
            return $"!EXTR_MM({position.GetZeroBaseIndex()}) = {distance_mm}";
        }
        public override GCodeString GetStartPartProgramGCode(string folder, string filename, Optional<FluxJobRecovery> recovery)
        {
            var feeder_axis = VariableStoreBase.FeederAxis;

            var feeder_g92_offset = recovery.Convert(r => r.AxisPosition.Lookup(feeder_axis)).ValueOr(() => 0.0);
            var start_block = recovery.Convert(r => r.BlockNumber).ValueOr(() => new BlockNumber(0, BlockType.None));

            return new[]
            {
                $"LS0 = \"{folder}\"",
                $"LS1 = \"{filename}\"",
                $"M4026[0, 1, {start_block}, {$"{feeder_g92_offset:0.##}".Replace(",", ".")}]"
            };
        }
        public override GCodeString GetSetExtruderMixingGCode(ArrayIndex machine_extruder, ArrayIndex mixing_extruder)
        {
            throw new NotImplementedException();
        }
        public override GCodeString GetResetPositionGCode(FLUX_AxisPosition axis_position, FLUX_AxisTransform transform)
        {
            var inverse_transform_position = transform.InverseTransformPosition(axis_position, false);
            if (!inverse_transform_position.HasValue)
                return default;

            return $"G92 {inverse_transform_position.Value.GetAxisPosition()}";
        }
        protected override GCodeString GetSetToolTemperatureGCodeInner(ArrayIndex position, double temperature, bool wait)
        {
            return $"M4104 [{position.GetArrayBaseIndex()}, {temperature}, {(wait ? 1 : 0)}]";
        }
        protected override GCodeString GetSetPlateTemperatureGCodeInner(ArrayIndex position, double temperature, bool wait)
        {
            return $"M4141 [{temperature}, {(wait ? 1 : 0)}]";
        }
        protected override GCodeString GetSetChamberTemperatureGCodeInner(ArrayIndex position, double temperature, bool wait)
        {
            return $"M4140 [{temperature}, {(wait ? 1 : 0)}]";
        }

        public override GCodeString GetLogExtrusionGCode(ArrayIndex position, Optional<ExtrusionKey> extr_key, FluxJob job)
        {
            return default;
        }
        public override GCodeString GetPausePrependGCode(Optional<JobKey> job_key)
        {
            return default;
        }

        public override GCodeString GetGotoMaintenancePositionGCode()
        {
            return $"(CLS, MACRO\\goto_maintenance_position)";
        }
    }
}
