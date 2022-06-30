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

namespace Flux.ViewModels
{
    public class MCodeStorageViewModel : RemoteControl<MCodeStorageViewModel>, IFluxMCodeStorageViewModel
    {
        private ObservableAsPropertyHelper<Optional<MCodeAnalyzer>> _Analyzer;
        public Optional<MCodeAnalyzer> Analyzer => _Analyzer.Value;

        public Guid MCodeGuid { get; }

        private ObservableAsPropertyHelper<ushort> _FileNumber;
        [RemoteOutput(true)]
        public ushort FileNumber => _FileNumber.Value;

        public MCodesViewModel MCodes { get; }
        public FluxViewModel Flux => MCodes.Flux;

        private double _UploadPercentage;
        [RemoteOutput(true)]
        public double UploadPercentage
        {
            get => _UploadPercentage;
            set => this.RaiseAndSetIfChanged(ref _UploadPercentage, value);
        }

        private ObservableAsPropertyHelper<bool> _IsUploading;
        public bool IsUploading => _IsUploading.Value;

        private bool _ShowInfo;
        public bool ShowInfo
        {
            get => _ShowInfo;
            set => this.RaiseAndSetIfChanged(ref _ShowInfo, value);
        }

        public IObservableCache<Nozzle, ushort> Nozzles { get; }
        public IObservableCache<Material, ushort> Materials { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> DeleteMCodeStorageCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> SelectMCodeStorageCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> CancelSelectMCodeStorageCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ToggleMCodeStorageInfoCommand { get; }

        private ObservableAsPropertyHelper<bool> _CanSelect;
        public bool CanSelect => _CanSelect.Value;

        private ObservableAsPropertyHelper<bool> _CanDelete;
        public bool CanDelete => _CanDelete.Value;

        public MCodeStorageViewModel(MCodesViewModel mcodes, Guid mcode_guid) : base($"{typeof(MCodeStorageViewModel).GetRemoteControlName()}??{mcode_guid}")
        {
            MCodes = mcodes;
            MCodeGuid = mcode_guid;
            
            _Analyzer = mcodes.Flux.DatabaseProvider
                .WhenAnyValue(v => v.Database)
                .Convert(db => MCodeAnalyzer.CreateFromZip(db, mcode_guid, Directories.MCodes))
                .ToProperty(this, v => v.Analyzer);

            _IsUploading = this.WhenAnyValue(v => v.UploadPercentage)
                .Select(p => p > 0)
                .ToProperty(this, v => v.IsUploading);

            Materials = Observable.CombineLatest(
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                this.WhenAnyValue(v => v.Analyzer),
                (db, a) => FindDocuments<Material>(db, a, r => r.MaterialId))
                .ToObservableChangeSet(t => t.position)
                .Transform(t => t.document)
                .AsObservableCache()
                .DisposeWith(Disposables);

            Nozzles = Observable.CombineLatest(
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                this.WhenAnyValue(v => v.Analyzer),
                (db, a) => FindDocuments<Nozzle>(db, a, r => r.NozzleId))
                .ToObservableChangeSet(t => t.position)
                .Transform(t => t.document)
                .AsObservableCache()
                .DisposeWith(Disposables);

            var mcode_analyzer = mcodes.AvaiableMCodes.Connect()
                .AutoRefresh(m => m.Analyzer)
                .QueryWhenChanged(f => f.KeyValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Analyzer))
                .StartWith(new Dictionary<Guid, Optional<MCodeAnalyzer>>());

            _FileNumber = Observable.CombineLatest(
                this.WhenAnyValue(v => v.Analyzer),
                mcode_analyzer,
                FindFileNumber)
                .ToProperty(this, f => f.FileNumber)
                .DisposeWith(Disposables);

            var is_selecting_file = Flux.MCodes
                .WhenAnyValue(f => f.IsPreparingFile)
                .StartWith(false);

            var in_queue = mcodes.QueuedMCodes.Connect()
                .AutoRefresh(q => q.Storage)
                .Transform(q => q.Storage, true)
                .QueryWhenChanged(c => c.Items.Contains(this))
                .StartWith(false);

            _CanDelete = Observable.CombineLatest(
                is_selecting_file,
                in_queue,
                (s, q) => !s && !q)
                .ToProperty(this, v => v.CanDelete)
                .DisposeWith(Disposables);

            _CanSelect = Observable.CombineLatest(
                is_selecting_file,
                Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS),
                Flux.StatusProvider.WhenAnyValue(e => e.PrintingEvaluation),
                CanSelectMCode)
                .ToProperty(this, v => v.CanSelect)
                .DisposeWith(Disposables);

            ToggleMCodeStorageInfoCommand = ReactiveCommand.Create(() => { ShowInfo = !ShowInfo; })
                .DisposeWith(Disposables);

            DeleteMCodeStorageCommand = ReactiveCommand.CreateFromTask(
                async () => { await mcodes.DeleteFileAsync(false, this); },
                this.WhenAnyValue(v => v.CanDelete))
                .DisposeWith(Disposables);

            SelectMCodeStorageCommand = ReactiveCommand.CreateFromTask(
                async () => { await mcodes.AddToQueueAsync(this); },
                this.WhenAnyValue(v => v.CanSelect))
                .DisposeWith(Disposables);

            CancelSelectMCodeStorageCommand = ReactiveCommand.Create(
                () => mcodes.CancelPrepareMCode(),
                this.WhenAnyValue(v => v.IsUploading))
                .DisposeWith(Disposables);

            AddOutput("infoToggled", this.WhenAnyValue(v => v.ShowInfo));
            AddOutput("name", this.WhenAnyValue(v => v.Analyzer).Convert(a => a.MCode.Name));
            AddOutput("nozzles", Nozzles.Connect().QueryWhenChanged(f => f.Items.Select(i => i.Name)));
            AddOutput("quality", this.WhenAnyValue(v => v.Analyzer).Convert(a => a.MCode.PrintQuality));
            AddOutput("materials", Materials.Connect().QueryWhenChanged(f => f.Items.Select(i => i.Name)));
            AddOutput("duration", this.WhenAnyValue(v => v.Analyzer).Convert(a => a.MCode.Duration), typeof(TimeSpanConverter));
            AddOutput("created", this.WhenAnyValue(v => v.Analyzer).Convert(a => a.MCode.Created), typeof(DateTimeConverter<DateTimeFormat>));
            AddOutput("quantities", this.WhenAnyValue(v => v.Analyzer).Convert(a => a.Extrusions.Select(e => e.Value.WeightG)), typeof(EnumerableConverter<WeightConverter, double>));
        }

        private bool CanSelectMCode(bool selecting, Optional<FLUX_ProcessStatus> status, PrintingEvaluation printing_eval)
        {
            if (selecting)
                return false;
            if (!printing_eval.SelectedMCode.HasValue && !printing_eval.Recovery.HasValue)
                return true;
            if (!status.ConvertOr(s => s == FLUX_ProcessStatus.IDLE, () => false))
                return false;
            return true;
        }
        private ushort FindFileNumber(Optional<MCodeAnalyzer> analyzer, Dictionary<Guid, Optional<MCodeAnalyzer>> mcode_analyzers)
        {
            if (!analyzer.HasValue)
                return (ushort)0;

            var mcodes = mcode_analyzers.Values
                .Where(a => a.HasValue)
                .Select(a => a.Value)
                .OrderByDescending(a => a.MCode.Created)
                .Select((a, index) => (a.MCode.MCodeGuid, index))
                .ToDictionary(t => t.MCodeGuid, t => t.index);

            if (!mcodes.TryGetValue(analyzer.Value.MCode.MCodeGuid, out var index))
                return 0;

            return (ushort)index;
        }
        private IEnumerable<(TDocument document, ushort position)> FindDocuments<TDocument>(Optional<ILocalDatabase> database, Optional<MCodeAnalyzer> analyzer, Func<FeederReport, int> get_id)
            where TDocument : IDocument
        {
            if (!database.HasValue)
                yield break;
            if (!analyzer.HasValue)
                yield break;
            foreach (var feeder in analyzer.Value.MCode.FeederReports)
            {
                var result = database.Value.FindById<TDocument>(get_id(feeder.Value));
                if (result.HasDocuments)
                    yield return (result.Documents.FirstOrDefault(), feeder.Key);
            }
        }
    }
}
