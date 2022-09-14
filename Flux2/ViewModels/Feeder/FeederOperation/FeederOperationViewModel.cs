using DynamicData;
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
    public interface IOperationViewModel
    {
        string OperationText { get; }
        bool AllConditionsTrue { get; }
        string TitleText { get; }
    }

    public abstract class FeederOperationViewModel<TViewModel> : FluxRoutableViewModel<TViewModel>, IOperationViewModel
        where TViewModel : FeederOperationViewModel<TViewModel>
    {
        public FeederViewModel Feeder { get; }

        private ObservableAsPropertyHelper<string> _TitleText;
        [RemoteOutput(true)]
        public string TitleText => _TitleText.Value;

        private ObservableAsPropertyHelper<string> _OperationText;
        [RemoteOutput(true)]
        public string OperationText => _OperationText.Value;

        [RemoteContent(true)]
        public IObservableCache<IConditionViewModel, string> FilteredConditions { get; private set; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> CancelOperationCommand { get; private set; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ExecuteOperationCommand { get; private set; }


        private ObservableAsPropertyHelper<bool> _AllConditionsTrue;
        public bool AllConditionsTrue => _AllConditionsTrue.Value;

        private ObservableAsPropertyHelper<bool> _AllConditionsFalse;
        public bool AllConditionsFalse => _AllConditionsFalse.Value;

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
                .AsObservableChangeSet(t => t.condition.Name)
                .Filter(is_idle.Select(idle =>
                {
                    return (Func<(IConditionViewModel condition, bool filter_on_cycle), bool>)filter_condition;
                    bool filter_condition((IConditionViewModel condition, bool filter_on_cycle) t) => !t.filter_on_cycle || idle;
                }))
                .Transform(t => t.condition)
                .AutoRefresh(c => c.State)
                .AsObservableCache();

            _AllConditionsTrue = FilteredConditions.Connect()
                .TrueForAll(c => c.StateChanged, state => state.Valid)
                .ToProperty(this, v => v.AllConditionsTrue);

            _AllConditionsFalse = FilteredConditions.Connect()
                .TrueForAll(c => c.StateChanged, state => !state.Valid)
                .ToProperty(this, v => v.AllConditionsFalse);

            _TitleText = is_idle.Select(i => FindTitleText(i))
                .ToProperty(this, v => v.TitleText);

            _OperationText = is_idle.Select(i => FindOperationText(i))
                .ToProperty(this, v => v.OperationText);

            var can_cancel = CanCancelOperation();

            var can_execute = Observable.CombineLatest(
                is_idle,
                CanExecuteOperation(),
                this.WhenAnyValue(v => v.AllConditionsTrue),
                (is_idle, execute, conditions) => is_idle && execute && conditions);

            var tool_key = Flux.ConnectionProvider.GetArrayUnit(m => m.TEMP_TOOL, Feeder.Position);
            if (!tool_key.HasValue)
                return;

            _CurrentTemperature = Flux.ConnectionProvider.ObserveVariable(m => m.TEMP_TOOL, tool_key.Value.Alias)
                .ObservableOrDefault()
                .ToProperty(this, v => v.CurrentTemperature);

            _TemperaturePercentage = this.WhenAnyValue(v => v.CurrentTemperature)
                .ConvertOr(t => t.Percentage, () => 0)
                .ToProperty(this, v => v.TemperaturePercentage);

            CancelOperationCommand = ReactiveCommand.CreateFromTask(async () => { await CancelOperationAsync(); }, can_cancel);
            ExecuteOperationCommand = ReactiveCommand.CreateFromTask(async () => { await ExecuteOperationAsync(); }, can_execute);
        }

        public abstract Task UpdateNFCAsync();

        protected abstract Task<bool> CancelOperationAsync();
        protected abstract IObservable<bool> CanCancelOperation();

        protected abstract Task<bool> ExecuteOperationAsync();
        protected abstract IObservable<bool> FindCanUpdateNFC();
        protected abstract IObservable<bool> CanExecuteOperation();

        protected abstract string FindTitleText(bool idle);
        protected abstract string FindOperationText(bool idle);

        protected abstract IEnumerable<(IConditionViewModel condition, bool filter_on_cycle)> FindConditions();
    }
}
