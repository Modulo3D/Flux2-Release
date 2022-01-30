using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System.Collections.Generic;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class TemperaturesViewModel : FluxRoutableViewModel<TemperaturesViewModel>
    {
        [RemoteContent(true)]
        public IObservableCache<TemperatureViewModel, string> Temperatures { get; set; }

        public TemperaturesViewModel(FluxViewModel flux) : base(flux)
        {
            Temperatures = Flux.SettingsProvider
                .WhenAnyValue(v => v.ExtrudersCount)
                .Select(FindTemperatures)
                .ToObservableChangeSet(t => t.Name)
                .AsObservableCache();
        }

        private IEnumerable<TemperatureViewModel> FindTemperatures(Optional<ushort> extruders)
        {
            if (extruders.HasValue && Flux.ConnectionProvider.VariableStore.HasVariable(m => m.TEMP_TOOL))
            {
                for (ushort i = 0; i < extruders.Value; i++)
                {
                    var extruder = i;
                    var extr_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.TEMP_TOOL, i);
                    if (!extr_key.HasValue)
                        continue;

                    var extr_temp = Flux.ConnectionProvider.ObserveVariable(m => m.TEMP_TOOL, extr_key.Value);
                    yield return new TemperatureViewModel(this, $"Estrusore {i + 1}", $"{i}", t => Flux.ConnectionProvider.WriteVariableAsync(m => m.TEMP_TOOL, extr_key.Value, t), extr_temp);
                }
            }

            if (Flux.ConnectionProvider.VariableStore.HasVariable(m => m.TEMP_CHAMBER))
            {
                var chamber_temp = Flux.ConnectionProvider.ObserveVariable(m => m.TEMP_CHAMBER);
                yield return new TemperatureViewModel(this, "Camera", $"chamber", t => Flux.ConnectionProvider.WriteVariableAsync(m => m.TEMP_CHAMBER, t), chamber_temp);
            }

            if (Flux.ConnectionProvider.VariableStore.HasVariable(m => m.TEMP_PLATE))
            {
                var plate_temp = Flux.ConnectionProvider.ObserveVariable(m => m.TEMP_PLATE);
                yield return new TemperatureViewModel(this, "Piatto", $"plate", t => Flux.ConnectionProvider.WriteVariableAsync(m => m.TEMP_PLATE, t), plate_temp);
            }
        }
    }
}
