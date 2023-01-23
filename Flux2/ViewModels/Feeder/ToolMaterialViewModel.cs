using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class ToolMaterialViewModel : ReactiveObject, IFluxToolMaterialViewModel, IDisposable
    {
        public ushort Position { get; }
        public FluxViewModel Flux { get; }
        public IFluxMaterialViewModel Material { get; }
        public IFluxToolNozzleViewModel ToolNozzle { get; }

        private readonly ObservableAsPropertyHelper<Optional<ToolMaterial>> _Document;
        public Optional<ToolMaterial> Document => _Document.Value;

        private readonly ObservableAsPropertyHelper<Optional<double>> _ExtrusionTemp;
        public Optional<double> ExtrusionTemp => _ExtrusionTemp.Value;

        private readonly ObservableAsPropertyHelper<Optional<double>> _BreakTemp;
        public Optional<double> BreakTemp => _BreakTemp.Value;

        private readonly ObservableAsPropertyHelper<ToolMaterialState> _State;
        public ToolMaterialState State => _State.Value;

        public CompositeDisposable Disposables { get; }

        public ToolMaterialViewModel(IFluxToolNozzleViewModel tool_nozzle, MaterialViewModel material)
        {
            Material = material;
            Flux = material.Flux;
            ToolNozzle = tool_nozzle;
            Disposables = new CompositeDisposable();

            _Document = Observable.CombineLatest(
                Material.WhenAnyValue(v => v.Document),
                ToolNozzle.WhenAnyValue(v => v.Document),
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                FindToolMaterialAsync)
                .SelectMany(o => Observable.FromAsync(() => o))
                .ToProperty(this, v => v.Document)
                .DisposeWith(Disposables);

            _State = Observable.CombineLatest(
                Material.WhenAnyValue(v => v.Document),
                ToolNozzle.WhenAnyValue(v => v.Document),
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                FindToolMaterialStateAsync)
                .SelectMany(o => Observable.FromAsync(() => o))
                .ToProperty(this, v => v.State)
                .DisposeWith(Disposables);

            _ExtrusionTemp = this.WhenAnyValue(f => f.Document)
                .Select(d => d.Convert(d => d[d => d.PrintTemperature, 0.0]))
                .ToProperty(this, v => v.ExtrusionTemp)
                .DisposeWith(Disposables);

            _BreakTemp = this.WhenAnyValue(f => f.Document)
                .Select(d => d.Convert(d => d[d => d.BreakTemperature, 0.0]))
                .ToProperty(this, v => v.BreakTemp)
                .DisposeWith(Disposables);
        }

        private async Task<ToolMaterialState> FindToolMaterialStateAsync(Optional<Material> material, (Optional<Tool> tool, Optional<Nozzle> nozzle) tool_nozzle, Optional<ILocalDatabase> database)
        {
            if (!database.HasValue)
                return new ToolMaterialState(default);

            if (!material.HasValue)
                return new ToolMaterialState(default);

            if (!tool_nozzle.tool.HasValue)
                return new ToolMaterialState(default);

            if (!tool_nozzle.nozzle.HasValue)
                return new ToolMaterialState(default);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var toolmaterials = await CompositeQuery.Create(database.Value,
                db => (_, ct) => db.FindAsync(material.Value, ToolMaterial.SchemaInstance, ct), db => db.GetTargetAsync,
                db => (tm, ct) => db.FindAsync(tool_nozzle.nozzle.Value, tm, ct), db => db.GetTargetAsync)
                .ExecuteAsync<ToolMaterial>(cts.Token);

            var tool_material = toolmaterials.FirstOrDefault().ToOptional();
            return new ToolMaterialState(tool_material.HasValue);
        }

        public static async Task<Optional<ToolMaterial>> FindToolMaterialAsync(Optional<Material> material, (Optional<Tool> tool, Optional<Nozzle> nozzle) tool_nozzle, Optional<ILocalDatabase> database)
        {
            if (!database.HasValue)
                return default;

            if (!material.HasValue)
                return default;

            if (!tool_nozzle.tool.HasValue)
                return default;

            if (!tool_nozzle.nozzle.HasValue)
                return default;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var toolmaterials = await CompositeQuery.Create(database.Value,
                db => (_, ct) => db.FindAsync(material.Value, ToolMaterial.SchemaInstance, ct), db => db.GetTargetAsync,
                db => (tm, ct) => db.FindAsync(tool_nozzle.nozzle.Value, tm, ct), db => db.GetTargetAsync)
                .ExecuteAsync<ToolMaterial>(cts.Token);

            return toolmaterials.FirstOrDefault().ToOptional();
        }

        public void Dispose()
        {
            Disposables.Dispose();
        }
    }
}
