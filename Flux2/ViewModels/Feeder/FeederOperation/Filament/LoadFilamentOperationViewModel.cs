using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
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
            if (!Flux.ConnectionProvider.HasVariable(c => c.FILAMENT_AFTER_GEAR))
            {
                var insert_iteration_dist = 10;
                ushort max_insert_iterations = 3;
                if (!await Flux.IterateConfirmDialogAsync("CARICO FILO", "FILO INSERITO CORRETTAMENTE?", max_insert_iterations,
                    () => Flux.ConnectionProvider.ManualFilamentInsert(Feeder.Position, insert_iteration_dist, 100)))
                    return await Flux.ConnectionProvider.ManualFilamentExtract(Feeder.Position, max_insert_iterations, insert_iteration_dist, 500);
            }

            if (!Flux.ConnectionProvider.HasVariable(c => c.FILAMENT_ON_HEAD))
                Material.SetMaterialLoaded(true);

            var filament_settings = GCodeFilamentOperation.Create(Material, false, true);
            if (!await ExecuteFilamentOperation(filament_settings, c => c.GetLoadFilamentGCode))
                return false;

            Feeder.ToolNozzle.SetLastBreakTemp(filament_settings.Value);

            if (!await Flux.IterateConfirmDialogAsync("CARICO FILO", "FILO SPURGATO CORRETTAMENTE?", 3,
                () => Flux.ConnectionProvider.PurgeAsync(filament_settings.Value)))
                return false;

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
                    Material.WhenAnyValue(m => m.Document),
                    Material.WhenAnyValue(f => f.State),
                    Material.ToolMaterial.WhenAnyValue(m => m.State),
                    Feeder.ToolNozzle.WhenAnyValue(f => f.MaterialLoaded),
                    (document, material, tool_material, loaded) => (document, material, tool_material, loaded)),
                    (state, value) =>
                    {
                        if (value.loaded.HasValue)
                        {
                            var mat = value.loaded.Value.Document;
                            var pos = value.loaded.Value.Position;

                            if(value.loaded.Value.Position == Material.Position)
                                return state.Create(EConditionState.Disabled, $"MATERIALE CARICATO ({mat} POS. {pos + 1})");
                            return state.Create(EConditionState.Warning, $"MATERIALE GIA' CARICATO ({mat} POS. {pos + 1})");
                        }

                        var update_nfc = state.Create("nFC", UpdateNFCAsync, can_update_nfc);

                        if (value.document.ConvertOr(d => d.Id == 0, () => true))
                            return state.Create(EConditionState.Warning, "LEGGI UN MATERIALE", update_nfc);

                        if (!value.tool_material.Compatible.ValueOrDefault())
                            return state.Create(EConditionState.Error, $"{value.document} NON COMPATIBILE");

                        if (!value.material.Locked)
                            return state.Create(EConditionState.Warning, "BLOCCA IL MATERIALE", update_nfc);

                        return new ConditionState(EConditionState.Stable, $"{value.document} PRONTO AL CARICAMENTO");
                    }), new FilamentOperationConditionAttribute());
        }

        public override async Task<bool> UpdateNFCAsync()
        {
            await Material.UpdateTagAsync();

            var result = Material.Nfc.IsVirtualTag ?
                Material.LockTag(default) :
                await Flux.UseReader(h => Material.LockTag(h));

            if (!result.HasValue || !result.Value)
                return false;

            return true;
        }
        protected override Task<bool> CancelOperationAsync()
        {
            return CancelFilamentOperationAsync(c => c.GetCancelLoadFilamentGCode);
        }
    }
}
