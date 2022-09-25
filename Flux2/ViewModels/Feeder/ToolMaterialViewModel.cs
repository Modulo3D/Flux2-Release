using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class ToolMaterialViewModel : ReactiveObject, IFluxToolMaterialViewModel
    {
        public ushort Position { get; }
        public FluxViewModel Flux { get; }
        public MaterialViewModel Material { get; }
        public ToolNozzleViewModel ToolNozzle { get; }

        private ObservableAsPropertyHelper<Optional<ToolMaterial>> _Document;
        public Optional<ToolMaterial> Document => _Document.Value;

        private ObservableAsPropertyHelper<Optional<double>> _ExtrusionTemp;
        public Optional<double> ExtrusionTemp => _ExtrusionTemp.Value;

        private ObservableAsPropertyHelper<Optional<double>> _BreakTemp;
        public Optional<double> BreakTemp => _BreakTemp.Value;

        private ObservableAsPropertyHelper<ToolMaterialState> _State;
        public ToolMaterialState State => _State.Value;

        public ToolMaterialViewModel(ToolNozzleViewModel tool_nozzle, MaterialViewModel material)
        {
            Material = material;
            Flux = material.Flux;
            ToolNozzle = tool_nozzle;

            _Document = Observable.CombineLatest(
                Material.WhenAnyValue(v => v.Document),
                ToolNozzle.WhenAnyValue(v => v.Document),
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                FindToolMaterial)
                .ToProperty(this, v => v.Document)
                .DisposeWith(Material.Disposables);

            _State = Observable.CombineLatest(
                Material.WhenAnyValue(v => v.Document),
                ToolNozzle.WhenAnyValue(v => v.Document),
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                FindToolMaterialState)
                .ToProperty(this, v => v.State)
                .DisposeWith(Material.Disposables);

            _ExtrusionTemp = this.WhenAnyValue(f => f.Document)
                .Select(d => d.Convert(d => d[d => d.PrintTemperature, 0.0]))
                .ToProperty(this, v => v.ExtrusionTemp)
                .DisposeWith(Material.Disposables);

            _BreakTemp = this.WhenAnyValue(f => f.Document)
                .Select(d => d.Convert(d => d[d => d.BreakTemperature, 0.0]))
                .ToProperty(this, v => v.BreakTemp)
                .DisposeWith(Material.Disposables);
        }

        private ToolMaterialState FindToolMaterialState(Optional<Material> material, (Optional<Tool> tool, Optional<Nozzle> nozzle) tool_nozzle, Optional<ILocalDatabase> database)
        {
            if (!database.HasValue)
                return new ToolMaterialState(default);

            if (!material.HasValue)
                return new ToolMaterialState(default);

            if (!tool_nozzle.tool.HasValue)
                return new ToolMaterialState(default);

            if (!tool_nozzle.nozzle.HasValue)
                return new ToolMaterialState(default);

            var toolmaterials = CompositeQuery.Create(database.Value,
                db => _ => db.Find(material.Value, ToolMaterial.SchemaInstance), db => db.GetTarget,
                db => tm => db.Find(tool_nozzle.nozzle.Value, tm), db => db.GetTarget)
                .Execute()
                .Convert<ToolMaterial>();

            var tool_material = toolmaterials.Documents.FirstOrDefault().ToOptional();
            return new ToolMaterialState(tool_material.HasValue);
        }

        public Optional<ToolMaterial> FindToolMaterial(Optional<Material> material, (Optional<Tool> tool, Optional<Nozzle> nozzle) tool_nozzle, Optional<ILocalDatabase> database)
        {
            if (!database.HasValue)
                return default;

            if (!material.HasValue)
                return default;

            if (!tool_nozzle.tool.HasValue)
                return default;

            if (!tool_nozzle.nozzle.HasValue)
                return default;

            var toolmaterials = CompositeQuery.Create(database.Value,
                db => _ => db.Find(material.Value, ToolMaterial.SchemaInstance), db => db.GetTarget,
                db => tm => db.Find(tool_nozzle.nozzle.Value, tm), db => db.GetTarget)
                .Execute()
                .Convert<ToolMaterial>();

            return toolmaterials.Documents.FirstOrDefault().ToOptional();
        }
    }
}
