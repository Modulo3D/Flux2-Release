using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class UnloadFilamentOperationViewModel : ChangeFilamentOperationViewModel<UnloadFilamentOperationViewModel>
    {
        public UnloadFilamentOperationViewModel(MaterialViewModel material) : base(material)
        {
        }

        protected override string FindTitleText(bool idle)
        {
            return idle ? "ATTESA SCARICAMENTO" : "SCARICO FILO...";
        }
        protected override string FindOperationText(bool idle)
        {
            return idle ? "SCARICA" : "---";
        }
        protected override async Task<bool> ExecuteOperationAsync()
        {
            var filament_settings = GCodeFilamentOperation.Create(Material, true, false);
            if (!await ExecuteFilamentOperation(filament_settings, c => c.GetUnloadFilamentGCode))
                return false;

            if(!Flux.ConnectionProvider.HasVariable(c => c.FILAMENT_ON_HEAD))
                Material.SetMaterialLoaded(false);

            return true;
        }
        protected override IEnumerable<(IConditionViewModel condition, FilamentOperationConditionAttribute condition_attribute)> FindConditions()
        {
            foreach (var condition in base.FindConditions())
                yield return condition;

            yield return (ConditionViewModel.Create(
                Flux.StatusProvider,
                "material",
                Observable.CombineLatest(
                    Feeder.ToolNozzle.WhenAnyValue(f => f.MaterialLoaded),
                    Material.WhenAnyValue(m => m.Document),
                    Material.WhenAnyValue(f => f.State),
                    Material.ToolMaterial.WhenAnyValue(m => m.State),
                    (loaded, document, material, tool_material) => (loaded, document, material, tool_material)),
                    (state, value) =>
                    {
                        if (!value.loaded.HasValue)
                            return state.Create(EConditionState.Warning, $"MATERIALE NON CARICATO");

                        var update_nfc = state.Create("nFC", UpdateNFCAsync);

                        if (value.document.ConvertOr(d => d.Id == 0, () => true))
                            return state.Create(EConditionState.Warning, "LEGGI UN MATERIALE", update_nfc);

                        if (!value.tool_material.Compatible.ValueOrDefault())
                            return state.Create(EConditionState.Error, $"{value.document} NON COMPATIBILE", update_nfc);

                        if (!value.material.Locked)
                            return state.Create(EConditionState.Warning, "SBLOCCA IL MATERIALE", update_nfc);

                        return new ConditionState(EConditionState.Stable, $"{value.document} PRONTO ALLO SCARICAMENTO");
                    }), new FilamentOperationConditionAttribute());
        }
        public override async Task<bool> UpdateNFCAsync()
        {
            await Material.UpdateTagAsync();

            var result = Material.Nfc.IsVirtualTag ?
                Material.UnlockTag(default) :
                await Flux.UseReader(h => Material.UnlockTag(h));

            if (!result.HasValue || !result.Value)
                return false;

            Flux.Navigator.NavigateBack();
            return true;
        }

        protected override Task<bool> CancelOperationAsync()
        {
            return CancelFilamentOperationAsync(c => c.GetCancelUnloadFilamentGCode);
        }
    }
}
