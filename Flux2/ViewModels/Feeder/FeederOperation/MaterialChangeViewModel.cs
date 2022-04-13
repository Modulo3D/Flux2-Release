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

        protected override async Task<bool> CancelOperationAsync()
        {
            try
            {
                IsCanceled = true;
                if (!await Flux.ConnectionProvider.ResetAsync())
                    return false;
                var unit = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.TEMP_TOOL, Feeder.Position);
                if (!unit.HasValue)
                    return false;
                if (!await Flux.ConnectionProvider.WriteVariableAsync(m => m.TEMP_TOOL, unit.Value, 0))
                    return false;
                Flux.Navigator.NavigateBack();
                return true;
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
                return false;
            }
        }
        protected async Task<(bool result, Optional<double> current_break_temp)> ExecuteFilamentOperation(Func<IFLUX_Connection, Func<ushort, Nozzle, double, Optional<IEnumerable<string>>>> filament_operation, bool use_last_temp)
        {
            try
            {
                IsCanceled = false;

                if (!await Flux.ConnectionProvider.MGuard_PurgePosition())
                    return (false, default);

                if (!await Flux.ConnectionProvider.ResetAsync())
                    return (false, default);

                if (!Feeder.ToolNozzle.Document.nozzle.HasValue)
                    return (false, default);
                var nozzle = Feeder.ToolNozzle.Document.nozzle.Value;

                var tool_material = Feeder.ToolMaterials.Lookup(Material.Position);
                if (!tool_material.HasValue)
                    return (false, default);

                var current_break_temp = tool_material.Value.BreakTemp;
                if (!current_break_temp.HasValue)
                    return (false, current_break_temp);

                var last_break_temp = Feeder.ToolNozzle.Nfc.Tag
                    .Convert(n => n.LastBreakTemperature)
                    .ValueOr(() => 0);

                var break_temp = Math.Max(
                    current_break_temp.Value,
                    use_last_temp ? last_break_temp : 0);

                if (break_temp < 150)
                {
                    Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_INVALID_BREAK_TEMP);
                    return (false, current_break_temp);
                }

                using var put_filament_op_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var wait_filament_op_cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                if (!await Flux.ConnectionProvider.ExecuteParamacroAsync(f => filament_operation(f)(Feeder.Position, nozzle, break_temp), put_filament_op_cts.Token, true, wait_filament_op_cts.Token))
                {
                    Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_PARAMACRO, default);
                    return (false, current_break_temp);
                }

                return (!IsCanceled, current_break_temp);
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
            var operation = await ExecuteFilamentOperation(c => c.GetLoadFilamentGCode, true);
            if (operation.result == false)
            {
                Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_PARAMACRO, default);
                return false;
            }

            var material = Feeder.Materials
                .Lookup(Material.Position);

            if (!material.HasValue)
                return false;

            material.Value.StoreTag(t => t.SetLoaded(Feeder.Position));

            var tool_material = Feeder.ToolMaterials
                .Lookup(Material.Position);

            if (!tool_material.HasValue)
                return false;

            var break_temp = tool_material.Value.BreakTemp;
            if (!break_temp.HasValue)
            {
                Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_INVALID_BREAK_TEMP, default);
                return false;
            }

            Feeder.ToolNozzle.StoreTag(t => t.SetLastBreakTemp(break_temp.Value));

            var extrusion_temp = tool_material.Value.ExtrusionTemp;
            if (!extrusion_temp.HasValue)
            {
                Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_INVALID_EXTRUSION_TEMP, default);
                return false;
            }

            var try_count = 0;
            var result = ContentDialogResult.None;
            while (result != ContentDialogResult.Primary && try_count < 3)
            {
                try_count++;
                await Flux.ConnectionProvider.PurgeAsync(Feeder.Position, extrusion_temp.Value);
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
            if (Flux.ConnectionProvider.VariableStore.HasVariable(c => c.LOCK_CLOSED))
            {
                yield return Flux.StatusProvider.TopLockClosed;
                yield return Flux.StatusProvider.ChamberLockClosed;
            }

            var material = Feeder.Materials.Connect()
                .WatchOptional(Material.Position);

            var tool_material = Feeder.ToolMaterials.Connect()
                .WatchOptional(Material.Position);

            yield return ConditionViewModel.Create("materialKnown??0",
                material.ConvertMany(m => m.WhenAnyValue(m => m.Document)), m => true,
                (m, valid) => m.ConvertOr(m => $"MATERIALE LETTO: {m.Name}", () => "LEGGI UN MATERIALE"));

            yield return ConditionViewModel.Create("materialReady??1",
                material.ConvertMany(m => m.WhenAnyValue(f => f.State)), state => state.Locked,
                (value, valid) => valid ? "MATERIALE PRONTO AL CARICAMENTO" : "BLOCCA IL MATERIALE");

            yield return ConditionViewModel.Create("toolMaterialCompatible??2",
                tool_material.ConvertMany(tm => tm.WhenAnyValue(m => m.State)),
                m =>
                {
                    return m.KnownNozzle && m.KnownMaterial && m.Compatible;
                },
                (s, valid) =>
                {
                    if (!s.HasValue)
                        return "";
                    if (!s.Value.KnownNozzle)
                        return "LEGGERE UN UTENSILE";
                    if (!s.Value.KnownMaterial)
                        return "LEGGERE UN MATERIALE";
                    if (!s.Value.Compatible)
                        return "MATERIALE NON COMPATIBILE";
                    return "MATERIALE COMPATIBILE";
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

            var operation = await ExecuteFilamentOperation(c => c.GetUnloadFilamentGCode, false);
            if (operation.result == false)
                return false;

            material.Value.StoreTag(t => t.SetLoaded(default));

            var tool_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.TEMP_TOOL, Feeder.Position);
            if (!tool_key.HasValue)
                return false;
            await Flux.ConnectionProvider.WriteVariableAsync(m => m.TEMP_TOOL, tool_key.Value, 0);

            return true;
        }
        protected override IEnumerable<IConditionViewModel> FindConditions()
        {
            // TODO
            if (Flux.ConnectionProvider.VariableStore.HasVariable(c => c.LOCK_CLOSED))
            {
                yield return Flux.StatusProvider.TopLockClosed;
                yield return Flux.StatusProvider.ChamberLockClosed;
            }

            var material = Feeder.Materials.Connect()
                .WatchOptional(Material.Position);

            var tool_material = Feeder.ToolMaterials.Connect()
                .WatchOptional(Material.Position);

            yield return ConditionViewModel.Create("materialKnown??0",
                material.ConvertMany(m => m.WhenAnyValue(m => m.Document)), m => true,
                (m, valid) => m.ConvertOr(m => $"MATERIALE LETTO: {m.Name}", () => "LEGGI UN MATERIALE"));

            yield return ConditionViewModel.Create("materialReady??1",
                material.ConvertMany(m => m.WhenAnyValue(f => f.State)),
                s =>
                {
                    if (!s.Known)
                        return false;
                    if (s.Loaded && s.Locked)
                        return true;
                    if (!s.Loaded && !s.Locked)
                        return true;
                    return false;
                },
                (value, valid) =>
                {
                    if (!value.HasValue)
                        return "IMPOSSIBILE BLOCCARE IL MATERIALE";
                    if (value.Value.Loaded)
                        return valid ? "MATERIALE PRONTO ALLO SCARICAMENTO" : "BLOCCA IL MATERIALE";
                    else
                        return valid ? "MATERIALE SCARICATO" : "SBLOCCA IL MATERALE";
                });

            yield return ConditionViewModel.Create("toolMaterialCompatible??2",
                tool_material.ConvertMany(tm => tm.WhenAnyValue(tm => tm.State)),
                m =>
                {
                    return m.KnownNozzle && m.KnownMaterial && m.Compatible;
                },
                (s, valid) =>
                {
                    if (!s.HasValue)
                        return "";
                    if (!s.Value.KnownNozzle)
                        return "LEGGERE UN UTENSILE";
                    if (!s.Value.KnownMaterial)
                        return "LEGGERE UN MATERIALE";
                    if (!s.Value.Compatible)
                        return "MATERIALE NON COMPATIBILE";
                    return "MATERIALE COMPATIBILE";
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
    }
}
