using DynamicData;
using DynamicData.Binding;
using DynamicData.Kernel;
using DynamicData.PLinq;
using DynamicData.Aggregation;
using Microsoft.FSharp.Data.UnitSystems.SI.UnitNames;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class MCodesViewModel : FluxRoutableNavBarViewModel<MCodesViewModel>, IFluxMCodesViewModel
    {
        public const int MCodePageSize = 5;

        private readonly ObservableAsPropertyHelper<int> _MCodePageCount;
        public int MCodePageCount => _MCodePageCount.Value;


        private readonly ObservableAsPropertyHelper<string> _Page;
        [RemoteOutput(true)]
        public string Page => _Page.Value;

        private SemaphoreSlim PrepareMCodeSemaphore { get; set; }
        private CancellationTokenSource PrepareMCodeCTS { get; set; }

        public ISourceCache<IFluxMCodeStorageViewModel, MCodeKey> AvaiableMCodes { get; private set; }

        private PageRequest _MCodePage = new PageRequest(1, MCodePageSize);
        public PageRequest MCodePage
        {
            get => _MCodePage;
            set => this.RaiseAndSetIfChanged(ref _MCodePage, value);
        }

        [RemoteContent(true, nameof(IFluxMCodeStorageViewModel.FileNumber))]
        public IObservableList<IFluxMCodeStorageViewModel> PagedMCodes { get; private set; }


        [RemoteContent(true, nameof(IFluxMCodeQueueViewModel.QueuePosition))]
        public IObservableCache<IFluxMCodeQueueViewModel, QueuePosition> QueuedMCodes { get; private set; }

        [RemoteCommand]
        public ReactiveCommandBaseRC<Unit, Unit> DeleteAllCommand { get; }
        [RemoteCommand]
        public ReactiveCommandBaseRC<Unit, Unit> NextPageCommand { get; }
        [RemoteCommand]
        public ReactiveCommandBaseRC<Unit, Unit> PreviousPageCommand { get; }

        public ReactiveCommandBaseRC<IFluxMCodeStorageViewModel, bool> AddToQueueCommand { get; }


        private Optional<DirectoryInfo[]> _RemovableDrivePaths;
        public Optional<DirectoryInfo[]> RemovableDrivePaths
        {
            get => _RemovableDrivePaths;
            set => this.RaiseAndSetIfChanged(ref _RemovableDrivePaths, value);
        }

        private Optional<OperatorUSB> _OperatorUSB;
        public Optional<OperatorUSB> OperatorUSB
        {
            get => _OperatorUSB;
            set => this.RaiseAndSetIfChanged(ref _OperatorUSB, value);
        }

        private bool _IsPreparingFile = false;
        public bool IsPreparingFile
        {
            get => _IsPreparingFile;
            set => this.RaiseAndSetIfChanged(ref _IsPreparingFile, value);
        }

        private readonly ObservableAsPropertyHelper<QueuePosition> _QueuePosition;
        [RemoteOutput(true)]
        public QueuePosition QueuePosition => _QueuePosition.Value;

        public MCodesViewModel(FluxViewModel flux) : base(flux)
        {
            PrepareMCodeSemaphore = new SemaphoreSlim(1, 1);

            _QueuePosition = Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE_POS)
                .ValueOr(() => new QueuePosition(-1))
                .ToPropertyRC(this, v => v.QueuePosition);

            SourceCacheRC.Create(this, v => v.AvaiableMCodes, f => f.MCodeKey);

            _MCodePageCount = AvaiableMCodes.Connect().Count()
                .Select(count => (int)Math.Ceiling(Math.Max(1, count / (float)MCodePageSize)))
                .ToPropertyRC(this, v => v.MCodePageCount);

            _Page = Observable.CombineLatest(
                this.WhenAnyValue(v => v.MCodePage),
                this.WhenAnyValue(v => v.MCodePageCount),
                (page_request, page_count) =>
                $"{page_request.Page}/{page_count}")
                .ToPropertyRC(this, v => v.Page);

            var page_request = this.WhenAnyValue(v => v.MCodePage);
            var mcode_sort = SortExpressionComparer<IFluxMCodeStorageViewModel>
                .Descending(v => v.Analyzer.MCode.Created);

            PagedMCodes = AvaiableMCodes.Connect()
                .RemoveKey()
                .Sort(mcode_sort)
                .Page(page_request)
                .AsObservableListRC(this);

            var can_delete_all = AvaiableMCodes.Connect()
                .TrueForAll(mcode => mcode.WhenAnyValue(m => m.CanDelete), d => d);

            var is_selecting_file = this
                .WhenAnyValue(f => f.IsPreparingFile)
                .StartWith(false);

            var process_status = Flux.ConnectionProvider
                .ObserveVariable(m => m.PROCESS_STATUS)
                .StartWithDefault();

            var printing_evaluation = Flux.StatusProvider
                .WhenAnyValue(e => e.PrintingEvaluation)
                .StartWith(new PrintingEvaluation());

            var can_select = Observable.CombineLatest(
                is_selecting_file,
                process_status,
                printing_evaluation,
                CanSelectMCode)
                .StartWith(true);

            DeleteAllCommand = ReactiveCommandBaseRC.CreateFromTask(async () => { await ClearMCodeStorageAsync(); }, this, can_delete_all);
            AddToQueueCommand = ReactiveCommandBaseRC.CreateFromTask((Func<IFluxMCodeStorageViewModel, Task<bool>>)AddToQueueAsync, this, can_select);
            this.WhenAnyValue(v => v.RemovableDrivePaths)
                .SubscribeRC(async drives => await ExploreDrivesAsync(drives), this);

            var can_next_page_command = Observable.CombineLatest(
                this.WhenAnyValue(v => v.MCodePage),
                this.WhenAnyValue(v => v.MCodePageCount),
                (page_request, page_count) => page_request.Page < page_count);

            var can_previous_page_command = this.WhenAnyValue(v => v.MCodePage).Select(page_request => page_request.Page > 1);

            NextPageCommand = ReactiveCommandBaseRC.Create(() => { MCodePage = new PageRequest(MCodePage.Page + 1, MCodePageSize); }, this, can_next_page_command);
            PreviousPageCommand = ReactiveCommandBaseRC.Create(() => { MCodePage = new PageRequest(MCodePage.Page - 1, MCodePageSize); }, this, can_previous_page_command);
        }

        private bool CanSelectMCode(bool selecting, Optional<FLUX_ProcessStatus> status, PrintingEvaluation printing_eval)
        {
            var connection_provider = Flux.ConnectionProvider;
            if (selecting)
                return false;
            if (connection_provider.VariableStoreBase.HasPrintUnloader)
                return true;
            if (!printing_eval.MCode.HasValue)
                return true;
            if (!status.ConvertOr(s => s == FLUX_ProcessStatus.IDLE, () => false))
                return false;
            return true;
        }

        public void Initialize()
        {
            Task.Run(() => ImportMCodesAsync());

            var filter_queue = this.WhenAnyValue(v => v.QueuePosition)
                .Select(p => (Func<IFluxMCodeQueueViewModel, bool>)(q => p > -1 && q.FluxJob.QueuePosition >= p));

            QueuedMCodes = Flux.StatusProvider
               .WhenAnyValue(s => s.JobQueue)
               .Select(CreateMCodeQueue)
               .AsObservableChangeSet(kvp => kvp.FluxJob.QueuePosition)
               .Filter(filter_queue)
               .DisposeMany()
               .AsObservableCacheRC(this);
        }

        public void FindDrive()
        {
            try
            {
                var media = new DirectoryInfo("/media");
                RemovableDrivePaths = media.ToOptional(m => m.Exists)
                    .Convert(m => m.GetDirectories())
                    .Convert(m => m.ToArray());
            }
            catch (Exception ex)
            {
                RemovableDrivePaths = default;
                // Flux.Messages.LogException(this, ex);
            }
        }
        private async Task ExploreDrivesAsync(Optional<DirectoryInfo[]> folders)
        {
            try
            {
                if (!folders.HasValue)
                    return;
                
                if (folders.Value.Length == 0)
                    return;

                var settings = Flux.SettingsProvider.CoreSettings.Local;
                foreach (var folder in folders.Value)
                {
                    try
                    {
                        _ = Task.Run(async () => { await ImportMCodesAsync(folder); });
                    }
                    catch (Exception ex)
                    {
                        // Flux.Messages.LogException(this, ex);
                    }

                    var parse_ip_file_result = await parse_ip_file_async(folder);
                    if (parse_ip_file_result)
                        return;

                    //if (parse_operator_file(folder))
                    //    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            bool parse_operator_file(DirectoryInfo folder)
            {
                var operator_file = folder.GetFiles("operator.modulo").FirstOrOptional(_ => true);
                if (!operator_file.HasValue)
                {
                    OperatorUSB = default;
                    return false;
                }

                try
                {
                    // Find mcode files
                    using var operator_open = operator_file.Value.OpenRead();
                    OperatorUSB = JsonUtils.Deserialize<OperatorUSB>(operator_open);
                    return true;
                }
                catch (Exception ex)
                {
                    // Flux.Messages.LogException(this, ex);
                    OperatorUSB = default;
                    return false;
                }
            }

            async Task<bool> parse_ip_file_async(DirectoryInfo folder)
            {
                var ipaddress_file = folder.GetFiles("ipaddress.modulo").FirstOrOptional(_ => true);
                if (!ipaddress_file.HasValue)
                    return false;

                try
                {
                    // Find mcode files
                    using var operator_open = ipaddress_file.Value.OpenRead();
                    var ipaddress_config = JsonUtils.Deserialize<OperatorEthernetConfig>(operator_open);
                    if (!ipaddress_config.HasValue)
                        return false;

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        var dhcpcd_conf = new FileInfo("/etc/dhcpcd.conf");
                        if (dhcpcd_conf.Exists)
                            dhcpcd_conf.Delete();

                        using var dhcpcd_conf_writer = dhcpcd_conf.CreateText();
                        dhcpcd_conf_writer.WriteLine("hostname");
                        dhcpcd_conf_writer.WriteLine("clientid");
                        dhcpcd_conf_writer.WriteLine("persistent");
                        dhcpcd_conf_writer.WriteLine("option rapid_commit");
                        dhcpcd_conf_writer.WriteLine("option domain_name_servers, domain_name, domain_search, host_name");
                        dhcpcd_conf_writer.WriteLine("option classless_static_routes");
                        dhcpcd_conf_writer.WriteLine("option interface_mtu");
                        dhcpcd_conf_writer.WriteLine("require dhcp_server_identifier");
                        dhcpcd_conf_writer.WriteLine("slaac private");

                        dhcpcd_conf_writer.WriteLine("");
                        dhcpcd_conf_writer.WriteLine("interface eth0");
                        dhcpcd_conf_writer.WriteLine($"static ip_address={ipaddress_config.Value.Eth0Interface}");

                        dhcpcd_conf_writer.WriteLine("");
                        dhcpcd_conf_writer.WriteLine("interface eth1");
                        dhcpcd_conf_writer.WriteLine($"static ip_address={ipaddress_config.Value.Eth1Interface}");
                        dhcpcd_conf_writer.WriteLine($"static routers={ipaddress_config.Value.Eth1Router}");
                        dhcpcd_conf_writer.WriteLine($"static domain_name_servers={ipaddress_config.Value.Eth1DNS}");

                        await ProcessUtils.RunLinuxCommandsAsync("sudo reboot");
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
        }


        // IMPORTING
        public async Task ImportMCodesAsync(Optional<DirectoryInfo> import_directory = default)
        {
            var work_directory = import_directory.ValueOr(() => Directories.MCodes);
            var settings = Flux.SettingsProvider.UserSettings.Local;
            var mcode_directory = Directories.MCodes;
            var files = work_directory.GetFiles();

            if (files.Length == 0)
                return;

            var mcodes = new Dictionary<MCodeKey, FileInfo>();
            foreach (var file in files)
            {
                var file_name = Path.GetFileNameWithoutExtension(file.Name);
                if (!MCodeKey.TryParse(file_name, out var mcode_key))
                    continue;

                var file_vm = AvaiableMCodes.Lookup(mcode_key);
                if (file_vm.HasValue)
                    File.Delete(file_name);

                mcodes.Add(mcode_key, file);
            }
            if (mcodes.Count == 0)
                return;
            try
            {
                foreach (var mcode in mcodes)
                {
                    //report_file(mcode.Key.ToString());

                    var file_name = Path.GetFileName(mcode.Value.FullName);
                    var file_path = Path.Combine(mcode_directory.FullName, file_name);
                    if (work_directory.FullName != mcode_directory.FullName)
                    {
                        if (File.Exists(file_path))
                            File.Delete(file_path);

                        using (var base_stream = mcode.Value.OpenRead())
                        using (var read_stream = new ProgressStream(base_stream, report_load))
                        using (var write_stream = File.Create(file_path))
                            await read_stream.CopyToAsync(write_stream);

                        if (settings.DeleteFromUSB.ValueOr(() => false))
                            mcode.Value.Delete();
                    }

                    var analyzer = MCodeAnalyzer.CreateFromZip(mcode.Key, Directories.MCodes);
                    if (!analyzer.HasValue)
                        continue;

                    AvaiableMCodes.AddOrUpdate(new MCodeStorageViewModel(Flux, this, mcode.Key, analyzer.Value));

                    void report_load(double percentage)
                    {
                       // progress.Value = percentage;
                    }
                    void report_file(string name)
                    {
                        //RxApp.MainThreadScheduler.Schedule(() => tb.Text = name);
                    }
                }
            }
            catch (Exception ex)
            {
                // Flux.Messages.LogException(this, ex);
            }
            finally
            {
                //dialog.ShowAsyncSource.SetResult(DialogResult.None);
            }
        }

        // STORAGE
        public void CancelPrepareMCode()
        {
            try
            {
                PrepareMCodeCTS?.Cancel();
            }
            catch (Exception)
            {
            }
        }
        public async Task<bool> ClearMCodeStorageAsync()
        {

            var result = await Flux.ShowDialogAsync(f => new ConfirmDialog(f, new RemoteText("clearMCodes", true), new RemoteText()));
            if (result.result != DialogResult.Primary)
                return true;

            Directories.Clear(Directories.MCodes);

            using var clear_queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.ClearFolderAsync(c => c.StoragePath, clear_queue_cts.Token))
                return false;

            AvaiableMCodes.Clear();

            return true;
        }
        public async Task<bool> PrepareMCodeAsync(IFluxMCodeStorageViewModel mcode, Action<double> report_progress = default)
        {
            report_progress_internal(0);
            var mcode_vm = Optional<IFluxMCodeStorageViewModel>.None;
            try
            {

                IsPreparingFile = true;
                mcode_vm = AvaiableMCodes.Lookup(mcode.MCodeKey);
                if (!mcode_vm.HasValue)
                {
                    // Flux.Messages.LogMessage("Impossibile preparare il lavoro", "MCode non disponibile", MessageLevel.ERROR, 0);
                    return false;
                }

                using (PrepareMCodeCTS = new CancellationTokenSource())
                    if (!await Flux.ConnectionProvider.PreparePartProgramAsync(mcode_vm.Value.Analyzer, PrepareMCodeCTS.Token, report_progress_internal))
                        return false;

                if (Flux.ConnectionProvider.HasVariable(c => c.ENABLE_VACUUM))
                {
                    if (!await Flux.ConnectionProvider.WriteVariableAsync(m => m.ENABLE_VACUUM, true))
                    {
                        // Flux.Messages.LogMessage("Impossibile preparare il lavoro", "Impossibile attivare la pompa a vuoto", MessageLevel.ERROR, 0);
                        return false;
                    }
                }

                return true;

            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
            finally
            {
                IsPreparingFile = false;
                report_progress_internal(0);
            }

            void report_progress_internal(double percentage)
            {
                percentage = Math.Ceiling(percentage);
                mcode.UploadPercentage = percentage;
                report_progress?.Invoke(percentage);
            }
        }

        public async Task<bool> DeleteAsync(bool hard_delete, IFluxMCodeStorageViewModel file)
        {
            try
            {
                // Invalid file: delete by default
                var mcode_file = Files.AccessFile(Directories.MCodes, $"{file.MCodeKey}.zip");

                // Confirm dialog
                if (!hard_delete)
                {
                    var name = file.Analyzer.MCode.Name;
                    var result = await Flux.ShowDialogAsync(f => new ConfirmDialog(f, new RemoteText($"deleteMCode", true), new RemoteText(name, false)));
                    if (result.result != DialogResult.Primary)
                        return true;
                }

                // Delete the file
                mcode_file.Delete();
                AvaiableMCodes.Remove(file);

                using var delete_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                if (!await Flux.ConnectionProvider.DeleteAsync(c => c.StoragePath, $"{file.MCodeKey}", delete_cts.Token))
                    return false;

                // Flux.Messages.LogMessage(FileResponse.FILE_DELETED, file);
                return true;
            }
            catch (Exception ex)
            {
                // Flux.Messages.LogMessage(FileResponse.FILE_DELETE_ERROR, file, ex);
                return false;
            }
        }

        // QUEUE
        public async Task<bool> ClearQueueAsync()
        {
            using var clear_inner_queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.ClearFolderAsync(c => c.QueuePath, clear_inner_queue_cts.Token))
            {
                Console.WriteLine("Impossibile pulire la coda");
                return false;
            }

            return true;
        }
        public async Task<Optional<JobQueue>> GetJobQueueAsync()
        {
            var connection_provider = Flux.ConnectionProvider;

            var queue_pos = await connection_provider.ReadVariableAsync(c => c.QUEUE_POS);
            if (!queue_pos.HasValue)
                return default;

            var queue_preview = await connection_provider.ReadVariableAsync(c => c.QUEUE);
            if (!queue_preview.HasValue)
            {
                Console.WriteLine("Impossibile leggere lo stato della coda");
                return default;
            }

            using var queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await queue_preview.Value.GetJobQueueAsync(connection_provider, queue_pos.Value, queue_cts.Token);
        }
        public async Task<bool> GenerateQueueAsync(IEnumerable<FluxJob> queue)
        {
            queue = queue
                .OrderBy(job => job.QueuePosition)
                .Select((j, i) => j with { QueuePosition = i });

            var connection_provider = Flux.ConnectionProvider;

            var queue_pos = await Flux.ConnectionProvider.ReadVariableAsync(c => c.QUEUE_POS);
            if (!queue_pos.HasValue)
            {
                Console.WriteLine("Impossibile leggere la posizione della coda");
                return false;
            }

            var queue_source_gcode_lines = queue.Select(job => job.ToString());
            var queue_source_gcode = new GCodeString(queue_source_gcode_lines);
            using var put_queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await connection_provider.PutFileAsync(
                c => c.QueuePath, "queue.temp", true, put_queue_cts.Token, queue_source_gcode))
            {
                Console.WriteLine("Impossibile caricare il file \"queue.temp\"");
                return false;
            }

            var get_queue_file_cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            var queue_file_str = await connection_provider.GetFileAsync(c => c.QueuePath, "queue.temp", get_queue_file_cts.Token);
            if(!queue_file_str.HasValue)
            {
                Console.WriteLine("Impossibile leggere il file \"queue.temp\"");
                return false;
            }

            if(!queue_source_gcode.VerifySHA256(queue_file_str.Value))
            {
                Console.WriteLine("Hash non valido per il file \"queue.temp\"");
                return false;
            }

            var job_queue = new JobQueue(queue_pos.Value);
            foreach(var flux_job in queue)
                job_queue.Add(flux_job.QueuePosition, flux_job);

            var inner_queue_gcodes = job_queue
                .Select(j => connection_provider.ConnectionBase.GenerateInnerQueueGCodes(j.Value));

            if(!await generate_temp_inner_queue(
                ("end.g", g => g.End),
                ("spin.g", g => g.Spin),
                ("pause.g", g => g.Pause),
                ("begin.g", g => g.Begin),
                ("start.g", g => g.Start),
                ("resume.g", g => g.Resume),
                ("cancel.g", g => g.Cancel)))
            {
                Console.WriteLine("Impossibile creare la coda interna");
                return false;
            }

            var queue_temp_path = connection_provider.CombinePaths(connection_provider.QueuePath, "queue.temp");
            var queue_path = connection_provider.CombinePaths(connection_provider.QueuePath, "queue");
            var rename_inner_queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            if (!await connection_provider.RenameAsync(queue_temp_path, queue_path, rename_inner_queue_cts.Token))
            {
                Console.WriteLine("Impossibile rinominare il file \"queue.temp\"");
                return false;
            }

            if (!await rename_temp_inner_queue(
                  "end.g",
                  "spin.g",
                  "pause.g",
                  "begin.g",
                  "start.g",
                  "resume.g",
                  "cancel.g"))
            {
                Console.WriteLine("Impossibile rinominare la coda interna");
                return false;
            }

            if (queue_pos.Value < 0)
            {
                queue_pos = (QueuePosition)0;
                if (!await Flux.ConnectionProvider.WriteVariableAsync(c => c.QUEUE_POS, queue_pos.Value))
                {
                    Console.WriteLine("Impossibile scrivere la posizione della coda");
                    return false;
                }
            }

            var last_queue_pos = Math.Max(0, queue.Count() - 1);
            if (queue_pos.Value > last_queue_pos)
            {
                queue_pos = (QueuePosition)(queue_pos.Value - 1);
                if (!await Flux.ConnectionProvider.WriteVariableAsync(c => c.QUEUE_POS, queue_pos.Value))
                {
                    Console.WriteLine("Impossibile scrivere la posizione della coda");
                    return false;
                }
            }

            if (queue_pos.Value == -1)
                await Flux.ConnectionProvider.CancelPrintAsync(true);

            return true;


            async Task<bool> generate_temp_inner_queue(params (string filename, Func<InnerQueueGCodes, GCodeString> get_gcode)[] macros)
            {
                foreach (var (inner_queue_filename, get_inner_queue_gcode) in macros)
                {
                    var inner_queue_gcode_lines = inner_queue_gcodes.Select((g, i) =>
                    {
                        var (start_compare, end_compare) = connection_provider.GetCompareQueuePosGCode(i);
                        return GCodeString.Create(
                            start_compare,
                            get_inner_queue_gcode(g).Pad(4),
                            end_compare);
                    });

                    var inner_queue_gcode_string = new GCodeString(inner_queue_gcode_lines);

                    var temp_filename = Path.ChangeExtension(inner_queue_filename, ".temp");
                    using var put_cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    if (!await connection_provider.PutFileAsync(c => c.InnerQueuePath, temp_filename, true, put_cts.Token, inner_queue_gcode_string))
                    {
                        Console.WriteLine("Impossibile caricare il file coda interna");
                        return false;
                    }

                    using var get_inner_queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    var inner_queue_file_str = await connection_provider.GetFileAsync(c => c.InnerQueuePath, temp_filename, get_inner_queue_cts.Token);
                    if (!inner_queue_file_str.HasValue)
                    {
                        Console.WriteLine("Impossibile leggere il file coda interna");
                        return false;
                    }

                    if (!inner_queue_gcode_string.VerifySHA256(inner_queue_file_str.Value))
                    {
                        Console.WriteLine($"Hash non valido per il file \"{inner_queue_filename}\"");
                        return false;
                    }
                }
                return true;
            }

            async Task<bool> rename_temp_inner_queue(params string[] macros)
            {

                foreach (var inner_queue_filename in macros)
                {
                    var inner_queue_temp_filename = Path.ChangeExtension(inner_queue_filename, ".temp");
                    var inner_queue_temp_path = connection_provider.CombinePaths(connection_provider.InnerQueuePath, inner_queue_temp_filename);
                    var inner_queue_path = connection_provider.CombinePaths(connection_provider.InnerQueuePath, inner_queue_filename);

                    var rename_inner_queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    if (!await connection_provider.RenameAsync(inner_queue_temp_path, inner_queue_path, rename_inner_queue_cts.Token))
                    {
                        Console.WriteLine("Impossibile rinominare il file coda interna");
                        return false;
                    }
                }
                return true;
            }
        }
        public async Task<bool> AddToQueueAsync(IFluxMCodeStorageViewModel mcode)
        {
            using var prepare_mcode_cts = new CancellationTokenSource(TimeSpan.FromSeconds(0.5));
            await PrepareMCodeSemaphore.WaitAsync(prepare_mcode_cts.Token);

            try
            {
                Flux.StatusProvider.StartWithLowNozzles = false;
                Flux.StatusProvider.StartWithLowMaterials = false;

                var connection_provider = Flux.ConnectionProvider;
                var queue_size = connection_provider.VariableStoreBase.HasPrintUnloader ? 99 : 1;

                if (!await PrepareMCodeAsync(mcode))
                    return false;

                var queue = await GetJobQueueAsync();
                if (!queue.HasValue)
                    return false;

                var jobs = queue.Value.Select(j => j.Value)
                    .SkipLast((queue.Value.Count + 1) - queue_size)
                    .Append(FluxJob.CreateNew(mcode.MCodeKey, queue_size));

                if (!await GenerateQueueAsync(jobs))
                    return false;

                if (queue_size <= 1)
                    Flux.Navigator.NavigateHome();

                Console.WriteLine("Lavoro aggiunto alla coda!");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
            finally 
            {
                PrepareMCodeSemaphore.Release();
            }
        }
        public async Task<bool> DeleteFromQueueAsync(IFluxMCodeQueueViewModel mcode)
        {
            var queue = await GetJobQueueAsync();
            if (!queue.HasValue)
                return false;

            if (!queue.Value.TryGetValue(mcode.FluxJob.QueuePosition, out var job))
                return false;

            if (!mcode.FluxJob.Equals(job))
                return false;

            if (queue.Value.Keys.Count <= 0)
                return false;

            var jobs = queue.Value.Values
                .Where(j => j.QueuePosition != mcode.FluxJob.QueuePosition);

            if (!jobs.Any())
                await Flux.ConnectionProvider.CancelPrintAsync(true);

            if (!await GenerateQueueAsync(jobs))
                return false;

            return true;
        }
        public async Task<bool> MoveInQueueAsync(IFluxMCodeQueueViewModel mcode, Func<QueuePosition, QueuePosition> move)
        {
            var queue = await GetJobQueueAsync();
            if (!queue.HasValue)
                return false;

            var current_index = mcode.FluxJob.QueuePosition;
            var other_index = move(current_index);

            if (other_index < 0 || other_index > queue.Value.Count)
                return false;

            if (current_index == other_index)
                return true;

            var current_job = queue.Value.Lookup(current_index);
            if (!current_job.HasValue)
                return false;

            var other_job = queue.Value.Lookup(other_index);
            if (!other_job.HasValue)
                return false;

            var new_current_job = current_job.Value with { QueuePosition = other_index };
            var new_other_job = other_job.Value with { QueuePosition = current_index };

            var jobs = queue.Value.Values
                .Select(j =>
                {
                    if (j.QueuePosition == current_index)
                        return new_current_job;
                    if (j.QueuePosition == other_index)
                        return new_other_job;
                    return j;
                });

            if (!await GenerateQueueAsync(jobs))
                return false;

            return true;
        }
        private IEnumerable<IFluxMCodeQueueViewModel> CreateMCodeQueue(Optional<JobQueue> job_queue)
        {
            if (!job_queue.HasValue)
                yield break;
            foreach (var job in job_queue.Value.Values)
                yield return new MCodeQueueViewModel(Flux, this, job);
        }
    }
}
