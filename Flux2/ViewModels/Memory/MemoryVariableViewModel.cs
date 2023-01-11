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
        public Optional<ReactiveCommand<Unit, Unit>> SetPositionCommand { get; }

        private readonly ObservableAsPropertyHelper<Optional<object>> _Value;
        [RemoteOutput(true, typeof(MemoryConverter))]
        public Optional<object> Value => _Value.Value;

        public MemoryVariableViewModel(FluxViewModel flux, IFLUX_Variable variable, Optional<List<FLUX_VariableAttribute>> attributes) : base($"{typeof(MemoryVariableViewModel).GetRemoteControlName()}??{variable.Name}{variable.Unit}")
        {
            Flux = flux;
            Variable = variable;
            SetValueCommand = ReactiveCommand.CreateFromTask(SetValueAsync);

            if (variable == flux.ConnectionProvider.VariableStoreBase.X_MAGAZINE_POS.Value.Variables.Items.FirstOrDefault())
            {
                int i = 0;
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

            var variable_position = axis_position.Value.Axes.Dictionary.Lookup(position_attribute.Axis);
            if(!variable_position.HasValue) 
                return;

            var position = double.Round(variable_position.Value, 2);
            await @double.WriteAsync(position);
        }

        private async Task SetValueAsync()
        {
            switch (Variable)
            {
                case IFLUX_Variable<bool, bool> @bool:
                    var start_value = (uint)@bool.Value.ConvertOr(b => b ? BoolSelection.True : BoolSelection.False, () => BoolSelection.False);
                    var cb_bool_value = ComboOption.Create("cbValue", "VALORE?", Enum.GetValues<BoolSelection>(), b => (uint)b, start_value);

                    var bool_result = await Flux.ShowSelectionAsync(
                        VariableName, new[] { cb_bool_value });

                    if (bool_result == ContentDialogResult.Primary &&
                        cb_bool_value.Value.HasValue)
                        await @bool.WriteAsync(cb_bool_value.Value.Value == BoolSelection.True);
                    break;

                case IFLUX_Variable<double, double> @double:
                    var tb_double_value = new TextBox("tbValue", "VALORE?", $"{@double.Value.ValueOr(() => 0):0.###}".Replace(",", "."));

                    var double_result = await Flux.ShowSelectionAsync(
                        VariableName, new[] { tb_double_value });

                    if (double_result == ContentDialogResult.Primary &&
                        double.TryParse(
                            tb_double_value.Value,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out var double_value))
                        await @double.WriteAsync(double_value);
                    break;

                case IFLUX_Variable<short, short> @short:
                    var tb_short_value = new TextBox("tbValue", "VALORE?", @short.Value.ValueOr(() => (short)0).ToString());

                    var short_result = await Flux.ShowSelectionAsync(
                        VariableName, new[] { tb_short_value });

                    if (short_result == ContentDialogResult.Primary &&
                        short.TryParse(tb_short_value.Value, out var short_value))
                        await @short.WriteAsync(short_value);
                    break;

                case IFLUX_Variable<ushort, ushort> word:
                    var tb_word_value = new TextBox("tbValue", "VALORE?", word.Value.ValueOr(() => (ushort)0).ToString());

                    var word_result = await Flux.ShowSelectionAsync(
                        VariableName, new[] { tb_word_value });

                    if (word_result == ContentDialogResult.Primary &&
                        ushort.TryParse(tb_word_value.Value, out var word_value))
                        await word.WriteAsync(word_value);
                    break;


                case IFLUX_Variable<string, string> @string:
                    var tb_string_value = new TextBox("tbValue", "VALORE?", @string.Value.ValueOr(() => ""));

                    var string_result = await Flux.ShowSelectionAsync(
                        VariableName, new[] { tb_string_value });

                    if (string_result == ContentDialogResult.Primary)
                        await @string.WriteAsync(tb_string_value.Value);
                    break;
            }
        }
    }
}
