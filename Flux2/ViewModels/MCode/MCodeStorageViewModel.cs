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
        public MCodeAnalyzer Analyzer { get; }

        public MCodeKey MCodeKey { get; }

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

        public MCodeStorageViewModel(MCodesViewModel mcodes, MCodeKey mcode_key, MCodeAnalyzer analyzer) : base($"{typeof(MCodeStorageViewModel).GetRemoteControlName()}??{mcode_key}")
        {
            MCodes = mcodes;
            Analyzer = analyzer;
            MCodeKey = mcode_key;

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

            _FileNumber = mcodes.AvaiableMCodes.Connect()
                .QueryWhenChanged(FindFileNumber)
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

            var queue_size = Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE_SIZE)
                .ValueOr(() => (ushort)0);

            _CanSelect = Observable.CombineLatest(
                is_selecting_file,
                queue_size,
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

            AddOutput("name", Analyzer.MCode.Name);
            AddOutput("quality", Analyzer.MCode.PrintQuality);
            // TODO
            //AddOutput("quantities", Analyzer.Extrusions.Select(e => e.Value.WeightG)), typeof(EnumerableConverter<WeightConverter, double>));

            AddOutput("infoToggled", this.WhenAnyValue(v => v.ShowInfo));
            AddOutput("nozzles", Nozzles.Connect().QueryWhenChanged(f => f.Items.Select(i => i.Name)));
            AddOutput("materials", Materials.Connect().QueryWhenChanged(f => f.Items.Select(i => i.Name)));
            AddOutput("duration", Analyzer.MCode.Duration, typeof(TimeSpanConverter));
            AddOutput("created", Analyzer.MCode.Created, typeof(DateTimeConverter<RelativeDateTimeFormat>));
        }

        private bool CanSelectMCode(bool selecting, ushort queue_size, Optional<FLUX_ProcessStatus> status, PrintingEvaluation printing_eval)
        {
            if (selecting)
                return false;
            if (queue_size > 1)
                return true;
            if (!printing_eval.CurrentMCode.HasValue && !printing_eval.CurrentRecovery.HasValue)
                return true;
            if (!status.ConvertOr(s => s == FLUX_ProcessStatus.IDLE, () => false))
                return false;
            return true;
        }
        private ushort FindFileNumber(IQuery<IFluxMCodeStorageViewModel, MCodeKey> mcode_analyzers)
        {
            var mcodes = mcode_analyzers.Items
                .OrderByDescending(a => a.Analyzer.MCode.Created)
                .Select((a, index) => (a.Analyzer.MCode.MCodeKey, index))
                .ToDictionary(t => t.MCodeKey, t => t.index);

            if (!mcodes.TryGetValue(Analyzer.MCode.MCodeKey, out var index))
                return 0;

            return (ushort)index;
        }
        private IEnumerable<(TDocument document, ushort position)> FindDocuments<TDocument>(Optional<ILocalDatabase> database, Optional<MCodeAnalyzer> analyzer, Func<FeederReport, int> get_id)
            where TDocument : IDocument, new()
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
