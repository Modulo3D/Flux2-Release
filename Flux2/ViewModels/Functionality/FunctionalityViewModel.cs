using DynamicData;
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
        public NFCInnerViewModel(FluxViewModel flux, string name, IFluxTagViewModel tag_vm, ushort load_position) : base(flux, $"{name.ToCamelCase()}??{tag_vm.Position + 1}")
        {
            var can_lock = Observable.CombineLatest(
                is_unlocked_tag(),
                is_unloaded_tag(),
                (l, un) => l && un).ToOptional();
            AddCommand($"lock{name}Tag??{tag_vm.Position + 1}", lock_tag_async, can_lock);

            var can_unlock = Observable.CombineLatest(
                is_locked_tag(),
                is_unloaded_tag(),
                (l, un) => l && un).ToOptional();
            AddCommand($"unlock{name}Tag??{tag_vm.Position + 1}", unlock_tag_async, can_unlock);

            var can_load = Observable.CombineLatest(
                is_locked_tag(),
                is_unloaded_tag(),
                (l, un) => l && un).ToOptional();
            AddCommand($"load{name}Tag??{tag_vm.Position + 1}", load_tag, can_load);

            var can_unload = Observable.CombineLatest(
                is_locked_tag(),
                is_loaded_tag(),
                (l, lo) => l && lo).ToOptional();
            AddCommand($"unload{name}Tag??{tag_vm.Position + 1}", unload_tag, can_unload);

            void load_tag()
            {
                tag_vm.NFCSlot.StoreTag(t => t.SetLoaded(load_position));
            }
            void unload_tag()
            {
                tag_vm.NFCSlot.StoreTag(t => t.SetLoaded(default));
            }
            async Task lock_tag_async()
            {
                await Flux.UseReader(tag_vm, (h, m) => m.LockTagAsync(h));
            }
            async Task unlock_tag_async()
            {
                await Flux.UseReader(tag_vm, (h, m) => m.UnlockTagAsync(h));
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

    public class NFCInnerViewModel<TTagViewModel> : NavPanelViewModel<NFCInnerViewModel<TTagViewModel>>
        where TTagViewModel : IFluxTagViewModel
    {
        public NFCInnerViewModel(FluxViewModel flux, string name, IObservableCache<TTagViewModel, ushort> tag_cache, ushort tag_position, ushort load_position) : base(flux, $"{name.ToCamelCase()}??{tag_position + 1}")
        {
            var tag_vm = tag_cache.Lookup(tag_position);

            var can_lock = Observable.CombineLatest(
                is_unlocked_tag(),
                is_unloaded_tag(),
                (l, un) => l && un).ToOptional();
            AddCommand($"lock{name}Tag??{tag_position + 1}", lock_tag_async, can_lock);

            var can_unlock = Observable.CombineLatest(
                is_locked_tag(),
                is_unloaded_tag(),
                (l, un) => l && un).ToOptional();
            AddCommand($"unlock{name}Tag??{tag_position + 1}", unlock_tag_async, can_unlock);

            var can_load = Observable.CombineLatest(
                is_locked_tag(),
                has_loaded_tag(),
                is_unloaded_tag(),
                (lk, lo, un) => lk && !lo && un).ToOptional();
            AddCommand($"load{name}Tag??{tag_position + 1}", load_tag, can_load);

            var can_unload = Observable.CombineLatest(
                is_locked_tag(),
                is_loaded_tag(),
                (l, lo) => l && lo).ToOptional();
            AddCommand($"unload{name}Tag??{tag_position + 1}", unload_tag, can_unload);

            void load_tag()
            {
                if (!tag_vm.HasValue)
                    return;
                tag_vm.Value.NFCSlot.StoreTag(t => t.SetLoaded(load_position));
            }
            void unload_tag()
            {
                if (!tag_vm.HasValue)
                    return;
                tag_vm.Value.NFCSlot.StoreTag(t => t.SetLoaded(default));
            }
            async Task lock_tag_async()
            {
                if (!tag_vm.HasValue)
                    return;
                await Flux.UseReader(tag_vm.Value, (h, m) => m.LockTagAsync(h));
            }
            async Task unlock_tag_async()
            {
                if (!tag_vm.HasValue)
                    return;
                await Flux.UseReader(tag_vm.Value, (h, m) => m.UnlockTagAsync(h));
            }
            IObservable<bool> is_loaded_tag()
            {
                return tag_cache.Connect()
                    .WatchOptional(tag_position)
                    .ConvertMany(t => t.NFCSlot.WhenAnyValue(t => t.Nfc))
                    .Convert(nfc => nfc.Tag.Convert(t => t.Loaded.HasValue && t.Loaded.Value == load_position))
                    .ValueOr(() => false);
            }
            IObservable<bool> has_loaded_tag()
            {
                return tag_cache.Connect()
                    .TrueForAny(f => f.NFCSlot.WhenAnyValue(f => f.Nfc), nfc => nfc.Tag.ConvertOr(t => t.Loaded.HasValue, () => false));
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
                .Subscribe(extruders =>
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
                            AddModal(new NFCInnerViewModel(flux, "Tool", feeder.Value.ToolNozzle, current_machine_e));

                            foreach (var material in feeder.Value.Materials.Keys)
                                AddModal(new NFCInnerViewModel<IFluxMaterialViewModel>(flux, "Material", feeder.Value.Materials, material, current_machine_e));
                        }
                    }
                });
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
                AddCommand("power", ShutdownAsync, can_execute: IS_IDLE);

            AddCommand("cleanPlate", CleanPlate);
            AddCommand("keepChamber", m => m.KEEP_CHAMBER);
            AddCommand("keepExtruders", m => m.KEEP_TOOL, visible: advanced_mode);
            AddCommand("runDaemon", m => m.RUN_DAEMON, visible: advanced_mode);

            var user_settings = Flux.SettingsProvider.UserSettings;
            AddCommand(
                new ToggleButton(
                    "deleteUsb",
                    () =>
                    {
                        var delete = !user_settings.Local.DeleteFromUSB.ValueOr(() => false);
                        user_settings.Local.DeleteFromUSB = delete;
                        user_settings.PersistLocalSettings();
                    },
                    user_settings.Local.WhenAnyValue(s => s.DeleteFromUSB)));

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
                        user_settings.PersistLocalSettings();
                    },
                    advanced_mode_source));


            AddModal(Flux.Temperatures);
            AddCommand("resetPrinter", Flux.ConnectionProvider.StartConnection, visible: advanced_mode);
            AddCommand("vacuumPump", m => m.ENABLE_VACUUM, can_execute: IS_IDLE, visible: advanced_mode);
            AddCommand("openClamp", m => m.OPEN_HEAD_CLAMP, can_execute: IS_IDLE, visible: advanced_mode);
            AddCommand("reloadDatabase", () => Flux.DatabaseProvider.Initialize(), visible: advanced_mode);
            AddCommand("swFilamentSensor", Flux.SettingsProvider.UserSettings, s => s.PauseOnEmptyOdometer, visible: advanced_mode);

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
                .Subscribe(extruders =>
                {
                    Clear();

                    if (Flux.ConnectionProvider.VariableStoreBase.CanMeshProbePlate)
                        AddModal(() => new HeightmapViewModel(Flux));

                    AddCommand(
                        "homePrinter",
                        () => Flux.ConnectionProvider.HomeAsync(),
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

                    var variable_store = Flux.ConnectionProvider.VariableStoreBase;
                    if (extruders.HasValue)
                    {
                        for (var extruder = ArrayIndex.FromZeroBase(0, variable_store); extruder.GetZeroBaseIndex() < extruders.Value.machine_extruders; extruder++)
                        {
                            var extr = extruder;
                            AddCommand(
                                $"selectExtruder??{extr.GetZeroBaseIndex() + 1}",
                                () => Flux.ConnectionProvider.SelectToolAsync(extr),
                                can_execute: IS_IEHS);
                        }
                    }

                    if (extruders.HasValue)
                    {
                        if (Flux.ConnectionProvider.VariableStoreBase.HasToolChange)
                        {
                            for (var extruder = ArrayIndex.FromZeroBase(0, variable_store); extruder.GetZeroBaseIndex() < extruders.Value.machine_extruders; extruder++)
                            {
                                var extr = extruder;
                                AddCommand(
                                    $"probeMagazine??{extr.GetZeroBaseIndex() + 1}",
                                    () => Flux.ConnectionProvider.ProbeMagazineAsync(extr),
                                    can_execute: IS_IEHS,
                                    visible: advanced_mode);
                            }
                        }
                    }
                });
        }
    }

    public class FunctionalityViewModel : NavPanelViewModel<FunctionalityViewModel>
    {
        public Lazy<MagazineViewModel> Magazine { get; private set; }
        public FunctionalityViewModel(FluxViewModel flux) : base(flux)
        {
            Magazine = new Lazy<MagazineViewModel>(() => new MagazineViewModel(flux));

            var advanced_mode = Flux.MCodes
                .WhenAnyValue(s => s.OperatorUSB)
                .Select(o => o.ConvertOr(o => o.AdvancedSettings, () => false))
                .ToOptional();

            AddModal(() => new ManageViewModel(Flux));
            AddModal(() => new SettingsViewModel(Flux));
            AddModal(() => new RoutinesViewModel(Flux));
        }
    }
}
