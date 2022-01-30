using DynamicData;
using DynamicData.Kernel;
using Flux.ViewModels;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
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

            CreateFSCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var options = ComboOption.Create("type", "Tipo di documento", Enum.GetValues<FLUX_FileType>(), b => (uint)b);
                var name = new TextBox("name", "Nome del documento", "", false);
                var result = await Flux.ShowSelectionAsync("Cosa vuoi creare?", true, options, name);
                if (result != ContentDialogResult.Primary)
                    return;

                if (!options.Value.HasValue)
                    return;

                var path = Folder.ConvertOr(f => f.FSFullPath, () => "");
                switch (options.Value.Value)
                {
                    case FLUX_FileType.File:
                        var file_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await Flux.ConnectionProvider.PutFileAsync(path, name.Value, file_cts.Token);
                        break;
                    case FLUX_FileType.Directory:
                        var folder_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await Flux.ConnectionProvider.CreateFolderAsync(path, name.Value, folder_cts.Token);
                        break;
                }

                UpdateFolder.OnNext(Unit.Default);
            });

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

        private async Task<(Optional<FolderViewModel>, Optional<FLUX_FileList>)> ListFilesAsync(Optional<FolderViewModel> folder)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var path = folder.ConvertOr(f => f.FSFullPath, () => "");
            var file_list = await Flux.ConnectionProvider.ListFilesAsync(path, cts.Token);
            return (folder, file_list);
        }

        private IEnumerable<IFSViewModel> GetFolderContent((Optional<FolderViewModel> folder, Optional<FLUX_FileList> files) file_list)
        {
            if (!file_list.files.HasValue)
                yield break;
            foreach (var file in file_list.files.Value.Files)
            {
                switch (file.Type)
                {
                    case FLUX_FileType.File:
                        var file_vm = new FileViewModel(this, file_list.folder, file);
                        yield return file_vm;
                        break;
                    case FLUX_FileType.Directory:
                        var folder_vm = new FolderViewModel(this, file_list.folder, file);
                        yield return folder_vm;
                        break;
                }
            }
        }
    }
}
