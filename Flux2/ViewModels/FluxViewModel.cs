using DynamicData;
using DynamicData.Kernel;
using EmbedIO.Routing;
using Flux.ViewModels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Disposables;
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
        public static double MaxZBedHeight = 10.0;

        [RemoteCommand]
        public Optional<ReactiveCommandBaseRC> LeftButtonCommand { get; private set; }
        [RemoteCommand]
        public Optional<ReactiveCommandBaseRC> RightButtonCommand { get; private set; }
        [RemoteCommand]
        public ReactiveCommandBaseRC OpenStatusBarCommand { get; private set; }

        public HomeViewModel Home { get; private set; }
        public WebcamViewModel Webcam { get; private set; }
        public MCodesViewModel MCodes { get; private set; }
        [RemoteContent(false)]
        public FluxNavigatorViewModel Navigator { get; private set; }
        public LoggingProvider LoggingProvider { get; private set; }
        public StartupViewModel Startup { get; private set; }
        public FeedersViewModel Feeders { get; private set; }
        public MessagesViewModel Messages { get; private set; }
        [RemoteContent(false)]
        public StatusBarViewModel StatusBar { get; private set; }
        public CalibrationViewModel Calibration { get; private set; }
        public FunctionalityViewModel Functionality { get; private set; }
        public ConditionsProvider ConditionsProvider { get; private set; }
        public Lazy<TemperaturesViewModel> Temperatures { get; private set; }
        public NetProvider NetProvider { get; private set; }
        public StatsProvider StatsProvider { get; private set; }
        public StatusProvider StatusProvider { get; private set; }
        public SettingsProvider SettingsProvider { get; private set; }
        public DatabaseProvider DatabaseProvider { get; private set; }
        public IFLUX_ConnectionProvider ConnectionProvider { get; private set; }

        private readonly ObservableAsPropertyHelper<DateTime> _CurrentTime;
        [RemoteOutput(true, typeof(DateTimeConverter<AbsoluteDateTimeFormat>))]
        public DateTime CurrentTime => _CurrentTime.Value;

        private ObservableAsPropertyHelper<string> _LeftIconForeground;
        [RemoteOutput(true)]
        public string LeftIconForeground => _LeftIconForeground?.Value;

        private ObservableAsPropertyHelper<string> _RightIconForeground;
        [RemoteOutput(true)]
        public string RightIconForeground => _RightIconForeground?.Value;

        private ObservableAsPropertyHelper<RemoteText> _StatusText;
        [RemoteOutput(true)]
        public RemoteText StatusText => _StatusText?.Value ?? default;

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

        private Optional<IDialog> _ContentDialog;
        [RemoteContent(true)]
        public Optional<IDialog> Dialog
        {
            get => _ContentDialog;
            set => this.RaiseAndSetIfChanged(ref _ContentDialog, value);
        }

        public ILogger<IFlux> Logger { get; }

        public FluxViewModel(ILogger<IFlux> logger)
        {
            Logger = logger;

            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.Expect100Continue = false;

            RxApp.DefaultExceptionHandler = new FluxExceptionHandler(this);

            _CurrentTime = Observable.Interval(TimeSpan.FromSeconds(5))
                .Select(_ => DateTime.Now)
                .ToPropertyRC(this, v => v.CurrentTime);

            DatabaseProvider = new DatabaseProvider(this);
            SettingsProvider = new SettingsProvider(this);

            DatabaseProvider.InitializeAsync(async db =>
            {
                try
                {
                    Messages = new MessagesViewModel(this);
                    NetProvider = new NetProvider(this);

                    var printer_id = SettingsProvider.CoreSettings.Local.PrinterID;

                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var printer_result = await db.FindByIdAsync<Printer>(printer_id.ValueOr(() => 0), cts.Token);
                    var printer = printer_result.FirstOrOptional(_ => true);

                    var connection_provider = printer.ConvertOr(p => p.Id, () => -1) switch
                    {
                        5 => new OSAI_ConnectionProvider(this),
                        6 => new OSAI_ConnectionProvider(this),
                        7 => new OSAI_ConnectionProvider(this),
                        8 => new RRF_ConnectionProvider(this, c => new RRF_VariableStoreS300(c)),
                        9 => new RRF_ConnectionProvider(this, c => new RRF_VariableStoreS300(c)),
                        10 => new RRF_ConnectionProvider(this, c => new RRF_VariableStoreS300C(c)),
                        11 => new RRF_ConnectionProvider(this, c => new RRF_VariableStoreS300C(c)),
                        12 => new RRF_ConnectionProvider(this, c => new RRF_VariableStoreMP500(c)),
                        13 => new RRF_ConnectionProvider(this, c => new RRF_VariableStoreS300A(c)),
                        14 => new RRF_ConnectionProvider(this, c => new RRF_VariableStoreS300A(c)),
                        15 => new OSAI_ConnectionProvider(this),
                        _ => Optional<IFLUX_ConnectionProvider>.None
                    };

                    if (!connection_provider.HasValue)
                    {
                        Console.WriteLine("Nessuna configurazione trovata");
                        Environment.Exit(-1);
                        return;
                    }

                    ConnectionProvider = connection_provider.Value;

                    Feeders = new FeedersViewModel(this);
                    MCodes = new MCodesViewModel(this);
                    ConditionsProvider = new ConditionsProvider(this);
                    StatusProvider = new StatusProvider(this);
                    Calibration = new CalibrationViewModel(this);
                    Webcam = new WebcamViewModel(this);
                    StatsProvider = new StatsProvider(this);
                    Home = new HomeViewModel(this);
                    StatusBar = new StatusBarViewModel(this);
                    Functionality = new FunctionalityViewModel(this);
                    Navigator = new FluxNavigatorViewModel(this);
                    LoggingProvider = new LoggingProvider(this);
                    Startup = new StartupViewModel(this);
                    Temperatures = new Lazy<TemperaturesViewModel>(() => new TemperaturesViewModel(this));

                    var main_lock_unit = ConnectionProvider.GetArrayUnit(m => m.OPEN_LOCK, "main.lock");
                    _LeftIconForeground = ConnectionProvider.ObserveVariable(m => m.OPEN_LOCK, main_lock_unit)
                        .ObservableOrDefault()
                        .Convert(l => l ? FluxColors.Active : FluxColors.Inactive)
                        .ValueOr(() => FluxColors.Empty)
                        .ToPropertyRC(this, v => v.LeftIconForeground);

                    _RightIconForeground = ConnectionProvider.ObserveVariable(m => m.CHAMBER_LIGHT)
                        .ObservableOrDefault()
                        .Convert(l => l ? FluxColors.Active : FluxColors.Inactive)
                        .ValueOr(() => FluxColors.Empty)
                        .ToPropertyRC(this, v => v.RightIconForeground);

                    var is_idle = StatusProvider
                        .WhenAnyValue(s => s.StatusEvaluation)
                        .Select(s => s.IsIdle);

                    // COMMANDS
                    if (ConnectionProvider.HasVariable(s => s.CHAMBER_LIGHT))
                        RightButtonCommand = ReactiveCommandBaseRC.CreateFromTask(async () => { await ConnectionProvider.ToggleVariableAsync(m => m.CHAMBER_LIGHT); }, this);

                    if (ConnectionProvider.HasVariable(s => s.OPEN_LOCK, main_lock_unit))
                        LeftButtonCommand = ReactiveCommandBaseRC.CreateFromTask(async () => { await ConnectionProvider.ToggleVariableAsync(m => m.OPEN_LOCK, main_lock_unit); }, this, is_idle);

                    var status_bar_nav = new NavModalViewModel<StatusBarViewModel>(this, StatusBar);
                    OpenStatusBarCommand = ReactiveCommandBaseRC.Create(() => { Navigator.Navigate(status_bar_nav); }, this);

                    ConnectionProvider.Initialize();
                    StatusProvider.Initialize();
                    NetProvider.Initialize();
                    MCodes.Initialize();

                    _StatusText = Observable.CombineLatest(
                        StatusProvider.WhenAnyValue(v => v.FluxStatus),
                        StatusProvider.WhenAnyValue(v => v.PrintingEvaluation),
                        GetStatusText)
                        .Throttle(TimeSpan.FromSeconds(0.25))
                        .ToPropertyRC(this, v => v.StatusText);

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
                        .Throttle(TimeSpan.FromSeconds(0.25))
                        .ToPropertyRC(this, v => v.StatusBrush);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return Task.CompletedTask;
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
        private RemoteText GetStatusText(FLUX_ProcessStatus status, PrintingEvaluation printing_evaluation)
        {
            return status switch
            {
                FLUX_ProcessStatus.IDLE => new RemoteText("status.idle", true),
                FLUX_ProcessStatus.ERROR => new RemoteText("status.error", true),
                FLUX_ProcessStatus.EMERG => new RemoteText("status.emerg", true),
                FLUX_ProcessStatus.CYCLE => new RemoteText("status.cycle", true),
                FLUX_ProcessStatus.NONE => new RemoteText("status.connecting", true),
                FLUX_ProcessStatus.WAIT => wait(),
                _ => new RemoteText("status.online", true),
            };

            RemoteText wait()
            {
                if (printing_evaluation.Recovery.HasValue)
                    return new RemoteText("status.paused", true);
                return new RemoteText("status.wait", true);
            }
        }

        public async Task<(Optional<TDialogResult> data, DialogResult result)> ShowDialogAsync<TDialogResult>(Func<IFlux, IDialog<TDialogResult>> get_dialog)
        {
            using var dialog = get_dialog(this);
            return await dialog.ShowAsync();
        }
        public async Task<(Optional<Unit> data, DialogResult result)> ShowModalDialogAsync<TFluxRoutableViewModel>(Func<FluxViewModel, TFluxRoutableViewModel> get_modal)
            where TFluxRoutableViewModel : IFluxRoutableViewModel
        {
            using var dialog = new ModalDialog(this, f => get_modal((FluxViewModel)f));
            return await dialog.ShowAsync();
        }
        public async Task<(Optional<Unit> data, DialogResult result)> ShowModalDialogAsync<TFluxRoutableViewModel>(Func<FluxViewModel, Lazy<TFluxRoutableViewModel>> get_modal)
            where TFluxRoutableViewModel : IFluxRoutableViewModel
        {
            using var dialog = new ModalDialog(this, f => get_modal((FluxViewModel)f).Value);
            return await dialog.ShowAsync();
        }
        public async Task<bool> IterateDialogAsync<TDialogResult>(Func<IFlux, IDialog<TDialogResult>> get_dialog, ushort max_iterations, Func<Task> iteration_task)
        {
            ushort iterations = 0;
            (Optional<TDialogResult> data, DialogResult result) result = default;
            while (result.result != DialogResult.Primary && iterations < max_iterations)
            {
                iterations++;
                await iteration_task();
                result = await ShowDialogAsync(get_dialog);
            }
            return result.result == DialogResult.Primary;
        }

        public async Task<ValueResult<(TResult, NFCTagRW)>> ShowNFCDialog<TResult>(INFCHandle handle, Func<INFCHandle, INFCRWViewModel, Task<(TResult, NFCTagRW)>> func, Func<TResult, NFCTagRW, bool> success, int millisecond_delay = 200)
        {
            var reading = true;

            using var dialog = new ContentDialog(this, new NFCRWViewModel());
            var dialog_result = Task.Run(async () =>
            {
                var result = await dialog.ShowAsync();
                reading = false;
                return result.result == DialogResult.Primary;
            });

            var reading_result = Task.Run(async () =>
            {
                (TResult data, NFCTagRW rw) result = (default, NFCTagRW.None);
                do
                {
                    result = await func(handle, (NFCRWViewModel)dialog.Content);
                    if (!success(result.data, result.rw))
                        await Task.Delay(millisecond_delay);
                }
                while (!success(result.data, result.rw) && reading);

                dialog.ShowAsyncSource.TrySetResult((Unit.Default, DialogResult.Primary));
                return result;
            });

            await Task.WhenAll(dialog_result, reading_result);
            return await reading_result;
        }
        public async Task<ValueResult<(TResult, NFCTagRW)>> ShowNFCDialog<TResult>(INFCHandle handle, Func<INFCHandle, INFCRWViewModel, (TResult, NFCTagRW)> func, Func<TResult, NFCTagRW, bool> success, int millisecond_delay = 200)
        {
            var reading = true;

            using var dialog = new ContentDialog(this, new NFCRWViewModel());
            var dialog_result = Task.Run(async () =>
            {
                var result = await dialog.ShowAsync();
                reading = false;
                return result.result == DialogResult.Primary;
            });

            var reading_result = Task.Run(async () =>
            {
                (TResult data, NFCTagRW rw) result = (default, NFCTagRW.None);
                do
                {
                    result = func(handle, (NFCRWViewModel)dialog.Content);
                    if (!success(result.data, result.rw))
                        await Task.Delay(millisecond_delay);
                }
                while (!success(result.data, result.rw) && reading);

                dialog.ShowAsyncSource.TrySetResult((Unit.Default, DialogResult.Primary));
                return result;
            });

            await Task.WhenAll(dialog_result, reading_result);
            return await reading_result;
        }

        public async Task<ValueResult<(TResult, NFCTagRW)>> UseReader<TResult>(Func<Optional<INFCHandle>, Task<(TResult, NFCTagRW)>> func, Func<TResult, NFCTagRW, bool> success)
        {
            var task = await NFCReader.OpenAsync(log_result);
            if (task.HasValue && success(task.Value.Item1, task.Value.Item2))
                return task.Value;

            return default;

            async Task<ValueResult<(TResult, NFCTagRW)>> log_result(INFCHandle handle)
            {
                var result = await ShowNFCDialog(handle, (h, card_info) => func(h.ToOptional()), success);
                var light = result.HasValue && success(result.Value.Item1, result.Value.Item2) ? LightSignalMode.LongGreen : LightSignalMode.LongRed;
                var beep = result.HasValue && success(result.Value.Item1, result.Value.Item2) ? BeepSignalMode.TripletMelody : BeepSignalMode.Short;
                handle.ReaderUISignal(light, beep);
                return result;
            }
        }
        public async Task<ValueResult<(TResult, NFCTagRW)>> UseReader<TResult>(IFluxTagViewModel tag, Func<Optional<INFCHandle>, INFCSlot, Optional<INFCRWViewModel>, Task<(TResult, NFCTagRW)>> func, Func<TResult, NFCTagRW, bool> success)
        {
            var reading = tag.NFCSlot.Nfc;
            if (!reading.IsVirtualTag.ValueOr(() => false))
            {
                var task = await NFCReader.OpenAsync(log_result);
                if (task.HasValue && success(task.Value.Item1, task.Value.Item2))
                    return task.Value;
            }

            return await func(default, tag.NFCSlot, default);

            async Task<ValueResult<(TResult, NFCTagRW)>> log_result(INFCHandle handle)
            {
                var result = await ShowNFCDialog(handle, (h, card_info) => func(h.ToOptional(), tag.NFCSlot, card_info.ToOptional()), success);
                var light = result.HasValue && success(result.Value.Item1, result.Value.Item2) ? LightSignalMode.LongGreen : LightSignalMode.LongRed;
                var beep = result.HasValue && success(result.Value.Item1, result.Value.Item2) ? BeepSignalMode.TripletMelody : BeepSignalMode.Short;
                handle.ReaderUISignal(light, beep);
                return result;
            }
        }

        public async Task<ValueResult<(TResult, NFCTagRW)>> UseReader<TResult>(Func<Optional<INFCHandle>, (TResult, NFCTagRW)> func, Func<TResult, NFCTagRW, bool> success)
        {
            var task = await NFCReader.OpenAsync(log_result);
            if (task.HasValue && success(task.Value.Item1, task.Value.Item2))
                return task.Value;

            return default;

            async Task<ValueResult<(TResult, NFCTagRW)>> log_result(INFCHandle handle)
            {
                var result = await ShowNFCDialog(handle, (h, card_info) => func(h.ToOptional()), success);
                var light = result.HasValue && success(result.Value.Item1, result.Value.Item2) ? LightSignalMode.LongGreen : LightSignalMode.LongRed;
                var beep = result.HasValue && success(result.Value.Item1, result.Value.Item2) ? BeepSignalMode.TripletMelody : BeepSignalMode.Short;
                handle.ReaderUISignal(light, beep);
                return result;
            }
        }
        public async Task<ValueResult<(TResult, NFCTagRW)>> UseReader<TResult>(IFluxTagViewModel tag, Func<Optional<INFCHandle>, INFCSlot, Optional<INFCRWViewModel>, (TResult, NFCTagRW)> func, Func<TResult, NFCTagRW, bool> success)
        {
            var reading = tag.NFCSlot.Nfc;
            if (!reading.IsVirtualTag.ValueOr(() => false))
            {
                var task = await NFCReader.OpenAsync(log_result);
                if (task.HasValue && success(task.Value.Item1, task.Value.Item2))
                    return task.Value;
            }

            return func(default, tag.NFCSlot, default);

            async Task<ValueResult<(TResult, NFCTagRW)>> log_result(INFCHandle handle)
            {
                var result = await ShowNFCDialog(handle, (h, card_info) => func(h.ToOptional(), tag.NFCSlot, card_info.ToOptional()), success);
                var light = result.HasValue && success(result.Value.Item1, result.Value.Item2) ? LightSignalMode.LongGreen : LightSignalMode.LongRed;
                var beep = result.HasValue && success(result.Value.Item1, result.Value.Item2) ? BeepSignalMode.TripletMelody : BeepSignalMode.Short;
                handle.ReaderUISignal(light, beep);
                return result;
            }
        }

        // -------

        public async Task<ValueResult<NFCTagRW>> ShowNFCDialog(INFCHandle handle, Func<INFCHandle, INFCRWViewModel, Task<NFCTagRW>> func, Func<NFCTagRW, bool> success, int millisecond_delay = 200)
        {
            var reading = true;

            using var dialog = new ContentDialog(this, new NFCRWViewModel());
            var dialog_result = Task.Run(async () =>
            {
                var result = await dialog.ShowAsync();
                reading = false;
                return result.result == DialogResult.Primary;
            });

            var reading_result = Task.Run(async () =>
            {
                NFCTagRW result = NFCTagRW.None;
                do
                {
                    result = await func(handle, (NFCRWViewModel)dialog.Content);
                    if (!success(result))
                        await Task.Delay(millisecond_delay);
                }
                while (!success(result) && reading);

                dialog.ShowAsyncSource.TrySetResult((Unit.Default, DialogResult.Primary));
                return result;
            });

            await Task.WhenAll(dialog_result, reading_result);
            return await reading_result;
        }
        public async Task<ValueResult<NFCTagRW>> ShowNFCDialog(INFCHandle handle, Func<INFCHandle, INFCRWViewModel, NFCTagRW> func, Func<NFCTagRW, bool> success, int millisecond_delay = 200)
        {
            var reading = true;

            using var dialog = new ContentDialog(this, new NFCRWViewModel());
            var dialog_result = Task.Run(async () =>
            {
                var result = await dialog.ShowAsync();
                reading = false;
                return result.result == DialogResult.Primary;
            });

            var reading_result = Task.Run(async () =>
            {
                NFCTagRW result = NFCTagRW.None;
                do
                {
                    result = func(handle, (NFCRWViewModel)dialog.Content);
                    if (!success(result))
                        await Task.Delay(millisecond_delay);
                }
                while (!success(result) && reading);

                dialog.ShowAsyncSource.TrySetResult((Unit.Default, DialogResult.Primary));
                return result;
            });

            await Task.WhenAll(dialog_result, reading_result);
            return await reading_result;
        }

        public async Task<ValueResult<NFCTagRW>> UseReader(Func<Optional<INFCHandle>, Task<NFCTagRW>> func, Func<NFCTagRW, bool> success)
        {
            var task = await NFCReader.OpenAsync(log_result);
            if (task.HasValue && success(task.Value))
                return task.Value;

            return default;

            async Task<ValueResult<NFCTagRW>> log_result(INFCHandle handle)
            {
                var result = await ShowNFCDialog(handle, (h, card_info) => func(h.ToOptional()), success);
                var light = result.HasValue && success(result.Value) ? LightSignalMode.LongGreen : LightSignalMode.LongRed;
                var beep = result.HasValue && success(result.Value) ? BeepSignalMode.TripletMelody : BeepSignalMode.Short;
                handle.ReaderUISignal(light, beep);
                return result;
            }
        }
        public async Task<ValueResult<NFCTagRW>> UseReader(IFluxTagViewModel tag, Func<Optional<INFCHandle>, INFCSlot, Optional<INFCRWViewModel>, Task<NFCTagRW>> func, Func<NFCTagRW, bool> success)
        {
            var reading = tag.NFCSlot.Nfc;
            if (!reading.IsVirtualTag.ValueOr(() => false))
            {
                var task = await NFCReader.OpenAsync(log_result);
                if (task.HasValue && success(task.Value))
                    return task.Value;
            }

            return await func(default, tag.NFCSlot, default);

            async Task<ValueResult<NFCTagRW>> log_result(INFCHandle handle)
            {
                var result = await ShowNFCDialog(handle, (h, card_info) => func(h.ToOptional(), tag.NFCSlot, card_info.ToOptional()), success);
                var light = result.HasValue && success(result.Value) ? LightSignalMode.LongGreen : LightSignalMode.LongRed;
                var beep = result.HasValue && success(result.Value) ? BeepSignalMode.TripletMelody : BeepSignalMode.Short;
                handle.ReaderUISignal(light, beep);
                return result;
            }
        }

        public async Task<ValueResult<NFCTagRW>> UseReader(Func<Optional<INFCHandle>, NFCTagRW> func, Func<NFCTagRW, bool> success)
        {
            var task = await NFCReader.OpenAsync(log_result);
            if (task.HasValue && success(task.Value))
                return task.Value;

            return default;

            async Task<ValueResult<NFCTagRW>> log_result(INFCHandle handle)
            {
                var result = await ShowNFCDialog(handle, (h, card_info) => func(h.ToOptional()), success);
                var light = result.HasValue && success(result.Value) ? LightSignalMode.LongGreen : LightSignalMode.LongRed;
                var beep = result.HasValue && success(result.Value) ? BeepSignalMode.TripletMelody : BeepSignalMode.Short;
                handle.ReaderUISignal(light, beep);
                return result;
            }
        }
        public async Task<ValueResult<NFCTagRW>> UseReader(IFluxTagViewModel tag, Func<Optional<INFCHandle>, INFCSlot, Optional<INFCRWViewModel>, NFCTagRW> func, Func<NFCTagRW, bool> success)
        {
            var reading = tag.NFCSlot.Nfc;
            if (!reading.IsVirtualTag.ValueOr(() => false))
            {
                var task = await NFCReader.OpenAsync(log_result);
                if (task.HasValue && success(task.Value))
                    return task.Value;
            }

            return func(default, tag.NFCSlot, default);

            async Task<ValueResult<NFCTagRW>> log_result(INFCHandle handle)
            {
                var result = await ShowNFCDialog(handle, (h, card_info) => func(h.ToOptional(), tag.NFCSlot, card_info.ToOptional()), success);
                var light = result.HasValue && success(result.Value) ? LightSignalMode.LongGreen : LightSignalMode.LongRed;
                var beep = result.HasValue && success(result.Value) ? BeepSignalMode.TripletMelody : BeepSignalMode.Short;
                handle.ReaderUISignal(light, beep);
                return result;
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
