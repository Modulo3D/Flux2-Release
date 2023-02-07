﻿using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class NFCInnerViewModel : NavPanelViewModel<NFCInnerViewModel>
    {      
        //private ObservableAsPropertyHelper<string> _SerializedTag;
        //public string SerializedTag => _SerializedTag.Value;

        public NFCInnerViewModel(FluxViewModel flux, string name, IFluxTagViewModel tag_vm, ushort load_position) : base(flux, $"{name.ToCamelCase()}??{tag_vm.Position + 1}")
        {
            /*_SerializedTag = tag_vm.NFCSlot.WhenAnyValue(s => s.Nfc)
                .Select(nfc => JsonUtils.Serialize(nfc))
                .ToPropertyRC(this, v => v.SerializedTag);*/

            var can_lock = Observable.CombineLatest(
                is_unlocked_tag(),
                is_unloaded_tag(),
                (l, un) => l && un).ToOptional();
            AddCommand($"nfc.lockTag??{tag_vm.Position + 1}", lock_tag_async, can_lock);

            var can_unlock = Observable.CombineLatest(
                is_locked_tag(),
                is_unloaded_tag(),
                (l, un) => l && un).ToOptional();
            AddCommand($"nfc.unlockTag??{tag_vm.Position + 1}", unlock_tag_async, can_unlock);

            var can_load = Observable.CombineLatest(
                is_locked_tag(),
                is_unloaded_tag(),
                (l, un) => l && un).ToOptional();
            AddCommand($"nfc.loadTag??{tag_vm.Position + 1}", load_tag, can_load);

            var can_unload = Observable.CombineLatest(
                is_locked_tag(),
                is_loaded_tag(),
                (l, lo) => l && lo).ToOptional();
            AddCommand($"nfc.unloadTag??{tag_vm.Position + 1}", unload_tag, can_unload);

            void load_tag()
            {
                var core_setting = Flux.SettingsProvider.CoreSettings.Local;
                var result = tag_vm.NFCSlot.StoreTag(t => t.SetLoaded(core_setting.PrinterGuid, load_position));
                Console.WriteLine(result);
            }
            void unload_tag()
            {
                var core_setting = Flux.SettingsProvider.CoreSettings.Local;
                var result = tag_vm.NFCSlot.StoreTag(t => t.SetLoaded(core_setting.PrinterGuid, default));
                Console.WriteLine(result);
            }
            async Task lock_tag_async()
            {
                var result = await Flux.UseReader(tag_vm, (h, m, c) => m.LockTagAsync(h, c), r => r == NFCTagRW.Success);
                Console.WriteLine(result);
            }
            async Task unlock_tag_async()
            {
                var result = await Flux.UseReader(tag_vm, (h, m, c) => m.UnlockTagAsync(h, c), r => r == NFCTagRW.Success);
                Console.WriteLine(result);
            }
            IObservable<bool> is_locked_tag()
            {
                var printer_guid = Flux.SettingsProvider.CoreSettings.Local.PrinterGuid;
                return tag_vm.NFCSlot.WhenAnyValue(t => t.Nfc)
                    .Select(nfc => nfc.Tag.Convert(t => t.PrinterGuid == printer_guid))
                    .ValueOr(() => false);
            }
            IObservable<bool> is_unlocked_tag()
            {
                return tag_vm.NFCSlot.WhenAnyValue(t => t.Nfc)
                   .Select(nfc => nfc.Tag.Convert(t => t.PrinterGuid == Guid.Empty))
                   .ValueOr(() => false);
            }
            IObservable<bool> is_unloaded_tag()
            {
                return tag_vm.NFCSlot.WhenAnyValue(t => t.Nfc)
                    .Select(nfc => nfc.Tag.Convert(t => !t.Loaded.HasValue))
                    .ValueOr(() => false);
            }
            IObservable<bool> is_loaded_tag()
            {
                return tag_vm.NFCSlot.WhenAnyValue(t => t.Nfc)
                    .Select(nfc => nfc.Tag.Convert(t => t.Loaded.HasValue && t.Loaded.Value == load_position))
                    .ValueOr(() => false);
            }
        }
    }

    public class NFCInnerViewModel<TTagViewModel, TNFCTag> : NavPanelViewModel<NFCInnerViewModel<TTagViewModel, TNFCTag>>
        where TTagViewModel : IFluxTagViewModel<TNFCTag>
        where TNFCTag : INFCOdometerTag<TNFCTag>, new()
    {
        //private ObservableAsPropertyHelper<Optional<string>> _SerializedTag;
        //public Optional<string> SerializedTag => _SerializedTag.Value;

        public NFCInnerViewModel(FluxViewModel flux, string name, 
            IObservableCache<TTagViewModel, ushort> tag_cache, 
            ushort tag_position, ushort load_position,
            Func<Guid, ushort, INFCSlot<TNFCTag>, NFCTagRW> load_tag, 
            Func<Guid, ushort, INFCSlot<TNFCTag>, NFCTagRW> unload_tag) : base(flux, $"{name.ToCamelCase()}??{tag_position + 1}")
        {
            var tag_vm = tag_cache.Lookup(tag_position);

            /*_SerializedTag = tag_vm
                .Convert(vm => vm.NFCSlot)
                .ConvertToObservable(slot => slot.WhenAnyValue(s => s.Nfc))
                .ConvertToObservable(nfc => JsonUtils.Serialize(nfc))
                .ObservableOrOptional()
                .ToPropertyRC(this, v => v.SerializedTag);*/

            var can_lock = Observable.CombineLatest(
                is_unlocked_tag(),
                is_unloaded_tag(),
                (l, un) => l && un).ToOptional();
            AddCommand($"nfc.lockTag??{tag_position + 1}", lock_tag_async, can_lock);

            var can_unlock = Observable.CombineLatest(
                is_locked_tag(),
                is_unloaded_tag(),
                (l, un) => l && un).ToOptional();
            AddCommand($"nfc.unlockTag??{tag_position + 1}", unlock_tag_async, can_unlock);

            var can_load = Observable.CombineLatest(
                is_locked_tag(),
                is_unloaded_tag(),
                (lk, un) => lk && un).ToOptional();
            AddCommand($"nfc.loadTag??{tag_position + 1}", inner_load_tag, can_load);

            var can_unload = Observable.CombineLatest(
                is_locked_tag(),
                is_loaded_tag(),
                (l, lo) => l && lo).ToOptional();
            AddCommand($"nfc.unloadTag??{tag_position + 1}", inner_unload_tag, can_unload);

            void inner_load_tag()
            {
                if (!tag_vm.HasValue)
                    return; 
                var core_setting = Flux.SettingsProvider.CoreSettings.Local;

                Console.WriteLine(load_tag(core_setting.PrinterGuid, load_position, tag_vm.Value.NFCSlot));
                Console.WriteLine(tag_vm.Value.NFCSlot.StoreTag(t => t.SetLoaded(core_setting.PrinterGuid, load_position)));
            }
            void inner_unload_tag()
            {
                if (!tag_vm.HasValue)
                    return;
                var core_setting = Flux.SettingsProvider.CoreSettings.Local;

                Console.WriteLine(unload_tag(core_setting.PrinterGuid, load_position, tag_vm.Value.NFCSlot));
                Console.WriteLine(tag_vm.Value.NFCSlot.StoreTag(t => t.SetLoaded(core_setting.PrinterGuid, default)));
            }
            async Task lock_tag_async()
            {
                if (!tag_vm.HasValue)
                    return;
                var result = await Flux.UseReader(tag_vm.Value, (h, m, c) => m.LockTagAsync(h, c), r => r == NFCTagRW.Success);
                Console.WriteLine(result);
            }
            async Task unlock_tag_async()
            {
                if (!tag_vm.HasValue)
                    return;
                var result = await Flux.UseReader(tag_vm.Value, (h, m, c) => m.UnlockTagAsync(h, c), r => r == NFCTagRW.Success);
                Console.WriteLine(result);
            }
            IObservable<bool> is_loaded_tag()
            {
                return tag_cache.Connect()
                    .WatchOptional(tag_position)
                    .ConvertMany(t => t.NFCSlot.WhenAnyValue(t => t.Nfc))
                    .Convert(nfc => nfc.Tag.Convert(t => t.Loaded.HasValue && t.Loaded.Value == load_position))
                    .ValueOr(() => false);
            }
            IObservable<bool> is_locked_tag()
            {
                var printer_guid = Flux.SettingsProvider.CoreSettings.Local.PrinterGuid;
                return tag_cache.Connect()
                    .WatchOptional(tag_position)
                    .ConvertMany(t => t.NFCSlot.WhenAnyValue(t => t.Nfc))
                    .Convert(nfc => nfc.Tag.Convert(t => t.PrinterGuid == printer_guid))
                    .ValueOr(() => false);
            }
            IObservable<bool> is_unlocked_tag()
            {
                return tag_cache.Connect()
                   .WatchOptional(tag_position)
                   .ConvertMany(t => t.NFCSlot.WhenAnyValue(t => t.Nfc))
                   .Convert(nfc => nfc.Tag.Convert(t => t.PrinterGuid == Guid.Empty))
                   .ValueOr(() => false);
            }
            IObservable<bool> is_unloaded_tag()
            {
                return tag_cache.Connect()
                    .WatchOptional(tag_position)
                    .ConvertMany(t => t.NFCSlot.WhenAnyValue(t => t.Nfc))
                    .Convert(nfc => nfc.Tag.Convert(t => !t.Loaded.HasValue))
                    .ValueOr(() => false);
            }
        }
    }

    public class NFCViewModel : NavPanelViewModel<NFCViewModel>
    {
        public NFCViewModel(FluxViewModel flux) : base(flux)
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
                            var feeder = flux.Feeders.Feeders.Lookup(current_machine_e);
                            if (!feeder.HasValue)
                                continue;
                            AddModal(new NFCInnerViewModel(flux, "tool.tool", feeder.Value.ToolNozzle, current_machine_e));

                            foreach (var material in feeder.Value.Materials.Keys)
                                AddModal(new NFCInnerViewModel<IFluxMaterialViewModel, NFCMaterial>(
                                    flux, "material.material", feeder.Value.Materials, material, current_machine_e,
                                    (g, e, s) => s.StoreTag(t => t.SetInserted(g, e)),
                                    (g, e, s) => s.StoreTag(t => t.SetInserted(g, default))));
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

            var can_naviagate_back = Flux.StatusProvider.ClampCondition
                .ConvertToObservable(c => c.StateChanged)
                .ConvertToObservable(s => s.Valid)
                .ObservableOr(() => true)
                .ToOptional();

            if (Flux.ConnectionProvider.VariableStoreBase.HasToolChange)
                AddModal(Flux.Functionality.Magazine);

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
                AddCommand("manage.power", ShutdownAsync, can_execute: IS_IDLE);

            AddCommand("manage.cleanPlate", CleanPlate);
            AddCommand("manage.keepChamber", m => m.KEEP_CHAMBER);
            AddCommand("manage.keepExtruders", m => m.KEEP_TOOL, visible: advanced_mode);
            AddCommand("manage.runDaemon", m => m.RUN_DAEMON, visible: advanced_mode);
            AddCommand("manage.plotReferenceCount", () => ReactiveRC.PlotReferenceCount(), visible: advanced_mode);

            AddCommand(
                new ToggleButton(
                    "manage.debug",
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


            AddModal(Flux.Temperatures);
            AddCommand("manage.resetPrinter", Flux.ConnectionProvider.StartConnection, visible: advanced_mode);
            AddCommand("manage.vacuumPump", m => m.ENABLE_VACUUM, can_execute: IS_IDLE, visible: advanced_mode);
            AddCommand("manage.openClamp", m => m.OPEN_HEAD_CLAMP, can_execute: IS_IDLE, visible: advanced_mode);
            AddCommand("manage.reloadDatabase", () => Flux.DatabaseProvider.InitializeAsync(), visible: advanced_mode);
            AddCommand("manage.swFilamentSensor", Flux.SettingsProvider.UserSettings, s => s.PauseOnEmptyOdometer, visible: advanced_mode);

            AddModal(() => new MemoryViewModel(Flux), visible: advanced_mode);
            AddModal(() => new FilesViewModel(Flux), visible: advanced_mode);
            AddModal(() => new MoveViewModel(Flux), visible: advanced_mode);

            AddModal(() => new NFCViewModel(Flux), visible: advanced_mode);
        }

        //private byte[] array { get; set; }

        private void CleanPlate()
        {
            Flux.StatsProvider.ClearUsedPrintAreas();
        }

        private async Task ShutdownAsync()
        {
            var result = await Flux.ShowConfirmDialogAsync("ATTENZIONE", "LA STAMPANTE VERRA' SPENTA");
            if (result == ContentDialogResult.Primary)
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

            Flux.SettingsProvider
                .WhenAnyValue(v => v.ExtrudersCount)
                .SubscribeRC(extruders =>
                {
                    Clear();

                    if (variable_store.CanMeshProbePlate)
                        AddModal(() => new HeightmapViewModel(Flux));

                    AddCommand(
                        "routines.homePrinter",
                        () => Flux.ConnectionProvider.HomeAsync(),
                        can_execute: IS_IEHS,
                        visible: advanced_mode);

                    AddCommand(
                        "routines.stopOperation",
                        async () => await Flux.ConnectionProvider.StopAsync(),
                        can_execute: Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation).Select(s => s.CanSafeStop).ToOptional());

                    AddCommand(
                        "routines.lowerPlate",
                        Flux.ConnectionProvider.LowerPlateAsync,
                        can_execute: IS_IEHS);

                    AddCommand(
                        "routines.raisePlate",
                        Flux.ConnectionProvider.RaisePlateAsync,
                        can_execute: IS_IEHS);

                    AddCommand(
                        "routines.parkTool",
                        Flux.ConnectionProvider.ParkToolAsync,
                        can_execute: IS_IEHS);

                    if (extruders.HasValue)
                    {
                        for (var extruder = ArrayIndex.FromZeroBase(0, variable_store); extruder.GetZeroBaseIndex() < extruders.Value.machine_extruders; extruder++)
                        {
                            var extr = extruder;
                            AddCommand(
                                $"routines.selectExtruder??{extr.GetZeroBaseIndex() + 1}",
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
                                    $"routines.probeMagazine??{extr.GetZeroBaseIndex() + 1}",
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

            Manage = AddModal(() => new ManageViewModel(flux));
            Settings = AddModal(() => new SettingsViewModel(flux));
            Routines = AddModal(() => new RoutinesViewModel(flux));
        }
    }
}
