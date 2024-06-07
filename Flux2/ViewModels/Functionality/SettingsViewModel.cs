using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace Flux.ViewModels
{
    public class SettingsViewModel : FluxRoutableNavBarViewModel<SettingsViewModel>
    {
        private string _FluxReleaseDate = "";
        [RemoteOutput(false)]
        public string FluxReleaseDate
        {
            get
            {
                if (string.IsNullOrEmpty(_FluxReleaseDate))
                    _FluxReleaseDate = $"Data del software: {File.GetCreationTime(Assembly.GetExecutingAssembly().Location):dd/MM/yyyy}";
                return _FluxReleaseDate;
            }
        }

        private string _PrinterGuid = "";
        [RemoteOutput(true)]
        public string PrinterGuid
        {
            get => _PrinterGuid;
            set => this.RaiseAndSetIfChanged(ref _PrinterGuid, value);
        }

        [RemoteInput]
        public OptionalSelectableCache<Printer, int> Printers { get; }

        [RemoteInput]
        public SelectableCache<IPAddress, string> HostAddress { get; }

        [RemoteInput]
        public SelectableCache<string, string> NFCFormats { get; }

        [RemoteInput]
        public SelectableCache<INFCTag, string> NFCTagType { get; }

        private Optional<string> _PlcAddress = "";
        [RemoteInput]
        public Optional<string> PlcAddress
        {
            get => _PlcAddress;
            set => this.RaiseAndSetIfChanged(ref _PlcAddress, value);
        }

        private Optional<string> _HostPort = "3001";
        [RemoteInput]
        public Optional<string> HostPort
        {
            get => _HostPort;
            set => this.RaiseAndSetIfChanged(ref _HostPort, value);
        }

        private Optional<string> _PassthroughPort = default;
        [RemoteInput]
        public Optional<string> PassthroughPort
        {
            get => _PassthroughPort;
            set => this.RaiseAndSetIfChanged(ref _PassthroughPort, value);
        }

        private Optional<string> _VPNPort = default;
        [RemoteInput]
        public Optional<string> VpnPort
        {
            get => _VPNPort;
            set => this.RaiseAndSetIfChanged(ref _VPNPort, value);
        }

        private Optional<string> _LoggerAddress = "";
        [RemoteInput]
        public Optional<string> LoggerAddress
        {
            get => _LoggerAddress;
            set => this.RaiseAndSetIfChanged(ref _LoggerAddress, value);
        }

        private Optional<string> _WebcamAddress = "";
        [RemoteInput]
        public Optional<string> WebcamAddress
        {
            get => _WebcamAddress;
            set => this.RaiseAndSetIfChanged(ref _WebcamAddress, value);
        }

        private Optional<string> _PrinterName = "";
        [RemoteInput]
        public Optional<string> PrinterName
        {
            get => _PrinterName;
            set => this.RaiseAndSetIfChanged(ref _PrinterName, value);
        }

        private Optional<double> _CostHour = 15;
        [RemoteInput(step: 0.5, min: 0)]
        public Optional<double> CostHour
        {
            get => _CostHour;
            set => this.RaiseAndSetIfChanged(ref _CostHour, value);
        }

        private Optional<double> _StartupCost = 15;
        [RemoteInput(step: 0.5, min: 0)]
        public Optional<double> StartupCost
        {
            get => _StartupCost;
            set => this.RaiseAndSetIfChanged(ref _StartupCost, value);
        }

        private Optional<int> _StandbyMinutes = 0;
        [RemoteInput(step: 15, min: 0, max: 120, converter: typeof(StandbyConverter))]
        public Optional<int> StandbyMinutes
        {
            get => _StandbyMinutes;
            set => this.RaiseAndSetIfChanged(ref _StandbyMinutes, value);
        }

        private Optional<int> _UltraFastReaderPeriod = 0;
        [RemoteInput(step: 10, min: 0)]
        public Optional<int> UltraFastReaderPeriod
        {
            get => _UltraFastReaderPeriod;
            set => this.RaiseAndSetIfChanged(ref _UltraFastReaderPeriod, value);
        }
        private Optional<int> _FastReaderPeriod = 0;
        [RemoteInput(step: 10, min: 0)]
        public Optional<int> FastReaderPeriod
        {
            get => _FastReaderPeriod;
            set => this.RaiseAndSetIfChanged(ref _FastReaderPeriod, value);
        }
        private Optional<int> _MediumReaderPeriod = 0;
        [RemoteInput(step: 50, min: 0)]
        public Optional<int> MediumReaderPeriod
        {
            get => _MediumReaderPeriod;
            set => this.RaiseAndSetIfChanged(ref _MediumReaderPeriod, value);
        }
        private Optional<int> _SlowReaderPeriod = 0;
        [RemoteInput(step: 50, min: 0)]
        public Optional<int> SlowReaderPeriod
        {
            get => _SlowReaderPeriod;
            set => this.RaiseAndSetIfChanged(ref _SlowReaderPeriod, value);
        }
        private Optional<int> _UltraSlowReaderPeriod = 0;
        [RemoteInput(step: 100, min: 0)]
        public Optional<int> UltraSlowReaderPeriod
        {
            get => _UltraSlowReaderPeriod;
            set => this.RaiseAndSetIfChanged(ref _UltraSlowReaderPeriod, value);
        }

        [RemoteCommand]
        public ReactiveCommandBaseRC<Unit, Unit> SaveSettingsCommand { get; }
        
        [RemoteCommand]
        public ReactiveCommandBaseRC<Unit, Unit> GenerateGuidCommand { get; }

        [RemoteCommand]
        public ReactiveCommandBaseRC<Unit, Unit> TestNFCCommand { get; }

        public SettingsViewModel(FluxViewModel flux) : base(flux)
        {
            var database_changed = flux.DatabaseProvider.WhenAnyValue(v => v.Database);

            var printer_cache = database_changed.Select(FindPrintersAsync)
                .Select(p => p.ToObservable()).Switch()
                .ToObservableChangeSet(p => p.ConvertOr(p => p.Id, () => 0));
            Printers = OptionalSelectableCache.Create(printer_cache, 0);

            var host_address_cache = Flux.SettingsProvider.HostAddressCache
                .Connect();
            HostAddress = SelectableCache.Create(host_address_cache, "");
            
            var nfc_format_cache = NFCFormat.Formats.Values.Select(f => f.FormatName)
                .AsObservableChangeSet(f => f);
            NFCFormats = SelectableCache.Create(nfc_format_cache, "");

            var nfc_tag_type_cache = new INFCTag[] 
                {
                    default(NFCMaterial),
                    default(NFCToolNozzle),
                }
                .AsObservableChangeSet(f => f.GetType().Name);
            NFCTagType = SelectableCache.Create(nfc_tag_type_cache, "");

            var user_settings = Flux.SettingsProvider.UserSettings.Local;
            var core_settings = Flux.SettingsProvider.CoreSettings.Local;
            var memory_settings = Flux.SettingsProvider.MemorySettings.Local;

            core_settings.WhenAnyValue(s => s.PrinterGuid)
                .Select(g => g.ToString())
                .BindToRC(this, v => v.PrinterGuid);

            core_settings.WhenAnyValue(s => s.PrinterID)
                .BindToRC(this, v => v.Printers.SelectedKey);

            core_settings.WhenAnyValue(s => s.HostID)
                .BindToRC(this, v => v.HostAddress.SelectedKey);

            core_settings.WhenAnyValue(s => s.HostPort)
                .Convert(host_port => $"{host_port}")
                .BindToRC(this, v => v.HostPort);

            core_settings.WhenAnyValue(s => s.PassthroughPort)
                .Convert(passthrough_port => $"{passthrough_port}")
                .BindToRC(this, v => v.PassthroughPort);

            core_settings.WhenAnyValue(s => s.VPNPort)
                .Convert(vpn_port => $"{vpn_port}")
                .BindToRC(this, v => v.VpnPort);

            core_settings.WhenAnyValue(s => s.NFCFormat)
                .BindToRC(this, v => v.NFCFormats.SelectedKey);

            core_settings.WhenAnyValue(s => s.PLCAddress)
                .BindToRC(this, v => v.PlcAddress);

            core_settings.WhenAnyValue(s => s.WebcamAddress)
                .BindToRC(this, v => v.WebcamAddress);

            core_settings.WhenAnyValue(s => s.LoggerAddress)
                .BindToRC(this, v => v.LoggerAddress);

            user_settings.WhenAnyValue(s => s.PrinterName)
                .BindToRC(this, v => v.PrinterName);

            user_settings.WhenAnyValue(s => s.CostHour)
                .BindToRC(this, v => v.CostHour);

            user_settings.WhenAnyValue(s => s.StartupCost)
                .BindToRC(this, v => v.StartupCost);

            user_settings.WhenAnyValue(s => s.StandbyMinutes)
                .BindToRC(this, v => v.StandbyMinutes);

            memory_settings.WhenAnyValue(s => s.UltraFastReaderPeriod)
                .BindToRC(this, v => v.UltraFastReaderPeriod);

            memory_settings.WhenAnyValue(s => s.FastReaderPeriod)
                .BindToRC(this, v => v.FastReaderPeriod);

            memory_settings.WhenAnyValue(s => s.MediumReaderPeriod)
                .BindToRC(this, v => v.MediumReaderPeriod);

            memory_settings.WhenAnyValue(s => s.SlowReaderPeriod)
                .BindToRC(this, v => v.SlowReaderPeriod);

            memory_settings.WhenAnyValue(s => s.UltraSlowReaderPeriod)
                .BindToRC(this, v => v.UltraSlowReaderPeriod);

            SaveSettingsCommand = ReactiveCommandBaseRC.Create(SaveSettings, this);
            GenerateGuidCommand = ReactiveCommandBaseRC.Create(GenerateGuid, this);

            var can_test_nfc = Observable.CombineLatest(
                NFCFormats.SelectedValueChanged,
                NFCTagType.SelectedValueChanged,
                (format, tag) => format.HasValue && tag.HasValue);

            TestNFCCommand = ReactiveCommandBaseRC.CreateFromTask(TestNFCAsync, this, can_test_nfc);
        }

        private async Task TestNFCAsync()
        {
            var core_settings = Flux.SettingsProvider.CoreSettings.Local;
            var nfc_format = NFCFormat.Formats.LookupOptional(core_settings.NFCFormat);
            if (!nfc_format.HasValue)
                return;

            var nfc_tag = await Flux.UseReader(h =>
            {
                if (!h.HasValue)
                    return (Optional<INFCTag>.None, NFCTagRW.ReaderNotFound);

                var tag = NFCTagType.SelectedValue.ValueOr(() => default) switch
                {
                    NFCMaterial m => h.Value.ReadTag<NFCMaterial>(nfc_format.Value).tag.Cast<NFCMaterial, INFCTag>(),
                    NFCToolNozzle tn => h.Value.ReadTag<NFCToolNozzle>(nfc_format.Value).tag.Cast<NFCToolNozzle, INFCTag>(),
                    _ => default
                };

                return (tag, NFCTagRW.Success);
            }, (tag, rw) => tag.HasValue);

            // Flux.Messages.LogMessage($"LETTURA TAG {NFCTagType.SelectedKey}", JsonUtils.Serialize(nfc_tag), MessageLevel.INFO, 0);
        }

        private void SaveSettings()
        {
            try
            {
                var user_settings = Flux.SettingsProvider.UserSettings.Local;
                var core_settings = Flux.SettingsProvider.CoreSettings.Local;
                var memory_settings = Flux.SettingsProvider.MemorySettings.Local;

                core_settings.PLCAddress = PlcAddress;
                core_settings.HostPort = HostPort.Convert(host_port_str =>
                {
                    if (!int.TryParse(host_port_str, out var host_port))
                        return Optional<int>.None;
                    return host_port;
                });
                core_settings.PassthroughPort = PassthroughPort.Convert(passthrough_port_str =>
                {
                    if (!int.TryParse(passthrough_port_str, out var passthrough_port))
                        return Optional<int>.None;
                    return passthrough_port;
                });
                core_settings.VPNPort = VpnPort.Convert(vpn_port_str =>
                {
                    if (!int.TryParse(vpn_port_str, out var vpn_port))
                        return Optional<int>.None;
                    return vpn_port;
                });

                core_settings.WebcamAddress = WebcamAddress;
                core_settings.LoggerAddress = LoggerAddress;
                core_settings.PrinterID = Printers.SelectedKey;
                core_settings.HostID = HostAddress.SelectedKey;
                core_settings.NFCFormat = NFCFormats.SelectedKey;
                core_settings.PrinterGuid = Guid.Parse(PrinterGuid);

                user_settings.CostHour = CostHour;
                user_settings.StartupCost = StartupCost;
                user_settings.PrinterName = PrinterName;
                user_settings.StandbyMinutes = StandbyMinutes;

                memory_settings.UltraFastReaderPeriod = UltraFastReaderPeriod;
                memory_settings.FastReaderPeriod = FastReaderPeriod;
                memory_settings.MediumReaderPeriod = MediumReaderPeriod;
                memory_settings.SlowReaderPeriod = SlowReaderPeriod;
                memory_settings.UltraSlowReaderPeriod = UltraSlowReaderPeriod;

                if (!Flux.SettingsProvider.PersistLocalSettings())
                    return;
            }
            catch (Exception ex)
            {
                // Flux.Messages.LogException(this, ex);
            }
        }

        private void GenerateGuid()
        {
            PrinterGuid = Guid.NewGuid().ToString();
        }

        private async IAsyncEnumerable<Optional<Printer>> FindPrintersAsync(Optional<ILocalDatabase> database)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var printers = await database.ConvertAsync(db => db.FindAllAsync<Printer>(cts.Token));
            if (!printers.HasValue)
                yield break;

            foreach (var printer in printers.Value)
                yield return printer;
        }
    }
}
