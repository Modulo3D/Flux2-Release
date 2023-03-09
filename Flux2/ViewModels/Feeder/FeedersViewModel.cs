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
    public class FeedersViewModel : FluxRoutableNavBarViewModel<FeedersViewModel>, IFluxFeedersViewModel
    {
        [RemoteContent(true, comparer:(nameof(IFluxFeederViewModel.Position)))]
        public IObservableCache<IFluxFeederViewModel, ushort> Feeders { get; }
        public IObservableCache<IFluxMaterialViewModel, ushort> ToolMaterials { get; }
        public IObservableCache<IFluxToolNozzleViewModel, ushort> ToolNozzles { get; }

        public ObservableAsPropertyHelper<short> _SelectedExtruder;
        public short SelectedExtruder => _SelectedExtruder.Value;

        public ObservableAsPropertyHelper<Optional<IFluxFeederViewModel>> _SelectedFeeder;
        public Optional<IFluxFeederViewModel> SelectedFeeder => _SelectedFeeder.Value;

        public ObservableAsPropertyHelper<bool> _HasInvalidStates;
        public bool HasInvalidStates => _HasInvalidStates.Value;

        public IObservableCache<Optional<Material>, ushort> Materials { get; }
        public IObservableCache<Optional<Nozzle>, ushort> Nozzles { get; }
        public IObservableCache<Optional<Tool>, ushort> Tools { get; }

        public OdometerManager OdometerManager { get; }
        IOdometerManager IFluxFeedersViewModel.OdometerManager => OdometerManager;

        public NFCStorage<FluxUserSettings, NFCMaterial> NFCMaterials { get; }
        public NFCStorage<FluxUserSettings, NFCToolNozzle> NFCToolNozzles { get; }

        public FeedersViewModel(FluxViewModel flux) : base(flux)
        {
            OdometerManager = new OdometerManager(flux);

            Feeders = Flux.SettingsProvider
                .WhenAnyValue(v => v.ExtrudersCount)
                .Select(CreateFeeders)
                .AsObservableChangeSet(f => f.Position)
                .DisposeMany()
                .AsObservableCacheRC(this);

            ToolNozzles = Feeders.Connect()
                .TransformMany(CreateToolNozzles, tn => tn.Position)
                .DisposeMany()
                .AsObservableCacheRC(this);

            ToolMaterials = Feeders.Connect()
                .TransformMany(CreateToolMaterials, tn => tn.Position)
                .DisposeMany()
                .AsObservableCacheRC(this);

            var user_settings = Flux.SettingsProvider.CoreSettings;
            var lock_guid = user_settings.Local.PrinterGuid;

            NFCMaterials = new NFCStorage<FluxUserSettings, NFCMaterial>(
                this, Flux.SettingsProvider.UserSettings, lock_guid,
                Directories.NFCBackup, s => s.Materials);

            NFCToolNozzles = new NFCStorage<FluxUserSettings, NFCToolNozzle>(
                this, Flux.SettingsProvider.UserSettings, lock_guid,
                Directories.NFCBackup, s => s.ToolNozzles);

            _HasInvalidStates = Feeders.Connect()
                .TrueForAny(f => f.HasInvalidStateChanged, i => i)
                .ToPropertyRC(this, f => f.HasInvalidStates);

            // TODO
            Materials = Feeders.Connect()
                .AutoRefresh(f => f.SelectedMaterial)
                .Transform(f => f.SelectedMaterial, true)
                .AutoRefresh(m => m.Document)
                .Transform(f => f.Convert(f => f.Document), true)
                .AsObservableCacheRC(this);

            Tools = Feeders.Connect()
                .AutoRefresh(f => f.ToolNozzle.Document)
                .Transform(f => f.ToolNozzle.Document, true)
                .Transform(tn => tn.tool)
                .AsObservableCacheRC(this);

            Nozzles = Feeders.Connect()
              .AutoRefresh(f => f.ToolNozzle.Document)
              .Transform(f => f.ToolNozzle.Document, true)
              .Transform(tn => tn.nozzle)
              .AsObservableCacheRC(this);

            var selected_extruder = Flux.ConnectionProvider
                .ObserveVariable(m => m.TOOL_ON_TRAILER)
                .Convert(c => c.QueryWhenChanged(q => (short)q.Items.IndexOf(true)));

            _SelectedExtruder = selected_extruder
                .ObservableOr(() => (short)-1)
                .ToPropertyRC(this, v => v.SelectedExtruder);

            _SelectedFeeder = selected_extruder
                .ConvertToObservable(FindSelectedFeeder)
                .ObservableOrDefault()
                .ToPropertyRC(this, v => v.SelectedFeeder);
        }

        private IObservable<Optional<IFluxFeederViewModel>> FindSelectedFeeder(short selected_extruder)
        {
            return Feeders.Connect().WatchOptional((ushort)selected_extruder);
        }

        private IEnumerable<IFluxFeederViewModel> CreateFeeders(Optional<(ushort machine_extruders, ushort mixing_extruders)> extruders)
        {
            if (!extruders.HasValue)
                yield break;
            for (ushort position = 0; position < extruders.Value.machine_extruders; position++)
                yield return new FeederViewModel(this, position, extruders.Value.mixing_extruders);
        }
        private IEnumerable<IFluxToolNozzleViewModel> CreateToolNozzles(IFluxFeederViewModel feeder)
        {
            yield return new ToolNozzleViewModel(this, (FeederViewModel)feeder);
        }
        private IEnumerable<IFluxMaterialViewModel> CreateToolMaterials(IFluxFeederViewModel feeder)
        {
            for (ushort position = 0; position < feeder.MixingCount; position++)
            {
                var material = new MaterialViewModel(this, (FeederViewModel)feeder, (ushort)(position + (feeder.Position * feeder.MixingCount)));
                material.Initialize();
                yield return material;
            }
        }

        async Task<NFCReading<NFCMaterial>> INFCStorageProvider<NFCMaterial>.CreateTagAsync(ushort position, Optional<NFCMaterial> last_tag, CardId virtual_card_id)
        {
            var database = Flux.DatabaseProvider.Database;
            if (!database.HasValue)
                return default;

            var printer = Flux.SettingsProvider.Printer;
            if (!printer.HasValue)
                return default;
           
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var documents = await CompositeQuery.Create(database.Value,
                    db => (_, ct) => db.FindAsync(printer.Value, Tool.SchemaInstance, ct), db => db.GetTargetAsync,
                    db => (t, ct) => db.FindAsync(t, Nozzle.SchemaInstance, ct), db => db.GetTargetAsync,
                    db => (n, ct) => db.FindAsync(n, ToolMaterial.SchemaInstance, ct), db => db.GetTargetAsync,
                    db => (tm, ct) => db.FindAsync(Material.SchemaInstance, tm, ct), db => db.GetSourceAsync)
                    .ExecuteAsync<Material>(cts.Token);

            var materials = documents.Distinct()
                .OrderBy(d => d[d => d.MaterialType])
                .ThenBy(d => d.Name)
                .AsObservableChangeSet(m => m.DocumentGuid);

            var result = await Flux.ShowDialogAsync(f => new CreateMaterialDialog(f, last_tag.ValueOr(() => new NFCMaterial()), materials));
            if (result.result != DialogResult.Primary)
                return default;

            return new NFCReading<NFCMaterial>(virtual_card_id, result.data);
        }
        async Task<NFCReading<NFCToolNozzle>> INFCStorageProvider<NFCToolNozzle>.CreateTagAsync(ushort position, Optional<NFCToolNozzle> last_tag, CardId virtual_card_id)
        {
            var database = Flux.DatabaseProvider.Database;
            if (!database.HasValue)
                return default;

            var printer = Flux.SettingsProvider.Printer;
            if (!printer.HasValue)
                return default;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var documents = await CompositeQuery.Create(database.Value,
               db => (_, ct) => db.FindAsync(printer.Value, Tool.SchemaInstance, ct), db => db.GetTargetAsync)
               .ExecuteAsync<Tool>(cts.Token);

            var tools = documents.Distinct()
              .AsObservableChangeSet(m => m.DocumentGuid);

            var result = await Flux.ShowDialogAsync(f => new CreateToolNozzleDialog(f, last_tag.ValueOr(() => new NFCToolNozzle()), tools));
            if (result.result != DialogResult.Primary)
                return default;

            return new NFCReading<NFCToolNozzle>(virtual_card_id, result.data);
        }

        IObservable<IChangeSet<NFCSlot<NFCMaterial>, ushort>> INFCStorageProvider<NFCMaterial>.GetNFCSlots() => ToolMaterials.Connect().Transform(m => m.NFCSlot);
        IObservable<IChangeSet<NFCSlot<NFCToolNozzle>, ushort>> INFCStorageProvider<NFCToolNozzle>.GetNFCSlots() => ToolNozzles.Connect().Transform(m => m.NFCSlot);
    }

    public abstract class CreateTagDialog<TCreateTagDialog, TNFCDocument, TTag> : InputDialog<TCreateTagDialog, TTag>
        where TCreateTagDialog : CreateTagDialog<TCreateTagDialog, TNFCDocument, TTag>
        where TNFCDocument : INFCDocument
        where TTag : INFCTag
    {
        [RemoteInput()]
        public SelectableCache<TNFCDocument, Guid> Documents { get; }

        public CreateTagDialog(IFlux flux, TTag startValue, Func<TTag, Guid> get_selected_key, IObservable<IChangeSet<TNFCDocument, Guid>> documents) : base(flux, startValue, new RemoteText("title", true)) 
        {
            Documents = SelectableCache.Create(documents, get_selected_key(startValue));
            Documents.AutoSelect = Documents.ItemsChanged.Select(n => n.Keys.FirstOrOptional(k => k != Guid.Empty)).ToOptional();
        }
    }
    public abstract class CreateOdometerTagDialog<TCreateOdometerTagDialog, TNFCOdometerDocument, TNFCOdometerTag> : CreateTagDialog<TCreateOdometerTagDialog, TNFCOdometerDocument, TNFCOdometerTag>
        where TCreateOdometerTagDialog : CreateOdometerTagDialog<TCreateOdometerTagDialog, TNFCOdometerDocument, TNFCOdometerTag>
        where TNFCOdometerDocument : INFCDocument
        where TNFCOdometerTag : INFCOdometerTag
    {
        private double _MaxWeight = 1000.0;
        [RemoteInput(step: 50.0, min: 100, max: 20000, converter: typeof(WeightConverter))]
        public double MaxWeight
        {
            get => _MaxWeight;
            set => this.RaiseAndSetIfChanged(ref _MaxWeight, value);
        }

        private double _CurWeight = 1000.0;
        [RemoteInput(step: 50.0, min: 0, max: 20000, converter: typeof(WeightConverter))]
        public double CurWeight
        {
            get => _CurWeight;
            set => this.RaiseAndSetIfChanged(ref _CurWeight, value);
        }

        public CreateOdometerTagDialog(IFlux flux, TNFCOdometerTag startValue, double maxWeight, Func<TNFCOdometerTag, Guid> get_selected_key, IObservable<IChangeSet<TNFCOdometerDocument, Guid>> documents) 
            : base(flux, startValue, get_selected_key, documents)
        {
            MaxWeight = maxWeight;
            CurWeight = maxWeight;
            this.WhenAnyValue(v => v.MaxWeight)
                .BindToRC((TCreateOdometerTagDialog)this, v => v.CurWeight);
        }
    }
    public class CreateMaterialDialog : CreateOdometerTagDialog<CreateMaterialDialog, Material, NFCMaterial>
    {
        public CreateMaterialDialog(IFlux flux, NFCMaterial start_value, IObservable<IChangeSet<Material, Guid>> documents) : base(flux, start_value, 1000, m => m.MaterialGuid, documents)
        {
        }
        public override Optional<NFCMaterial> Confirm() => StartValue.Value with
        {
            CurWeightG = CurWeight,
            MaxWeightG = MaxWeight,
            MaterialGuid = Documents.SelectedKey.ValueOrDefault()
        };
    }
    public class CreateToolNozzleDialog : CreateOdometerTagDialog<CreateToolNozzleDialog, Tool, NFCToolNozzle>
    {
        [RemoteInput()]
        public SelectableCache<Nozzle, Guid> Nozzles { get; }
        public CreateToolNozzleDialog(IFlux flux, NFCToolNozzle start_value, IObservable<IChangeSet<Tool, Guid>> documents) : base(flux, start_value, 10000, m => m.ToolGuid, documents)
        {
            var nozzles = Observable.CombineLatest(
                flux.DatabaseProvider.WhenAnyValue(p => p.Database),
                Documents.SelectedValueChanged,
                (db, tool) => (db, tool))
                .SelectAsync(async t => 
                {
                    if (!t.db.HasValue)
                        return (IEnumerable<Nozzle>)Array.Empty<Nozzle>();
                    if (!t.tool.HasValue)
                        return (IEnumerable<Nozzle>)Array.Empty<Nozzle>();
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    return await CompositeQuery.Create(t.db.Value,
                       db => (_, ct) => db.FindAsync(t.tool.Value, Nozzle.SchemaInstance, ct), db => db.GetTargetAsync)
                       .ExecuteAsync<Nozzle>(cts.Token);
                })
                .AsObservableChangeSet(n => n.DocumentGuid);

            Nozzles = SelectableCache.Create(nozzles, start_value.NozzleGuid);
            Nozzles.AutoSelect = Nozzles.ItemsChanged.Select(n => n.Keys.FirstOrOptional(k => k != Guid.Empty)).ToOptional();
        }
        public override Optional<NFCToolNozzle> Confirm() => StartValue.Value with
        {
            CurWeightG = CurWeight,
            MaxWeightG = MaxWeight,
            ToolGuid = Documents.SelectedKey.ValueOrDefault(),
            NozzleGuid = Nozzles.SelectedKey.ValueOrDefault(),
        };
    }
}
