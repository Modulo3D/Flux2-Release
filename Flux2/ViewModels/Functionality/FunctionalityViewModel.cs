using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class NFCInnerViewModel : NavPanelViewModel<NFCInnerViewModel>
    {
        public NFCInnerViewModel(string name, IFluxTagViewModel tag_vm) : base((FluxViewModel)tag_vm.Feeder.Flux, $"{name.ToCamelCase()}??{tag_vm.Position + 1}")
        {
            var can_lock = Observable.CombineLatest(
                is_unlocked_tag(tag_vm),
                is_unloaded_tag(tag_vm),
                (l, un) => l && un).ToOptional();
            AddCommand($"lock{name}Tag??{tag_vm.Position + 1}", () => lock_tag_async(tag_vm), can_lock);

            var can_unlock = Observable.CombineLatest(
                is_locked_tag(tag_vm),
                is_unloaded_tag(tag_vm),
                (l, un) => l && un).ToOptional();
            AddCommand($"unlock{name}Tag??{tag_vm.Position + 1}", () => unlock_tag_async(tag_vm), can_unlock);

            var can_load = Observable.CombineLatest(
                is_locked_tag(tag_vm),
                is_unloaded_tag(tag_vm),
                (l, un) => l && un).ToOptional();
            AddCommand($"load{name}Tag??{tag_vm.Position + 1}", () => load_tag(tag_vm), can_load);

            var can_unload = Observable.CombineLatest(
                is_locked_tag(tag_vm),
                is_loaded_tag(tag_vm),
                (l, lo) => l && lo).ToOptional();
            AddCommand($"unload{name}Tag??{tag_vm.Position + 1}", () =>  unload_tag(tag_vm), can_unload);
        }
        private void load_tag(IFluxTagViewModel tag_vm)
        {
            tag_vm.StoreTag(t => t.SetLoaded(tag_vm.Position));
        }
        private void unload_tag(IFluxTagViewModel tag_vm)
        {
            tag_vm.StoreTag(t => t.SetLoaded(default));
        }
        private async Task lock_tag_async(IFluxTagViewModel tag_vm)
        {
            await tag_vm.LockTagAsync(default);
        }
        private async Task unlock_tag_async(IFluxTagViewModel tag_vm)
        {
            await tag_vm.UnlockTagAsync(default);
        }
        private IObservable<bool> is_locked_tag(IFluxTagViewModel tag_vm)
        {
            var printer_guid = Flux.SettingsProvider.CoreSettings.Local.PrinterGuid;
            return tag_vm.WhenAnyValue(t => t.Nfc)
                .Select(nfc => nfc.Tag.Convert(t => t.PrinterGuid == printer_guid))
                .ValueOr(() => false);
        }
        private IObservable<bool> is_unlocked_tag(IFluxTagViewModel tag_vm)
        {
            return tag_vm.WhenAnyValue(t => t.Nfc)
                .Select(nfc => nfc.Tag.Convert(t => t.PrinterGuid == Guid.Empty))
                .ValueOr(() => false);
        }
        private IObservable<bool> is_unloaded_tag(IFluxTagViewModel tag_vm)
        {
            return tag_vm.WhenAnyValue(t => t.Nfc)
                .Select(nfc => nfc.Tag.Convert(t => !t.Loaded.HasValue))
                .ValueOr(() => false);
        }
        private IObservable<bool> is_loaded_tag(IFluxTagViewModel tag_vm)
        {
            return tag_vm.WhenAnyValue(t => t.Nfc)
                .Select(nfc => nfc.Tag.Convert(t => t.Loaded.HasValue && t.Loaded.Value == tag_vm.Position))
                .ValueOr(() => false);
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
                        for (ushort extr = 0; extr < extruders.Value; extr++)
                        {
                            var feeder = flux.Feeders.Feeders.Lookup(extr);
                            if (!feeder.HasValue)
                                continue;

                            AddModal(new NFCInnerViewModel("Tool", feeder.Value.ToolNozzle));
                            foreach (var material in feeder.Value.Materials.Items)
                                AddModal(new NFCInnerViewModel("Material", material));
                        }
                    }
                });
        }
    }

    public class ManageViewModel : NavPanelViewModel<ManageViewModel>
    {
        public MoveViewModel Movement { get; }
        public MemoryViewModel Memory { get; }
        public FilesViewModel Files { get; }
        public TemperaturesViewModel Temperatures { get; }

        public ManageViewModel(FluxViewModel flux) : base(flux)
        {
            Movement = new MoveViewModel(Flux);
            Memory = new MemoryViewModel(Flux);
            Files = new FilesViewModel(Flux);
            Temperatures = new TemperaturesViewModel(Flux);

            var status = Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation);
            var IS_HOME = status
                .Select(e =>
                    e.IsHomed.ValueOrDefault());
            var IS_ENAB = status
                .Select(e =>
                    e.IsEnabledAxis.ValueOrDefault())
                .ToOptional();
            var IS_SAFE = status
                .Select(e => e.CanSafeCycle)
                .ToOptional();
            var IS_IDLE = status
                .Select(e =>
                    e.IsIdle.ValueOrDefault())
                .ToOptional();
            var IS_IH = status
                .Select(e =>
                    e.IsIdle.ValueOrDefault() &&
                    e.IsHomed.ValueOrDefault())
                .ToOptional();
            var IS_IEH = status
                .Select(e =>
                    e.IsIdle.ValueOrDefault() &&
                    e.IsEnabledAxis.ValueOrDefault() &&
                    e.IsHomed.ValueOrDefault())
                .ToOptional();
            var IS_IEHS = status
                .Select(e =>
                    e.IsIdle.ValueOrDefault() &&
                    e.IsEnabledAxis.ValueOrDefault() &&
                    e.IsHomed.ValueOrDefault() &&
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

            var can_naviagate_back = Flux.StatusProvider.ClampClosed.IsValidChanged
                .ValueOr(() => true)
                .ToOptional();

            AddModal(
                Flux.Magazine,
                can_navigate: IS_IDLE,
                navigate_back: can_naviagate_back);

            if (Flux.ConnectionProvider.VariableStore.HasVariable(c => c.DISABLE_24V))
                AddCommand("power", ShutdownAsync, can_execute: IS_IDLE);

            AddCommand("cleanPlate", CleanPlate);
            /*AddCommand("keepChamber", m => m.KEEP_CHAMBER);
            AddCommand("keepExtruders", m => m.KEEP_TOOL, visible: advanced_mode);*/

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
                                RemoteOperation = true,
                                RewriteNFC = true
                            };
                        }
                        user_settings.PersistLocalSettings();
                    },
                    advanced_mode_source));

            AddModal(Temperatures);
            AddCommand("reloadDatabase", () => Flux.DatabaseProvider.Initialize(), can_execute: IS_IDLE, visible: advanced_mode);
            AddCommand("resetPrinter", Flux.Startup.ResetPrinter, visible: advanced_mode);
            AddCommand("vacuumPump", m => m.VariableStore.ENABLE_VACUUM, can_execute: IS_IDLE, visible: advanced_mode);
            AddCommand("openClamp", m => m.VariableStore.OPEN_HEAD_CLAMP, can_execute: IS_IDLE, visible: advanced_mode);
            //AddCommand("createArray", () => array = new byte[1024 * 1024 * 200]);

            AddModal(Movement, visible: advanced_mode);
            AddModal(Memory, visible: advanced_mode);
            AddModal(Files, visible: advanced_mode);
        }

        //private byte[] array { get; set; }

        private void CleanPlate()
        {
            Flux.StatsProvider.ClearUsedPrintAreas();
        }

        public async Task ShutdownAsync()
        {
            foreach (var feeder in Flux.Feeders.Feeders.Items)
            {
                if (feeder.ToolNozzle.Temperature.ConvertOr(t => t.Current > 60, () => false))
                {
                    Flux.Messages.LogMessage("Estrusori caldi", "Aspetta che tutti gli estrusori si siano raffreddati", MessageLevel.ERROR, 100);
                    return;
                }
            }
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
                    e.IsHomed.ValueOrDefault());
            var IS_ENAB = status
                .Select(e =>
                    e.IsEnabledAxis.ValueOrDefault())
                .ToOptional();
            var IS_SAFE = status
                .Select(e => e.CanSafeCycle)
                .ToOptional();
            var IS_IDLE = status
                .Select(e =>
                    e.IsIdle.ValueOrDefault())
                .ToOptional();
            var IS_IEHS = status
                .Select(e =>
                    e.IsIdle.ValueOrDefault() &&
                    e.IsEnabledAxis.ValueOrDefault() &&
                    e.IsHomed.ValueOrDefault() &&
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

                    AddCommand(
                        "homePrinter",
                        () => Flux.ConnectionProvider.HomeAsync(),
                        can_execute: IS_IEHS,
                        visible: advanced_mode);

                    AddCommand(
                        "stopOperation",
                        async () => await Flux.ConnectionProvider.CancelPrintAsync(false),
                        can_execute: Flux.StatusProvider.CanSafeStop.ToOptional());

                    AddCommand(
                        "lowerPlate",
                        Flux.ConnectionProvider.LowerPlateAsync,
                        can_execute: IS_IEHS);

                    AddCommand(
                        "raisePlate",
                        Flux.ConnectionProvider.RaisePlateAsync,
                        can_execute: IS_IEHS);

                    AddCommand(
                         "setLowCurrentAsync",
                         Flux.ConnectionProvider.SetLowCurrentAsync,
                         can_execute: IS_IEHS);

                    AddCommand(
                        "setMagazineLimits",
                        () =>
                        {
                            using var put_mag_limits_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                            Flux.ConnectionProvider.ExecuteParamacroAsync(m => new[] { "M98 P\"/macros/magazine_limits\"" }, put_mag_limits_cts.Token);
                        },
                        can_execute: IS_IEHS,
                        visible: advanced_mode);

                    AddCommand(
                        "parkTool",
                        Flux.ConnectionProvider.ParkToolAsync,
                        can_execute: IS_IEHS,
                        visible: advanced_mode);

                    if (extruders.HasValue)
                    {
                        for (ushort extruder = 0; extruder < extruders.Value; extruder++)
                        {
                            var extr = extruder;
                            AddCommand(
                                $"selectExtruder??{extr + 1}",
                                () => Flux.ConnectionProvider.SelectToolAsync(extr),
                                can_execute: IS_IEHS,
                                visible: advanced_mode);
                        }
                    }

                    if (extruders.HasValue)
                    {
                        for (ushort extruder = 0; extruder < extruders.Value; extruder++)
                        {
                            var extr = extruder;
                            AddCommand(
                                $"probeMagazine??{extr + 1}",
                                () => Flux.ConnectionProvider.ProbeMagazineAsync(extr),
                                can_execute: IS_IEHS,
                                visible: advanced_mode);
                        }
                    }
                });
        }
    }

    public class FunctionalityViewModel : NavPanelViewModel<FunctionalityViewModel>
    {
        public NFCViewModel NFC { get; private set; }
        public ManageViewModel Manage { get; private set; }
        public RoutinesViewModel Routines { get; private set; }
        public SettingsViewModel Settings { get; private set; }

        public FunctionalityViewModel(FluxViewModel flux) : base(flux)
        {
            NFC = new NFCViewModel(Flux);
            Manage = new ManageViewModel(Flux);
            Routines = new RoutinesViewModel(Flux);
            Settings = new SettingsViewModel(Flux);

            var advanced_mode = Flux.MCodes
                .WhenAnyValue(s => s.OperatorUSB)
                .Select(o => o.ConvertOr(o => o.AdvancedSettings, () => false))
                .ToOptional();

            AddModal(Manage);
            AddModal(Settings);
            AddModal(Routines);
            AddModal(NFC, advanced_mode, advanced_mode);
        }
    }
}
