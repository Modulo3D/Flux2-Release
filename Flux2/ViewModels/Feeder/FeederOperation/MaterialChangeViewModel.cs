using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
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
        
        public MaterialChangeViewModel(MaterialViewModel material) : base(material.Feeder)
        {
            Material = material;
        }

        protected override IObservable<bool> CanCancelOperation()
        {
            return Flux.StatusProvider.CanSafeStop;
        }
        protected override IObservable<bool> CanExecuteOperation()
        {
            return Observable.CombineLatest(
                this.WhenAnyValue(f => f.AllConditionsTrue),
                Flux.StatusProvider.CanSafeCycle,
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
        protected async Task<(bool result, Optional<double> current_break_temp)> ExecuteFilamentOperation(FilamentOperationSettings settings, Func<IFLUX_Connection, Func<Nozzle, FilamentOperationSettings, Optional<IEnumerable<string>>>> filament_operation, bool use_last_temp)
        {
            try
            {
                IsCanceled = false;

                if (!await Flux.ConnectionProvider.ResetAsync())
                    return (false, default);

                var nozzle = Feeder.ToolNozzle.Document.nozzle;
                if (!nozzle.HasValue)
                    return (false, default);

                var last_break_temp = Feeder.ToolNozzle.Nfc.Tag
                    .Convert(n => n.LastBreakTemperature)
                    .ValueOr(() => 0);

                var break_temp = Math.Max(
                    settings.BreakTemperature.ValueOr(() => 0),
                    use_last_temp ? last_break_temp : 0);

                if (break_temp < 150)
                {
                    Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_INVALID_BREAK_TEMP);
                    return (false, break_temp);
                }

                using var put_filament_op_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var wait_filament_op_cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                if (!await Flux.ConnectionProvider.ExecuteParamacroAsync(f => filament_operation(f)(nozzle.Value, settings), put_filament_op_cts.Token, true, wait_filament_op_cts.Token))
                {
                    Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_PARAMACRO, default);
                    return (false, break_temp);
                }

                return (!IsCanceled, break_temp);
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
                return (false, default);
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

            material.Value.StoreTag(t => t.SetLoaded(Feeder.Position));

            var filament_settings = new FilamentOperationSettings(Flux, Material);
            if (!filament_settings.ExtrusionTemperature.HasValue || !filament_settings.ExtrusionTemperature.HasValue)
                return false;
            
            var operation = await ExecuteFilamentOperation(filament_settings, c => c.GetLoadFilamentGCode, true);
            if (operation.result == false)
            {
                Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_PARAMACRO, default);
                return false;
            }

            Feeder.ToolNozzle.StoreTag(t => t.SetLastBreakTemp(filament_settings.BreakTemperature.Value));

            var try_count = 0;
            var result = ContentDialogResult.None;
            while (result != ContentDialogResult.Primary && try_count < 3)
            {
                try_count++;
                await Flux.ConnectionProvider.PurgeAsync(filament_settings);
                result = await Flux.ShowConfirmDialogAsync("Caricamento del filo", "Filo spurgato correttamente?");
            }

            if (result != ContentDialogResult.Primary)
            {
                Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_CANCELLED, default);
                return false;
            }

            Flux.Navigator.NavigateBack();
            return result == ContentDialogResult.Primary;
        }
        protected override IEnumerable<IConditionViewModel> FindConditions()
        {
            // TODO
            if (Flux.StatusProvider.TopLockClosed.HasValue)
                yield return Flux.StatusProvider.TopLockClosed.Value;
            if (Flux.StatusProvider.ChamberLockClosed.HasValue)
                yield return Flux.StatusProvider.ChamberLockClosed.Value;

            var material = Feeder.Materials.Connect()
                .WatchOptional(Material.Position);

            var tool_material = Feeder.ToolMaterials.Connect()
                .WatchOptional(Material.Position);

            yield return ConditionViewModel.Create("material",
                Observable.CombineLatest(
                    Feeder.ToolNozzle.WhenAnyValue(f => f.MaterialLoaded),
                    material.ConvertMany(m => m.WhenAnyValue(m => m.Document)),
                    material.ConvertMany(m => m.WhenAnyValue(f => f.State)),
                    tool_material.ConvertMany(tm => tm.WhenAnyValue(m => m.State)),
                    (loaded, document, material, tool_material) => (loaded, document, material, tool_material)),
                    value =>
                    {
                        if (value.loaded.HasValue)
                        {
                            var mat = value.loaded.Value.Document;
                            var pos = value.loaded.Value.Position;
                            return new ConditionState(false, $"MATERIALE GIA' CARICATO ({mat} POS. {pos + 1})");
                        }

                        if (value.document.ConvertOr(d => d.Id == 0, () => true)) 
                            return new ConditionState(false, "LEGGI UN MATERIALE");

                        if (value.material.ConvertOr(m => !m.Locked, () => true))
                            return new ConditionState(false, "BLOCCA IL MATERIALE");

                        if (value.tool_material.Convert(tm => tm.Compatible).ConvertOr(c => !c, () => true))
                            return new ConditionState(false, $"{value.document} NON COMPATIBILE");

                        return new ConditionState(true, $"{value.document} PRONTO AL CARICAMENTO");
                    });
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

                if (!reading.HasValue)
                {
                    if (operator_usb.ConvertOr(o => o.RewriteNFC, () => false))
                    {
                        var tag = await material.Value.CreateTagAsync();
                        reading = new NFCReading<NFCMaterial>(tag, material.Value.VirtualCardId);
                    }

                    if (!reading.HasValue)
                        return false;
                }

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
        protected override IObservable<string> FindUpdateNFCText()
        {
            var material = Feeder.Materials.Connect()
                .WatchOptional(Material.Position);

            return Observable.CombineLatest(
                material.ConvertMany(m => m.WhenAnyValue(m => m.Document)),
                this.WhenAnyValue(v => v.CanUpdateNFC),
                (d, nfc) => d.HasValue ? (nfc ? "BLOCCA" : "✔") : "LEGGI");
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

            var nfc = material.Value.Nfc;

            if (material.Value.WirePresence1 == false)
            {
                if (nfc.Tag.HasValue)
                {
                    var tag = material.Value.Nfc.Tag.Value;
                    if (tag.CurWeightG.HasValue)
                    {
                        var material_limit = tag.MaxWeightG / 100 * 5;
                        if (tag.CurWeightG.Value < material_limit)
                            material.Value.StoreTag(m => m.SetCurWeight(default));
                    }
                }
            }

            var tool_material = Feeder.ToolMaterials.Lookup(Material.Position);
            if (!tool_material.HasValue)
                return false;

            var extrusion_temp = tool_material.Value.ExtrusionTemp;
            if (!extrusion_temp.HasValue)
                return false;

            var break_temp = tool_material.Value.BreakTemp;
            if (!break_temp.HasValue)
                return false;

            var filament_settings = new FilamentOperationSettings()
            {
                Mixing = Material.Position,
                Extruder = Feeder.Position,
                BreakTemperature = break_temp.Value,
                ExtrusionTemperature = extrusion_temp.Value,
                FilamentOnHead = Flux.ConnectionProvider.GetArrayUnit(c => c.FILAMENT_ON_HEAD, Feeder.Position),
                FilamentAfterGear = Flux.ConnectionProvider.GetArrayUnit(c => c.FILAMENT_AFTER_GEAR, Material.Position),
                FilamentBeforeGear = Flux.ConnectionProvider.GetArrayUnit(c => c.FILAMENT_BEFORE_GEAR, Material.Position),
            };

            var operation = await ExecuteFilamentOperation(filament_settings, c => c.GetUnloadFilamentGCode, false);
            if (operation.result == false)
                return false;

            material.Value.StoreTag(t => t.SetLoaded(default));

            var tool_key = Flux.ConnectionProvider.GetArrayUnit(m => m.TEMP_TOOL, Feeder.Position);
            if (!tool_key.HasValue)
                return false;
            await Flux.ConnectionProvider.WriteVariableAsync(m => m.TEMP_TOOL, tool_key.Value.Alias, 0);

            return true;
        }
        protected override IEnumerable<IConditionViewModel> FindConditions()
        {
            // TODO
            if(Flux.StatusProvider.TopLockClosed.HasValue)
                yield return Flux.StatusProvider.TopLockClosed.Value;

            if (Flux.StatusProvider.ChamberLockClosed.HasValue)
                yield return Flux.StatusProvider.ChamberLockClosed.Value;

            var material = Feeder.Materials.Connect()
                .WatchOptional(Material.Position);

            var tool_material = Feeder.ToolMaterials.Connect()
                .WatchOptional(Material.Position);

            yield return ConditionViewModel.Create("materialKnown??0",
                material.ConvertMany(m => m.WhenAnyValue(m => m.Document)),
                value =>
                {
                    if (!value.HasValue || value.Value.Id == 0)
                        return new ConditionState(false, "LEGGI UN MATERIALE");
                    return new ConditionState(true, $"MATERIALE LETTO: {value.Value}");
                });

            yield return ConditionViewModel.Create("toolMaterialCompatible??1",
                tool_material.ConvertMany(tm => tm.WhenAnyValue(m => m.State)),
                value =>
                {
                    if (!value.HasValue)
                        return new ConditionState(default, "");
                    if (!value.Value.Compatible.HasValue)
                        return new ConditionState(default, "");
                    if (!value.Value.Compatible.Value)
                        return new ConditionState(false, "MATERIALE NON COMPATIBILE");
                    return new ConditionState(true, "MATERIALE COMPATIBILE");
                });

            yield return ConditionViewModel.Create("materialReady??2",
                material.ConvertMany(m => m.WhenAnyValue(f => f.State)),
                value => 
                {
                    if (!value.HasValue)
                        return new ConditionState(default, "");
                    if (value.Value.Loaded)
                        return new ConditionState(default, "");
                    if (value.Value.Locked)
                        return new ConditionState(false, "SBLOCCA IL MATERIALE");
                    return new ConditionState(true, "MATERIALE SBLOCCATO");
                });
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

                if (!reading.HasValue)
                {
                    if (operator_usb.ConvertOr(o => o.RewriteNFC, () => false))
                    {
                        var tag = await material.Value.CreateTagAsync();
                        reading = new NFCReading<NFCMaterial>(tag, material.Value.VirtualCardId);
                    }

                    if (!reading.HasValue)
                        return false;
                }

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
        protected override IObservable<string> FindUpdateNFCText()
        {
            var material = Feeder.Materials.Connect()
                .WatchOptional(Material.Position);

            return Observable.CombineLatest(
                material.ConvertMany(m => m.WhenAnyValue(m => m.State)),
                this.WhenAnyValue(v => v.CanUpdateNFC),
                (s, nfc) => s.HasValue && s.Value.Loaded ? (nfc ? "BLOCCA" : "✔") : (nfc ? "SBLOCCA" : "✔"));
        }

        protected override Task<bool> CancelOperationAsync()
        {
            return CancelFilamentOperationAsync(c => c.GetCancelUnloadFilamentGCode);
        }
    }
}
