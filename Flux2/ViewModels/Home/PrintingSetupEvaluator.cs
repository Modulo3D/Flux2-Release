﻿using DynamicData;
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
        public FeederEvaluator FeederEvaluator { get; }
        public abstract Optional<IFluxTagViewModel<TNFCTag, TTagDocument, TState>> TagViewModel { get; }

        private ObservableAsPropertyHelper<Optional<Dictionary<FluxJob, TDocument>>> _ExpectedDocumentQueue;
        public Optional<Dictionary<FluxJob, TDocument>> ExpectedDocumentQueue => _ExpectedDocumentQueue.Value;

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
            var queue = FeederEvaluator.Status.Flux.ConnectionProvider.ObserveVariable(c => c.QUEUE);
            var queue_pos = FeederEvaluator.Status.Flux.ConnectionProvider.ObserveVariable(c => c.QUEUE_POS);

            var db_changed = FeederEvaluator.Status.Flux.DatabaseProvider.WhenAnyValue(v => v.Database);
            var eval_changed = FeederEvaluator.WhenAnyValue(e => e.FeederReportQueue);

            _ExpectedDocumentQueue = Observable.CombineLatest(
                queue,
                queue_pos,
                db_changed,
                eval_changed,
                (q, p, db, f) => GetExpectedDocumentQueue(q, p, db, f, GetDocumentId))
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
                .Convert(e => e.Values.Sum(e => e.ConvertOr(e => e.WeightG, () => 0)))
                .ToProperty(this, v => v.ExpectedWeight);

            _CurrentWeight = this.WhenAnyValue(v => v.TagViewModel)
                .ConvertMany(t => t.Odometer.WhenAnyValue(t => t.Value))
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
        public abstract bool GetInvalid(Optional<TTagDocument> document, Optional<TState> state, Optional<Dictionary<FluxJob, FeederReport>> report);

        private Optional<Dictionary<FluxJob, TDocument>> GetExpectedDocumentQueue(
            Optional<Dictionary<QueuePosition, FluxJob>> queue,
            Optional<QueuePosition> queue_position, 
            Optional<ILocalDatabase> database, 
            Optional<Dictionary<FluxJob, FeederReport>> feered_queue,
            Func<FeederReport, int> get_id)
        {
            try
            {
                if (!queue.HasValue)
                    return default;
                if (!queue_position.HasValue)
                    return default;
                if (!database.HasValue)
                    return default;
                if (!feered_queue.HasValue)
                    return default;

                var job = queue.Value.Lookup(queue_position.Value);
                if (!job.HasValue)
                    return default;

                var document_queue = new Dictionary<FluxJob, TDocument>();
                foreach (var feeder_queue in feered_queue.Value)
                {
                    if (feeder_queue.Key.MCodeGuid != job.Value.MCodeGuid)
                        continue;

                    if (feeder_queue.Key.QueuePosition < job.Value.QueuePosition)
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
        private bool LowWeight(Optional<double> expected, Optional<double> current, Optional<Dictionary<FluxJob, FeederReport>> feeder_queue)
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
        
        public MaterialEvaluator(FeederEvaluator feeder_eval) : base(feeder_eval)
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
        public override bool GetInvalid(Optional<Optional<Material>> document, Optional<MaterialState> state, Optional<Dictionary<FluxJob, FeederReport>> report_queue)
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
        public override bool GetInvalid(Optional<(Optional<Tool> tool, Optional<Nozzle> nozzle)> document, Optional<ToolNozzleState> state, Optional<Dictionary<FluxJob, FeederReport>> report_queue)
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
        public StatusProvider Status { get; }
        public IFluxFeederViewModel Feeder { get; }

        private ObservableAsPropertyHelper<Optional<Dictionary<FluxJob, FeederReport>>> _FeederReportQueue;
        public Optional<Dictionary<FluxJob, FeederReport>> FeederReportQueue => _FeederReportQueue.Value;

        public MaterialEvaluator Material { get; }
        public ToolNozzleEvaluator ToolNozzle { get; }

        private ObservableAsPropertyHelper<Optional<IFluxOffsetViewModel>> _Offset;
        public Optional<IFluxOffsetViewModel> Offset => _Offset.Value;

        private ObservableAsPropertyHelper<bool> _IsInvalidProbe;
        public bool IsInvalidProbe => _IsInvalidProbe.Value;

        private ObservableAsPropertyHelper<bool> _HasColdNozzle;
        public bool HasColdNozzle => _HasColdNozzle.Value;

        private ObservableAsPropertyHelper<Optional<Dictionary<FluxJob, Optional<Extrusion>>>> _ExtrusionQueue;
        public Optional<Dictionary<FluxJob, Optional<Extrusion>>> ExtrusionQueue => _ExtrusionQueue.Value;

        public FeederEvaluator(StatusProvider status, IFluxFeederViewModel feeder)
        {
            Status = status;
            Feeder = feeder;
            Material = new MaterialEvaluator(this);
            ToolNozzle = new ToolNozzleEvaluator(this);
        }

        public void Initialize()
        {
            var queue_pos = Status.Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE_POS)
                .StartWithDefault()
                .DistinctUntilChanged();

            bool compare_queue(Optional<Dictionary<QueuePosition, FluxJob>> q1, Optional<Dictionary<QueuePosition, FluxJob>> q2)
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
                            if (!qi.Value.Equals(guid))
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

            var queue = Status.Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE)
                .StartWithDefault()
                .DistinctUntilChanged(compare_queue);

            var mcode_analyzers = Status.Flux.MCodes.AvaiableMCodes.Connect()
                 .AutoRefresh(m => m.Analyzer)
                 .QueryWhenChanged(m => m.KeyValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Analyzer))
                 .StartWith(new Dictionary<Guid, Optional<MCodeAnalyzer>>())
                 .DistinctUntilChanged();

            _FeederReportQueue = Observable.CombineLatest(
                queue_pos,
                queue,
                mcode_analyzers,
                GetFeederReportQueue)
                .ToProperty(this, e => e.FeederReportQueue);

            _ExtrusionQueue = Status.WhenAnyValue(s => s.ExtrusionSetQueue)
                .Convert(e => e.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Lookup(Feeder.Position)))
                .ToProperty(this, v => v.ExtrusionQueue);

            _Offset = Status.Flux.Calibration.Offsets.Connect()
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

            var tool_material = Feeder.WhenAnyValue(f => f.SelectedToolMaterial);

            _HasColdNozzle = Observable.CombineLatest(
                Status.WhenAnyValue(s => s.PrintingEvaluation),
                Feeder.ToolNozzle.WhenAnyValue(t => t.NozzleTemperature),
                tool_material.ConvertMany(tm => tm.WhenAnyValue(t => t.ExtrusionTemp)),
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
            if (plc_temp.Value.Target == 0)
                return true;
            return plc_temp.Value.Target - plc_temp.Value.Current > 10;
        }

        private Optional<IFluxOffsetViewModel> FindOffset(IQuery<IFluxOffsetViewModel, ushort> offsets)
        {
            return offsets.LookupOptional(Feeder.Position);
        }

        private bool InvalidProbe(Optional<bool> is_probed, Optional<Dictionary<FluxJob, FeederReport>> report_queue)
        {
            if (!report_queue.HasValue)
                return true;

            if (report_queue.Value.Count == 0)
                return false;

            if (!is_probed.HasValue)
                return true;

            return !is_probed.Value;
        }

        private Optional<Dictionary<FluxJob, FeederReport>> GetFeederReportQueue(Optional<QueuePosition> queue_pos, Optional<Dictionary<QueuePosition, FluxJob>> job_queue, Dictionary<Guid, Optional<MCodeAnalyzer>> mcode_analyzers)
        {
            try
            {
                if (!queue_pos.HasValue)
                    return default;
                if (!job_queue.HasValue)
                    return default;

                var feeder_report_queue = new Dictionary<FluxJob, FeederReport>();
                foreach (var job in job_queue.Value.Values)
                {
                    if (job.QueuePosition < queue_pos.Value)
                        continue;

                    var mcode_analyzer = mcode_analyzers.LookupOptional(job.MCodeGuid);
                    if (!mcode_analyzer.HasValue)
                        continue;

                    var feeder_report = mcode_analyzer.Value.MCode.FeederReports.Lookup(Feeder.Position);
                    if (!feeder_report.HasValue)
                        continue;

                    feeder_report_queue.Add(job, feeder_report.Value);
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
