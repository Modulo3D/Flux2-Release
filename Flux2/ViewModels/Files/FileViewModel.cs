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

    public enum FLUX_FileModify : uint
    {
        Rename = 0,
        Delete = 1,
    }

    public class FileViewModel : FSViewModel<FileViewModel>
    {
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> EditFileCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ModifyFileCommand { get; }

        public FileViewModel(FilesViewModel files, Optional<FolderViewModel> folder, FLUX_File file) : base(files, folder, file)
        {
            EditFileCommand = ReactiveCommand.CreateFromTask(() => files.EditFileAsync(this))
                .DisposeWith(Disposables);

            ModifyFileCommand = ReactiveCommand.CreateFromTask(() => files.ModifyFSAsync(this))
                .DisposeWith(Disposables);
        }
    }
}
