using DynamicData.Kernel;
using Modulo3DDatabase;
using Modulo3DStandard;
using ReactiveUI;
using System.Linq;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class ToolMaterialViewModel : ReactiveObject, IFluxToolMaterialViewModel
    {
        public FluxViewModel Flux { get; }
        public FeederViewModel Feeder { get; }

        private ObservableAsPropertyHelper<Optional<ToolMaterial>> _Document;
        public Optional<ToolMaterial> Document => _Document.Value;

        private ObservableAsPropertyHelper<Optional<double>> _ExtrusionTemp;
        public Optional<double> ExtrusionTemp => _ExtrusionTemp.Value;

        private ObservableAsPropertyHelper<Optional<double>> _BreakTemp;
        public Optional<double> BreakTemp => _BreakTemp.Value;

        public ToolMaterialViewModel(FeederViewModel feeder)
        {
            Feeder = feeder;
            Flux = feeder.Flux;
        }

        public void Initialize()
        {
            _Document = Observable.CombineLatest(
                Feeder.Material.WhenAnyValue(v => v.Document),
                Feeder.ToolNozzle.WhenAnyValue(v => v.Document),
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                FindToolMaterial)
                .ToProperty(this, v => v.Document);

            _ExtrusionTemp = this.WhenAnyValue(f => f.Document)
                .Select(d => d.Convert(d => d.PrintTemperature).ToOptional(t => t > 150))
                .ToProperty(this, v => v.ExtrusionTemp);

            _BreakTemp = this.WhenAnyValue(f => f.Document)
                .Select(d => d.Convert(d => d.BreakTemperature).ToOptional(t => t > 150))
                .ToProperty(this, v => v.BreakTemp);
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
