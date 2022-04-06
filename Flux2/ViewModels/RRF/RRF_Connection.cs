using DynamicData;
using DynamicData.Kernel;
using GreenSuperGreen.Queues;
using Modulo3DStandard;
using ReactiveUI;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Flux.ViewModels
{
    public enum RRF_RequestPriority : ushort
    {
        Immediate = 3,
        High = 2,
        Medium = 1,
        Low = 0
    }

    public struct RRF_Request
    {
        public RestRequest Request { get; }
        public RRF_RequestPriority Priority { get; }
        public CancellationToken CancellationToken { get; }
        public TaskCompletionSource<RRF_Response> Response { get; }
        public RRF_Request(string request, Method method, RRF_RequestPriority priority, CancellationToken ct = default)
        {
            Priority = priority;
            CancellationToken = ct;
            Request = new RestRequest(request, method);
            Response = new TaskCompletionSource<RRF_Response>();
        }
        public override string ToString() => $"{nameof(RRF_Request)} Method:{Request.Method} Resource:{Request.Resource}?{string.Join("&", Request.Parameters.Select(p => $"{p.Name}={p.Value}"))}";
    }

    public struct RRF_Response
    {
        public RestResponse Response { get; }
        public bool Ok => Response.StatusCode == HttpStatusCode.OK;
        public RRF_Response(RestResponse response)
        {
            Response = response;
        }

        public Optional<T> GetContent<T>()
        {
            if (!Ok)
                return default;
            return JsonUtils.Deserialize<T>(Response.Content);
        }

        public override string ToString() => $"{nameof(RRF_Response)} Status:{Enum.GetName(typeof(HttpStatusCode), Response.StatusCode)}, Error: {Response.ErrorMessage}";
    }

    public class RRF_Client : IDisposable
    {
        private RestClient Client { get; }
        private CompositeDisposable Disposables { get; }
        private PriorityQueueNotifierUC<RRF_RequestPriority, RRF_Request> Requests { get; }
        public RRF_Client(string address)
        {
            Client = new RestClient(address);
            Disposables = new CompositeDisposable();
            var values = ((RRF_RequestPriority[])Enum.GetValues(typeof(RRF_RequestPriority)))
                .OrderByDescending(e => (ushort)e);
            Requests = new PriorityQueueNotifierUC<RRF_RequestPriority, RRF_Request>(values);

            new DisposableThread(async () =>
                {
                    await Requests.EnqueuedItemsAsync();
                    while (Requests.TryDequeu(out var rrf_request))
                    {
                        var response = await Client.ExecuteAsync(rrf_request.Request, rrf_request.CancellationToken);
                        var rrf_response = new RRF_Response(response);
                        rrf_request.Response.SetResult(rrf_response);
                    }
                }, TimeSpan.Zero)
                .DisposeWith(Disposables);
        }

        public async Task<RRF_Response> ExecuteAsync(RRF_Request rrf_request)
        {
            Requests.Enqueue(rrf_request.Priority, rrf_request);
            return await rrf_request.Response.Task;
        }

        public void Dispose()
        {
            Disposables.Dispose();
        }
    }

    public class RRF_Connection : FLUX_Connection<RRF_VariableStore, RRF_Client, RRF_MemoryBuffer>
    {
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

        public override string InnerQueuePath => "gcodes/queue/inner";
        public override string StoragePath => "gcodes/storage";
        public override string QueuePath => "gcodes/queue";
        public string GlobalPath => "sys/global";

        // FLUX_FolderType.InnerQueue => "gcodes/queue/inner",
        // FLUX_FolderType.Queue => "gcodes/queue",
        // StoragePath => "gcodes/storage",
        // FLUX_FolderType.System => "sys",
        // FLUX_FolderType.Global => "sys/global",

        public FluxViewModel Flux { get; }

        public RRF_Connection(FluxViewModel flux, RRF_VariableStore variable_store, string address) : base(variable_store, new RRF_Client(address))
        {
            Flux = flux;
            Client.DisposeWith(Disposables);
        }

        public async Task<bool> InitializeVariablesAsync(CancellationToken ct)
        {
            var files = await ListFilesAsync(GlobalPath, ct);
            if (!files.HasValue)
                return false;

            var file_set = files.Value.Files.Select(f => f.Name).ToHashSet();
            if (!file_set.Contains("initialize_variables.g"))
            {
                var variables = VariableStore.Variables.Values
                   .SelectMany(v => v switch
                   {
                       IFLUX_Array array => array.Variables.Items,
                       IFLUX_Variable variable => new[] { variable },
                       _ => throw new NotImplementedException()
                   })
                   .Where(v => v is IRRF_VariableGlobalModel global)
                   .Select(v => (IRRF_VariableGlobalModel)v);

                var source = variables
                    .Select(v => v.LoadVariableMacro)
                    .Select(m => $"M98 P\"/sys/global/{m}\"")
                    .ToOptional();

                if (!await PutFileAsync(c => ((RRF_Connection)c).GlobalPath, "initialize_variables.g", ct, source))
                    return false;
            }

            return await PostGCodeAsync("M98 P\"/sys/global/initialize_variables.g\"", ct);
        }

        public async Task<bool> CreateVariablesAsync(CancellationToken ct)
        {
            var files = await ListFilesAsync(GlobalPath, ct);
            if (!files.HasValue)
                return false;

            var variables = VariableStore.Variables.Values
                .SelectMany(v => v switch
                {
                    IFLUX_Array array => array.Variables.Items,
                    IFLUX_Variable variable => new[] { variable },
                    _ => throw new NotImplementedException()
                })
                .Where(v => v is IRRF_VariableGlobalModel global)
                .Select(v => (IRRF_VariableGlobalModel)v);

            var file_set = files.Value.Files.Select(f => f.Name).ToHashSet();
            var files_to_create = variables
                .Where(v => !file_set.Contains(v.LoadVariableMacro))
                .Select(v => v);
            
            if (files_to_create.Any())
            {
                var advanced_mode = Flux.MCodes.OperatorUSB
                    .ConvertOr(usb => usb.AdvancedSettings, () => false);
                if (!advanced_mode)
                    return false;
                
                var files_str = string.Join(Environment.NewLine, files_to_create);
                var result = await Flux.ShowConfirmDialogAsync("Creare file di variabile?", files_str);

                switch(result)
                {
                    case ContentDialogResult.Primary:
                        foreach (var variable in variables)
                            if (!file_set.Contains(variable.LoadVariableMacro))
                                if (!await variable.InitializeVariableAsync())
                                    return false;
                        break;
                    default:
                        return false;
                }    
            }
            
            return true;
        }

        // GCODE
        public async Task<bool> PostGCodeAsync(string gcode, CancellationToken ct, bool wait = false, CancellationToken gcode_ct = default)
        {
            try
            {
                var resource = $"rr_gcode?gcode={gcode}";
                if (resource.Length >= 160)
                {
                    Flux.Messages.LogMessage("Errore esecuzione gcode", $"Lunghezza gcode oltre i limiti", MessageLevel.EMERG, 0);
                    return false;
                }

                var rrf_request = new RRF_Request(resource, Method.Get, RRF_RequestPriority.Immediate, ct);
                var rrf_response = await Client.ExecuteAsync(rrf_request);
                if (!rrf_response.Ok)
                {
                    Flux.Messages.LogMessage($"{rrf_request}", $"{rrf_response}", MessageLevel.ERROR, 0);
                    return false;
                }

                if (wait && gcode_ct != CancellationToken.None && !await WaitProcessStatusAsync(
                    status => status == FLUX_ProcessStatus.IDLE,
                    TimeSpan.FromSeconds(0.5),
                    TimeSpan.FromSeconds(0.1),
                    gcode_ct))
                {
                    Flux.Messages.LogMessage($"{rrf_request}", $"Timeout esecuzione gcode", MessageLevel.ERROR, 0);
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
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
            return await ExecuteParamacroAsync(new[] { "M24" }, wait, ct);
        }
        public override async Task<bool> ExecuteParamacroAsync(IEnumerable<string> paramacro, bool wait = false, CancellationToken ct = default)
        {
            paramacro = paramacro.Select(line => Regex.Replace(line, "M98 P\"(.*)\"", m => $"M98 P\"{m.Groups[1].Value.ToLower().Replace(" ", "_")}\""));
            var lenght = GetGCodeLenght(paramacro);
            if (lenght < 160)
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                return await PostGCodeAsync(string.Join("%0A", paramacro), cts.Token, wait, ct);
            }
            else
            { 
                return await base.ExecuteParamacroAsync(paramacro, wait, ct);
            }
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

                var path = $"{folder}/{filename}".TrimStart('/');
                var clear_folder_ctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                if (!await ClearFolderAsync(path, true, clear_folder_ctk.Token))
                    return false;

                var request = new RRF_Request($"rr_delete?name=0:/{path}", Method.Get, RRF_RequestPriority.Immediate, ct);
                var response = await Client.ExecuteAsync(request);

                if (wait)
                {
                    var file_system = Observable.Interval(TimeSpan.FromSeconds(0.1))
                        .Select(_ => Observable.FromAsync(() => ListFilesAsync(folder, ct)))
                        .Merge(1);

                    return await WaitUtils.WaitForOptionalAsync(
                        file_system,
                        f => !f.Files.Any(f => f.Name == filename),
                        ct);
                }

                return response.Ok;
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

                var request = new RRF_Request($"rr_download?name=0:/{folder}/{filename}", Method.Get, RRF_RequestPriority.Immediate, ct);
                var response = await Client.ExecuteAsync(request);
                return response.Response.Content.ToOptional();
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

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                if (!await PostGCodeAsync($"M23 storage/{filename}", cts.Token))
                {
                    Flux.Messages.LogMessage("Errore selezione partprogram", "Impossibile eseguire il gcode", MessageLevel.ERROR, 0);
                    return false;
                }

                var selected_pp = MemoryBuffer.RRFObjectModel
                    .WhenAnyValue(o => o.Job)
                    .Convert(j => 
                    {
                        return j.File
                            .Convert(f => f.FileName)
                            .ValueOrOptional(() => j.LastFileName);
                    })
                    .ConvertOr(f => Path.GetFileName(f) == filename, () => false);

                if (!await WaitUtils.WaitForAsync(selected_pp, ct))
                {
                    Flux.Messages.LogMessage("Errore selezione partprogram", "Timeout di selezione", MessageLevel.ERROR, 0);
                    return false;
                }

                return true;
            }
            catch(Exception ex)
            {
                Flux.Messages.LogException(this, ex);
                return false;
            }
        }
        public override async Task<Optional<FLUX_FileList>> ListFilesAsync(string folder, CancellationToken ct)
        {
            try
            {
                var request = new RRF_Request($"rr_filelist?dir={folder}", Method.Get, RRF_RequestPriority.Immediate, ct);
                var response = await Client.ExecuteAsync(request);
                return response.GetContent<FLUX_FileList>();
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
            Optional<IEnumerable<string>> end = default,
            Optional<uint> source_blocks = default,
            Action<double> report_progress = null)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return default;

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

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    Flux.Messages.LogMessage("Errore durante l'upload del file", $"Stato: {Enum.GetName(typeof(HttpStatusCode), response.StatusCode)}", MessageLevel.ERROR, 0);
                    return false;
                }

                return true;

                IEnumerable<string> get_full_source()
                {
                    long current_block = 0;
                    if (source.HasValue)
                    {
                        if (source_blocks.HasValue && source_blocks.Value > 0)
                        {
                            foreach (var line in source.Value)
                            {
                                var progress = (double)current_block++ / source_blocks.Value * 100;
                                if (progress - (int)progress < 0.001)
                                    report_progress((int)progress);
                                yield return line;
                            }
                        }
                        else
                        {
                            foreach (var line in source.Value)
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
            catch(Exception ex)
            {
                Flux.Messages.LogException(this, ex);
                return false;
            }

            async Task<bool> clear_f_async(string folder)
            {
                try
                {
                    if (ct.IsCancellationRequested)
                    {
                        Flux.Messages.LogMessage("Errore durante la pulizia della cartella", "Cancellazione token richiesta", MessageLevel.ERROR, 0);
                        return false;
                    }

                    var list_req = new RRF_Request($"/rr_filelist?dir=0:/{folder}", Method.Get, RRF_RequestPriority.Immediate, ct);
                    var list_res = await Client.ExecuteAsync(list_req);
                    var file_list = list_res.GetContent<FLUX_FileList>();
                    if (!file_list.HasValue)
                    {
                        Flux.Messages.LogMessage("Errore durante la pulizia della cartella", "Lista dei file non trovata", MessageLevel.ERROR, 0);
                        return false;
                    }

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
                                var del_f_req = new RRF_Request($"rr_delete?name={filename}", Method.Get, RRF_RequestPriority.Immediate, ct);
                                var del_f_res = await Client.ExecuteAsync(del_f_req);
                                if (!del_f_res.Ok)
                                {
                                    Flux.Messages.LogMessage("Errore durante la pulizia della cartella", $"Impossibile cancellare il file: {del_f_res.Response.StatusCode}", MessageLevel.ERROR, 0);
                                    return false;
                                }
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

                    await clear_f_async(directory);
                    var del_d_req = new RRF_Request($"rr_delete?name={directory}", Method.Get, RRF_RequestPriority.Immediate, ct);
                    var del_d_res = await Client.ExecuteAsync(del_d_req);
                    if (!del_d_res.Ok)
                    {
                        Flux.Messages.LogMessage("Errore durante la pulizia della cartella", $"Impossibile cancellare la cartella: {del_d_res.Response.StatusCode}", MessageLevel.ERROR, 0);
                        return false;
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Flux.Messages.LogException(this, ex);
                    return false;
                }
            }
        }
        public override Optional<IEnumerable<string>> GetHomingGCode()
        {
            return new[] { "G28" };
        }
        public override Optional<IEnumerable<string>> GetParkToolGCode()
        {
            return new[] { $"T-1" };
        }
        public override Optional<IEnumerable<string>> GetProbePlateGCode()
        {
            throw new NotImplementedException();
        }
        public override Optional<IEnumerable<string>> GetLowerPlateGCode()
        {
            return new[] { "M98 P\"/macros/lower_plate\"" };
        }
        public override Optional<IEnumerable<string>> GetRaisePlateGCode()
        {
            return new[] { "M98 P\"/macros/raise_plate\"" };
        }
        public override Optional<IEnumerable<string>> GetSelectToolGCode(ushort position)
        {
            return new[] { $"T{position}" };
        }
        public override Optional<IEnumerable<string>> GetGotoReaderGCode(ushort position)
        {
            throw new NotImplementedException();
        }
        public override Optional<IEnumerable<string>> GetStartPartProgramGCode(string file_name)
        {
            return new[] { $"M32 storage/{file_name}" };
        }
        public override Optional<IEnumerable<string>> GetGotoPurgePositionGCode(ushort position)
        {
            throw new NotImplementedException();
        }
        public override Optional<IEnumerable<string>> GetSetToolTemperatureGCode(ushort position, double temperature)
        {
            return new[] { $"M104 T{position} S{temperature}" };
        }
        public override Optional<IEnumerable<string>> GetPurgeToolGCode(ushort position, Nozzle nozzle, double temperature)
        {
            var gcode = nozzle.GetPurgeFilamentGCode(temperature);
            if (!gcode.HasValue)
                return default;
            return load_filament().ToOptional();
            IEnumerable<string> load_filament()
            {
                yield return $"T{position}";
                foreach (var line in gcode.Value)
                    yield return line;
                yield return "T-1";
            }
        }
        public override Optional<IEnumerable<string>> GetProbeToolGCode(ushort position, Nozzle nozzle, double temperature)
        {
            throw new NotImplementedException();
        }
        public override Optional<IEnumerable<string>> GetLoadFilamentGCode(ushort position, Nozzle nozzle, double temperature)
        {
            var gcode = nozzle.GetLoadFilamentGCode(temperature);
            if (!gcode.HasValue)
                return default;
            return load_filament().ToOptional();
            IEnumerable<string> load_filament()
            {
                yield return $"T{position}";
                foreach (var line in gcode.Value)
                    yield return line;
                yield return "T-1";
            }
        }
        public override Optional<IEnumerable<string>> GetUnloadFilamentGCode(ushort position, Nozzle nozzle, double temperature)
        {
            var gcode = nozzle.GetUnloadFilamentGCode(temperature);
            if (!gcode.HasValue)
                return default;
            return load_filament().ToOptional();
            IEnumerable<string> load_filament()
            {
                yield return $"T{position}";
                foreach (var line in gcode.Value)
                    yield return line;
                yield return "T-1";
            }
        }
        public override Optional<IEnumerable<string>> GetRelativeXMovementGCode(double distance, double feedrate) => new string[] { "M120", "G91", $"G1 X{distance} F{feedrate}".Replace(",", "."), "G90", "M121" };
        public override Optional<IEnumerable<string>> GetRelativeYMovementGCode(double distance, double feedrate) => new string[] { "M120", "G91", $"G1 Y{distance} F{feedrate}".Replace(",", "."), "G90", "M121" };
        public override Optional<IEnumerable<string>> GetRelativeZMovementGCode(double distance, double feedrate) => new string[] { "M120", "G91", $"G1 Z{distance} F{feedrate}".Replace(",", "."), "G90", "M121" };
        public override Optional<IEnumerable<string>> GetRelativeEMovementGCode(double distance, double feedrate) => new string[] { "M120", "G91", $"G1 E{distance} F{feedrate}".Replace(",", "."), "G90", "M121" };

        public override async Task<bool> CreateFolderAsync(string folder, string name, CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return false;

                var path = $"{folder}/{name}".TrimStart('/');
                var request = new RRF_Request($"rr_mkdir?dir=0:/{path}", Method.Get, RRF_RequestPriority.Immediate, ct);
                var response = await Client.ExecuteAsync(request);
                return response.Ok;
            }
            catch
            {
                return false;
            }
        }

        public override Optional<IEnumerable<string>> GetSetToolOffsetGCode(ushort position, double x, double y, double z)
        {
            return new[]
            {
                $"G10 P{position} X{{{x * -1}}}",
                $"G10 P{position} Y{{{y * -1}}}",
                $"G10 P{position} Z{{{z * -1}}}",
            };
        }

        public override async Task<bool> CancelPrintAsync(bool hard_cancel)
        {
            // TODO
            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            return await ExecuteParamacroAsync(new []
            {
                "M108", "M25", "M0",
                "M98 P\"/macros/cancel_print\"",
            }, true, cts.Token);
        }

        // test
        public override async Task<bool> RenameFileAsync(string folder, string old_filename, string new_filename, bool wait, CancellationToken ct = default)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return false;

                var old_path = $"{folder}/{old_filename}".TrimStart('/');
                var new_path = $"{folder}/{new_filename}".TrimStart('/');

                var request = new RRF_Request($"rr_move?old=0:/{old_path}&new=0:/{new_path}", Method.Get, RRF_RequestPriority.Immediate, ct);
                var response = await Client.ExecuteAsync(request);

                if (wait)
                {
                    var file_system = Observable.Interval(TimeSpan.FromSeconds(0.1))
                        .Select(_ => Observable.FromAsync(() => ListFilesAsync(folder, ct)))
                        .Merge(1);

                    return await WaitUtils.WaitForOptionalAsync(
                        file_system, 
                        f => f.Files.Any(f => f.Name == new_filename),
                        ct);
                }

                return response.Ok;
            }
            catch
            {
                return false;
            }
        }
    }
}
