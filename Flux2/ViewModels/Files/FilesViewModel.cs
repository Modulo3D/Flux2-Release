using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
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
        public ReactiveCommand<Unit, Unit> ExitFolderCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> CreateFSCommand { get; }

        public Subject<Unit> UpdateFolder { get; }

        public FilesViewModel(FluxViewModel flux) : base(flux)
        {
            UpdateFolder = new Subject<Unit>();

            ExitFolderCommand = ReactiveCommand.Create(() =>
            {
                Folder = Folder.Convert(f => f.Folder);
            });

            CreateFSCommand = ReactiveCommand.CreateFromTask(CreateFSAsync);

            var folderContent = Observable.CombineLatest(
                UpdateFolder.StartWith(Unit.Default).ObserveOn(RxApp.MainThreadScheduler),
                flux.ConnectionProvider.WhenAnyValue(c => c.IsConnecting),
                this.WhenAnyValue(v => v.Folder),
                (_, _, folder) => folder)
                .Select(f => Observable.FromAsync(() => ListFilesAsync(f)))
                .Merge(1)
                .Select(GetFolderContent)
                .ToObservableChangeSet(f => f.FSName);

            FileSystem = folderContent
                .DisposeMany()
                .AsObservableCache()
                .DisposeWith(Disposables);
        }

        public async Task CreateFSAsync()
        {
            var options = ComboOption.Create("type", "Tipo di documento", Enum.GetValues<FLUX_FileType>(), b => (uint)b);
            var name = new TextBox("name", "Nome del documento", "", false);

            var result = await Flux.ShowSelectionAsync(
                "Cosa vuoi creare?",
                Observable.Return(true),
                options, 
                name);

            if (result != ContentDialogResult.Primary)
                return;

            if (!options.Value.HasValue)
                return;

            using var fs_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var path = Folder.ConvertOr(f => f.FSFullPath, () => Flux.ConnectionProvider.PathSeparator);
            switch (options.Value.Value)
            {
                case FLUX_FileType.File:
                    await Flux.ConnectionProvider.PutFileAsync(path, name.Value, true, fs_cts.Token);
                    break;
                case FLUX_FileType.Directory:
                    await Flux.ConnectionProvider.CreateFolderAsync(path, name.Value, fs_cts.Token);
                    break;
            }

            UpdateFolder.OnNext(Unit.Default);
        }

        public async Task ModifyFSAsync(IFSViewModel fs)
        {
            var modify_option = ComboOption.Create("operations", "Operazione:", Enum.GetValues<FLUX_FileModify>(), f => (uint)f);
            
            var modify_dialog_result = await Flux.ShowSelectionAsync(
                "Tipo di operazione",
                Observable.Return(true),
                modify_option);
            
            if (modify_dialog_result != ContentDialogResult.Primary)
                return;

            if (!modify_option.Value.HasValue)
                return;


            using var fs_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            switch (modify_option.Value.Value)
            {
                case FLUX_FileModify.Rename:
                    var rename_option = new TextBox("rename", "Nome del file", fs.FSName);

                    var rename_dialog_result = await Flux.ShowSelectionAsync(
                        "Rinominare il file?",
                        Observable.Return(true),
                        rename_option);

                    if (rename_dialog_result != ContentDialogResult.Primary)
                        return;

                    var rename_result = await Flux.ConnectionProvider.RenameFileAsync(fs.FSPath, fs.FSName, rename_option.Value, true, fs_cts.Token);
                    if (rename_result)
                        UpdateFolder.OnNext(Unit.Default);
                    break;

                case FLUX_FileModify.Delete:
                    var delete_dialog_result = await Flux.ShowConfirmDialogAsync("Cancellare il file?", $"Il file {fs.FSPath}/{fs.FSName} non potrà essere recuperato");
                    if (delete_dialog_result != ContentDialogResult.Primary)
                        return;

                    var delete_result = await Flux.ConnectionProvider.DeleteFileAsync(fs.FSPath, fs.FSName, true, fs_cts.Token);
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
            using var download_cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var file_source = await Flux.ConnectionProvider.DownloadFileAsync(file.FSPath, file.FSName, download_cts.Token);
            if (!file_source.HasValue)
                return;

            var textbox = new TextBox("source", file.FSName, file_source.Value, multiline: true);
            var combo = ComboOption.Create("file_access", "Accesso al file",
                Enum.GetValues<FLUX_FileAccess>(), f => (uint)f,
                (uint)FLUX_FileAccess.ReadOnly, f =>
                {
                    var textbox_value = textbox.RemoteInputs.Lookup("value");
                    if (!textbox_value.HasValue)
                        return;

                    var enabled = f.HasValue && ((FLUX_FileAccess)f.Value) == FLUX_FileAccess.ReadWrite;
                    textbox_value.Value.IsEnabled = enabled;
                });

            textbox.InitializeRemoteView();
            combo.InitializeRemoteView();

            var can_confirm = combo.Items.SelectedValueChanged
                .Select(f => f.HasValue && f.Value == FLUX_FileAccess.ReadWrite);

            var result = await Flux.ShowSelectionAsync(
                "Modifica File",
                Observable.Return(true),
                can_confirm,
                textbox,
                combo); 
            
            if (result != ContentDialogResult.Primary)
                return;

            var source = read_source(file_source.Value)
                .ToOptional();

            using var upload_cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await Flux.ConnectionProvider.PutFileAsync(file.FSPath, file.FSName, true, upload_cts.Token, source);

            IEnumerable<string> read_source(string source)
            {
                string line;
                using var reader = new StringReader(textbox.Value);
                while ((line = reader.ReadLine()) != null)
                    yield return line;
            }
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
                        var file_vm = new FileViewModel(this, file_list.folder, file, Flux.ConnectionProvider.PathSeparator);
                        yield return file_vm;
                        break;
                    case FLUX_FileType.Directory:
                        var folder_vm = new FolderViewModel(this, file_list.folder, file, Flux.ConnectionProvider.PathSeparator);
                        yield return folder_vm;
                        break;
                }
            }
        }
    }
}
