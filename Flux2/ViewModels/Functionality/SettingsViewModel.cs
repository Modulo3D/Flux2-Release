using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace Flux.ViewModels
{
    public class SettingsViewModel : FluxRoutableNavBarViewModel<SettingsViewModel>
    {
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

        private Optional<float> _CostHour = 15;
        [RemoteInput]
        public Optional<float> CostHour
        {
            get => _CostHour;
            set => this.RaiseAndSetIfChanged(ref _CostHour, value);
        }

        private Optional<int> _StandbyMinutes = 0;
        [RemoteInput(step: 15, min: 0, max: 120, converter: typeof(StandbyConverter))]
        public Optional<int> StandbyMinutes
        {
            get => _StandbyMinutes;
            set => this.RaiseAndSetIfChanged(ref _StandbyMinutes, value);
        }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> SaveSettingsCommand { get; }
        
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> GenerateGuidCommand { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> TestNFCCommand { get; }

        public SettingsViewModel(FluxViewModel flux) : base(flux)
        {
            var database_changed = flux.DatabaseProvider.WhenAnyValue(v => v.Database);

            var printer_cache = database_changed.Select(FindPrinters)
                .AsObservableChangeSet(p => p.ConvertOr(p => p.Id, () => 0));
            Printers = OptionalSelectableCache.Create(printer_cache);

            var host_address_cache = Flux.SettingsProvider.HostAddressCache
                .Connect();
            HostAddress = SelectableCache.Create(host_address_cache);
            
            var nfc_format_cache = NFCFormat.Formats.Values.Select(f => f.FormatName)
                .AsObservableChangeSet(f => f);
            NFCFormats = SelectableCache.Create(nfc_format_cache);

            var nfc_tag_type_cache = new INFCTag[] 
                {
                    default(NFCMaterial),
                    default(NFCToolNozzle),
                }
                .AsObservableChangeSet(f => f.GetType().Name);
            NFCTagType = SelectableCache.Create(nfc_tag_type_cache);

            var user_settings = Flux.SettingsProvider.UserSettings.Local;
            var core_settings = Flux.SettingsProvider.CoreSettings.Local;

            core_settings.WhenAnyValue(s => s.PrinterGuid)
                .Select(g => g.ToString())
                .BindTo(this, v => v.PrinterGuid);

            core_settings.WhenAnyValue(s => s.PrinterID)
                .BindTo(this, v => v.Printers.SelectedKey);

            core_settings.WhenAnyValue(s => s.HostID)
                .BindTo(this, v => v.HostAddress.SelectedKey);

            core_settings.WhenAnyValue(s => s.NFCFormat)
                .BindTo(this, v => v.NFCFormats.SelectedKey);

            core_settings.WhenAnyValue(s => s.PLCAddress)
                .BindTo(this, v => v.PlcAddress);

            core_settings.WhenAnyValue(s => s.WebcamAddress)
                .BindTo(this, v => v.WebcamAddress);

            core_settings.WhenAnyValue(s => s.LoggerAddress)
                .BindTo(this, v => v.LoggerAddress);

            user_settings.WhenAnyValue(s => s.PrinterName)
                .BindTo(this, v => v.PrinterName);

            user_settings.WhenAnyValue(s => s.CostHour)
                .BindTo(this, v => v.CostHour);

            user_settings.WhenAnyValue(s => s.StandbyMinutes)
                .BindTo(this, v => v.StandbyMinutes);

            SaveSettingsCommand = ReactiveCommand.Create(SaveSettings);
            GenerateGuidCommand = ReactiveCommand.Create(GenerateGuid);

            var can_test_nfc = Observable.CombineLatest(
                NFCFormats.SelectedKeyChanged,
                NFCTagType.SelectedKeyChanged,
                (format, tag) => format.HasValue && tag.HasValue);

            TestNFCCommand = ReactiveCommand.CreateFromTask(TestNFCAsync, can_test_nfc);
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

            Flux.Messages.LogMessage($"LETTURA TAG {NFCTagType.SelectedKey}", JsonUtils.Serialize(nfc_tag), MessageLevel.INFO, 0);
        }

        private void SaveSettings()
        {
            try
            {
                var user_settings = Flux.SettingsProvider.UserSettings.Local;
                var core_settings = Flux.SettingsProvider.CoreSettings.Local;

                user_settings.CostHour = CostHour;
                core_settings.PLCAddress = PlcAddress;
                user_settings.PrinterName = PrinterName;
                core_settings.WebcamAddress = WebcamAddress;
                core_settings.LoggerAddress = LoggerAddress;
                user_settings.StandbyMinutes = StandbyMinutes;
                core_settings.PrinterID = Printers.SelectedKey;
                core_settings.HostID = HostAddress.SelectedKey;
                core_settings.NFCFormat = NFCFormats.SelectedKey;
                core_settings.PrinterGuid = Guid.Parse(PrinterGuid);

                if (!Flux.SettingsProvider.PersistLocalSettings())
                    return;
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
            }
        }

        private void GenerateGuid()
        {
            PrinterGuid = Guid.NewGuid().ToString();
        }

        private IEnumerable<Optional<Printer>> FindPrinters(Optional<ILocalDatabase> database)
        {
            var printers = database.Convert(db => db.FindAll<Printer>());
            if (!printers.HasValue)
                yield break;

            foreach (var printer in printers.Value.Documents)
                yield return printer;
        }
    }
}
