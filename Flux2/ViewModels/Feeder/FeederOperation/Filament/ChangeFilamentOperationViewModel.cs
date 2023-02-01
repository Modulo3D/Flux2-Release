using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class FilamentOperationConditionAttribute : FeederOperationConditionAttribute
    {
        public FilamentOperationConditionAttribute(string name = default, bool filter_on_cycle = true, string[] include_alias = default, string[] exclude_alias = default)
            : base(name, filter_on_cycle, include_alias, exclude_alias)
        {
        }
    }
    public abstract class ChangeFilamentOperationViewModel<TChangeFilamentOperationViewModel> : FeederOperationViewModel<TChangeFilamentOperationViewModel, FilamentOperationConditionAttribute>
        where TChangeFilamentOperationViewModel : ChangeFilamentOperationViewModel<TChangeFilamentOperationViewModel>
    {
        public MaterialViewModel Material { get; }

        private bool IsCanceled { get; set; }

        private readonly ObservableAsPropertyHelper<Optional<bool>> _WirePresenceBeforeGear;
        [RemoteOutput(true)]
        public Optional<bool> WirePresenceBeforeGear => _WirePresenceBeforeGear.Value;

        private readonly ObservableAsPropertyHelper<Optional<bool>> _WirePresenceAfterGear;
        [RemoteOutput(true)]
        public Optional<bool> WirePresenceAfterGear => _WirePresenceAfterGear.Value;

        private readonly ObservableAsPropertyHelper<Optional<bool>> _WirePresenceOnHead;
        [RemoteOutput(true)]
        public Optional<bool> WirePresenceOnHead => _WirePresenceOnHead.Value;

        public ChangeFilamentOperationViewModel(MaterialViewModel material) : base(material.Feeder)
        {
            Material = material;

            var variable_store = Flux.ConnectionProvider.VariableStoreBase;
            var feeder_index = ArrayIndex.FromZeroBase(Feeder.Position, variable_store);
            var material_index = ArrayIndex.FromZeroBase(Material.Position, variable_store);

            var before_gear_key = Flux.ConnectionProvider.GetArrayUnit(m => m.FILAMENT_BEFORE_GEAR, material_index);
            _WirePresenceBeforeGear = Flux.ConnectionProvider.ObserveVariable(
                m => m.FILAMENT_BEFORE_GEAR,
                before_gear_key)
                .ObservableOrDefault()
                .ToPropertyRC((TChangeFilamentOperationViewModel)this, v => v.WirePresenceBeforeGear);

            var after_gear_key = Flux.ConnectionProvider.GetArrayUnit(m => m.FILAMENT_AFTER_GEAR, material_index);
            _WirePresenceAfterGear = Flux.ConnectionProvider.ObserveVariable(
                m => m.FILAMENT_AFTER_GEAR,
                after_gear_key)
                .ObservableOrDefault()
                .ToPropertyRC((TChangeFilamentOperationViewModel)this, v => v.WirePresenceAfterGear);

            var on_head_key = Flux.ConnectionProvider.GetArrayUnit(m => m.FILAMENT_ON_HEAD, feeder_index);
            _WirePresenceOnHead = Flux.ConnectionProvider.ObserveVariable(
                m => m.FILAMENT_ON_HEAD,
                on_head_key)
                .ObservableOrDefault()
                .ToPropertyRC((TChangeFilamentOperationViewModel)this, v => v.WirePresenceOnHead);
        }

        protected override IObservable<bool> CanCancelOperation()
        {
            return Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation).Select(s => s.CanSafeStop);
        }
        protected async Task<bool> CancelFilamentOperationAsync(Func<IFLUX_Connection, Func<ArrayIndex, GCodeString>> cancel_filament_operation)
        {
            try
            {
                IsCanceled = true;
                if (!await Flux.ConnectionProvider.StopAsync())
                    return false;

                var variable_store = Flux.ConnectionProvider.VariableStoreBase;
                var feeder_index = ArrayIndex.FromZeroBase(Feeder.Position, variable_store);

                using var put_cancel_filament_op_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var wait_cancel_filament_op_cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                if (!await Flux.ConnectionProvider.ExecuteParamacroAsync(f => cancel_filament_operation(f)(feeder_index), put_cancel_filament_op_cts.Token, true, wait_cancel_filament_op_cts.Token))
                {
                    Flux.Messages.LogMessage(MaterialChangeResult.MATERIAL_CHANGE_ERROR_PARAMACRO, default);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
                return false;
            }
        }
        protected async Task<bool> ExecuteFilamentOperation(Optional<GCodeFilamentOperation> settings, Func<IFLUX_Connection, Func<GCodeFilamentOperation, GCodeString>> filament_operation)
        {
            try
            {
                IsCanceled = false;

                if (!settings.HasValue)
                    return false;

                if (!await Flux.ConnectionProvider.StopAsync())
                    return false;

                using var put_filament_op_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var wait_filament_op_cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                if (!await Flux.ConnectionProvider.ExecuteParamacroAsync(f => filament_operation(f)(settings.Value), put_filament_op_cts.Token, true, wait_filament_op_cts.Token, true))
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
}
