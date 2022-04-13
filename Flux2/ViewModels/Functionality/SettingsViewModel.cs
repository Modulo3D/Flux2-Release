using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class SettingsViewModel : FluxRoutableNavBarViewModel<SettingsViewModel>
    {
        [RemoteInput]
        public SelectableCache<Optional<Printer>, int> Printers { get; }

        [RemoteInput]
        public SelectableCache<Optional<(IPAddress address, int id)>, int> HostAddress { get; }

        private Optional<string> _PlcAddress = "";
        [RemoteInput]
        public Optional<string> PlcAddress
        {
            get => _PlcAddress;
            set => this.RaiseAndSetIfChanged(ref _PlcAddress, value);
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

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> SaveSettingsCommand { get; }

        public SettingsViewModel(FluxViewModel flux) : base(flux)
        {
            var database_changed = flux.DatabaseProvider.WhenAnyValue(v => v.Database);

            var printer_cache = database_changed.Select(FindPrinters)
                .ToObservableChangeSet(p => p.ConvertOr(p => p.Id, () => 0));
            Printers = SelectableCache.Create(printer_cache);

            var host = Dns.GetHostEntry(Dns.GetHostName());
            var host_address_cache = host.AddressList.Select((ip, id) => (ip, id).ToOptional())
                .AsObservableChangeSet(t => t.ConvertOr(t => t.id, () => -1));
            HostAddress = SelectableCache.Create(host_address_cache);

            var user_settings = Flux.SettingsProvider.UserSettings.Local;
            var core_settings = Flux.SettingsProvider.CoreSettings.Local;

            core_settings.WhenAnyValue(s => s.PrinterID)
                .BindTo(this, v => v.Printers.SelectedKey);

            core_settings.WhenAnyValue(s => s.HostID)
                .BindTo(this, v => v.HostAddress.SelectedKey);

            core_settings.WhenAnyValue(s => s.PLCAddress)
                .BindTo(this, v => v.PlcAddress);

            core_settings.WhenAnyValue(s => s.WebcamAddress)
                .BindTo(this, v => v.WebcamAddress);

            user_settings.WhenAnyValue(s => s.PrinterName)
                .BindTo(this, v => v.PrinterName);

            user_settings.WhenAnyValue(s => s.CostHour)
                .BindTo(this, v => v.CostHour);

            var canSave = Printers.SelectedValueChanged
                .Select(p => p.HasValue);

            SaveSettingsCommand = ReactiveCommand.Create(SaveSettings, canSave);
        }

        public void SaveSettings()
        {
            try
            {
                var user_settings = Flux.SettingsProvider.UserSettings.Local;
                var core_settings = Flux.SettingsProvider.CoreSettings.Local;

                core_settings.PrinterID = Printers.SelectedValue.Convert(v => v).ConvertOr(p => p.Id, () => 0);
                core_settings.HostID = HostAddress.SelectedValue.Convert(v => v).ConvertOr(p => p.id, () => 0);
                user_settings.CostHour = CostHour;
                core_settings.PLCAddress = PlcAddress;
                user_settings.PrinterName = PrinterName;
                core_settings.WebcamAddress = WebcamAddress;

                if (!Flux.SettingsProvider.PersistLocalSettings())
                    return;
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
            }
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
