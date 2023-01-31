using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class FeederOperationConditionAttribute : FilterConditionAttribute
    {
        public FeederOperationConditionAttribute(string name = default, bool filter_on_cycle = true, string[] include_alias = default, string[] exclude_alias = default)
            : base(name, filter_on_cycle, include_alias, exclude_alias)
        {
        }
    }

    public interface IOperationViewModel
    {
        string OperationText { get; }
        string TitleText { get; }
    }

    public abstract class FeederOperationViewModel<TViewModel, TConditionAttribute> : FluxRoutableViewModel<TViewModel>, IOperationViewModel
        where TViewModel : FeederOperationViewModel<TViewModel, TConditionAttribute>
        where TConditionAttribute : FeederOperationConditionAttribute
    {
        public FeederViewModel Feeder { get; }

        private ObservableAsPropertyHelper<string> _TitleText;
        [RemoteOutput(true)]
        public string TitleText => _TitleText.Value;

        private ObservableAsPropertyHelper<string> _OperationText;
        [RemoteOutput(true)]
        public string OperationText => _OperationText.Value;

        [RemoteContent(true)]
        public IObservableList<IConditionViewModel> FilteredConditions { get; private set; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> CancelOperationCommand { get; private set; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ExecuteOperationCommand { get; private set; }

        private ObservableAsPropertyHelper<Optional<FLUX_Temp>> _CurrentTemperature;
        [RemoteOutput(true, typeof(FluxTemperatureConverter))]
        public Optional<FLUX_Temp> CurrentTemperature => _CurrentTemperature.Value;

        private ObservableAsPropertyHelper<Optional<double>> _TemperaturePercentage;
        [RemoteOutput(true)]
        public Optional<double> TemperaturePercentage => _TemperaturePercentage.Value;

        public FeederOperationViewModel(FeederViewModel feeder) : base(feeder.Flux)
        {
            Feeder = feeder;
        }

        public void Initialize()
        {
            var is_idle = Feeder.Flux.StatusProvider
                .WhenAnyValue(s => s.StatusEvaluation)
                .Select(s => s.IsIdle);

            var conditions = FindConditions();
            FilteredConditions = conditions
                .AsObservableChangeSet()
                .Filter(is_idle.Select(idle =>
                {
                    return (Func<(IConditionViewModel condition, TConditionAttribute condition_attribute), bool>)filter_condition;
                    bool filter_condition((IConditionViewModel condition, TConditionAttribute condition_attribute) t) => !t.condition_attribute.FilterOnCycle || idle;
                }))
                .Transform(t => t.condition)
                .AsObservableListRC(Disposables);

            _TitleText = is_idle.Select(i => FindTitleText(i))
                .ToPropertyRC(this, v => v.TitleText, Disposables);

            _OperationText = is_idle.Select(i => FindOperationText(i))
                .ToPropertyRC(this, v => v.OperationText, Disposables);

            var can_cancel = CanCancelOperation();

            var all_conditions_true = FilteredConditions.Connect()
                .AddKey(c => c.ConditionName)
                .TrueForAll(c => c.WhenAnyValue(c => c.State), state => state.Valid);

            var can_execute = Observable.CombineLatest(
                all_conditions_true,
                Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation).Select(s => s.CanSafeCycle),
                (can_cycle, conditions) => can_cycle && conditions);

            var variable_store = Flux.ConnectionProvider.VariableStoreBase;

            var tool_index = ArrayIndex.FromZeroBase(Feeder.Position, variable_store);
            var tool_key = Flux.ConnectionProvider.GetArrayUnit(m => m.TEMP_TOOL, tool_index);
            _CurrentTemperature = Flux.ConnectionProvider.ObserveVariable(m => m.TEMP_TOOL, tool_key)
                .ObservableOrDefault()
                .ToPropertyRC(this, v => v.CurrentTemperature, Disposables);

            _TemperaturePercentage = this.WhenAnyValue(v => v.CurrentTemperature)
                .ConvertOr(t => t.Percentage, () => 0)
                .ToPropertyRC(this, v => v.TemperaturePercentage, Disposables);

            CancelOperationCommand = ReactiveCommandRC.CreateFromTask(SafeCancelOperationAsync, Disposables, can_cancel);
            ExecuteOperationCommand = ReactiveCommandRC.CreateFromTask(SafeExecuteOperationAsync, Disposables, can_execute);
        }

        public abstract Task UpdateNFCAsync();

        protected abstract Task<bool> CancelOperationAsync();
        private async Task SafeCancelOperationAsync()
        {
            var (result, success) = await SafeExecuteAsync(CancelOperationAsync);
            if (result.HasValue && result.Value && success)
                Flux.Navigator.NavigateBack();
        }
        protected abstract IObservable<bool> CanCancelOperation();

        protected abstract Task<bool> ExecuteOperationAsync();
        private async Task SafeExecuteOperationAsync()
        {
            var (result, success) = await SafeExecuteAsync(ExecuteOperationAsync);
            if (result.HasValue && result.Value && success)
                Flux.Navigator.NavigateBack();
        }

        private async Task<(Optional<TResult> result, bool success)> SafeExecuteAsync<TResult>(Func<Task<TResult>> execute_task)
        {
            var park_tool_gcode = Flux.ConnectionProvider.ConnectionBase.GetParkToolGCode();
            if (!park_tool_gcode.HasValue)
                return (default, false);

            var variable_store = Flux.ConnectionProvider.VariableStoreBase;
            var set_tool_temp_gcode = Flux.ConnectionProvider.ConnectionBase.GetSetToolTemperatureGCode(ArrayIndex.FromZeroBase(Feeder.Position, variable_store), 0, false);
            if (!set_tool_temp_gcode.HasValue)
                return (default, false);

            var result = await execute_task();

            using var put_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var wait_cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var success = await Flux.ConnectionProvider.ExecuteParamacroAsync(c => new GCodeString(get_safe_gcode()), put_cts.Token, true, wait_cts.Token, false);

            return (result, success);

            IEnumerable<string> get_safe_gcode()
            {
                foreach (var line in set_tool_temp_gcode)
                    yield return line;

                if (variable_store.ParkToolAfterOperation)
                    foreach (var line in park_tool_gcode)
                        yield return line;
            }
        }

        protected abstract string FindTitleText(bool idle);
        protected abstract string FindOperationText(bool idle);

        protected virtual IEnumerable<(IConditionViewModel condition, TConditionAttribute condition_attribute)> FindConditions()
        {
            return Flux.StatusProvider.GetConditions<TConditionAttribute>().SelectMany(kvp => kvp.Value);
        }
    }
}
