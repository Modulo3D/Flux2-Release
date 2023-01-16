using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive;
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
        public string VariableName { get; }
        public IFLUX_VariableBase VariableBase { get; }
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

        public MemoryVariableViewModel(FluxViewModel flux, IFLUX_Variable variable, Optional<List<FLUX_VariableAttribute>> attributes) : base($"{typeof(MemoryVariableViewModel).GetRemoteControlName()}??{variable.Name}{variable.Unit}")
        {
            Flux = flux;
            Variable = variable;

            if (!variable.ReadOnly)
            {
                switch (Variable)
                {
                    case IFLUX_Variable<bool, bool> @bool:
                        SetValueCommand = ReactiveCommand.CreateFromTask(() => SetValueAsync(@bool,
                            b => b ? "1" : "0", s => int.Parse(s) == 1));
                        ToggleValueCommand = ReactiveCommand.CreateFromTask(() => ToggleValueAsync(@bool));
                        break;

                    case IFLUX_Variable<double, double> @double:
                        SetValueCommand = ReactiveCommand.CreateFromTask(() => SetValueAsync(@double,
                            d => $"{d:0.##}".Replace(",", "."), s => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture)));
                        break;

                    case IFLUX_Variable<short, short> @short:
                        SetValueCommand = ReactiveCommand.CreateFromTask(() => SetValueAsync(@short,
                            s => $"{s}", s => short.Parse(s)));
                        break;

                    case IFLUX_Variable<ushort, ushort> @ushort:
                        SetValueCommand = ReactiveCommand.CreateFromTask(() => SetValueAsync(@ushort,
                            s => $"{s}", s => ushort.Parse(s)));
                        break;

                    case IFLUX_Variable<string, string> @string:
                        SetValueCommand = ReactiveCommand.CreateFromTask(() => SetValueAsync(@string,
                            s => s, s => s));
                        break;
                }
            }

            if(attributes.HasValue)
            {
                var position_attribute = attributes.Value.FirstOrOptional(a => a is FLUX_VariablePositionAttribute);
                if(position_attribute.HasValue)
                    SetPositionCommand = ReactiveCommand.CreateFromTask(() => SetPositionAsync((FLUX_VariablePositionAttribute)position_attribute.Value));
            }

            _Value = Variable.IValueChanged
                .ToProperty(this, v => v.Value);
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

                var tb_value = new TextBox("tbValue", "VALORE?", get_str(value.ValueOrDefault()));

                var result = await Flux.ShowSelectionAsync(
                    VariableName, new[] { tb_value });

                if (result != ContentDialogResult.Primary)
                    return;

                value = get_value(tb_value.Value);
                if (!value.HasValue)
                    return;

                await variable.WriteAsync(value.Value);
            }
            catch (Exception ex)
            { 
            }
        }
    }
}
