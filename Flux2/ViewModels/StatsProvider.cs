using DynamicData;
using Modulo3DNet;

namespace Flux.ViewModels
{
    public class StatsProvider : IFluxStatsProvider
    {
        public FluxViewModel Flux { get; }
        IFlux IFluxStatsProvider.Flux => Flux;
        public LocalSettingsProvider<FluxStats> Stats { get; }

        public StatsProvider(FluxViewModel flux)
        {
            Flux = flux;
            Stats = new LocalSettingsProvider<FluxStats>(Files.Stats);
        }

        public void MarkUsedPrintArea(MCode mcode)
        {
            var print_areas = Stats.Local.UsedPrintAreas;
            foreach (var print_area in mcode.PrintAreas)
                print_areas.Add(print_area);
            PersistLocalSettings();
        }

        public void ClearUsedPrintAreas()
        {
            var print_areas = Stats.Local.UsedPrintAreas;
            print_areas.Clear();
            PersistLocalSettings();
        }

        public bool PersistLocalSettings()
        {
            var result = Stats.PersistLocalSettings();
            // Flux.Messages.LogMessage("Salvataggio statistiche", result ? "Statistiche salvate" : "Errore di salvataggio", result ? MessageLevel.DEBUG : MessageLevel.ERROR, 0);
            return result;
        }
    }
}
