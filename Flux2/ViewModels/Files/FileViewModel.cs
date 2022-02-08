using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public interface IFSViewModel : IRemoteControl
    {
        string FSName { get; }
        string FSPath { get; }
        FilesViewModel Files { get; }
        Optional<FolderViewModel> Folder { get; }
    }

    public abstract class FSViewModel<TViewModel> : RemoteControl<TViewModel>, IFSViewModel
        where TViewModel : FSViewModel<TViewModel>
    {

        [RemoteOutput(false)]
        public string FSName { get; }
        public string FSPath { get; }
        public string FSFullPath { get; }
        public FilesViewModel Files { get; }
        public Optional<FolderViewModel> Folder { get; }

        public FSViewModel(FilesViewModel files, Optional<FolderViewModel> folder, FLUX_File file) : base($"{typeof(TViewModel).GetRemoteControlName()}??{file.Name}")
        {
            Files = files;
            Folder = folder;
            FSName = file.Name;
            FSPath = Folder.ConvertOr(f => $"{f.FSPath}/{f.FSName}".TrimStart('/'), () => "");
            FSFullPath = Folder.ConvertOr(f => $"{f.FSPath}/{f.FSName}/{FSName}".TrimStart('/'), () => FSName);
        }

        public override string ToString() => $"{FSPath}/{FSName}";
    }

    public enum FLUX_FileAccess : uint
    {
        ReadOnly = 0,
        ReadWrite = 1,
    }

    public class FileViewModel : FSViewModel<FileViewModel>
    {
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> EditFileCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> DeleteFileCommand { get; }

        public FileViewModel(FilesViewModel files, Optional<FolderViewModel> folder, FLUX_File file) : base(files, folder, file)
        {
            EditFileCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var download_cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var file = await Files.Flux.ConnectionProvider.DownloadFileAsync(FSPath, FSName, download_cts.Token);
                if (!file.HasValue)
                    return;

                var textbox = new TextBox("source", FSName, file.Value, multiline: true);
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

                var result = await Files.Flux.ShowSelectionAsync("Modifica File", true, combo, textbox);
                if (result != ContentDialogResult.Primary)
                    return;

                var source = read_source(file.Value)
                    .ToOptional();

                var upload_cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await Files.Flux.ConnectionProvider.PutFileAsync(FSPath, FSName, upload_cts.Token, source);

                IEnumerable<string> read_source(string source)
                {
                    string line;
                    using var reader = new StringReader(textbox.Value);
                    while ((line = reader.ReadLine()) != null)
                        yield return line;
                }
            })
            .DisposeWith(Disposables);

            DeleteFileCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var dialog_result = await Files.Flux.ShowConfirmDialogAsync("Cancellare il file?", $"Il file {FSPath}/{FSName} non potrà essere recuperato");
                if (dialog_result != ContentDialogResult.Primary)
                    return;

                var delete_file_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var delete_result = await Files.Flux.ConnectionProvider.DeleteFileAsync(FSPath, FSName, true, delete_file_cts.Token);

                if (delete_result)
                    Files.UpdateFolder.OnNext(Unit.Default);
            })
            .DisposeWith(Disposables);
        }
    }
}
