﻿using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Disposables;

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
            FSPath = Folder.ConvertOr(f => files.Flux.ConnectionProvider.CombinePaths(f.FSPath, f.FSName).TrimStart(), () => "");
            FSFullPath = Folder.ConvertOr(f => files.Flux.ConnectionProvider.CombinePaths(f.FSPath, f.FSName, FSName).TrimStart(), () => FSName);
        }

        public override string ToString() => Files.Flux.ConnectionProvider.CombinePaths(FSPath, FSName);
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

        public FileViewModel(FilesViewModel files, Optional<FolderViewModel> folder, FLUX_File file) : base(files, folder, file)
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
