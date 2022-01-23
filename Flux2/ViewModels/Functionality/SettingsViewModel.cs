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
        [RemoteContent(false)]
        public ToolNozzleNFCSettings ToolNozzleNFCSettings { get; }

        [RemoteContent(true)]
        public IObservableCache<MaterialNFCSettings, ushort> MaterialNFCSettings { get; }

        [RemoteInput]
        public SelectableCache<Printer, int> Printers { get; }

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
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> LoadSettingsCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> UpdateNFCReadersCommand { get; }

        public SettingsViewModel(FluxViewModel flux) : base(flux)
        {
            var database_changed = flux.DatabaseProvider.WhenAnyValue(v => v.Database);

            var cache = database_changed.Select(FindPrinters)
                .ToObservableChangeSet(p => p.ConvertOr(p => p.Id, () => 0));

            Printers = new SelectableCache<Printer, int>(cache);

            ToolNozzleNFCSettings = FindToolLoaderSettings();
            MaterialNFCSettings = Printers.SelectedValueChanged.Select(FindFeederSettings)
                .ToObservableChangeSet(selector => selector.Position)
                .AsObservableCache();

            LoadSettings();

            var canSave = Printers.SelectedValueChanged.Convert(p => p.Id).ConvertOr(id => id > 0, () => false);
            LoadSettingsCommand = ReactiveCommand.Create(LoadSettings);
            UpdateNFCReadersCommand = ReactiveCommand.CreateFromTask(UpdateNFCReadersAsync);
            SaveSettingsCommand = ReactiveCommand.Create(SaveSettings, canSave);
        }

        private async Task UpdateNFCReadersAsync()
        {
            await Flux.NFCProvider.UpdateReadersAsync();
        }

        public void LoadSettings()
        {
            var user_settings = Flux.SettingsProvider.UserSettings.Local;
            var core_settings = Flux.SettingsProvider.CoreSettings.Local;

            core_settings.WhenAnyValue(s => s.PrinterID)
                .BindTo(this, v => v.Printers.SelectedKey);

            core_settings.WhenAnyValue(s => s.PLCAddress)
                .BindTo(this, v => v.PlcAddress);

            core_settings.WhenAnyValue(s => s.WebcamAddress)
                .BindTo(this, v => v.WebcamAddress);

            user_settings.WhenAnyValue(s => s.PrinterName)
                .BindTo(this, v => v.PrinterName);

            user_settings.WhenAnyValue(s => s.CostHour)
                .BindTo(this, v => v.CostHour);

            if (core_settings.Tool.HasValue)
            {
                core_settings.WhenAnyValue(s => s.Tool)
                    .Where(t => t.HasValue)
                    .Select(t => t.Value.SerialDescription)
                    .Throttle(TimeSpan.FromSeconds(0.5))
                    .BindTo(this, v => v.ToolNozzleNFCSettings.NFCReaders.SelectedKey);
            }
            foreach (var feeder_settings_vm in MaterialNFCSettings.Items)
            {
                core_settings.Feeders.Connect()
                    .WatchOptional(feeder_settings_vm.Position)
                    .Where(f => f.HasValue)
                    .Select(f => f.Value.SerialDescription)
                    .BindTo(feeder_settings_vm, v => v.NFCReaders.SelectedKey);
            }
        }
        public void SaveSettings()
        {
            try
            {
                var user_settings = Flux.SettingsProvider.UserSettings.Local;
                var core_settings = Flux.SettingsProvider.CoreSettings.Local;

                core_settings.PrinterID = Printers.SelectedValue.ConvertOr(p => p.Id, () => 0);
                user_settings.CostHour = CostHour;
                core_settings.PLCAddress = PlcAddress;
                user_settings.PrinterName = PrinterName;
                core_settings.WebcamAddress = WebcamAddress;

                // Save Tool
                if (!core_settings.Tool.HasValue)
                    core_settings.Tool = new NFCToolNozzleSettings();

                core_settings.Tool.IfHasValue(t =>
                {
                    t.SerialDescription = ToolNozzleNFCSettings.NFCReaders.SelectedKey;
                });

                // Save Lines
                foreach (var feeder_settings_vm in MaterialNFCSettings.Items)
                {
                    var feeder_lookup = core_settings.Feeders.Lookup(feeder_settings_vm.Position);
                    if (!feeder_lookup.HasValue)
                        core_settings.Feeders.AddOrUpdate(new NFCMaterialSettings(feeder_settings_vm.Position));

                    feeder_lookup = core_settings.Feeders.Lookup(feeder_settings_vm.Position);
                    feeder_lookup.IfHasValue(f =>
                    {
                        f.SerialDescription = feeder_settings_vm.NFCReaders.SelectedKey;
                    });
                }

                if (!Flux.SettingsProvider.PersistLocalSettings())
                    return;
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
            }
        }

        private ToolNozzleNFCSettings FindToolLoaderSettings()
        {
            var tool_setting_vm = new ToolNozzleNFCSettings(Flux);
            var tool_setting = Flux.SettingsProvider.CoreSettings.Local.Tool;
            if (tool_setting.HasValue)
            {
                var tool_reader = tool_setting.Value.SerialDescription;
                if (tool_reader.HasValue)
                    tool_setting_vm.NFCReaders.SelectedKey = tool_reader.Value;
            }
            return tool_setting_vm;
        }
        private IEnumerable<Optional<Printer>> FindPrinters(Optional<ILocalDatabase> database)
        {
            var printers = database.Convert(db => db.FindAll<Printer>());
            if (!printers.HasValue)
                yield break;

            foreach (var printer in printers.Value.Documents)
                yield return printer;
        }
        private IEnumerable<MaterialNFCSettings> FindFeederSettings(Optional<Printer> printer)
        {
            var feeder_settings_list = new List<MaterialNFCSettings>();
            if (!printer.HasValue)
                return feeder_settings_list;

            var machine_extruder_count = printer.Value["machine_extruder_count"].TryGetValue<ushort>();
            if (!machine_extruder_count.HasValue)
                return feeder_settings_list;

            for (ushort extruder = 0; extruder < machine_extruder_count.Value; extruder++)
            {
                var feeders_settings_cache = Flux.SettingsProvider.CoreSettings.Local.Feeders;
                var feeder_settings_vm = new MaterialNFCSettings(Flux, extruder);
                var feeder_settings_lookup = feeders_settings_cache.Lookup(extruder);
                if (feeder_settings_lookup.HasValue)
                {
                    var feeder_reader = feeder_settings_lookup.Value.SerialDescription;
                    if (feeder_reader.HasValue)
                        feeder_settings_vm.NFCReaders.SelectedKey = feeder_reader.Value;
                }

                feeder_settings_list.Add(feeder_settings_vm);
            }

            return feeder_settings_list;
        }
    }
}
