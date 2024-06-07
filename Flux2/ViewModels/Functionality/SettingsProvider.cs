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
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class SettingsProvider : ReactiveObjectRC<SettingsProvider>, IFluxSettingsProvider
    {
        public FluxViewModel Flux { get; }

        private readonly ObservableAsPropertyHelper<Optional<Printer>> _Printer;
        public Optional<Printer> Printer => _Printer.Value;

        private readonly ObservableAsPropertyHelper<Optional<IPEndPoint>> _FluxEndpoint;
        public Optional<IPEndPoint> FluxEndPoint => _FluxEndpoint.Value;

        private readonly ObservableAsPropertyHelper<Optional<IPEndPoint>> _PLCEndPoint;
        public Optional<IPEndPoint> PLCEndPoint => _PLCEndPoint.Value;
        
        private readonly ObservableAsPropertyHelper<Optional<int>> _PassthroughPort;
        public Optional<int> PassthroughPort => _PassthroughPort.Value;

        private readonly ObservableAsPropertyHelper<Optional<int>> _VPNPort;
        public Optional<int> VPNPort => _VPNPort.Value;

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

        private LocalSettingsProvider<FluxMemorySettings> _MemorySettings;
        public LocalSettingsProvider<FluxMemorySettings> MemorySettings
        {
            get
            {
                if (_MemorySettings == default)
                    _MemorySettings = new LocalSettingsProvider<FluxMemorySettings>(Files.MemorySettings);
                return _MemorySettings;
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
                .SelectAsync()
                .ToPropertyRC(this, v => v.Printer);

            HostAddressCache = NetworkInterface.GetAllNetworkInterfaces()
                .AsObservableChangeSet(nic => nic.Id)
                .Transform(nic => nic.GetIPProperties().ToOptional())
                .Transform(ip => ip.Convert(ip => ip.UnicastAddresses))
                .Transform(i => i.Convert(i => i.Select(a => a.Address)))
                .Transform(a => a.Convert(a => a.FirstOrOptional(a => a.AddressFamily == AddressFamily.InterNetwork)))
                .Filter(ip => ip.HasValue)
                .Transform(ip => ip.Value)
                .AsObservableCacheRC(this);

            _FluxEndpoint = Observable.CombineLatest(
                HostAddressCache.Connect().QueryWhenChanged(),
                CoreSettings.Local.WhenAnyValue(s => s.HostID),
                CoreSettings.Local.WhenAnyValue(s => s.HostPort),
                (host_address_cache, host_id, host_port) =>
                {
                    if (!host_id.HasValue)
                        return Optional<IPEndPoint>.None;
                    if (!host_port.HasValue)
                        return Optional<IPEndPoint>.None;
                    var flux_address = host_address_cache.Lookup(host_id.Value);
                    if (!flux_address.HasValue)
                        return Optional<IPEndPoint>.None;
                    return new IPEndPoint(flux_address.Value, host_port.Value);
                })
                .ToPropertyRC(this, v => v.FluxEndPoint);

            _PassthroughPort = CoreSettings.Local.WhenAnyValue(s => s.PassthroughPort)
               .ToPropertyRC(this, v => v.PassthroughPort);

            _VPNPort = CoreSettings.Local.WhenAnyValue(s => s.VPNPort)
               .ToPropertyRC(this, v => v.VPNPort);

            _PLCEndPoint = CoreSettings.Local.WhenAnyValue(s => s.PLCAddress)
                .Convert(address => address.ParseAsIPv4EndPoint())
                .ToPropertyRC(this, v => v.PLCEndPoint);

            _ExtrudersCount = this.WhenAnyValue(v => v.Printer)
                .Convert(p =>
                {
                    var machine_extruder_count = p[p => p.MachineExtruderCount];
                    var mixing_extruder_count = p[p => p.MixingExtruderCount];
                    return (machine_extruder_count, mixing_extruder_count);
                })
                .ToPropertyRC(this, v => v.ExtrudersCount);

            UserSettings.Local.WhenAnyValue(s => s.StandbyMinutes)
                .SubscribeRC(async s =>
                {
                    var seconds = (int)TimeSpan.FromMinutes(s.Value).TotalSeconds;
                    await ProcessUtils.RunLinuxCommandsAsync("DISPLAY=:0 xset s reset", $"DISPLAY=:0 xset s {seconds}");
                }, this);
        }

        private async Task<Optional<Printer>> FindPrinter(Optional<ILocalDatabase> database, Optional<int> printer_id)
        {
            if (!database.HasValue)
                return default;
            if (!printer_id.HasValue)
                return default;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var printers = await database.Value.FindByIdAsync<Printer>(printer_id.Value, cts.Token);
            if (!printers.HasDocuments)
                return default;
            return printers.FirstOrDefault();
        }

        // GET EXTRUDERS
        public bool PersistLocalSettings()
        {
            var result = CoreSettings.PersistLocalSettings() && UserSettings.PersistLocalSettings() && MemorySettings.PersistLocalSettings();
            // Flux.Messages.LogMessage("Salvataggio impostazioni", result ? "Impostazioni salvate" : "Errore di salvataggio", result ? MessageLevel.DEBUG : MessageLevel.ERROR, 0);
            return result;
        }
    }
}
