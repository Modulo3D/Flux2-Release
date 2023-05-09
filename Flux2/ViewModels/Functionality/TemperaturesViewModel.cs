using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System.Collections.Generic;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class TemperaturesViewModel : FluxRoutableViewModel<TemperaturesViewModel>
    {
        [RemoteContent(true, comparer:(nameof(TemperatureViewModel.Position)))]
        public IObservableCache<TemperatureViewModel, string> Temperatures { get; set; }

        public TemperaturesViewModel(FluxViewModel flux) : base(flux)
        {
            Temperatures = Flux.SettingsProvider
                .WhenAnyValue(v => v.ExtrudersCount)
                .Select(FindTemperatures)
                .AsObservableChangeSet(t => t.Name)
                .AsObservableCacheRC(this);
        }

        private IEnumerable<TemperatureViewModel> FindTemperatures(Optional<(ushort machine_extruders, ushort mixing_extruders)> extruders)
        {
            ushort temp_pos = 0;

            if (extruders.HasValue)
            {
                var variable_store = Flux.ConnectionProvider.VariableStoreBase;

                for (ushort i = 0; i < extruders.Value.machine_extruders; i++)
                {
                    var extr_index = ArrayIndex.FromZeroBase(i, variable_store);
                    var extr_key = Flux.ConnectionProvider.GetArrayUnit(m => m.TEMP_TOOL, extr_index);
                    var extr_temp = Flux.ConnectionProvider.GetVariable(m => m.TEMP_TOOL, extr_key);
                    if (!extr_temp.HasValue)
                        continue;

                    yield return new TemperatureViewModel(Flux, this, temp_pos++, extr_temp.Value);
                }
            }

            var chamber_units = Flux.ConnectionProvider.GetArrayUnits(c => c.TEMP_CHAMBER);
            foreach (var chamber_unit in chamber_units)
            {
                var chamber_temp = Flux.ConnectionProvider.GetVariable(m => m.TEMP_CHAMBER, chamber_unit.Alias);
                if (chamber_temp.HasValue)
                    yield return new TemperatureViewModel(Flux, this, temp_pos++, chamber_temp.Value);
            }

            var plate_units = Flux.ConnectionProvider.GetArrayUnits(c => c.TEMP_PLATE);
            foreach (var plate_unit in plate_units)
            {
                var plate_temp = Flux.ConnectionProvider.GetVariable(m => m.TEMP_PLATE, plate_unit.Alias);
                if (plate_temp.HasValue)
                    yield return new TemperatureViewModel(Flux, this, temp_pos++, plate_temp.Value);
            }
        }
    }
}
