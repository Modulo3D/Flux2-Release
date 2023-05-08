using DynamicData;
using DynamicData.Kernel;
using GreenSuperGreen.Queues;
using Modulo3DNet;
using OSAI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Flux.ViewModels
{
    public readonly struct RRF_Request<TData> : IFLUX_Request<HttpClient, RRF_Response<TData>>
    {
        public TimeSpan Timeout { get; }
        public HttpRequestMessage Request { get; }
        public FLUX_RequestPriority Priority { get; }
        public CancellationToken Cancellation { get; }
        public TaskCompletionSource<RRF_Response<TData>> Response { get; }
        public RRF_Request(string request, HttpMethod httpMethod, FLUX_RequestPriority priority, CancellationToken ct, TimeSpan timeout = default)
        {
            Timeout = timeout;
            Cancellation = ct;
            Priority = priority;
            Request = new HttpRequestMessage(httpMethod, request);
            Response = new TaskCompletionSource<RRF_Response<TData>>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        public override string ToString() => $"{nameof(RRF_Request<TData>)} Method:{Request.Method} Resource:{Request.RequestUri}";
        public async Task<bool> TrySendAsync(HttpClient client, CancellationToken ct)
        {
            try
            {
                using var response = await client.SendAsync(Request, HttpCompletionOption.ResponseHeadersRead, ct);
                using var stream = await response.Content.ReadAsStreamAsync(ct);

                using var reader = new StreamReader(stream);
                var payload = typeof(TData) == typeof(string) ?
                    ((TData)(object)await reader.ReadToEndAsync(ct)).ToOptional() :
                    JsonUtils.Deserialize<TData>(reader);

                var rrf_response = new RRF_Response<TData>(response.StatusCode, payload);
                return Response.TrySetResult(rrf_response);
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

    public readonly record struct RRF_Response<TData>(HttpStatusCode StatusCode, Optional<TData> Content) : IFLUX_Response
    {
        public bool Ok
        {
            get
            {
                if (!Content.HasValue)
                    return false;
                return StatusCode == HttpStatusCode.OK;
            }
        }

        public override string ToString()
        {
            if (!Content.HasValue)
                return $"{nameof(RRF_Response<TData>)} Task cancellata";
            if (StatusCode == HttpStatusCode.OK)
                return $"{nameof(RRF_Response<TData>)} OK";
            return $"{nameof(RRF_Response<TData>)} Status:{Enum.GetName(typeof(HttpStatusCode), StatusCode)}";
        }
    }

    public class RRF_Connection : FLUX_Connection<RRF_Connection, RRF_ConnectionProvider, RRF_VariableStoreBase, HttpClient>
    {
        public override string CombinePaths(params string[] paths) => string.Join("/", paths);
        public static string SystemPath => "sys";
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

        public RRF_Connection(FluxViewModel flux, RRF_ConnectionProvider connection_provider) : base(connection_provider)
        {
            Flux = flux;
        }

        // BASIC OPERATIONS
        public static int GetGCodeLenght(GCodeString paramacro)
        {
            return $"rr_gcode?gcode={string.Join("%0A", paramacro)}".Length;
        }
        public override async Task<bool> InitializeVariablesAsync(CancellationToken ct)
        {
            var missing_variables = await FindMissingGlobalVariables(ct);
            if (!missing_variables.result)
                return false;

            if (missing_variables.variables.Count != 0)
            {
                var advanced_mode = Flux.MCodes.OperatorUSB
                    .ConvertOr(usb => usb.AdvancedSettings, () => false);
                if (!advanced_mode)
                    return false;

                var files_str = string.Join(Environment.NewLine, missing_variables.variables.Select(v => v.LoadVariableMacro));
                var create_variables_result = await Flux.ShowDialogAsync(f => new ConfirmDialog(f, new RemoteText("createVariables", true), new RemoteText(files_str, false)));

                if (create_variables_result.result != DialogResult.Primary)
                    return false;

                await ConnectionProvider.DeleteAsync(c => ((RRF_Connection)c).GlobalPath, "init_vars.g", ct);

                foreach (var variable in missing_variables.variables)
                    if (!await variable.CreateVariableAsync(ct))
                        return false;
            }

            var written_file = await GetFileAsync(GlobalPath, "init_vars.g", ct);
            var global_variables = VariableStore.Variables.Values
               .SelectMany(v => v switch
               {
                   IFLUX_Array array => array.Variables.Items,
                   IFLUX_Variable variable => new[] { variable },
                   _ => throw new NotImplementedException()
               })
               .Where(v => v is IRRF_VariableGlobalModel global)
               .Select(v => (IRRF_VariableGlobalModel)v);

            var source_variables = new GCodeString(global_variables
                .Select(v => GetExecuteMacroGCode(GlobalPath, v.LoadVariableMacro)));

            var source_hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("", source_variables))).ToHex();
            var written_hash = written_file.Convert(w => SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("", w.SplitLines()))).ToHex());
            if (!written_hash.HasValue || written_hash != source_hash)
            {
                if (!await PutFileAsync(GlobalPath, "init_vars.g", true, ct, source_variables))
                    return false;

                await WriteVariableAsync(c => c.INIT_VARS, false);
            }

            var init_vars = await ReadVariableAsync(c => c.INIT_VARS);
            if (!init_vars.HasValue || !init_vars.Value)
            {
                var init_vars_gcode = GCodeString.Create(
                    GetExecuteMacroGCode(GlobalPath, "init_vars.g"),
                    "set global.init_vars = true");

                if (!await PostGCodeAsync(init_vars_gcode, ct))
                    return false;
            }

            return true;
        }
        public async Task<RRF_Response<TData>> TryEnqueueRequestAsync<TData>(RRF_Request<TData> rrf_request)
        {
            return await TryEnqueueRequestAsync<RRF_Request<TData>, RRF_Response<TData>>(rrf_request);
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

                var request = new RRF_Request<string>(resource, HttpMethod.Get, FLUX_RequestPriority.Immediate, ct);
                var response = await TryEnqueueRequestAsync<RRF_Request<string>, RRF_Response<string>>(request);
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
        private async Task<(bool result, List<IRRF_VariableGlobalModel> variables)> FindMissingGlobalVariables(CancellationToken ct)
        {
            var variables = VariableStore.Variables.Values
                .SelectMany(v => v switch
                {
                    IFLUX_Array array => array.Variables.Items,
                    IFLUX_Variable variable => new[] { variable },
                    _ => throw new NotImplementedException()
                })
                .Where(v => v is IRRF_Variable global)
                .Select(v => (IRRF_Variable)v);

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

        // CONNECTION
        protected override HttpClient ConnectAsyncInternal(string plc_address)
        {
            return new HttpClient() { BaseAddress = new Uri(plc_address) };
        }
        protected override Task CloseAsyncInternal(HttpClient client)
        {
            client.Dispose();
            return Task.CompletedTask;
        }
       
        // CONTROL
        public override async Task<bool> StopAsync(CancellationToken ct)
        {
            using var put_reset_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await PostGCodeAsync(new[]
            {
                "M108", "M25",
                "set global.iterator = false", "M0"
            }, ct);
        }
        public override async Task<bool> CancelAsync(CancellationToken ct)
        {
            return await PostGCodeAsync(new GCodeString[]
            {
                "M108", "M25",
                "set global.iterator = false", "M0",
                GetExecuteMacroGCode(InnerQueuePath, "cancel.g"),
                GetExecuteMacroGCode(MacroPath, "end_print")
            }, ct);
        }
        public override async Task<bool> PauseAsync(CancellationToken ct)
        {
            return await PostGCodeAsync(new[]
            {
                "M108", "M25",
                "set global.iterator = false", "M0",
                GetExecuteMacroGCode(InnerQueuePath, "pause.g")
            }, ct);
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
                    var delete_job_response = await DeleteAsync(StoragePath, "job.mcode", delete_job_ctk.Token);
                    if (!delete_job_response)
                        return false;

                    using var delete_paramacro_ctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var delete_paramacro_response = await DeleteAsync(StoragePath, "paramacro.mcode", delete_paramacro_ctk.Token);
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

        // FILE SYSTEM
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
                    return false;

                // delete file
                if (!await DeleteAsync(folder, filename, ct))
                    return false;

                // Write content
                using var lines_stream = new GCodeStream(get_full_source());
                using var content_stream = new StreamContent(lines_stream);
                content_stream.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                content_stream.Headers.ContentLength = lines_stream.Length;

                var plc_address = Flux.SettingsProvider.CoreSettings.Local.PLCAddress;
                if (!plc_address.HasValue)
                    return false;

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromHours(1);
                client.BaseAddress = new Uri(plc_address.Value);
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
        public override Task<Stream> GetFileStreamAsync(string folder, string name, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
        public override async Task<bool> CreateFolderAsync(string folder, string name, CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return false;

                var path = $"{folder}/{name}".TrimStart('/');
                var request = new RRF_Request<string>($"rr_mkdir?dir=0:/{path}", HttpMethod.Get, FLUX_RequestPriority.Immediate, ct);
                var response = await TryEnqueueRequestAsync<RRF_Request<string>, RRF_Response<string>>(request);
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
                    var first = file_list.ConvertOr(f => f.Next, () => 0);
                    var request = new RRF_Request<FLUX_FileList>($"rr_filelist?dir={folder}&first={first}", HttpMethod.Get, FLUX_RequestPriority.Immediate, ct);
                    var response = await TryEnqueueRequestAsync<RRF_Request<FLUX_FileList>, RRF_Response<FLUX_FileList>>(request);

                    file_list = response.Content;
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
        public override async Task<bool> ClearFolderAsync(string folder, CancellationToken ct)
        {
            try
            {
                var file_list = await ListFilesAsync(folder, ct);
                if (!file_list.HasValue)
                {
                    Flux.Messages.LogMessage("Errore pulizia cartella", $"Impossibile leggere lista file", MessageLevel.EMERG, 0);
                    return false;
                }

                foreach (var file in file_list.Value.Files)
                {
                    if (!await DeleteAsync(folder, file.Name, ct))
                    {
                        Flux.Messages.LogMessage("Errore pulizia cartella", $"Impossibile cancellare file", MessageLevel.EMERG, 0);
                        return false;
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
        public override Task<bool> PutFileStreamAsync(string folder, string name, Stream data, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
        public override async Task<Optional<string>> GetFileAsync(string folder, string filename, CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return default;

                var request = new RRF_Request<string>($"rr_download?name=0:/{folder}/{filename}", HttpMethod.Get, FLUX_RequestPriority.Immediate, ct);
                var response = await TryEnqueueRequestAsync<RRF_Request<string>, RRF_Response<string>>(request);
                return response.Content;
            }
            catch
            {
                return default;
            }
        }
        public override async Task<bool> DeleteAsync(string folder, string filename, CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return false;

                var path = $"{folder}/{filename}".TrimStart('/');
                using var clear_folder_ctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                if (!await ClearFolderAsync(path, clear_folder_ctk.Token))
                    return false;

                var file_list = await ListFilesAsync(folder, ct);
                if (!file_list.HasValue)
                    return false;

                if (!file_list.Value.Files.Any(f => f.Name == filename))
                    return true;

                var request = new RRF_Request<RRF_Err>($"rr_delete?name=0:/{path}", HttpMethod.Get, FLUX_RequestPriority.Immediate, ct);
                var response = await TryEnqueueRequestAsync<RRF_Request<RRF_Err>, RRF_Response<RRF_Err>>(request);

                if (!response.Ok)
                    return false;

                var result = response.Content;
                if (!result.HasValue)
                    return false;

                return result.Value.Error == 0;
            }
            catch
            {
                return false;
            }
        }
        public override async Task<bool> RenameAsync(string folder, string old_filename, string new_filename, CancellationToken ct = default)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return false;

                var old_path = $"{folder}/{old_filename}".TrimStart('/');
                var new_path = $"{folder}/{new_filename}".TrimStart('/');

                var request = new RRF_Request<RRF_Err>($"rr_move?old=0:/{old_path}&new=0:/{new_path}&deleteexisting=yes", HttpMethod.Get, FLUX_RequestPriority.Immediate, ct);
                var response = await TryEnqueueRequestAsync(request);

                if (!response.Ok)
                    return false;

                var result = response.Content;
                if (!result.HasValue)
                    return false;

                return result.Value.Error == 0;
            }
            catch
            {
                return false;
            }
        }

        // GCODE
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
        public override GCodeString GetMovementGCode(FLUX_AxisMove axis_move, FLUX_AxisTransform transform)
        {
            var inverse_transform_move = transform.InverseTransformMove(axis_move);
            if (!inverse_transform_move.HasValue)
                return default;

            return GCodeString.Create(
                axis_move.Relative ? new[] { "M120", "G91" } : default,
                $"G1 {inverse_transform_move.Value.GetAxisPosition()} {axis_move.GetFeedrate('F')}",
                axis_move.Relative ? new[] { "G90", "M121" } : default);
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
        public override GCodeString GetResetPositionGCode(FLUX_AxisPosition axis_position, FLUX_AxisTransform transform)
        {
            var inverse_transform_position = transform.InverseTransformPosition(axis_position, false);
            if (!inverse_transform_position.HasValue)
                return default;

            return $"G92 {inverse_transform_position.Value.GetAxisPosition()}";
        }
        public override GCodeString GetExecuteMacroGCode(string folder, string filename)
        {
            return $"M98 P\"0:/{folder}/{filename}\"";
        }
        public override GCodeString GetProbeToolGCode(ArrayIndex position, double temperature)
        {
            throw new NotImplementedException();
        }
        public override GCodeString GetFilamentSensorSettingsGCode(ArrayIndex position, bool enabled)
        {
            var filament_unit = VariableStore.GetArrayUnit(c => c.FILAMENT_BEFORE_GEAR, position);
            if (!filament_unit.HasValue)
                return default;

            return $"M591 D{filament_unit.Value.Index} S{(enabled ? 1 : 0)}";
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
        protected override GCodeString GetSetToolTemperatureGCodeInner(ArrayIndex position, double temperature, bool wait)
        {
            return $"{(wait ? "M109" : "M104")} T{position.GetArrayBaseIndex()} S{temperature}";
        }
        protected override GCodeString GetSetPlateTemperatureGCodeInner(ArrayIndex position, double temperature, bool wait)
        {
            return $"{(wait ? "M190" : "M140")} P{position.GetArrayBaseIndex()} S{temperature}";
        }
        protected override GCodeString GetSetChamberTemperatureGCodeInner(ArrayIndex position, double temperature, bool wait)
        {
            return $"{(wait ? "M191" : "M141")} P{position.GetArrayBaseIndex()} S{temperature}";
        }
        public override GCodeString GetStartPartProgramGCode(string folder, string filename, Optional<FluxJobRecovery> recovery)
        {
            var start_block = recovery.ConvertOr(r => r.BlockNumber, () => new BlockNumber(0, BlockType.None));

            return new[]
            {
                $"M23 0:/{CombinePaths(folder, filename)}",
                $"M26 S{start_block}",
                $"M24"
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


        public override GCodeString GetLogExtrusionGCode(ArrayIndex position, Optional<ExtrusionKey> extr_key, FluxJob job)
        {
            if (!extr_key.HasValue)
                return default;

            var extr_mm_array_unit = GetArrayUnit(c => c.EXTR_MM, position);
            var extr_mm_var = GetVariable(c => c.EXTR_MM, extr_mm_array_unit);
            if (!extr_mm_var.HasValue)
                return default;
            
            var extr_key_array_unit = GetArrayUnit(c => c.EXTR_KEY, position);
            var extr_key_var = GetVariable(c => c.EXTR_KEY, extr_key_array_unit);
            if (!extr_key_var.HasValue)
                return default;

            var extrusion_path = CombinePaths(ExtrusionEventPath, $"{extr_key.Value}");
            
            return GCodeString.Create(
                $"if {extr_key_var} != \"\"",
                $"  var extrusion_pos = 0",
                $"  var extrusion_diff = 0",
                $"  set var.extrusion_pos = move.extruders[{position.GetArrayBaseIndex()}].position",
                $"  set var.extrusion_diff = var.extrusion_pos - {extr_mm_var}",
                $"  if var.extrusion_diff > 0",
                $"    set {extr_mm_var} = var.extrusion_pos",
                $"    echo >>\"0:/{extrusion_path}\" \"{job.JobKey};\"^{{var.extrusion_diff}}");
        }
        public override GCodeString GetWriteCurrentJobGCode(Optional<JobKey> job_key)
        {
            var job_key_str = job_key.ConvertOr(k => k.ToString(), () => "");
            return $"set {VariableStore.CUR_JOB} = \"{job_key_str}\"";
        }
        public override GCodeString GetDeleteFileGCode(string folder, string filename)
        {
            var file_path = CombinePaths(folder, filename);
            return $"M30 \"0:/{file_path}\"";
        }
        public override GCodeString GetLogEventGCode(FluxJob job, FluxEventType event_type)
        {
            var event_path = CombinePaths(JobEventPath, $"{job.MCodeKey};{job.JobKey}");
            var event_type_str = event_type.ToEnumString();

            return $"echo >>\"0:/{event_path}\" \"{event_type_str};\"^{{state.time}}";
        }
        public override GCodeString GetWriteExtrusionMMGCode(ArrayIndex position, double distance_mm)
        {
            var array_unit = GetArrayUnit(c => c.EXTR_MM, position);
            var extr_mm_var = GetVariable(c => c.EXTR_MM, array_unit);
            if (!extr_mm_var.HasValue)
                return default;

            return $"set {extr_mm_var.Value} = {distance_mm}";
        }
        public override GCodeString GetWriteExtrusionKeyGCode(ArrayIndex position, Optional<ExtrusionKey> extr_key)
        {
            var array_unit = GetArrayUnit(c => c.EXTR_KEY, position);
            var extr_key_var = GetVariable(c => c.EXTR_KEY, array_unit);
            if (!extr_key_var.HasValue)
                return default;

            var extr_key_str = extr_key.ConvertOr(k => k.ToString(), () => "");
            return $"set {extr_key_var.Value} = \"{extr_key_str}\"";
        }
        public override GCodeString GetPausePrependGCode(Optional<JobKey> job_key)
        {
            if (!job_key.HasValue)
                return default;
            return $"M471 S\"{CombinePaths(SystemPath, "resurrect.g")}\" T\"{CombinePaths(QueuePath, $"{job_key.Value}.recovery")}\" D1";
        }

        public override GCodeString GetGotoMaintenancePositionGCode()
        {
            return "M98 P\"/macros/goto_maintenance_position\"";
        }
    }
}
