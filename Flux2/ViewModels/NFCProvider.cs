using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.IO.Ports;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class NFCProvider : ReactiveObject, IFluxNFCProvider
    {
        public FluxViewModel Flux { get; }
        public ISourceCache<NFCReaderHandle, string> Readers { get; }

        public NFCProvider(FluxViewModel flux)
        {
            Flux = flux;
            Readers = new SourceCache<NFCReaderHandle, string>(r => r.Info.SerialDescription);

            Task.Run(UpdateReadersAsync);
        }

        public async Task UpdateReadersAsync()
        {
            Readers.Clear();
            var ports = SerialPort.GetPortNames();
            foreach (var port in ports)
            {
                var reader = await NFCReaderHandle.GetReaderAsync(port, TimeSpan.FromSeconds(5));
                if (reader.HasValue)
                    Readers.AddOrUpdate(reader.Value);
            }

            if (Readers.Count == 0)
            {
                var reader = await NFCReaderHandle.GetReaderAsync(TimeSpan.FromSeconds(5));
                if (reader.HasValue)
                    Readers.AddOrUpdate(reader.Value);
            }
        }

        public IObservable<Optional<NFCReaderHandle>> GetToolReader()
        {
            var settings = Flux.SettingsProvider.CoreSettings.Local;

            var serial_description = settings.WhenAnyValue(s => s.Tool)
                .Convert(t => t.SerialDescription);

            return Readers.Connect().WatchOptional(serial_description);
        }
        public IObservable<Optional<NFCReaderHandle>> GetMaterialReader(ushort position)
        {
            var settings = Flux.SettingsProvider.CoreSettings.Local;

            var serial_description = settings.Feeders.Connect()
                .WatchOptional(position)
                .Convert(t => t.SerialDescription);

            return Readers.Connect().WatchOptional(serial_description);
        }
    }
}
