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
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public interface IRRF_MemoryReaderBase : IReactiveObject
    {
        bool HasMemoryRead { get; }
        RRF_Connection Connection { get; }
    }
    public interface IRRF_MemoryReader : IRRF_MemoryReaderBase
    {
        string Name { get; }
        string Resource { get; }
    }
    public interface IRRF_MemoryReaderGroup : IRRF_MemoryReaderBase, IDisposable
    {
        TimeSpan Period { get; }
        TimeSpan Timeout { get; }
    }

    public class DisposableThread : IDisposable
    {
        private readonly Task _Task;
        private readonly Stopwatch _Stopwatch;
        private readonly CancellationTokenSource _CancellationTokenSource;

        public DisposableThread(Action action, TimeSpan period)
        {
            _Stopwatch = Stopwatch.StartNew();
            _CancellationTokenSource = new CancellationTokenSource();

            var token = _CancellationTokenSource.Token;
            _Task = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var delay = period - _Stopwatch.Elapsed;
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(period, token);
                    action();
                }
            }, token);
        }
        public DisposableThread(Func<Task> task, TimeSpan period)
        {
            _Stopwatch = Stopwatch.StartNew();
            _CancellationTokenSource = new CancellationTokenSource();

            var token = _CancellationTokenSource.Token;
            _Task = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var delay = period - _Stopwatch.Elapsed;
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(period, token);
                    await task();
                    _Stopwatch.Restart();
                }
            }, token);
        }

        public void Dispose()
        {
            _CancellationTokenSource.Cancel();
            try
            {
                _Task.Wait();
            }
            catch (Exception)
            {
            }
        }
    }

    public class RRF_MemoryReader : ReactiveObject, IRRF_MemoryReader
    {
        public string Name { get; }
        public string Resource { get; }
        public RRF_Connection Connection { get; }
        public Action<RRF_Response> Action { get; }

        private bool _HasMemoryRead;
        public bool HasMemoryRead
        {
            get => _HasMemoryRead;
            private set => this.RaiseAndSetIfChanged(ref _HasMemoryRead, value);
        }

        public RRF_MemoryReader(RRF_Connection connection, string name, string resource, Action<RRF_Response> action)
        {
            Name = name;
            Action = action; 
            Resource = resource;
            Connection = connection;
        }

        public async Task TryScheduleAsync(CancellationToken ct)
        {
            var request = new RRF_Request(Resource, Method.Get, RRF_RequestPriority.Medium, ct);
            var rrf_response = await Connection.Client.ExecuteAsync(request);
            if (!rrf_response.Ok)
                return;
        
            Action?.Invoke(rrf_response);
            HasMemoryRead = true;
        }
    }


    public class RRF_MemoryReaderGroup : ReactiveObject, IRRF_MemoryReaderGroup
    {
        public TimeSpan Period { get; }
        public TimeSpan Timeout { get; }
        public DisposableThread Thread { get; }
        public SourceCache<RRF_MemoryReader, string> MemoryReaders { get; }

        public RRF_Connection Connection { get; }

        private ObservableAsPropertyHelper<bool> _HasMemoryRead;
        public bool HasMemoryRead => _HasMemoryRead.Value;

        public CompositeDisposable Disposables { get; }

        public RRF_MemoryReaderGroup(RRF_Connection connection, TimeSpan period, TimeSpan timeout)
        {
            Period = period;
            Timeout = timeout;
            Connection = connection;
            Disposables = new CompositeDisposable();

            Thread = new DisposableThread(TryScheduleAsync, period)
                .DisposeWith(Disposables);

            MemoryReaders = new SourceCache<RRF_MemoryReader, string>(r => r.Name)
                .DisposeWith(Disposables);

            _HasMemoryRead = MemoryReaders.Connect()
                .TrueForAll(r => r.WhenAnyValue(r => r.HasMemoryRead), r => r)
                .ToProperty(this, v => v.HasMemoryRead)
                .DisposeWith(Disposables);
        }

        public async Task TryScheduleAsync()
        {
            foreach (var memory_reader in MemoryReaders.Items)
            { 
                var cts = new CancellationTokenSource(Timeout);
                await memory_reader.TryScheduleAsync(cts.Token);
            }
        }

        public void AddModelReader<T>(string key, Action<T> model)
        {
            var reader = new RRF_MemoryReader(Connection, key, $"rr_model?key={key}&flags=d99v", r => 
            {
                var model_data = r.GetContent<RRF_ObjectModelResponse<T>>();
                if (model_data.HasValue)
                    model?.Invoke(model_data.Value.Result);
            });
            MemoryReaders.AddOrUpdate(reader);
        }

        public void AddFileSytemReader(string key, Action<FLUX_FileList> file_system)
        {
            var reader = new RRF_MemoryReader(Connection, key, $"rr_filelist?dir={key}", r =>
            {
                var files = r.GetContent<FLUX_FileList>();
                if (files.HasValue)
                    file_system?.Invoke(files.Value);
            });
            MemoryReaders.AddOrUpdate(reader);
        }

        public void Dispose()
        {
            Disposables.Dispose();
        }
    }

    public class RRF_MemoryBuffer : FLUX_MemoryBuffer
    {
        public override RRF_Connection Connection { get; }

        private SourceCache<RRF_MemoryReaderGroup, TimeSpan> MemoryReaders { get; }

        public RRF_ObjectModel RRFObjectModel { get; }
        private IObservable<Optional<Dictionary<string, object>>> GlobalChanged { get; }

        private ObservableAsPropertyHelper<bool> _HasFullMemoryRead;
        public bool HasFullMemoryRead => _HasFullMemoryRead.Value;

        public RRF_MemoryBuffer(RRF_Connection connection)
        {
            Connection = connection;
            RRFObjectModel = new RRF_ObjectModel();
            GlobalChanged = RRFObjectModel.WhenAnyValue(m => m.Global);

            var ultra_fast = TimeSpan.FromMilliseconds(100);
            var fast = TimeSpan.FromMilliseconds(200);
            var medium = TimeSpan.FromMilliseconds(350);
            var slow = TimeSpan.FromMilliseconds(500);
            var timeout = TimeSpan.FromMilliseconds(5000);

            MemoryReaders = new SourceCache<RRF_MemoryReaderGroup, TimeSpan>(f => f.Period);

            AddModelReader<RRF_ObjectModelState>("state", ultra_fast, timeout, s => RRFObjectModel.State = s);
            
            AddModelReader<List<RRF_ObjectModelTool>>("tools", fast, timeout, s => RRFObjectModel.Tools = s);
            AddModelReader<RRF_ObjectModelSensors>("sensors", fast, timeout, s => RRFObjectModel.Sensors = s);
            AddModelReader<JObject>("global", fast, timeout, g => RRFObjectModel.Global = g.ToObject<Dictionary<string, object>>());
            
            AddModelReader<RRF_ObjectModelMove>("move", medium, timeout, m => RRFObjectModel.Move = m);
            AddModelReader<RRF_ObjectModelHeat>("heat", medium, timeout, h => RRFObjectModel.Heat = h);
            AddFileSytemReader("gcodes/queue", medium, timeout, f => RRFObjectModel.Queue = f);
            AddFileSytemReader("gcodes/storage", medium, timeout, f => RRFObjectModel.Storage = f);
            
            AddModelReader<RRF_ObjectModelJob>("job", medium, timeout, j => RRFObjectModel.Job = j);
            AddModelReader<List<RRF_ObjectModelInput>>("inputs", medium, timeout, i => RRFObjectModel.Inputs = i);

            _HasFullMemoryRead = MemoryReaders.Connect()
                .TrueForAll(f => f.WhenAnyValue(f => f.HasMemoryRead), r => r)
                .ToProperty(this, v => v.HasFullMemoryRead)
                .DisposeWith(Disposables);
        }

        private void AddModelReader<T>(string key, TimeSpan period, TimeSpan timeout, Action<T> model)
        {
            var memory_reader = MemoryReaders.Lookup(period);
            if (!memory_reader.HasValue)
                MemoryReaders.AddOrUpdate(new RRF_MemoryReaderGroup(Connection, period, timeout));
            memory_reader = MemoryReaders.Lookup(period);
            if (!memory_reader.HasValue)
                return;
            memory_reader.Value.AddModelReader(key, model);
        }

        private void AddFileSytemReader(string key, TimeSpan period, TimeSpan timeout, Action<FLUX_FileList> file_system)
        {
            var memory_reader = MemoryReaders.Lookup(period);
            if (!memory_reader.HasValue)
                MemoryReaders.AddOrUpdate(new RRF_MemoryReaderGroup(Connection, period, timeout));
            memory_reader = MemoryReaders.Lookup(period);
            if (!memory_reader.HasValue)
                return;
            memory_reader.Value.AddFileSytemReader(key, file_system);
        }

        public async Task<Optional<T>> GetModelDataAsync<T>(string key, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            return await GetModelDataAsync<T>(key, cts.Token);
        }
        public async Task<Optional<T>> GetModelDataAsync<T>(string key, CancellationToken ct)
        {
            var request = new RRF_Request($"rr_model?key={key}&flags=d99v", Method.Get, RRF_RequestPriority.Immediate, ct);
            var response = await Connection.Client.ExecuteAsync(request);
            return response.GetContent<RRF_ObjectModelResponse<T>>()
                .Convert(r => r.Result);
        }

        public IObservable<Optional<TRData>> ObserveState<TState, TRData>(
            Func<RRF_ObjectModel, IObservable<Optional<TState>>> get_state,
            Func<RRF_Connection, TState, Optional<TRData>> get_data)
        {
            return get_state(RRFObjectModel)
                .Convert(s => get_data(Connection, s))
                .DistinctUntilChanged();
        }
        public IObservable<Optional<TRData>> ObserveState<TState, TRData>(
            Func<RRF_ObjectModel, IObservable<Optional<TState>>> get_state,
            Func<RRF_Connection, TState, Task<Optional<TRData>>> get_data)
        {
            return get_state(RRFObjectModel)
                .Select(s => Observable.FromAsync(() => s.ConvertAsync(async s => await get_data(Connection, s))))
                .Merge(1)
                .DistinctUntilChanged();
        }

        public IObservable<Optional<TRData>> ObserveGlobalState<TRData>(
            Func<Optional<Dictionary<string, object>>, Optional<TRData>> get_data)
        {
            return GlobalChanged.Select(s => get_data(s))
                .DistinctUntilChanged();
        }
        public IObservable<Optional<TRData>> ObserveGlobalState<TRData>(
            Func<Optional<Dictionary<string, object>>, Task<Optional<TRData>>> get_data)
        {
            return GlobalChanged
                .Select(s => Observable.FromAsync(async () => await get_data(s)))
                .Merge(1)
                .DistinctUntilChanged();
        }
    }
}

/*using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public interface IRRF_MemoryReader : IReactiveObject
    {
        string Name { get; }
        TimeSpan Period { get; }
        TimeSpan Timeout { get; }
        bool HasMemoryRead { get; }
        RRF_Connection Connection { get; }
        Task TryScheduleAsync();
    }

    public class DisposableThread : IDisposable
    {
        private readonly Task _Task;
        private readonly Stopwatch _Stopwatch;
        private readonly CancellationTokenSource _CancellationTokenSource;

        public DisposableThread(Action action, TimeSpan period)
        {
            _Stopwatch = Stopwatch.StartNew();
            _CancellationTokenSource = new CancellationTokenSource();

            var token = _CancellationTokenSource.Token;
            _Task = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var delay = period - _Stopwatch.Elapsed;
                    if(delay > TimeSpan.Zero)
                        await Task.Delay(period, token);
                    action();
                }
            }, token);
        }
        public DisposableThread(Func<Task> task, TimeSpan period)
        {
            _Stopwatch = Stopwatch.StartNew();
            _CancellationTokenSource = new CancellationTokenSource();

            var token = _CancellationTokenSource.Token;
            _Task = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var delay = period - _Stopwatch.Elapsed;
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(period, token);
                    await task();
                }
            }, token);
        }

        public void Dispose()
        {
            _CancellationTokenSource.Cancel();
            try
            {
                _Task.Wait();
            }
            catch (Exception)
            {
            }
        }
    }

    public class RRF_MemoryReader : ReactiveObject, IRRF_MemoryReader
    {
        public string Name { get; }
        public string Request { get; }
        public TimeSpan Period { get; }
        public TimeSpan Timeout { get; }
        public Action<RRF_Response> MemoryRead { get; }

        public RRF_Connection Connection { get; }

        private bool _HasMemoryRead;
        public bool HasMemoryRead
        {
            get => _HasMemoryRead;
            private set => this.RaiseAndSetIfChanged(ref _HasMemoryRead, value);
        }

        public RRF_MemoryReader(RRF_Connection connection, string name, string request, TimeSpan period, TimeSpan timeout, Action<RRF_Response> memory_read)
        {
            Name = name;
            Period = period;
            Timeout = timeout;
            Request = request;
            Connection = connection;
            MemoryRead = memory_read;
        }

        public async Task TryScheduleAsync()
        {
            var cts = new CancellationTokenSource(Timeout);
            var request = new RestRequest(Request, Method.Get);
            var rrf_response = await Connection.PostRequestAsync(request, cts.Token);
            if (rrf_response.HasValue)
            { 
                MemoryRead?.Invoke(rrf_response.Value);
                HasMemoryRead = true;
            }
        }
    }

    public class RRF_MemoryBuffer : FLUX_MemoryBuffer
    {
        public override RRF_Connection Connection { get; }

        private IObservableCache<DisposableThread, TimeSpan> MemoryThreads { get; }
        private SourceCache<IRRF_MemoryReader, string> MemoryReaders { get; }

        public RRF_ObjectModel RRFObjectModel { get; }
        private IObservable<Optional<Dictionary<string, object>>> GlobalChanged { get; }

        private ObservableAsPropertyHelper<bool> _HasFullMemoryRead;
        public bool HasFullMemoryRead => _HasFullMemoryRead.Value;

        public RRF_MemoryBuffer(RRF_Connection connection)
        {
            Connection = connection;
            RRFObjectModel = new RRF_ObjectModel();
            GlobalChanged = RRFObjectModel.WhenAnyValue(m => m.Global);

            var ultra_fast = TimeSpan.FromMilliseconds(200);
            var fast = TimeSpan.FromMilliseconds(350);
            var medium = TimeSpan.FromMilliseconds(350);
            var slow = TimeSpan.FromMilliseconds(500);
            var timeout = TimeSpan.FromMilliseconds(2000);

            MemoryReaders = new SourceCache<IRRF_MemoryReader, string>(f => f.Name);
            MemoryThreads = MemoryReaders.Connect()
                .Group(r => r.Timeout)
                .Transform(g =>
                {
                    return new DisposableThread(async () =>
                    {
                        foreach(var reader in g.Cache.Items)
                            await reader.TryScheduleAsync();
                    }, g.Key);
                }, true)
                .DisposeMany()
                .AsObservableCache()
                .DisposeWith(Disposables);

            // Model
            MemoryReaders.Edit(r =>
            {
                AddModelReader<RRF_ObjectModelState>(r, "state", ultra_fast, timeout, s => RRFObjectModel.State = s);

                AddModelReader<RRF_ObjectModelJob>(r, "job", fast, timeout, j => RRFObjectModel.Job = j);
                AddModelReader<List<RRF_ObjectModelTool>>(r, "tools", fast, timeout, s => RRFObjectModel.Tools = s);
                AddModelReader<RRF_ObjectModelSensors>(r, "sensors", fast, timeout, s => RRFObjectModel.Sensors = s);
                AddModelReader<JObject>(r, "global", fast, timeout, g => RRFObjectModel.Global = g.ToObject<Dictionary<string, object>>());
                AddModelReader<JObject>(r, "queue", fast, timeout, g => RRFObjectModel.Global = g.ToObject<Dictionary<string, object>>());

                AddModelReader<RRF_ObjectModelMove>(r, "move", medium, timeout, m => RRFObjectModel.Move = m);
                AddModelReader<RRF_ObjectModelHeat>(r, "heat", medium, timeout, h => RRFObjectModel.Heat = h);
                AddModelReader<List<RRF_ObjectModelInput>>(r, "inputs", medium, timeout, i => RRFObjectModel.Inputs = i);

                AddModelReader<RRF_ObjectModelSeqs>(r, "seqs", slow, timeout, s => RRFObjectModel.Seqs = s);
                AddModelReader<List<RRF_ObjectModelFan>>(r, "fans", slow, timeout, f => RRFObjectModel.Fans = f);
                AddModelReader<List<RRF_ObjectModelBoard>>(r, "boards", slow, timeout, b => RRFObjectModel.Boards = b);

                //AddModelReader<List<RRF_ObjectModelSpindle>>("spindles", slow, timeout, IRRF_RequestPriority.High, s => RRFObjectModel.Spindles = s);

                // File System
                AddFileSytemReader(r, "gcodes/queue", medium, timeout, f => RRFObjectModel.Queue = f);
                AddFileSytemReader(r, "gcodes/storage", medium, timeout, f => RRFObjectModel.Storage = f);
            });

            _HasFullMemoryRead = MemoryReaders.Connect()
                .TrueForAll(f => f.WhenAnyValue(f => f.HasMemoryRead), r => r)
                .ToProperty(this, v => v.HasFullMemoryRead)
                .DisposeWith(Disposables);
        }

        private void AddModelReader<T>(ISourceUpdater<IRRF_MemoryReader, string> readers, string key, TimeSpan period, TimeSpan timeout, Action<T> model)
        {
            var model_reader = new RRF_MemoryReader(Connection, key, $"rr_model?key={key}&flags=d99v", period, timeout, r =>
            {
                var model_data = r.GetObjectModel<T>();
                if (model_data.HasValue)
                    model?.Invoke(model_data.Value);
            });
            readers.AddOrUpdate(model_reader);
        }

        private void AddFileSytemReader(ISourceUpdater<IRRF_MemoryReader, string> readers, string key, TimeSpan period, TimeSpan timeout, Action<FLUX_FileList> file_system)
        {
            var file_system_reader = new RRF_MemoryReader(Connection, key, $"rr_filelist?dir={key}", period, timeout, r =>
            {
                var files = r.GetFileSystem();
                if (files.HasValue)
                    file_system?.Invoke(files.Value);
            });
            readers.AddOrUpdate(file_system_reader);
        }

        public async Task<Optional<T>> GetModelDataAsync<T>(string key, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            return await GetModelDataAsync<T>(key, cts.Token);
        }
        public async Task<Optional<T>> GetModelDataAsync<T>(string key, CancellationToken ct)
        {
            var response = await Connection.PostRequestAsync(new RestRequest($"rr_model?key={key}&flags=d99v", Method.Get), ct);
            return response.Convert(r => r.GetObjectModel<T>());
        }

        public IObservable<Optional<TRData>> ObserveState<TState, TRData>(
            Func<RRF_ObjectModel, IObservable<Optional<TState>>> get_state,
            Func<RRF_Connection, TState, Optional<TRData>> get_data)
        {
            return get_state(RRFObjectModel)
                .Convert(s => get_data(Connection, s))
                .DistinctUntilChanged();
        }
        public IObservable<Optional<TRData>> ObserveState<TState, TRData>(
            Func<RRF_ObjectModel, IObservable<Optional<TState>>> get_state,
            Func<RRF_Connection, TState, Task<Optional<TRData>>> get_data)
        {
            return get_state(RRFObjectModel)
                .Select(s => Observable.FromAsync(() => s.ConvertAsync(async s => await get_data(Connection, s))))
                .Merge(1)
                .DistinctUntilChanged();
        }

        public IObservable<Optional<TRData>> ObserveGlobalState<TRData>(
            Func<Optional<Dictionary<string, object>>, Optional<TRData>> get_data)
        {
            return GlobalChanged.Select(s => get_data(s))
                .DistinctUntilChanged();
        }
        public IObservable<Optional<TRData>> ObserveGlobalState<TRData>(
            Func<Optional<Dictionary<string, object>>, Task<Optional<TRData>>> get_data)
        {
            return GlobalChanged
                .Select(s => Observable.FromAsync(async () => await get_data(s)))
                .Merge(1)
                .DistinctUntilChanged();
        }
    }
}*/