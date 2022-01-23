using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using Splat;
using System;
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

        private ObservableAsPropertyHelper<Optional<object>> _Value;
        [RemoteOutput(true)]
        public Optional<object> Value => _Value.Value;


        public MemoryVariableViewModel(FluxViewModel flux, IFLUX_Variable variable) : base($"{typeof(MemoryVariableViewModel).GetRemoteControlName()}??{variable.Name}")
        {
            Flux = flux;
            Variable = variable;
            SetValueCommand = GetValueSetter()
                .Convert(s => ReactiveCommand.CreateFromTask(() => SetValueAsync(s)));

            _Value = Variable.IValueChanged
                .ToProperty(this, v => v.Value);


        }

        private Optional<Func<ContentDialog, bool, Task>> GetValueSetter()
        {
            switch (Variable)
            {
                case IFLUX_Variable<bool, bool> @bool:
                    return Optional.Some((Func<ContentDialog, bool, Task>)set_bool_async);
                    async Task set_bool_async(ContentDialog dialog, bool is_virtual)
                    {
                        var start_value = (uint)@bool.Value.ConvertOr(b => b ? BoolSelection.True : BoolSelection.False, () => BoolSelection.False);
                        var cb_bool_value = ComboOption.Create("cbValue", "VALORE?", Enum.GetValues<BoolSelection>(), b => (uint)b, start_value);
                        dialog.AddContent(cb_bool_value);

                        var bool_result = await dialog.ShowAsync();
                        if (bool_result == ContentDialogResult.Primary)
                        {
                            var value = cb_bool_value.Value.ValueOr(() => BoolSelection.False) == BoolSelection.True;
                            if (is_virtual)
                                @bool.SetMemoryValue(value);
                            else
                                await Flux.ConnectionProvider.WriteVariableAsync(@bool, value);
                        }
                    }

                case IFLUX_Variable<double, double> @double:
                    return Optional.Some((Func<ContentDialog, bool, Task>)set_double_async);
                    async Task set_double_async(ContentDialog dialog, bool is_virtual)
                    { 
                        var tb_double_value = new TextBox("tbValue", "VALORE?", @double.Value.ValueOr(() => 0).ToString());
                        dialog.AddContent(tb_double_value);

                        var double_result = await dialog.ShowAsync();
                        if (double_result == ContentDialogResult.Primary && double.TryParse(tb_double_value.Text, out var double_value))
                        {
                            if (is_virtual)
                                @double.SetMemoryValue(double_value);
                            else
                                await Flux.ConnectionProvider.WriteVariableAsync(@double, double_value);
                        }
                    }

                case IFLUX_Variable<short, short> @short:
                    return Optional.Some((Func<ContentDialog, bool, Task>)set_short_async);
                    async Task set_short_async(ContentDialog dialog, bool is_virtual)
                    {
                        var tb_short_value = new TextBox("tbValue", "VALORE?", @short.Value.ValueOr(() => (short)0).ToString());
                        dialog.AddContent(tb_short_value);

                        var short_result = await dialog.ShowAsync();
                        if (short_result == ContentDialogResult.Primary && short.TryParse(tb_short_value.Text, out var short_value))
                        {
                            if (is_virtual)
                                @short.SetMemoryValue(short_value);
                            else
                                await Flux.ConnectionProvider.WriteVariableAsync(@short, short_value);
                        }
                    }

                case IFLUX_Variable<ushort, ushort> word:
                    return Optional.Some((Func<ContentDialog, bool, Task>)set_word_async);
                    async Task set_word_async(ContentDialog dialog, bool is_virtual)
                    {
                        var tb_word_value = new TextBox("tbValue", "VALORE?", word.Value.ValueOr(() => (ushort)0).ToString());
                        dialog.AddContent(tb_word_value);

                        var word_result = await dialog.ShowAsync();
                        if (word_result == ContentDialogResult.Primary && ushort.TryParse(tb_word_value.Text, out var word_value))
                        {
                            if (is_virtual)
                                word.SetMemoryValue(word_value);
                            else
                                await Flux.ConnectionProvider.WriteVariableAsync(word, word_value);
                        }
                    }

                case IFLUX_Variable<string, string> @string:
                    return Optional.Some((Func<ContentDialog, bool, Task>)set_string_async);
                    async Task set_string_async(ContentDialog dialog, bool is_virtual)
                    {
                        var tb_named_string_value = new TextBox("tbValue", "VALORE?", @string.Value.ValueOr(() => ""));
                        dialog.AddContent(tb_named_string_value);

                        var named_string_result = await dialog.ShowAsync();
                        if (named_string_result == ContentDialogResult.Primary)
                        {
                            if (is_virtual)
                                @string.SetMemoryValue(tb_named_string_value.Text);
                            else
                                await Flux.ConnectionProvider.WriteVariableAsync(@string, tb_named_string_value.Text);
                        }
                    }

                default:
                    return default;
            }
        }

        private async Task SetValueAsync(Func<ContentDialog, bool, Task> show_content_dialog)
        {
            try
            {
               using var dialog = new ContentDialog(Flux, Variable.Name, default, () => { }, () => { });

                var cb_virtual_memory = ComboOption.Create("cbVirtual", "MEMORIA VIRTUALE?", Enum.GetValues<BoolSelection>(), b => (uint)b);
                dialog.AddContent(cb_virtual_memory);
                var is_virtual = cb_virtual_memory.Value.ValueOr(() => BoolSelection.False) == BoolSelection.True;

                await show_content_dialog(dialog, is_virtual);
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
            }
        }
    }
}
