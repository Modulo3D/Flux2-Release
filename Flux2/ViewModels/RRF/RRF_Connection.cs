﻿using DynamicData;
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
        public override bool ParkToolAfterOperation => false;
        public override string InnerQueuePath => "gcodes/queue/inner";
        public override string ExtrusionPath => "gcodes/events/extr";
        public override string JobEventPath => "gcodes/events/job";
        public override string StoragePath => "gcodes/storage";
        public override string QueuePath => "gcodes/queue";
        public override string PathSeparator => "/";
        public override string MacroPath => "macros";
        public string GlobalPath => "sys/global";
        public override string RootPath => "";
        public override ushort ArrayBase => 0;

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

        public async Task<bool> InitializeVariablesAsync(CancellationToken ct)
        {
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

            var source_variables = variables
                .Select(v => v.LoadVariableMacro)
                .Select(m => $"M98 P\"/sys/global/{m}\"")
                .ToOptional();

            using var sha256 = SHA256.Create();
            var written_hash = written_file.ConvertOr(w =>
            {
                var written = w.Split(Environment.NewLine.ToCharArray(),
                    StringSplitOptions.RemoveEmptyEntries);
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(string.Join("", written))).ToHex();
            }, () => "");

            var source_hash = source_variables.ConvertOr(s =>
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(string.Join("", s))).ToHex();
            }, () => "");

            if (written_hash != source_hash)
                if (!await PutFileAsync(c => ((RRF_Connection)c).GlobalPath, "initialize_variables.g", true, ct, source_variables))
                    return false;

            return await PostGCodeAsync(new[] { "M98 P\"/sys/global/initialize_variables.g\"" }, ct);
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

        public async Task<bool> CreateVariablesAsync(CancellationToken ct)
        {
            var missing_variables = await FindMissingVariables(ct);
            if (!missing_variables.result)
                return false;

            if (missing_variables.variables.Count == 0)
                return true;

            var advanced_mode = Flux.MCodes.OperatorUSB
                .ConvertOr(usb => usb.AdvancedSettings, () => false);
            if (!advanced_mode)
                return false;

            var files_str = string.Join(Environment.NewLine, missing_variables.variables.Select(v => v.LoadVariableMacro));
            var create_variables_result = await Flux.ShowConfirmDialogAsync("Creare file di variabile?", files_str);

            if (create_variables_result != ContentDialogResult.Primary)
                return false;

            await Flux.ConnectionProvider.DeleteAsync(c => ((RRF_Connection)c).GlobalPath, "initialize_variables.g", false, ct);

            foreach (var variable in missing_variables.variables)
                if (!await variable.CreateVariableAsync(ct))
                    return false;

            return true;
        }

        // GCODE
        public async Task<bool> PostGCodeAsync(IEnumerable<string> gcode, CancellationToken ct, bool wait = false, CancellationToken gcode_ct = default)
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

                if (wait && gcode_ct != CancellationToken.None && !await WaitProcessStatusAsync(
                    status => status == FLUX_ProcessStatus.IDLE,
                    TimeSpan.FromSeconds(0.5),
                    TimeSpan.FromSeconds(0.1),
                    gcode_ct))
                {
                    Flux.Messages.LogMessage($"{request}", $"Timeout esecuzione gcode", MessageLevel.ERROR, 0);
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public override async Task<bool> HoldAsync()
        {
            using var put_hold_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var wait_hold_cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            return await PostGCodeAsync(new[] { "M108", "M25", "M0" }, put_hold_cts.Token, true, wait_hold_cts.Token);
        }
        public override async Task<bool> ResetAsync()
        {
            using var put_reset_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var wait_reset_cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            return await PostGCodeAsync(new[]
            {
                "M108", "M25",
                "set global.iterator = false", "M0"
            }, put_reset_cts.Token, true, wait_reset_cts.Token);
        }
        private int GetGCodeLenght(IEnumerable<string> paramacro)
        {
            return $"rr_gcode?gcode={string.Join("%0A", paramacro)}".Length;
        }
        public override async Task<bool> CancelPrintAsync()
        {
            using var put_cancel_print_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var wait_cancel_print_cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            return await PostGCodeAsync(new[]
            {
                "M108", "M25",
                "set global.iterator = false", "M0",
                "M98 P\"/macros/cancel_print\" R-1",
            }, put_cancel_print_cts.Token, true, wait_cancel_print_cts.Token);
        }
        public override async Task<bool> CycleAsync(string folder, string filename, bool wait = false, CancellationToken wait_ct = default)
        {
            using var put_start_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await PostGCodeAsync(new[] { $"M32 \"0:/{folder}/{filename}\"" }, put_start_cts.Token, wait, wait_ct);
        }
        public override async Task<bool> ExecuteParamacroAsync(IEnumerable<string> paramacro, CancellationToken put_ct, bool wait = false, CancellationToken wait_ct = default, bool can_cancel = false)
        {
            try
            {
                var lenght = GetGCodeLenght(paramacro);
                if (!can_cancel && lenght < 160)
                {
                    return await PostGCodeAsync(paramacro, put_ct, wait, wait_ct);
                }
                else
                {
                    using var delete_job_ctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var delete_job_response = await DeleteAsync(StoragePath, "job.mcode", true, delete_job_ctk.Token);

                    using var delete_paramacro_ctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var delete_paramacro_response = await DeleteAsync(StoragePath, "paramacro.mcode", true, delete_paramacro_ctk.Token);

                    // put file
                    var put_paramacro_response = await PutFileAsync(
                        StoragePath,
                        "paramacro.mcode", true,
                        put_ct, get_paramacro_gcode().ToOptional());

                    if (put_paramacro_response == false)
                        return false;

                    var put_job_response = await PutFileAsync(
                        StoragePath,
                        "job.mcode", true,
                        put_ct, get_job_gcode().ToOptional());

                    if (put_job_response == false)
                        return false;

                    // Set PLC to Cycle
                    return await CycleAsync(StoragePath, "job.mcode", wait, wait_ct);

                    IEnumerable<string> get_job_gcode()
                    {
                        yield return $"M98 P\"0:/gcodes/storage/paramacro.mcode\"";
                        yield return $"if global.iterator == false";
                        yield return $" M98 P\"0:/macros/cancel_print\" R0";
                    }

                    IEnumerable<string> get_paramacro_gcode()
                    {
                        yield return $"M98 R{(can_cancel ? 1 : 0)}";
                        yield return $"set global.iterator = true";
                        yield return $"";
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
            Optional<IEnumerable<string>> source = default,
            Optional<IEnumerable<string>> start = default,
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
                        file_system,
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
                        file_system,
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

        public override Optional<IEnumerable<string>> GetParkToolGCode()
        {
            return new[] { $"T-1" };
        }
        public override Optional<IEnumerable<string>> GetProbePlateGCode()
        {
            return new[] { "M98 P\"/macros/probe_plate\"" };
        }
        public override Optional<IEnumerable<string>> GetLowerPlateGCode()
        {
            return new[] { "M98 P\"/macros/lower_plate\"" };
        }
        public override Optional<IEnumerable<string>> GetRaisePlateGCode()
        {
            return new[] { "M98 P\"/macros/raise_plate\"" };
        }
        public override Optional<IEnumerable<string>> GetCenterPositionGCode()
        {
            return new[] { "M98 P\"/macros/center_position\"" };
        }
        public override Optional<IEnumerable<string>> GetSetLowCurrentGCode()
        {
            return new[] { "M98 P\"/macros/low_current\"" };
        }
        public override Optional<IEnumerable<string>> GetProbeMagazineGCode()
        {
            return new[] { "M98 P\"/macros/probe_magazine\"" };
        }
        public override Optional<IEnumerable<string>> GetHomingGCode(params char[] axis)
        {
            return new[] { $"G28 {string.Join(" ", axis.Select(a => $"{a}0"))}" };
        }
        public override Optional<IEnumerable<string>> GetSelectToolGCode(ArrayIndex position)
        {
            return new[] { $"T{position.GetArrayBaseIndex(this)}" };
        }
        public override Optional<IEnumerable<string>> GetManualCalibrationPositionGCode()
        {
            return new[] { "M98 P\"/macros/manual_calibration_position\"" };
        }
        public override Optional<IEnumerable<string>> GetExecuteMacroGCode(string folder, string filename)
        {
            return new[] { $"M98 P\"0:/{folder}/{filename}\"" };
        }
        public override Optional<IEnumerable<string>> GetCancelLoadFilamentGCode(ArrayIndex position)
        {
            return new[] 
            {
                $"G10 P{position.GetArrayBaseIndex(this)} S0 R0",
                "T-1"
            };
        }
        public override Optional<IEnumerable<string>> GetCancelUnloadFilamentGCode(ArrayIndex position)
        {
            return new[]
             {
                $"G10 P{position.GetArrayBaseIndex(this)} S0 R0",
                "T-1"
            };
        }
        public override Optional<IEnumerable<string>> GetStartPartProgramGCode(JobPartPrograms job_partprograms)
        {
            var job = job_partprograms.Job;
            var part_program = job_partprograms.GetCurrentPartProgram();
            if (!part_program.HasValue)
                return default;

            return new[]
            {
                $"M98 P\"/macros/job/start\" A\"{extrusion_key(0)}\" B\"{extrusion_key(1)}\" C\"{extrusion_key(2)}\" D\"{extrusion_key(3)}\" J\"{job.JobKey}\" K\"{job.MCodeKey}\" R1",
                $"M32 \"0:/{StoragePath}/{part_program.Value}\""
            };

            string extrusion_key(ushort position)
            {
                return Flux.Feeders.Feeders.Lookup(position)
                    .Convert(f => f.SelectedMaterial)
                    .Convert(m => m.ExtrusionKey)
                    .ConvertOrDefault(e => $"{e}");
            }
        }
        public override Optional<IEnumerable<string>> GetSetToolTemperatureGCode(ArrayIndex position, double temperature)
        {
            return new[] { $"M104 T{position.GetArrayBaseIndex(this)} S{temperature}" };
        }
        public override Optional<IEnumerable<string>> GetSetToolOffsetGCode(ArrayIndex position, double x, double y, double z)
        {
            return new[]
            {
                $"G10 P{position.GetArrayBaseIndex(this)} X{{{x * -1}}}",
                $"G10 P{position.GetArrayBaseIndex(this)} Y{{{y * -1}}}",
                $"G10 P{position.GetArrayBaseIndex(this)} Z{{{z * -1}}}",
            };
        }
        public override Optional<IEnumerable<string>> GetProbeToolGCode(ArrayIndex position, double temperature)
        {
            throw new NotImplementedException();
        }
        public override Optional<IEnumerable<string>> GetSetExtruderMixingGCode(ArrayIndex machine_extruder, ArrayIndex mixing_extruder)
        {
            var extruder_count = Flux.SettingsProvider.ExtrudersCount;
            if (!extruder_count.HasValue)
                return default;
            
            var mixing_start = new ArrayIndex(0).GetArrayBaseIndex(this);
            var selected_extruder = mixing_extruder.GetArrayBaseIndex(this);

            var mixing_count = extruder_count.Value.mixing_extruders;
            var mixing = Enumerable.Range(mixing_start, mixing_count)
                .Select(i => i == selected_extruder ? "1" : "0");

            return new[] { $"M567 P{machine_extruder.GetArrayBaseIndex(this)} E1:{string.Join(":", mixing)}" };
        }
        public override Optional<IEnumerable<string>> GetRelativeXMovementGCode(double distance, double feedrate) => new string[] { "M120", "G91", $"G1 X{distance} F{feedrate}".Replace(",", "."), "G90", "M121" };
        public override Optional<IEnumerable<string>> GetRelativeYMovementGCode(double distance, double feedrate) => new string[] { "M120", "G91", $"G1 Y{distance} F{feedrate}".Replace(",", "."), "G90", "M121" };
        public override Optional<IEnumerable<string>> GetRelativeZMovementGCode(double distance, double feedrate) => new string[] { "M120", "G91", $"G1 Z{distance} F{feedrate}".Replace(",", "."), "G90", "M121" };
        public override Optional<IEnumerable<string>> GetRelativeEMovementGCode(double distance, double feedrate) => new string[] { "M120", "G91", $"G1 E{distance} F{feedrate}".Replace(",", "."), "G90", "M121" };

        public override Optional<IEnumerable<string>> GetCancelOperationGCode(ushort reason)
        {
            return new[] { $"M98 P\"0:/macros/cancel_print\" R{reason}", "M99" };
        }

        public override Optional<IEnumerable<string>> GetManualFilamentInsertGCode(ArrayIndex position, double iteration_distance, double feedrate)
        {
            var gcode = new List<string>();

            var select_tool_gcode = GetSelectToolGCode(position);
            if (!select_tool_gcode.HasValue)
                return default;
            gcode.Add(select_tool_gcode.Value);

            var movement_gcode = GetRelativeEMovementGCode(iteration_distance, feedrate);
            if (!movement_gcode.HasValue)
                return default;
            gcode.Add(movement_gcode.Value);

            return gcode;
        }

        public override Optional<IEnumerable<string>> GetManualFilamentExtractGCode(ArrayIndex position, ushort iterations, double iteration_distance, double feedrate)
        {
            var gcode = new List<string>();

            var select_tool_gcode = GetSelectToolGCode(position);
            if (!select_tool_gcode.HasValue)
                return default;
            gcode.Add(select_tool_gcode.Value);

            var extract_distance = (iteration_distance * iterations) + 50;
            var movement_gcode = GetRelativeEMovementGCode(-extract_distance, 500);
            if (!movement_gcode.HasValue)
                return default;
            gcode.Add(movement_gcode.Value);

            var park_tool_gcode = GetParkToolGCode();
            if (!park_tool_gcode.HasValue)
                return default;
            gcode.Add(park_tool_gcode.Value);

            return gcode;
        }

        public override Task<bool> PutFileStreamAsync(string folder, string name, Stream data, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public override Task<Stream> GetFileStreamAsync(string folder, string name, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
