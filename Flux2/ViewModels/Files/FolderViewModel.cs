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
        public ReactiveCommandBaseRC<Unit, Unit> OpenFolderCommand { get; }
        [RemoteCommand]
        public ReactiveCommandBaseRC<Unit, Unit> ModifyFolderCommand { get; }

        public FolderViewModel(FluxViewModel flux, FilesViewModel files, Optional<FolderViewModel> folder, FLUX_File file) : base(flux, files, folder, file)
        {
            OpenFolderCommand = ReactiveCommandBaseRC.Create(() =>
            {
                Files.Folder = this;
            }, this)
            .DisposeWith(Disposables);

            ModifyFolderCommand = ReactiveCommandBaseRC.CreateFromTask(() => files.ModifyFSAsync(this), this)
                .DisposeWith(Disposables);
        }
    }
}
