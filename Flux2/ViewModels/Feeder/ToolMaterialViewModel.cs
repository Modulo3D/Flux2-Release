using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
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
        }

        public void Initialize()
        {
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
                this.WhenAnyValue(v => v.Document),
                (m, tn, tm) => new ToolMaterialState(tn.tool.HasValue && tn.nozzle.HasValue, m.HasValue, tm.HasValue))
                .ToProperty(this, v => v.State)
                .DisposeWith(Material.Disposables);

            _ExtrusionTemp = this.WhenAnyValue(f => f.Document)
                .Select(d => d.Convert(d => d.PrintTemperature).ToOptional(t => t > 150))
                .ToProperty(this, v => v.ExtrusionTemp)
                .DisposeWith(Material.Disposables);

            _BreakTemp = this.WhenAnyValue(f => f.Document)
                .Select(d => d.Convert(d => d.BreakTemperature).ToOptional(t => t > 150))
                .ToProperty(this, v => v.BreakTemp)
                .DisposeWith(Material.Disposables);
        }

        public Optional<ToolMaterial> FindToolMaterial(Optional<Material> material, (Optional<Tool> tool, Optional<Nozzle> nozzle) feeder, Optional<ILocalDatabase> database)
        {
            if (!database.HasValue)
                return default;

            if (!material.HasValue)
                return default;

            if (!feeder.tool.HasValue)
                return default;

            if (!feeder.nozzle.HasValue)
                return default;

            var toolmaterials = CompositeQuery.Create(database.Value,
                db => _ => db.Find(material.Value, ToolMaterial.SchemaInstance), db => db.GetTarget,
                db => tm => db.Find(feeder.nozzle.Value, tm), db => db.GetTarget)
                .Execute()
                .Convert<ToolMaterial>();

            return toolmaterials.Documents.FirstOrDefault().ToOptional();
        }
    }
}
