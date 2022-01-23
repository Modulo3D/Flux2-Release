using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class SettingsProvider : ReactiveObject, IFluxSettingsProvider
    {
        public FluxViewModel Flux { get; }

        private ObservableAsPropertyHelper<Optional<Printer>> _Printer;
        public Optional<Printer> Printer => _Printer.Value;

        private LocalSettingsProvider<FluxCoreSettings> _CoreSettings;
        public LocalSettingsProvider<FluxCoreSettings> CoreSettings
        {
            get
            {
                if (_CoreSettings == default)
                    _CoreSettings = new LocalSettingsProvider<FluxCoreSettings>(Files.CoreSettings);
                return _CoreSettings;
            }
        }

        private LocalSettingsProvider<FluxUserSettings> _UserSettings;
        public LocalSettingsProvider<FluxUserSettings> UserSettings
        {
            get
            {
                if (_UserSettings == default)
                    _UserSettings = new LocalSettingsProvider<FluxUserSettings>(Files.UserSettings);
                return _UserSettings;
            }
        }

        private ObservableAsPropertyHelper<Optional<ushort>> _ExtrudersCount;
        public Optional<ushort> ExtrudersCount => _ExtrudersCount.Value;

        public SettingsProvider(FluxViewModel flux)
        {
            Flux = flux;

            _Printer = Observable.CombineLatest(
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                CoreSettings.Local.WhenAnyValue(s => s.PrinterID),
                (db, id) => db.Convert(db => id.Convert(id => db.FindById<Printer>(id))))
                .Convert(r => r.Documents.FirstOrOptional(_ => true))
                .ToProperty(this, v => v.Printer);

            _ExtrudersCount = this.WhenAnyValue(v => v.Printer)
                .Select(GetExtruderCount)
                .ToProperty(this, v => v.ExtrudersCount);
        }

        public async Task<bool> ResetMagazineAsync()
        {
            if (!ExtrudersCount.HasValue)
            {
                Flux.Messages.LogMessage("Errore resetta magazzino", $"Stampante non configurata", MessageLevel.ERROR, 0);
                return false;
            }

            var enable_drivers = Flux.ConnectionProvider.VariableStore.GetVariables(m => m.ENABLE_DRIVERS);
            if (enable_drivers.HasValue)
            {
                foreach (var variable in enable_drivers.Value.Items)
                {
                    if (!await Flux.ConnectionProvider.WriteVariableAsync(variable, false))
                    {
                        Flux.Messages.LogMessage("Errore resetta magazzino", $"Asse {variable.Unit} non disabilitato", MessageLevel.ERROR, 0);
                        return false;
                    }
                }
            }

            var result = await Flux.ShowConfirmDialogAsync("Riponi l'utensile selezionato", "Se è presente un utensile sul carrello, riporlo manualmente prima di continuare");
            if (result != ContentDialogResult.Primary)
                return true;

            if (!await Flux.ConnectionProvider.ResetClampAsync())
            { 
                Flux.Messages.LogMessage("Errore resetta magazzino", "Pinza non resettata", MessageLevel.ERROR, 0);
                return false;
            }

            if (!await Flux.ConnectionProvider.WriteVariableAsync(m => m.IN_CHANGE, false))
            { 
                Flux.Messages.LogMessage("Errore resetta magazzino", "Utensile non deselezionato", MessageLevel.ERROR, 0);
                return false;
            }

            var tool_on_trailer = Flux.ConnectionProvider.VariableStore.GetVariables(m => m.MEM_TOOL_ON_TRAILER);
            if (tool_on_trailer.HasValue)
            {
                foreach (var variable in tool_on_trailer.Value.Items)
                {
                    if (!await Flux.ConnectionProvider.WriteVariableAsync(variable, false))
                    { 
                        Flux.Messages.LogMessage("Errore resetta magazzino", "Tool sul carrello", MessageLevel.ERROR, 0);
                        return false;
                    }
                }
            }

            var tool_on_magazine = Flux.ConnectionProvider.VariableStore.GetVariables(m => m.MEM_TOOL_IN_MAGAZINE);
            if (tool_on_magazine.HasValue)
            {
                foreach (var variable in tool_on_magazine.Value.Items)
                {
                    if (!await Flux.ConnectionProvider.WriteVariableAsync(variable, false))
                    {
                        Flux.Messages.LogMessage("Errore resetta magazzino", "Tool nel magazzino", MessageLevel.ERROR, 0);
                        return false;
                    }
                }
            }

            for (ushort extruder = 0; extruder < ExtrudersCount.Value; extruder++)
            {
                var extr_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.MEM_TOOL_IN_MAGAZINE, extruder);
                if (!extr_key.HasValue)
                    return false;

                if(!await Flux.ConnectionProvider.WriteVariableAsync(m => m.MEM_TOOL_IN_MAGAZINE, extr_key.Value, true))
                {
                    Flux.Messages.LogMessage("Errore resetta magazzino", "Tool non nel magazzino", MessageLevel.ERROR, 0);
                    return false;
                }
            }

            return await Flux.ConnectionProvider.ResetAsync();
        }

        // GET EXTRUDERS
        private Optional<ushort> GetExtruderCount(Optional<Printer> printer)
        {
            if (!printer.HasValue)
                return default;
            return printer.Value["machine_extruder_count"].TryGetValue<ushort>();
        }

        public bool PersistLocalSettings()
        {
            var result = CoreSettings.PersistLocalSettings() && UserSettings.PersistLocalSettings();
            Flux.Messages.LogMessage("Salvataggio impostazioni", result ? "Impostazioni salvate" : "Errore di salvataggio", result ? MessageLevel.DEBUG : MessageLevel.ERROR, 0);
            return result;
        }
    }
}
