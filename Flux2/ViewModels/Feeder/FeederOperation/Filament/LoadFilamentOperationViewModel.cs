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
        public LoadFilamentConditionViewModel LoadFilamentCondition { get; }
        public LoadFilamentOperationViewModel(MaterialViewModel material) : base(material)
        {
            LoadFilamentCondition = new LoadFilamentConditionViewModel(Flux, this);
            LoadFilamentCondition.Initialize();
        }

        protected override RemoteText FindTitleText(bool idle)
        {
            return idle ? new RemoteText("waitLoadMaterial", true) : new RemoteText("loadingMaterial", true);
        }
        protected override RemoteText FindOperationText(bool idle)
        {
            return idle ? new RemoteText("loadMaterial", true) : new RemoteText("---", false);
        }
        protected override async Task<bool> ExecuteOperationAsync()
        {
            var core_settings = Flux.SettingsProvider.CoreSettings.Local;
            var variable_store = Flux.ConnectionProvider.VariableStoreBase;
            var feeder_index = ArrayIndex.FromZeroBase(Feeder.Position, variable_store);

            var filament_on_head_index = ArrayIndex.FromZeroBase(Feeder.Position, variable_store);
            var filament_on_head_unit = Flux.ConnectionProvider.GetArrayUnit(c => c.FILAMENT_ON_HEAD, filament_on_head_index);
            var has_filament_on_head = await Flux.ConnectionProvider.ReadVariableAsync(c => c.FILAMENT_ON_HEAD, filament_on_head_unit).ValueOrAsync(() => false);

            if (!Flux.ConnectionProvider.HasVariable(c => c.FILAMENT_AFTER_GEAR))
            {
                var insert_iteration_dist = 10;
                ushort max_insert_iterations = 3;
                if (!await Flux.IterateDialogAsync(f => new ConfirmDialog(f, new RemoteText("insertFilament", true), new RemoteText()), max_insert_iterations,
                    () => Flux.ConnectionProvider.ManualFilamentInsert(feeder_index, insert_iteration_dist, 100)))
                    return await Flux.ConnectionProvider.ManualFilamentExtract(feeder_index, max_insert_iterations, insert_iteration_dist, 500);

                Material.NFCSlot.StoreTag(m => m.SetInserted(core_settings.PrinterGuid, Feeder.Position));
            }

            var filament_settings = GCodeFilamentOperation.Create(Material, park_tool: false, keep_temp: true);
            if (!await ExecuteFilamentOperation(filament_settings, c => c.GetLoadFilamentGCode))
                return false;

            if(!has_filament_on_head)
            {
                ushort max_purge_iterations = 3;
                if (!await Flux.IterateDialogAsync(f => new ConfirmDialog(f, new RemoteText("purgeFilament", true), new RemoteText()), max_purge_iterations,
                    () => Flux.ConnectionProvider.PurgeAsync(filament_settings.Value)))
                    return false;
            }

            Feeder.ToolNozzle.NFCSlot.StoreTag(n => n.SetLastBreakTemp(core_settings.PrinterGuid, filament_settings.Value.CurBreakTemp));
            if (!Flux.ConnectionProvider.HasVariable(c => c.FILAMENT_ON_HEAD))
                Material.NFCSlot.StoreTag(m => m.SetLoaded(core_settings.PrinterGuid, Feeder.Position));  

            return true;
        }
        protected override IEnumerable<(IConditionViewModel condition, FilamentOperationConditionAttribute condition_attribute)> FindConditions()
        {
            foreach (var condition in base.FindConditions())
                yield return condition;
            yield return (LoadFilamentCondition, new FilamentOperationConditionAttribute());
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
