using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public abstract class MaterialChangeViewModel<T> : FeederOperationViewModel<T>
        where T : MaterialChangeViewModel<T>
    {
        public MaterialViewModel Material { get; }
        private bool IsCanceled { get; set; }

        private ObservableAsPropertyHelper<Optional<bool>> _WirePresenceBeforeGear;
        [RemoteOutput(true)]
        public Optional<bool> WirePresenceBeforeGear => _WirePresenceBeforeGear.Value;

        private ObservableAsPropertyHelper<Optional<bool>> _WirePresenceAfterGear;
        [RemoteOutput(true)]
        public Optional<bool> WirePresenceAfterGear => _WirePresenceAfterGear.Value;

        private ObservableAsPropertyHelper<Optional<bool>> _WirePresenceOnHead;
        [RemoteOutput(true)]
        public Optional<bool> WirePresenceOnHead => _WirePresenceOnHead.Value;


        public MaterialChangeViewModel(MaterialViewModel material) : base(material.Feeder)
        {
            Material = material;

            var before_gear_key = Flux.ConnectionProvider
                .GetArrayUnit(m => m.FILAMENT_BEFORE_GEAR, material.Position)
                .Convert(u => u.Alias)
                .ValueOrDefault();

            _WirePresenceBeforeGear = Flux.ConnectionProvider.ObserveVariable(
                m => m.FILAMENT_BEFORE_GEAR,
                before_gear_key)
                .ObservableOrDefault()
                .ToProperty(this, v => v.WirePresenceBeforeGear)
                .DisposeWith(Disposables);

            var after_gear_key = Flux.ConnectionProvider
                .GetArrayUnit(m => m.FILAMENT_AFTER_GEAR, material.Position)
                .Convert(u => u.Alias)
                .ValueOrDefault();

            _WirePresenceAfterGear = Flux.ConnectionProvider.ObserveVariable(
                m => m.FILAMENT_AFTER_GEAR,
                after_gear_key)
                .ObservableOrDefault()
                .ToProperty(this, v => v.WirePresenceAfterGear)
                .DisposeWith(Disposables);

            var on_head_key = Flux.ConnectionProvider
                .GetArrayUnit(m => m.FILAMENT_ON_HEAD, Feeder.Position)
                .Convert(u => u.Alias)
                .ValueOrDefault();

            _WirePresenceOnHead = Flux.ConnectionProvider.ObserveVariable(
                m => m.FILAMENT_ON_HEAD,
                on_head_key)
                .ObservableOrDefault()
                .ToProperty(this, v => v.WirePresenceOnHead)
                .DisposeWith(Disposables);
        }

        protected override IObservable<bool> CanCancelOperation()
        {
            return Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation).Select(s => s.CanSafeStop);
        }
        protected override IObservable<bool> CanExecuteOperation()
        {
            return Observable.CombineLatest(
                this.WhenAnyValue(f => f.AllConditionsTrue),
                Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation).Select(s => s.CanSafeCycle),
                (c, s) => c && s);
        }
        protected async Task<bool> CancelFilamentOperationAsync(Func<IFLUX_Connection, Func<ushort, Optional<IEnumerable<string>>>> cancel_filament_operation)
        { 
            try
            {
                IsCanceled = true;
                if (!await Flux.ConnectionProvider.ResetAsync())
                    return false;

                using var put_cancel_filament_op_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var wait_cancel_filament_op_cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                if (!await Flux.ConnectionProvider.ExecuteParamacroAsync(f => cancel_filament_operation(f)(Feeder.Position), put_cancel_filament_op_cts.Token, true, wait_cancel_filament_op_cts.Token))
                {
                    Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_PARAMACRO, default);
                    return false;
                }

                Flux.Navigator.NavigateBack();
                return true;
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
                return false;
            }
        }
        protected async Task<bool> ExecuteFilamentOperation(GCodeFilamentOperation settings, Func<IFLUX_Connection, Func<GCodeFilamentOperation, Optional<IEnumerable<string>>>> filament_operation)
        {
            try
            {
                IsCanceled = false;

                if (!await Flux.ConnectionProvider.ResetAsync())
                    return false;

                using var put_filament_op_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var wait_filament_op_cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                if (!await Flux.ConnectionProvider.ExecuteParamacroAsync(f => filament_operation(f)(settings), put_filament_op_cts.Token, true, wait_filament_op_cts.Token, true))
                {
                    Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_PARAMACRO, default);
                    return false;
                }

                return !IsCanceled;
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
                return false;
            }
        }
    }

    public class LoadMaterialViewModel : MaterialChangeViewModel<LoadMaterialViewModel>
    {
        public LoadMaterialViewModel(MaterialViewModel material) : base(material)
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
            var material = Feeder.Materials
                .Lookup(Material.Position);

            if (!material.HasValue)
                return false;

            if (!material.Value.Nfc.Tag.HasValue)
                return false;

            var nfc_tag = material.Value.Nfc.Tag;
            if (!nfc_tag.HasValue)
                return false;

            var cur_weight = nfc_tag.Value.CurWeightG;
            if (!cur_weight.HasValue)
                return false;

            if (cur_weight.Value <= 0)
            {
                Flux.Messages.LogMessage("Impossibile caricare il materiale", "Bobina terminata", MessageLevel.WARNING, 0);
                return false;
            }

            var has_filament_after_gear = Flux.ConnectionProvider.HasVariable(c => c.FILAMENT_AFTER_GEAR);
            if (!has_filament_after_gear)
            {
                var iterations = 0;
                var max_iterations = 2;
                var iteration_dist = 10;
                var dialog_result = ContentDialogResult.None;
                do
                {
                    using var put_move_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    using var wait_move_cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var move_result = await Flux.ConnectionProvider.ExecuteParamacroAsync(c =>
                    {
                        var gcode = new List<string>();

                        var select_tool_gcode = c.GetSelectToolGCode(Feeder.Position);
                        if (!select_tool_gcode.HasValue)
                            return default;
                        gcode.Add(select_tool_gcode.Value);

                        var movement_gcode = c.GetRelativeEMovementGCode(iteration_dist, 100);
                        if (!movement_gcode.HasValue)
                            return default;
                        gcode.Add(movement_gcode.Value);

                        return gcode;
                    }, put_move_cts.Token, true, wait_move_cts.Token);
                    if (!move_result)
                        return false;

                    dialog_result = await Flux.ShowConfirmDialogAsync("CARICO FILO", "FILO INSERITO CORRETTAMENTE?");
                } while (dialog_result != ContentDialogResult.Primary && ++iterations < max_iterations);

                if (dialog_result != ContentDialogResult.Primary)
                {
                    using var put_move_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    using var wait_move_cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var move_result = await Flux.ConnectionProvider.ExecuteParamacroAsync(c =>
                    {
                        var gcode = new List<string>();

                        var select_tool_gcode = c.GetSelectToolGCode(Feeder.Position);
                        if (!select_tool_gcode.HasValue)
                            return default;
                        gcode.Add(select_tool_gcode.Value);

                        var extract_distance = (iteration_dist * max_iterations) + 50;
                        var movement_gcode = c.GetRelativeEMovementGCode(-iteration_dist, 500);
                        if (!movement_gcode.HasValue)
                            return default;
                        gcode.Add(movement_gcode.Value);

                        var park_tool_gcode = c.GetParkToolGCode();
                        if (!park_tool_gcode.HasValue)
                            return default;
                        gcode.Add(park_tool_gcode.Value);

                        return gcode;
                    }, put_move_cts.Token, true, wait_move_cts.Token);
                    if (!move_result)
                        return false;

                    return false;
                }
            }

            if (!Flux.ConnectionProvider.HasVariable(c => c.FILAMENT_ON_HEAD))
                material.Value.StoreTag(t => t.SetLoaded(Feeder.Position));

            var filament_settings = GCodeFilamentOperation.Create(Flux, Feeder, Material, true, false, true);
            if (!filament_settings.HasValue)
                return false;
            
            var operation = await ExecuteFilamentOperation(filament_settings.Value, c => c.GetLoadFilamentGCode);
            if (operation == false)
            {
                Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_PARAMACRO, default);
                material.Value.StoreTag(t => t.SetLoaded(default));
                return false;
            }

            // check endstop
            if (Flux.ConnectionProvider.HasVariable(c => c.FILAMENT_ON_HEAD))
            {
                var filament_on_head_unit = Flux.ConnectionProvider.GetArrayUnit(c => c.FILAMENT_ON_HEAD, Feeder.Position);
                if (!filament_on_head_unit.HasValue)
                {
                    material.Value.StoreTag(t => t.SetLoaded(default));
                    return false;
                }

                var has_filament_on_head = await Flux.ConnectionProvider.ReadVariableAsync(c => c.FILAMENT_ON_HEAD, filament_on_head_unit.Value.Alias);
                if (!has_filament_on_head.HasValue || !has_filament_on_head.Value)
                {
                    material.Value.StoreTag(t => t.SetLoaded(default));
                    return false;
                }
            }

            Feeder.ToolNozzle.StoreTag(t => t.SetLastBreakTemp(filament_settings.Value.BreakTemperature));

            var try_count = 0;
            var result = ContentDialogResult.None;
            while (result != ContentDialogResult.Primary && try_count < 3)
            {
                try_count++;
                await Flux.ConnectionProvider.PurgeAsync(filament_settings.Value);
                result = await Flux.ShowConfirmDialogAsync("Caricamento del filo", "Filo spurgato correttamente?");
            }

            if (result != ContentDialogResult.Primary)
            {
                Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_CANCELLED, default);
                return false;
            }

            if (!await Flux.ConnectionProvider.ParkToolAsync())
            {
                Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_CANCELLED, default);
                return false;
            }

            Flux.Navigator.NavigateBack();
            return result == ContentDialogResult.Primary;
        }
        protected override IEnumerable<(IConditionViewModel condition, bool filter_on_cycle)> FindConditions()
        {
            // TODO
            if (Flux.StatusProvider.TopLockClosed.HasValue)
                yield return (Flux.StatusProvider.TopLockClosed.Value, true);

            if (Flux.StatusProvider.ChamberLockClosed.HasValue)
                yield return (Flux.StatusProvider.ChamberLockClosed.Value, true);

            if (Flux.StatusProvider.SpoolsLock.HasValue)
                yield return (Flux.StatusProvider.SpoolsLock.Value, false);

            var material = Feeder.Materials.Connect()
                .WatchOptional(Material.Position);

            var tool_material = Feeder.ToolMaterials.Connect()
                .WatchOptional(Material.Position);

            var can_update_nfc = FindCanUpdateNFC();

            yield return (ConditionViewModel.Create(
                Flux,
                "material",
                Observable.CombineLatest(
                    Feeder.ToolNozzle.WhenAnyValue(f => f.MaterialLoaded),
                    material.ConvertMany(m => m.WhenAnyValue(m => m.Document)),
                    material.ConvertMany(m => m.WhenAnyValue(f => f.State)),
                    tool_material.ConvertMany(tm => tm.WhenAnyValue(m => m.State)),
                    (loaded, document, material, tool_material) => (loaded, document, material, tool_material)),
                    (state, value) =>
                    {
                        if (value.loaded.HasValue)
                        {
                            var mat = value.loaded.Value.Document;
                            var pos = value.loaded.Value.Position;
                            return state.Create(false, $"MATERIALE GIA' CARICATO ({mat} POS. {pos + 1})");
                        }

                        if (value.document.ConvertOr(d => d.Id == 0, () => true)) 
                            return state.Create(false, "LEGGI UN MATERIALE", "nFC", UpdateNFCAsync, can_update_nfc);
                        
                        if (value.tool_material.Convert(tm => tm.Compatible).ConvertOr(c => !c, () => true))
                            return state.Create(false, $"{value.document} NON COMPATIBILE");

                        if (value.material.ConvertOr(m => !m.Locked, () => true))
                            return state.Create(false, "BLOCCA IL MATERIALE", "nFC", UpdateNFCAsync, can_update_nfc);

                        return new ConditionState(true, $"{value.document} PRONTO AL CARICAMENTO");
                    }), true);
        }

        public override async Task<bool> UpdateNFCAsync()
        {
            var material = Feeder.Materials
                .Lookup(Material.Position);

            if (!material.HasValue)
                return false;

            if (!material.Value.Nfc.Tag.HasValue)
            {
                var operator_usb = Flux.MCodes.OperatorUSB;
                var reading = await Flux.UseReader(h => material.Value.ReadTag(h, true), r => r.HasValue);

                if (operator_usb.ConvertOr(o => o.RewriteNFC, () => false))
                {
                    var tag = await material.Value.CreateTagAsync(reading);
                    reading = new NFCReading<NFCMaterial>(tag, material.Value.VirtualCardId);
                }

                if (!reading.HasValue)
                    return false;

                await material.Value.StoreTagAsync(reading.Value);
            }
            
            var result = material.Value.Nfc.IsVirtualTag ?
                await material.Value.LockTagAsync(default) :
                await Flux.UseReader(h => material.Value.LockTagAsync(h), l => l);
            
            return result.ValueOr(() => false);
        }
        protected override IObservable<bool> FindCanUpdateNFC()
        {
            var material = Feeder.Materials.Connect()
                .WatchOptional(Material.Position);

            var state = material.ConvertMany(m => m.WhenAnyValue(m => m.State));
            return state.ConvertOr(s =>
            {
                if (s.Inserted && !s.Known)
                    return false;
                if (s.Loaded || s.Locked)
                    return false;
                return true;
            }, () => false);
        }

        protected override Task<bool> CancelOperationAsync()
        {
            return CancelFilamentOperationAsync(c => c.GetCancelLoadFilamentGCode);
        }
    }

    public class UnloadMaterialViewModel : MaterialChangeViewModel<UnloadMaterialViewModel>
    {
        public UnloadMaterialViewModel(MaterialViewModel material) : base(material)
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
            var material = Feeder.Materials
                .Lookup(Material.Position);

            if (!material.HasValue)
                return false;

            var tool_material = Feeder.ToolMaterials.Lookup(Material.Position);
            if (!tool_material.HasValue)
                return false;

            var extrusion_temp = tool_material.Value.ExtrusionTemp;
            if (!extrusion_temp.HasValue)
                return false;

            var break_temp = tool_material.Value.BreakTemp;
            if (!break_temp.HasValue)
                return false;

            var filament_settings = GCodeFilamentOperation.Create(Flux, Feeder, Material, false, true, false);
            if (!filament_settings.HasValue)
                return false;

            var operation = await ExecuteFilamentOperation(filament_settings.Value, c => c.GetUnloadFilamentGCode);
            if (operation == false)
                return false;

            material.Value.StoreTag(t => t.SetLoaded(default));

            var tool_key = Flux.ConnectionProvider.GetArrayUnit(m => m.TEMP_TOOL, Feeder.Position);
            if (!tool_key.HasValue)
                return false;
            await Flux.ConnectionProvider.WriteVariableAsync(m => m.TEMP_TOOL, tool_key.Value.Alias, 0);

            return true;
        }
        protected override IEnumerable<(IConditionViewModel condition, bool filter_on_cycle)> FindConditions()
        {
            // TODO
            if(Flux.StatusProvider.TopLockClosed.HasValue)
                yield return (Flux.StatusProvider.TopLockClosed.Value, true);

            if (Flux.StatusProvider.ChamberLockClosed.HasValue)
                yield return (Flux.StatusProvider.ChamberLockClosed.Value, true);

            if (Flux.StatusProvider.SpoolsLock.HasValue)
                yield return (Flux.StatusProvider.SpoolsLock.Value, false);

            var material = Feeder.Materials.Connect()
                .WatchOptional(Material.Position);

            var tool_material = Feeder.ToolMaterials.Connect()
                .WatchOptional(Material.Position);

            yield return (ConditionViewModel.Create(
                Flux,
                "material",
                Observable.CombineLatest(
                    Feeder.ToolNozzle.WhenAnyValue(f => f.MaterialLoaded),
                    material.ConvertMany(m => m.WhenAnyValue(m => m.Document)),
                    material.ConvertMany(m => m.WhenAnyValue(f => f.State)),
                    tool_material.ConvertMany(tm => tm.WhenAnyValue(m => m.State)),
                    (loaded, document, material, tool_material) => (loaded, document, material, tool_material)),
                    (state, value) =>
                    {
                        if (value.document.ConvertOr(d => d.Id == 0, () => true))
                            return state.Create(false, "LEGGI UN MATERIALE", "nFC", UpdateNFCAsync);

                        if (value.tool_material.Convert(tm => tm.Compatible).ConvertOr(c => !c, () => true))
                            return state.Create(false, $"{value.document} NON COMPATIBILE", "nFC", UpdateNFCAsync);

                        if (!value.loaded.HasValue)
                            return state.Create(false, "SBLOCCA IL MATERIALE", "nFC", UpdateNFCAsync);

                        return new ConditionState(true, $"{value.document} PRONTO ALLO SCARICAMENTO");
                    }), true);
        }
        public override async Task<bool> UpdateNFCAsync()
        {
            var material = Feeder.Materials
                .Lookup(Material.Position);
            
            if (!material.HasValue)
                return false;

            if (!material.Value.Nfc.Tag.HasValue)
            {
                var operator_usb = Flux.MCodes.OperatorUSB;
                var reading = await Flux.UseReader(h => material.Value.ReadTag(h, true), r => r.HasValue);

                if (operator_usb.ConvertOr(o => o.RewriteNFC, () => false))
                {
                    var tag = await material.Value.CreateTagAsync(reading);
                    reading = new NFCReading<NFCMaterial>(tag, material.Value.VirtualCardId);
                }

                if (!reading.HasValue)
                    return false;

                await material.Value.StoreTagAsync(reading.Value);
            }

            var result = material.Value.Nfc.IsVirtualTag ? 
                await material.Value.UnlockTagAsync(default) :
                await Flux.UseReader(h => material.Value.UnlockTagAsync(h), u => u);
            
            if (!result.HasValue || !result.Value)
                return false;

            Flux.Navigator.NavigateBack();
            return true;
        }
        protected override IObservable<bool> FindCanUpdateNFC()
        {
            var material = Feeder.Materials.Connect()
                .WatchOptional(Material.Position);

            var state = material.ConvertMany(m => m.WhenAnyValue(m => m.State));
            return state.Select(s =>
            {
                if (!s.HasValue)
                    return false;
                if (!s.Value.Known)
                    return false;
                if (s.Value.Loaded && !s.Value.Locked)
                    return true;
                if (!s.Value.Loaded && s.Value.Locked)
                    return true;
                return false;
            });
        }

        protected override Task<bool> CancelOperationAsync()
        {
            return CancelFilamentOperationAsync(c => c.GetCancelUnloadFilamentGCode);
        }
    }
}
