using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public interface IRRF_MemoryReader : IReactiveObject, IDisposable
    {
        string Name { get; }
        bool Processed { get; }
        TimeSpan Period { get; }
        TimeSpan Timeout { get; }
        bool HasMemoryRead { get; }
        DateTime Scheduled { get; }
        RRF_Connection Connection { get; }
        IRRF_RequestPriority Priority { get; }

        void TrySchedule();
        void ResetScheduling();
    }

    public class RRF_MemoryReader : ReactiveObject, IRRF_MemoryReader
    {
        public string Name { get; }
        public string Request { get; }
        public TimeSpan Period { get; }
        public TimeSpan Timeout { get; }
        public Action<RRF_Response> MemoryRead { get; }
        public IRRF_RequestPriority Priority { get; }
        public CancellationTokenSource CTS { get; private set; }
        public CancellationTokenRegistration CTR { get; private set; }

        private bool _Processed;
        public bool Processed
        {
            get => _Processed;
            private set => this.RaiseAndSetIfChanged(ref _Processed, value);
        }

        private DateTime _Scheduled;

        public RRF_Connection Connection { get; }
        public DateTime Scheduled
        {
            get => _Scheduled;
            private set => this.RaiseAndSetIfChanged(ref _Scheduled, value);
        }

        private bool _HasMemoryRead;
        public bool HasMemoryRead
        {
            get => _HasMemoryRead;
            private set => this.RaiseAndSetIfChanged(ref _HasMemoryRead, value);
        }

        public RRF_MemoryReader(RRF_Connection connection, string name, string request, TimeSpan period, TimeSpan timeout, IRRF_RequestPriority priority, Action<RRF_Response> memory_read)
        {
            Name = name;
            Period = period;
            Processed = true;
            Timeout = timeout;
            Request = request;
            Priority = priority;
            Connection = connection;
            MemoryRead = memory_read;
            Scheduled = DateTime.MinValue;
        }

        public void TrySchedule()
        {
            if (!Processed)
                return;
            if (DateTime.Now - Scheduled < Period)
                return;

            Processed = false;
            Scheduled = DateTime.Now;

            CTR.Dispose();
            CTS?.Dispose();
            CTS = new CancellationTokenSource(Timeout);
            CTR = CTS.Token.Register(() => Processed = true);

            Connection.PostRequest(
                new RestRequest(Request, Method.Get),
                Priority,
                response =>
                {
                    Processed = true;
                    HasMemoryRead = true;
                    if (response.HasValue)
                        MemoryRead?.Invoke(response.Value);
                }, CTS?.Token ?? CancellationToken.None);
        }
        public void ResetScheduling()
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
                    CTR.Dispose();
                    CTS?.Dispose();
                    Processed = true;
                    HasMemoryRead = false;
                    Scheduled = DateTime.MinValue;
                }
                catch
                {
                }
            }
        }

        public void Dispose()
        {
            ResetScheduling();
        }
    }

    public class RRF_MemoryBuffer : FLUX_MemoryBuffer
    {
        public override RRF_Connection Connection { get; }

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

            var ultra_fast = TimeSpan.FromMilliseconds(500);
            var fast = TimeSpan.FromMilliseconds(750);
            var medium = TimeSpan.FromMilliseconds(1000);
            var slow = TimeSpan.FromMilliseconds(1500);
            var timeout = TimeSpan.FromMilliseconds(2000);

            MemoryReaders = new SourceCache<IRRF_MemoryReader, string>(f => f.Name);

            // Model
            AddModelReader<RRF_ObjectModelState>("state", ultra_fast, timeout, IRRF_RequestPriority.High, s => RRFObjectModel.State = s);

            AddModelReader<RRF_ObjectModelJob>("job", fast, timeout, IRRF_RequestPriority.High, j => RRFObjectModel.Job = j);
            AddModelReader<List<RRF_ObjectModelTool>>("tools", fast, timeout, IRRF_RequestPriority.High, s => RRFObjectModel.Tools = s);
            AddModelReader<RRF_ObjectModelSensors>("sensors", fast, timeout, IRRF_RequestPriority.High, s => RRFObjectModel.Sensors = s);
            AddModelReader<JObject>("global", fast, timeout, IRRF_RequestPriority.High, g => RRFObjectModel.Global = g.ToObject<Dictionary<string, object>>());
            AddModelReader<JObject>("queue", fast, timeout, IRRF_RequestPriority.High, g => RRFObjectModel.Global = g.ToObject<Dictionary<string, object>>());

            AddModelReader<RRF_ObjectModelMove>("move", medium, timeout, IRRF_RequestPriority.Medium, m => RRFObjectModel.Move = m);
            AddModelReader<List<RRF_ObjectModelInput>>("inputs", medium, timeout, IRRF_RequestPriority.Medium, i => RRFObjectModel.Inputs = i);

            AddModelReader<RRF_ObjectModelSeqs>("seqs", slow, timeout, IRRF_RequestPriority.Medium, s => RRFObjectModel.Seqs = s);
            AddModelReader<RRF_ObjectModelHeat>("heat", slow, timeout, IRRF_RequestPriority.Medium, h => RRFObjectModel.Heat = h);
            AddModelReader<List<RRF_ObjectModelFan>>("fans", slow, timeout, IRRF_RequestPriority.Medium, f => RRFObjectModel.Fans = f);
            AddModelReader<List<RRF_ObjectModelBoard>>("boards", slow, timeout, IRRF_RequestPriority.Medium, b => RRFObjectModel.Boards = b);

            //AddModelReader<List<RRF_ObjectModelSpindle>>("spindles", slow, timeout, IRRF_RequestPriority.High, s => RRFObjectModel.Spindles = s);

            // File System
            AddFileSytemReader("gcodes/queue", medium, timeout, IRRF_RequestPriority.Medium, f => RRFObjectModel.Queue = f);
            AddFileSytemReader("gcodes/storage", medium, timeout, IRRF_RequestPriority.Medium, f => RRFObjectModel.Storage = f);

            _HasFullMemoryRead = MemoryReaders.Connect()
                .TrueForAll(f => f.WhenAnyValue(f => f.HasMemoryRead), r => r)
                .ToProperty(this, v => v.HasFullMemoryRead)
                .DisposeWith(Disposables);
        }

        private void AddModelReader<T>(string key, TimeSpan period, TimeSpan timeout, IRRF_RequestPriority priority, Action<T> model)
        {
            var model_reader = new RRF_MemoryReader(Connection, key, $"rr_model?key={key}&flags=d99v", period, timeout, priority, r =>
            {
                var model_data = r.GetObjectModel<T>();
                if (model_data.HasValue)
                    model?.Invoke(model_data.Value);
            });
            model_reader.DisposeWith(Disposables);
            MemoryReaders.AddOrUpdate(model_reader);
        }

        private void AddFileSytemReader(string key, TimeSpan period, TimeSpan timeout, IRRF_RequestPriority priority, Action<FLUX_FileList> file_system)
        {
            var file_system_reader = new RRF_MemoryReader(Connection, key, $"rr_filelist?dir={key}", period, timeout, priority, r =>
            {
                var files = r.GetFileSystem();
                if (files.HasValue)
                    file_system?.Invoke(files.Value);
            });
            file_system_reader.DisposeWith(Disposables);
            MemoryReaders.AddOrUpdate(file_system_reader);
        }

        public void UpdateBuffer()
        {
            foreach (var fence in MemoryReaders.Items)
                fence.TrySchedule();
        }
        public void ResetBuffer()
        {
            foreach (var fence in MemoryReaders.Items)
                fence.ResetScheduling();
        }

        public async Task<Optional<T>> GetModelDataAsync<T>(string key, IRRF_RequestPriority priority, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            return await GetModelDataAsync<T>(key, priority, cts.Token);
        }
        public async Task<Optional<T>> GetModelDataAsync<T>(string key, IRRF_RequestPriority priority, CancellationToken ct)
        {
            var response = await Connection.PostRequestAsync(
                new RestRequest($"rr_model?key={key}&flags=d99v", Method.Get),
                priority,
                ct);
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
}
