using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public enum IRRF_RequestPriority
    {
        Immediate = 0,
        High = 1,
        Medium = 2,
        Low = 3
    }

    public struct RRF_Request
    {
        public int Id { get; }
        public RestRequest Request { get; }
        public CancellationToken CancellationToken { get; }
        public Action<Optional<RRF_Response>> Action { get; }
        public RRF_Request(RestRequest request, int id, Action<Optional<RRF_Response>> action = default, CancellationToken ct = default)
        {
            Id = id;
            Action = action;
            Request = request;
            CancellationToken = ct;
        }
    }

    public struct RRF_Response
    {
        public int Id { get; }
        public RestResponse Response { get; }
        public RRF_Response(int id, RestResponse response)
        {
            Id = id;
            Response = response;
        }

        public Optional<T> GetObjectModel<T>()
        {
            if ((Response?.StatusCode ?? 0) != HttpStatusCode.OK)
                return default;

            return JsonUtils.Deserialize<RRF_ObjectModelResponse<T>>(Response.Content)
                .Convert(v => v.Result);
        }

        public Optional<FLUX_FileList> GetFileSystem()
        {
            if ((Response?.StatusCode ?? 0) != HttpStatusCode.OK)
                return default;

            return JsonUtils.Deserialize<FLUX_FileList>(Response.Content);
        }
    }

    public class RRF_Connection : FLUX_Connection<RRF_VariableStore, RestClient, RRF_MemoryBuffer>
    {
        private Random Random { get; }
        private RRF_MemoryBuffer _MemoryBuffer;
        public override RRF_MemoryBuffer MemoryBuffer
        {
            get
            {
                if (_MemoryBuffer == default)
                    _MemoryBuffer = new RRF_MemoryBuffer(this);
                return _MemoryBuffer;
            }
        }

        public Subject<RRF_Request> GCodeRequestSubject { get; }
        public Subject<RRF_Request> ImmediateRequestSubject { get; }
        public Subject<RRF_Request> FastRequestSubject { get; }
        public Subject<RRF_Request> MediumRequestSubject { get; }
        public Subject<RRF_Request> SlowRequestSubject { get; }

        public override string InnerQueuePath => "gcodes/queue/inner";
        public override string StoragePath => "gcodes/storage";
        public override string QueuePath => "gcodes/queue";
        public string GlobalPath => "sys/global";

        // FLUX_FolderType.InnerQueue => "gcodes/queue/inner",
        // FLUX_FolderType.Queue => "gcodes/queue",
        // StoragePath => "gcodes/storage",
        // FLUX_FolderType.System => "sys",
        // FLUX_FolderType.Global => "sys/global",

        private ObservableAsPropertyHelper<Optional<RRF_Response>> _ProcessedRequest;
        public Optional<RRF_Response> ProcessedRequest => _ProcessedRequest.Value;

        public FluxViewModel Flux { get; }
        public RRF_Connection(FluxViewModel flux, RRF_VariableStore variable_store) : base(variable_store)
        {
            Flux = flux;
            Random = new Random();

            ImmediateRequestSubject = new Subject<RRF_Request>().DisposeWith(Disposables);
            MediumRequestSubject = new Subject<RRF_Request>().DisposeWith(Disposables);
            GCodeRequestSubject = new Subject<RRF_Request>().DisposeWith(Disposables);
            FastRequestSubject = new Subject<RRF_Request>().DisposeWith(Disposables);
            SlowRequestSubject = new Subject<RRF_Request>().DisposeWith(Disposables);

            _ProcessedRequest = GCodeRequestSubject
                .WithInterval(TimeSpan.FromSeconds(0.3))
                .MergeWithLowPriorityStream(ImmediateRequestSubject)
                .MergeWithLowPriorityStream(FastRequestSubject)
                .MergeWithLowPriorityStream(MediumRequestSubject)
                .MergeWithLowPriorityStream(SlowRequestSubject)
                .Select(r =>
                {
                    return Observable.FromAsync(async () =>
                    {
                        Optional<RRF_Response> rrf_response = default;
                        try
                        {
                            if (Client.HasValue && !r.CancellationToken.IsCancellationRequested)
                            {
                                var response = await Client.Value.ExecuteAsync(
                                    r.Request,
                                    r.CancellationToken);
                                rrf_response = new RRF_Response(r.Id, response);
                            }
                        }
                        catch (Exception)
                        {
                        }
                        finally
                        {
                            r.Action?.Invoke(rrf_response);
                        }
                        return rrf_response;
                    });
                })
                .Merge(1)
                .ToProperty(this, v => v.ProcessedRequest)
                .DisposeWith(Disposables);
        }

        public override Task<bool> CreateClientAsync(string address)
        {
            Client = new RestClient(address);
            return Task.FromResult(true);
        }

        public async Task<bool> InitializeVariablesAsync(CancellationToken ct)
        {
            var source = VariableStore.Variables.Values
                .Where(v => v is IRRF_VariableBaseGlobalModel)
                .Select(v => (IRRF_VariableBaseGlobalModel)v)
                .Select(v => v.InitializeVariableString);
            return await ExecuteParamacroAsync(source, false, ct);
        }

        public async Task<bool> CreateVariablesAsync(CancellationToken ct)
        {
            var files = await ListFilesAsync(GlobalPath, ct);
            if (!files.HasValue)
                return false;

            foreach (var variable in VariableStore.Variables.Values)
            {
                if (variable is IRRF_VariableBaseGlobalModel global)
                {
                    if (files.Value.Files.Any(f => f.Name == global.CreateVariableName))
                        continue;

                    var source = global.CreateVariableString.ToOptional();
                    if (!await PutFileAsync(GlobalPath, global.CreateVariableName, ct, source))
                        return false;
                }
            }
            return true;
        }

        public async Task<bool> WaitResponseAsync(int id, CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return default;

                return await WaitUtils.WaitForOptionalAsync(
                    this.WhenAnyValue(c => c.ProcessedRequest),
                    is_valid, ct);
            }
            catch
            {
                return default;
            }

            bool is_valid(RRF_Response response)
            {
                if (id != response.Id)
                    return false;
                return true;
            }
        }
        public async Task<Optional<RRF_Response>> WaitResponseValueAsync(int id, CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return default;

                return await WaitUtils.WaitForOptionalAsync(
                    this.WhenAnyValue(c => c.ProcessedRequest),
                    is_valid, r => r, ct);
            }
            catch
            {
                return default;
            }

            bool is_valid(RRF_Response response)
            {
                if (id != response.Id)
                    return false;
                return true;
            }
        }

        // GCODE
        public async Task<bool> PostGCodeAsync(string paramacro, bool wait = false, TimeSpan timeout = default)
        {
            using var cts = new CancellationTokenSource(timeout);
            return await PostGCodeAsync(paramacro, wait, cts.Token);
        }
        public async Task<bool> PostGCodeAsync(string paramacro, bool wait = false, CancellationToken ct = default)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return default;

                if (!Client.HasValue)
                    return false;

                var lenght = GetGCodeLenght(new[] { paramacro });
                if (lenght >= 160)
                    return false;

                if (string.IsNullOrEmpty(paramacro))
                    return true;

                var resource = $"rr_gcode?gcode={paramacro}";
                var request = new RestRequest(resource);
                return await PostGCodeAsync(request, wait, ct);
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> PostGCodeAsync(RestRequest request, bool wait = false, TimeSpan timeout = default)
        {
            using var cts = new CancellationTokenSource(timeout);
            return await PostGCodeAsync(request, wait, cts.Token);
        }
        public async Task<bool> PostGCodeAsync(RestRequest request, bool wait = false, CancellationToken ct = default)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return default;

                var id = Random.Next();
                var wait_response = WaitResponseAsync(id, ct);

                var rrf_request = new RRF_Request(request, id, ct: ct);
                GCodeRequestSubject.OnNext(rrf_request);

                var response = await wait_response;
                if (!response)
                    return false;

                if (wait && ct != CancellationToken.None && !await WaitProcessStatusAsync(
                    status => status == FLUX_ProcessStatus.IDLE,
                    TimeSpan.FromSeconds(0.5),
                    TimeSpan.FromSeconds(0.1),
                    ct))
                    return false;

                return true;
            }
            catch
            {
                return default;
            }
        }

        public Optional<RRF_Request> PostGCode(string paramacro, Action<Optional<RRF_Response>> action = default, CancellationToken ct = default)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return default;

                if (!Client.HasValue)
                    return default;

                var lenght = GetGCodeLenght(new[] { paramacro });
                if (lenght >= 160)
                    return default;

                if (string.IsNullOrEmpty(paramacro))
                    return default;

                var resource = $"rr_gcode?gcode={paramacro}";
                var request = new RestRequest(resource);
                return PostGCode(request, action, ct);
            }
            catch
            {
                return default;
            }
        }
        public Optional<RRF_Request> PostGCode(RestRequest request, Action<Optional<RRF_Response>> action = default, CancellationToken ct = default)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return default;

                var id = Random.Next();

                var rrf_request = new RRF_Request(request, id, action, ct);
                GCodeRequestSubject.OnNext(rrf_request);

                return rrf_request;
            }
            catch
            {
                return default;
            }
        }

        // REQUEST
        public async Task<Optional<RRF_Response>> PostRequestAsync(RestRequest request, IRRF_RequestPriority priority, TimeSpan timeout = default)
        {
            using var cts = new CancellationTokenSource(timeout);
            return await PostRequestAsync(request, priority, cts.Token);
        }
        public async Task<Optional<RRF_Response>> PostRequestAsync(RestRequest request, IRRF_RequestPriority priority, CancellationToken ct = default)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return default;

                var id = Random.Next();
                var wait_response = WaitResponseValueAsync(id, ct);
                var rrf_request = new RRF_Request(request, id, ct: ct);
                switch (priority)
                {
                    case IRRF_RequestPriority.Immediate:
                        ImmediateRequestSubject.OnNext(rrf_request);
                        break;
                    case IRRF_RequestPriority.High:
                        FastRequestSubject.OnNext(rrf_request);
                        break;
                    case IRRF_RequestPriority.Medium:
                        MediumRequestSubject.OnNext(rrf_request);
                        break;
                    case IRRF_RequestPriority.Low:
                        SlowRequestSubject.OnNext(rrf_request);
                        break;
                    default:
                        return default;
                }
                return await wait_response;
            }
            catch
            {
                return default;
            }
        }

        public Optional<RRF_Request> PostRequest(RestRequest request, IRRF_RequestPriority priority, Action<Optional<RRF_Response>> action = default, CancellationToken ct = default)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return default;

                var id = Random.Next();
                var rrf_request = new RRF_Request(request, id, action, ct);
                switch (priority)
                {
                    case IRRF_RequestPriority.Immediate:
                        ImmediateRequestSubject.OnNext(rrf_request);
                        break;
                    case IRRF_RequestPriority.High:
                        FastRequestSubject.OnNext(rrf_request);
                        break;
                    case IRRF_RequestPriority.Medium:
                        MediumRequestSubject.OnNext(rrf_request);
                        break;
                    case IRRF_RequestPriority.Low:
                        SlowRequestSubject.OnNext(rrf_request);
                        break;
                    default:
                        return default;
                }
                return rrf_request;
            }
            catch
            {
                return default;
            }
        }

        public Optional<bool> PutRequest(RestRequest request, IRRF_RequestPriority priority, Action<Optional<RRF_Response>> action = default, CancellationToken ct = default)
        {
            var rrf_request = PostRequest(request, priority, action, ct);
            return rrf_request.HasValue;
        }
        public async Task<bool> PutRequestAsync(RestRequest request, IRRF_RequestPriority priority, TimeSpan timeout = default)
        {
            using var cts = new CancellationTokenSource(timeout);
            return await PutRequestAsync(request, priority, cts.Token);
        }
        public async Task<bool> PutRequestAsync(RestRequest request, IRRF_RequestPriority priority, CancellationToken ct = default)
        {
            var response = await PostRequestAsync(request, priority, ct);
            if (!response.HasValue)
                return false;
            return response.Value.Response.StatusCode == HttpStatusCode.OK;
        }

        public override async Task<bool> ResetAsync()
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            return await ExecuteParamacroAsync(new[] { "M108", "M25", "M0" }, true, cts.Token);
        }
        private int GetGCodeLenght(IEnumerable<string> paramacro)
        {
            return 15 + paramacro.Sum(line => line.Length) + ((paramacro.Count() - 2) * 3);
        }
        public override async Task<bool> CycleAsync(bool start, bool wait, CancellationToken ct = default)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return default;

                if (!await PostGCodeAsync("M24", false, ct))
                    return false;

                if (wait && ct != CancellationToken.None && !await WaitProcessStatusAsync(
                    status => status == FLUX_ProcessStatus.IDLE,
                    TimeSpan.FromSeconds(0.5),
                    TimeSpan.FromSeconds(0.1),
                    ct))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }
        public override async Task<bool> ExecuteParamacroAsync(IEnumerable<string> paramacro, bool wait = false, CancellationToken ct = default)
        {
            var lenght = GetGCodeLenght(paramacro);
            if (lenght < 160)
                return await PostGCodeAsync(string.Join("%0A", paramacro), wait, ct);
            else
                return await base.ExecuteParamacroAsync(paramacro, wait, ct);
        }

        public override async Task<bool> DeselectPartProgramAsync(bool from_drive, bool wait, CancellationToken ct = default)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return false;

                var file_list_ctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var files = await ListFilesAsync(StoragePath, file_list_ctk.Token);
                if (!files.HasValue)
                    return false;

                var put_file_ctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                if (!files.Value.Files.Any(f => f.Name == "deselected.mcode"))
                    await PutFileAsync(StoragePath, "deselected.mcode", put_file_ctk.Token);

                if (!await SelectPartProgramAsync("deselected.mcode", true, wait, ct))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }
        public override async Task<bool> DeleteFileAsync(string folder, string filename, bool wait, CancellationToken ct = default)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return false;

                if (!Client.HasValue)
                    return false;

                var path = $"{folder}/{filename}".TrimStart('/');
                var clear_folder_ctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                if (!await ClearFolderAsync(path, true, clear_folder_ctk.Token))
                    return false;

                var request = new RestRequest($"rr_delete?name=0:/{path}");
                var response = await Client.Value.ExecuteAsync(request, ct);
                return response.ResponseStatus == ResponseStatus.Completed;
            }
            catch
            {
                return false;
            }
        }
        public override async Task<bool> HoldAsync()
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            return await ExecuteParamacroAsync(new[] { "M108", "M25", "M0" }, true, cts.Token);
        }
        public override async Task<Optional<string>> DownloadFileAsync(string folder, string filename, CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return default;

                if (!Client.HasValue)
                    return default;

                var request = new RestRequest($"rr_download?name=0:/{folder}/{filename}");
                var response = await Client.Value.ExecuteAsync(request, ct);
                if (response.ResponseStatus == ResponseStatus.Completed)
                    return response.Content;
                return default;
            }
            catch
            {
                return default;
            }
        }
        public override async Task<bool> SelectPartProgramAsync(string filename, bool from_drive, bool wait, CancellationToken ct = default)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return false;

                if (!await PostGCodeAsync($"M23 storage/{filename}", false, ct))
                    return false;

                var selected_pp = Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(5))
                    .Select(_ => Observable.FromAsync(() => MemoryBuffer.GetModelDataAsync<RRF_ObjectModelJob>("job", IRRF_RequestPriority.Immediate, ct)))
                    .Merge(1)
                    .Convert(j => j.File.HasValue ? (j.File.Value.FileName.HasValue ? j.File.Value.FileName : j.LastFileName) : j.LastFileName);

                return await WaitUtils.WaitForAsync(selected_pp,
                    pp => pp.ConvertOr(p => Path.GetFileName(p) == filename, () => false),
                    ct);
            }
            catch
            {
                return false;
            }
        }
        public override async Task<Optional<FLUX_FileList>> ListFilesAsync(string folder, CancellationToken ct)
        {
            try
            {
                var response = await PostRequestAsync(
                    new RestRequest($"rr_filelist?dir={folder}", Method.Get),
                    IRRF_RequestPriority.Immediate,
                    ct);
                return response.Convert(r => r.GetFileSystem());
            }
            catch
            {
                return default;
            }
        }

        public override async Task<bool> PutFileAsync(
            string folder,
            string filename,
            CancellationToken ct,
            Optional<IEnumerable<string>> source = default,
            Optional<IEnumerable<string>> start = default,
            Optional<IEnumerable<string>> recovery = default,
            Optional<IEnumerable<string>> end = default,
            Optional<uint> recovery_block = default,
            Optional<uint> source_blocks = default,
            Action<double> report_progress = null,
            bool debug_chunks = false)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return default;

                if (!Client.HasValue)
                    return false;

                long actual_start_block = 0;
                if (recovery_block.HasValue)
                    actual_start_block = recovery_block.Value;

                long actual_source_blocks = 0;
                if (source_blocks.HasValue)
                    actual_source_blocks = source_blocks.Value;
                actual_source_blocks -= actual_start_block;

                // Write content
                using var lines_stream = new GCodeStream(get_full_source());
                using var content_stream = new StreamContent(lines_stream);
                content_stream.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                content_stream.Headers.ContentLength = lines_stream.Length;

                var plc_address = Flux.SettingsProvider.CoreSettings.Local.PLCAddress;

                using var client = new HttpClient();
                client.BaseAddress = new Uri($"http://{plc_address}");
                client.DefaultRequestHeaders.Add("Connection", "Keep-Alive");
                var response = await client.PostAsync($"rr_upload?name=0:/{folder}/{filename}&time={DateTime.Now:s}", content_stream, ct);

                return response.StatusCode == HttpStatusCode.OK;

                IEnumerable<string> get_full_source()
                {
                    if (start.HasValue)
                    {
                        yield return "; preprocessing";
                        foreach (var line in start.Value)
                            yield return line;
                        yield return "";
                    }

                    if (recovery.HasValue)
                    {
                        yield return "; recovery";
                        foreach (var line in recovery.Value)
                            yield return line;
                        yield return "";
                    }

                    long current_block = 0;
                    if (source.HasValue)
                    {
                        foreach (var line in source.Value)
                        {
                            if (current_block++ < actual_start_block)
                                continue;
                            if (actual_source_blocks > 0)
                            {
                                var progress = (double)current_block / actual_source_blocks * 100;
                                report_progress(Math.Ceiling(progress));
                            }
                            yield return line;
                        }
                    }

                    if (end.HasValue)
                    {
                        yield return "; end";
                        foreach (var line in end.Value)
                            yield return line;
                        yield return "";
                    }
                }
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
                return false;
            }
        }
        public override async Task<bool> ClearFolderAsync(string folder, bool wait, CancellationToken ct = default)
        {
            try
            {
                return await clear_f_async(folder);
            }
            catch
            {
                return false;
            }

            async Task<bool> clear_f_async(string folder)
            {
                try
                {
                    if (ct.IsCancellationRequested)
                        return false;

                    if (!Client.HasValue)
                        return false;

                    var list_res = await Client.Value.ExecuteGetAsync(new RestRequest($"/rr_filelist?dir=0:/{folder}"));
                    var file_list = JsonUtils.Deserialize<FLUX_FileList>(list_res.Content);
                    if (!file_list.HasValue)
                        return false;

                    foreach (var file in file_list.Value.Files)
                    {
                        var filename = string.Join("/", folder, file.Name);
                        switch (file.Type)
                        {
                            case FLUX_FileType.Directory:
                                if (!await delete_dir_async(filename))
                                    return false;
                                break;
                            case FLUX_FileType.File:
                                if (Path.GetFileName(filename) == "deselected.gcode")
                                    continue;
                                var del_f_req = new RestRequest($"rr_delete?name={filename}");
                                var del_f_res = await Client.Value.ExecuteGetAsync(del_f_req, ct);
                                if (del_f_res.ResponseStatus != ResponseStatus.Completed)
                                    return false;
                                break;
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Flux.Messages.LogException(this, ex);
                    return false;
                }
            }
            async Task<bool> delete_dir_async(string directory)
            {
                try
                {
                    if (ct.IsCancellationRequested)
                        return false;

                    if (!Client.HasValue)
                        return false;

                    await clear_f_async(directory);
                    var del_d_req = new RestRequest($"rr_delete?name={directory}");
                    var del_d_res = await Client.Value.ExecuteGetAsync(del_d_req, ct);
                    return del_d_res.ResponseStatus == ResponseStatus.Completed;
                }
                catch (Exception ex)
                {
                    Flux.Messages.LogException(this, ex);
                    return false;
                }
            }
        }
        public override string[] GetHomingGCode()
        {
            return new[] { "G28" };
        }
        public override string[] GetParkToolGCode()
        {
            return new[] { $"T-1" };
        }
        public override string[] GetProbePlateGCode()
        {
            throw new NotImplementedException();
        }
        public override string[] GetLowerPlateGCode()
        {
            return new[] { "M98 P\"/macros/Lower Plate\"" };
        }
        public override string[] GetRaisePlateGCode()
        {
            return new[] { "M98 P\"/macros/Raise Plate\"" };
        }
        public override string[] GetResetPrinterGCode()
        {
            // TODO
            return new[]
            {
                "M98 P\"/macros/Lower Plate\"",
                "M98 P\"/sys/global/write_queue_pos.g\" S-1",
                "M98 P\"/sys/global/write_hold_pp.g\" S\"\"",
                "M98 P\"/sys/global/write_hold_tool.g\" S-1",
                "M98 P\"/sys/global/write_hold_blk_num.g\" S0",
                "M98 P\"/sys/global/write_hold_temp.g\" T0 S0",
                "M98 P\"/sys/global/write_hold_temp.g\" T1 S0",
                "M98 P\"/sys/global/write_hold_temp.g\" T2 S0",
                "M98 P\"/sys/global/write_hold_temp.g\" T3 S0",
            };
        }
        public override string[] GetSelectToolGCode(ushort position)
        {
            return new[] { $"T{position}" };
        }
        public override string[] GetGotoReaderGCode(ushort position)
        {
            throw new NotImplementedException();
        }
        public override string[] GetStartPartProgramGCode(string file_name)
        {
            return new[] { $"M32 storage/{file_name}" };
        }
        public override string[] GetGotoPurgePositionGCode(ushort position)
        {
            throw new NotImplementedException();
        }
        public override string[] GetSetToolTemperatureGCode(ushort position, double temperature)
        {
            return new[] { $"M104 T{position} S{temperature}" };
        }
        public override string[] GetPurgeToolGCode(ushort position, Nozzle nozzle, double temperature)
        {
            var gcode = nozzle.GetPurgeFilamentGCode(position, temperature);
            if (gcode.HasValue)
                return new[] { $"T{position}", gcode.Value, "T-1" };
            return new string[0];
        }
        public override string[] GetProbeToolGCode(ushort position, Nozzle nozzle, double temperature)
        {
            throw new NotImplementedException();
        }
        public override string[] GetLoadFilamentGCode(ushort position, Nozzle nozzle, double temperature)
        {
            var gcode = nozzle.GetLoadFilamentGCode(position, temperature);
            if (gcode.HasValue)
                return new[] { $"T{position}", gcode.Value, "T-1" };
            return new string[0];
        }
        public override string[] GetUnloadFilamentGCode(ushort position, Nozzle nozzle, double temperature)
        {
            var gcode = nozzle.GetUnloadFilamentGCode(position, temperature);
            if (gcode.HasValue)
                return new[] { $"T{position}", gcode.Value, "T-1" };
            return new string[0];
        }
        public override string[] GetRelativeXMovementGCode(double distance, double feedrate) => new string[] { "M120", "G91", $"G1 X {distance} F{feedrate}".Replace(",", "."), "G90", "M121" };
        public override string[] GetRelativeYMovementGCode(double distance, double feedrate) => new string[] { "M120", "G91", $"G1 Y {distance} F{feedrate}".Replace(",", "."), "G90", "M121" };
        public override string[] GetRelativeZMovementGCode(double distance, double feedrate) => new string[] { "M120", "G91", $"G1 Z {distance} F{feedrate}".Replace(",", "."), "G90", "M121" };
        public override string[] GetRelativeEMovementGCode(double distance, double feedrate) => new string[] { "M120", "G91", $"G1 A {distance} F{feedrate}".Replace(",", "."), "G90", "M121" };

        public override async Task<bool> CreateFolderAsync(string folder, string name, CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return false;

                if (!Client.HasValue)
                    return false;

                var path = $"{folder}/{name}".TrimStart('/');
                var request = new RestRequest($"rr_mkdir?dir=0:/{path}");
                var response = await Client.Value.ExecuteAsync(request, ct);
                return response.ResponseStatus == ResponseStatus.Completed;
            }
            catch
            {
                return false;
            }
        }
    }
}
