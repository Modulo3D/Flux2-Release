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
            return idle ? "waitLoadMaterial" : "loadingMaterial";
        }
        protected override string FindOperationText(bool idle)
        {
            return idle ? "loadMaterial" : "---";
        }
        protected override async Task<bool> ExecuteOperationAsync()
        {
            var core_settings = Flux.SettingsProvider.CoreSettings.Local;
            var variable_store = Flux.ConnectionProvider.VariableStoreBase;
            var feeder_index = ArrayIndex.FromZeroBase(Feeder.Position, variable_store);

            if (!Flux.ConnectionProvider.HasVariable(c => c.FILAMENT_AFTER_GEAR))
            {
                var insert_iteration_dist = 10;
                ushort max_insert_iterations = 3;
                if (!await Flux.IterateConfirmDialogAsync("loadFilamentDialog", "isFilamentLoaded", max_insert_iterations,
                    () => Flux.ConnectionProvider.ManualFilamentInsert(feeder_index, insert_iteration_dist, 100)))
                    return await Flux.ConnectionProvider.ManualFilamentExtract(feeder_index, max_insert_iterations, insert_iteration_dist, 500);

                Material.NFCSlot.StoreTag(m => m.SetInserted(core_settings.PrinterGuid, Material.Position));
            }

            var filament_settings = GCodeFilamentOperation.Create(Material, park_tool: false, keep_temp: true);
            if (!await ExecuteFilamentOperation(filament_settings, c => c.GetLoadFilamentGCode))
                return false;

            if (!await Flux.IterateConfirmDialogAsync("loadFilamentDialog", "isFilamentPurged", 3,
                () => Flux.ConnectionProvider.PurgeAsync(filament_settings.Value)))
                return false;

            Feeder.ToolNozzle.NFCSlot.StoreTag(n => n.SetLastBreakTemp(core_settings.PrinterGuid, filament_settings.Value.CurBreakTemp));
            if (!Flux.ConnectionProvider.HasVariable(c => c.FILAMENT_ON_HEAD))
                Material.NFCSlot.StoreTag(m => m.SetLoaded(core_settings.PrinterGuid, Material.Position));

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
                            return new ConditionState(EConditionState.Disabled, $"material.loaded; {value.material}");

                        if (!value.material.HasValue)
                            return new ConditionState(EConditionState.Disabled, "material.read");

                        if (!value.tool_material.Compatible.ValueOr(() => false))
                            return new ConditionState(EConditionState.Error, $"material.incompatible; {value.material}");

                        if (!value.state.Locked)
                            return new ConditionState(EConditionState.Warning, "material.lock");

                        return new ConditionState(EConditionState.Stable, $"material.readyToLoad; {value.material}");
                    }, state => state.Create(UpdateNFCAsync, "nFC", can_update_nfc)), new FilamentOperationConditionAttribute());
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
