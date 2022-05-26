using DynamicData;
using DynamicData.Kernel;
using Microsoft.Extensions.Hosting;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public static class Files
    {
        public static FileInfo AccessFile(DirectoryInfo parentDirectory, string name) => new FileInfo(Path.Combine(parentDirectory.FullName, name));
        public static FileInfo AccessResource(string name) => new FileInfo(name);

        public static FileInfo Stats => AccessFile(Directories.Storage, "Stats.json");
        public static FileInfo CoreSettings => AccessFile(Directories.Storage, "CoreSettings.json");
        public static FileInfo UserSettings => AccessFile(Directories.Storage, "UserSettings.json");
        public static FileInfo Status => AccessFile(Directories.Storage, "Status.json");
        public static FileInfo Database => AccessFile(Directories.Storage, "Database.db");
        public static FileInfo MCode => AccessFile(Directories.Storage, "MCode.mcode");
        public static FileInfo JobStorage => AccessFile(Directories.Storage, "JobStorage.json");
        public static FileInfo HTMLScreen => AccessResource("HTML/screen.html");

    }

    public static class Directories
    {
        private static DirectoryInfo _Local;
        public static DirectoryInfo Local
        {
            get
            {
                if (_Local == default)
                {
                    var folder = Directory.GetCurrentDirectory();
                    _Local = new DirectoryInfo(folder);
                }
                return _Local;
            }
        }

        private static DirectoryInfo _Parent;
        public static DirectoryInfo Parent
        {
            get
            {
                if (_Parent == default)
                    _Parent = Local.Parent;
                return _Parent;
            }
        }

        public static DirectoryInfo AccessDirectory(DirectoryInfo parent, string name)
        {
            var directory = new DirectoryInfo(Path.Combine(parent.FullName, name));
            if (!directory.Exists)
                return parent.CreateSubdirectory(name);
            return directory;
        }

        public static DirectoryInfo Flux => AccessDirectory(Parent, "Flux");
        public static DirectoryInfo MCodes => AccessDirectory(Flux, "MCodes");
        public static DirectoryInfo Storage => AccessDirectory(Flux, "Storage");
        public static DirectoryInfo NFCBackup => AccessDirectory(Flux, "NFCBackup");

        public static void Clear(DirectoryInfo dir)
        {
            if (!dir.Exists)
                return;

            foreach (FileInfo file in dir.GetFiles())
                file.Delete();
            foreach (DirectoryInfo sub_dir in dir.GetDirectories())
                sub_dir.Delete(true);
        }
    }

    [RemoteControl()]
    public class FluxViewModel : RemoteControl<FluxViewModel>, IFlux, IHostedService
    {
        [RemoteCommand]
        public Optional<ReactiveCommand<Unit, Unit>> LeftButtonCommand { get; private set; }
        [RemoteCommand]
        public Optional<ReactiveCommand<Unit, Unit>> RightButtonCommand { get; private set; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> OpenStatusBarCommand { get; private set; }

        public HomeViewModel Home { get; private set; }
        public WebcamViewModel Webcam { get; private set; }
        public MCodesViewModel MCodes { get; private set; }
        [RemoteContent(false)]
        public FluxNavigatorViewModel Navigator { get; private set; }
        public StartupViewModel Startup { get; private set; }
        public FeedersViewModel Feeders { get; private set; }
        public MagazineViewModel Magazine { get; private set; }
        public MessagesViewModel Messages { get; private set; }
        [RemoteContent(false)]
        public StatusBarViewModel StatusBar { get; private set; }
        public CalibrationViewModel Calibration { get; private set; }
        public FunctionalityViewModel Functionality { get; private set; }
        public NetProvider NetProvider { get; private set; }
        public StatsProvider StatsProvider { get; private set; }
        public StatusProvider StatusProvider { get; private set; }
        public SettingsProvider SettingsProvider { get; private set; }
        public DatabaseProvider DatabaseProvider { get; private set; }
        public IFLUX_ConnectionProvider ConnectionProvider { get; private set; }

        private ObservableAsPropertyHelper<DateTime> _CurrentTime;
        [RemoteOutput(true, typeof(DateTimeConverter<DateTimeFormat>))]
        public DateTime CurrentTime => _CurrentTime.Value;

        private ObservableAsPropertyHelper<string> _LeftIconForeground;
        [RemoteOutput(true)]
        public string LeftIconForeground => _LeftIconForeground?.Value;

        private ObservableAsPropertyHelper<string> _RightIconForeground;
        [RemoteOutput(true)]
        public string RightIconForeground => _RightIconForeground?.Value;

        private ObservableAsPropertyHelper<string> _StatusText;
        [RemoteOutput(true)]
        public string StatusText => _StatusText?.Value;

        private ObservableAsPropertyHelper<string> _StatusBrush;
        [RemoteOutput(true)]
        public string StatusBrush => _StatusBrush?.Value;

        IFluxNetProvider IFlux.NetProvider => NetProvider;
        IFluxStatsProvider IFlux.StatsProvider => StatsProvider;
        IFluxStatusProvider IFlux.StatusProvider => StatusProvider;
        IFluxSettingsProvider IFlux.SettingsProvider => SettingsProvider;
        IFluxDatabaseProvider IFlux.DatabaseProvider => DatabaseProvider;

        IFluxMCodesViewModel IFlux.MCodes => MCodes;
        IMessageViewModel IFlux.Messages => Messages;
        IFluxFeedersViewModel IFlux.Feeders => Feeders;
        IFluxNavigatorViewModel IFlux.Navigator => Navigator;
        IFluxCalibrationViewModel IFlux.Calibration => Calibration;

        private Optional<IContentDialog> _ContentDialog;
        [RemoteContent(true, "dialog")]
        public Optional<IContentDialog> ContentDialog 
        { 
            get => _ContentDialog;
            set => this.RaiseAndSetIfChanged(ref _ContentDialog, value);
        }

        public FluxViewModel() : base("flux")
        {
            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.Expect100Continue = false;

            RxApp.DefaultExceptionHandler = new FluxExceptionHandler(this);

            _CurrentTime = Observable.Interval(TimeSpan.FromSeconds(5))
                .Select(_ => DateTime.Now)
                .ToProperty(this, v => v.CurrentTime);

            DatabaseProvider = new DatabaseProvider(this);
            SettingsProvider = new SettingsProvider(this);

            DatabaseProvider.Initialize(db =>
            {
                try
                {
                    var printer_id = SettingsProvider.CoreSettings.Local.PrinterID;
                    var printer_result = db.FindById<Printer>(printer_id.ValueOr(() => 0));
                    var printer = printer_result.Documents.FirstOrDefault().ToOptional();
                    ConnectionProvider = printer.Convert(p => p.MachineGCodeFlavor).ValueOr(() => "") switch
                    {
                        "Modulo3D (Duet)" => new RRF_ConnectionProvider(this),
                        "Modulo3D (Osai)" => new OSAI_ConnectionProvider(this),
                        _ => new Dummy_ConnectionProvider(this)
                    };

                    Messages = new MessagesViewModel(this);
                    NetProvider = new NetProvider(this);
                    Feeders = new FeedersViewModel(this);
                    MCodes = new MCodesViewModel(this);
                    StatusProvider = new StatusProvider(this);
                    Calibration = new CalibrationViewModel(this);
                    Startup = new StartupViewModel(this);
                    Webcam = new WebcamViewModel(this);
                    StatsProvider = new StatsProvider(this);
                    Magazine = new MagazineViewModel(this);
                    Home = new HomeViewModel(this);
                    StatusBar = new StatusBarViewModel(this);
                    Functionality = new FunctionalityViewModel(this);
                    Navigator = new FluxNavigatorViewModel(this);

                    _LeftIconForeground = ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, "chamber")
                        .ObservableOrDefault()
                        .Convert(l => l ? FluxColors.Active : FluxColors.Inactive)
                        .ValueOr(() => FluxColors.Empty)
                        .ToProperty(this, v => v.LeftIconForeground);

                    _RightIconForeground = ConnectionProvider.ObserveVariable(m => m.CHAMBER_LIGHT)
                        .ObservableOrDefault()
                        .Convert(l => l ? FluxColors.Active : FluxColors.Inactive)
                        .ValueOr(() => FluxColors.Empty)
                        .ToProperty(this, v => v.RightIconForeground);

                    var is_idle = StatusProvider.IsIdle
                        .ValueOrDefault();

                    // COMMANDS
                    if (ConnectionProvider.HasVariable(s => s.CHAMBER_LIGHT))
                        RightButtonCommand = ReactiveCommand.CreateFromTask(async () => { await ConnectionProvider.ToggleVariableAsync(m => m.CHAMBER_LIGHT); });

                    if (ConnectionProvider.HasVariable(s => s.OPEN_LOCK, "chamber"))
                        LeftButtonCommand = ReactiveCommand.CreateFromTask(async () => { await ConnectionProvider.ToggleVariableAsync(m => m.OPEN_LOCK, "chamber"); }, is_idle);

                    var status_bar_nav = new NavModalViewModel(this, StatusBar);
                    OpenStatusBarCommand = ReactiveCommand.Create(() => { Navigator.Navigate(status_bar_nav); });

                    ConnectionProvider.Initialize();
                    StatusProvider.Initialize();
                    NetProvider.Initialize();
                    MCodes.Initialize();

                    _StatusText = Observable.CombineLatest(
                        StatusProvider.WhenAnyValue(v => v.FluxStatus),
                        ConnectionProvider.ObserveVariable(m => m.RUNNING_MACRO).ObservableOrDefault(),
                        ConnectionProvider.ObserveVariable(m => m.RUNNING_MCODE).ObservableOrDefault(),
                        ConnectionProvider.ObserveVariable(m => m.RUNNING_GCODE).ObservableOrDefault(),
                        GetStatusText)
                        .ToProperty(this, v => v.StatusText);

                    var offlineBrush = "#999999";
                    var idleBrush = "#00B189";
                    var cycleBrush = "#1ab324";
                    var erroBrush = "#fec02f";
                    var emergBrush = "#f75a5c";
                    var waitBrush = "#275ac3";

                    _StatusBrush = StatusProvider.WhenAnyValue(v => v.FluxStatus)
                        .Select(status =>
                        {
                            return status switch
                            {
                                FLUX_ProcessStatus.IDLE => idleBrush,
                                FLUX_ProcessStatus.WAIT => waitBrush,
                                FLUX_ProcessStatus.NONE => offlineBrush,
                                FLUX_ProcessStatus.CYCLE => cycleBrush,
                                FLUX_ProcessStatus.ERROR => erroBrush,
                                FLUX_ProcessStatus.EMERG => emergBrush,
                                _ => idleBrush
                            };
                        })
                        .ToProperty(this, v => v.StatusBrush);
                }
                catch (Exception ex)
                { 
                }
            });

            Task.Run(async () =>
            {
                await Task.Delay(5000);
                if (ConnectionProvider is Dummy_ConnectionProvider)
                {
                    if (!DatabaseProvider.Database.HasValue)
                        Environment.Exit(1);

                    var printers = DatabaseProvider.Database.Value
                        .FindAll<Printer>().Documents
                        .Distinct()
                        .OrderBy(d => d.Name)
                        .AsObservableChangeSet(m => m.Id)
                        .AsObservableCache();

                    var printer_option = ComboOption.Create($"printer", "Stampante:", printers);
                    var result = await ShowSelectionAsync(
                        "Seleziona un modello di stampante",
                        default,
                        printer_option);

                    if (result != ContentDialogResult.Primary)
                        Environment.Exit(2);

                    var printer_id = printer_option.Items.SelectedKey;
                    SettingsProvider.CoreSettings.Local.PrinterID = printer_id;
                    SettingsProvider.CoreSettings.PersistLocalSettings();
                }
            });
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
        private string GetStatusText(FLUX_ProcessStatus status, Optional<OSAI_Macro> macro, Optional<OSAI_MCode> mcode, Optional<OSAI_GCode> gcode)
        {
            return status switch
            {
                FLUX_ProcessStatus.IDLE => "LIBERA",
                FLUX_ProcessStatus.ERROR => "ERRORE",
                FLUX_ProcessStatus.EMERG => "EMERGENZA",
                FLUX_ProcessStatus.NONE => "ACCENSIONE...",
                FLUX_ProcessStatus.WAIT => "ATTESA OPERATORE",
                FLUX_ProcessStatus.CYCLE => in_cycle(),
                _ => "ONLINE",
            };

            string in_cycle()
            {
                if (!macro.HasValue)
                    return "IN FUNZIONE";

                switch (macro.Value)
                {
                    case OSAI_Macro.PROGRAM:
                        return $"IN STAMPA";

                    case OSAI_Macro.GCODE_OR_MCODE:
                        if (!mcode.HasValue)
                            return "IN FUNZIONE";
                        switch (mcode.Value)
                        {
                            case OSAI_MCode.CHAMBER_TEMP:
                                return "ATTESA CAMERA";
                            case OSAI_MCode.PLATE_TEMP:
                                return "ATTESA PIATTO";
                            case OSAI_MCode.TOOL_TEMP:
                                return "ATTESA ESTRUSORE";
                            case OSAI_MCode.GCODE:
                                if (!gcode.HasValue)
                                    return "IN FUNZIONE";
                                switch (gcode.Value)
                                {
                                    case OSAI_GCode.RAPID_MOVE:
                                    case OSAI_GCode.INTERP_MOVE:
                                        return "IN MOVIMENTO";
                                    default:
                                        return "IN FUNZIONE";
                                }
                            default:
                                return "IN FUNZIONE";
                        }
                    case OSAI_Macro.HOME:
                        return "AZZERAMENTO";
                    case OSAI_Macro.PROBE_PLATE:
                        return "TASTA PIATTO";
                    case OSAI_Macro.PROBE_TOOL:
                        return "TASTA UTENSILE";
                    case OSAI_Macro.CHANGE_TOOL:
                        return "CAMBIO UTENSILE";
                    case OSAI_Macro.READ_TOOL:
                        return "LEGGI UTENSILE";
                    case OSAI_Macro.LOAD_FILAMENT:
                        return "CARICO FILO";
                    case OSAI_Macro.UNLOAD_FILAMENT:
                        return "SCARICO FILO";
                    case OSAI_Macro.PURGE_FILAMENT:
                        return "SPURGO";
                    case OSAI_Macro.END_PRINT:
                        return "FINE STAMPA";
                    case OSAI_Macro.PAUSE_PRINT:
                        return "PAUSA";
                    default:
                        return "IN FUNZIONE";
                }
            }
        }

        public async Task<ContentDialogResult> ShowContentDialogAsync(Func<IFlux, IContentDialog> get_dialog)
        {
            using var dialog = get_dialog(this);
            return await dialog.ShowAsync();
        }
        public async Task<ContentDialogResult> ShowConfirmDialogAsync(string title, string content)
        {
            return await ShowContentDialogAsync(f =>
            {
                var dialog = new ContentDialog(f, title,
                    can_cancel: Observable.Return(true),
                    can_confirm: Observable.Return(true));
                dialog.AddContent(new TextBlock("content", content));
                return dialog;
            });
        }
        public async Task<ContentDialogResult> ShowProgressDialogAsync(string title, Func<IContentDialog, IDialogOption<double>, Task> operation)
        {
            return await ShowContentDialogAsync(f =>
            {
                var dialog = new ContentDialog(this, title);

                var progress = new ProgressBar("progress", "PROGRESSO...");
                dialog.AddContent(progress);
                var operation_task = Task.Run(async () =>
                {
                    try
                    {
                        await operation(dialog, progress);
                    }
                    catch
                    { }
                });

                return dialog;
            });
        }
        public async Task<ContentDialogResult> ShowSelectionAsync(string title, IObservable<bool> can_cancel, IObservable<bool> can_confirm, params IDialogOption[] options)
        {
            return await ShowContentDialogAsync(f =>
            {
                var dialog = new ContentDialog(f, title,
                    can_cancel: can_cancel,
                    can_confirm: can_confirm);
                dialog.AddContent("options", options);
                return dialog;
            });
        }
        public async Task<ContentDialogResult> ShowSelectionAsync(string title, IObservable<bool> can_cancel, params IDialogOption[] options)
        {
            return await ShowContentDialogAsync(f =>
            {
                var can_confirm = Observable.CombineLatest(options.Select(o => o.WhenAnyValue(o => o.HasValue)), l => l.All(l => l));
                var dialog = new ContentDialog(f, title,
                    can_cancel: can_cancel,
                    can_confirm: can_confirm);
                dialog.AddContent("options", options);
                return dialog;
            });
        }
        public async Task<(bool success, Optional<TResult> result)> ShowNFCDialog<TResult>(Optional<INFCHandle> handle, Func<Optional<INFCHandle>, Task<Optional<TResult>>> func, Func<Optional<TResult>, bool> success_func)
        {
            bool reading = true;
            Optional<TResult> result = default;

            using var dialog = new ContentDialog(this, "Lettura tag in corso...", can_cancel: Observable.Return(true));

            var dialog_result = Task.Run(async () =>
            {
                var result = await dialog.ShowAsync();
                reading = false;
                return result == ContentDialogResult.Primary;
            });

            var reading_result = Task.Run(async () =>
            {
                bool success = false;
                do
                {
                    result = await func(handle);
                    success = result.HasValue && success_func(result.Value);
                    if (!success)
                        await Task.Delay(1000);
                }
                while (!success && reading);

                dialog.ShowAsyncSource.TrySetResult(ContentDialogResult.None);
                return success;
            });

            var success = await Task.WhenAll(dialog_result, reading_result);
            return (success[1], result);
        }



        public Task<Optional<TResult>> UseReader<TResult>(Func<Optional<INFCHandle>, TResult> func, Func<TResult, bool> success_func)
        {
            return UseReader(h => func(h).ToOptional(), r => r.HasValue && success_func(r.Value));
        }
        public Task<Optional<TResult>> UseReader<TResult>(Func<Optional<INFCHandle>, Task<TResult>> func, Func<TResult, bool> success_func)
        {
            return UseReader(async h => (await func(h)).ToOptional(), r => r.HasValue && success_func(r.Value));
        }
        public async Task<Optional<TResult>> UseReader<TResult>(Func<Optional<INFCHandle>, Optional<TResult>> func, Func<Optional<TResult>, bool> success_func)
        {
            var task = await NFCReader.OpenAsync(log_result);
            if (task.HasValue)
                return task.Value;

            return func(default);

            async Task<Optional<TResult>> log_result(INFCHandle handle)
            {
                var reading = await ShowNFCDialog(handle.ToOptional(), h => Task.FromResult(func(h)), success_func);

                var light = reading.success ? LightSignalMode.LongGreen : LightSignalMode.LongRed;
                var beep = reading.success ? BeepSignalMode.TripleShort : BeepSignalMode.DoubleShort;
                handle.ReaderUISignal(light, beep);

                return reading.result;
            }
        }
        public async Task<Optional<TResult>> UseReader<TResult>(Func<Optional<INFCHandle>, Task<Optional<TResult>>> func, Func<Optional<TResult>, bool> success_func)
        {
            var task = await NFCReader.OpenAsync(log_result);
            if (task.HasValue)
                return task.Value;

            return await func(default);

            async Task<Optional<TResult>> log_result(INFCHandle handle)
            {
                var reading = await ShowNFCDialog(handle.ToOptional(), func, success_func);

                var light = reading.success ? LightSignalMode.LongGreen : LightSignalMode.LongRed;
                var beep = reading.success ? BeepSignalMode.TripleShort : BeepSignalMode.DoubleShort;
                handle.ReaderUISignal(light, beep);

                return reading.result;
            }
        }
    }

    public class FluxExceptionHandler : IObserver<Exception>
    {
        public FluxViewModel Flux { get; }
        public FluxExceptionHandler(FluxViewModel flux)
        {
            Flux = flux;
        }

        public void OnNext(Exception value)
        {
            //Flux.Messages.LogException(this, value);
        }

        public void OnError(Exception error)
        {
            //Flux.Messages.LogException(this, error);
        }

        public void OnCompleted()
        {
        }
    }
}
