using DynamicData;
using DynamicData.Kernel;
using Modulo3DDatabase;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Aggregation;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class MCodeStorageViewModel : RemoteControl<MCodeStorageViewModel>, IFluxMCodeStorageViewModel
    {
        public MCodeAnalyzer Analyzer { get; }

        private ObservableAsPropertyHelper<int> _FileNumber;
        [RemoteOutput(true)]
        public int FileNumber => _FileNumber.Value;

        public MCodesViewModel MCodes { get; }
        public FluxViewModel Flux => MCodes.Flux;

        private double _LoadPercentage;
        [RemoteOutput(true)]
        public double LoadPercentage
        {
            get => _LoadPercentage;
            set => this.RaiseAndSetIfChanged(ref _LoadPercentage, value);
        }

        private bool _ShowInfo;
        public bool ShowInfo
        {
            get => _ShowInfo;
            set => this.RaiseAndSetIfChanged(ref _ShowInfo, value);
        }

        public IObservableCache<Nozzle, ushort> Nozzles { get; }
        public IObservableCache<Material, ushort> Materials { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> SelectCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ToggleInfoCommand { get; }

        private ObservableAsPropertyHelper<bool> _CanSelect;
        public bool CanSelect => _CanSelect.Value;

        private ObservableAsPropertyHelper<bool> _CanDelete;
        public bool CanDelete => _CanDelete.Value;

        public MCodeStorageViewModel(MCodesViewModel mcodes, MCodeAnalyzer analyzer) : base($"mcodeStorage??{analyzer.MCode.MCodeGuid}")
        {
            MCodes = mcodes;
            Analyzer = analyzer;

            Materials = Flux.DatabaseProvider.WhenAnyValue(v => v.Database)
                .Select(db => FindDocuments<Material>(db, r => r.MaterialId))
                .ToObservableChangeSet(t => t.position)
                .Transform(t => t.document)
                .AsObservableCache()
                .DisposeWith(Disposables);

            Nozzles = Flux.DatabaseProvider.WhenAnyValue(v => v.Database)
                .Select(db => FindDocuments<Nozzle>(db, r => r.NozzleId))
                .ToObservableChangeSet(t => t.position)
                .Transform(t => t.document)
                .AsObservableCache()
                .DisposeWith(Disposables);

            _FileNumber = mcodes.AvaiableMCodes
                .Connect()
                .QueryWhenChanged()
                .Select(FindFileNumber)
                .ToProperty(this, f => f.FileNumber)
                .DisposeWith(Disposables);

            var is_selecting_file = Flux.MCodes
                .WhenAnyValue(f => f.IsPreparingFile)
                .StartWith(false);

            var can_modify = Observable.CombineLatest(
                is_selecting_file,
                Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS),
                Flux.StatusProvider.WhenAnyValue(e => e.PrintingEvaluation),
                CanModifyMCode);

            _CanDelete = Observable.CombineLatest(
                can_modify,
                Flux.ConnectionProvider.ObserveVariable(c => c.QUEUE),
                (m, queue) => m && queue.ConvertOr(q => !q.Values.Contains(analyzer.MCode.MCodeGuid), () => false))
                .ToProperty(this, v => v.CanDelete)
                .DisposeWith(Disposables);

            _CanSelect = can_modify
                .ToProperty(this, v => v.CanSelect)
                .DisposeWith(Disposables);

            ToggleInfoCommand = ReactiveCommand.Create(() => { ShowInfo = !ShowInfo; })
                .DisposeWith(Disposables);

            DeleteCommand = ReactiveCommand.CreateFromTask(
                async () => { await mcodes.DeleteFileAsync(false, this); }, 
                this.WhenAnyValue(v => v.CanDelete))
                .DisposeWith(Disposables);

            SelectCommand = ReactiveCommand.CreateFromTask(
                async () => { await mcodes.AddToQueueAsync(this); },
                this.WhenAnyValue(v => v.CanSelect))
                .DisposeWith(Disposables);

            var name = analyzer.MCode.Name;
            AddOutput("name", name);

            var infoToggled = this.WhenAnyValue(v => v.ShowInfo);
            AddOutput("infoToggled", infoToggled);

            var duration = analyzer.MCode.Duration;
            AddOutput("duration", duration, typeof(TimeSpanConverter));

            var quantities = analyzer.MCode.Feeders.Select(f => f.Value.WeightG);
            AddOutput("quantities", quantities, typeof(EnumerableConverter<WeightConverter, double>));

            var quality = analyzer.MCode.PrintQuality;
            AddOutput("quality", quality);

            var created = analyzer.MCode.Created;
            AddOutput("created", created, typeof(DateTimeConverter<DateTimeFormat>));

            var materials = Flux.DatabaseProvider.WhenAnyValue(v => v.Database)
                .Select(db => FindDocuments<Material>(db, r => r.MaterialId).Select(d => d.document.Name));
            AddOutput("materials", materials);

            var nozzles = Flux.DatabaseProvider.WhenAnyValue(v => v.Database)
                .Select(db => FindDocuments<Nozzle>(db, r => r.NozzleId).Select(d => d.document.Name));
            AddOutput("nozzles", nozzles);
        }

        private bool CanModifyMCode(bool selecting, Optional<FLUX_ProcessStatus> status, PrintingEvaluation printing_eval)
        {
            if (selecting)
                return false;
            if (!printing_eval.SelectedMCode.HasValue && 
                !printing_eval.SelectedRecovery.HasValue)
                return true;
            if (!status.HasValue)
                return false;
            if (status.Value != FLUX_ProcessStatus.IDLE)
                return false;
            return true;
        }
        private int FindFileNumber(IQuery<IFluxMCodeStorageViewModel, Guid> mcodes_query)
        {
            var mcodes = mcodes_query.Items
                .OrderByDescending(m => m.Analyzer.MCode.Created)
                .Select((mcode, index) => (mcode.Analyzer.MCode, index))
                .ToDictionary(t => t.MCode.MCodeGuid, t => t.index);

            if (mcodes.TryGetValue(Analyzer.MCode.MCodeGuid, out var index))
                return index;

            return 0;
        }
        private IEnumerable<(TDocument document, ushort position)> FindDocuments<TDocument>(Optional<ILocalDatabase> database, Func<FeederReport, int> get_id)
            where TDocument : IDocument
        {
            if (!database.HasValue)
                yield break;
            foreach (var feeder in Analyzer.MCode.Feeders)
            {
                var result = database.Value.FindById<TDocument>(get_id(feeder.Value));
                if (result.HasDocuments)
                    yield return (result.Documents.FirstOrDefault(), feeder.Key);
            }
        }
    }
}
