using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class SettingsProvider : ReactiveObject, IFluxSettingsProvider
    {
        public FluxViewModel Flux { get; }

        private ObservableAsPropertyHelper<Optional<Printer>> _Printer;
        public Optional<Printer> Printer => _Printer.Value;

        private ObservableAsPropertyHelper<Optional<IPAddress>> _HostAddress;
        public Optional<IPAddress> HostAddress => _HostAddress.Value;

        private LocalSettingsProvider<FluxCoreSettings> _CoreSettings;
        public LocalSettingsProvider<FluxCoreSettings> CoreSettings
        {
            get
            {
                if (_CoreSettings == default)
                    _CoreSettings = new LocalSettingsProvider<FluxCoreSettings>(Files.CoreSettings);
                return _CoreSettings;
            }
        }

        private LocalSettingsProvider<FluxUserSettings> _UserSettings;
        public LocalSettingsProvider<FluxUserSettings> UserSettings
        {
            get
            {
                if (_UserSettings == default)
                    _UserSettings = new LocalSettingsProvider<FluxUserSettings>(Files.UserSettings);
                return _UserSettings;
            }
        }

        private ObservableAsPropertyHelper<Optional<(ushort machine_extruders, ushort mixing_extruders)>> _ExtrudersCount;
        public Optional<(ushort machine_extruders, ushort mixing_extruders)> ExtrudersCount => _ExtrudersCount.Value;

        public IObservableCache<IPAddress, string> HostAddressCache { get; }

        public SettingsProvider(FluxViewModel flux)
        {
            Flux = flux;

            _Printer = Observable.CombineLatest(
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                CoreSettings.Local.WhenAnyValue(s => s.PrinterID),
                FindPrinter)
                .ToProperty(this, v => v.Printer);

            HostAddressCache = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.SupportsMulticast)
                .AsObservableChangeSet(nic => nic.Id)
                .Transform(nic => nic.GetIPProperties().ToOptional())
                .Transform(ip => ip.Convert(ip => ip.UnicastAddresses))
                .Transform(i => i.Convert(i => i.Select(a => a.Address)))
                .Transform(a => a.Convert(a => a.FirstOrOptional(a => a.AddressFamily == AddressFamily.InterNetwork)))
                .Filter(ip => ip.HasValue)
                .Transform(ip => ip.Value)
                .AsObservableCache();

            _HostAddress = CoreSettings.Local.WhenAnyValue(s => s.HostID)
                .Convert(id => HostAddressCache.Lookup(id))
                .ToProperty(this, v => v.HostAddress);

            _ExtrudersCount = this.WhenAnyValue(v => v.Printer)
                .Convert(p => 
                {
                    if (!p.MachineExtruderCount.HasValue)
                        return default;
                    if (!p.MixingExtruderCount.HasValue)
                        return default;
                    return (p.MachineExtruderCount.Value, p.MixingExtruderCount.Value);
                })
                .ToProperty(this, v => v.ExtrudersCount);
        }

        private Optional<Printer> FindPrinter(Optional<ILocalDatabase> database, Optional<int> printer_id)
        {
            if (!database.HasValue)
                return default;
            if (!printer_id.HasValue)
                return default;
            var printers = database.Value.FindById<Printer>(printer_id.Value);
            if (!printers.HasDocuments)
                return default;
            return printers.Documents.FirstOrDefault();
        }

        public async Task<bool> ResetMagazineAsync()
        {
            if (!ExtrudersCount.HasValue)
            {
                Flux.Messages.LogMessage("Errore resetta magazzino", $"Stampante non configurata", MessageLevel.ERROR, 0);
                return false;
            }

            var enable_drivers = Flux.ConnectionProvider.GetVariables(m => m.ENABLE_DRIVERS);
            foreach (var variable in enable_drivers.Items)
            {
                if (!await variable.WriteAsync(false))
                {
                    Flux.Messages.LogMessage("Errore resetta magazzino", $"Asse {variable.Unit} non disabilitato", MessageLevel.ERROR, 0);
                    return false;
                }
            }

            var result = await Flux.ShowConfirmDialogAsync("Riponi l'utensile selezionato", "Se è presente un utensile sul carrello, riporlo manualmente prima di continuare");
            if (result != ContentDialogResult.Primary)
                return true;

            if (!await Flux.ConnectionProvider.ResetClampAsync())
            {
                Flux.Messages.LogMessage("Errore resetta magazzino", "Pinza non resettata", MessageLevel.ERROR, 0);
                return false;
            }

            if (!await Flux.ConnectionProvider.WriteVariableAsync(m => m.IN_CHANGE, false))
            {
                Flux.Messages.LogMessage("Errore resetta magazzino", "Utensile non deselezionato", MessageLevel.ERROR, 0);
                return false;
            }

            var tool_on_trailer = Flux.ConnectionProvider.GetVariables(m => m.MEM_TOOL_ON_TRAILER);
            if (tool_on_trailer.HasValue)
            {
                foreach (var variable in tool_on_trailer.Value.Items)
                {
                    if (!await variable.WriteAsync(false))
                    {
                        Flux.Messages.LogMessage("Errore resetta magazzino", "Tool sul carrello", MessageLevel.ERROR, 0);
                        return false;
                    }
                }
            }

            var tool_on_magazine = Flux.ConnectionProvider.GetVariables(m => m.MEM_TOOL_IN_MAGAZINE);
            if (tool_on_magazine.HasValue)
            {
                foreach (var variable in tool_on_magazine.Value.Items)
                {
                    if (!await variable.WriteAsync(false))
                    {
                        Flux.Messages.LogMessage("Errore resetta magazzino", "Tool nel magazzino", MessageLevel.ERROR, 0);
                        return false;
                    }
                }
            }

            for (ushort extruder = 0; extruder < ExtrudersCount.Value.machine_extruders; extruder++)
            {
                var extr_key = Flux.ConnectionProvider.GetArrayUnit(m => m.MEM_TOOL_IN_MAGAZINE, extruder);
                if (!extr_key.HasValue)
                    return false;

                if (!await Flux.ConnectionProvider.WriteVariableAsync(m => m.MEM_TOOL_IN_MAGAZINE, extr_key.Value.Alias, true))
                {
                    Flux.Messages.LogMessage("Errore resetta magazzino", "Tool non nel magazzino", MessageLevel.ERROR, 0);
                    return false;
                }
            }

            return await Flux.ConnectionProvider.ResetAsync();
        }

        // GET EXTRUDERS
        public bool PersistLocalSettings()
        {
            var result = CoreSettings.PersistLocalSettings() && UserSettings.PersistLocalSettings();
            Flux.Messages.LogMessage("Salvataggio impostazioni", result ? "Impostazioni salvate" : "Errore di salvataggio", result ? MessageLevel.DEBUG : MessageLevel.ERROR, 0);
            return result;
        }
    }
}
