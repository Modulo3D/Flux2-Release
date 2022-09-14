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
        public string PathSeparator { get; }
        public Optional<FolderViewModel> Folder { get; }

        public FSViewModel(FilesViewModel files, Optional<FolderViewModel> folder, FLUX_File file, string path_separator) : base($"{typeof(TViewModel).GetRemoteControlName()}??{file.Name}")
        {
            Files = files;
            Folder = folder;
            FSName = file.Name;
            PathSeparator = path_separator;
            FSPath = Folder.ConvertOr(f => $"{f.FSPath}{path_separator}{f.FSName}".TrimStart(path_separator.ToCharArray()), () => "");
            FSFullPath = Folder.ConvertOr(f => $"{f.FSPath}{path_separator}{f.FSName}{path_separator}{FSName}".TrimStart(path_separator.AsArray()), () => FSName);
        }

        public override string ToString() => $"{FSPath}{PathSeparator}{FSName}";
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
        [RemoteCommand]
        public Optional<ReactiveCommand<Unit, Unit>> ExecuteFileCommand { get; }

        public FileViewModel(FilesViewModel files, Optional<FolderViewModel> folder, FLUX_File file, string path_separator) : base(files, folder, file, path_separator)
        {
            EditFileCommand = ReactiveCommand.CreateFromTask(() => files.EditFileAsync(this))
                .DisposeWith(Disposables);

            ModifyFileCommand = ReactiveCommand.CreateFromTask(() => files.ModifyFSAsync(this))
                .DisposeWith(Disposables);

            if (folder.ConvertOr(f => f.FSFullPath.Contains(files.Flux.ConnectionProvider.MacroPath), () => false) ||
                folder.ConvertOr(f => f.FSFullPath.Contains(files.Flux.ConnectionProvider.StoragePath), () => false))
            { 
                ExecuteFileCommand = ReactiveCommand.CreateFromTask(() => files.ExecuteFileAsync(this))
                    .DisposeWith(Disposables);
            }
        }
    }
}
