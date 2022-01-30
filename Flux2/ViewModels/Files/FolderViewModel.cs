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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class FolderViewModel : FSViewModel<FolderViewModel>
    {
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> OpenFolderCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> DeleteFolderCommand { get; }

        public FolderViewModel(FilesViewModel files, Optional<FolderViewModel> folder, FLUX_File file) : base(files, folder, file)
        {
            OpenFolderCommand = ReactiveCommand.Create(() => 
            { 
                Files.Folder = this;
            })
            .DisposeWith(Disposables);

            DeleteFolderCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var dialog_result = await Files.Flux.ShowConfirmDialogAsync("Cancellare la cartella?", $"La cartella {FSPath}/{FSName} non potrà essere recuperata");
                if (dialog_result != ContentDialogResult.Primary)
                    return;

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var delete_result = await Files.Flux.ConnectionProvider.DeleteFileAsync(FSPath, FSName, cts.Token);
                
                if(delete_result)
                    Files.UpdateFolder.OnNext(Unit.Default);
            })
            .DisposeWith(Disposables);
        }
    }
}
