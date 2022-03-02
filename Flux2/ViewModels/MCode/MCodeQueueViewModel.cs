using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
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

        [RemoteOutput(false, typeof(ImplicitTypeConverter<short>))]
        public QueuePosition QueueIndex { get; }

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

        public MCodeQueueViewModel(MCodesViewModel mcodes, QueuePosition queue_index, Guid queue_guid) : base($"{typeof(MCodeQueueViewModel).GetRemoteControlName()}??{queue_index}")
        {
            MCodes = mcodes;
            MCodeGuid = queue_guid;
            QueueIndex = queue_index;

            var queue_pos = MCodes.Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE_POS)
                .ValueOr(() => (short)-1)
                .DistinctUntilChanged();

            var print_progress = MCodes.Flux.StatusProvider
                .WhenAnyValue(s => s.PrintProgress)
                .DistinctUntilChanged();

            _CurrentIndex = queue_pos
                .Select(q => q == queue_index)
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
                queue_pos,
                print_progress,
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

            var is_idle = MCodes.Flux.ConnectionProvider
                .ObserveVariable(v => v.PROCESS_STATUS)
                .Convert(s => s == FLUX_ProcessStatus.IDLE)
                .ValueOr(() => false)
                .DistinctUntilChanged();

            var can_move_up = Observable.Return(queue_index > 0);

            var can_move_down = mcodes.Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE)
                .Convert(q => q?.Count ?? 0)
                .ValueOr(() => 0)
                .Select(c => queue_index < c - 1);

            DeleteMCodeQueueCommand = ReactiveCommand.CreateFromTask(async () => { await MCodes.DeleteFromQueueAsync(this); }, is_idle);
            MoveUpMCodeQueueCommand = ReactiveCommand.CreateFromTask(async () => { await MCodes.MoveInQueueAsync(this, i => (short)(i - 1)); }, Observable.CombineLatest(is_idle, can_move_up, (i,m) => i && m));
            MoveDownMCodeQueueCommand = ReactiveCommand.CreateFromTask(async () => { await MCodes.MoveInQueueAsync(this, i => (short)(i + 1)); }, Observable.CombineLatest(is_idle, can_move_down, (i, m) => i && m));
        }

        private DateTime FindEndTime(DateTime start_time, QueuePosition queue_pos, PrintProgress progress, IQuery<Optional<IFluxMCodeStorageViewModel>, QueuePosition> queue)
        {
            try
            {
                return start_time + queue.KeyValues
                    .Where(kvp => kvp.Key <= QueueIndex)
                    .Aggregate(TimeSpan.Zero, (acc, kvp) => acc + find_duration(kvp));

                TimeSpan find_duration(KeyValuePair<QueuePosition, Optional<IFluxMCodeStorageViewModel>> kvp)
                {
                    if (!kvp.Value.HasValue)
                        return TimeSpan.Zero;
                    if (kvp.Key == queue_pos)
                        return progress.RemainingTime;
                    return kvp.Value.Value.Analyzer.MCode.Duration;
                }
            }
            catch(Exception ex)
            {
                return start_time;
            }
        }
    }
}
