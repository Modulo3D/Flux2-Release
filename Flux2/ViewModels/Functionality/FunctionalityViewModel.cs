using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public abstract class NFCTagViewModel<TNFCTagViewModel, TTagViewModel, TNFCTag> : NavPanelViewModel<TNFCTagViewModel>
        where TNFCTagViewModel : NFCTagViewModel<TNFCTagViewModel, TTagViewModel, TNFCTag>
        where TTagViewModel : IFluxTagViewModel<TNFCTag>
        where TNFCTag : INFCOdometerTag<TNFCTag>, new()
    {
        private readonly ObservableAsPropertyHelper<Optional<TTagViewModel>> _TagVm;
        public Optional<TTagViewModel> TagVm => _TagVm.Value;
        public ushort LoadPosition { get; }
        public ushort TagPosition { get; }
        public NFCTagViewModel(FluxViewModel flux,
            IObservable<Optional<TTagViewModel>> tag_vm,
            ushort tag_position, ushort load_position) : base(flux, $"{typeof(TNFCTagViewModel).GetRemoteElementClass()};{tag_position + 1}")
        {
            TagPosition = tag_position;
            LoadPosition = load_position;
            _TagVm = tag_vm.ToPropertyRC((TNFCTagViewModel)this, v => v.TagVm);

            var can_lock = Observable.CombineLatest(
              IsUnlocked(),
              IsUnloaded(),
              (l, un) => l && un).ToOptional();
            AddCommand($"{typeof(TNFCTagViewModel).GetRemoteElementClass()}Lock;{tag_position + 1}", lock_tag_async, can_lock);

            var can_unlock = Observable.CombineLatest(
                IsLocked(),
                IsUnloaded(),
                (l, un) => l && un).ToOptional();
            AddCommand($"{typeof(TNFCTagViewModel).GetRemoteElementClass()}Unlock;{tag_position + 1}", unlock_tag_async, can_unlock);

            var can_load = Observable.CombineLatest(
                IsLocked(),
                IsUnloaded(),
                (lk, un) => lk && un).ToOptional();
            AddCommand($"{typeof(TNFCTagViewModel).GetRemoteElementClass()}Load;{tag_position + 1}", inner_load_tag, can_load);

            var can_unload = Observable.CombineLatest(
                IsLocked(),
                IsLoaded(),
                (l, lo) => l && lo).ToOptional();
            AddCommand($"{typeof(TNFCTagViewModel).GetRemoteElementClass()}Unload;{tag_position + 1}", inner_unload_tag, can_unload);

            void inner_load_tag()
            {
                if (!TagVm.HasValue)
                    return;
                var core_setting = Flux.SettingsProvider.CoreSettings.Local;
                LoadTag(core_setting.PrinterGuid, load_position, TagVm.Value.NFCSlot);
            }
            void inner_unload_tag()
            {
                if (!TagVm.HasValue)
                    return;
                var core_setting = Flux.SettingsProvider.CoreSettings.Local;
                UnloadTag(core_setting.PrinterGuid, TagVm.Value.NFCSlot);
            }
            async Task lock_tag_async()
            {
                if (!TagVm.HasValue)
                    return;
                await Flux.UseReader(TagVm.Value, (h, m, c) => m.LockTagAsync(h, c), r => r == NFCTagRW.Success);
            }
            async Task unlock_tag_async()
            {
                if (!TagVm.HasValue)
                    return;
                await Flux.UseReader(TagVm.Value, (h, m, c) => m.UnlockTagAsync(h, c), r => r == NFCTagRW.Success);
            }
        }
        private IObservable<bool> IsLocked()
        {
            var printer_guid = Flux.SettingsProvider.CoreSettings.Local.PrinterGuid;
            return this.WhenAnyValue(v => v.TagVm)
                .ConvertMany(t => t.NFCSlot.WhenAnyValue(t => t.Nfc))
                .Convert(nfc => nfc.Tag.Convert(t => t.PrinterGuid == printer_guid))
                .ValueOr(() => false);
        }
        private IObservable<bool> IsUnlocked()
        {
            return this.WhenAnyValue(v => v.TagVm)
                .ConvertMany(t => t.NFCSlot.WhenAnyValue(t => t.Nfc))
                .Convert(nfc => nfc.Tag.Convert(t => t.PrinterGuid == Guid.Empty))
                .ValueOr(() => false);
        }
        private IObservable<bool> IsLoaded()
        {
            return this.WhenAnyValue(v => v.TagVm)
                .ConvertMany(t => t.NFCSlot.WhenAnyValue(t => t.Nfc))
                .Convert(nfc => nfc.Tag.Convert(IsLoaded))
                .ValueOr(() => false);
        }
        private IObservable<bool> IsUnloaded()
        {
            return this.WhenAnyValue(v => v.TagVm)
                .ConvertMany(t => t.NFCSlot.WhenAnyValue(t => t.Nfc))
                .Convert(nfc => nfc.Tag.Convert(IsUnloaded))
                .ValueOr(() => false);
        }

        protected abstract bool IsLoaded(TNFCTag tag);
        protected abstract bool IsUnloaded(TNFCTag tag);
        public abstract NFCTagRW UnloadTag(Guid guid, INFCSlot<TNFCTag> slot);
        public abstract NFCTagRW LoadTag(Guid guid, ushort position, INFCSlot<TNFCTag> slot);
    }

    public class MaterialTagViewModel : NFCTagViewModel<MaterialTagViewModel, IFluxMaterialViewModel, NFCMaterial>
    {
        public MaterialTagViewModel(FluxViewModel flux, IObservable<Optional<IFluxMaterialViewModel>> tag_cache, ushort tag_position, ushort load_position)
            : base(flux, tag_cache, tag_position, load_position)
        {
        }

        protected override bool IsLoaded(NFCMaterial tag)
        {
            if (!tag.Inserted.HasValue)
                return false;
            if (tag.Inserted.Value != LoadPosition)
                return false;
            if (!tag.Loaded.HasValue)
                return false;
            if (tag.Loaded.Value != LoadPosition)
                return false;
            return true;
        }
        protected override bool IsUnloaded(NFCMaterial tag)
        {
            if (!tag.Inserted.HasValue)
                return true;
            if (tag.Inserted.Value != LoadPosition)
                return true;
            if (!tag.Loaded.HasValue)
                return true;
            if (tag.Loaded.Value != LoadPosition)
                return true;
            return false;
        }

        public override NFCTagRW LoadTag(Guid guid, ushort position, INFCSlot<NFCMaterial> slot)
        {
            var result = slot.StoreTag(m => m.SetInserted(guid, position));
            if (result != NFCTagRW.Success)
                return result;
            return slot.StoreTag(m => m.SetLoaded(guid, position));
        }

        public override NFCTagRW UnloadTag(Guid guid, INFCSlot<NFCMaterial> slot)
        {
            var result = slot.StoreTag(m => m.SetLoaded(guid, default));
            if (result != NFCTagRW.Success)
                return result;
            return slot.StoreTag(m => m.SetInserted(guid, default));
        }
    }

    public class ToolTagViewModel : NFCTagViewModel<ToolTagViewModel, IFluxToolNozzleViewModel, NFCToolNozzle>
    {
        public ToolTagViewModel(FluxViewModel flux, IObservable<Optional<IFluxToolNozzleViewModel>> tag_cache, ushort tag_position, ushort load_position)
            : base(flux, tag_cache, tag_position, load_position)
        {
        }
        protected override bool IsLoaded(NFCToolNozzle tag)
        {
            if (!tag.Loaded.HasValue)
                return false;
            if (tag.Loaded.Value != LoadPosition)
                return false;
            return true;
        }
        protected override bool IsUnloaded(NFCToolNozzle tag)
        {
            if (!tag.Loaded.HasValue)
                return true;
            if (tag.Loaded.Value != LoadPosition)
                return true;
            return false;
        }

        public override NFCTagRW LoadTag(Guid guid, ushort position, INFCSlot<NFCToolNozzle> slot)
        {
            return slot.StoreTag(m => m.SetLoaded(guid, position));
        }

        public override NFCTagRW UnloadTag(Guid guid, INFCSlot<NFCToolNozzle> slot)
        {
            return slot.StoreTag(m => m.SetLoaded(guid, default));
        }
    }

    public class NFCViewModel : NavPanelViewModel<NFCViewModel>
    {
        public NFCViewModel(FluxViewModel flux, string name = "") : base(flux, name)
        {
            Flux.SettingsProvider
                .WhenAnyValue(v => v.ExtrudersCount)
                .SubscribeRC(extruders =>
                {
                    Clear();
                    if (extruders.HasValue)
                    {
                        for (ushort machine_e = 0; machine_e < extruders.Value.machine_extruders; machine_e++)
                        {
                            var current_machine_e = machine_e;
                            var feeder = flux.Feeders.Feeders.WatchOptional(current_machine_e);
                            AddModal(new ToolTagViewModel(flux, feeder.Convert(f => f.ToolNozzle), current_machine_e, current_machine_e));
                        }
            
                        for (ushort mixing_e = 0; mixing_e < extruders.Value.machine_extruders * extruders.Value.mixing_extruders; mixing_e++)
                        {
                            var current_mixing_e = mixing_e;
                            var current_machine_e = (ushort)(current_mixing_e / extruders.Value.mixing_extruders);

                            var feeder = flux.Feeders.Feeders.WatchOptional(current_machine_e);
                            var material = feeder.ConvertMany(f => f.Materials.WatchOptional(current_mixing_e));

                            AddModal(new MaterialTagViewModel(flux, material, current_mixing_e, current_machine_e));
                        }
                    }
                }, this);
        }
    }

    public class ManageViewModel : NavPanelViewModel<ManageViewModel>
    {
        public ManageViewModel(FluxViewModel flux) : base(flux)
        {
            var status = Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation);
            var IS_HOME = status
                .Select(e =>
                    e.IsHomed);
            var IS_ENAB = status
                .Select(e =>
                    e.IsEnabledAxis)
                .ToOptional();
            var IS_SAFE = status
                .Select(e => e.CanSafeCycle)
                .ToOptional();
            var IS_IDLE = status
                .Select(e =>
                    e.IsIdle)
                .ToOptional();
            var IS_IH = status
                .Select(e =>
                    e.IsIdle &&
                    e.IsHomed)
                .ToOptional();
            var IS_IEH = status
                .Select(e =>
                    e.IsIdle &&
                    e.IsEnabledAxis &&
                    e.IsHomed)
                .ToOptional();
            var IS_IEHS = status
                .Select(e =>
                    e.IsIdle &&
                    e.IsEnabledAxis &&
                    e.IsHomed &&
                    e.CanSafeCycle)
                .ToOptional();

            var normal_mode = Observable.Return(true);
            var navigate_back = Observable.Return(true);

            var advanced_mode_source = Flux.MCodes.WhenAnyValue(s => s.OperatorUSB)
                .Convert(o => o.AdvancedSettings)
                .Select(o => o.ValueOrDefault().ToOptional());

            var advanced_mode = advanced_mode_source
                .ValueOr(() => false)
                .ToOptional();

            var can_naviagate_back = Flux.ConditionsProvider.ClampCondition
                .ConvertToObservable(c => c.StateChanged)
                .ConvertToObservable(s => s.Valid)
                .ObservableOr(() => true)
                .ToOptional();

            AddModal(flux, Flux.Functionality.Magazine);

            var can_shutdown = Observable.CombineLatest(
                IS_IDLE,
                Flux.ConnectionProvider.ObserveVariable(c => c.TEMP_TOOL).QueryWhenChanged(),
                Flux.ConnectionProvider.ObserveVariable(c => c.TEMP_PLATE).Convert(t => t.QueryWhenChanged()),
                Flux.ConnectionProvider.ObserveVariable(c => c.TEMP_CHAMBER).Convert(t => t.QueryWhenChanged()),
                (is_idle, temp_tool, temp_plate, temp_chamber) =>
                {
                    if (!is_idle.ValueOr(() => false))
                        return false;

                    foreach (var tool in temp_tool.Items)
                        if (tool.Value.Current > 60)
                            return false;

                    if (temp_plate.HasChange)
                        foreach (var plate in temp_plate.Change.Items)
                            if (plate.Value.Current > 60)
                                return false;

                    if (temp_chamber.HasChange)
                        foreach (var chamber in temp_chamber.Change.Items)
                            if (chamber.Value.Current > 60)
                                return false;

                    return true;
                });

            if (Flux.ConnectionProvider.VariableStoreBase.HasVariable(c => c.DISABLE_24V))
                AddCommand("shutdownPrinter", ShutdownAsync, can_execute: IS_IDLE);

            AddCommand("cleanPlate", CleanPlate);
            AddCommand("keepChamber", m => m.KEEP_CHAMBER);
            AddCommand("keepExtruders", m => m.KEEP_TOOL, visible: advanced_mode);
            AddCommand("runDaemon", m => m.RUN_DAEMON, visible: advanced_mode);
            AddCommand("plotReferenceCount", ReactiveRC.PlotReferenceCount, visible: advanced_mode);

            AddCommand(
                new ToggleButton(
                    "debug",
                    () =>
                    {
                        var operator_usb = Flux.MCodes.OperatorUSB;
                        if (operator_usb.HasValue)
                        {
                            Flux.MCodes.OperatorUSB = default;
                        }
                        else
                        {
                            Flux.MCodes.OperatorUSB = new OperatorUSB()
                            {
                                AdvancedSettings = true,
                                RewriteNFC = true
                            };
                        }
                    },
                    advanced_mode_source));


            AddModal(flux, Flux.Temperatures);          
            AddCommand("resetPrinter", Flux.ConnectionProvider.StartConnection, visible: advanced_mode);
            AddCommand("vacuumPump", m => m.ENABLE_VACUUM, can_execute: IS_IDLE, visible: advanced_mode);
            AddCommand("openClamp", m => m.OPEN_HEAD_CLAMP, can_execute: IS_IDLE, visible: advanced_mode);
            AddCommand("reloadDatabase", () => Flux.DatabaseProvider.InitializeAsync(), visible: advanced_mode);
            AddCommand("swFilamentSensor", Flux.SettingsProvider.UserSettings, s => s.PauseOnEmptyOdometer, visible: advanced_mode);

            AddModal(flux, () => new MemoryViewModel(Flux), visible: advanced_mode);
            AddModal(flux, () => new FilesViewModel(Flux), visible: advanced_mode);
            AddModal(flux, () => new MoveViewModel(Flux), visible: advanced_mode);

            AddModal(flux, () => new NFCViewModel(Flux), visible: advanced_mode);
        }

        //private byte[] array { get; set; }

        private void CleanPlate()
        {
            Flux.StatsProvider.ClearUsedPrintAreas();
        }

        private async Task ShutdownAsync()
        {
            var result = await Flux.ShowDialogAsync(f => new ConfirmDialog(f, new RemoteText("shutdownPrinter", true), new RemoteText()));
            if (result.result == DialogResult.Primary)
                await Flux.ConnectionProvider.WriteVariableAsync(m => m.DISABLE_24V, true);
        }
    }

    public class RoutinesViewModel : NavPanelViewModel<RoutinesViewModel>
    {
        public RoutinesViewModel(FluxViewModel flux) : base(flux)
        {
            var variable_store = Flux.ConnectionProvider.VariableStoreBase;

            var status = Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation);
            var IS_HOME = status
                .Select(e =>
                    e.IsHomed);
            var IS_ENAB = status
                .Select(e =>
                    e.IsEnabledAxis)
                .ToOptional();
            var IS_SAFE = status
                .Select(e => e.CanSafeCycle)
                .ToOptional();
            var IS_IDLE = status
                .Select(e =>
                    e.IsIdle)
                .ToOptional();
            var IS_IEHS = status
                .Select(e =>
                    e.IsIdle &&
                    e.IsEnabledAxis &&
                    e.IsHomed &&
                    e.CanSafeCycle)
                .ToOptional();

            var advanced_mode = Flux.MCodes
                .WhenAnyValue(s => s.OperatorUSB)
                .Select(o => o.ConvertOr(o => o.AdvancedSettings, () => false))
                .ToOptional();

            var can_maintenance = Observable.CombineLatest(
                IS_IEHS, Flux.ConnectionProvider.ObserveVariable(c => c.TOOL_CUR),
                (is_iehs, cur_tool) => is_iehs.HasChange && is_iehs.Change && cur_tool.HasValue && cur_tool.Value.GetZeroBaseIndex() > -1)
                .ToOptional();

            Flux.SettingsProvider
                .WhenAnyValue(v => v.ExtrudersCount)
                .SubscribeRC(extruders =>
                {
                    Clear();

                    if (variable_store.CanMeshProbePlate)
                        AddModal(flux, () => new HeightmapViewModel(Flux));

                    AddCommand(
                        "maintenancePosition",
                        () => Flux.ConnectionProvider.GotoMaintenancePosition(),
                        can_execute: can_maintenance); 
                    
                    AddCommand(
                        "homePrinter",
                        () => Flux.ConnectionProvider.HomeAsync(true),
                        can_execute: IS_IEHS,
                        visible: advanced_mode);

                    AddCommand(
                        "stopOperation",
                        async () => await Flux.ConnectionProvider.StopAsync(),
                        can_execute: Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation).Select(s => s.CanSafeStop).ToOptional());

                    AddCommand(
                        "lowerPlate",
                        Flux.ConnectionProvider.LowerPlateAsync,
                        can_execute: IS_IEHS);

                    AddCommand(
                        "raisePlate",
                        Flux.ConnectionProvider.RaisePlateAsync,
                        can_execute: IS_IEHS);

                    AddCommand(
                        "parkTool",
                        Flux.ConnectionProvider.ParkToolAsync,
                        can_execute: IS_IEHS);

                    if (extruders.HasValue)
                    {
                        for (var extruder = ArrayIndex.FromZeroBase(0, variable_store); extruder.GetZeroBaseIndex() < extruders.Value.machine_extruders; extruder++)
                        {
                            var extr = extruder;
                            AddCommand(
                                $"selectTool;{extr.GetZeroBaseIndex() + 1}",
                                () => Flux.ConnectionProvider.SelectToolAsync(extr),
                                can_execute: IS_IEHS);
                        }
                    }

                    if (extruders.HasValue)
                    {
                        if (variable_store.HasToolChange && variable_store.CanProbeMagazine)
                        {
                            for (var extruder = ArrayIndex.FromZeroBase(0, variable_store); extruder.GetZeroBaseIndex() < extruders.Value.machine_extruders; extruder++)
                            {
                                var extr = extruder;
                                AddCommand(
                                    $"probeMagazine;{extr.GetZeroBaseIndex() + 1}",
                                    () => Flux.ConnectionProvider.ProbeMagazineAsync(extr),
                                    can_execute: IS_IEHS,
                                    visible: advanced_mode);
                            }
                        }
                    }
                }, this);
        }
    }

    public class FunctionalityViewModel : NavPanelViewModel<FunctionalityViewModel>
    {
        public Lazy<ManageViewModel> Manage { get; private set; }
        public Lazy<MagazineViewModel> Magazine { get; private set; }
        public Lazy<RoutinesViewModel> Routines { get; private set; }
        public Lazy<SettingsViewModel> Settings { get; private set; }
        public FunctionalityViewModel(FluxViewModel flux) : base(flux)
        {
            Magazine = new Lazy<MagazineViewModel>(() => new MagazineViewModel(flux));

            var advanced_mode = Flux.MCodes
                .WhenAnyValue(s => s.OperatorUSB)
                .Select(o => o.ConvertOr(o => o.AdvancedSettings, () => false))
                .ToOptional();

            Manage = AddModal(flux, () => new ManageViewModel(flux));
            Settings = AddModal(flux, () => new SettingsViewModel(flux));
            Routines = AddModal(flux, () => new RoutinesViewModel(flux));
        }
    }
}
