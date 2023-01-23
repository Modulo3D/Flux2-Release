using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public interface IRRF_MemoryReader : IFLUX_MemoryReader<RRF_MemoryBuffer>
    {
        Task TryScheduleAsync(RRF_RequestPriority priority, CancellationToken ct);
    }
    public interface IRRF_MemoryReader<TData> : IRRF_MemoryReader, IFLUX_MemoryReader<RRF_MemoryBuffer, TData>
    {
        new Task<Optional<TData>> TryScheduleAsync(RRF_RequestPriority priority, CancellationToken ct);
    }
    public abstract class RRF_MemoryReader<TData> : FLUX_MemoryReader<RRF_MemoryBuffer, TData>, IRRF_MemoryReader<TData>
    {
        private RRF_RequestPriority Priority { get; }
        protected Action<Optional<TData>> Action { get; }
        public CancellationTokenSource CTS { get; private set; }
        protected RRF_MemoryReader(RRF_MemoryBuffer memory_buffer, string resource, RRF_RequestPriority priority, Action<Optional<TData>> action) : base(memory_buffer, resource)
        {
            Action = action;
            Priority = priority;
        }

        public override async Task<Optional<TData>> TryScheduleAsync()
        {
            if (CTS?.IsCancellationRequested ?? true)
            {
                CTS?.Dispose();
                CTS = new CancellationTokenSource(MemoryBuffer.TaskTimeout);
            }

            var data = await TryScheduleAsync(Priority, CTS.Token);

            if (data.HasValue)
            {
                Action.Invoke(data);
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

            return data;
        }
        public abstract Task<Optional<TData>> TryScheduleAsync(RRF_RequestPriority priority, CancellationToken ct);
        Task IRRF_MemoryReader.TryScheduleAsync(RRF_RequestPriority priority, CancellationToken ct) => TryScheduleAsync(priority, ct);
    }

    public class RRF_ModelReader<TData> : RRF_MemoryReader<TData>
    {
        public string Flags { get; }
        public RRF_ModelReader(RRF_MemoryBuffer memory_buffer, string resource, string flags, RRF_RequestPriority priority, Action<Optional<TData>> action)
            : base(memory_buffer, resource, priority, action)
        {
            Flags = flags;
        }

        public override async Task<Optional<TData>> TryScheduleAsync(RRF_RequestPriority priority, CancellationToken ct)
        {
            var connection = MemoryBuffer.ConnectionProvider.Connection;
            var request = new RRF_Request<RRF_ObjectModelResponse<TData>>($"rr_model?key={Resource}&flags={Flags}", HttpMethod.Get, priority, ct, MemoryBuffer.RequestTimeout);
            var response = await connection.ExecuteAsync(request);
            return response.Content.Convert(m => m.Result);
        }
    }
    public class RRF_FileSystemReader : RRF_MemoryReader<FLUX_FileList>
    {
        public RRF_FileSystemReader(RRF_MemoryBuffer memory_buffer, string resource, RRF_RequestPriority priority, Action<Optional<FLUX_FileList>> action)
            : base(memory_buffer, resource, priority, action)
        {
        }

        public override async Task<Optional<FLUX_FileList>> TryScheduleAsync(RRF_RequestPriority priority, CancellationToken ct)
        {
            var connection = MemoryBuffer.ConnectionProvider.Connection;

            Optional<FLUX_FileList> file_list = default;
            var full_file_list = new FLUX_FileList(Resource);

            do
            {
                var first = file_list.ConvertOr(f => f.Next, () => 0);
                var request = new RRF_Request<FLUX_FileList>($"rr_filelist?dir={Resource}&first={first}", HttpMethod.Get, priority, ct, MemoryBuffer.RequestTimeout);
                var response = await connection.ExecuteAsync(request);
                    
                file_list = response.Content;
                if (!file_list.HasValue)
                    break;

                if (file_list.HasValue)
                    full_file_list.Files.AddRange(file_list.Value.Files);

            } while (file_list.HasValue && file_list.Value.Next != 0);

            if (file_list.HasValue)
                return full_file_list;
            return default;
        }
    }

    public class RRF_MemoryReaderGroup : FLUX_MemoryReaderGroup<RRF_MemoryBuffer, RRF_ConnectionProvider, RRF_VariableStoreBase>
    {
        public RRF_MemoryReaderGroup(RRF_MemoryBuffer memory_buffer, TimeSpan period) : base(memory_buffer, period)
        {
        }
        public RRF_ModelReader<TData> AddModelReader<TData>(string path, string flags, RRF_RequestPriority priority, Action<Optional<TData>> model)
        {
            var reader = new RRF_ModelReader<TData>(MemoryBuffer, path, flags, priority, model);
            MemoryReaders.AddOrUpdate(reader);
            return reader;
        }
        public RRF_FileSystemReader AddFileSytemReader(Func<RRF_ConnectionProvider, string> get_path, RRF_RequestPriority priority, Action<Optional<FLUX_FileList>> file_system)
        {
            var reader = new RRF_FileSystemReader(MemoryBuffer, get_path(MemoryBuffer.ConnectionProvider), priority, file_system);
            MemoryReaders.AddOrUpdate(reader);
            return reader;
        }
    }

    public class RRF_MemoryBuffer : FLUX_MemoryBuffer<RRF_ConnectionProvider, RRF_VariableStoreBase>
    {
        public override RRF_ConnectionProvider ConnectionProvider { get; }

        public Dictionary<string, IRRF_MemoryReader> MemoryReaders { get; }
        private SourceCache<RRF_MemoryReaderGroup, TimeSpan> MemoryReaderGroups { get; }

        public RRF_ObjectModel RRFObjectModel { get; }

        private readonly ObservableAsPropertyHelper<bool> _HasFullMemoryRead;
        public override bool HasFullMemoryRead => _HasFullMemoryRead.Value;
        public TimeSpan TaskTimeout { get; }
        public TimeSpan RequestTimeout { get; }

        public RRF_MemoryBuffer(RRF_ConnectionProvider connection_provider)
        {
            ConnectionProvider = connection_provider;
            RRFObjectModel = new RRF_ObjectModel();

            TaskTimeout = TimeSpan.FromSeconds(10);
            RequestTimeout = TimeSpan.FromMilliseconds(500);

            var ultra_fast = TimeSpan.FromMilliseconds(350);
            var fast = TimeSpan.FromMilliseconds(500);
            var medium = TimeSpan.FromMilliseconds(750);
            var slow = TimeSpan.FromMilliseconds(1000);
            var job = TimeSpan.FromSeconds(5);
            var extrusion = TimeSpan.FromSeconds(5);

            MemoryReaders = new Dictionary<string, IRRF_MemoryReader>();
            MemoryReaderGroups = new SourceCache<RRF_MemoryReaderGroup, TimeSpan>(f => f.Period);

            AddModelReader(ultra_fast, "state", "f", m => m.State, RRF_RequestPriority.Medium);
            AddModelReader(fast, "tools", "f", m => m.Tools, RRF_RequestPriority.Medium);
            AddModelReader(fast, "sensors", "f", m => m.Sensors, RRF_RequestPriority.Medium);
            AddModelReader(fast, "global", "v", m => m.Global, RRF_RequestPriority.Medium);
            AddModelReader(fast, "job", "v", m => m.FluxJob, RRF_RequestPriority.Medium);
            AddModelReader(medium, "inputs", "f", m => m.Inputs, RRF_RequestPriority.Medium);
            AddModelReader(medium, "move", "v", m => m.Move, RRF_RequestPriority.Medium);
            AddModelReader(medium, "heat", "f", m => m.Heat, RRF_RequestPriority.Medium);

            AddFileSytemReader(slow, c => c.QueuePath, m => m.Queue, RRF_RequestPriority.Immediate);
            AddFileSytemReader(slow, c => c.StoragePath, m => m.Storage, RRF_RequestPriority.Immediate);
            AddFileSytemReader(job, c => c.JobEventPath, m => m.JobEvents, RRF_RequestPriority.Immediate);
            AddFileSytemReader(extrusion, c => c.ExtrusionEventPath, m => m.Extrusions, RRF_RequestPriority.Immediate);

            _HasFullMemoryRead = MemoryReaderGroups.Connect()
                .TrueForAll(f => f.WhenAnyValue(f => f.HasMemoryRead), r => r)
                .ToProperty(this, v => v.HasFullMemoryRead)
                .DisposeWith(Disposables);
        }

        public override void Initialize(RRF_VariableStoreBase variableStore)
        {
        }

        private void AddModelReader<T>(TimeSpan period, string path, string flags, Expression<Func<RRF_ObjectModel, Optional<T>>> model, RRF_RequestPriority priority)
        {
            var memory_reader = MemoryReaderGroups.Lookup(period);
            if (!memory_reader.HasValue)
                MemoryReaderGroups.AddOrUpdate(new RRF_MemoryReaderGroup(this, period));
            memory_reader = MemoryReaderGroups.Lookup(period);
            if (!memory_reader.HasValue)
                return;
            var setter = model.GetCachedSetterDelegate();
            MemoryReaders.Add(model.ToString(), memory_reader.Value.AddModelReader<T>(path, flags, priority, v => setter(RRFObjectModel, v)));
        }

        private void AddFileSytemReader(TimeSpan period, Func<RRF_ConnectionProvider, string> get_path, Expression<Func<RRF_ObjectModel, Optional<FLUX_FileList>>> file_system, RRF_RequestPriority priority)
        {
            var memory_reader = MemoryReaderGroups.Lookup(period);
            if (!memory_reader.HasValue)
                MemoryReaderGroups.AddOrUpdate(new RRF_MemoryReaderGroup(this, period));
            memory_reader = MemoryReaderGroups.Lookup(period);
            if (!memory_reader.HasValue)
                return;
            var setter = file_system.GetCachedSetterDelegate();
            MemoryReaders.Add(file_system.ToString(), memory_reader.Value.AddFileSytemReader(get_path, priority, v => setter(RRFObjectModel, v)));
        }

        public async Task<Optional<TData>> GetModelDataAsync<TData>(Expression<Func<RRF_ObjectModel, Optional<TData>>> dummy_expression, CancellationToken ct)
        {
            var memory_reader = MemoryReaders.Lookup(dummy_expression.ToString());
            if (!memory_reader.HasValue)
                return default;
            return await ((IRRF_MemoryReader<TData>)memory_reader.Value).TryScheduleAsync(RRF_RequestPriority.Immediate, ct);
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
                .Select(s => Observable.FromAsync(() => s.ConvertAsync(s => get_data(ConnectionProvider, s))))
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
                .ConvertMany(s => Observable.FromAsync(() => get_data(s)))
                .DistinctUntilChanged();
        }
    }
}