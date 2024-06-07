using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class FilesViewModel : FluxRoutableViewModel<FilesViewModel>
    {
        [RemoteContent(true)]
        public IObservableCache<IFSViewModel, string> FileSystem { get; }


        private Optional<FolderViewModel> _Folder;
        [RemoteOutput(true, typeof(ToStringConverter))]
        public Optional<FolderViewModel> Folder
        {
            get => _Folder;
            set => this.RaiseAndSetIfChanged(ref _Folder, value);
        }

        [RemoteCommand]
        public ReactiveCommandBaseRC<Unit, Unit> ExitFolderCommand { get; }
        [RemoteCommand]
        public ReactiveCommandBaseRC<Unit, Unit> CreateFSCommand { get; }

        public Subject<Unit> UpdateFolder { get; }

        public FilesViewModel(FluxViewModel flux) : base(flux)
        {
            UpdateFolder = new Subject<Unit>();

            ExitFolderCommand = ReactiveCommandBaseRC.Create(() =>
            {
                Folder = Folder.Convert(f => f.Folder);
            }, this);

            CreateFSCommand = ReactiveCommandBaseRC.CreateFromTask(CreateFSAsync, this);

            var folderContent = Observable.CombineLatest(
                UpdateFolder.StartWith(Unit.Default).ObserveOn(RxApp.MainThreadScheduler),
                flux.ConnectionProvider.WhenAnyValue(c => c.IsConnecting),
                this.WhenAnyValue(v => v.Folder),
                (_, _, folder) => folder)
                .SelectAsync(ListFilesAsync)
                .Select(GetFolderContent)
                .AsObservableChangeSet(f => f.FSName);

            FileSystem = folderContent
                .DisposeMany()
                .AsObservableCacheRC(this);
        }

        public async Task CreateFSAsync()
        {
            var result = await Flux.ShowDialogAsync(f => new CreateFSDialog(f, (FLUX_FileType.Directory, "")));
            if (result.result != DialogResult.Primary || !result.data.HasValue)
                return;

            var path = Folder.ConvertOr(f => f.FSFullPath, () => Flux.ConnectionProvider.CombinePaths("", ""));
            
            using var fs_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            switch (result.data.Value.file_type)
            {
                case FLUX_FileType.File:
                    await Flux.ConnectionProvider.PutFileAsync(path, result.data.Value.file_name, true, fs_cts.Token);
                    break;
                case FLUX_FileType.Directory:
                    await Flux.ConnectionProvider.CreateFolderAsync(path, result.data.Value.file_name, fs_cts.Token);
                    break;
            }

            UpdateFolder.OnNext(Unit.Default);
        }

        public async Task ModifyFSAsync(IFSViewModel fs)
        {
            var result = await Flux.ShowDialogAsync(f => new ModifyFSDialog(f, (FLUX_FileModify.Delete, fs.FSName)));
            if (result.result != DialogResult.Primary || !result.data.HasValue)
                return;

            using var fs_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            switch (result.data.Value.file_modify)
            {
                case FLUX_FileModify.Rename:
                    var old_path = Flux.ConnectionProvider.CombinePaths(fs.FSPath, fs.FSName);
                    var new_path = Flux.ConnectionProvider.CombinePaths(fs.FSPath, result.data.Value.file_name);
                    var rename_result = await Flux.ConnectionProvider.RenameAsync(old_path, new_path, fs_cts.Token);
                    if (rename_result)
                        UpdateFolder.OnNext(Unit.Default);
                    break;

                case FLUX_FileModify.Delete:
                    var delete_dialog_result = await Flux.ShowDialogAsync(f => new ConfirmDialog(f, new RemoteText("fsDelete", true), new RemoteText(fs.Name, false)));
                    if (delete_dialog_result.result != DialogResult.Primary)
                        return;

                    var delete_result = await Flux.ConnectionProvider.DeleteAsync(fs.FSPath, fs.FSName, fs_cts.Token);
                    if (delete_result)
                        UpdateFolder.OnNext(Unit.Default);
                    break;
            }
        }

        public async Task ExecuteFileAsync(IFSViewModel fs)
        {
            using var put_macro_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await Flux.ConnectionProvider.ExecuteParamacroAsync(c => c.GetExecuteMacroGCode(fs.FSPath, fs.FSName), put_macro_cts.Token);
        }

        public async Task EditFileAsync(FileViewModel file)
        {
            //using var download_cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            //var file_source = await Flux.ConnectionProvider.GetFileAsync(file.FSPath, file.FSName, download_cts.Token);
            //if (!file_source.HasValue)
            //    return;

            //var result = await Flux.ShowDialogAsync(f => new FileEditorDialog(f, new RemoteText(file.FSName, false)));

            //if (result != DialogResult.Primary)
            //    return;

            //var source = read_source(file_source.Value)
            //    .ToOptional();

            //using var upload_cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            //await Flux.ConnectionProvider.PutFileAsync(file.FSPath, file.FSName, true, upload_cts.Token, source);

            //IEnumerable<string> read_source(string source)
            //{
            //    string line;
            //    using var reader = new StringReader(textbox.Value);
            //    while ((line = reader.ReadLine()) != null)
            //        yield return line;
            //}
        }

        public async Task<(Optional<FolderViewModel>, Optional<FLUX_FileList>)> ListFilesAsync(Optional<FolderViewModel> folder)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var path = folder.ConvertOr(f => f.FSFullPath, () => Flux.ConnectionProvider.RootPath);
            var file_list = await Flux.ConnectionProvider.ListFilesAsync(path, cts.Token);
            return (folder, file_list);
        }

        public IEnumerable<IFSViewModel> GetFolderContent((Optional<FolderViewModel> folder, Optional<FLUX_FileList> files) file_list)
        {
            if (!file_list.files.HasValue)
                yield break;
            foreach (var file in file_list.files.Value.Files)
            {
                switch (file.Type)
                {
                    case FLUX_FileType.File:
                        var file_vm = new FileViewModel(Flux, this, file_list.folder, file);
                        yield return file_vm;
                        break;
                    case FLUX_FileType.Directory:
                        var folder_vm = new FolderViewModel(Flux, this, file_list.folder, file);
                        yield return folder_vm;
                        break;
                }
            }
        }
    }
    public class CreateFSDialog : InputDialog<CreateFSDialog, (FLUX_FileType file_type, string file_name)>
    {
        [RemoteInput()]
        public SelectableCache<FLUX_FileType, string> FileType { get; }

        private string _FSName;
        [RemoteInput()]
        public string FSName 
        {
            get => _FSName;
            set => this.RaiseAndSetIfChanged(ref _FSName, value);
        }

        public CreateFSDialog(IFlux flux, (FLUX_FileType file_type, string file_name) startValue) : base(flux, startValue, new RemoteText("title", true))
        {
            var file_type = Enum.GetValues<FLUX_FileType>().AsObservableChangeSet(f => Enum.GetName(f));
            FileType = SelectableCache.Create(file_type, Enum.GetName(startValue.file_type));
            FSName = startValue.file_name;
        }

        public override Optional<(FLUX_FileType file_type, string file_name)> Confirm() => (FileType.SelectedValue.ValueOrDefault(), FSName);
    }
    public class ModifyFSDialog : InputDialog<ModifyFSDialog, (FLUX_FileModify file_modify, string file_name)>
    {
        [RemoteInput()]
        public SelectableCache<FLUX_FileModify, string> FileModify { get; }

        private string _FSName;
        [RemoteInput()]
        public string FSName
        {
            get => _FSName;
            set => this.RaiseAndSetIfChanged(ref _FSName, value);
        }

        public ModifyFSDialog(IFlux flux, (FLUX_FileModify file_modify, string file_name) startValue) : base(flux, startValue, new RemoteText(startValue.file_name, false))
        {
            var file_modify = Enum.GetValues<FLUX_FileModify>().AsObservableChangeSet(f => Enum.GetName(f));
            FileModify = SelectableCache.Create(file_modify, Enum.GetName(startValue.file_modify));
            FileModify.SelectedValueChanged
                .Where(m => m.HasValue)
                .Where(m => m.Value == FLUX_FileModify.Delete)
                .SubscribeRC(m => FSName = startValue.file_name, this);
        }

        public override Optional<(FLUX_FileModify file_modify, string file_name)> Confirm() => (FileModify.SelectedValue.ValueOrDefault(), FSName);
    }
}
