using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
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

        private readonly ObservableAsPropertyHelper<Optional<IFluxMCodeStorageViewModel>> _Storage;
        public Optional<IFluxMCodeStorageViewModel> Storage => _Storage.Value;

        public ObservableAsPropertyHelper<Optional<string>> _MCodeName;
        [RemoteOutput(true)]
        public Optional<string> MCodeName => _MCodeName.Value;

        [RemoteOutput(false, typeof(ToStringConverter))]
        public FluxJob FluxJob { get; }

        public ObservableAsPropertyHelper<QueuePosition> _QueuePosition;
        [RemoteOutput(true)]
        public QueuePosition QueuePosition => _QueuePosition.Value;

        private readonly ObservableAsPropertyHelper<short> _FileNumber;
        [RemoteOutput(true)]
        public short FileNumber => _FileNumber.Value;

        private readonly ObservableAsPropertyHelper<bool> _CurrentIndex;
        [RemoteOutput(true)]
        public bool CurrentIndex => _CurrentIndex.Value;

        [RemoteCommand]
        public ReactiveCommandBaseRC DeleteMCodeQueueCommand { get; }

        [RemoteCommand]
        public ReactiveCommandBaseRC MoveUpMCodeQueueCommand { get; }

        [RemoteCommand]
        public ReactiveCommandBaseRC MoveDownMCodeQueueCommand { get; }

        private readonly ObservableAsPropertyHelper<DateTime> _EndTime;
        [RemoteOutput(true, typeof(DateTimeConverter<RelativeDateTimeFormat>))]
        public DateTime EndTime => _EndTime.Value;

        public MCodeQueueViewModel(MCodesViewModel mcodes, FluxJob job)
            : base($"{typeof(MCodeQueueViewModel).GetRemoteElementClass()};{job.QueuePosition}")
        {
            FluxJob = job;
            MCodes = mcodes;
            _QueuePosition =

           _QueuePosition = MCodes.Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE_POS)
                .ValueOr(() => (short)-1)
                .DistinctUntilChanged()
                .ToPropertyRC(this, v => v.QueuePosition);

            var print_progress = MCodes.Flux.StatusProvider
                .WhenAnyValue(s => s.PrintProgress)
                .DistinctUntilChanged();

            _CurrentIndex = this.WhenAnyValue(v => v.QueuePosition)
                .Select(q => q == job.QueuePosition)
                .ToPropertyRC(this, v => v.CurrentIndex);

            _Storage = MCodes.AvaiableMCodes.Connect()
                .WatchOptional(job.MCodeKey)
                .ToPropertyRC(this, v => v.Storage);

            var mcode_analyzers = MCodes.QueuedMCodes.Connect()
                 .AutoRefresh(m => m.Storage)
                 .Filter(m => m.Storage.HasValue)
                 .Transform(m => m.Storage.Value)
                 .QueryWhenChanged(m => m.KeyValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Analyzer))
                 .StartWith(new Dictionary<QueuePosition, MCodeAnalyzer>())
                 .DistinctUntilChanged();

            _EndTime = Observable.CombineLatest(
                mcodes.Flux.WhenAnyValue(f => f.CurrentTime),
                this.WhenAnyValue(v => v.QueuePosition),
                print_progress,
                mcode_analyzers,
                FindEndTime)
                .ToPropertyRC(this, v => v.EndTime);

            _FileNumber = this.WhenAnyValue(v => v.Storage)
                .ConvertMany(s => s.WhenAnyValue(s => s.FileNumber))
                .ConvertOr(n => (short)n, () => (short)-1)
                .ToPropertyRC(this, v => v.FileNumber);

            _MCodeName = this.WhenAnyValue(v => v.Storage)
                .Convert(s => s.Analyzer)
                .Convert(s => s.MCode.Name)
                .ToPropertyRC(this, v => v.MCodeName);

            var is_idle = MCodes.Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(e => e.IsIdle);

            var can_safe_cycle = MCodes.Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(e => e.CanSafeCycle);

            var last_queue_pos = mcodes.Flux.StatusProvider
                .WhenAnyValue(s => s.JobQueue)
                .Convert(q => q?.Count - 1 ?? -1)
                .Convert(c => new QueuePosition((short)c))
                .ValueOr(() => new QueuePosition(-1));

            var can_move_up = Observable.CombineLatest(is_idle, this.WhenAnyValue(v => v.QueuePosition), CanMoveUpQueue);
            var can_delete = Observable.CombineLatest(can_safe_cycle, this.WhenAnyValue(v => v.QueuePosition), CanDeleteQueue);
            var can_move_down = Observable.CombineLatest(is_idle, this.WhenAnyValue(v => v.QueuePosition), last_queue_pos, CanMoveDownQueue);

            DeleteMCodeQueueCommand = ReactiveCommandBaseRC.CreateFromTask(async () => { await MCodes.DeleteFromQueueAsync(this); }, this, can_delete);
            MoveUpMCodeQueueCommand = ReactiveCommandBaseRC.CreateFromTask(async () => { await MCodes.MoveInQueueAsync(this, i => (short)(i - 1)); }, this, can_move_up);
            MoveDownMCodeQueueCommand = ReactiveCommandBaseRC.CreateFromTask(async () => { await MCodes.MoveInQueueAsync(this, i => (short)(i + 1)); }, this, can_move_down);
        }

        private bool CanDeleteQueue(bool is_idle, QueuePosition queue_position)
        {
            if (is_idle)
                return true;
            return FluxJob.QueuePosition > queue_position;
        }
        private bool CanMoveUpQueue(bool is_idle, QueuePosition queue_position)
        {
            if (is_idle)
                return FluxJob.QueuePosition > 0;
            return FluxJob.QueuePosition > queue_position + 1;
        }
        private bool CanMoveDownQueue(bool is_idle, QueuePosition queue_position, QueuePosition last_queue_pos)
        {
            if (is_idle)
                return FluxJob.QueuePosition < last_queue_pos;
            return FluxJob.QueuePosition > queue_position && FluxJob.QueuePosition < last_queue_pos;
        }

        private DateTime FindEndTime(DateTime start_time, QueuePosition queue_pos, PrintProgress progress, Dictionary<QueuePosition, MCodeAnalyzer> mcode_analyzers)
        {
            try
            {
                return start_time + mcode_analyzers
                    .Where(kvp => kvp.Key <= FluxJob.QueuePosition)
                    .Aggregate(TimeSpan.Zero, (acc, kvp) => acc + find_duration(kvp));

                TimeSpan find_duration(KeyValuePair<QueuePosition, MCodeAnalyzer> kvp)
                {
                    if (kvp.Key == queue_pos)
                        return progress.RemainingTime;
                    return kvp.Value.MCode.Duration;
                }
            }
            catch (Exception)
            {
                return start_time;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
