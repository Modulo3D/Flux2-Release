using DynamicData;
using DynamicData.Binding;
using DynamicData.Kernel;
using DynamicData.PLinq;
using Modulo3DStandard;
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
        public ISourceCache<IFluxMCodeStorageViewModel, Guid> AvaiableMCodes { get; }

        [RemoteContent(true)]
        public IObservableCache<IFluxMCodeQueueViewModel, QueuePosition> QueuedMCodes { get; }

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

            var filter_queue = this.WhenAnyValue(v => v.QueuePosition)
                .Select(p =>
                {
                    return (Func<IFluxMCodeQueueViewModel, bool>)filter_queue_func;
                    bool filter_queue_func(IFluxMCodeQueueViewModel queue)
                    {
                        return p > -1 && queue.Job.QueuePosition >= p;
                    }
                });

            bool distinct_queue(Dictionary<QueuePosition, FluxJob> d1, Dictionary<QueuePosition, FluxJob> d2)
            {
                if(d1.Count != d2.Count)
                    return false;
                foreach (var j1 in d1)
                {
                    if (!d2.TryGetValue(j1.Key, out var j2))
                        return false;
                    if (!j1.Value.Equals(j2))
                        return false;
                }
                return true;
            }

            AvaiableMCodes = new SourceCache<IFluxMCodeStorageViewModel, Guid>(f => f.MCodeGuid);
            QueuedMCodes = Flux.ConnectionProvider.ObserveVariable(c => c.QUEUE)
                .ValueOr(() => new Dictionary<QueuePosition, FluxJob>())
                .DistinctUntilChanged(distinct_queue)
                .Select(CreateMCodeQueue)
                .ToObservableChangeSet(kvp => kvp.Job.QueuePosition)
                .Filter(filter_queue)
                .DisposeMany()
                .AsObservableCache();

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

            var mcodes = new Dictionary<Guid, FileInfo>();
            foreach (var file in files)
            {
                var file_name = Path.GetFileNameWithoutExtension(file.Name);
                if (!Guid.TryParse(file_name, out var mcode_guid))
                    continue;

                var file_vm = AvaiableMCodes.Lookup(mcode_guid);
                if (file_vm.HasValue)
                    continue;

                mcodes.Add(mcode_guid, file);
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

                        var mcode_storage = Flux.MCodes.CreateMCodeStorage(mcode.Key);
                        if (mcode_storage.HasValue)
                            AvaiableMCodes.AddOrUpdate(mcode_storage.Value);

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
            catch (Exception ex)
            { 
            }
        }
        private async Task<Optional<Dictionary<Guid, Dictionary<BlockNumber, MCodePartProgram>>>> ReadMCodeStorageAsync()
        {
            var qctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var queue = await Flux.ConnectionProvider.ListFilesAsync(
                c => c.StoragePath,
                qctk.Token);

            if (!queue.HasValue)
                return default;

            return queue.Value.GetPartProgramDictionaryFromStorage();
        }
        public async Task<bool> ClearMCodeStorageAsync()
        {
            var result = await Flux.ShowConfirmDialogAsync("ELIMINARE TUTTI I FILE?", "NON SARA' POSSIBILE RECUPERARE I FILE");
            if (result != ContentDialogResult.Primary)
                return true;

            Directories.Clear(Directories.MCodes);

            var clear_queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.ClearFolderAsync(c => c.StoragePath, true, clear_queue_cts.Token))
                return false;

            AvaiableMCodes.Clear();

            return true;
        }
        public async Task<bool> PrepareMCodeAsync(IFluxMCodeStorageViewModel mcode, bool select, Action<double> report_progress = default)
        {
            report_progress_internal(0);
            var mcode_vm = Optional<IFluxMCodeStorageViewModel>.None;
            try
            {
                IsPreparingFile = true;
                mcode_vm = AvaiableMCodes.Lookup(mcode.MCodeGuid);
                if (!mcode_vm.HasValue)
                {
                    Flux.Messages.LogMessage("Impossibile preparare il lavoro", "MCode non disponibile", MessageLevel.ERROR, 0);
                    return false;
                }

                var analyzer = mcode_vm.Value.Analyzer;
                if (!analyzer.HasValue)
                    return false;

                var recovery = Flux.StatusProvider.PrintingEvaluation.Recovery;

                using (PrepareMCodeCTS = new CancellationTokenSource())
                    if (!await Flux.ConnectionProvider.PreparePartProgramAsync(analyzer.Value, recovery, select, PrepareMCodeCTS.Token, report_progress_internal))
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

        public async Task<bool> DeleteFileAsync(bool hard_delete, IFluxMCodeStorageViewModel file)
        {
            try
            {
                // Invalid file: delete by default
                var mcode_file = Files.AccessFile(Directories.MCodes, $"{file.MCodeGuid}.zip");

                // Confirm dialog
                if (!hard_delete)
                {
                    var name = file.Analyzer.Convert(a => a.MCode.Name);
                    var result = await Flux.ShowConfirmDialogAsync($"ELIMINARE \"{name}\"?", "NON SARA' POSSIBILE RECUPERARE IL FILE");
                    if (result != ContentDialogResult.Primary)
                        return true;
                }

                // Delete the file
                mcode_file.Delete();
                AvaiableMCodes.Remove(file);

                var files = await ReadMCodeStorageAsync();
                if (!files.HasValue)
                    return false;

                if (files.Value.TryGetValue(file.MCodeGuid, out var mcodes))
                {
                    foreach (var mcode in mcodes)
                    { 
                        var delete_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        if (!await Flux.ConnectionProvider.DeleteFileAsync(c => c.StoragePath, $"{mcode}", true, delete_cts.Token))
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
        private Optional<IFluxMCodeStorageViewModel> CreateMCodeStorage(Guid mcode_guid)
        {
            var mcode_storage = new MCodeStorageViewModel(this, mcode_guid);
            mcode_storage.InitializeRemoteView();
            return mcode_storage;
        }

        // QUEUE
        public async Task<bool> ClearQueueAsync()
        {
            var clear_queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.ClearFolderAsync(c => c.QueuePath, true, clear_queue_cts.Token))
                return false;

            var clear_inner_queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.ClearFolderAsync(c => c.InnerQueuePath, true, clear_inner_queue_cts.Token))
                return false;

            return true;
        }
        public async Task<bool> AddToQueueAsync(IFluxMCodeStorageViewModel mcode)
        {
            var queue_pos = await Flux.ConnectionProvider.ReadVariableAsync(c => c.QUEUE_POS);
            if (!queue_pos.HasValue)
            {
                Flux.Messages.LogMessage("Errore durante la selezione del lavoro", "Posizione della coda non trovata", MessageLevel.ERROR, 0);
                return false;
            }

            if (queue_pos.Value < 0)
            {
                if (!await ClearQueueAsync())
                {
                    Flux.Messages.LogMessage("Errore durante la selezione del lavoro", "Impossibile pulire la coda", MessageLevel.ERROR, 0);
                    return false;
                }
            }

            var queue = await ReadMCodeQueueAsync();
            if (!queue.HasValue)
            {
                Flux.Messages.LogMessage("Errore durante la selezione del lavoro", "Impossibile leggere lo stato della coda", MessageLevel.ERROR, 0);
                return false;
            }

            var optional_queue_size = await Flux.ConnectionProvider
                .ReadVariableAsync(c => c.QUEUE_SIZE);

            var queue_size = optional_queue_size
                .ConvertOr(s => s > 0 ? s : (ushort)1, () => (ushort)1);

            var queue_count = queue.Value.Count;
            var last_queue_pos = (QueuePosition)(queue_count - 1);
            if (queue_count >= queue_size)
            {
                var last_mcode = QueuedMCodes.Lookup(last_queue_pos);
                if (last_mcode.HasValue)
                { 
                    if (!await DeleteFromQueueAsync(last_mcode.Value))
                    {
                        Flux.Messages.LogMessage("Errore durante la selezione del lavoro", "Impossibile cancellare il lavoro dalla coda", MessageLevel.ERROR, 0);
                        return false;
                    }
                }
                queue_count--;
                last_queue_pos--;
                if (queue_pos.Value > last_queue_pos)
                    queue_pos = last_queue_pos;
            }

            if (!await PrepareMCodeAsync(mcode, false))
                return false;

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.PutFileAsync(
                c => c.QueuePath,
                $"{last_queue_pos + 1};{Guid.NewGuid()};{mcode.MCodeGuid}",
                cts.Token))
            {
                Flux.Messages.LogMessage("Errore durante la selezione del lavoro", "Impossibile aggiungere alla coda", MessageLevel.ERROR, 0);
                return false;
            }

            if (queue_pos.Value < 0)
            {
                Flux.StatusProvider.StartWithLowMaterials = false;
                if (!await Flux.ConnectionProvider.WriteVariableAsync(c => c.QUEUE_POS, (short)0))
                {
                    Flux.Messages.LogMessage("Errore durante la selezione del lavoro", "Impossibile selezionare il lavoro", MessageLevel.ERROR, 0);
                    return false;
                }
            }

            if (queue_size <= 1)
                Flux.Navigator.NavigateHome();

            return true;
        }
        public async Task<bool> DeleteFromQueueAsync(IFluxMCodeQueueViewModel mcode)
        {
            var queue = await ReadMCodeQueueAsync();
            if (!queue.HasValue)
                return false;

            if (!queue.Value.TryGetValue(mcode.Job.QueuePosition, out var job))
                return false;

            if (!mcode.Job.Equals(job))
                return false;

            if (queue.Value.Keys.Count <= 0)
                return false;

            var queue_count = queue.Value.Count;
            var last_queue_pos = (QueuePosition)(queue_count - 1);
            for (var position = mcode.Job.QueuePosition; position < queue_count; position++)
            {
                var current_position = position;
                var next_position = new QueuePosition((short)(position.Value + 1));

                if (!queue.Value.TryGetValue(current_position, out var current_job))
                    continue;

                var delete_queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                if (!await Flux.ConnectionProvider.DeleteFileAsync(
                    c => c.QueuePath,
                    $"{current_position};{current_job.JobGuid};{current_job.MCodeGuid}",
                    true,
                    delete_queue_cts.Token))
                    return false;

                if (!queue.Value.TryGetValue(next_position, out var next_job))
                    continue;

                var put_queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                if (!await Flux.ConnectionProvider.PutFileAsync(
                   c => c.QueuePath,
                   $"{current_position};{next_job.JobGuid};{next_job.MCodeGuid}",
                   put_queue_cts.Token))
                    return false;
            }

            var queue_pos = await Flux.ConnectionProvider.ReadVariableAsync(c => c.QUEUE_POS);
            if (!queue_pos.HasValue)
                return false;

            if (queue_pos.Value >= last_queue_pos)
            {
                var new_queue_pos = (short)(queue_pos.Value - 1);
                if (!await Flux.ConnectionProvider.WriteVariableAsync(c => c.QUEUE_POS, new_queue_pos))
                    return false;
            }

            return true;
        }
        public async Task<bool> MoveInQueueAsync(IFluxMCodeQueueViewModel mcode, Func<QueuePosition, QueuePosition> move)
        {
            var queue = await ReadMCodeQueueAsync();
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

            var delete_current_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.DeleteFileAsync(
                c => c.QueuePath,
                $"{current_index};{current_job.Value.JobGuid};{current_job.Value.MCodeGuid}",
                true, delete_current_cts.Token))
                return false;

            var delete_other_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.DeleteFileAsync(
                c => c.QueuePath,
                $"{other_index};{other_job.Value.JobGuid};{other_job.Value.MCodeGuid}",
                true, delete_other_cts.Token))
                return false;

            var put_other_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.PutFileAsync(
               c => c.QueuePath,
               $"{current_index};{other_job.Value.JobGuid};{other_job.Value.MCodeGuid}",
               put_other_cts.Token))
                return false;

            var put_current_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.PutFileAsync(
               c => c.QueuePath,
               $"{other_index};{current_job.Value.JobGuid};{current_job.Value.MCodeGuid}",
               put_current_cts.Token))
                return false;

            return true;
        }

        private async Task<Optional<Dictionary<QueuePosition, FluxJob>>> ReadMCodeQueueAsync()
        {
            var qctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var queue = await Flux.ConnectionProvider.ListFilesAsync(
                c => c.QueuePath,
                qctk.Token);

            if (!queue.HasValue)
                return default;

            return queue.Value.GetJobDictionaryFromQueue();
        }
        private IEnumerable<IFluxMCodeQueueViewModel> CreateMCodeQueue(Dictionary<QueuePosition, FluxJob> job_queue)
        {
            foreach (var job in job_queue.Values)
            {
                var mcode_queue = new MCodeQueueViewModel(this, job);
                mcode_queue.InitializeRemoteView();
                yield return mcode_queue;
            }
        }
    }
}
