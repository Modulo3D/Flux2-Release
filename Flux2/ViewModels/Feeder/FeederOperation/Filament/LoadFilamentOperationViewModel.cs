using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class LoadFilamentOperationViewModel : ChangeFilamentOperationViewModel<LoadFilamentOperationViewModel>
    {
        public LoadFilamentOperationViewModel(MaterialViewModel material) : base(material)
        {
        }

        protected override string FindTitleText(bool idle)
        {
            return idle ? "ATTESA CARICAMENTO" : "CARICO FILO...";
        }
        protected override string FindOperationText(bool idle)
        {
            return idle ? "CARICA" : "---";
        }
        protected override async Task<bool> ExecuteOperationAsync()
        {
            var variable_store = Flux.ConnectionProvider.VariableStoreBase;
            var feeder_index = ArrayIndex.FromZeroBase(Feeder.Position, variable_store);

            if (!Flux.ConnectionProvider.HasVariable(c => c.FILAMENT_AFTER_GEAR))
            {
                var insert_iteration_dist = 10;
                ushort max_insert_iterations = 3;
                if (!await Flux.IterateConfirmDialogAsync("CARICO FILO", "FILO INSERITO CORRETTAMENTE?", max_insert_iterations,
                    () => Flux.ConnectionProvider.ManualFilamentInsert(feeder_index, insert_iteration_dist, 100)))
                    return await Flux.ConnectionProvider.ManualFilamentExtract(feeder_index, max_insert_iterations, insert_iteration_dist, 500);
            }

            var filament_settings = GCodeFilamentOperation.Create(Material, park_tool: false, keep_temp: true);
            if (!await ExecuteFilamentOperation(filament_settings, c => c.GetLoadFilamentGCode))
                return false;

            if (!await Flux.IterateConfirmDialogAsync("CARICO FILO", "FILO SPURGATO CORRETTAMENTE?", 3,
                () => Flux.ConnectionProvider.PurgeAsync(filament_settings.Value)))
                return false;

            Feeder.ToolNozzle.SetLastBreakTemp(filament_settings.Value);
            if (!Flux.ConnectionProvider.HasVariable(c => c.FILAMENT_ON_HEAD))
                Material.SetMaterialLoaded(true);

            return true;
        }
        protected override IEnumerable<(IConditionViewModel condition, FilamentOperationConditionAttribute condition_attribute)> FindConditions()
        {
            foreach (var condition in base.FindConditions())
                yield return condition;

            var can_update_nfc = Material
                .WhenAnyValue(m => m.State)
                .Select(s =>
                {
                    if (s.Inserted && !s.Known)
                        return false;
                    if (s.Loaded || s.Locked)
                        return false;
                    return true;
                });

            yield return (ConditionViewModel.Create(
                Flux.StatusProvider,
                "material",
                Observable.CombineLatest(
                    Material.WhenAnyValue(f => f.State),
                    Material.WhenAnyValue(m => m.Document),
                    Material.ToolMaterial.WhenAnyValue(m => m.State),
                    (state, material, tool_material) => (state, material, tool_material)),
                    (state, value) =>
                    {
                        if (value.state.Loaded)
                            return state.Create(EConditionState.Disabled, $"{value.material} CARICATO");

                        var update_nfc = state.Create("nFC", UpdateNFCAsync, can_update_nfc);

                        if (!value.material.HasValue)
                            return state.Create(EConditionState.Disabled, "LEGGI UN MATERIALE", update_nfc);

                        if (!value.tool_material.Compatible.ValueOr(() => false))
                            return state.Create(EConditionState.Error, $"{value.material} NON COMPATIBILE");

                        if (!value.state.Locked)
                            return state.Create(EConditionState.Warning, "BLOCCA IL MATERIALE", update_nfc);

                        return new ConditionState(EConditionState.Stable, $"{value.material} PRONTO AL CARICAMENTO");
                    }), new FilamentOperationConditionAttribute());
        }

        public override async Task<NFCTagRW> UpdateNFCAsync()
        {
            return await Flux.UseReader(Material, (h, m, c) => m.LockTagAsync(h, c), r => r == NFCTagRW.Success);
        }
        protected override Task<bool> CancelOperationAsync()
        {
            return CancelFilamentOperationAsync(c => c.GetCancelLoadFilamentGCode);
        }
    }
}
