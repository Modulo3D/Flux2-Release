using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading;

namespace Flux.ViewModels
{
    public class FolderViewModel : FSViewModel<FolderViewModel>
    {
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> OpenFolderCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ModifyFolderCommand { get; }

        public FolderViewModel(FilesViewModel files, Optional<FolderViewModel> folder, FLUX_File file, string path_separator) : base(files, folder, file, path_separator)
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
