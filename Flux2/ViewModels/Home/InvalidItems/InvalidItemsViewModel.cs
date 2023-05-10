using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public interface IInvalidItemViewModel : IRemoteControl
    {
        ushort Position { get; }
        FeederEvaluator Evaluation { get; }
        Optional<string> CurrentValue { get; }
        Optional<string> ExpectedValue { get; }
    }

    public interface IInvalidValueViewModel : IInvalidItemViewModel
    {
        Optional<string> Item { get; }
    }

    [RemoteControl(baseClass: typeof(InvalidItemViewModel<>))]
    public abstract class InvalidItemViewModel<TInvalidItemViewModel> : RemoteControl<TInvalidItemViewModel>, IInvalidItemViewModel
        where TInvalidItemViewModel : InvalidItemViewModel<TInvalidItemViewModel>
    {
        public FeederEvaluator Evaluation { get; }

        [RemoteOutput(false)]
        public ushort Position => Evaluation.Feeder.Position;

        private readonly ObservableAsPropertyHelper<Optional<string>> _CurrentValue;
        [RemoteOutput(true)]
        public Optional<string> CurrentValue => _CurrentValue.Value;

        private readonly ObservableAsPropertyHelper<Optional<string>> _ExpectedValue;
        [RemoteOutput(true)]
        public Optional<string> ExpectedValue => _ExpectedValue.Value;

        [RemoteOutput(true)]
        public abstract string InvalidItemBrush { get; }

        public InvalidItemViewModel(FluxViewModel flux, FeederEvaluator eval)
            : base($"{typeof(TInvalidItemViewModel).GetRemoteElementClass()};{eval.Feeder.Position}")
        {
            Evaluation = eval;

            _CurrentValue = GetCurrentValue(eval)
                .ToProperty(this, e => e.CurrentValue)
                .DisposeWith(Disposables);

            _ExpectedValue = GetExpectedValue(eval)
                .ToProperty(this, e => e.ExpectedValue)
                .DisposeWith(Disposables);
        }

        public abstract IObservable<Optional<string>> GetCurrentValue(FeederEvaluator eval);
        public abstract IObservable<Optional<string>> GetExpectedValue(FeederEvaluator eval);
    }

    [RemoteControl(baseClass: typeof(InvalidValueViewModel<>))]
    public abstract class InvalidValueViewModel<T> : InvalidItemViewModel<T>, IInvalidValueViewModel
        where T : InvalidValueViewModel<T>
    {
        private readonly ObservableAsPropertyHelper<Optional<string>> _Item;
        [RemoteOutput(true)]
        public Optional<string> Item => _Item.Value;

        public InvalidValueViewModel(FluxViewModel flux, FeederEvaluator eval) : base(flux, eval)
        {
            _Item = GetItem(eval)
                .ToProperty(this, e => e.Item)
                .DisposeWith(Disposables);
        }

        public abstract IObservable<Optional<string>> GetItem(FeederEvaluator eval);
    }

    public interface IInvalidFeedersViewModel : IHomePhaseViewModel
    {
        public ReactiveCommandBaseRC<Unit, Unit> ChangeItemsCommand { get; }
    }

    public abstract class InvalidFeedersViewModel<TInvalidFederViewModel> : HomePhaseViewModel<TInvalidFederViewModel>, IInvalidFeedersViewModel
        where TInvalidFederViewModel : InvalidFeedersViewModel<TInvalidFederViewModel>
    {
        [RemoteCommand]
        public ReactiveCommandBaseRC<Unit, Unit> ChangeItemsCommand { get; }

        public InvalidFeedersViewModel(FluxViewModel flux) : base(flux)
        {
            ChangeItemsCommand = ReactiveCommandBaseRC.CreateFromTask(ChangeItemsAsync, (TInvalidFederViewModel)this);
        }

        public abstract Task ChangeItemsAsync();
    }

    public interface IInvalidItemsViewModel : IInvalidFeedersViewModel
    {
        public IObservableList<IInvalidItemViewModel> InvalidItems { get; }
    }

    [RemoteControl(baseClass: typeof(InvalidItemsViewModel<>))]
    public abstract class InvalidItemsViewModel<TInvalidItemsViewModel> : InvalidFeedersViewModel<TInvalidItemsViewModel>, IInvalidItemsViewModel
        where TInvalidItemsViewModel : InvalidItemsViewModel<TInvalidItemsViewModel>
    {
        [RemoteContent(true, comparer: (nameof(IInvalidItemViewModel.Position)))]
        public IObservableList<IInvalidItemViewModel> InvalidItems { get; protected set; }

        public InvalidItemsViewModel(FluxViewModel flux) : base(flux)
        {
        }
    }

    public interface IInvalidValuesViewModel : IInvalidFeedersViewModel
    {
        Optional<ReactiveCommandBaseRC<Unit, Unit>> StartWithInvalidValuesCommand { get; }
        IObservableList<IInvalidValueViewModel> InvalidValues { get; }
        bool CanStartWithInvalidValues { get; }
    }

    [RemoteControl(baseClass: typeof(InvalidValuesViewModel<>))]
    public abstract class InvalidValuesViewModel<TInvalidValuesViewModel> : InvalidFeedersViewModel<TInvalidValuesViewModel>, IInvalidValuesViewModel
        where TInvalidValuesViewModel : InvalidValuesViewModel<TInvalidValuesViewModel>
    {
        [RemoteContent(true, comparer:(nameof(IInvalidValueViewModel.Position)))]
        public IObservableList<IInvalidValueViewModel> InvalidValues { get; protected set; }
        [RemoteCommand]
        public Optional<ReactiveCommandBaseRC<Unit, Unit>> StartWithInvalidValuesCommand { get; protected set; }

        [RemoteOutput(true)]
        public abstract bool CanStartWithInvalidValues { get; }
        [RemoteOutput(true)]
        public abstract bool StartWithInvalidValuesEnabled { get; }

        public InvalidValuesViewModel(FluxViewModel flux) : base(flux)
        {
        }

        public override void Initialize()
        {
            if (CanStartWithInvalidValues)
            { 
                var can_start = this.WhenAnyValue(v => v.StartWithInvalidValuesEnabled);
                StartWithInvalidValuesCommand = ReactiveCommandBaseRC.Create(StartWithInvalidValues, (TInvalidValuesViewModel)this, can_start);
            }
        }

        public abstract void StartWithInvalidValues();
    }
}
