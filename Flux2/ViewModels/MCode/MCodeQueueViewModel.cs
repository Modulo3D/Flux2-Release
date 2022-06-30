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
        public FluxJob Job { get; }

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
        [RemoteOutput(true, typeof(DateTimeConverter<DateTimeFormat>))]
        public DateTime EndTime => _EndTime.Value;

        public MCodeQueueViewModel(MCodesViewModel mcodes, FluxJob job) : base($"{typeof(MCodeQueueViewModel).GetRemoteControlName()}??{job.QueuePosition}")
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
                .WatchOptional(job.MCodeGuid)
                .ToProperty(this, v => v.Storage)
                .DisposeWith(Disposables);

            var mcode_analyzers = MCodes.QueuedMCodes.Connect()
                 .AutoRefresh(m => m.Storage)
                 .Filter(m => m.Storage.HasValue)
                 .Transform(m => m.Storage.Value)
                 .AutoRefresh(m => m.Analyzer)
                 .QueryWhenChanged(m => m.KeyValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Analyzer))
                 .StartWith(new Dictionary<QueuePosition, Optional<MCodeAnalyzer>>())
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

            var is_idle = MCodes.Flux.ConnectionProvider
                .ObserveVariable(v => v.PROCESS_STATUS)
                .Convert(s => s == FLUX_ProcessStatus.IDLE)
                .ValueOr(() => false)
                .DistinctUntilChanged();

            var can_move_up = Observable.Return(job.QueuePosition > 0);

            var can_move_down = mcodes.Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE)
                .Convert(q => q?.Count ?? 0)
                .ValueOr(() => 0)
                .Select(c => job.QueuePosition < c - 1);

            DeleteMCodeQueueCommand = ReactiveCommand.CreateFromTask(async () => { await MCodes.DeleteFromQueueAsync(this); }, is_idle);
            MoveUpMCodeQueueCommand = ReactiveCommand.CreateFromTask(async () => { await MCodes.MoveInQueueAsync(this, i => (short)(i - 1)); }, Observable.CombineLatest(is_idle, can_move_up, (i,m) => i && m));
            MoveDownMCodeQueueCommand = ReactiveCommand.CreateFromTask(async () => { await MCodes.MoveInQueueAsync(this, i => (short)(i + 1)); }, Observable.CombineLatest(is_idle, can_move_down, (i, m) => i && m));
        }

        private DateTime FindEndTime(DateTime start_time, QueuePosition queue_pos, PrintProgress progress, Dictionary<QueuePosition, Optional<MCodeAnalyzer>> mcode_analyzers)
        {
            try
            {
                return start_time + mcode_analyzers
                    .Where(kvp => kvp.Key <= Job.QueuePosition)
                    .Aggregate(TimeSpan.Zero, (acc, kvp) => acc + find_duration(kvp));

                TimeSpan find_duration(KeyValuePair<QueuePosition, Optional<MCodeAnalyzer>> kvp)
                {
                    if (!kvp.Value.HasValue)
                        return TimeSpan.Zero;
                    if (kvp.Key == queue_pos)
                        return progress.RemainingTime;
                    return kvp.Value.Value.MCode.Duration;
                }
            }
            catch(Exception ex)
            {
                return start_time;
            }
        }
    }
}
