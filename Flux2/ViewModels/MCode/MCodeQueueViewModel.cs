using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;

namespace Flux.ViewModels
{
    public class MCodeQueueViewModel : RemoteControl<MCodeQueueViewModel>, IFluxMCodeQueueViewModel
    {
        public MCodesViewModel MCodes { get; }

        private ObservableAsPropertyHelper<Optional<IFluxMCodeStorageViewModel>> _Storage;
        public Optional<IFluxMCodeStorageViewModel> Storage => _Storage.Value;

        public ObservableAsPropertyHelper<Optional<string>> _MCodeName;
        [RemoteOutput(true)]
        public Optional<string> MCodeName => _MCodeName.Value;

        [RemoteOutput(false)]
        public ushort QueueIndex { get; }

        private ObservableAsPropertyHelper<short> _FileNumber;
        [RemoteOutput(true)]
        public short FileNumber => _FileNumber.Value;

        public Guid MCodeGuid { get; }

        private ObservableAsPropertyHelper<bool> _CurrentIndex;
        [RemoteOutput(true)]
        public bool CurrentIndex => _CurrentIndex.Value;

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> DeleteMCodeQueueCommand { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MoveUpMCodeQueueCommand { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MoveDownMCodeQueueCommand { get; }

        private ObservableAsPropertyHelper<DateTime> _EndTime;
        [RemoteOutput(true, typeof(DateTimeConverter<DateTimeFormat>))]
        public DateTime EndTime => _EndTime.Value;

        public MCodeQueueViewModel(MCodesViewModel mcodes, ushort queue_index, Guid queue_guid) : base($"{typeof(MCodeQueueViewModel).GetRemoteControlName()}??{queue_index}")
        {
            MCodes = mcodes;
            MCodeGuid = queue_guid;
            QueueIndex = queue_index;

            _CurrentIndex = MCodes.Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE_POS)
                .Select(q => q.HasValue && q.Value == queue_index)
                .ToProperty(this, v => v.CurrentIndex)
                .DisposeWith(Disposables);

            _Storage = MCodes.AvaiableMCodes
                .Connect()
                .WatchOptional(queue_guid)
                .ToProperty(this, v => v.Storage)
                .DisposeWith(Disposables);

            var strorage_list = MCodes.QueuedMCodes
                .Connect()
                .AutoRefresh(q => q.Storage)
                .Transform(q => q.Storage, true)
                .QueryWhenChanged();

            _EndTime = Observable.CombineLatest(
                mcodes.Flux.WhenAnyValue(f => f.CurrentTime),
                strorage_list,
                FindEndTime)
                .ToProperty(this, v => v.EndTime)
                .DisposeWith(Disposables);

            _FileNumber = this.WhenAnyValue(v => v.Storage)
                .ConvertMany(s => s.WhenAnyValue(s => s.FileNumber))
                .ConvertOr(n => (short)n, () => (short)-1)
                .ToProperty(this, v => v.FileNumber)
                .DisposeWith(Disposables);

            _MCodeName = this.WhenAnyValue(v => v.Storage)
                .Convert(s => s.Analyzer.MCode.Name)
                .ToProperty(this, v => v.MCodeName)
                .DisposeWith(Disposables);

            var can_move_up = Observable.Return(queue_index > 0);

            var can_move_down = mcodes.Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE)
                .Convert(q => q.Count)
                .ValueOr(() => 0)
                .Select(c => queue_index < c - 1);

            DeleteMCodeQueueCommand = ReactiveCommand.CreateFromTask(async () => { await MCodes.DeleteFromQueueAsync(this); });
            MoveUpMCodeQueueCommand = ReactiveCommand.CreateFromTask(async () => { await MCodes.MoveInQueueAsync(this, i => (short)(i - 1)); }, can_move_up);
            MoveDownMCodeQueueCommand = ReactiveCommand.CreateFromTask(async () => { await MCodes.MoveInQueueAsync(this, i => (short)(i + 1)); }, can_move_down);
        }

        private DateTime FindEndTime(DateTime start_time, IQuery<Optional<IFluxMCodeStorageViewModel>, ushort> queue)
        {
            return start_time + queue.KeyValues
                .Where(kvp => kvp.Key <= QueueIndex)
                .Where(kvp => kvp.Value.HasValue)
                .Aggregate(TimeSpan.Zero, (acc, kvp) => acc + kvp.Value.Value.Analyzer.MCode.Duration);
        }
    }
}
