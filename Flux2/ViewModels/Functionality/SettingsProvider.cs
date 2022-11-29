using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Runtime.InteropServices;

namespace Flux.ViewModels
{
    public class SettingsProvider : ReactiveObject, IFluxSettingsProvider
    {
        public FluxViewModel Flux { get; }

        private readonly ObservableAsPropertyHelper<Optional<Printer>> _Printer;
        public Optional<Printer> Printer => _Printer.Value;

        private readonly ObservableAsPropertyHelper<Optional<IPAddress>> _HostAddress;
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

        private readonly ObservableAsPropertyHelper<Optional<(ushort machine_extruders, ushort mixing_extruders)>> _ExtrudersCount;
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
                    var machine_extruder_count = p[p => p.MachineExtruderCount, 0];
                    var mixing_extruder_count = p[p => p.MixingExtruderCount, 0];
                    return (machine_extruder_count, mixing_extruder_count);
                })
                .ToProperty(this, v => v.ExtrudersCount);

            UserSettings.Local.WhenAnyValue(s => s.StandbyMinutes)
                .Subscribe(s =>
                {
                    try
                    {
                        if (!s.HasValue)
                            return;

                        var seconds = (int)TimeSpan.FromMinutes(s.Value).TotalSeconds;
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        {
                            using var process = new Process
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    UseShellExecute = true,
                                    FileName = "/bin/bash",
                                    Arguments = $"-c \"xset s {seconds} {seconds}\"",
                                }
                            };
                            process.Start();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                });
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

        // GET EXTRUDERS
        public bool PersistLocalSettings()
        {
            var result = CoreSettings.PersistLocalSettings() && UserSettings.PersistLocalSettings();
            Flux.Messages.LogMessage("Salvataggio impostazioni", result ? "Impostazioni salvate" : "Errore di salvataggio", result ? MessageLevel.DEBUG : MessageLevel.ERROR, 0);
            return result;
        }
    }
}
