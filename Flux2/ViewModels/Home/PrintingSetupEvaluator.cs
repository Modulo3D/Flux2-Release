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
        where TDocument : IDocument
    {
        public FeederEvaluator FeederEvaluator { get; }
        public abstract IFluxTagViewModel<TNFCTag, TTagDocument, TState> TagViewModel { get; }

        private ObservableAsPropertyHelper<Optional<Dictionary<QueueKey, TDocument>>> _ExpectedDocumentQueue;
        public Optional<Dictionary<QueueKey, TDocument>> ExpectedDocumentQueue => _ExpectedDocumentQueue.Value;

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

        public TagViewModelEvaluator(FeederEvaluator feeder_eval)
        {
            FeederEvaluator = feeder_eval;
        }

        public void Initialize()
        {
            var partprogram = FeederEvaluator.Feeder.Flux.ConnectionProvider.ObserveVariable(c => c.PART_PROGRAM);
            var queue_pos = FeederEvaluator.Feeder.Flux.ConnectionProvider.ObserveVariable(c => c.QUEUE_POS);

            var queue_key = Observable.CombineLatest(partprogram, queue_pos, (pp, q) => 
                pp.Convert(pp => q.Convert(q => new QueueKey(pp.MCodeGuid, q))));

            var db_changed = FeederEvaluator.Feeder.Flux.DatabaseProvider.WhenAnyValue(v => v.Database);
            var eval_changed = FeederEvaluator.WhenAnyValue(e => e.FeederReportQueue);

            _ExpectedDocumentQueue = Observable.CombineLatest(
                queue_key,
                db_changed,
                eval_changed,
                (p, db, f) => GetExpectedDocumentQueue(p, db, f, GetDocumentId))
                .ToProperty(this, v => v.ExpectedDocumentQueue);

            _CurrentDocument = TagViewModel.WhenAnyValue(t => t.Document)
                .Select(GetCurrentDocument)
                .ToProperty(this, v => v.CurrentDocument);

            _IsInvalid = Observable.CombineLatest(
                TagViewModel.WhenAnyValue(f => f.Document),
                TagViewModel.WhenAnyValue(f => f.State),
                eval_changed,
                GetInvalid)
                .ToProperty(this, e => e.IsInvalid);

            _ExpectedWeight = FeederEvaluator.WhenAnyValue(f => f.ExtrusionQueue)
                .Convert(e => e.Values.Sum(e => e.ConvertOr(e => (double)e.WeightG, () => 0)))
                .ToProperty(this, v => v.ExpectedWeight);

            _CurrentWeight = TagViewModel.Odometer.WhenAnyValue(v => v.Value)
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
        public abstract bool GetInvalid(TTagDocument document, TState state, Optional<Dictionary<QueueKey, FeederReport>> report);

        private Optional<Dictionary<QueueKey, TDocument>> GetExpectedDocumentQueue(Optional<QueueKey> queue_key, Optional<ILocalDatabase> database, Optional<Dictionary<QueueKey, FeederReport>> feered_queue, Func<FeederReport, int> get_id)
        {
            try
            {
                if (!queue_key.HasValue)
                    return default;
                if (!database.HasValue)
                    return default;
                if (!feered_queue.HasValue)
                    return default;

                var document_queue = new Dictionary<QueueKey, TDocument>();
                foreach (var feeder_queue in feered_queue.Value)
                {
                    if (feeder_queue.Key.MCodeGuid != queue_key.Value.MCodeGuid)
                        continue;

                    if (feeder_queue.Key.QueuePosition < queue_key.Value.QueuePosition)
                        continue;

                    var feeder_report = feered_queue.Value.Lookup(feeder_queue.Key);
                    if (!feeder_report.HasValue)
                        continue;

                    var document_id = get_id(feeder_report.Value);
                    var result = database.Value.FindById<TDocument>(document_id);
                    if (!result.HasDocuments)
                        continue;

                    document_queue.Add(feeder_queue.Key, result.Documents.First());
                }
                return document_queue;
            }
            catch (Exception ex)
            {
                return default;
            }
        }
        private bool LowWeight(Optional<double> expected, Optional<double> current, Optional<Dictionary<QueueKey, FeederReport>> feeder_queue)
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
        public override IFluxTagViewModel<NFCMaterial, Optional<Material>, MaterialState> TagViewModel => FeederEvaluator.Feeder.Material;
        public MaterialEvaluator(FeederEvaluator feeder_eval) : base(feeder_eval)
        {
        }
        public override Optional<Material> GetCurrentDocument(Optional<Material> tag_document)
        {
            return tag_document;
        }
        public override int GetDocumentId(FeederReport feeder_report)
        {
            return feeder_report.MaterialId;
        }
        public override bool GetInvalid(Optional<Material> document, MaterialState state, Optional<Dictionary<QueueKey, FeederReport>> report_queue)
        {
            if (!report_queue.HasValue)
                return true;

            if (report_queue.Value.Count == 0)
                return false;

            if (!document.HasValue)
                return true;

            if (!state.IsLoaded())
                return true;

            foreach (var report in report_queue.Value)
                if (report.Value.MaterialId != document.Value.Id)
                    return true;

            return false;
        }
    }

    public class ToolNozzleEvaluator : TagViewModelEvaluator<NFCToolNozzle, (Optional<Tool> tool, Optional<Nozzle> nozzle), Nozzle, ToolNozzleState>
    {
        public override IFluxTagViewModel<NFCToolNozzle, (Optional<Tool> tool, Optional<Nozzle> nozzle), ToolNozzleState> TagViewModel => FeederEvaluator.Feeder.ToolNozzle;
        public ToolNozzleEvaluator(FeederEvaluator feeder_eval) : base(feeder_eval)
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
        public override bool GetInvalid((Optional<Tool> tool, Optional<Nozzle> nozzle) document, ToolNozzleState state, Optional<Dictionary<QueueKey, FeederReport>> report_queue)
        {
            if (!report_queue.HasValue)
                return true;

            if (report_queue.Value.Count == 0)
                return false;

            if (!document.tool.HasValue)
                return true;

            if (!document.nozzle.HasValue)
                return true;

            if (!state.IsLoaded())
                return true;

            foreach (var report in report_queue.Value)
                if (report.Value.NozzleId != document.nozzle.Value.Id)
                    return true;

            return false;
        }
    }

    public class FeederEvaluator : ReactiveObject
    {
        public StatusProvider Status { get; }
        public IFluxFeederViewModel Feeder { get; }

        private ObservableAsPropertyHelper<Optional<Dictionary<QueueKey, FeederReport>>> _FeederReportQueue;
        public Optional<Dictionary<QueueKey, FeederReport>> FeederReportQueue => _FeederReportQueue.Value;

        public MaterialEvaluator Material { get; }
        public ToolNozzleEvaluator ToolNozzle { get; }

        private ObservableAsPropertyHelper<Optional<IFluxOffsetViewModel>> _Offset;
        public Optional<IFluxOffsetViewModel> Offset => _Offset.Value;

        private ObservableAsPropertyHelper<bool> _IsInvalidProbe;
        public bool IsInvalidProbe => _IsInvalidProbe.Value;

        private ObservableAsPropertyHelper<bool> _HasColdNozzle;
        public bool HasColdNozzle => _HasColdNozzle.Value;

        private ObservableAsPropertyHelper<Optional<Dictionary<QueueKey, Optional<Extrusion>>>> _ExtrusionQueue;
        public Optional<Dictionary<QueueKey, Optional<Extrusion>>> ExtrusionQueue => _ExtrusionQueue.Value;

        public FeederEvaluator(StatusProvider status, IFluxFeederViewModel feeder)
        {
            Status = status;
            Feeder = feeder;
            Material = new MaterialEvaluator(this);
            ToolNozzle = new ToolNozzleEvaluator(this);
        }

        public void Initialize()
        {
            var queue_pos = Feeder.Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE_POS)
                .StartWithEmpty()
                .DistinctUntilChanged();

            bool compare_queue(Optional<Dictionary<QueuePosition, Guid>> q1, Optional<Dictionary<QueuePosition, Guid>> q2)
            {
                try
                {
                    if (q1.HasValue != q2.HasValue)
                        return false;
                    if (q1.HasValue && q2.HasValue)
                    {
                        foreach (var qi in q2.Value)
                        {
                            if (!q1.Value.TryGetValue(qi.Key, out var guid))
                                return false;
                            if (qi.Value != guid)
                                return false;
                        }
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }

            var queue = Feeder.Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE)
                .StartWithEmpty()
                .DistinctUntilChanged(compare_queue);

            var mcodes = Feeder.Flux.MCodes.AvaiableMCodes
                .Connect()
                .QueryWhenChanged();

            _FeederReportQueue = Observable.CombineLatest(
                queue_pos,
                queue,
                mcodes,
                GetFeederReportQueue)
                .ToProperty(this, e => e.FeederReportQueue);

            _ExtrusionQueue = Status.WhenAnyValue(s => s.PrintingEvaluation)
                .Select(e => e.ExtrusionSetQueue)
                .Convert(e => e.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Extrusions.Lookup(Feeder.Position)))
                .ToProperty(this, v => v.ExtrusionQueue);

            _Offset = Feeder.Flux.Calibration.Offsets.Connect()
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

            _HasColdNozzle = Observable.CombineLatest(
                Status.WhenAnyValue(s => s.PrintingEvaluation),
                Feeder.ToolNozzle.WhenAnyValue(t => t.Temperature),
                Feeder.ToolMaterial.WhenAnyValue(t => t.ExtrusionTemp),
                ColdNozzle)
                .ToProperty(this, e => e.HasColdNozzle);

            Material.Initialize();
            ToolNozzle.Initialize();
        }

        private bool ColdNozzle(PrintingEvaluation evaluation, Optional<FLUX_Temp> plc_temp, Optional<double> extrusion_temp)
        {
            if (!evaluation.Recovery.ConvertOr(r => r.IsSelected, () => false))
                return false;
            if (!evaluation.Recovery.ConvertOr(r =>  r.ToolNumber == Feeder.Position, () => false))
                return false;
            if (!extrusion_temp.HasValue)
                return true;
            if (!plc_temp.HasValue)
                return true;
            if (plc_temp.Value.Target > 150)
                return Math.Abs(plc_temp.Value.Current - plc_temp.Value.Target) > 10;
            return Math.Abs(plc_temp.Value.Current - extrusion_temp.Value) > 15;
        }

        private Optional<IFluxOffsetViewModel> FindOffset(IQuery<IFluxOffsetViewModel, ushort> offsets)
        {
            return offsets.LookupOptional(Feeder.Position);
        }

        private bool InvalidProbe(Optional<bool> is_probed, Optional<Dictionary<QueueKey, FeederReport>> report_queue)
        {
            if (!report_queue.HasValue)
                return true;

            if (report_queue.Value.Count == 0)
                return false;

            if (!is_probed.HasValue)
                return true;

            return !is_probed.Value;
        }

        private Optional<Dictionary<QueueKey, FeederReport>> GetFeederReportQueue(Optional<QueuePosition> queue_pos, Optional<Dictionary<QueuePosition, Guid>> queue, IQuery<IFluxMCodeStorageViewModel, Guid> mcodes)
        {
            try
            {
                if (!queue_pos.HasValue)
                    return default;
                if (!queue.HasValue)
                    return default;

                var feeder_report_queue = new Dictionary<QueueKey, FeederReport>();
                foreach (var mcode_queue in queue.Value)
                {
                    if (mcode_queue.Key < queue_pos.Value)
                        continue;

                    var mcode_vm = mcodes.Lookup(mcode_queue.Value);
                    if (!mcode_vm.HasValue)
                        continue;

                    var analyzer = mcode_vm.Value.Analyzer;
                    var feeder_report = analyzer.MCode.Feeders.Lookup(Feeder.Position);
                    if (!feeder_report.HasValue)
                        continue;

                    var queue_key = new QueueKey(analyzer.MCode.MCodeGuid, mcode_queue.Key);
                    feeder_report_queue.Add(queue_key, feeder_report.Value);
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
