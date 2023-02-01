using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public abstract class TagViewModel<TTagViewModel, TNFCTag, TDocument, TState> : RemoteControl<TTagViewModel>, IFluxTagViewModel<TNFCTag, TDocument, TState>
        where TTagViewModel : TagViewModel<TTagViewModel, TNFCTag, TDocument, TState>
        where TNFCTag : INFCOdometerTag<TNFCTag>, new()
    {
        public ushort Position { get; }
        public FluxViewModel Flux { get; }
        IFlux IFluxTagViewModel.Flux => Flux;
        public abstract TState State { get; }
        public FeederViewModel Feeder { get; }
        public FeedersViewModel Feeders { get; }
        public Func<TNFCTag, Guid> CheckTag { get; }
        public abstract ushort VirtualTagId { get; }
        public OdometerViewModel<TNFCTag> Odometer { get; private set; }
        IOdometerViewModel<TNFCTag> IFluxTagViewModel<TNFCTag>.Odometer => Odometer;

        private readonly ObservableAsPropertyHelper<TDocument> _Document;
        public TDocument Document => _Document.Value;


        [RemoteOutput(true)]
        public abstract Optional<string> DocumentLabel { get; }

        private ObservableAsPropertyHelper<double> _OdometerPercentage;
        [RemoteOutput(true)]
        public double OdometerPercentage => _OdometerPercentage.Value;

        private ObservableAsPropertyHelper<Optional<double>> _RemainingWeight;
        [RemoteOutput(true, typeof(WeightConverter))]
        public Optional<double> RemainingWeight => _RemainingWeight.Value;

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> UpdateTagCommand { get; internal set; }

        public NFCSlot<TNFCTag> NFCSlot { get; }
        INFCSlot IFluxTagViewModel.NFCSlot => NFCSlot;

        IFluxFeederViewModel IFluxTagViewModel.Feeder => Feeder;
        IFluxFeedersViewModel IFluxTagViewModel.Feeders => Feeders;

        public bool WatchOdometerForPause { get; set; }

        public TagViewModel(
            FeedersViewModel feeders, FeederViewModel feeder, ushort position,
            Func<IFluxFeedersViewModel, INFCStorage<TNFCTag>> get_tag_storage,
            Func<ILocalDatabase, TNFCTag, Task<TDocument>> find_document,
            Func<TNFCTag, Guid> check_tag, bool watch_odometer_for_pause) : base($"{typeof(TTagViewModel).GetRemoteControlName()}??{position}")
        {
            Feeder = feeder;
            Feeders = feeders;
            Flux = feeders.Flux;
            Position = position;
            CheckTag = check_tag;
            WatchOdometerForPause = watch_odometer_for_pause;

            var virtual_card_id = Flux.MCodes
                .WhenAnyValue(m => m.OperatorUSB)
                .Convert(o => o.RewriteNFC)
                .Convert(r => r ? $"00-00-{VirtualTagId:00}-{Position:00}" : "")
                .Convert(k => new CardId(k));

            var storage = get_tag_storage(feeders);
            NFCSlot = new NFCSlot<TNFCTag>(feeder.Flux, storage, position, virtual_card_id);

            _Document = Observable.CombineLatest(
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                NFCSlot.WhenAnyValue(v => v.Nfc),
                (db, nfc) => (db, nfc))
                .Select(tuple =>
                {
                    if (!tuple.db.HasValue)
                        return Task.FromResult(default(TDocument));
                    if (!tuple.nfc.Tag.HasValue)
                        return Task.FromResult(default(TDocument));
                    return find_document(tuple.db.Value, tuple.nfc.Tag.Value);
                })
                .SelectAsync()
                .ToPropertyRC((TTagViewModel)this, vm => vm.Document);

            UpdateTagCommand = ReactiveCommandRC.CreateFromTask(async () => 
            { 
                await Flux.UseReader(this, (h, s, c) => 
                    s.UpdateTagAsync(h, c), r => r == NFCTagRW.Success); 
            }, (TTagViewModel)this, Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation).Select(s => s.CanSafeCycle));

            NFCSlot.RestoreBackupTag();
        }

        public virtual void Initialize()
        {
            var multiplier = Observable.Return(1.0);
            Odometer = new OdometerViewModel<TNFCTag>(this, multiplier);

            _OdometerPercentage = Odometer.WhenAnyValue(v => v.Percentage)
                .ToPropertyRC((TTagViewModel)this, v => v.OdometerPercentage);

            _RemainingWeight = Odometer.WhenAnyValue(v => v.CurrentValue)
                .ToPropertyRC((TTagViewModel)this, v => v.RemainingWeight);

            NFCSlot.UnlockingTag
                .SubscribeRC(async _ => await Odometer.OdometerManager.StoreCurrentWeightsAsync(), (TTagViewModel)this);

            if (WatchOdometerForPause)
            {
                var pause_on_empty_odometer = Flux.SettingsProvider.UserSettings.Local
                    .WhenAnyValue(c => c.PauseOnEmptyOdometer);

                var nfc = NFCSlot.WhenAnyValue(s => s.Nfc);

                Observable.CombineLatest(pause_on_empty_odometer, nfc, (pause_on_empty_odometer, nfc) =>
                    {
                        if (!pause_on_empty_odometer)
                            return false;
                        if (!nfc.HasTag)
                            return false;
                        if (!nfc.Tag.Value.CurWeightG.HasValue)
                            return false;
                        if (nfc.Tag.Value.CurWeightG.Value > -50)
                            return false;
                        return true;
                    })
                    .Where(has_pause => has_pause)
                    .SubscribeRC(_ => Flux.ConnectionProvider.PausePrintAsync(true), (TTagViewModel)this);
            }
        }
    }
}
