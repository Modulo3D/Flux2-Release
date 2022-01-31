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
using System.Text;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public abstract class DialogOption<TViewModel, TValue> : RemoteControl<TViewModel>, IDialogOption<TValue>
        where TViewModel : DialogOption<TViewModel, TValue>
    {
        [RemoteOutput(false)]
        public string Title { get; }
        [RemoteOutput(true)]
        public abstract TValue Value { get; set; }
        public DialogOption(string name, string title) : base($"{typeof(TViewModel).GetRemoteControlName()}??{name}")
        {
            Title = title;
        }
    }

    public class ComboOption<TValue, TKey> : DialogOption<ComboOption<TValue, TKey>, Optional<TValue>>
    {
        public SelectableCache<TValue, TKey> Items { get; }
        [RemoteOutput(true)]
        public override Optional<TValue> Value
        {
            get => Items.SelectedValue;
            set => throw new Exception();
        }
        public ComboOption(string name, string title, IObservableCache<TValue, TKey> items_source, Optional<TKey> key = default, Action<Optional<TKey>> selection_changed = default, Type converter = default) : base(name, title)
        {
            Items = SelectableCache.Create(items_source.Connect().Transform(v => v.ToOptional()))
                .DisposeWith(Disposables);

            if (key.HasValue)
                Items.StartAutoSelect(q => q.KeyValues.FirstOrOptional(kvp => kvp.Key.Equals(key.Value)).Convert(kvp => kvp.Key));
            else
                Items.StartAutoSelect(q => q.KeyValues.FirstOrOptional(kvp => kvp.Value.HasValue).Convert(kvp => kvp.Key));

            Items.SelectedKeyChanged
                .Subscribe(v => selection_changed?.Invoke(v))
                .DisposeWith(Disposables);

            AddInput("items", Items, converter);
        }
    }

    public static class ComboOption
    {
        public static ComboOption<TValue, TKey> Create<TValue, TKey>(string name, string title, IObservableCache<TValue, TKey> items_source, Optional<TKey> key = default, Action<Optional<TKey>> selection_changed = default, Type converter = default)
        {
            return new ComboOption<TValue, TKey>(name, title, items_source, key, selection_changed, converter: converter);
        }
        public static ComboOption<TValue, TKey> Create<TValue, TKey>(string name, string title, IEnumerable<TValue> items_source, Func<TValue, TKey> add_key, Optional<TKey> key = default, Action<Optional<TKey>> selection_changed = default, Type converter = default)
        {
            return new ComboOption<TValue, TKey>(name, title, items_source.AsObservableChangeSet().AddKey(add_key).AsObservableCache(), key, selection_changed, converter: converter);
        }
        public static ComboOption<T, T> Create<T>(string name, string title, IEnumerable<T> items_source, Optional<T> key = default, Action<Optional<T>> selection_changed = default, Type converter = default)
        {
            return new ComboOption<T, T>(name, title, items_source.AsObservableChangeSet(p => p).AsObservableCache(), key, selection_changed, converter: converter);
        }
    }

    public class NumericOption : DialogOption<NumericOption, double>
    {
        public double Min
        {
            get => RemoteInputs.Lookup("value").ConvertOr(v => v.Min, () => double.MinValue);
            set => RemoteInputs.Lookup("value").IfHasValue(v => v.Min = value);
        }

        public double Max
        {
            get => RemoteInputs.Lookup("value").ConvertOr(v => v.Max, () => double.MaxValue);
            set => RemoteInputs.Lookup("value").IfHasValue(v => v.Max = value);
        }

        public Optional<double> Step
        {
            get => RemoteInputs.Lookup("value").Convert(v => v.Step);
            set => RemoteInputs.Lookup("value").IfHasValue(v => v.Step = value);
        }

        private double _Value;
        [RemoteOutput(true)]
        public override double Value
        {
            get => _Value;
            set => this.RaiseAndSetIfChanged(ref _Value, value);
        }

        public NumericOption(string name, string title, double value, double step, double min = double.MinValue, double max = double.MaxValue, Type converter = default) : base(name, title)
        {
            Min = min;
            Max = max;
            Step = step;
            Value = value;
            AddInput("value", this.WhenAnyValue(v => v.Value), SetValue, Observable.Return(new List<InputOption>()), step, converter: converter);
        }
        void SetValue(double value) => Value = value;
    }

    public class ContentDialog : RemoteControl<ContentDialog>, IContentDialog
    {
        public FluxViewModel Flux { get; }

        [RemoteOutput(false)]
        public string Title { get; }

        [RemoteCommand()]
        public Optional<ReactiveCommand<Unit, Unit>> CloseCommand { get; }

        [RemoteCommand()]
        public Optional<ReactiveCommand<Unit, Unit>> ConfirmCommand { get; }

        [RemoteCommand()]
        public Optional<ReactiveCommand<Unit, Unit>> CancelCommand { get; }

        private ObservableAsPropertyHelper<bool> _IsShown;
        public bool IsShown => _IsShown.Value;

        public ContentDialogResult Result { get; private set; }
        public TaskCompletionSource<ContentDialogResult> ShowAsyncSource { get; private set; }

        public ContentDialog(FluxViewModel flux, string title, Action close = default, Action confirm = default, Action cancel = default) : base("dialog")
        {
            Flux = flux;
            Title = title;

            if (close != null)
            {
                CloseCommand = ReactiveCommand.Create(() =>
                {
                    Result = ContentDialogResult.None;
                    close();
                    Hide();
                }).DisposeWith(Disposables);
            }

            if (confirm != null)
            {
                ConfirmCommand = ReactiveCommand.Create(() =>
                {
                    Result = ContentDialogResult.Primary;
                    confirm();
                    Hide();
                }).DisposeWith(Disposables);
            }

            if (cancel != null)
            {
                CancelCommand = ReactiveCommand.Create(() =>
                {
                    Result = ContentDialogResult.Secondary;
                    cancel();
                    Hide();
                }).DisposeWith(Disposables);
            }

            _IsShown = Flux.RemoteContents.Connect()
                .WatchOptional(Name)
                .Select(c => c.HasValue)
                .ToProperty(this, v => v.IsShown)
                .DisposeWith(Disposables);

            ShowAsyncSource = new TaskCompletionSource<ContentDialogResult>();
        }

        public async Task<ContentDialogResult> ShowAsync()
        {
            Flux.AddContent(this);
            var result = await ShowAsyncSource.Task;
            await Observable.CombineLatest(
                CloseCommand.ConvertOr(c => c.IsExecuting, () => Observable.Return(false)),
                CancelCommand.ConvertOr(c => c.IsExecuting, () => Observable.Return(false)),
                ConfirmCommand.ConvertOr(c => c.IsExecuting, () => Observable.Return(false)))
                .PairWithPreviousValue()
                .FirstOrDefaultAsync(e => (e.OldValue?.Any(e => e) ?? false) && (e.NewValue?.All(e => !e) ?? false));
            return result;
        }

        public void Hide()
        {
            ShowAsyncSource.SetResult(Result);
            Flux.RemoteContents.RemoveKey(Name);
        }
    }

    public class ProgressBar : DialogOption<ProgressBar, double>
    {
        private double _Value;
        [RemoteOutput(true)]
        public override double Value
        {
            get => _Value;
            set => this.RaiseAndSetIfChanged(ref _Value, value);
        }

        public ProgressBar(string name, string title) : base(name, title)
        {
        }
    }

    public class TextBlock : DialogOption<TextBlock, string>
    {
        [RemoteOutput(false)]
        public override string Value
        {
            get => Title;
            set => throw new Exception();
        }

        public TextBlock(string name, string text) : base(name, text)
        {
        }
    }

    public class TextBox : DialogOption<TextBox, string>
    {
        private string _Value;
        [RemoteInput]
        public override string Value
        {
            get => _Value;
            set => this.RaiseAndSetIfChanged(ref _Value, value);
        }
        [RemoteOutput(false)]
        public bool Multiline { get; }

        public TextBox(string name, string title, string text, bool multiline = false) : base(name, title)
        {
            Value = text;
            Multiline = multiline;
        }
    }

    public static class FluxColors
    {
        public static string Selected = "#00B189";
        public static string Warning = "#fec02f";
        public static string Error = "#f75a5c";
        public static string Inactive = "#AAA";
        public static string Active = "#FFF";
        public static string Empty = "#444";
    }
}
