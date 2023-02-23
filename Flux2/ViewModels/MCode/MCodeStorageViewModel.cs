﻿using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class MCodeStorageViewModel : RemoteControl<MCodeStorageViewModel>, IFluxMCodeStorageViewModel
    {
        public MCodeAnalyzer Analyzer { get; }

        public MCodeKey MCodeKey { get; }

        private readonly ObservableAsPropertyHelper<ushort> _FileNumber;
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

        private readonly ObservableAsPropertyHelper<bool> _IsUploading;
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

        private readonly ObservableAsPropertyHelper<bool> _CanSelect;
        public bool CanSelect => _CanSelect.Value;

        private readonly ObservableAsPropertyHelper<bool> _CanDelete;
        public bool CanDelete => _CanDelete.Value;

        public MCodeStorageViewModel(MCodesViewModel mcodes, MCodeKey mcode_key, MCodeAnalyzer analyzer) 
            : base($"{typeof(MCodeStorageViewModel).GetRemoteElementClass()};{mcode_key}")
        {
            MCodes = mcodes;
            Analyzer = analyzer;
            MCodeKey = mcode_key;

            _IsUploading = this.WhenAnyValue(v => v.UploadPercentage)
                .Select(p => p > 0)
                .ToPropertyRC(this, v => v.IsUploading);

            Materials = Observable.CombineLatest(
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                this.WhenAnyValue(v => v.Analyzer),
                (db, a) => FindDocumentsAsync<Material>(db, a, r => r.MaterialId))
                .Select(o => o.ToObservable()).Switch()
                .ToObservableChangeSet(t => t.position)
                .Transform(t => t.document)
                .AsObservableCacheRC(this);

            Nozzles = Observable.CombineLatest(
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                this.WhenAnyValue(v => v.Analyzer),
                (db, a) => FindDocumentsAsync<Nozzle>(db, a, r => r.NozzleId))
                .Select(o => o.ToObservable()).Switch()
                .ToObservableChangeSet(t => t.position)
                .Transform(t => t.document)
                .AsObservableCacheRC(this);

            _FileNumber = mcodes.AvaiableMCodes.Connect()
                .QueryWhenChanged(FindFileNumber)
                .ToPropertyRC(this, f => f.FileNumber);

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
                .ToPropertyRC(this, v => v.CanDelete);

            var is_idle = MCodes.Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(e => e.IsIdle);

            _CanSelect = Observable.CombineLatest(
                is_idle,
                is_selecting_file,
                Flux.ConnectionProvider.ObserveVariable(m => m.PROCESS_STATUS),
                Flux.StatusProvider.WhenAnyValue(e => e.PrintingEvaluation),
                CanSelectMCode)
                .ToPropertyRC(this, v => v.CanSelect);

            ToggleMCodeStorageInfoCommand = ReactiveCommandRC.Create(() => { ShowInfo = !ShowInfo; }, this);

            DeleteMCodeStorageCommand = ReactiveCommandRC.CreateFromTask(
                async () => { await mcodes.DeleteAsync(false, this); }, this,
                this.WhenAnyValue(v => v.CanDelete));

            SelectMCodeStorageCommand = ReactiveCommandRC.CreateFromTask(
                async () => { await mcodes.AddToQueueAsync(this); }, this,
                this.WhenAnyValue(v => v.CanSelect));

            CancelSelectMCodeStorageCommand = ReactiveCommandRC.Create(
                () => mcodes.CancelPrepareMCode(), this,
                this.WhenAnyValue(v => v.IsUploading));

            var quanitites = Flux.DatabaseProvider
                .WhenAnyValue(d => d.Database)
                .Select(db =>
                {
                    return Analyzer.MCode.FeederReports
                        .Select(f => get_material(f.Value))
                        .Select(m => m.Result)
                        .Where(m => m.material.HasValue)
                        .Select(m => ExtrusionG.CreateTotalExtrusion(default, m.feeder, m.material.Value))
                        .Select(e => e.WeightG);

                    async Task<(FeederReport feeder, Optional<Material> material)> get_material(FeederReport feeder)
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        var result = await db.Value.FindByIdAsync<Material>(feeder.MaterialId, cts.Token);
                        return (feeder, result.FirstOrOptional(m => m != null));
                    }
                });

            AddOutput("name", Analyzer.MCode.Name);
            AddOutput("quality", Analyzer.MCode.PrintQuality);
            AddOutput("quantities", quanitites, typeof(EnumerableConverter<WeightConverter, double>));
            AddOutput("infoToggled", this.WhenAnyValue(v => v.ShowInfo));
            AddOutput("pos", Nozzles.Connect().QueryWhenChanged(f => f.Keys));
            AddOutput("nozzles", Nozzles.Connect().QueryWhenChanged(f => f.Items.Select(i => i.Name)));
            AddOutput("materials", Materials.Connect().QueryWhenChanged(f => f.Items.Select(i => i.Name)));
            AddOutput("duration", Analyzer.MCode.Duration, typeof(TimeSpanConverter));
            AddOutput("created", Analyzer.MCode.Created, typeof(DateTimeConverter<RelativeDateTimeFormat>));
        }

        private bool CanSelectMCode(bool is_idle, bool selecting, Optional<FLUX_ProcessStatus> status, PrintingEvaluation printing_eval)
        {
            var connection_provider = Flux.ConnectionProvider;
            /*if (!is_idle)
                return false;*/
            if (selecting)
                return false;
            if (connection_provider.VariableStoreBase.HasPrintUnloader)
                return true;
            if (!printing_eval.MCode.HasValue)
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
        private static async IAsyncEnumerable<(TDocument document, ushort position)> FindDocumentsAsync<TDocument>(Optional<ILocalDatabase> database, Optional<MCodeAnalyzer> analyzer, Func<FeederReport, int> get_id)
            where TDocument : IDocument, new()
        {
            if (!database.HasValue)
                yield break;
            if (!analyzer.HasValue)
                yield break;
            foreach (var feeder in analyzer.Value.MCode.FeederReports)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var result = await database.Value.FindByIdAsync<TDocument>(get_id(feeder.Value), cts.Token);
                if (result.HasDocuments)
                    yield return (result.FirstOrDefault(), feeder.Key);
            }
        }
    }
}
