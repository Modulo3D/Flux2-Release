using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.IO.Ports;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class NFCProvider : ReactiveObject, IFluxNFCProvider
    {
        public FluxViewModel Flux { get; }
        public ISourceCache<(INFCReader reader, NFCDeviceInformations info), string> ComReaders { get; }

        private Optional<(INFCReader reader, NFCDeviceInformations info)> _SimpleReader;
        public Optional<(INFCReader reader, NFCDeviceInformations info)> SimpleReader 
        {
            get => _SimpleReader;
            private set => this.RaiseAndSetIfChanged(ref _SimpleReader, value);
        }

        public NFCProvider(FluxViewModel flux)
        {
            Flux = flux;
            ComReaders = new SourceCache<(INFCReader reader, NFCDeviceInformations info), string>(t => t.info.SerialDescription);

            Task.Run(UpdateReadersAsync);
        }

        public async Task UpdateReadersAsync()
        {
            ComReaders.Clear();
            var ports = SerialPort.GetPortNames();
            foreach (var port in ports)
            {
                var reader = await NFCReader.GetReaderAsync(port, TimeSpan.FromSeconds(5));
                if (reader.HasValue)
                    ComReaders.AddOrUpdate(reader.Value);
            }

            if (ComReaders.Count == 0)
            {
                var reader = await NFCReader.GetReaderAsync(TimeSpan.FromSeconds(5));
                if (reader.HasValue)
                    ComReaders.AddOrUpdate(reader.Value);
            }

            SimpleReader = await NFCReader.GetReaderAsync(TimeSpan.FromSeconds(5));
        }

        public IObservable<Optional<INFCReader>> GetToolReader()
        {
            var settings = Flux.SettingsProvider.CoreSettings.Local;

            var serial_description = settings.WhenAnyValue(s => s.Tool)
                .Convert(t => t.SerialDescription);

            return Observable.CombineLatest(
                serial_description,
                this.WhenAnyValue(v => v.SimpleReader),
                (sd, sh) =>
                {
                    if (!sd.HasValue)
                        return default;
                    if (!sh.HasValue)
                        return default;
                    if (sd.Value != sh.Value.info.SerialDescription)
                        return default;
                    return sh.Value.ToOptional();
                })
                .ValueOrOptional(() => ComReaders.Connect().WatchOptional(serial_description))
                .Convert(t => t.reader);
        }
        public IObservable<Optional<INFCReader>> GetMaterialReader(ushort position)
        {
            var settings = Flux.SettingsProvider.CoreSettings.Local;

            var serial_description = settings.Feeders.Connect()
                .WatchOptional(position)
                .Convert(t => t.SerialDescription);

            return Observable.CombineLatest(
                serial_description,
                this.WhenAnyValue(v => v.SimpleReader),
                (sd, sh) =>
                {
                    if (!sd.HasValue)
                        return default;
                    if (!sh.HasValue)
                        return default;
                    if (sd.Value != sh.Value.info.SerialDescription)
                        return default;
                    return sh.Value.ToOptional();
                })
                .ValueOrOptional(() => ComReaders.Connect().WatchOptional(serial_description))
                .Convert(t => t.reader);
        }
    }
}
