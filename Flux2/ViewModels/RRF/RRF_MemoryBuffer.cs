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
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public interface IFLUX_MemoryReaderBase<TFLUX_ConnectionProvider> : IReactiveObject
        where TFLUX_ConnectionProvider : IFLUX_ConnectionProvider
    {
        bool HasMemoryRead { get; }
        TFLUX_ConnectionProvider ConnectionProvider { get; }
    }
    public interface IFLUX_MemoryReader<TFLUX_ConnectionProvider> : IFLUX_MemoryReaderBase<TFLUX_ConnectionProvider>
        where TFLUX_ConnectionProvider : IFLUX_ConnectionProvider
    {
        string Resource { get; }
        Task TryScheduleAsync(TimeSpan timeout);
    }
    public interface IFLUX_MemoryReaderGroup<TFLUX_ConnectionProvider> : IFLUX_MemoryReaderBase<TFLUX_ConnectionProvider>, IDisposable
        where TFLUX_ConnectionProvider : IFLUX_ConnectionProvider
    {
        TimeSpan Period { get; }
        TimeSpan Timeout { get; }
        SourceCache<IFLUX_MemoryReader<TFLUX_ConnectionProvider>, string> MemoryReaders { get; }
    }
    public abstract class FLUX_MemoryReader<TFLUX_ConnectionProvider, TData> : ReactiveObject, IFLUX_MemoryReader<TFLUX_ConnectionProvider>
        where TFLUX_ConnectionProvider : IFLUX_ConnectionProvider
    {
        public int MaxRetries { get; }
        public string Resource { get; }
        public int RetryCount { get; private set; }
        public TFLUX_ConnectionProvider ConnectionProvider { get; }
        private bool _HasMemeoryRead;
        public bool HasMemoryRead
        {
            get => _HasMemeoryRead;
            private set => this.RaiseAndSetIfChanged(ref _HasMemeoryRead, value);
        }
        private Action<Optional<TData>> Action { get; }
        public FLUX_MemoryReader(TFLUX_ConnectionProvider connection_provider, string resource, Action<Optional<TData>> action, int max_retries)
        {
            Action = action;
            Resource = resource;
            MaxRetries = max_retries;
            ConnectionProvider = connection_provider;
        }
        public abstract Task TryScheduleAsync(TimeSpan timeout);
        protected void SetMemoryRead(Optional<TData> data)
        {
            Action?.Invoke(data);
            HasMemoryRead = true;
            RetryCount = 0;
        }
        protected void SetMemoryError()
        {
            RetryCount++;
            if (RetryCount > MaxRetries)
            {
                Action?.Invoke(default);
                HasMemoryRead = false;
            }
        }
    }
    public abstract class FLUX_MemoryReaderGroup<TFLUX_ConnectionProvider, TFLUX_Connection, TFLUX_VariableStore> : ReactiveObject, IFLUX_MemoryReaderGroup<TFLUX_ConnectionProvider>
        where TFLUX_ConnectionProvider : IFLUX_ConnectionProvider<TFLUX_Connection, TFLUX_VariableStore>
        where TFLUX_VariableStore : IFLUX_VariableStore<TFLUX_VariableStore, TFLUX_ConnectionProvider>
        where TFLUX_Connection : IFLUX_Connection<TFLUX_VariableStore>
    {
        public TimeSpan Period { get; }
        public TimeSpan Timeout { get; }
        public DisposableThread Thread { get; }
        public SourceCache<IFLUX_MemoryReader<TFLUX_ConnectionProvider>, string> MemoryReaders { get; }

        public TFLUX_ConnectionProvider ConnectionProvider { get; }

        private ObservableAsPropertyHelper<bool> _HasMemoryRead;
        public bool HasMemoryRead => _HasMemoryRead.Value;

        public CompositeDisposable Disposables { get; }

        public FLUX_MemoryReaderGroup(TFLUX_ConnectionProvider connection_provider, TimeSpan period, TimeSpan timeout)
        {
            Period = period;
            Timeout = timeout;
            ConnectionProvider = connection_provider;
            Disposables = new CompositeDisposable();

            Console.WriteLine($"Starting memory reader {period}");
            MemoryReaders = new SourceCache<IFLUX_MemoryReader<TFLUX_ConnectionProvider>, string>(r => r.Resource)
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
                await memory_reader.TryScheduleAsync(Timeout);
        }
        public void Dispose()
        {
            Disposables.Dispose();
        }
    }

    public class RRF_ModelReader<TData> : FLUX_MemoryReader<RRF_ConnectionProvider, TData>
    {
        public RRF_ModelReader(RRF_ConnectionProvider connection_provider, string resource, int max_retries, Action<Optional<TData>> action) : base(connection_provider, resource, action, max_retries)
        {
        }

        public override async Task TryScheduleAsync(TimeSpan timeout)
        {
            var connection = ConnectionProvider.Connection;
            using var cts = new CancellationTokenSource(timeout);
            var request = new RRF_Request($"rr_model?key={Resource}&flags=d99v", Method.Get, RRF_RequestPriority.Medium, cts.Token);
            var rrf_response = await connection.ExecuteAsync(request);
            if (!rrf_response.Ok)
            {
                SetMemoryError();
                return;
            }

            var model_data = rrf_response.GetContent<RRF_ObjectModelResponse<TData>>();
            if(model_data.HasValue)
                SetMemoryRead(model_data.Value.Result);
        }
    }
    public class RRF_FileSystemReader : FLUX_MemoryReader<RRF_ConnectionProvider, FLUX_FileList>
    {
        public RRF_FileSystemReader(RRF_ConnectionProvider connection_provider, string resource, int max_retries, Action<Optional<FLUX_FileList>> action) : base(connection_provider, resource, action, max_retries)
        {
        }

        public override async Task TryScheduleAsync(TimeSpan timeout)
        {
            var connection = ConnectionProvider.Connection;
            Optional<FLUX_FileList> file_list = default;
            var full_file_list = new FLUX_FileList(Resource);
            do
            {
                using var cts = new CancellationTokenSource(timeout);
                var first = file_list.ConvertOr(f => f.Next, () => 0);
                var request = new RRF_Request($"rr_filelist?dir={Resource}&first={first}", Method.Get, RRF_RequestPriority.Immediate, cts.Token);

                var rrf_response = await connection.ExecuteAsync(request);
                if (!rrf_response.Ok)
                {
                    SetMemoryError();
                    return;
                }

                file_list = rrf_response.GetContent<FLUX_FileList>();
                if (file_list.HasValue)
                    full_file_list.Files.AddRange(file_list.Value.Files);

            } while (file_list.HasValue && file_list.Value.Next != 0);

            SetMemoryRead(full_file_list);
        }
    }

    public class RRF_MemoryReaderGroup : FLUX_MemoryReaderGroup<RRF_ConnectionProvider, RRF_Connection, RRF_VariableStoreBase>
    {
        public RRF_MemoryReaderGroup(RRF_ConnectionProvider connection_provider, TimeSpan period, TimeSpan timeout) : base(connection_provider, period, timeout)
        {
        }
        public void AddModelReader<TData>(RRF_MemoryBuffer buffer, Action<Optional<TData>> model)
        {
            var key = buffer.ModelKeys.Lookup(typeof(TData));
            if (!key.HasValue)
                return;

            var reader = new RRF_ModelReader<TData>(ConnectionProvider, key.Value, 5, model);
            MemoryReaders.AddOrUpdate(reader);
        }
        public void AddFileSytemReader(Func<RRF_ConnectionProvider, string> get_path, Action<Optional<FLUX_FileList>> file_system)
        {
            var reader = new RRF_FileSystemReader(ConnectionProvider, get_path(ConnectionProvider), 5, file_system);
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

        public RRF_MemoryBuffer(RRF_ConnectionProvider connection_provider)
        {
            ConnectionProvider = connection_provider;
            RRFObjectModel = new RRF_ObjectModel();

            var ultra_fast = TimeSpan.FromMilliseconds(200);
            var fast = TimeSpan.FromMilliseconds(300);
            var medium = TimeSpan.FromMilliseconds(500);
            var slow = TimeSpan.FromMilliseconds(750);
            var timeout = TimeSpan.FromMilliseconds(500);

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

            AddModelReader<RRF_ObjectModelState>(ultra_fast, timeout, s => RRFObjectModel.State = s);
            
            AddModelReader<List<RRF_ObjectModelTool>>(fast, timeout, s => RRFObjectModel.Tools = s);
            AddModelReader<RRF_ObjectModelSensors>(fast, timeout, s => RRFObjectModel.Sensors = s);
            AddModelReader<RRF_ObjectModelGlobal>(fast, timeout, g => RRFObjectModel.Global = g);
            AddModelReader<RRF_ObjectModelJob>(fast, timeout, j => RRFObjectModel.Job = j);
            
            AddModelReader<List<RRF_ObjectModelInput>>(medium, timeout, i => RRFObjectModel.Inputs = i);
            AddModelReader<RRF_ObjectModelMove>(medium, timeout, m => RRFObjectModel.Move = m);
            AddModelReader<RRF_ObjectModelHeat>(medium, timeout, h => RRFObjectModel.Heat = h);
            
            AddFileSytemReader(c => c.StoragePath, slow, timeout, f => RRFObjectModel.Storage = f);
            AddFileSytemReader(c => c.QueuePath, slow, timeout, f => RRFObjectModel.Queue = f);

            AddFileSytemReader(c => c.JobEventPath, slow, timeout, f => RRFObjectModel.JobEvents = f);
            AddFileSytemReader(c => c.ExtrusionPath, slow, timeout, f => RRFObjectModel.Extrusions = f);

            _HasFullMemoryRead = MemoryReaders.Connect()
                .TrueForAll(f => f.WhenAnyValue(f => f.HasMemoryRead), r => r)
                .ToProperty(this, v => v.HasFullMemoryRead)
                .DisposeWith(Disposables);
        }

        public override void Initialize(RRF_VariableStoreBase variableStore)
        {
        }

        private void AddModelReader<T>(TimeSpan period, TimeSpan timeout, Action<Optional<T>> model)
        {
            var memory_reader = MemoryReaders.Lookup(period);
            if (!memory_reader.HasValue)
                MemoryReaders.AddOrUpdate(new RRF_MemoryReaderGroup(ConnectionProvider, period, timeout));
            memory_reader = MemoryReaders.Lookup(period);
            if (!memory_reader.HasValue)
                return;
            memory_reader.Value.AddModelReader(this, model);
        }

        private void AddFileSytemReader(Func<RRF_ConnectionProvider, string> get_path, TimeSpan period, TimeSpan timeout, Action<Optional<FLUX_FileList>> file_system)
        {
            var memory_reader = MemoryReaders.Lookup(period);
            if (!memory_reader.HasValue)
                MemoryReaders.AddOrUpdate(new RRF_MemoryReaderGroup(ConnectionProvider, period, timeout));
            memory_reader = MemoryReaders.Lookup(period);
            if (!memory_reader.HasValue)
                return;
            memory_reader.Value.AddFileSytemReader(get_path, file_system);
        }

        public async Task<Optional<T>> GetModelDataAsync<T>(CancellationToken ct)
        {
            var connection = ConnectionProvider.Connection;
            var key = ModelKeys.Lookup(typeof(T));
            if (!key.HasValue)
                return default;

            var request = new RRF_Request($"rr_model?key={key.Value}&flags=d99v", Method.Get, RRF_RequestPriority.Immediate, ct);
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

            var request = new RRF_Request($"rr_model?key={key.Value}&flags=d99v", Method.Get, RRF_RequestPriority.Immediate, ct);
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