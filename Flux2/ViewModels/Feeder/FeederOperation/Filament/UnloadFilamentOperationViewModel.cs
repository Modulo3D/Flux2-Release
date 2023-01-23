﻿using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System.Collections.Generic;
using System.Reactive.Linq;
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
            var core_settings = Flux.SettingsProvider.CoreSettings.Local;

            if (!Flux.ConnectionProvider.HasVariable(c => c.FILAMENT_ON_HEAD))
                Material.NFCSlot.StoreTag(m => m.SetLoaded(core_settings.PrinterGuid, default));

            var filament_settings = GCodeFilamentOperation.Create(Material);
            if (!await ExecuteFilamentOperation(filament_settings, c => c.GetUnloadFilamentGCode))
                return false;

            if (!Flux.ConnectionProvider.HasVariable(c => c.FILAMENT_BEFORE_GEAR))
                Material.NFCSlot.StoreTag(m => m.SetInserted(core_settings.PrinterGuid, default));

            if (Material.Odometer.CurrentValue.HasValue)
            {
                var core_setting = Flux.SettingsProvider.CoreSettings.Local;
                if (Material.Odometer.CurrentValue.Value <= 0)
                    Material.NFCSlot.StoreTag(m => m.SetCurWeight(core_setting.PrinterGuid, default));
            }

            return false;
        }
        protected override IEnumerable<(IConditionViewModel condition, FilamentOperationConditionAttribute condition_attribute)> FindConditions()
        {
            foreach (var condition in base.FindConditions())
                yield return condition;

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
                        var update_nfc = state.Create("nFC", UpdateNFCAsync);

                        if (!value.state.Loaded && !value.state.Inserted && value.state.Locked)
                            return state.Create(EConditionState.Warning, "SBLOCCA IL MATERIALE", update_nfc);

                        if (!value.state.Loaded && !value.state.Inserted && !value.state.Locked)
                            return state.Create(EConditionState.Disabled, $"MATERIALE SCARICATO");

                        if (!value.material.HasValue)
                            return state.Create(EConditionState.Warning, "LEGGI UN MATERIALE", update_nfc);

                        if (!value.tool_material.Compatible.ValueOr(() => false))
                            return state.Create(EConditionState.Error, $"{value.material} NON COMPATIBILE", update_nfc);

                        return new ConditionState(EConditionState.Stable, $"{value.material} PRONTO ALLO SCARICAMENTO");
                    }), new FilamentOperationConditionAttribute());
        }
        public override async Task<NFCTagRW> UpdateNFCAsync()
        {
            var result = await Flux.UseReader(Material, (h, m, c) => m.UnlockTagAsync(h, c), r => r == NFCTagRW.Success);
            if (result == NFCTagRW.Success)
                Flux.Navigator.NavigateBack();
            return result;
        }

        protected override Task<bool> CancelOperationAsync()
        {
            return CancelFilamentOperationAsync(c => c.GetCancelUnloadFilamentGCode);
        }
    }
}
