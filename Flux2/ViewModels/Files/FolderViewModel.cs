using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Disposables;

namespace Flux.ViewModels
{
    public class FolderViewModel : FSViewModel<FolderViewModel>
    {
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> OpenFolderCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ModifyFolderCommand { get; }

        public FolderViewModel(FilesViewModel files, Optional<FolderViewModel> folder, FLUX_File file) : base(files, folder, file)
        {
            OpenFolderCommand = ReactiveCommand.Create(() =>
            {
                Files.Folder = this;
            })
            .DisposeWith(Disposables);

            ModifyFolderCommand = ReactiveCommand.CreateFromTask(() => files.ModifyFSAsync(this))
                .DisposeWith(Disposables);
        }
    }
}
