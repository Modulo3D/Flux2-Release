﻿using DynamicData;
using DynamicData.Kernel;
using EmbedIO.Routing;
using Modulo3DNet;
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
    /*public abstract class DialogOption<TViewModel, TValue> : RemoteControl<TViewModel>, IDialogOption<TValue>
        where TViewModel : DialogOption<TViewModel, TValue>
    {
        [RemoteOutput(false)]
        public string Title { get; }
        [RemoteOutput(true)]
        public abstract TValue Value { get; set; }
        [RemoteOutput(true)]
        public abstract bool HasValue { get; }

        public DialogOption(string name, string title) : base($"{name}")
        {
            Title = title;
        }
    }

    public class ComboOption<TValue, TKey> : DialogOption<ComboOption<TValue, TKey>, Optional<TValue>>
    {
        public OptionalSelectableCache<TValue, TKey> Items { get; }
        [RemoteOutput(true)]
        public override Optional<TValue> Value
        {
            get => Items?.SelectedValue.Convert(v => v) ?? default;
            set => throw new Exception();
        }
        private readonly ObservableAsPropertyHelper<bool> _HasValue;
        [RemoteOutput(true)]
        public override bool HasValue => _HasValue.Value;

        public ComboOption(string name, string title, Func<CompositeDisposable, IObservableCache<TValue, TKey>> items_source, Optional<TKey> key = default, Action<Optional<TKey>> selection_changed = default, Type converter = default) : base(name, title)
        {
            Items = OptionalSelectableCache.Create(items_source(Disposables).Connect().Transform(v => v.ToOptional()));

            if (key.HasValue)
                Items.AutoSelect = Observable.Return(key).ToOptional();
            else
                Items.AutoSelect = Items.ItemsChanged.KeyOf(i => i.HasValue).ToOptional();

            Items.SelectedKeyChanged
                .StartWithDefault()
                .SubscribeRC(v => selection_changed?.Invoke(v), this);

            _HasValue = Items.SelectedValueChanged
               .Select(v => v.HasValue)
               .ToPropertyRC(this, v => v.HasValue);

            AddInput("items", Items, converter: converter);
        }
    }

    public static class ComboOption
    {
        public static ComboOption<TValue, TKey> Create<TValue, TKey>(string name, string title, Func<CompositeDisposable, IObservableCache<TValue, TKey>> items_source, Optional<TKey> key = default, Action<Optional<TKey>> selection_changed = default, Type converter = default)
        {
            return new ComboOption<TValue, TKey>(name, title, d => items_source(d), key, selection_changed, converter: converter);
        }
        public static ComboOption<TValue, TKey> Create<TValue, TKey>(string name, string title, IEnumerable<TValue> items_source, Func<TValue, TKey> add_key, Optional<TKey> key = default, Action<Optional<TKey>> selection_changed = default, Type converter = default)
        {
            return new ComboOption<TValue, TKey>(name, title, d => items_source.AsObservableChangeSet().AddKey(add_key).AsObservableCache().DisposeWith(d), key, selection_changed, converter: converter);
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
        private readonly ObservableAsPropertyHelper<bool> _HasValue;
        [RemoteOutput(true)]
        public override bool HasValue => _HasValue.Value;

        public NumericOption(string name, string title, double value, double step, double min = double.MinValue, double max = double.MaxValue, Action<double> value_changed = default, Type converter = default, Func<double, bool> has_value = default) : base(name, title)
        {
            Min = min;
            Max = max;
            Step = step;
            Value = value;
            AddInput("value", this.WhenAnyValue(v => v.Value), SetValue, step: step, converter: converter);

            this.WhenAnyValue(v => v.Value)
                .SubscribeRC(v => value_changed?.Invoke(v), this);

            _HasValue = this.WhenAnyValue(v => v.Value)
               .Select(v => has_value?.Invoke(v) ?? true)
               .ToPropertyRC(this, v => v.HasValue);
        }

        private void SetValue(double value) => Value = value;
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
        [RemoteOutput(false)]
        public override bool HasValue => true;

        public ProgressBar(string name, string title) : base(name, title)
        {
        }
    }

    public class TextBlock : DialogOption<TextBlock, string>
    {
        private string _Value;
        [RemoteOutput(true)]
        public override string Value
        {
            get => _Value;
            set => this.RaiseAndSetIfChanged(ref _Value, value);
        }
        [RemoteOutput(false)]
        public override bool HasValue => true;

        public TextBlock(string name, string text) : base(name, "")
        {
            Value = text;
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
        [RemoteOutput(false)]
        public bool Relaxed { get; }

        private readonly ObservableAsPropertyHelper<bool> _HasValue;
        [RemoteOutput(true)]
        public override bool HasValue => _HasValue.Value;

        public TextBox(string name, string title, string text, bool multiline = false, Func<string, bool> has_value = default) : base(name, title)
        {
            Value = text;
            Multiline = multiline;

            _HasValue = this.WhenAnyValue(v => v.Value)
               .Select(v => has_value?.Invoke(v) ?? true)
               .ToPropertyRC(this, v => v.HasValue);
        }
    }*/

    public static class FluxColors
    {
        public static string Selected = "#00B189";
        public static string Warning = "#fec02f";
        public static string Error = "#f75a5c";
        public static string Inactive = "#AAA";
        public static string Idle = "#275ac3";
        public static string Active = "#FFF";
        public static string Empty = "#444";
    }
}
