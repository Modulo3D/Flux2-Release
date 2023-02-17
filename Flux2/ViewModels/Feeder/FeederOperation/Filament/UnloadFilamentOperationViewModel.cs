using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class UnloadFilamentOperationViewModel : ChangeFilamentOperationViewModel<UnloadFilamentOperationViewModel>
    {
        public UnloadFilamentConditionViewModel UnloadFilamentCondition { get; }
        public UnloadFilamentOperationViewModel(MaterialViewModel material) : base(material)
        {
            UnloadFilamentCondition = new UnloadFilamentConditionViewModel(this);
            UnloadFilamentCondition.Initialize();
        }

        protected override RemoteText FindTitleText(bool idle)
        {
            return idle ? new RemoteText("waitUnloadMaterial", true) : new RemoteText("unloadingMaterial", true);
        }
        protected override RemoteText FindOperationText(bool idle)
        {
            return idle ? new RemoteText("unloadMaterial", true) : new RemoteText("---", false);
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
            yield return (UnloadFilamentCondition, new FilamentOperationConditionAttribute());
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
