using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public enum BoolSelection : uint
    {
        False = 0,
        True = 1,
    }

    public interface IMemoryVariableBase : IRemoteControl
    {
        string VariableName { get; }
        IFLUX_VariableBase VariableBase { get; }
    }

    public class MemoryVariableViewModel : RemoteControl<MemoryVariableViewModel>, IMemoryVariableBase
    {
        public FluxViewModel Flux { get; }
        public IFLUX_Variable Variable { get; }
        public IFLUX_VariableBase VariableBase => Variable;

        [RemoteOutput(false)]
        public string VariableName => Variable.Name;

        [RemoteCommand]
        public Optional<ReactiveCommand<Unit, Unit>> SetValueCommand { get; }

        [RemoteCommand]
        public Optional<ReactiveCommand<Unit, Unit>> ToggleValueCommand { get; }

        [RemoteCommand]
        public Optional<ReactiveCommand<Unit, Unit>> SetPositionCommand { get; }

        private readonly ObservableAsPropertyHelper<Optional<object>> _Value;
        [RemoteOutput(true, typeof(MemoryConverter))]
        public Optional<object> Value => _Value.Value;

        private readonly ObservableAsPropertyHelper<bool> _HasValue;
        [RemoteOutput(true)]
        public bool HasValue => _HasValue.Value;

        public MemoryVariableViewModel(FluxViewModel flux, IFLUX_Variable variable, Optional<List<FLUX_VariableAttribute>> attributes)
            : base($"{typeof(MemoryVariableViewModel).GetRemoteElementClass()};{variable.Name}")
        {
            Flux = flux;
            Variable = variable;

            if (!variable.ReadOnly)
            {
                switch (Variable)
                {
                    case IFLUX_Variable<bool, bool> @bool:
                        SetValueCommand = ReactiveCommandRC.CreateFromTask(() => SetValueAsync(@bool,
                            b => b ? "1" : "0", s => int.Parse(s) == 1), this);
                        ToggleValueCommand = ReactiveCommandRC.CreateFromTask(() => ToggleValueAsync(@bool), this);
                        break;

                    case IFLUX_Variable<double, double> @double:
                        SetValueCommand = ReactiveCommandRC.CreateFromTask(() => SetValueAsync(@double,
                            d => $"{d:0.##}".Replace(",", "."), s => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture)), this);
                        break;

                    case IFLUX_Variable<short, short> @short:
                        SetValueCommand = ReactiveCommandRC.CreateFromTask(() => SetValueAsync(@short,
                            s => $"{s}", s => short.Parse(s)), this);
                        break;

                    case IFLUX_Variable<ushort, ushort> @ushort:
                        SetValueCommand = ReactiveCommandRC.CreateFromTask(() => SetValueAsync(@ushort,
                            s => $"{s}", s => ushort.Parse(s)), this);
                        break;

                    case IFLUX_Variable<string, string> @string:
                        SetValueCommand = ReactiveCommandRC.CreateFromTask(() => SetValueAsync(@string,
                            s => s, s => s), this);
                        break;
                }
            }

            if(attributes.HasValue)
            {
                var position_attribute = attributes.Value.FirstOrOptional(a => a is FLUX_VariablePositionAttribute);
                if(position_attribute.HasValue)
                    SetPositionCommand = ReactiveCommandRC.CreateFromTask(() => SetPositionAsync((FLUX_VariablePositionAttribute)position_attribute.Value), this);
            }

            _Value = Variable.IValueChanged
                .ToPropertyRC(this, v => v.Value);

            _HasValue = variable.IValueChanged
                .Select(v => v.HasValue)
                .ToPropertyRC(this, v => v.HasValue);
        }

        private async Task SetPositionAsync(FLUX_VariablePositionAttribute position_attribute)
        {
            if (Variable is not IFLUX_Variable<double, double> @double)
                return;

            var axis_position = await Flux.ConnectionProvider.ReadVariableAsync(c => c.AXIS_POSITION);
            if (!axis_position.HasValue)
                return;

            var variable_store = Flux.ConnectionProvider.VariableStoreBase;
            var transformed_position = variable_store.MoveTransform.InverseTransformPosition(axis_position.Value, false);
            if (!transformed_position.HasValue)
                return;

            var variable_position = transformed_position.Value.Axes.Dictionary.Lookup(position_attribute.Axis);
            if(!variable_position.HasValue) 
                return;

            var position = double.Round(variable_position.Value, 2);
            await @double.WriteAsync(position);
        }

        private async Task ToggleValueAsync(IFLUX_Variable<bool, bool> variable)
        {
            try
            {
                Optional<bool> value = await variable.ReadAsync();
                if (!value.HasValue)
                    return;

                await variable.WriteAsync(!value.Value);
            }
            catch (Exception ex)
            {
            }
        }

        private async Task SetValueAsync<TData>(IFLUX_Variable<TData, TData> variable, Func<TData, string> get_str, Func<string, Optional<TData>> get_value)
        {
            try
            {
                Optional<TData> value = await variable.ReadAsync();
                var result = await Flux.ShowDialogAsync(f => new SetVariableDialog(f, value.Convert(get_str), variable));
                if (result.result != DialogResult.Primary || !result.data.HasValue)
                    return;

                value = get_value(result.data.Value);
                if (!value.HasValue)
                    return;

                await variable.WriteAsync(value.Value);
            }
            catch (Exception ex)
            { 
            }
        }
    }

    public class SetVariableDialog : InputDialog<SetVariableDialog, string>
    {
        private Optional<string> _Value;
        [RemoteInput()]
        public Optional<string> Value
        {
            get => _Value;
            set => this.RaiseAndSetIfChanged(ref _Value, value);
        }
        public SetVariableDialog(IFlux flux, Optional<string> startValue, IFLUX_Variable variable) : base(flux, startValue, new RemoteText(variable.Name, false))
        {
            Value = startValue;
        }
        public override Optional<string> Confirm() => Value;
    }
}
