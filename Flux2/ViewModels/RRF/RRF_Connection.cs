using DynamicData;
using DynamicData.Kernel;
using GreenSuperGreen.Queues;
using Modulo3DStandard;
using ReactiveUI;
using RestSharp;
using System;
using System.Collections.Concurrent;
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
using System.Security.Cryptography;
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
        public TimeSpan Timeout { get; }
        public HttpRequestMessage Request { get; }
        public RRF_RequestPriority Priority { get; }
        public CancellationToken Cancellation { get; }
        public TaskCompletionSource<RRF_Response> Response { get; }
        public RRF_Request(string request, HttpMethod httpMethod, RRF_RequestPriority priority, CancellationToken ct, TimeSpan timeout = default)
        {
            Timeout = timeout;
            Cancellation = ct;
            Priority = priority;
            Request = new HttpRequestMessage(httpMethod, request);
            Response = new TaskCompletionSource<RRF_Response>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        public override string ToString() => $"{nameof(RRF_Request)} Method:{Request.Method} Resource:{Request.RequestUri}";
    }

    public struct RRF_Response
    {
        public Optional<string> Content { get; }
        public HttpStatusCode StatusCode { get; }
        public bool Ok
        {
            get
            {
                if (!Content.HasValue)
                    return false;
                return StatusCode == HttpStatusCode.OK;
            }
        }
        public RRF_Response(HttpStatusCode statusCode, string content)
        {
            StatusCode = statusCode;
            Content = content;
        }

        public Optional<T> GetContent<T>()
        {
            if (!Content.HasValue)
                return default;
            if (StatusCode != HttpStatusCode.OK)
                return default;
            return JsonUtils.Deserialize<T>(Content.Value);
        }

        public override string ToString()
        {
            if (!Content.HasValue)
                return $"{nameof(RRF_Response)} Task cancellata";
            if (StatusCode == HttpStatusCode.OK)
                return $"{nameof(RRF_Response)} OK";
            return $"{nameof(RRF_Response)} Status:{Enum.GetName(typeof(HttpStatusCode), StatusCode)}";
        }
    }

    public class RRF_Connection : FLUX_Connection<RRF_ConnectionProvider, RRF_VariableStoreBase, HttpClient>
    {
        public override string CombinePaths(params string[] paths) => string.Join("/", paths);
        public string SystemPath => "sys";
        public override string RootPath => "";
        public override string MacroPath => "macros";
        public override string QueuePath => "gcodes/queue";
        public override string EventPath => "gcodes/events";
        public override string StoragePath => "gcodes/storage";
        public string GlobalPath => CombinePaths(SystemPath, "global");
        public override string JobEventPath => CombinePaths(EventPath, "job");
        public override string InnerQueuePath => CombinePaths(QueuePath, "inner");
        public override string ExtrusionEventPath => CombinePaths(EventPath, "extr");

        public FluxViewModel Flux { get; }
        private PriorityQueueNotifierUC<RRF_RequestPriority, RRF_Request> Requests { get; }

        public RRF_Connection(FluxViewModel flux, RRF_ConnectionProvider connection_provider) : base(connection_provider)
        {
            Flux = flux;

            var values = ((RRF_RequestPriority[])Enum.GetValues(typeof(RRF_RequestPriority)))
                .OrderByDescending(e => (ushort)e);
            Requests = new PriorityQueueNotifierUC<RRF_RequestPriority, RRF_Request>(values);

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

                    var response        = await Client.Value.SendAsync(rrf_request.Request, request_cts.Token);
                    var content         = await response.Content.ReadAsStringAsync(request_cts.Token);
                    var rrf_response    = new RRF_Response(response.StatusCode, content);

                    rrf_request.Response.TrySetResult(rrf_response);
                }
                catch
                {
                    rrf_request.Response.TrySetResult(default); 
                }
            }
        }
        public async Task<RRF_Response> ExecuteAsync(RRF_Request rrf_request)
        {
            using (rrf_request.Cancellation.Register(() => rrf_request.Response.TrySetResult(default)))
            {
                if (!Client.HasValue)
                    return default;
                Requests.Enqueue(rrf_request.Priority, rrf_request);
                return await rrf_request.Response.Task;
            }
        }

        public override Task<bool> CloseAsync()
        {
            try
            {
                if (!Client.HasValue)
                    return Task.FromResult(true);
                Client = null;

                while (Requests.TryDequeu(out var rrf_request))
                    rrf_request.Response.TrySetResult(default);

                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
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

                Client = new HttpClient() { BaseAddress = new Uri(plc_address.Value) };

                //Client = new RestClient(plc_address.Value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> InitializeVariablesAsync(CancellationToken ct)
        {
            var missing_variables = await FindMissingVariables(ct);
            if (!missing_variables.result)
                return false;

            if (missing_variables.variables.Count != 0)
            {
                var advanced_mode = Flux.MCodes.OperatorUSB
                    .ConvertOr(usb => usb.AdvancedSettings, () => false);
                if (!advanced_mode)
                    return false;

                var files_str = string.Join(Environment.NewLine, missing_variables.variables.Select(v => v.LoadVariableMacro));
                var create_variables_result = await Flux.ShowConfirmDialogAsync("Creare file di variabile?", files_str);

                if (create_variables_result != ContentDialogResult.Primary)
                    return false;

                await ConnectionProvider.DeleteAsync(c => ((RRF_Connection)c).GlobalPath, "initialize_variables.g", false, ct);

                foreach (var variable in missing_variables.variables)
                    if (!await variable.CreateVariableAsync(ct))
                        return false;
            }

            var written_file = await GetFileAsync(GlobalPath, "initialize_variables.g", ct);
            var variables = VariableStore.Variables.Values
               .SelectMany(v => v switch
               {
                   IFLUX_Array array => array.Variables.Items,
                   IFLUX_Variable variable => new[] { variable },
                   _ => throw new NotImplementedException()
               })
               .Where(v => v is IRRF_VariableGlobalModel global)
               .Select(v => (IRRF_VariableGlobalModel)v);

            var source_variables = new GCodeString(variables
                .Select(v => GetExecuteMacroGCode(GlobalPath, v.LoadVariableMacro)));

            using var sha256 = SHA256.Create();
            var source_hash = sha256.ComputeHash(string.Join("", source_variables)).ToHex();

            var written_hash = written_file.Convert(w => sha256.ComputeHash(string.Join("", w.SplitLines())).ToHex());

            if (!written_hash.HasValue || written_hash != source_hash)
                if (!await PutFileAsync(GlobalPath, "initialize_variables.g", true, ct, source_variables))
                    return false;

            return await PostGCodeAsync(GetExecuteMacroGCode(GlobalPath, "initialize_variables.g"), ct);
        }
        private async Task<(bool result, List<IRRF_VariableGlobalModel> variables)> FindMissingVariables(CancellationToken ct)
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

            var files = await ListFilesAsync(GlobalPath, ct);
            if (!files.HasValue)
                return (false, default);

            var file_set = files.Value.Files
                .Select(f => f.Name)
                .ToHashSet();

            var missing_variables = VariableStore.Variables.Values
                .SelectMany(v => v switch
                {
                    IFLUX_Array array => array.Variables.Items,
                    IFLUX_Variable variable => new[] { variable },
                    _ => throw new NotImplementedException()
                })
                .Where(v => v is IRRF_VariableGlobalModel global)
                .Select(v => (IRRF_VariableGlobalModel)v)
                .Where(v => !file_set.Contains(v.LoadVariableMacro))
                .Select(v => v)
                .ToList();

            return (true, missing_variables);
        }


        // GCODE
        public override async Task<bool> StopAsync()
        {
            using var put_reset_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await PostGCodeAsync(new[]
            {
                "M108", "M25",
                "set global.iterator = false", "M0"
            }, put_reset_cts.Token);
        }
        public override async Task<bool> PauseAsync(bool end_filament)
        {
            using var put_reset_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await PostGCodeAsync(new[]
            {
                "M108", "M25",
                "set global.iterator = false", "M0",
                GetExecuteMacroGCode(CombinePaths(MacroPath, "job"), end_filament ? "end_filament.g" : "pause.g")
            }, put_reset_cts.Token);
        }
        public override async Task<bool> CancelAsync()
        {
            using var put_reset_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await PostGCodeAsync(new GCodeString[]
            {
                "M108", "M25",
                "set global.iterator = false", "M0",
                GetExecuteMacroGCode(CombinePaths(MacroPath, "job"), "cancel.g"),
                GetExecuteMacroGCode(MacroPath, "end_print")
            }, put_reset_cts.Token);
        }
        public int GetGCodeLenght(GCodeString paramacro)
        {
            return $"rr_gcode?gcode={string.Join("%0A", paramacro)}".Length;
        }
        public async Task<bool> PostGCodeAsync(GCodeString gcode, CancellationToken ct)
        {
            try
            {
                var resource = $"rr_gcode?gcode={string.Join("%0A", gcode)}";
                if (resource.Length >= 160)
                {
                    Flux.Messages.LogMessage("Errore esecuzione gcode", $"Lunghezza gcode oltre i limiti", MessageLevel.EMERG, 0);
                    return false;
                }

                var request     = new RRF_Request(resource, HttpMethod.Get, RRF_RequestPriority.Immediate, ct);
                var response    = await ExecuteAsync(request);
                if (!response.Ok)
                {
                    Flux.Messages.LogMessage($"{request}", $"{response}", MessageLevel.ERROR, 0);
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
        public override async Task<bool> ExecuteParamacroAsync(GCodeString paramacro, CancellationToken put_ct, bool can_cancel = false)
        {
            try
            {
                if (!paramacro.HasValue)
                    return false;

                var lenght = GetGCodeLenght(paramacro);
                if (!can_cancel && lenght < 160)
                {
                    return await PostGCodeAsync(paramacro, put_ct);
                }
                else
                {
                    using var delete_job_ctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var delete_job_response = await DeleteAsync(StoragePath, "job.mcode", true, delete_job_ctk.Token);
                    if (!delete_job_response)
                        return false;

                    using var delete_paramacro_ctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var delete_paramacro_response = await DeleteAsync(StoragePath, "paramacro.mcode", true, delete_paramacro_ctk.Token);
                    if (!delete_paramacro_response)
                        return false;

                    // put file
                    var put_paramacro_response = await PutFileAsync(
                        StoragePath,
                        "paramacro.mcode", true,
                        put_ct, get_paramacro_gcode().ToOptional());

                    if (put_paramacro_response == false)
                        return false;

                    var job_source = $"M98 P\"0:/gcodes/storage/paramacro.mcode\"";
                    var put_job_response = await PutFileAsync(
                        StoragePath,
                        "job.mcode", true,
                        put_ct, job_source);

                    if (put_job_response == false)
                        return false;

                    // Set PLC to Cycle
                    using var put_reset_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    return await PostGCodeAsync(new[] { $"M32 \"0:/{CombinePaths(StoragePath, "job.mcode")}\"" }, put_reset_cts.Token);

                    IEnumerable<string> get_paramacro_gcode()
                    {
                        yield return $"M98 R{(can_cancel ? 1 : 0)}";
                        yield return $"set global.iterator = true";
                        foreach (var line in paramacro)
                            yield return line.TrimEnd();
                    }
                }
            }
            catch
            {
                return false;
            }
        }


        public override async Task<bool> PutFileAsync(
            string folder,
            string filename,
            bool is_paramacro,
            CancellationToken ct,
            GCodeString source = default,
            GCodeString start = default,
            GCodeString end = default,
            Optional<BlockNumber> source_blocks = default,
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
                if (!plc_address.HasValue)
                    return default;

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromHours(1);
                client.BaseAddress = new Uri(plc_address.Value);
                client.DefaultRequestHeaders.Add("Connection", "Keep-Alive");
                var response = await client.PostAsync($"rr_upload?name=0:/{folder}/{filename}&time={DateTime.Now}", content_stream, ct);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    Flux.Messages.LogMessage("Errore durante l'upload del file", $"Stato: {Enum.GetName(typeof(HttpStatusCode), response.StatusCode)}", MessageLevel.ERROR, 0);
                    return false;
                }

                return true;

                IEnumerable<string> get_full_source()
                {
                    if (start.HasValue)
                    {
                        yield return "; flux start gcode";
                        foreach (var line in start)
                            yield return line;
                        yield return "";
                    }

                    long current_block = 0;
                    if (source.HasValue)
                    {
                        if (source_blocks.HasValue && source_blocks.Value > 0)
                        {
                            foreach (var line in source)
                            {
                                var progress = (double)current_block++ / source_blocks.Value * 100;
                                if (progress - (int)progress < 0.001)
                                    report_progress((int)progress);
                                yield return line;
                            }
                        }
                        else
                        {
                            foreach (var line in source)
                                yield return line;
                        }
                    }

                    if (end.HasValue)
                    {
                        yield return "; flux end gcode";
                        foreach (var line in end)
                            yield return line;
                        yield return "";
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                    Flux.Messages.LogException(this, ex);
                return false;
            }
        }
        public override async Task<bool> CreateFolderAsync(string folder, string name, CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return false;

                var path     = $"{folder}/{name}".TrimStart('/');
                var request  = new RRF_Request($"rr_mkdir?dir=0:/{path}", HttpMethod.Get, RRF_RequestPriority.Immediate, ct);
                var response = await ExecuteAsync(request);
                return response.Ok;
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
                Optional<FLUX_FileList> file_list = default;
                var full_file_list = new FLUX_FileList(folder);
                do
                {
                    var first    = file_list.ConvertOr(f => f.Next, () => 0);
                    var request  = new RRF_Request($"rr_filelist?dir={folder}&first={first}", HttpMethod.Get, RRF_RequestPriority.Immediate, ct);
                    var response = await ExecuteAsync(request);

                    file_list = response.GetContent<FLUX_FileList>();
                    if (file_list.HasValue)
                        full_file_list.Files.AddRange(file_list.Value.Files);

                } while (file_list.HasValue && file_list.Value.Next != 0);

                return full_file_list;
            }
            catch
            {
                return default;
            }
        }
        public override async Task<bool> ClearFolderAsync(string folder, bool wait, CancellationToken ct = default)
        {
            try
            {
                return await clear_f_async(folder);
            }
            catch (Exception ex)
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

                    var list_req = new RRF_Request($"rr_filelist?dir=0:/{folder}", HttpMethod.Get, RRF_RequestPriority.Immediate, ct);
                    var list_res = await ExecuteAsync(list_req);
                    if (!list_res.Ok)
                    {
                        Flux.Messages.LogMessage("Errore durante la pulizia della cartella", "Lista dei file non trovata", MessageLevel.ERROR, 0);
                        return false;
                    }

                    var file_list = list_res.GetContent<FLUX_FileList>();
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
                                var del_f_req = new RRF_Request($"rr_delete?name={filename}", HttpMethod.Get, RRF_RequestPriority.Immediate, ct);
                                var del_f_res = await ExecuteAsync(del_f_req);
                                if (!del_f_res.Ok)
                                {
                                    Flux.Messages.LogMessage("Errore durante la pulizia della cartella", $"Impossibile cancellare il file: {del_f_res}", MessageLevel.ERROR, 0);
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
                    var del_d_req = new RRF_Request($"rr_delete?name={directory}", HttpMethod.Get, RRF_RequestPriority.Immediate, ct);
                    var del_d_res = await ExecuteAsync(del_d_req);
                    if (!del_d_res.Ok)
                    {
                        Flux.Messages.LogMessage("Errore durante la pulizia della cartella", $"Impossibile cancellare la cartella: {del_d_res}", MessageLevel.ERROR, 0);
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
        public override async Task<Optional<string>> GetFileAsync(string folder, string filename, CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return default;

                var request  = new RRF_Request($"rr_download?name=0:/{folder}/{filename}", HttpMethod.Get, RRF_RequestPriority.Immediate, ct);
                var response = await ExecuteAsync(request);
                return response.Content;
            }
            catch
            {
                return default;
            }
        }
        public override async Task<bool> DeleteAsync(string folder, string filename, bool wait, CancellationToken ct = default)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return false;

                var path = $"{folder}/{filename}".TrimStart('/');
                using var clear_folder_ctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                if (!await ClearFolderAsync(path, true, clear_folder_ctk.Token))
                    return false;

                var request = new RRF_Request($"rr_delete?name=0:/{path}", HttpMethod.Get, RRF_RequestPriority.Immediate, ct);
                var response = await ExecuteAsync(request);

                if (!response.Ok)
                    return false;

                if (wait)
                {
                    var file_system = Observable.Interval(TimeSpan.FromSeconds(0.1))
                        .Select(_ => Observable.FromAsync(() => ListFilesAsync(folder, ct)))
                        .Merge(1);

                    return await WaitUtils.WaitForOptionalAsync(
                        file_system, TimeSpan.FromSeconds(0.2),
                        f => !f.Files.Any(f => f.Name == filename),
                        ct);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
        public override async Task<bool> RenameAsync(string folder, string old_filename, string new_filename, bool wait, CancellationToken ct = default)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return false;

                var old_path = $"{folder}/{old_filename}".TrimStart('/');
                var new_path = $"{folder}/{new_filename}".TrimStart('/');

                var request = new RRF_Request($"rr_move?old=0:/{old_path}&new=0:/{new_path}", HttpMethod.Get, RRF_RequestPriority.Immediate, ct);
                var response = await ExecuteAsync(request);

                if (!response.Ok)
                    return false;

                if (wait)
                {
                    var file_system = Observable.Interval(TimeSpan.FromSeconds(0.1))
                        .Select(_ => Observable.FromAsync(() => ListFilesAsync(folder, ct)))
                        .Merge(1);

                    return await WaitUtils.WaitForOptionalAsync(
                        file_system, TimeSpan.FromSeconds(0.2),
                        f => f.Files.Any(f => f.Name == new_filename),
                        ct);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public override GCodeString GetParkToolGCode()
        {
            return $"T-1";
        }
        public override GCodeString GetProbePlateGCode()
        {
            return "M98 P\"/macros/probe_plate\"";
        }
        public override GCodeString GetLowerPlateGCode()
        {
            return "M98 P\"/macros/lower_plate\"";
        }
        public override GCodeString GetRaisePlateGCode()
        {
            return "M98 P\"/macros/raise_plate\"";
        }
        public override GCodeString GetCenterPositionGCode()
        {
            return "M98 P\"/macros/center_position\"";
        }
        public override GCodeString GetSetLowCurrentGCode()
        {
            return "M98 P\"/macros/low_current\"";
        }
        public override GCodeString GetProbeMagazineGCode()
        {
            return "M98 P\"/macros/probe_magazine\"";
        }
        public override GCodeString GetExitPartProgramGCode()
        {
            return "M99";
        }
        public override GCodeString GetHomingGCode(params char[] axis)
        {
            return $"G28 {string.Join(" ", axis.Select(a => $"{a}0"))}";
        }
        public override GCodeString GetManualCalibrationPositionGCode()
        {
            return "M98 P\"/macros/manual_calibration_position\"";
        }
        public override GCodeString GetSelectToolGCode(ArrayIndex position)
        {
            return $"T{position.GetArrayBaseIndex()}";
        }
        public override GCodeString GetCancelLoadFilamentGCode(ArrayIndex position)
        {
            return new[] 
            {
                $"G10 P{position.GetArrayBaseIndex()} S0 R0",
                "T-1"
            };
        }
        public override GCodeString GetCancelUnloadFilamentGCode(ArrayIndex position)
        {
            return new[]
             {
                $"G10 P{position.GetArrayBaseIndex()} S0 R0",
                "T-1"
            };
        }
        public override GCodeString GetExecuteMacroGCode(string folder, string filename)
        {
            return $"M98 P\"0:/{folder}/{filename}\"";
        }
        public override GCodeString GetProbeToolGCode(ArrayIndex position, double temperature)
        {
            throw new NotImplementedException();
        }
        public override GCodeString GetSetToolTemperatureGCode(ArrayIndex position, double temperature, bool wait)
        {
            return $"{(wait ? "M109" : "M104")} T{position.GetArrayBaseIndex()} S{temperature}";
        }
        public override GCodeString GetSetPlateTemperatureGCode(ArrayIndex position, double temperature, bool wait)
        {
            return $"{(wait ? "M190" : "M140")} P{position.GetArrayBaseIndex()} S{temperature}";
        }
        public override GCodeString GetSetChamberTemperatureGCode(ArrayIndex position, double temperature, bool wait)
        {
            return $"{(wait ? "M191" : "M141")} P{position.GetArrayBaseIndex()} S{temperature}";
        }
        public override GCodeString GetResetPositionGCodeInner(FLUX_AxisMove axis_move)
        {
            return $"G92 {axis_move.GetAxisPosition()}";
        }
        public override GCodeString GetFilamentSensorSettingsGCode(ArrayIndex position, bool enabled)
        {
            var filament_unit = VariableStore.GetArrayUnit(c => c.FILAMENT_BEFORE_GEAR, position);
            if (!filament_unit.HasValue)
                return default;

            return $"M591 D{filament_unit.Value.Index} S{(enabled ? 1 : 0)}";
        }
        public override GCodeString GetMovementGCodeInner(FLUX_AxisMove axis_move)
        {
            return GCodeString.Create(
                axis_move.Relative ? new[] { "M120", "G91" } : default,
                $"G1 {axis_move.GetAxisPosition()} {axis_move.GetFeedrate('F')}",
                axis_move.Relative ? new[] { "G90", "M121" } : default);
        }
        public override GCodeString GetSetToolOffsetGCode(ArrayIndex position, double x, double y, double z)
        {
            return new[]
            {
                $"G10 P{position.GetArrayBaseIndex()} X{{{x * -1}}}",
                $"G10 P{position.GetArrayBaseIndex()} Y{{{y * -1}}}",
                $"G10 P{position.GetArrayBaseIndex()} Z{{{z * -1}}}",
            };
        }

        public override GCodeString GetSetExtruderMixingGCode(ArrayIndex machine_extruder, ArrayIndex mixing_extruder)
        {
            var extruder_count = Flux.SettingsProvider.ExtrudersCount;
            if (!extruder_count.HasValue)
                return default;
            
            var mixing_start = ArrayIndex.FromZeroBase(0, VariableStoreBase).GetArrayBaseIndex();
            var selected_extruder = mixing_extruder.GetArrayBaseIndex();

            var mixing_count = extruder_count.Value.mixing_extruders;
            var mixing = Enumerable.Range(mixing_start, mixing_count)
                .Select(i => i == selected_extruder ? "1" : "0");

            return $"M567 P{machine_extruder.GetArrayBaseIndex()} E1:{string.Join(":", mixing)}";
        }

        public override Task<Stream> GetFileStreamAsync(string folder, string name, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
        public override Task<bool> PutFileStreamAsync(string folder, string name, Stream data, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetStartPartProgramGCode(string folder, string filename, BlockNumber start_block)
        {
            return new[]
            {
                $"M23 0:/{CombinePaths(folder, filename)}",
                $"M26 S{start_block}",
                $"M24"
            };
        }
    }
}
