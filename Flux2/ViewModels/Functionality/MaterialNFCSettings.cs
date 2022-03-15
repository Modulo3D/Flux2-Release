using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public abstract class NFCSettingsViewModel<T> : RemoteControl<T>
        where T : NFCSettingsViewModel<T>
    {
        public IFlux Flux { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> TweetReaderCommand { get; }

        [RemoteInput]
        public SelectableCache<(INFCReader reader, NFCDeviceInformations info), string> NFCReaders { get; }

        public NFCSettingsViewModel(IFlux flux, string name) : base(name)
        {
            Flux = flux;
            var cache = flux.NFCProvider.ComReaders.Connect()
                .Transform(reader => reader.ToOptional());

            NFCReaders = new SelectableCache<(INFCReader reader, NFCDeviceInformations info), string>(cache);
            NFCReaders.StartAutoSelect(q =>
            {
                return q.KeyValues.FirstOrOptional(kvp => kvp.Value.HasValue)
                    .Convert(kvp => kvp.Key);
            });

            var can_tweet = NFCReaders.SelectedValueChanged
                .Select(nfc => nfc.HasValue);
            TweetReaderCommand = ReactiveCommand.CreateFromTask(TweetReaderAsync, can_tweet);
        }

        private async Task TweetReaderAsync()
        {
            if (!NFCReaders.SelectedValue.HasValue)
                return;
            var nfc_reader = NFCReaders.SelectedValue.Value;
            await nfc_reader.reader.OpenAsync(h => h.ReaderUISignal(LightSignalMode.Flash, BeepSignalMode.TripleShort), TimeSpan.FromSeconds(1));
        }
    }

    public class MaterialNFCSettings : NFCSettingsViewModel<MaterialNFCSettings>, IFeederSettingsViewModel
    {
        [RemoteOutput(false)]
        public ushort Position { get; }
        public MaterialNFCSettings(FluxViewModel flux, ushort position) : base(flux, $"materialNFCSettings??{position}")
        {
            Position = position;
            var settings = flux.SettingsProvider.CoreSettings.Local;
            settings.Feeders.Connect()
                .WatchOptional(position)
                .Convert(t => t.SerialDescription)
                .BindTo(this, v => v.NFCReaders.SelectedKey);
        }
    }

    public class ToolNozzleNFCSettings : NFCSettingsViewModel<ToolNozzleNFCSettings>, IReaderSettingsViewModel
    {
        public ToolNozzleNFCSettings(FluxViewModel flux) : base(flux, "toolNozzleNFCSettings")
        {
            var settings = flux.SettingsProvider.CoreSettings.Local;
            settings.WhenAnyValue(v => v.Tool)
                .Convert(t => t.SerialDescription)
                .BindTo(this, v => v.NFCReaders.SelectedKey);
        }
    }
}
