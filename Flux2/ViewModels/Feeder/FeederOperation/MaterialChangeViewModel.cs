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
        private bool IsCanceled { get; set; }

        public MaterialChangeViewModel(FeederViewModel feeder) : base(feeder)
        {
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

                var current_break_temp = Feeder.ToolMaterial.BreakTemp;
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
                    Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_PARAMACRO, Feeder.Material.State);
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
        public LoadMaterialViewModel(FeederViewModel feeder) : base(feeder)
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
                Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_PARAMACRO, Feeder.Material.State);
                return false;
            }

            Feeder.Material.StoreTag(t => t.SetLoaded(Feeder.Position));

            var break_temp = Feeder.ToolMaterial.BreakTemp;
            if (!break_temp.HasValue)
            {
                Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_INVALID_BREAK_TEMP, Feeder.Material.State);
                return false;
            }

            Feeder.ToolNozzle.StoreTag(t => t.SetLastBreakTemp(break_temp.Value));

            var extrusion_temp = Feeder.ToolMaterial.ExtrusionTemp;
            if (!extrusion_temp.HasValue)
            {
                Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_INVALID_EXTRUSION_TEMP, Feeder.Material.State);
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
                Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_CANCELLED, Feeder.Material.State);
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

            yield return ConditionViewModel.Create("materialKnown??0",
                Feeder.Material.WhenAnyValue(m => m.Document).Select(d => d.ToOptional()), m => m.HasValue,
                (m, valid) => m.Convert(m => m.Convert(m => $"MATERIALE LETTO: {m.Name}")).ValueOr(() => "LEGGI UN MATERIALE"));

            yield return ConditionViewModel.Create("materialReady??1",
                Feeder.Material.WhenAnyValue(f => f.State).Select(s => s.ToOptional()), state => state.Locked,
                (value, valid) => valid ? "MATERIALE PRONTO AL CARICAMENTO" : "BLOCCA IL MATERIALE");

            yield return ConditionViewModel.Create("toolMaterialCompatible??2",
                Feeder.ToolMaterial.WhenAnyValue(m => m.State).Select(d => d.ToOptional()),
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
            if (!Feeder.Material.Nfc.Tag.HasValue)
            {
                var operator_usb = Flux.MCodes.OperatorUSB;
                var reading = await Flux.UseReader(h => Feeder.Material.ReadTag(h, true), r => r.HasValue);

                if (!reading.HasValue)
                {
                    if (operator_usb.ConvertOr(o => o.RewriteNFC, () => false))
                    {
                        var tag = await Feeder.Material.CreateTagAsync();
                        reading = new NFCReading<NFCMaterial>(tag, Feeder.Material.VirtualCardId);
                    }

                    if (!reading.HasValue)
                        return false;
                }

                await Feeder.Material.StoreTagAsync(reading.Value);
            }
            
            var result = Feeder.Material.Nfc.IsVirtualTag ?
                await Feeder.Material.LockTagAsync(default) :
                await Flux.UseReader(h => Feeder.Material.LockTagAsync(h), l => l);
            
            return result.ValueOr(() => false);
        }
        protected override IObservable<bool> FindCanUpdateNFC()
        {
            var state = Feeder.Material.WhenAnyValue(m => m.State);
            return state.Select(s =>
            {
                if (s.Inserted && !s.Known)
                    return false;
                if (s.Loaded || s.Locked)
                    return false;
                return true;
            });
        }
        protected override IObservable<string> FindUpdateNFCText()
        {
            return Observable.CombineLatest(
                Feeder.Material.WhenAnyValue(m => m.Document),
                this.WhenAnyValue(v => v.CanUpdateNFC),
                (d, nfc) => d.HasValue ? (nfc ? "BLOCCA" : "✔") : "LEGGI");
        }
    }

    public class UnloadMaterialViewModel : MaterialChangeViewModel<UnloadMaterialViewModel>
    {
        public UnloadMaterialViewModel(FeederViewModel feeder) : base(feeder)
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
            var nfc = Feeder.Material.Nfc;

            if (Feeder.Material.WirePresence1 == false)
            {
                if (nfc.Tag.HasValue)
                {
                    var tag = Feeder.Material.Nfc.Tag.Value;
                    if (tag.CurWeightG.HasValue)
                    {
                        var material_limit = tag.MaxWeightG / 100 * 5;
                        if (tag.CurWeightG.Value < material_limit)
                            Feeder.Material.StoreTag(m => m.SetCurWeight(default));
                    }
                }
            }

            var operation = await ExecuteFilamentOperation(c => c.GetUnloadFilamentGCode, false);
            if (operation.result == false)
                return false;

            Feeder.Material.StoreTag(t => t.SetLoaded(default));

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

            yield return ConditionViewModel.Create("materialKnown??0",
                Feeder.Material.WhenAnyValue(m => m.Document).Select(d => d.ToOptional()), m => m.HasValue,
                (m, valid) => m.Convert(m => m.Convert(m => $"MATERIALE LETTO: {m.Name}")).ValueOr(() => "LEGGI UN MATERIALE"));

            yield return ConditionViewModel.Create("materialReady??1",
                Feeder.Material.WhenAnyValue(f => f.State).Select(s => s.ToOptional()),
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
                Feeder.ToolMaterial.WhenAnyValue(m => m.State).Select(d => d.ToOptional()),
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
            if (!Feeder.Material.Nfc.Tag.HasValue)
            {
                var operator_usb = Flux.MCodes.OperatorUSB;
                var reading = await Flux.UseReader(h => Feeder.Material.ReadTag(h, true), r => r.HasValue);

                if (!reading.HasValue)
                {
                    if (operator_usb.ConvertOr(o => o.RewriteNFC, () => false))
                    {
                        var tag = await Feeder.Material.CreateTagAsync();
                        reading = new NFCReading<NFCMaterial>(tag, Feeder.Material.VirtualCardId);
                    }

                    if (!reading.HasValue)
                        return false;
                }

                await Feeder.Material.StoreTagAsync(reading.Value);
            }

            var result = Feeder.Material.Nfc.IsVirtualTag ? 
                await Feeder.Material.UnlockTagAsync(default) :
                await Flux.UseReader(h => Feeder.Material.UnlockTagAsync(h), u => u);
            
            if (!result.HasValue || !result.Value)
                return false;

            Flux.Navigator.NavigateBack();
            return true;
        }
        protected override IObservable<bool> FindCanUpdateNFC()
        {
            var state = Feeder.Material.WhenAnyValue(m => m.State);
            return state.Select(s =>
            {
                if (!s.Known)
                    return false;
                if (s.Loaded && !s.Locked)
                    return true;
                if (!s.Loaded && s.Locked)
                    return true;
                return false;
            });
        }
        protected override IObservable<string> FindUpdateNFCText()
        {
            return Observable.CombineLatest(
                Feeder.Material.WhenAnyValue(m => m.State),
                this.WhenAnyValue(v => v.CanUpdateNFC),
                (s, nfc) => s.Loaded ? (nfc ? "BLOCCA" : "✔") : (nfc ? "SBLOCCA" : "✔"));
        }
    }
}
