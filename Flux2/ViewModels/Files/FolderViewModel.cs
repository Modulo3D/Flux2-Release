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
        public ReactiveCommandBaseRC OpenFolderCommand { get; }
        [RemoteCommand]
        public ReactiveCommandBaseRC ModifyFolderCommand { get; }

        public FolderViewModel(FilesViewModel files, Optional<FolderViewModel> folder, FLUX_File file) : base(files, folder, file)
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
