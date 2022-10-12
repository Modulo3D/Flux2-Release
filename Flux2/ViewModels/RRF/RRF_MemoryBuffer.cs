using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public interface IFLUX_MemoryReaderBase<TFLUX_MemoryBuffer> : IReactiveObject
        where TFLUX_MemoryBuffer : IFLUX_MemoryBuffer
    {
        bool HasMemoryRead { get; }
        TFLUX_MemoryBuffer MemoryBuffer { get; }
    }
    public interface IFLUX_MemoryReader<TFLUX_MemoryBuffer> : IFLUX_MemoryReaderBase<TFLUX_MemoryBuffer>
        where TFLUX_MemoryBuffer : IFLUX_MemoryBuffer
    {
        string Resource { get; }
        CancellationTokenSource CTS { get; }

        Task TryScheduleAsync();
    }
    public interface IFLUX_MemoryReaderGroup<TFLUX_MemoryBuffer> : IFLUX_MemoryReaderBase<TFLUX_MemoryBuffer>, IDisposable
        where TFLUX_MemoryBuffer : IFLUX_MemoryBuffer
    {
        TimeSpan Period { get; }
        SourceCache<IFLUX_MemoryReader<TFLUX_MemoryBuffer>, string> MemoryReaders { get; }
    }
    public abstract class FLUX_MemoryReader<TFLUX_MemoryBuffer, TData> : ReactiveObject, IFLUX_MemoryReader<TFLUX_MemoryBuffer>
        where TFLUX_MemoryBuffer : IFLUX_MemoryBuffer
    {
        public string Resource { get; }
        public CancellationTokenSource CTS { get; protected set; }
        public TFLUX_MemoryBuffer MemoryBuffer { get; }
        private bool _HasMemeoryRead;
        public bool HasMemoryRead
        {
            get => _HasMemeoryRead;
            protected set => this.RaiseAndSetIfChanged(ref _HasMemeoryRead, value);
        }
        public FLUX_MemoryReader(TFLUX_MemoryBuffer memory_buffer, string resource)
        {
            Resource = resource;
            MemoryBuffer = memory_buffer;
        }
        public abstract Task TryScheduleAsync();
    }
    public abstract class FLUX_MemoryReaderGroup<TFLUX_MemoryBuffer, TFLUX_ConnectionProvider, TFLUX_VariableStore> : ReactiveObject, IFLUX_MemoryReaderGroup<TFLUX_MemoryBuffer>
        where TFLUX_VariableStore : IFLUX_VariableStore<TFLUX_VariableStore, TFLUX_ConnectionProvider>
        where TFLUX_MemoryBuffer : IFLUX_MemoryBuffer<TFLUX_ConnectionProvider, TFLUX_VariableStore>
        where TFLUX_ConnectionProvider : IFLUX_ConnectionProvider<TFLUX_VariableStore>
    {
        public TimeSpan Period { get; }
        public DisposableThread Thread { get; }
        public SourceCache<IFLUX_MemoryReader<TFLUX_MemoryBuffer>, string> MemoryReaders { get; }

        public TFLUX_MemoryBuffer MemoryBuffer { get; }

        private ObservableAsPropertyHelper<bool> _HasMemoryRead;
        public bool HasMemoryRead => _HasMemoryRead.Value;

        public CompositeDisposable Disposables { get; }

        public FLUX_MemoryReaderGroup(TFLUX_MemoryBuffer memory_buffer, TimeSpan period)
        {
            Period = period;
            MemoryBuffer = memory_buffer;
            Disposables = new CompositeDisposable();

            Console.WriteLine($"Starting memory reader {period}");
            MemoryReaders = new SourceCache<IFLUX_MemoryReader<TFLUX_MemoryBuffer>, string>(r => r.Resource)
                .DisposeWith(Disposables);

            _HasMemoryRead = MemoryReaders.Connect()
                .TrueForAll(r => r.WhenAnyValue(r => r.HasMemoryRead), r => r)
                .ToProperty(this, v => v.HasMemoryRead)
                .DisposeWith(Disposables);
          
            Thread = DisposableThread.Start(TryScheduleAsync, period)
                .DisposeWith(Disposables);
        }
        public async Task TryScheduleAsync()
        {
            foreach (var memory_reader in MemoryReaders.Items)
                await memory_reader.TryScheduleAsync();
        }
        public void Dispose()
        {
            Disposables.Dispose();
        }
    }

    public class RRF_ModelReader<TData> : FLUX_MemoryReader<RRF_MemoryBuffer, TData>
    {
        public string Flags { get; }
        private RRF_RequestPriority Priority { get; }
        private Action<Optional<TData>> Action { get; }
        public RRF_ModelReader(RRF_MemoryBuffer memory_buffer, string resource, string flags, RRF_RequestPriority priority, Action<Optional<TData>> action) : base(memory_buffer, resource)
        {
            Flags = flags;
            Action = action;
            Priority = priority;
        }

        public override async Task TryScheduleAsync()
        {
            var connection = MemoryBuffer.ConnectionProvider.Connection;

            if (CTS?.IsCancellationRequested ?? true)
            {
                CTS?.Dispose();
                CTS = new CancellationTokenSource(MemoryBuffer.TaskTimeout);
            }

            var request     = new RRF_Request($"rr_model?key={Resource}&flags={Flags}", HttpMethod.Get, Priority, CTS.Token, MemoryBuffer.RequestTimeout);
            var response    = await connection.ExecuteAsync(request);
            var model_data  = response.GetContent<RRF_ObjectModelResponse<TData>>()
                .Convert(m => m.Result);

            if (model_data.HasValue)
            {
                Action.Invoke(model_data);
                HasMemoryRead = true;
                CTS?.Cancel();
            }
            else
            {
                Console.WriteLine($"{Resource} timeout");
                if (CTS?.IsCancellationRequested ?? false)
                {
                    Console.WriteLine($"{Resource} cancellata");
                    Action.Invoke(default);
                    HasMemoryRead = false;
                }
            }
        }
    }
    public class RRF_FileSystemReader : FLUX_MemoryReader<RRF_MemoryBuffer, FLUX_FileList>
    {
        private RRF_RequestPriority Priority { get; }
        private Action<Optional<FLUX_FileList>> Action { get; }
        public RRF_FileSystemReader(RRF_MemoryBuffer memory_buffer, string resource, RRF_RequestPriority priority, Action<Optional<FLUX_FileList>> action) : base(memory_buffer, resource)
        {
            Action = action;
            Priority = priority;
        }

        public override async Task TryScheduleAsync()
        {
            var connection = MemoryBuffer.ConnectionProvider.Connection;

            Optional<FLUX_FileList> file_list = default;
            var full_file_list = new FLUX_FileList(Resource);

            if (CTS?.IsCancellationRequested ?? true)
            {
                CTS?.Dispose();
                CTS = new CancellationTokenSource(MemoryBuffer.TaskTimeout);
            }

            do
            {
                var first = file_list.ConvertOr(f => f.Next, () => 0);
                var request = new RRF_Request($"rr_filelist?dir={Resource}&first={first}", HttpMethod.Get, Priority, CTS.Token, MemoryBuffer.RequestTimeout);
                var response = await connection.ExecuteAsync(request);

                file_list = response.GetContent<FLUX_FileList>();
                if (!file_list.HasValue)
                    break;

                if (file_list.HasValue)
                    full_file_list.Files.AddRange(file_list.Value.Files);

            } while (file_list.HasValue && file_list.Value.Next != 0);

            if (file_list.HasValue)
            {
                Action.Invoke(full_file_list);
                HasMemoryRead = true;
                CTS?.Cancel();
            }
            else
            {
                Console.WriteLine($"{Resource} timeout");
                if (CTS?.IsCancellationRequested ?? false)
                {
                    Console.WriteLine($"{Resource} cancellata");
                    Action.Invoke(default);
                    HasMemoryRead = false;
                }
            }
        }
    }

    public class RRF_MemoryReaderGroup : FLUX_MemoryReaderGroup<RRF_MemoryBuffer, RRF_ConnectionProvider, RRF_VariableStoreBase>
    {
        public RRF_MemoryReaderGroup(RRF_MemoryBuffer memory_buffer, TimeSpan period) : base(memory_buffer, period)
        {
        }
        public void AddModelReader<TData>(RRF_MemoryBuffer buffer, string flags, RRF_RequestPriority priority, Action<Optional<TData>> model)
        {
            var key = buffer.ModelKeys.Lookup(typeof(TData));
            if (!key.HasValue)
                return;

            var reader = new RRF_ModelReader<TData>(MemoryBuffer, key.Value, flags, priority, model);
            MemoryReaders.AddOrUpdate(reader);
        }
        public void AddFileSytemReader(Func<RRF_ConnectionProvider, string> get_path, RRF_RequestPriority priority, Action<Optional<FLUX_FileList>> file_system)
        {
            var reader = new RRF_FileSystemReader(MemoryBuffer, get_path(MemoryBuffer.ConnectionProvider), priority, file_system);
            MemoryReaders.AddOrUpdate(reader);
        }
    }

    public class RRF_MemoryBuffer : FLUX_MemoryBuffer<RRF_ConnectionProvider, RRF_VariableStoreBase>
    {
        public override RRF_ConnectionProvider ConnectionProvider { get; }

        private SourceCache<RRF_MemoryReaderGroup, TimeSpan> MemoryReaders { get; }

        public RRF_ObjectModel RRFObjectModel { get; }

        private ObservableAsPropertyHelper<bool> _HasFullMemoryRead;
        public override bool HasFullMemoryRead => _HasFullMemoryRead.Value;

        public Dictionary<Type, string> ModelKeys { get; }

        public TimeSpan TaskTimeout { get; }
        public TimeSpan RequestTimeout { get; }

        public RRF_MemoryBuffer(RRF_ConnectionProvider connection_provider)
        {
            ConnectionProvider = connection_provider;
            RRFObjectModel = new RRF_ObjectModel();

            TaskTimeout = TimeSpan.FromSeconds(5);
            RequestTimeout = TimeSpan.FromMilliseconds(500);

            var ultra_fast = TimeSpan.FromMilliseconds(100);
            var fast = TimeSpan.FromMilliseconds(250);
            var medium = TimeSpan.FromMilliseconds(350);
            var slow = TimeSpan.FromMilliseconds(500);
            var job = TimeSpan.FromSeconds(5);
            var extrusion = TimeSpan.FromSeconds(10);

            ModelKeys = new Dictionary<Type, string>()
            {
                { typeof(List<RRF_ObjectModelInput>), "inputs" },
                { typeof(List<RRF_ObjectModelTool>), "tools" },
                { typeof(RRF_ObjectModelSensors), "sensors" },
                { typeof(RRF_ObjectModelState), "state" },
                { typeof(RRF_ObjectModelMove), "move" },
                { typeof(RRF_ObjectModelHeat), "heat" },
                { typeof(RRF_ObjectModelJob), "job" },
                { typeof(RRF_ObjectModelGlobal), "global" },
            };

            MemoryReaders = new SourceCache<RRF_MemoryReaderGroup, TimeSpan>(f => f.Period);

            AddModelReader<RRF_ObjectModelState>(ultra_fast, "f", RRF_RequestPriority.Medium, s => RRFObjectModel.State = s);

            AddModelReader<List<RRF_ObjectModelTool>>(fast, "f", RRF_RequestPriority.Medium, s => RRFObjectModel.Tools = s);
            AddModelReader<RRF_ObjectModelSensors>(fast, "f", RRF_RequestPriority.Medium, s => RRFObjectModel.Sensors = s);
            AddModelReader<RRF_ObjectModelGlobal>(fast, "v", RRF_RequestPriority.Medium, g => RRFObjectModel.Global = g);
            AddModelReader<RRF_ObjectModelJob>(fast, "f", RRF_RequestPriority.Medium, j => RRFObjectModel.Job = j);

            AddModelReader<List<RRF_ObjectModelInput>>(medium, "f", RRF_RequestPriority.Medium, i => RRFObjectModel.Inputs = i);
            AddModelReader<RRF_ObjectModelMove>(medium, "v", RRF_RequestPriority.Medium, m => RRFObjectModel.Move = m);
            AddModelReader<RRF_ObjectModelHeat>(medium, "f", RRF_RequestPriority.Medium, h => RRFObjectModel.Heat = h);

            AddFileSytemReader(slow, c => c.StoragePath, RRF_RequestPriority.Immediate, f => RRFObjectModel.Storage = f);
            AddFileSytemReader(slow, c => c.QueuePath, RRF_RequestPriority.Immediate, f => RRFObjectModel.Queue = f);

            AddFileSytemReader(job, c => c.JobEventPath, RRF_RequestPriority.Immediate, f => RRFObjectModel.JobEvents = f);
            AddFileSytemReader(extrusion, c => c.ExtrusionPath, RRF_RequestPriority.Immediate, f => RRFObjectModel.Extrusions = f);

            _HasFullMemoryRead = MemoryReaders.Connect()
                .TrueForAll(f => f.WhenAnyValue(f => f.HasMemoryRead), r => r)
                .ToProperty(this, v => v.HasFullMemoryRead)
                .DisposeWith(Disposables);
        }

        public override void Initialize(RRF_VariableStoreBase variableStore)
        {
        }

        private void AddModelReader<T>(TimeSpan period, string flags, RRF_RequestPriority priority, Action<Optional<T>> model)
        {
            var memory_reader = MemoryReaders.Lookup(period);
            if (!memory_reader.HasValue)
                MemoryReaders.AddOrUpdate(new RRF_MemoryReaderGroup(this, period));
            memory_reader = MemoryReaders.Lookup(period);
            if (!memory_reader.HasValue)
                return;
            memory_reader.Value.AddModelReader(this, flags, priority, model);
        }

        private void AddFileSytemReader(TimeSpan period, Func<RRF_ConnectionProvider, string> get_path, RRF_RequestPriority priority, Action<Optional<FLUX_FileList>> file_system)
        {
            var memory_reader = MemoryReaders.Lookup(period);
            if (!memory_reader.HasValue)
                MemoryReaders.AddOrUpdate(new RRF_MemoryReaderGroup(this, period));
            memory_reader = MemoryReaders.Lookup(period);
            if (!memory_reader.HasValue)
                return;
            memory_reader.Value.AddFileSytemReader(get_path, priority, file_system);
        }

        public async Task<ValueResult<T>> GetModelDataAsync<T>(CancellationToken ct)
        {
            var connection = ConnectionProvider.Connection;
            var key = ModelKeys.Lookup(typeof(T));
            if (!key.HasValue)
                return default;

            var request = new RRF_Request($"rr_model?key={key.Value}&flags=d99v", HttpMethod.Get, RRF_RequestPriority.Immediate, ct);
            var response = await connection.ExecuteAsync(request);
            return response.GetContent<RRF_ObjectModelResponse<T>>()
                .Convert(r => r.Result);
        }

        public async Task<Optional<T>> GetModelDataAsync<T>(Expression<Func<RRF_ObjectModel, Optional<T>>> dummy_expression, CancellationToken ct)
        {
            var connection = ConnectionProvider.Connection;
            var key = ModelKeys.Lookup(typeof(T));
            if (!key.HasValue)
                return default;

            var request = new RRF_Request($"rr_model?key={key.Value}&flags=d99v", HttpMethod.Get, RRF_RequestPriority.Immediate, ct);
            var response = await connection.ExecuteAsync(request);
            return response.GetContent<RRF_ObjectModelResponse<T>>()
                 .Convert(r => r.Result);
        }
        public IObservable<Optional<TModel>> ObserveModel<TModel>(
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model)
        {
            return get_model(RRFObjectModel)
                .DistinctUntilChanged();
        }
        public IObservable<Optional<TRData>> ObserveModel<TModel, TRData>(
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
            Func<RRF_ConnectionProvider, TModel, Optional<TRData>> get_data)
        {
            return get_model(RRFObjectModel)
                .Convert(s => get_data(ConnectionProvider, s))
                .DistinctUntilChanged();
        }
        public IObservable<Optional<TRData>> ObserveModel<TModel, TRData>(
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
            Func<RRF_ConnectionProvider, TModel, Task<Optional<TRData>>> get_data)
        {
            return get_model(RRFObjectModel)
                .Select(s => Observable.FromAsync(() => s.ConvertAsync(async s => await get_data(ConnectionProvider, s))))
                .Merge(1)
                .DistinctUntilChanged();
        }

        public IObservable<Optional<TRData>> ObserveGlobalModel<TRData>(
            Func<RRF_ObjectModelGlobal, Optional<TRData>> get_data)
        {
            return RRFObjectModel
                .WhenAnyValue(m => m.Global)
                .Convert(s => get_data(s))
                .DistinctUntilChanged();
        }
        public IObservable<Optional<TRData>> ObserveGlobalModel<TRData>(
            Func<RRF_ObjectModelGlobal, Task<Optional<TRData>>> get_data)
        {
            return RRFObjectModel
                .WhenAnyValue(m => m.Global)
                .Convert(s => Observable.FromAsync(async () => await get_data(s)))
                .ValueOr(() => Observable.Return(Optional<TRData>.None))
                .Merge(1)
                .DistinctUntilChanged();
        }
    }
}