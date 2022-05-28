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

        private ISourceCache<IConditionViewModel, string> Conditions { get; set; }
        [RemoteContent(true)]
        public IObservableCache<IConditionViewModel, string> FilteredConditions { get; private set; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> CancelOperationCommand { get; private set; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ExecuteOperationCommand { get; private set; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> UpdateNFCCommand { get; private set; }


        private ObservableAsPropertyHelper<bool> _AllConditionsTrue;
        public bool AllConditionsTrue => _AllConditionsTrue.Value;

        private ObservableAsPropertyHelper<bool> _AllConditionsFalse;
        public bool AllConditionsFalse => _AllConditionsFalse.Value;

        private ObservableAsPropertyHelper<bool> _CanUpdateNFC;
        [RemoteOutput(true)]
        public bool CanUpdateNFC => _CanUpdateNFC.Value;

        private ObservableAsPropertyHelper<string> _UpdateNFCText;
        [RemoteOutput(true)]
        public string UpdateNFCText => _UpdateNFCText.Value;

        private ObservableAsPropertyHelper<Optional<double>> _CurrentTemperature;
        [RemoteOutput(true, typeof(TemperatureConverter))]
        public Optional<double> CurrentTemperature => _CurrentTemperature.Value;

        private ObservableAsPropertyHelper<double> _TemperaturePercentage;
        [RemoteOutput(true)]
        public double TemperaturePercentage => _TemperaturePercentage.Value;

        public FeederOperationViewModel(FeederViewModel feeder) : base(feeder.Flux)
        {
            Feeder = feeder; 
        }

        public void Initialize()
        {
            Conditions = new SourceCache<IConditionViewModel, string>(c => c.Name);
            Conditions.Edit(innerList =>
            {
                innerList.AddOrUpdate(FindConditions());
            });

            var is_idle = Feeder.Flux.StatusProvider.IsIdle
                .ValueOrDefault();

            FilteredConditions = Conditions.Connect()
                .AutoRefresh(c => c.State)
                .Filter(is_idle.Select(idle =>
                {
                    return (Func<IConditionViewModel, bool>)filter;
                    bool filter(IConditionViewModel condition) => idle;
                }))
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

            _CanUpdateNFC = Observable.CombineLatest(
                is_idle,
                FindCanUpdateNFC(),
                (is_idle, execute) => is_idle && execute)
                .ToProperty(this, v => v.CanUpdateNFC);

            _UpdateNFCText = FindUpdateNFCText()
                .ToProperty(this, v => v.UpdateNFCText);

            var tool_key = Flux.ConnectionProvider.GetArrayUnit(m => m.TEMP_TOOL, Feeder.Position);
            if (!tool_key.HasValue)
                return;

            _CurrentTemperature = Flux.ConnectionProvider.ObserveVariable(m => m.TEMP_TOOL, tool_key.Value.Alias)
                .ObservableOrDefault()
                .Convert(t => t.Current)
                .ToProperty(this, v => v.CurrentTemperature);

            _TemperaturePercentage = Flux.ConnectionProvider.ObserveVariable(m => m.TEMP_TOOL, tool_key.Value.Alias)
                .ObservableOrDefault()
                .ConvertOr(t =>
                {
                    if (t.Current > t.Target)
                        return 100;
                    return Math.Max(0, Math.Min(100, t.Current / t.Target * 100.0));
                }, () => 0)
                .ToProperty(this, v => v.TemperaturePercentage);

            UpdateNFCCommand = ReactiveCommand.CreateFromTask(UpdateNFCAsync, this.WhenAnyValue(v => v.CanUpdateNFC));
            CancelOperationCommand = ReactiveCommand.CreateFromTask(async () => { await CancelOperationAsync(); }, can_cancel);
            ExecuteOperationCommand = ReactiveCommand.CreateFromTask(async () => { await ExecuteOperationAsync(); }, can_execute);
        }

        public abstract Task UpdateNFCAsync();

        protected abstract Task<bool> CancelOperationAsync();
        protected abstract IObservable<bool> CanCancelOperation();
        protected abstract IObservable<string> FindUpdateNFCText();

        protected abstract Task<bool> ExecuteOperationAsync();
        protected abstract IObservable<bool> FindCanUpdateNFC();
        protected abstract IObservable<bool> CanExecuteOperation();

        protected abstract string FindTitleText(bool idle);
        protected abstract string FindOperationText(bool idle);

        protected abstract IEnumerable<IConditionViewModel> FindConditions();
    }
}
