using DynamicData;
using DynamicData.Kernel;
using Modulo3DDatabase;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

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

    public abstract class TagViewModelEvaluator<TNFCTag, TTagDocument, TDocument, TState> : ReactiveObject, ITagViewModelEvaluator
        where TNFCTag : INFCOdometerTag<TNFCTag>
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

        public TagViewModelEvaluator(FluxViewModel flux, FeederEvaluator feeder_eval)
        {
            Flux = flux;
            FeederEvaluator = feeder_eval;
        }

        public void Initialize()
        {
            var queue = Flux.ConnectionProvider.ObserveVariable(c => c.QUEUE);
            var queue_pos = Flux.ConnectionProvider.ObserveVariable(c => c.QUEUE_POS);

            var db_changed = Flux.DatabaseProvider.WhenAnyValue(v => v.Database);
            var eval_changed = FeederEvaluator.WhenAnyValue(e => e.FeederReportQueue);

            _ExpectedDocumentQueue = Observable.CombineLatest(
                queue,
                queue_pos,
                db_changed,
                eval_changed,
                (q, p, db, f) => GetExpectedDocumentQueue(q, p, db, f))
                .ToProperty(this, v => v.ExpectedDocumentQueue);

            _CurrentDocument = this.WhenAnyValue(v => v.TagViewModel)
                .ConvertMany(t => t.WhenAnyValue(t => t.Document))
                .Convert(GetCurrentDocument)
                .ToProperty(this, v => v.CurrentDocument);

            _IsInvalid = Observable.CombineLatest(
                this.WhenAnyValue(v => v.TagViewModel)
                    .ConvertMany(t => t.WhenAnyValue(t => t.Document)),
                this.WhenAnyValue(v => v.TagViewModel)
                    .ConvertMany(t => t.WhenAnyValue(t => t.State)),
                eval_changed,
                GetInvalid)
                .ToProperty(this, e => e.IsInvalid);

            _ExpectedWeight = FeederEvaluator.WhenAnyValue(f => f.ExtrusionQueue)
                .Convert(e => e.Values.Sum(e => e.WeightG))
                .ToProperty(this, v => v.ExpectedWeight);

            _CurrentWeight = this.WhenAnyValue(v => v.TagViewModel)
                .ConvertMany(t => t.Odometer.WhenAnyValue(t => t.CurrentValue))
                .ToProperty(this, v => v.CurrentWeight);

            _HasLowWeight = Observable.CombineLatest(
                this.WhenAnyValue(v => v.ExpectedWeight),
                this.WhenAnyValue(v => v.CurrentWeight),
                eval_changed,
                LowWeight)
                .ToProperty(this, v => v.HasLowWeight);
        }

        public abstract int GetDocumentId(FeederReport feeder_report);
        public abstract Optional<TDocument> GetCurrentDocument(TTagDocument tag_document);
        public abstract bool GetInvalid(Optional<TTagDocument> document, Optional<TState> state, Optional<FeederReportQueue> report);

        private Optional<DocumentQueue<TDocument>> GetExpectedDocumentQueue(
            Optional<JobQueue> job_queue,
            Optional<QueuePosition> queue_position, 
            Optional<ILocalDatabase> database, 
            Optional<FeederReportQueue> feeder_queue)
        {
            try
            {
                if (!job_queue.HasValue)
                    return default;

                var start_queue_pos = queue_position.ValueOr(() => 0);
                if (start_queue_pos < 0)
                    start_queue_pos = 0;

                var job = job_queue.Value.LookupOptional(start_queue_pos);
                if (!job.HasValue)
                    return default;

                if (!database.HasValue)
                    return default;
                if (!feeder_queue.HasValue)
                    return default;

                var document_queue = new DocumentQueue<TDocument>();
                foreach (var feeder_report in feeder_queue.Value)
                {
                    if (!feeder_report.Key.Equals(job.Value.JobKey))
                        continue;

                    var document_id = GetDocumentId(feeder_report.Value);
                    var result = database.Value.FindById<TDocument>(document_id);
                    if (!result.HasDocuments)
                        continue;

                    document_queue.Add(feeder_report.Key, result.Documents.First());
                }
                return document_queue;
            }
            catch (Exception ex)
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
            catch (Exception ex)
            {
                return false;
            }
        }
    }

    public class MaterialEvaluator : TagViewModelEvaluator<NFCMaterial, Optional<Material>, Material, MaterialState>
    {
        private ObservableAsPropertyHelper<Optional<IFluxTagViewModel<NFCMaterial, Optional<Material>, MaterialState>>> _TagViewModel;
        public override Optional<IFluxTagViewModel<NFCMaterial, Optional<Material>, MaterialState>> TagViewModel => _TagViewModel.Value;
        
        public MaterialEvaluator(FluxViewModel flux, FeederEvaluator feeder_eval) : base(flux, feeder_eval)
        {
            _TagViewModel = FeederEvaluator.Feeder.WhenAnyValue(f => f.SelectedMaterial)
                .Convert(m => (IFluxTagViewModel<NFCMaterial, Optional<Material>, MaterialState>)m)
                .ToProperty(this, v => v.TagViewModel);
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

    public class ToolNozzleEvaluator : TagViewModelEvaluator<NFCToolNozzle, (Optional<Tool> tool, Optional<Nozzle> nozzle), Nozzle, ToolNozzleState>
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

    public class FeederEvaluator : ReactiveObject
    {
        FluxViewModel Flux { get; }
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

            var queue = Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE)
                .StartWithDefault();

            var mcode_analyzers = Flux.MCodes.AvaiableMCodes.Connect()
                 .QueryWhenChanged(m => m.KeyValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Analyzer))
                 .StartWith(new Dictionary<MCodeKey, MCodeAnalyzer>())
                 .DistinctUntilChanged();

            _FeederReportQueue = Observable.CombineLatest(
                queue_pos,
                queue,
                mcode_analyzers,
                GetFeederReportQueue)
                .ToProperty(this, e => e.FeederReportQueue);

            _ExtrusionQueue = Observable.CombineLatest(
                Flux.DatabaseProvider
                    .WhenAnyValue(d => d.Database),
                this.WhenAnyValue(f => f.FeederReportQueue),
                Feeder.ToolNozzle.WhenAnyValue(t => t.Nfc),
                Feeder.WhenAnyValue(v => v.SelectedMaterial)
                    .ConvertMany(m => m.WhenAnyValue(m => m.Nfc)),
                Flux.ConnectionProvider
                    .ObserveVariable(c => c.EXTRUSIONS),
                (db, feeder_report_queue, tool_nozzle, material, extrusions) =>
                {
                    if (!db.HasValue)
                        return default;

                    if (!feeder_report_queue.HasValue)
                        return default;

                    var tool_document = tool_nozzle.Tag.Convert(t => t.GetDocument<Tool>(db.Value, tn => tn.ToolGuid));
                    if (!tool_document.HasValue)
                        return default;

                    var material_document = material.Convert(m => m.Tag).Convert(t => t.GetDocument<Material>(db.Value, tn => tn.MaterialGuid));
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
                        extrusion_queue_g.Add(extrusion.Key, ExtrusionG.CreateExtrusion(tool_document.Value, material_document.Value, extrusion.Value));

                    foreach (var extrusion in extrusion_queue_g)
                        if (total_extrusion_queue.ContainsKey(extrusion.Key))
                            total_extrusion_queue[extrusion.Key] -= extrusion.Value;

                    return total_extrusion_queue.ToOptional();
                })
                .ToProperty(this, v => v.ExtrusionQueue);

            _Offset = Flux.Calibration.Offsets.Connect()
                .QueryWhenChanged(FindOffset)
                .ToProperty(this, v => v.Offset);

            var eval_changed = this.WhenAnyValue(e => e.FeederReportQueue);

            var valid_probe = this.WhenAnyValue(s => s.Offset)
                .ConvertMany(o => o.WhenAnyValue(o => o.ProbeState)
                .Select(state => state == FluxProbeState.VALID_PROBE));

            _IsInvalidProbe = Observable.CombineLatest(
                valid_probe,
                eval_changed,
                InvalidProbe)
                .ToProperty(this, e => e.IsInvalidProbe);

            var material = Feeder.WhenAnyValue(f => f.SelectedMaterial);
            var tool_material = material.Convert(m => m.ToolMaterial);

            _HasColdNozzle = Observable.CombineLatest(
                Flux.StatusProvider.WhenAnyValue(s => s.PrintingEvaluation),
                Feeder.ToolNozzle.WhenAnyValue(t => t.NozzleTemperature),
                tool_material.ConvertMany(tm => tm.WhenAnyValue(t => t.ExtrusionTemp)),
                ColdNozzle)
                .ToProperty(this, e => e.HasColdNozzle);

            Material.Initialize();
            ToolNozzle.Initialize();
        }

        private bool ColdNozzle(PrintingEvaluation evaluation, Optional<FLUX_Temp> plc_temp, Optional<double> extrusion_temp)
        {
            if (!evaluation.CurrentRecovery.ConvertOr(r =>  r.ToolNumber == Feeder.Position, () => false))
                return false;
            if (!extrusion_temp.HasValue)
                return true;
            if (!plc_temp.HasValue)
                return true;
            if (plc_temp.Value.Target == 0)
                return true;
            return plc_temp.Value.Target.ValueOr(() => 0) - plc_temp.Value.Current > 10;
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
                foreach (var job in job_queue.Value.Values)
                {
                    if (job.QueuePosition < queue_pos.Value)
                        continue;

                    var mcode_analyzer = mcode_analyzers.Lookup(job.PartProgram.MCodeKey);
                    if (!mcode_analyzer.HasValue)
                        continue;

                    var feeder_report = mcode_analyzer.Value.MCode.FeederReports.Lookup(Feeder.Position);
                    if (!feeder_report.HasValue)
                        continue;

                    feeder_report_queue.Add(job.JobKey, feeder_report.Value);
                }
                return feeder_report_queue;
            }
            catch (Exception ex)
            {
                return default;
            }
        }
    }
}
