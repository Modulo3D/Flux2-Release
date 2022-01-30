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
        public bool HasEnoughWeight { get; }
        public bool IsValid { get; }
    }

    public abstract class TagViewModelEvaluator<TNFCTag, TTagDocument, TDocument, TState> : ReactiveObject, ITagViewModelEvaluator
        where TNFCTag : INFCOdometerTag<TNFCTag>
        where TDocument : IDocument
    {
        public FeederEvaluator FeederEvaluator { get; }
        public abstract IFluxTagViewModel<TNFCTag, TTagDocument, TState> TagViewModel { get; }

        private ObservableAsPropertyHelper<Optional<Dictionary<ushort, TDocument>>> _ExpectedDocumentQueue;
        public Optional<Dictionary<ushort, TDocument>> ExpectedDocumentQueue => _ExpectedDocumentQueue.Value;

        private ObservableAsPropertyHelper<Optional<TDocument>> _CurrentDocument;
        public Optional<TDocument> CurrentDocument => _CurrentDocument.Value;

        private ObservableAsPropertyHelper<bool> _IsValid;
        public bool IsValid => _IsValid.Value;

        private ObservableAsPropertyHelper<Optional<double>> _ExpectedWeight;
        public Optional<double> ExpectedWeight => _ExpectedWeight.Value;

        private ObservableAsPropertyHelper<Optional<double>> _CurrentWeight;
        public Optional<double> CurrentWeight => _CurrentWeight.Value;

        private ObservableAsPropertyHelper<bool> _HasEnoughWeight;
        public bool HasEnoughWeight => _HasEnoughWeight.Value;

        public TagViewModelEvaluator(FeederEvaluator feeder_eval)
        {
            FeederEvaluator = feeder_eval;
        }

        public void Initialize()
        {
            var queue_pos = FeederEvaluator.Feeder.Flux.ConnectionProvider.ObserveVariable(c => c.QUEUE_POS);
            var db_changed = FeederEvaluator.Feeder.Flux.DatabaseProvider.WhenAnyValue(v => v.Database);
            var eval_changed = FeederEvaluator.WhenAnyValue(e => e.FeederReportQueue);

            _ExpectedDocumentQueue = Observable.CombineLatest(
                queue_pos,
                db_changed,
                eval_changed,
                (p, db, f) => GetExpectedDocumentQueue(p, db, f, GetDocumentId))
                .ToProperty(this, v => v.ExpectedDocumentQueue);

            _CurrentDocument = TagViewModel.WhenAnyValue(t => t.Document)
                .Select(GetCurrentDocument)
                .ToProperty(this, v => v.CurrentDocument);

            _IsValid = Observable.CombineLatest(
                TagViewModel.WhenAnyValue(f => f.Document),
                TagViewModel.WhenAnyValue(f => f.State),
                eval_changed,
                GetValid)
                .ToProperty(this, e => e.IsValid);

            _ExpectedWeight = FeederEvaluator.WhenAnyValue(f => f.ExtrusionQueue)
                .Convert(e => e.Values.Sum(e => e.ConvertOr(e => (double)e.WeightG, () => 0)))
                .ToProperty(this, v => v.ExpectedWeight);

            _CurrentWeight = TagViewModel.Odometer.WhenAnyValue(v => v.Value)
                .ToProperty(this, v => v.CurrentWeight);

            _HasEnoughWeight = Observable.CombineLatest(
                this.WhenAnyValue(v => v.ExpectedWeight),
                this.WhenAnyValue(v => v.CurrentWeight),
                eval_changed,
                GetEnoughWeight)
                .ToProperty(this, v => v.HasEnoughWeight);
        }

        public abstract int GetDocumentId(FeederReport feeder_report);
        public abstract Optional<TDocument> GetCurrentDocument(TTagDocument tag_document);
        public abstract bool GetValid(TTagDocument document, TState state, Optional<Dictionary<ushort, FeederReport>> report);

        private Optional<Dictionary<ushort, TDocument>> GetExpectedDocumentQueue(Optional<short> queue_pos, Optional<ILocalDatabase> database, Optional<Dictionary<ushort, FeederReport>> feered_queue, Func<FeederReport, int> get_id)
        {
            if (!queue_pos.HasValue)
                return default;
            if (!database.HasValue)
                return default;
            if (!feered_queue.HasValue)
                return default;

            var document_queue = new Dictionary<ushort, TDocument>();
            foreach (var feeder_queue in feered_queue.Value)
            {
                if (feeder_queue.Key < queue_pos.Value)
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
        private bool GetEnoughWeight(Optional<double> expected, Optional<double> current, Optional<Dictionary<ushort, FeederReport>> feeder_queue)
        {
            if (!feeder_queue.HasValue)
                return false;
            if (feeder_queue.Value.Count == 0)
                return true;
            if (!expected.HasValue)
                return false;
            if (!current.HasValue)
                return false;
            return current.Value >= expected.Value;
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
        public override bool GetValid(Optional<Material> document, MaterialState state, Optional<Dictionary<ushort, FeederReport>> report_queue)
        {
            if (!report_queue.HasValue)
                return false;

            if (report_queue.Value.Count == 0)
                return true;

            if (!document.HasValue)
                return false;

            if (!state.IsLoaded())
                return false;

            foreach (var report in report_queue.Value)
                if (report.Value.MaterialId != document.Value.Id)
                    return false;

            return true;
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
        public override bool GetValid((Optional<Tool> tool, Optional<Nozzle> nozzle) document, ToolNozzleState state, Optional<Dictionary<ushort, FeederReport>> report_queue)
        {
            if (!report_queue.HasValue)
                return false;

            if (report_queue.Value.Count == 0)
                return true;

            if (!document.tool.HasValue)
                return false;

            if (!document.nozzle.HasValue)
                return false;

            if (!state.IsLoaded())
                return false;

            foreach (var report in report_queue.Value)
                if (report.Value.NozzleId != document.nozzle.Value.Id)
                    return false;

            return true;
        }
    }

    public class FeederEvaluator : ReactiveObject
    {
        public StatusProvider Status { get; }
        public IFluxFeederViewModel Feeder { get; }

        private ObservableAsPropertyHelper<Optional<Dictionary<ushort, FeederReport>>> _FeederReportQueue;
        public Optional<Dictionary<ushort, FeederReport>> FeederReportQueue => _FeederReportQueue.Value;

        public MaterialEvaluator Material { get; }
        public ToolNozzleEvaluator ToolNozzle { get; }

        private ObservableAsPropertyHelper<Optional<IFluxOffsetViewModel>> _Offset;
        public Optional<IFluxOffsetViewModel> Offset => _Offset.Value;

        private ObservableAsPropertyHelper<bool> _IsValidProbe;
        public bool IsValidProbe => _IsValidProbe.Value;

        private ObservableAsPropertyHelper<Optional<bool>> _HasHotNozzle;
        public Optional<bool> HasHotNozzle => _HasHotNozzle.Value;

        private ObservableAsPropertyHelper<Optional<Dictionary<ushort, Optional<Extrusion>>>> _ExtrusionQueue;
        public Optional<Dictionary<ushort, Optional<Extrusion>>> ExtrusionQueue => _ExtrusionQueue.Value;

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

            bool compare_queue(Optional<Dictionary<ushort, Guid>> q1, Optional<Dictionary<ushort, Guid>> q2)
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

            _IsValidProbe = Observable.CombineLatest(
                valid_probe,
                eval_changed,
                ValidProbe)
                .ToProperty(this, e => e.IsValidProbe);

            _HasHotNozzle = Observable.CombineLatest(
                Status.WhenAnyValue(s => s.PrintingEvaluation),
                Feeder.ToolNozzle.WhenAnyValue(t => t.Temperature),
                Feeder.ToolMaterial.WhenAnyValue(t => t.ExtrusionTemp),
                HotNozzle)
                .ToProperty(this, e => e.HasHotNozzle);

            Material.Initialize();
            ToolNozzle.Initialize();
        }

        private Optional<bool> HotNozzle(PrintingEvaluation evaluation, Optional<FLUX_Temp> plc_temp, Optional<double> extrusion_temp)
        {
            if (!evaluation.SelectedRecovery.HasValue)
                return default;
            if (evaluation.SelectedRecovery.Value.ToolNumber != Feeder.Position)
                return default;
            if (!extrusion_temp.HasValue)
                return false;
            if (!plc_temp.HasValue)
                return false;
            // TODO
            if (plc_temp.Value.Target > 150 && Math.Abs(plc_temp.Value.Current - plc_temp.Value.Target) > 5)
                return false;
            return extrusion_temp.Value - plc_temp.Value.Current < 10;
        }

        private Optional<IFluxOffsetViewModel> FindOffset(IQuery<IFluxOffsetViewModel, ushort> offsets)
        {
            return offsets.LookupOptional(Feeder.Position);
        }

        private bool ValidProbe(Optional<bool> is_probed, Optional<Dictionary<ushort, FeederReport>> report_queue)
        {
            if (!report_queue.HasValue)
                return false;

            if (report_queue.Value.Count == 0)
                return true;

            if (!is_probed.HasValue)
                return false;

            return is_probed.Value;
        }

        private Optional<Dictionary<ushort, FeederReport>> GetFeederReportQueue(Optional<short> queue_pos, Optional<Dictionary<ushort, Guid>> queue, IQuery<IFluxMCodeStorageViewModel, Guid> mcodes)
        {
            if (!queue_pos.HasValue)
                return default;
            if (!queue.HasValue)
                return default;

            var feeder_report_queue = new Dictionary<ushort, FeederReport>();
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

                feeder_report_queue.Add(mcode_queue.Key, feeder_report.Value);
            }
            return feeder_report_queue;
        }
    }
}
