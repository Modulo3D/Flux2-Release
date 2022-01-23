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
        public Optional<IDocument> ExpectedDocument { get; }
        public Optional<IDocument> CurrentDocument { get; }
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

        Optional<IDocument> ITagViewModelEvaluator.ExpectedDocument => ExpectedDocument.Cast<TDocument, IDocument>();
        private ObservableAsPropertyHelper<Optional<TDocument>> _ExpectedDocument;
        public Optional<TDocument> ExpectedDocument => _ExpectedDocument.Value;

        Optional<IDocument> ITagViewModelEvaluator.CurrentDocument => CurrentDocument.Cast<TDocument, IDocument>();
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
            var db_changed = FeederEvaluator.Feeder.Flux.DatabaseProvider.WhenAnyValue(v => v.Database);
            var eval_changed = FeederEvaluator.WhenAnyValue(e => e.FeederReport);

            _ExpectedDocument = Observable.CombineLatest(db_changed, eval_changed,
                (db, f) => GetExpectedDocument(db, f, GetDocumentId))
                .ToProperty(this, v => v.ExpectedDocument);

            _CurrentDocument = TagViewModel.WhenAnyValue(t => t.Document)
                .Select(GetCurrentDocument)
                .ToProperty(this, v => v.CurrentDocument);

            _IsValid = Observable.CombineLatest(
                TagViewModel.WhenAnyValue(f => f.Document),
                TagViewModel.WhenAnyValue(f => f.State),
                eval_changed,
                GetValid)
                .ToProperty(this, e => e.IsValid);

            _ExpectedWeight = FeederEvaluator.WhenAnyValue(f => f.Extrusion)
                .Convert(e => (double)e.WeightG)
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
        public abstract bool GetValid(TTagDocument document, TState state, Optional<FeederReport> report);

        private Optional<TDocument> GetExpectedDocument(Optional<ILocalDatabase> database, Optional<FeederReport> feeder, Func<FeederReport, int> get_id)    
        {
            if (!database.HasValue)
                return default;
            if (!feeder.HasValue)
                return default;
            var result = database.Value.FindById<TDocument>(get_id(feeder.Value));
            if (!result.HasDocuments)
                return default;
            return result.Documents.FirstOrDefault();
        }
        private bool GetEnoughWeight(Optional<double> expected, Optional<double> current, Optional<FeederReport> report)
        {
            if (!report.HasValue)
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
        public override bool GetValid(Optional<Material> document, MaterialState state, Optional<FeederReport> report)
        {
            if (!report.HasValue)
                return true;

            if (!document.HasValue)
                return false;

            if (!state.IsLoaded())
                return false;

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
        public override bool GetValid((Optional<Tool> tool, Optional<Nozzle> nozzle) document, ToolNozzleState state, Optional<FeederReport> report)
        {
            if (!report.HasValue)
                return true;

            if (!document.tool.HasValue)
                return false;

            if (!document.nozzle.HasValue)
                return false;

            if (!state.IsLoaded())
                return false;

            if (report.Value.NozzleId != document.nozzle.Value.Id)
                return false;

            return true;
        }
    }

    public class FeederEvaluator : ReactiveObject
    {
        public StatusProvider Status { get; }
        public IFluxFeederViewModel Feeder { get; }

        private ObservableAsPropertyHelper<Optional<FeederReport>> _FeederReport;
        public Optional<FeederReport> FeederReport => _FeederReport.Value;

        public MaterialEvaluator Material { get; }
        public ToolNozzleEvaluator ToolNozzle { get; }

        private ObservableAsPropertyHelper<Optional<IFluxOffsetViewModel>> _Offset;
        public Optional<IFluxOffsetViewModel> Offset => _Offset.Value;

        private ObservableAsPropertyHelper<bool> _IsValidProbe;
        public bool IsValidProbe => _IsValidProbe.Value;

        private ObservableAsPropertyHelper<Optional<bool>> _HasHotNozzle;
        public Optional<bool> HasHotNozzle => _HasHotNozzle.Value;

        private ObservableAsPropertyHelper<Optional<Extrusion>> _Extrusion;
        public Optional<Extrusion> Extrusion => _Extrusion.Value;

        public FeederEvaluator(StatusProvider status, IFluxFeederViewModel feeder)
        {
            Status = status;
            Feeder = feeder;
            Material = new MaterialEvaluator(this);
            ToolNozzle = new ToolNozzleEvaluator(this);
        }

        public void Initialize()
        {
            var selected_mcode = Status.WhenAnyValue(s => s.PrintingEvaluation)
                .Select(e => e.SelectedMCode);

            _FeederReport = selected_mcode
                .Select(GetFeederReport)
                .ToProperty(this, e => e.FeederReport);

            _Extrusion = Status.WhenAnyValue(s => s.PrintingEvaluation)
                .Select(e => e.ExtrusionSet)
                .Convert(e => e.Extrusions.Lookup(Feeder.Position))
                .ToProperty(this, v => v.Extrusion);

            _Offset = Feeder.Flux.Calibration.Offsets.Connect()
                .QueryWhenChanged(FindOffset)
                .ToProperty(this, v => v.Offset);

            var eval_changed = this.WhenAnyValue(e => e.FeederReport);

            var valid_probe = this.WhenAnyValue(s => s.Offset)
                .ConvertMany(o => o.WhenAnyValue(o => o.ProbeState).Select(state => state == FluxProbeState.VALID_PROBE));

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

        private bool ValidProbe(Optional<bool> is_probed, Optional<FeederReport> report)
        {
            if (!report.HasValue)
                return true;

            if (!is_probed.HasValue)
                return false;

            return is_probed.Value;
        }

        private Optional<FeederReport> GetFeederReport(Optional<MCode> mcode)
        {
            if (!mcode.HasValue)
                return default;
            if (!mcode.Value.Feeders.TryGetValue(Feeder.Position, out var feeder))
                return default;
            return feeder;
        }
    }
}
