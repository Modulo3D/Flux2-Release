using DynamicData;
using DynamicData.Kernel;
using Modulo3DDatabase;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public abstract class TagViewModel<TTagViewModel, TNFCTag, TDocument, TState> : RemoteControl<TTagViewModel>, IFluxTagViewModel<TNFCTag, TDocument, TState>
        where TTagViewModel : TagViewModel<TTagViewModel, TNFCTag, TDocument, TState>
        where TNFCTag : INFCOdometerTag<TNFCTag>
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

        private bool PauseOnOdometer { get; }

        public TagViewModel(
            FeedersViewModel feeders, FeederViewModel feeder, ushort position,
            Func<IFluxFeedersViewModel, INFCStorage<TNFCTag>> get_tag_storage,
            Func<ILocalDatabase, TNFCTag, TDocument> find_document,
            Func<TNFCTag, Guid> check_tag, bool pause_on_odometer) : base($"{typeof(TTagViewModel).GetRemoteControlName()}??{position}")
        {
            Feeder = feeder;
            Feeders = feeders;
            Flux = feeders.Flux;
            Position = position;
            CheckTag = check_tag;
            PauseOnOdometer = pause_on_odometer;

            var virtual_card_id = Flux.MCodes
                .WhenAnyValue(m => m.OperatorUSB)
                .Convert(o => o.RewriteNFC)
                .Convert(r => r ? $"00-00-{VirtualTagId:00}-{Position:00}" : "")
                .Convert(k => new CardId(k));

            var storage = get_tag_storage(feeders);
            NFCSlot = new NFCSlot<TNFCTag>(storage, position, virtual_card_id);

            _Document = Observable.CombineLatest(
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                NFCSlot.WhenAnyValue(v => v.Nfc),
                (db, nfc) => (db, nfc))
                .Select(tuple =>
                {
                    if (!tuple.db.HasValue)
                        return default;
                    if (!tuple.nfc.Tag.HasValue)
                        return default;
                    return find_document(tuple.db.Value, tuple.nfc.Tag.Value);
                })
                .ToProperty(this, vm => vm.Document)
                .DisposeWith(Disposables);

            UpdateTagCommand = ReactiveCommand.CreateFromTask(async () => { await Flux.UseReader(this, (h, s) => s.UpdateTagAsync(h)); },
                Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation).Select(s => s.CanSafeCycle))
                .DisposeWith(Disposables);

            NFCSlot.RestoreBackupTag();
        }

        public virtual void Initialize() 
        {
            var multiplier = Observable.Return(1.0);
            Odometer = new OdometerViewModel<TNFCTag>(this, multiplier);

            _OdometerPercentage = Odometer.WhenAnyValue(v => v.Percentage)
                .ToProperty(this, v => v.OdometerPercentage)
                .DisposeWith(Disposables);

            _RemainingWeight = Odometer.WhenAnyValue(v => v.CurrentValue)
                .ToProperty(this, v => v.RemainingWeight)
                .DisposeWith(Disposables);

            NFCSlot.UnlockingTag
                .Subscribe(async _ => await Odometer.OdometerManager.StoreCurrentWeightsAsync())
                .DisposeWith(Disposables);

            if (PauseOnOdometer)
            {
                NFCSlot.WhenAnyValue(s => s.Nfc)
                    .Where(nfc => nfc.HasTag)
                    .Where(nfc => nfc.Tag.Value.CurWeightG.HasValue)
                    .Where(nfc => nfc.Tag.Value.CurWeightG.Value <= 0)
                    .Subscribe(_ => Flux.ConnectionProvider.PausePrintAsync(true, true))
                    .DisposeWith(Disposables);
            }
        }       
    }
}
