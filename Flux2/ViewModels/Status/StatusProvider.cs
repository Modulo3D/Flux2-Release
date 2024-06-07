using DynamicData;
using DynamicData.Kernel;
using Flux.ViewModels;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class ConditionDictionary<TConditionAttribute> : Dictionary<string, List<(IConditionViewModel condition, TConditionAttribute condition_attribute)>>
    {
    }

    public class StatusProvider : ReactiveObjectRC<StatusProvider>, IFluxStatusProvider
    {
        public FluxViewModel Flux { get; }

        private readonly ObservableAsPropertyHelper<Optional<JobQueue>> _JobQueue;
        public Optional<JobQueue> JobQueue => _JobQueue.Value;

        public IObservableCache<FeederEvaluator, ushort> FeederEvaluators { get; private set; }
        public IObservableCache<Optional<DocumentQueue<Material>>, ushort> ExpectedMaterialsQueue { get; private set; }
        public IObservableCache<Optional<DocumentQueue<Nozzle>>, ushort> ExpectedNozzlesQueue { get; private set; }

        private bool _StartWithLowNozzles;
        public bool StartWithLowNozzles
        {
            get => _StartWithLowNozzles;
            set => this.RaiseAndSetIfChanged(ref _StartWithLowNozzles, value);
        }

        private bool _StartWithLowMaterials;
        public bool StartWithLowMaterials
        {
            get => _StartWithLowMaterials;
            set => this.RaiseAndSetIfChanged(ref _StartWithLowMaterials, value);
        }


        private ObservableAsPropertyHelper<FLUX_ProcessStatus> _FluxStatus;
        public FLUX_ProcessStatus FluxStatus => _FluxStatus.Value;

        private readonly ObservableAsPropertyHelper<PrintProgress> _PrintProgress;
        public PrintProgress PrintProgress => _PrintProgress.Value;

        private readonly ObservableAsPropertyHelper<PrintingEvaluation> _PrintingEvaluation;
        public PrintingEvaluation PrintingEvaluation => _PrintingEvaluation.Value;

        private readonly ObservableAsPropertyHelper<StatusEvaluation> _StatusEvaluation;
        public StatusEvaluation StatusEvaluation => _StatusEvaluation.Value;

        private readonly ObservableAsPropertyHelper<StartEvaluation> _StartEvaluation;
        public StartEvaluation StartEvaluation => _StartEvaluation.Value;

        public StatusProvider(FluxViewModel flux)
        {
            Flux = flux;

            FeederEvaluators = Flux.Feeders.Feeders.Connect()
                .QueryWhenChanged(CreateFeederEvaluator)
                .AsObservableChangeSet(e => e.Feeder.Position)
                .AsObservableCacheRC(this);

            ExpectedMaterialsQueue = FeederEvaluators.Connect()
                .Transform(f => f.Material, true)
                .AutoTransform(f => f.ExpectedDocumentQueue)
                .AsObservableCacheRC(this);

            ExpectedNozzlesQueue = FeederEvaluators.Connect()
                .Transform(f => f.ToolNozzle, true)
                .AutoTransform(f => f.ExpectedDocumentQueue)
                .AsObservableCacheRC(this);

            var core_settings = Flux.SettingsProvider.CoreSettings.Local;

            var queue_pos = Flux.ConnectionProvider
                .ObserveVariable(m => m.QUEUE_POS)
                .StartWithDefault();

            var queue_preview = Flux.ConnectionProvider
                .ObserveVariable(m => m.QUEUE)
                .StartWithDefault();

            _JobQueue = Observable.CombineLatest(queue_preview, queue_pos,
                (queue_preview, queue_pos) => (queue_preview, queue_pos))
                .SelectAsync(t => get_queue(t.queue_preview, t.queue_pos))
                .StartWithDefault()
                .ToPropertyRC(this, v => v.JobQueue);

            var current_job = this.WhenAnyValue(v => v.JobQueue)
                .Convert(q => q.CurrentJob);

            async Task<Optional<JobQueue>> get_queue(Optional<FluxJobQueuePreview> preview, Optional<QueuePosition> queue_pos)
            {
                if (!preview.HasValue)
                    return default;
                if (!queue_pos.HasValue)
                    return default;
                using var queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                return await preview.Value.GetJobQueueAsync(Flux.ConnectionProvider, queue_pos.Value, queue_cts.Token);
            }

            var recovery_preview = Flux.ConnectionProvider
                .ObserveVariable(c => c.RECOVERY)
                .StartWithDefault();

            var current_recovery = Observable.CombineLatest(current_job, recovery_preview,
                (current_job, recovery_preview) => (current_job, recovery_preview))
                .SelectAsync(t => get_recovery(t.current_job, t.recovery_preview))
                .StartWithDefault();

            async Task<Optional<FluxJobRecovery>> get_recovery(Optional<FluxJob> current_job, Optional<FluxJobRecoveryPreview> recovery_preview)
            {
                if (!recovery_preview.HasValue)
                    return default;
                if (!current_job.HasValue)
                    return default;
                using var queue_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                return await recovery_preview.Value.GetFluxJobRecoveryAsync(Flux.ConnectionProvider, current_job.Value, queue_cts.Token);
            }

            var current_mcode_key = this.WhenAnyValue(v => v.JobQueue)
                .Convert(q => q.CurrentJob)
                .Convert(j => j.MCodeKey);

            var current_vm = Flux.MCodes.AvaiableMCodes.Connect()
                .WatchOptional(current_mcode_key);

            var current_mcode = current_vm
                .Convert(vm => vm.Analyzer)
                .Convert(a => a.MCode)
                .StartWithDefault()
                .DistinctUntilChanged();

            var is_idle = Flux.ConnectionProvider
                .ObserveVariable(m => m.PROCESS_STATUS)
                .Convert(data => data == FLUX_ProcessStatus.IDLE)
                .ValueOr(() => false);

            var is_cycle = Flux.ConnectionProvider
                .ObserveVariable(m => m.PROCESS_STATUS)
                .Convert(data => data == FLUX_ProcessStatus.CYCLE)
                .ValueOr(() => false);

            var start_with_low_nozzles = this.WhenAnyValue(s => s.StartWithLowNozzles);
            var start_with_low_materials = this.WhenAnyValue(s => s.StartWithLowMaterials);

            var has_invalid_materials = FeederEvaluators.Connect()
                .TrueForAny(line => line.WhenAnyValue(l => l.Material.IsInvalid), invalid => invalid)
                .StartWith(false)
                .DistinctUntilChanged();

            var has_invalid_tools = FeederEvaluators.Connect()
                .TrueForAny(line => line.WhenAnyValue(l => l.ToolNozzle.IsInvalid), invalid => invalid)
                .StartWith(false)
                .DistinctUntilChanged();

            var has_invalid_probes = FeederEvaluators.Connect()
                .TrueForAny(line => line.WhenAnyValue(l => l.IsInvalidProbe), invalid => invalid)
                .StartWith(false)
                .DistinctUntilChanged();

            var has_low_materials = FeederEvaluators.Connect()
                .TrueForAny(line => line.WhenAnyValue(l => l.Material.HasLowWeight), low => low)
                .StartWith(false)
                .DistinctUntilChanged();

            var has_low_nozzles = FeederEvaluators.Connect()
                .TrueForAny(line => line.WhenAnyValue(l => l.ToolNozzle.HasLowWeight), low => low)
                .StartWith(false)
                .DistinctUntilChanged();

            var has_cold_nozzles = FeederEvaluators.Connect()
                .TrueForAny(line => line.WhenAnyValue(l => l.HasColdNozzle), cold => cold)
                .StartWith(false)
                .DistinctUntilChanged();

            var has_invalid_printer = core_settings.WhenAnyValue(v => v.PrinterID).CombineLatest(
                current_mcode,
                (printer_id, selected_mcode) => !selected_mcode.HasValue || selected_mcode.Value.PrinterId != printer_id);

            _StartEvaluation = Observable.CombineLatest( 
                has_low_nozzles,
                has_cold_nozzles,
                has_low_materials,
                has_invalid_tools,
                has_invalid_probes,
                has_invalid_printer,
                has_invalid_materials,
                start_with_low_nozzles,
                start_with_low_materials,
                StartEvaluation.Create)
                .DistinctUntilChanged()
                .ToPropertyRC(this, v => v.StartEvaluation);

            _PrintingEvaluation = Observable.CombineLatest(
                current_job,
                current_mcode,
                current_recovery,
                PrintingEvaluation.Create)
                .DistinctUntilChanged()
                .ToPropertyRC(this, v => v.PrintingEvaluation);

            var is_homed = Flux.ConnectionProvider
                .ObserveVariable(m => m.IS_HOMED)
                .ValueOr(() => false);

            var has_safe_state = Observable.CombineLatest(
                Flux.ConnectionProvider.WhenAnyValue(v => v.IsConnecting),
                Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS),
                Flux.Feeders.WhenAnyValue(f => f.HasInvalidStates),
                HasSafeState)
                .StartWith(false);

            var cycle_conditions = Flux.ConditionsProvider.ObserveConditions<CycleConditionAttribute>();
            var print_conditions = Flux.ConditionsProvider.ObserveConditions<PrintConditionAttribute>();

            var can_safe_cycle = Observable.CombineLatest(
                is_idle,
                has_safe_state,
                cycle_conditions.QueryWhenChanged(),
                (idle, state, safe_cycle) => idle && state && safe_cycle.All(s => s.Valid))
                .StartWith(false)
                .DistinctUntilChanged();

            var can_safe_print = Observable.CombineLatest(
                is_idle,
                has_safe_state,
                print_conditions.QueryWhenChanged(),
                (idle, state, safe_print) => idle && state && safe_print.All(s => s.Valid));

            var can_safe_stop = has_safe_state.CombineLatest(
                cycle_conditions.QueryWhenChanged(),
                (state, safe_cycle) => state && safe_cycle.All(s => s.Valid))
                .StartWith(false)
                .DistinctUntilChanged();

            var can_safe_hold = Observable.CombineLatest(
                is_cycle,
                has_safe_state,
                cycle_conditions.QueryWhenChanged(),
                (cycle, state, safe_cycle) => cycle && state && safe_cycle.All(s => s.Valid))
                .StartWith(false)
                .DistinctUntilChanged();

            var is_enabled_axis = Flux.ConnectionProvider
                .ObserveVariable(m => m.ENABLE_DRIVERS)
                .QueryWhenChanged(e =>
                {
                    if (e.Items.Any(e => !e.HasValue))
                        return Optional<bool>.None;
                    return e.Items.All(e => e.Value);
                })
                .ValueOr(() => false);

            _StatusEvaluation = Observable.CombineLatest(
                is_idle,
                is_homed,
                is_cycle,
                can_safe_stop,
                can_safe_hold,
                can_safe_cycle, 
                can_safe_print, 
                is_enabled_axis,
                StatusEvaluation.Create)
                .DistinctUntilChanged()
                .ToPropertyRC(this, v => v.StatusEvaluation);

            var progress = Flux.ConnectionProvider
                .ObserveVariable(c => c.PROGRESS)
                .ValueOrDefault()
                .DistinctUntilChanged();

            var storage = Flux.ConnectionProvider
                .ObserveVariable(c => c.STORAGE)
                .DistinctUntilChanged();

            _PrintProgress = Observable.CombineLatest(
                this.WhenAnyValue(v => v.StatusEvaluation),
                this.WhenAnyValue(v => v.PrintingEvaluation),
                storage,
                progress,
                GetPrintProgress)
                .DistinctUntilChanged()
                .ToPropertyRC(this, v => v.PrintProgress);

            var database = Flux.DatabaseProvider
                .WhenAnyValue(v => v.Database);

            var sample_print_progress = Observable.CombineLatest(
                is_cycle,
                current_mcode,
                Observable.Interval(TimeSpan.FromSeconds(5)).StartWith(0),
                (_, _, _) => Unit.Default);

            var print_progress = this.WhenAnyValue(c => c.PrintProgress)
                .Sample(sample_print_progress)
                .DistinctUntilChanged();
        }

        public void Initialize()
        {
            _FluxStatus = Observable.CombineLatest(
                Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS),
                this.WhenAnyValue(v => v.PrintingEvaluation),
                Flux.Navigator.WhenAnyValue(nav => nav.CurrentViewModel),
                Flux.ConnectionProvider.WhenAnyValue(c => c.IsConnecting),
                FindFluxStatus)
                .ToPropertyRC(this, s => s.FluxStatus);
        }

        private FLUX_ProcessStatus FindFluxStatus(
            Optional<FLUX_ProcessStatus> status,
            PrintingEvaluation printing_eval,
            Optional<IFluxRoutableViewModel> current_vm,
            bool is_connecting)
        {
            if (!status.HasValue)
                return FLUX_ProcessStatus.NONE;

            if (!status.HasValue)
                return FLUX_ProcessStatus.NONE;

            if (is_connecting)
                return FLUX_ProcessStatus.NONE;

            switch (status.Value)
            {
                case FLUX_ProcessStatus.IDLE:
                    if (current_vm.HasValue && current_vm.Value is IOperationViewModel)
                        return FLUX_ProcessStatus.WAIT;
                    if (printing_eval.Recovery.HasValue)
                        return FLUX_ProcessStatus.WAIT;
                    if (printing_eval.MCode.HasValue)
                        return FLUX_ProcessStatus.WAIT;
                    return FLUX_ProcessStatus.IDLE;

                default:
                    return status.Value;
            }
        }

        private bool HasSafeState(
            bool is_connecting,
            Optional<FLUX_ProcessStatus> status,
            bool has_feeder_error)
        {
            if (is_connecting)
                return false;
            if (!status.HasValue)
                return false;
            if (status.Value == FLUX_ProcessStatus.EMERG)
                return false;
            if (status.Value == FLUX_ProcessStatus.ERROR)
                return false;
            if (has_feeder_error)
                return false;
            return true;
        }

        // Progress and extrusion
        private PrintProgress GetPrintProgress(StatusEvaluation status, PrintingEvaluation printing, Optional<MCodeStorage> storage, MCodeProgress progress)
        {
            var current_mcode = printing.MCode;
            if (!current_mcode.HasValue)
                return new PrintProgress(0, TimeSpan.Zero);

            var duration = current_mcode.Value.Duration;
            if (status.IsIdle && !printing.Recovery.HasValue)
                return new PrintProgress(0, duration);

            if (printing.Recovery.HasValue)
                progress = new MCodeProgress(printing.Recovery.Value);

            if (progress == default)
                return new PrintProgress(0, duration);

            if (!storage.HasValue)
                return new PrintProgress(0, duration);

            var percentage = progress.GetPercentage(printing, storage.Value);
            if (!percentage.HasValue)
                return PrintProgress;

            var remaining_time = progress.GetRemainingTime(duration, percentage.Value);
            return new PrintProgress(percentage.Value, remaining_time);
        }

        private IEnumerable<FeederEvaluator> CreateFeederEvaluator(IQuery<IFluxFeederViewModel, ushort> query)
        {
            foreach (var feeder in query.Items)
            {
                var evaluator = new FeederEvaluator(Flux, feeder);
                evaluator.Initialize();
                yield return evaluator;
            }
        }
    }
}
