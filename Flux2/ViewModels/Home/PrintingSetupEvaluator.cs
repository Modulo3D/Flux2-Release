using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public interface ITagViewModelEvaluator : IReactiveObject
    {
        public FeederEvaluator FeederEvaluator { get; }
        public Optional<double> ExpectedWeight { get; }
        public Optional<double> CurrentWeight { get; }
        public bool HasLowWeight { get; }
        public bool IsInvalid { get; }
    }

    public abstract class TagViewModelEvaluator<TTagViewModelEvaluator, TNFCTag, TTagDocument, TDocument, TState> : 
        ReactiveObjectRC<TTagViewModelEvaluator>, ITagViewModelEvaluator
        where TTagViewModelEvaluator : TagViewModelEvaluator<TTagViewModelEvaluator, TNFCTag, TTagDocument, TDocument, TState>
        where TNFCTag : INFCOdometerTag<TNFCTag>, new()
        where TDocument : IDocument, new()
    {
        public FluxViewModel Flux { get; }
        public FeederEvaluator FeederEvaluator { get; }
        public abstract Optional<IFluxTagViewModel<TNFCTag, TTagDocument, TState>> TagViewModel { get; }

        private ObservableAsPropertyHelper<Optional<DocumentQueue<TDocument>>> _ExpectedDocumentQueue;
        public Optional<DocumentQueue<TDocument>> ExpectedDocumentQueue => _ExpectedDocumentQueue.Value;

        private ObservableAsPropertyHelper<Optional<TDocument>> _CurrentDocument;
        public Optional<TDocument> CurrentDocument => _CurrentDocument.Value;

        private ObservableAsPropertyHelper<bool> _IsInvalid;
        public bool IsInvalid => _IsInvalid.Value;

        private ObservableAsPropertyHelper<Optional<double>> _ExpectedWeight;
        public Optional<double> ExpectedWeight => _ExpectedWeight.Value;

        private ObservableAsPropertyHelper<Optional<double>> _CurrentWeight;
        public Optional<double> CurrentWeight => _CurrentWeight.Value;

        private ObservableAsPropertyHelper<bool> _HasLowWeight;
        public bool HasLowWeight => _HasLowWeight.Value;

        private ObservableAsPropertyHelper<bool> _HasEmptyWeight;
        public bool HasEmptyWeight => _HasEmptyWeight.Value;

        public TagViewModelEvaluator(FluxViewModel flux, FeederEvaluator feeder_eval)
        {
            Flux = flux;
            FeederEvaluator = feeder_eval; 
        }

        public void Initialize()
        {
            var queue = Flux.StatusProvider.WhenAnyValue(c => c.JobQueue);
            var queue_pos = Flux.ConnectionProvider.ObserveVariable(c => c.QUEUE_POS);

            var db_changed = Flux.DatabaseProvider.WhenAnyValue(v => v.Database);
            var report_queue = FeederEvaluator.WhenAnyValue(e => e.FeederReportQueue);

            _ExpectedDocumentQueue = Observable.CombineLatest(
                queue,
                queue_pos,
                db_changed,
                report_queue,
                GetExpectedDocumentQueueAsync)
                .SelectAsync()
                .ToPropertyRC((TTagViewModelEvaluator)this, v => v.ExpectedDocumentQueue);

            _CurrentDocument = this.WhenAnyValue(v => v.TagViewModel)
                .ConvertMany(t => t.WhenAnyValue(t => t.Document))
                .Convert(GetCurrentDocument)
                .ToPropertyRC((TTagViewModelEvaluator)this, v => v.CurrentDocument);

            _IsInvalid = Observable.CombineLatest(
                this.WhenAnyValue(v => v.TagViewModel)
                    .ConvertMany(t => t.WhenAnyValue(t => t.Document)),
                this.WhenAnyValue(v => v.TagViewModel)
                    .ConvertMany(t => t.WhenAnyValue(t => t.State)),
                report_queue,
                GetInvalid)
                .ToPropertyRC((TTagViewModelEvaluator)this, e => e.IsInvalid);

            _ExpectedWeight = FeederEvaluator.WhenAnyValue(f => f.ExtrusionQueue)
                .Convert(e => e.Values.Sum(e => e.WeightG))
                .ToPropertyRC((TTagViewModelEvaluator)this, v => v.ExpectedWeight);

            _CurrentWeight = this.WhenAnyValue(v => v.TagViewModel)
                .ConvertMany(t => t.Odometer.WhenAnyValue(t => t.CurrentValue))
                .ToPropertyRC((TTagViewModelEvaluator)this, v => v.CurrentWeight);

            _HasLowWeight = Observable.CombineLatest(
                this.WhenAnyValue(v => v.ExpectedWeight),
                this.WhenAnyValue(v => v.CurrentWeight),
                report_queue,
                LowWeight)
                .ToPropertyRC((TTagViewModelEvaluator)this, v => v.HasLowWeight);

            _HasEmptyWeight = Observable.CombineLatest(
               this.WhenAnyValue(v => v.CurrentWeight),
               report_queue,
               EmptyWeight)
               .ToPropertyRC((TTagViewModelEvaluator)this, v => v.HasEmptyWeight);
        }

        public abstract int GetDocumentId(FeederReport feeder_report);
        public abstract Optional<TDocument> GetCurrentDocument(TTagDocument tag_document);
        public abstract bool GetInvalid(Optional<TTagDocument> document, Optional<TState> state, Optional<FeederReportQueue> report);

        private async Task<Optional<DocumentQueue<TDocument>>> GetExpectedDocumentQueueAsync(
            Optional<JobQueue> job_queue,
            Optional<QueuePosition> queue_position,
            Optional<ILocalDatabase> database,
            Optional<FeederReportQueue> feeder_queue)
        {
            try
            {
                if (!job_queue.HasValue)
                    return default;

                if (!queue_position.HasValue)
                    return default;

                if (queue_position.Value < 0)
                    return default;

                if (!database.HasValue)
                    return default;
                if (!feeder_queue.HasValue)
                    return default;

                var document_queue = new DocumentQueue<TDocument>();
                foreach (var feeder_report in feeder_queue.Value)
                {
                    var job = job_queue.Value
                        .Lookup(queue_position.Value);
                    if (!job.HasValue)
                        continue;

                    if (job.Value.QueuePosition < queue_position.Value)
                        continue;

                    var document_id = GetDocumentId(feeder_report.Value);

                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var result = await database.Value.FindByIdAsync<TDocument>(document_id, cts.Token);
                    if (!result.HasDocuments)
                        continue;

                    document_queue.Add(feeder_report.Key, result.First());
                }
                return document_queue;
            }
            catch (Exception)
            {
                return default;
            }
        }
        private bool LowWeight(Optional<double> expected, Optional<double> current, Optional<FeederReportQueue> feeder_queue)
        {
            try
            {
                if (!feeder_queue.HasValue)
                    return true;
                if (feeder_queue.Value.Count == 0)
                    return false;
                if (!expected.HasValue)
                    return true;
                if (!current.HasValue)
                    return true;
                return current.Value < expected.Value;
            }
            catch (Exception)
            {
                return false;
            }
        }
        private bool EmptyWeight(Optional<double> current, Optional<FeederReportQueue> feeder_queue)
        {
            if (!feeder_queue.HasValue)
                return true;
            if (feeder_queue.Value.Count == 0)
                return false;
            if (!current.HasValue)
                return true;
            return false;
        }
    }

    public class MaterialEvaluator : TagViewModelEvaluator<MaterialEvaluator, NFCMaterial, Optional<Material>, Material, MaterialState>
    {
        private readonly ObservableAsPropertyHelper<Optional<IFluxTagViewModel<NFCMaterial, Optional<Material>, MaterialState>>> _TagViewModel;
        public override Optional<IFluxTagViewModel<NFCMaterial, Optional<Material>, MaterialState>> TagViewModel => _TagViewModel.Value;

        public MaterialEvaluator(FluxViewModel flux, FeederEvaluator feeder_eval) : base(flux, feeder_eval)
        {
            _TagViewModel = FeederEvaluator.Feeder.WhenAnyValue(f => f.SelectedMaterial)
                .Convert(m => (IFluxTagViewModel<NFCMaterial, Optional<Material>, MaterialState>)m)
                .ToPropertyRC(this, v => v.TagViewModel);
        }
        public override Optional<Material> GetCurrentDocument(Optional<Material> tag_document)
        {
            return tag_document;
        }
        public override int GetDocumentId(FeederReport feeder_report)
        {
            return feeder_report.MaterialId;
        }
        public override bool GetInvalid(Optional<Optional<Material>> document, Optional<MaterialState> state, Optional<FeederReportQueue> report_queue)
        {
            if (!report_queue.HasValue)
                return true;

            if (report_queue.Value.Count == 0)
                return false;

            if (!document.HasValue || !document.Value.HasValue)
                return true;

            if (!state.HasValue || !state.Value.IsLoaded())
                return true;

            foreach (var report in report_queue.Value)
                if (report.Value.MaterialId != document.Value.Value.Id)
                    return true;

            return false;
        }
    }

    public class ToolNozzleEvaluator : TagViewModelEvaluator<ToolNozzleEvaluator, NFCToolNozzle, (Optional<Tool> tool, Optional<Nozzle> nozzle), Nozzle, ToolNozzleState>
    {
        public override Optional<IFluxTagViewModel<NFCToolNozzle, (Optional<Tool> tool, Optional<Nozzle> nozzle), ToolNozzleState>> TagViewModel =>
            ((IFluxTagViewModel<NFCToolNozzle, (Optional<Tool> tool, Optional<Nozzle> nozzle), ToolNozzleState>)FeederEvaluator.Feeder.ToolNozzle).ToOptional();

        public ToolNozzleEvaluator(FluxViewModel flux, FeederEvaluator feeder_eval) : base(flux, feeder_eval)
        {
        }
        public override int GetDocumentId(FeederReport feeder_report)
        {
            return feeder_report.NozzleId;
        }
        public override Optional<Nozzle> GetCurrentDocument((Optional<Tool> tool, Optional<Nozzle> nozzle) tag_document)
        {
            return tag_document.nozzle;
        }
        public override bool GetInvalid(Optional<(Optional<Tool> tool, Optional<Nozzle> nozzle)> document, Optional<ToolNozzleState> state, Optional<FeederReportQueue> report_queue)
        {
            if (!report_queue.HasValue)
                return true;
            if (report_queue.Value.Count == 0)
                return false;

            if (!document.HasValue)
                return true;

            if (!document.Value.tool.HasValue)
                return true;

            if (!document.Value.nozzle.HasValue)
                return true;

            if (!state.HasValue || !state.Value.IsLoaded())
                return true;

            foreach (var report in report_queue.Value)
                if (report.Value.NozzleId != document.Value.nozzle.Value.Id)
                    return true;

            return false;
        }
    }

    public class FeederEvaluator : ReactiveObjectRC<FeederEvaluator>
    {
        private FluxViewModel Flux { get; }
        public IFluxFeederViewModel Feeder { get; }

        private ObservableAsPropertyHelper<Optional<FeederReportQueue>> _FeederReportQueue;
        public Optional<FeederReportQueue> FeederReportQueue => _FeederReportQueue.Value;

        private ObservableAsPropertyHelper<Optional<ExtrusionQueue<ExtrusionG>>> _ExtrusionQueue;
        public Optional<ExtrusionQueue<ExtrusionG>> ExtrusionQueue => _ExtrusionQueue.Value;

        public MaterialEvaluator Material { get; }
        public ToolNozzleEvaluator ToolNozzle { get; }

        private ObservableAsPropertyHelper<Optional<IFluxOffsetViewModel>> _Offset;
        public Optional<IFluxOffsetViewModel> Offset => _Offset.Value;

        private ObservableAsPropertyHelper<bool> _IsInvalidProbe;
        public bool IsInvalidProbe => _IsInvalidProbe.Value;

        private ObservableAsPropertyHelper<bool> _HasColdNozzle;
        public bool HasColdNozzle => _HasColdNozzle.Value;

        private ObservableAsPropertyHelper<Optional<double>> _TargetTemperature;
        public Optional<double> TargetTemperature => _TargetTemperature.Value;

        public FeederEvaluator(FluxViewModel flux, IFluxFeederViewModel feeder)
        {
            Flux = flux;
            Feeder = feeder;
            Material = new MaterialEvaluator(flux, this);
            ToolNozzle = new ToolNozzleEvaluator(flux, this);
        }

        public void Initialize()
        {
            var queue_pos = Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE_POS)
                .StartWithDefault();

            var queue = Flux.StatusProvider
                .WhenAnyValue(s => s.JobQueue);

            var mcode_analyzers = Flux.MCodes.AvaiableMCodes.Connect()
                 .QueryWhenChanged(m => m.KeyValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Analyzer))
                 .StartWith(new Dictionary<MCodeKey, MCodeAnalyzer>())
                 .DistinctUntilChanged();

            _FeederReportQueue = Observable.CombineLatest(
                queue_pos,
                queue,
                mcode_analyzers,
                GetFeederReportQueue)
                .ToPropertyRC(this, e => e.FeederReportQueue);

            _ExtrusionQueue = Observable.CombineLatest(
                Flux.DatabaseProvider
                    .WhenAnyValue(d => d.Database),
                this.WhenAnyValue(f => f.FeederReportQueue),
                Feeder.ToolNozzle.NFCSlot.WhenAnyValue(t => t.Nfc),
                Feeder.WhenAnyValue(v => v.SelectedMaterial)
                    .ConvertMany(m => m.NFCSlot.WhenAnyValue(m => m.Nfc)),
                Flux.Feeders.OdometerManager
                    .WhenAnyValue(c => c.ExtrusionSetQueue),
                GetExtrusionQueue)
                .SelectAsync()
                .ToPropertyRC(this, v => v.ExtrusionQueue);

            _Offset = Flux.Calibration.Offsets.Connect()
                .QueryWhenChanged(FindOffset)
                .ToPropertyRC(this, v => v.Offset);

            var eval_changed = this.WhenAnyValue(e => e.FeederReportQueue);

            var valid_probe = this.WhenAnyValue(s => s.Offset)
                .ConvertMany(o => o.WhenAnyValue(o => o.ProbeState)
                .Select(state => state == FluxProbeState.VALID_PROBE));

            _IsInvalidProbe = Observable.CombineLatest(
                valid_probe,
                eval_changed,
                InvalidProbe)
                .ToPropertyRC(this, e => e.IsInvalidProbe);

            var material = Feeder.WhenAnyValue(f => f.SelectedMaterial);
            var tool_material = material.Convert(m => m.ToolMaterial);

            _TargetTemperature = Flux.StatusProvider.WhenAnyValue(s => s.PrintingEvaluation)
                .Select(TargetTemp)
                .ToPropertyRC(this, v => v.TargetTemperature);

            _HasColdNozzle = Observable.CombineLatest(
                Flux.StatusProvider.WhenAnyValue(s => s.PrintingEvaluation),
                Feeder.ToolNozzle.WhenAnyValue(t => t.NozzleTemperature),
                ColdNozzle)
                .ToPropertyRC(this, e => e.HasColdNozzle);

            Material.Initialize();
            ToolNozzle.Initialize();
        }

        private async Task<Optional<ExtrusionQueue<ExtrusionG>>> GetExtrusionQueue(
            Optional<ILocalDatabase> database,
            Optional<FeederReportQueue> feeder_report_queue,
            NFCReading<NFCToolNozzle> tool_nozzle, 
            Optional<NFCReading<NFCMaterial>> material,
            Optional<ExtrusionSetQueue<ExtrusionMM>> extrusions)
        {
            if (!database.HasValue)
                return default;

            if (!feeder_report_queue.HasValue)
                return default;

            var material_document = await material.Convert(m => m.Tag).ConvertAsync(t => t.GetDocumentAsync<Material>(database.Value, tn => tn.MaterialGuid));
            if (!material_document.HasValue)
                return default;

            if (!extrusions.HasValue)
                return default;

            var total_extrusion_queue = new ExtrusionQueue<ExtrusionG>();
            foreach (var feeder_report in feeder_report_queue.Value)
                total_extrusion_queue.Add(feeder_report.Key, ExtrusionG.CreateTotalExtrusion(feeder_report.Key, feeder_report.Value, material_document.Value));

            var extrusion_key = ExtrusionKey.Create(tool_nozzle, material);
            var extrusion_queue_mm = extrusions.Value.LookupOptional(extrusion_key);
            if (!extrusion_queue_mm.HasValue)
                return total_extrusion_queue;

            var extrusion_queue_g = new ExtrusionQueue<ExtrusionG>();
            foreach (var extrusion in extrusion_queue_mm.Value)
                extrusion_queue_g.Add(extrusion.Key, ExtrusionG.CreateExtrusion(material_document.Value, extrusion.Value));

            foreach (var extrusion in extrusion_queue_g)
                if (total_extrusion_queue.ContainsKey(extrusion.Key))
                    total_extrusion_queue[extrusion.Key] -= extrusion.Value;

            return total_extrusion_queue.ToOptional();
        }

        private bool ColdNozzle(PrintingEvaluation evaluation, Optional<FLUX_Temp> plc_temp)
        {
            var tool_index = evaluation.Recovery
                .Convert(r => r.ToolIndex);
            if (!tool_index.HasValue)
                return false;

            var temperature = evaluation.Recovery
                .Convert(pp => pp.ToolTemperatures)
                .Convert(tt => tt.LookupOptional(tool_index));
            if (!temperature.HasValue)
                return false;

            var variable_store = Flux.ConnectionProvider.VariableStoreBase;
            var tool_number = tool_index.Value.GetZeroBaseIndex();

            if (tool_number != Feeder.Position)
                return false;
            if (!plc_temp.HasValue)
                return true;
            if (plc_temp.Value.Target == 0)
                return true;
            return temperature.Value - plc_temp.Value.Current > 10;
        }
        private Optional<double> TargetTemp(PrintingEvaluation evaluation)
        {
            var tool_index = evaluation.Recovery
                .Convert(pp => pp.ToolIndex);
            if (!tool_index.HasValue)
                return default;

            var temperature = evaluation.Recovery
                .Convert(pp => pp.ToolTemperatures)
                .Convert(tt => tt.LookupOptional(tool_index));
            if (!temperature.HasValue)
                return default;

            var variable_store = Flux.ConnectionProvider.VariableStoreBase;
            var tool_number = tool_index.Value.GetZeroBaseIndex();

            if (tool_number != Feeder.Position)
                return default;
            return temperature.Value;
        }
        private Optional<IFluxOffsetViewModel> FindOffset(IQuery<IFluxOffsetViewModel, ushort> offsets)
        {
            return offsets.LookupOptional(Feeder.Position);
        }

        private bool InvalidProbe(Optional<bool> is_probed, Optional<FeederReportQueue> report_queue)
        {
            if (!report_queue.HasValue)
                return true;
            if (report_queue.Value.Count == 0)
                return false;

            if (!is_probed.HasValue)
                return true;

            return !is_probed.Value;
        }

        private Optional<FeederReportQueue> GetFeederReportQueue(Optional<QueuePosition> queue_pos, Optional<JobQueue> job_queue, Dictionary<MCodeKey, MCodeAnalyzer> mcode_analyzers)
        {
            try
            {
                if (!job_queue.HasValue)
                    return default;

                if (!queue_pos.HasValue)
                    return default;

                var feeder_report_queue = new FeederReportQueue();
                if (queue_pos.Value < 0)
                    return feeder_report_queue;

                foreach (var job in job_queue.Value.Values)
                {
                    if (job.QueuePosition < queue_pos.Value)
                        continue;

                    var mcode_analyzer = mcode_analyzers.Lookup(job.MCodeKey);
                    if (!mcode_analyzer.HasValue)
                        continue;

                    var feeder_report = mcode_analyzer.Value.MCode.FeederReports.Lookup(Feeder.Position);
                    if (!feeder_report.HasValue)
                        continue;

                    feeder_report_queue.Add(job.JobKey, feeder_report.Value);
                }
                return feeder_report_queue;
            }
            catch (Exception)
            {
                return default;
            }
        }
    }
}
