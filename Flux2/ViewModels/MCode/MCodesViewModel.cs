using DynamicData;
using DynamicData.Binding;
using DynamicData.Kernel;
using DynamicData.PLinq;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class MCodesViewModel : FluxRoutableNavBarViewModel<MCodesViewModel>, IFluxMCodesViewModel
    {
        private CancellationTokenSource PrepareMCodeCTS { get; set; }

        [RemoteContent(true)]
        public ISourceCache<IFluxMCodeStorageViewModel, MCodeKey> AvaiableMCodes { get; }

        [RemoteContent(true)]
        public IObservableCache<IFluxMCodeQueueViewModel, QueuePosition> QueuedMCodes { get; private set; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> DeleteAllCommand { get; }

        private Optional<DirectoryInfo> _RemovableDrivePath;
        public Optional<DirectoryInfo> RemovableDrivePath
        {
            get => _RemovableDrivePath;
            set => this.RaiseAndSetIfChanged(ref _RemovableDrivePath, value);
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

        private ObservableAsPropertyHelper<QueuePosition> _QueuePosition;
        [RemoteOutput(true)]
        public QueuePosition QueuePosition => _QueuePosition.Value;

        public MCodesViewModel(FluxViewModel flux) : base(flux)
        {
            _QueuePosition = Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE_POS)
                .ValueOr(() => new QueuePosition(-1))
                .ToProperty(this, v => v.QueuePosition);


            AvaiableMCodes = new SourceCache<IFluxMCodeStorageViewModel, MCodeKey>(f => f.MCodeKey);      

            var can_delete_all = AvaiableMCodes.Connect()
                .TrueForAll(mcode => mcode.WhenAnyValue(m => m.CanDelete), d => d);

            DeleteAllCommand = ReactiveCommand.CreateFromTask(async () => { await ClearMCodeStorageAsync(); }, can_delete_all)
                .DisposeWith(Disposables);

            this.WhenAnyValue(v => v.RemovableDrivePath)
                .DistinctUntilChanged(folder => folder.ConvertOr(f => f.FullName, () => ""))
                .Subscribe(drive => ExploreDrive(drive))
                .DisposeWith(Disposables);
        }

        public void Initialize()
        {
            Task.Run(() => ImportMCodesAsync());
          
            var filter_queue = this.WhenAnyValue(v => v.QueuePosition)
                .Select(p =>
                {
                    return (Func<IFluxMCodeQueueViewModel, bool>)filter_queue_func;
                    bool filter_queue_func(IFluxMCodeQueueViewModel queue)
                    {
                        return p > -1 && queue.Job.QueuePosition >= p;
                    }
                });

            QueuedMCodes = Flux.StatusProvider
               .WhenAnyValue(s => s.JobQueue)
               .Select(CreateMCodeQueue)
               .AsObservableChangeSet(kvp => kvp.Job.QueuePosition)
               .Filter(filter_queue)
               .DisposeMany()
               .AsObservableCache();

            Flux.StatusProvider
                .WhenAnyValue(s => s.JobQueue)
                .Where(q => q.HasValue)
                .Throttle(TimeSpan.FromSeconds(1))
                .Subscribe(async q => await Flux.ConnectionProvider.GenerateInnerQueueAsync(q.Value));
        }

        public void FindDrive()
        {
            try
            {
                var externalDevices = DriveInfo.GetDrives();
                foreach (var externalDevice in externalDevices)
                    Console.WriteLine(externalDevice.VolumeLabel);

                var removableDrive = externalDevices.FirstOrOptional(d =>
                    d.VolumeLabel.ToLower().Contains("modulo"));

                RemovableDrivePath = removableDrive.Convert(d => d.RootDirectory);
            }
            catch (Exception ex)
            {
                RemovableDrivePath = default;
                Flux.Messages.LogException(this, ex);
            }
        }
        private void ExploreDrive(Optional<DirectoryInfo> folder)
        {
            var settings = Flux.SettingsProvider.CoreSettings.Local;
            if (folder.HasValue)
            {
                try
                {
                    _ = Task.Run(async () => { await ImportMCodesAsync(folder.Value); });
                }
                catch (Exception ex)
                {
                    Flux.Messages.LogException(this, ex);
                }

                Optional<FileInfo> operator_file = default;
                try
                {
                    operator_file = folder.Value.GetFiles("operator.modulo").FirstOrDefault();
                }
                catch
                {
                }

                if (operator_file.HasValue)
                {
                    try
                    {
                        // Find mcode files
                        using var operator_open = operator_file.Value.OpenRead();
                        OperatorUSB = JsonUtils.Deserialize<OperatorUSB>(operator_open).ValueOrDefault();
                    }
                    catch (Exception ex)
                    {
                        Flux.Messages.LogException(this, ex);
                    }
                }
                else
                {
                    OperatorUSB = default;
                }
            }
            else
            {
                OperatorUSB = default;
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
                    continue;

                mcodes.Add(mcode_key, file);
            }
            if (mcodes.Count == 0)
                return;

            await Flux.ShowProgressDialogAsync("IMPORTO FILE", async (dialog, progress) =>
            {
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

                        AvaiableMCodes.AddOrUpdate(new MCodeStorageViewModel(this, mcode.Key, analyzer.Value));

                        void report_load(double percentage)
                        {
                            progress.Value = percentage;
                        }
                        /*void report_file(string name)
                        {
                            //RxApp.MainThreadScheduler.Schedule(() => tb.Text = name);
                        }*/
                    }
                }
                catch (Exception ex)
                {
                    Flux.Messages.LogException(this, ex);
                }
                finally
                {
                    dialog.ShowAsyncSource.SetResult(ContentDialogResult.None);
                }
            });
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
            var result = await Flux.ShowConfirmDialogAsync("ELIMINARE TUTTI I FILE?", "NON SARA' POSSIBILE RECUPERARE I FILE");
            if (result != ContentDialogResult.Primary)
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
                    Flux.Messages.LogMessage("Impossibile preparare il lavoro", "MCode non disponibile", MessageLevel.ERROR, 0);
                    return false;
                }

                using (PrepareMCodeCTS = new CancellationTokenSource())
                    if (!await Flux.ConnectionProvider.PreparePartProgramAsync(mcode_vm.Value.Analyzer, PrepareMCodeCTS.Token, report_progress_internal))
                        return false;

                if (Flux.ConnectionProvider.HasVariable(c => c.ENABLE_VACUUM))
                {
                    if (!await Flux.ConnectionProvider.WriteVariableAsync(m => m.ENABLE_VACUUM, true))
                    {
                        Flux.Messages.LogMessage("Impossibile preparare il lavoro", "Impossibile attivare la pompa a vuoto", MessageLevel.ERROR, 0);
                        return false;
                    }
                }

                return true;

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
                    var result = await Flux.ShowConfirmDialogAsync($"ELIMINARE \"{name}\"?", "NON SARA' POSSIBILE RECUPERARE IL FILE");
                    if (result != ContentDialogResult.Primary)
                        return true;
                }

                // Delete the file
                mcode_file.Delete();
                AvaiableMCodes.Remove(file);

                var job_queue = await GetJobQueueAsync();
                if (!job_queue.HasValue)
                    return false;

                foreach (var job_partprograms in job_queue.Value)
                {
                    foreach (var mcode_partprogram in job_partprograms.Value.PartPrograms)
                    {
                        if (!mcode_partprogram.MCodeKey.Equals(file.Analyzer.MCode.MCodeKey))
                            continue;

                        using var delete_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        if (!await Flux.ConnectionProvider.DeleteAsync(c => c.StoragePath, $"{mcode_partprogram}", delete_cts.Token))
                            return false;
                    }
                }

                Flux.Messages.LogMessage(FileResponse.FILE_DELETED, file);
                return true;
            }
            catch (Exception ex)
            {
                Flux.Messages.LogMessage(FileResponse.FILE_DELETE_ERROR, file, ex);
                return false;
            }
        }

        // QUEUE
        public async Task<bool> ClearQueueAsync()
        {
            using var clear_queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.DeleteAsync(c => c.QueuePath, "queue", clear_queue_cts.Token))
                return false;

            using var clear_inner_queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.ClearFolderAsync(c => c.InnerQueuePath, clear_inner_queue_cts.Token))
                return false;

            return true;
        }
        public async Task<Optional<JobQueue>> GetJobQueueAsync()
        {
            var connection_provider = Flux.ConnectionProvider;

            var queue_preview = await connection_provider.ReadVariableAsync(c => c.JOB_QUEUE);
            if (!queue_preview.HasValue)
            {
                Flux.Messages.LogMessage("Errore durante la selezione del lavoro", "Impossibile leggere lo stato della coda", MessageLevel.ERROR, 0);
                return default;
            }

            using var queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var queue = await queue_preview.Value.GetJobQueueAsync(connection_provider, queue_cts.Token);
            if (!queue.HasValue)
                return default;

            return queue;
        }
        public async Task<bool> GenerateQueueAsync(IEnumerable<Job> queue)
        {
            var connection_provider = Flux.ConnectionProvider;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var source = queue.Select((j, i) => $"{j with { QueuePosition = i }}").ToList();

            if (!await connection_provider.PutFileAsync(
                c => c.QueuePath, "queue", true, cts.Token, new GCodeString(source)))
                return false;

            var queue_pos = await Flux.ConnectionProvider
               .ReadVariableAsync(c => c.QUEUE_POS)
               .ToOptionalAsync();

            if (!queue_pos.HasValue)
                return false;

            if (queue_pos.Value < 0)
            { 
                queue_pos = (QueuePosition)0;
                if (!await Flux.ConnectionProvider.WriteVariableAsync(c => c.QUEUE_POS, queue_pos.Value))
                    return false;
            }

            var last_queue_pos = Math.Max(0, queue.Count() - 1);
            if (queue_pos.Value > last_queue_pos)
            {
                queue_pos = (QueuePosition)(queue_pos.Value - 1);
                if (!await Flux.ConnectionProvider.WriteVariableAsync(c => c.QUEUE_POS, queue_pos.Value))
                    return false;
            }

            if (queue_pos.Value == -1)
                await Flux.ConnectionProvider.CancelPrintAsync(true);

            return true;
        }
        public async Task<bool> AddToQueueAsync(IFluxMCodeStorageViewModel mcode)
        {
            Flux.StatusProvider.StartWithLowMaterials = false;

            var connection_provider = Flux.ConnectionProvider;
            var queue_size = connection_provider.VariableStoreBase.HasPrintUnloader ? 99 : 1;

            if (!await PrepareMCodeAsync(mcode))
                return false;

            var queue = await GetJobQueueAsync();
            if (!queue.HasValue)
                return false;

            var jobs = queue.Value.Select(j => j.Value.Job)
                .SkipLast((queue.Value.Count + 1) - queue_size)
                .Append(Job.CreateNew(mcode.MCodeKey, 0));

            if (!await GenerateQueueAsync(jobs))
                return false;

            if (queue_size <= 1)
                Flux.Navigator.NavigateHome();
            
            return true;
        }
        public async Task<bool> DeleteFromQueueAsync(IFluxMCodeQueueViewModel mcode)
        {
            var queue = await GetJobQueueAsync();
            if (!queue.HasValue)
                return false;

            if (!queue.Value.TryGetValue(mcode.Job.QueuePosition, out var job))
                return false;

            if (!mcode.Job.Equals(job.Job))
                return false;

            if (queue.Value.Keys.Count <= 0)
                return false;

            var jobs = queue.Value.Values.Select(j => j.Job)
                .Where(j => j.QueuePosition != mcode.Job.QueuePosition);

            if (!await GenerateQueueAsync(jobs))
                return false;

            return true;
        }
        public async Task<bool> MoveInQueueAsync(IFluxMCodeQueueViewModel mcode, Func<QueuePosition, QueuePosition> move)
        {
            var queue = await GetJobQueueAsync();
            if (!queue.HasValue)
                return false;

            var current_index = mcode.Job.QueuePosition;
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

            var new_current_job = current_job.Value.Job with { QueuePosition = other_index };
            var new_other_job = other_job.Value.Job with { QueuePosition = current_index };

            var jobs = queue.Value.Values.Select(j => j.Job)
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
                yield return new MCodeQueueViewModel(this, job.Job);
        }
    }
}
