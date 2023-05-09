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

        public FolderViewModel(FluxViewModel flux, FilesViewModel files, Optional<FolderViewModel> folder, FLUX_File file) : base(flux, files, folder, file)
        {
            OpenFolderCommand = ReactiveCommandRC.Create(() =>
            {
                Files.Folder = this;
            }, this)
            .DisposeWith(Disposables);

            ModifyFolderCommand = ReactiveCommandRC.CreateFromTask(() => files.ModifyFSAsync(this), this)
                .DisposeWith(Disposables);
        }
    }
}
