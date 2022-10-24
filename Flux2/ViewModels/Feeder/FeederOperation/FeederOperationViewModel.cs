﻿using DynamicData;
using DynamicData.Binding;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
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
                .AsObservableList();

            _TitleText = is_idle.Select(i => FindTitleText(i))
                .ToProperty(this, v => v.TitleText);

            _OperationText = is_idle.Select(i => FindOperationText(i))
                .ToProperty(this, v => v.OperationText);

            var can_cancel = CanCancelOperation();

            var all_conditions_true = FilteredConditions.Connect()
                .AddKey(c => c.ConditionName)
                .TrueForAll(c => c.WhenAnyValue(c => c.State), state => state.Valid);

            var can_execute = Observable.CombineLatest(
                all_conditions_true,
                Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation).Select(s => s.CanSafeCycle),
                (can_cycle, conditions) => can_cycle && conditions);

            var tool_key = Flux.ConnectionProvider.GetArrayUnit(m => m.TEMP_TOOL, Feeder.Position);
            _CurrentTemperature = Flux.ConnectionProvider.ObserveVariable(m => m.TEMP_TOOL, tool_key)
                .ObservableOrDefault()
                .ToProperty(this, v => v.CurrentTemperature);

            _TemperaturePercentage = this.WhenAnyValue(v => v.CurrentTemperature)
                .ConvertOr(t => t.Percentage, () => 0)
                .ToProperty(this, v => v.TemperaturePercentage);

            CancelOperationCommand = ReactiveCommand.CreateFromTask(CancelOperationAsync, can_cancel);
            ExecuteOperationCommand = ReactiveCommand.CreateFromTask(SafeExecuteOperationAsync, can_execute);
        }

        public abstract Task UpdateNFCAsync();

        protected abstract Task CancelOperationAsync();
        protected abstract IObservable<bool> CanCancelOperation();

        protected abstract Task<bool> ExecuteOperationAsync();
        private async Task SafeExecuteOperationAsync()
        {
            var navigate_back = await ExecuteOperationAsync();
            if (Flux.ConnectionProvider.ParkToolAfterOperation)
                await Flux.ConnectionProvider.ParkToolAsync();
            if (navigate_back)
                Flux.Navigator.NavigateBack();
        }

        protected abstract string FindTitleText(bool idle);
        protected abstract string FindOperationText(bool idle);

        protected virtual IEnumerable<(IConditionViewModel condition, TConditionAttribute condition_attribute)> FindConditions()
        {
            return Flux.StatusProvider.GetConditions<TConditionAttribute>().SelectMany(kvp => kvp.Value);
        }
    }
}
