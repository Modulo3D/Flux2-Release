﻿using DynamicData;
using DynamicData.Binding;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData.PLinq;
using System.Threading;

namespace Flux.ViewModels
{
    public class MCodesViewModel : FluxRoutableNavBarViewModel<MCodesViewModel>, IFluxMCodesViewModel
    {
        [RemoteContent(true)]
        public ISourceCache<IFluxMCodeStorageViewModel, Guid> AvaiableMCodes { get; }

        [RemoteContent(true)]
        public IObservableCache<IFluxMCodeQueueViewModel, ushort> QueuedMCodes { get; }

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

        private ObservableAsPropertyHelper<short> _QueuePosition;
        [RemoteOutput(true)]
        public short QueuePosition => _QueuePosition.Value;

        public MCodesViewModel(FluxViewModel flux) : base(flux)
        {
            _QueuePosition = Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE_POS)
                .ValueOr(() => (short)-1)
                .ToProperty(this, v => v.QueuePosition);

            var filter_queue = this.WhenAnyValue(v => v.QueuePosition)
                .Select(p =>
                {
                    return (Func<(IFluxMCodeQueueViewModel queue_mcode, ushort queue_pos), bool>)filter_queue_func;
                    bool filter_queue_func((IFluxMCodeQueueViewModel queue_mcode, ushort queue_pos) queue)
                    {
                        return queue.queue_pos >= p;
                    }
                });

            bool distinct_queue(Dictionary<ushort, Guid> d1, Dictionary<ushort, Guid> d2)
            {
                return string.Join(";", d1.Select(kvp => $"{kvp.Key}:{kvp.Value}")) ==
                    string.Join(";", d2.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
            }

            AvaiableMCodes = new SourceCache<IFluxMCodeStorageViewModel, Guid>(f => f.Analyzer.MCode.MCodeGuid);
            QueuedMCodes = Flux.ConnectionProvider.ObserveVariable(c => c.QUEUE)
                .ValueOr(() => new Dictionary<ushort, Guid>())
                .DistinctUntilChanged(distinct_queue)
                .Select(CreateMCodeQueue)
                .ToObservableChangeSet(kvp => kvp.queue_pos)
                .Filter(filter_queue)
                .Transform(kvp => kvp.queue_mcode)
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
                catch(Exception ex)
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
                    dialog.Hide();
                }
            });
        }

        // STORAGE TEST
        private async Task<Optional<Dictionary<Guid, MCodePartProgram>>> ReadMCodeStorageAsync()
        {
            var qctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var queue = await Flux.ConnectionProvider.ListFilesAsync(
                FLUX_FolderType.GCodes,
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
            if (!await Flux.ConnectionProvider.ClearFolderAsync(FLUX_FolderType.GCodes, clear_queue_cts.Token))
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
                mcode_vm = AvaiableMCodes.Lookup(mcode.Analyzer.MCode.MCodeGuid);
                if (!mcode_vm.HasValue)
                    return false;

                var evaluation = Flux.StatusProvider.PrintingEvaluation;
                var recovery = evaluation.SelectedRecovery;
                if (!recovery.HasValue)
                    recovery = evaluation.AvaiableRecovery;

                var put_ctk = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                var result = await Flux.ConnectionProvider.PreparePartProgramAsync(
                    mcode_vm.Value.Analyzer, recovery, select, put_ctk.Token,
                    report_progress_internal);

                if (!result)
                    return false;

                if(Flux.ConnectionProvider.VariableStore.HasVariable(c => c.ENABLE_VACUUM))
                    await Flux.ConnectionProvider.WriteVariableAsync(m => m.ENABLE_VACUUM, true);
                
                return true;

            }
            finally
            {
                IsPreparingFile = false;
                report_progress_internal(0);
            }
            
            void report_progress_internal(double percentage)
            {
                mcode.LoadPercentage = percentage;
                report_progress?.Invoke(percentage);
            }
        }

        public async Task<bool> DeleteFileAsync(bool hard_delete, IFluxMCodeStorageViewModel file)
        {
            try
            {
                // Invalid file: delete by default
                var analyzer = file.Analyzer;
                var mcode_guid = analyzer.MCode.MCodeGuid;
                var mcode_file = Files.AccessFile(Directories.MCodes, $"{mcode_guid}.zip");

                // Confirm dialog
                if (!hard_delete)
                {
                    var name = analyzer.MCode.Name;
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

                if (files.Value.TryGetValue(file.Analyzer.MCode.MCodeGuid, out var mcode))
                {
                    var delete_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    if (!await Flux.ConnectionProvider.DeleteFileAsync(FLUX_FolderType.GCodes, $"{mcode}", delete_cts.Token))
                        return false;
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
            var analyzer = MCodeAnalyzer.CreateFromZip(mcode_guid, Directories.MCodes);
            if (!analyzer.HasValue)
                return default;

            var mcode_storage = new MCodeStorageViewModel(this, analyzer.Value);
            mcode_storage.InitializeRemoteView();

            return mcode_storage;
        }

        // QUEUE
        public async Task<bool> ClearQueueAsync()
        {
            var clear_queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.ClearFolderAsync(FLUX_FolderType.Queue, clear_queue_cts.Token))
                return false;

            var clear_inner_queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.ClearFolderAsync(FLUX_FolderType.InnerQueue, clear_inner_queue_cts.Token))
                return false;

            return true;
        }
        public async Task<bool> AddToQueueAsync(IFluxMCodeStorageViewModel mcode)
        {
            if (!Flux.StatusProvider.PrintingEvaluation.SelectedPartProgram.HasValue)
            { 
                Flux.StatusProvider.StartWithLowMaterials = false;
                if (!await Flux.ConnectionProvider.WriteVariableAsync(c => c.QUEUE_POS, (short)0))
                    return false;
            }

            var queue = await ReadMCodeQueueAsync();
            if (!queue.HasValue)
                return false;

            var current_index = queue.Value.Keys.Count > 0 ? (short)queue.Value.Keys.Max() : (short)-1;
            if (current_index > -1)
            {
                var queue_size = await Flux.ConnectionProvider.ReadVariableAsync(c => c.QUEUE_SIZE);
                if (queue.Value.Count >= queue_size.ValueOr(() => (ushort)1))
                {
                    if (queue.Value.Count < 1)
                        return false;
                    var last_mcode = QueuedMCodes.Lookup((ushort)current_index);
                    if (!last_mcode.HasValue)
                        return false;
                    if (!await DeleteFromQueueAsync(last_mcode.Value))
                        return false;
                    current_index--;
                }
            }

            if (!await PrepareMCodeAsync(mcode, false))
                return false;

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.PutFileAsync(
                FLUX_FolderType.Queue,
                $"{current_index + 1};{mcode.Analyzer.MCode.MCodeGuid}", 
                cts.Token))
                return false;

            return true;
        }
        public async Task<bool> DeleteFromQueueAsync(IFluxMCodeQueueViewModel mcode)
        {
            var queue = await ReadMCodeQueueAsync();
            if (!queue.HasValue)
                return false;

            if (!queue.Value.TryGetValue(mcode.QueueIndex, out var mcode_guid))
                return false;

            if (mcode.MCodeGuid != mcode_guid)
                return false;

            if (queue.Value.Keys.Count <= 0)
                return false;

            var queue_count = queue.Value.Keys.Max() + 1;
            for (ushort position = mcode.QueueIndex; position < queue_count; position++)
            {
                var current_position = position;
                var next_position = (ushort)(position + 1);

                if (!queue.Value.TryGetValue(current_position, out var current_queue_guid))
                    continue;

                var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                if (!await Flux.ConnectionProvider.DeleteFileAsync(
                    FLUX_FolderType.Queue,
                    $"{current_position};{current_queue_guid}",
                    cts1.Token))
                    return false;

                if (!queue.Value.TryGetValue(next_position, out var next_queue_guid))
                    continue;

                var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                if (!await Flux.ConnectionProvider.PutFileAsync(
                   FLUX_FolderType.Queue,
                   $"{current_position};{next_queue_guid}",
                   cts2.Token))
                    return false;
            }

            return true;
        }
        public async Task<bool> MoveInQueueAsync(IFluxMCodeQueueViewModel mcode, Func<ushort, short> move)
        {
            var queue = await ReadMCodeQueueAsync();
            if (!queue.HasValue)
                return false;

            var current_index = mcode.QueueIndex;
            var other_index = move(current_index);

            if (other_index < 0 || other_index > queue.Value.Count)
                return false;

            if (current_index == other_index)
                return true;

            var current_guid = queue.Value.Lookup(current_index);
            if (!current_guid.HasValue)
                return false;

            var other_guid = queue.Value.Lookup((ushort)other_index);
            if (!other_guid.HasValue)
                return false;

            var delete_current_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.DeleteFileAsync(
                FLUX_FolderType.Queue,
                $"{current_index};{current_guid}",
                delete_current_cts.Token))
                return false;

            var delete_other_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.DeleteFileAsync(
                FLUX_FolderType.Queue,
                $"{other_index};{other_guid}",
                delete_other_cts.Token))
                return false;

            var put_other_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.PutFileAsync(
               FLUX_FolderType.Queue,
               $"{current_index};{other_guid}",
               put_other_cts.Token))
                return false;

            var put_current_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await Flux.ConnectionProvider.PutFileAsync(
               FLUX_FolderType.Queue,
               $"{other_index};{current_guid}",
               put_current_cts.Token))
                return false;

            return true;
        }

        private async Task<Optional<Dictionary<ushort, Guid>>> ReadMCodeQueueAsync()
        {
            var qctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var queue = await Flux.ConnectionProvider.ListFilesAsync(
                FLUX_FolderType.Queue,
                qctk.Token);

            if (!queue.HasValue)
                return default;

            return queue.Value.GetGuidDictionaryFromQueue();
        }
        private IEnumerable<(IFluxMCodeQueueViewModel queue_mcode, ushort queue_pos)> CreateMCodeQueue(Dictionary<ushort, Guid> queue) 
        {
            foreach (var kvp in queue)
            {
                var mcode_queue = new MCodeQueueViewModel(this, kvp.Key, kvp.Value);
                mcode_queue.InitializeRemoteView();
                yield return (mcode_queue, kvp.Key);
            }
        }
    }
}
