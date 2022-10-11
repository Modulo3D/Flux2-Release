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

        [RemoteOutput(false, typeof(ToStringConverter))]
        public Job Job { get; }

        private ObservableAsPropertyHelper<short> _FileNumber;
        [RemoteOutput(true)]
        public short FileNumber => _FileNumber.Value;

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
        [RemoteOutput(true, typeof(DateTimeConverter<RelativeDateTimeFormat>))]
        public DateTime EndTime => _EndTime.Value;

        public MCodeQueueViewModel(MCodesViewModel mcodes, Job job) : base($"{typeof(MCodeQueueViewModel).GetRemoteControlName()}??{job.QueuePosition}")
        {
            Job = job;
            MCodes = mcodes;

            var queue_pos = MCodes.Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE_POS)
                .ValueOr(() => (short)-1)
                .DistinctUntilChanged();

            var print_progress = MCodes.Flux.StatusProvider
                .WhenAnyValue(s => s.PrintProgress)
                .DistinctUntilChanged();

            _CurrentIndex = queue_pos
                .Select(q => q == job.QueuePosition)
                .ToProperty(this, v => v.CurrentIndex)
                .DisposeWith(Disposables);

            _Storage = MCodes.AvaiableMCodes
                .Connect()
                .WatchOptional(job.PartProgram.MCodeKey)
                .ToProperty(this, v => v.Storage)
                .DisposeWith(Disposables);

            var mcode_analyzers = MCodes.QueuedMCodes.Connect()
                 .AutoRefresh(m => m.Storage)
                 .Filter(m => m.Storage.HasValue)
                 .Transform(m => m.Storage.Value)
                 .QueryWhenChanged(m => m.KeyValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Analyzer))
                 .StartWith(new Dictionary<QueuePosition, MCodeAnalyzer>())
                 .DistinctUntilChanged();

            _EndTime = Observable.CombineLatest(
                mcodes.Flux.WhenAnyValue(f => f.CurrentTime),
                queue_pos,
                print_progress,
                mcode_analyzers,
                FindEndTime)
                .ToProperty(this, v => v.EndTime)
                .DisposeWith(Disposables);

            _FileNumber = this.WhenAnyValue(v => v.Storage)
                .ConvertMany(s => s.WhenAnyValue(s => s.FileNumber))
                .ConvertOr(n => (short)n, () => (short)-1)
                .ToProperty(this, v => v.FileNumber)
                .DisposeWith(Disposables);

            _MCodeName = this.WhenAnyValue(v => v.Storage)
                .Convert(s => s.Analyzer)
                .Convert(s => s.MCode.Name)
                .ToProperty(this, v => v.MCodeName)
                .DisposeWith(Disposables);

            var is_idle = MCodes.Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(e => e.IsIdle);

            var can_safe_cycle = MCodes.Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(e => e.CanSafeCycle);

            var last_queue_pos = mcodes.Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE)
                .Convert(q => q?.Count - 1 ?? -1)
                .Convert(c => new QueuePosition((short)c))
                .ValueOr(() => new QueuePosition(-1));

            var can_move_up = Observable.CombineLatest(is_idle, queue_pos, CanMoveUpQueue);
            var can_delete = Observable.CombineLatest(can_safe_cycle, queue_pos, CanDeleteQueue);
            var can_move_down = Observable.CombineLatest(is_idle, queue_pos, last_queue_pos, CanMoveDownQueue);

            DeleteMCodeQueueCommand = ReactiveCommand.CreateFromTask(async () => { await MCodes.DeleteFromQueueAsync(this); }, can_delete);
            MoveUpMCodeQueueCommand = ReactiveCommand.CreateFromTask(async () => { await MCodes.MoveInQueueAsync(this, i => (short)(i - 1)); }, can_move_up);
            MoveDownMCodeQueueCommand = ReactiveCommand.CreateFromTask(async () => { await MCodes.MoveInQueueAsync(this, i => (short)(i + 1)); }, can_move_down);
        }

        private bool CanDeleteQueue(bool is_idle, QueuePosition queue_position)
        {
            if (is_idle)
                return true;
            return Job.QueuePosition > queue_position;
        }
        private bool CanMoveUpQueue(bool is_idle, QueuePosition queue_position)
        {
            if (is_idle)
                return Job.QueuePosition > 0;
            return Job.QueuePosition > queue_position + 1;
        }
        private bool CanMoveDownQueue(bool is_idle, QueuePosition queue_position, QueuePosition last_queue_pos)
        {
            if (is_idle)
                return Job.QueuePosition < last_queue_pos;
            return Job.QueuePosition > queue_position && Job.QueuePosition < last_queue_pos;
        }

        private DateTime FindEndTime(DateTime start_time, QueuePosition queue_pos, PrintProgress progress, Dictionary<QueuePosition, MCodeAnalyzer> mcode_analyzers)
        {
            try
            {
                return start_time + mcode_analyzers
                    .Where(kvp => kvp.Key <= Job.QueuePosition)
                    .Aggregate(TimeSpan.Zero, (acc, kvp) => acc + find_duration(kvp));

                TimeSpan find_duration(KeyValuePair<QueuePosition, MCodeAnalyzer> kvp)
                {
                    if (kvp.Key == queue_pos)
                        return progress.RemainingTime;
                    return kvp.Value.MCode.Duration;
                }
            }
            catch(Exception ex)
            {
                return start_time;
            }
        }
    }
}
