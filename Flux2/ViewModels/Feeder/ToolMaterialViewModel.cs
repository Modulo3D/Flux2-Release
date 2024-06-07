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
    public class ToolMaterialViewModel : ReactiveObjectRC<ToolMaterialViewModel>, IFluxToolMaterialViewModel, IDisposable
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

        public ToolMaterialViewModel(IFluxToolNozzleViewModel tool_nozzle, MaterialViewModel material)
        {
            Material = material;
            Flux = material.Flux;
            ToolNozzle = tool_nozzle;

            _Document = Observable.CombineLatest(
                Material.WhenAnyValue(v => v.Document),
                ToolNozzle.WhenAnyValue(v => v.Document),
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                FindToolMaterialAsync)
                .SelectAsync()
                .ToPropertyRC(this, v => v.Document);

            _State = Observable.CombineLatest(
                Material.WhenAnyValue(v => v.Document),
                ToolNozzle.WhenAnyValue(v => v.Document),
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                FindToolMaterialStateAsync)
                .SelectAsync()
                .ToPropertyRC(this, v => v.State);

            _ExtrusionTemp = this.WhenAnyValue(f => f.Document)
                .Select(d => d.Convert(d => d[d => d.PrintTemperature]))
                .ToPropertyRC(this, v => v.ExtrusionTemp);

            _BreakTemp = this.WhenAnyValue(f => f.Document)
                .Select(d => d.Convert(d => d[d => d.BreakTemperature]))
                .ToPropertyRC(this, v => v.BreakTemp);
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

            var tool_material = toolmaterials.FirstOrOptional(_ => true);
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

            return toolmaterials.FirstOrOptional(_ => true);
        }
    }
}
