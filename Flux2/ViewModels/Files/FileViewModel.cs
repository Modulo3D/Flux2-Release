using DynamicData.Kernel;
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

    public class FileEditorDialog : Dialog<FileEditorDialog, DialogResult>
    {
        public FileEditorDialog(IFlux flux, RemoteText title) : base(flux, title)
        {
        }

        protected override OptionalObservable<bool> CanConfirm { get; } = OptionalObservable.Some(true);
        protected override OptionalObservable<bool> CanCancel { get; } = OptionalObservable.Some(true);
        protected override OptionalObservable<bool> CanClose { get; } = default;

        public override Optional<DialogResult> Confirm() => DialogResult.Primary;
        public override Optional<DialogResult> Cancel() => DialogResult.Secondary;
    }

    public abstract class FSViewModel<TFSViewModel> : RemoteControl<TFSViewModel>, IFSViewModel
        where TFSViewModel : FSViewModel<TFSViewModel>
    {

        [RemoteOutput(false)]
        public string FSName { get; }
        public string FSPath { get; }
        public string FSFullPath { get; }
        public FilesViewModel Files { get; }
        public Optional<FolderViewModel> Folder { get; }

        public FSViewModel(FluxViewModel flux, FilesViewModel files, Optional<FolderViewModel> folder, FLUX_File file)
            : base($"{typeof(TFSViewModel).GetRemoteElementClass()};{file.Name}")
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
        public ReactiveCommandBaseRC<Unit, Unit> EditFileCommand { get; }
        [RemoteCommand]
        public ReactiveCommandBaseRC<Unit, Unit> ModifyFileCommand { get; }
        [RemoteCommand]
        public Optional<ReactiveCommandBaseRC<Unit, Unit>> ExecuteFileCommand { get; }

        public FileViewModel(FluxViewModel flux, FilesViewModel files, Optional<FolderViewModel> folder, FLUX_File file) : base(flux, files, folder, file)
        {
            EditFileCommand = ReactiveCommandBaseRC.CreateFromTask(() => files.EditFileAsync(this), this)
                .DisposeWith(Disposables);

            ModifyFileCommand = ReactiveCommandBaseRC.CreateFromTask(() => files.ModifyFSAsync(this), this)
                .DisposeWith(Disposables);

            if (folder.ConvertOr(f => f.FSFullPath.Contains(files.Flux.ConnectionProvider.MacroPath), () => false) ||
                folder.ConvertOr(f => f.FSFullPath.Contains(files.Flux.ConnectionProvider.StoragePath), () => false))
            {
                ExecuteFileCommand = ReactiveCommandBaseRC.CreateFromTask(() => files.ExecuteFileAsync(this), this)
                    .DisposeWith(Disposables);
            }
        }
    }
}
