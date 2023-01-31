using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public interface IRRF_Variable : IFLUX_Variable
    {
    }
    public interface IRRF_Variable<TRData, TWData> : IRRF_Variable, IFLUX_Variable<TRData, TWData>
    {
    }
    public interface IRRF_VariableGlobalModel : IRRF_Variable
    {
        bool Stored { get; }
        string Variable { get; }
        string LoadVariableMacro { get; }
        Task<bool> CreateVariableAsync(CancellationToken ct);
    }
    public interface IRRF_VariableGlobalModel<TRData, TWData> : IRRF_VariableGlobalModel, IRRF_Variable<TRData, TWData>
    {
    }
    public class RRF_VariableObjectModel<TModel, TRData, TWData> : FLUX_ObservableVariable<RRF_ConnectionProvider, TRData, TWData>, IRRF_Variable<TRData, TWData>
    {
        public override string Group => "ObjectModel";

        public RRF_VariableObjectModel(
            RRF_ConnectionProvider connection_provider,
            string name,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
            Func<RRF_ConnectionProvider, TModel, Optional<TRData>> get_data,
            Func<RRF_ConnectionProvider, TWData, Task<bool>> write_data = default,
            VariableUnit unit = default) :
            base(connection_provider, name,
                observe_func: c => observe_func(c, get_model, get_data),
                read_func: c => read_variable(c, read_model, s => get_data(c, s)),
                write_func: write_data,
                unit)
        {
        }

        public RRF_VariableObjectModel(
            RRF_ConnectionProvider connection_provider,
            string name,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
            Func<RRF_ConnectionProvider, TModel, Task<Optional<TRData>>> get_data,
            Func<RRF_ConnectionProvider, TWData, Task<bool>> write_data = default,
            VariableUnit unit = default) :
            base(connection_provider, name,
                observe_func: c => observe_func(c, get_model, get_data),
                read_func: c => read_variable(c, read_model, s => get_data(c, s)),
                write_func: write_data,
                unit)
        {
        }

        private static IObservable<Optional<TRData>> observe_func(
            RRF_ConnectionProvider connection_provider,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
            Func<RRF_ConnectionProvider, TModel, Optional<TRData>> get_data)
        {
            return connection_provider.MemoryBuffer.ObserveModel(get_model, get_data);
        }

        private static IObservable<Optional<TRData>> observe_func(
            RRF_ConnectionProvider connection_provider,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
            Func<RRF_ConnectionProvider, TModel, Task<Optional<TRData>>> get_data)
        {
            return connection_provider.MemoryBuffer.ObserveModel(get_model, get_data);
        }

        private static async Task<Optional<TRData>> read_variable(
            RRF_ConnectionProvider connection_provider,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<TModel, Optional<TRData>> get_data)
        {
            var model = await read_model(connection_provider.MemoryBuffer);
            if (!model.HasValue)
                return default;
            return get_data(model.Value);
        }

        private static async Task<Optional<TRData>> read_variable(
            RRF_ConnectionProvider connection_provider,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<TModel, Task<Optional<TRData>>> get_data)
        {
            var model = await read_model(connection_provider.MemoryBuffer);
            if (!model.HasValue)
                return default;
            return await get_data(model.Value);
        }
    }
    public class RRF_VariableGlobalModel<TData> : FLUX_ObservableVariable<RRF_ConnectionProvider, TData, TData>, IRRF_VariableGlobalModel<TData, TData>
    {
        public bool Stored { get; }
        public string Variable { get; }
        public TData DefaultValue { get; }
        public override string Group => "Global";
        public string LoadVariableMacro => $"load_{Variable}.g";
        public override string ToString() => $"global.{Variable.ToLower()}";

        public RRF_VariableGlobalModel(
            RRF_ConnectionProvider connection_provider,
            string name,
            bool stored,
            TData default_value,
            Func<object, TData> convert_data = default)
            : base(connection_provider, name,
                observe_func: c => observe_func(c, name.ToLower(), convert_data),
                read_func: c => read_variable(c, name.ToLower(), convert_data),
                write_func: (c, v) => write_variable(c, name.ToLower(), v, stored))
        {
            Stored = stored;
            Variable = name.ToLower();
            DefaultValue = default_value;
        }

        private static string sanitize_value(TData value)
        {
            return typeof(TData) == typeof(string) || typeof(TData) == typeof(CardId) ? $"\"{value}\"" : $"{value:0.00}"
                .ToLower()
                .Replace(',', '.');
        }

        private static Optional<TData> get_data(RRF_ObjectModelGlobal global, string variable, Func<object, TData> convert_data)
        {
            return global.Lookup(variable)
                .Convert(v => convert_data != null ? convert_data(v) : v.ToObject<TData>());
        }

        private static IObservable<Optional<TData>> observe_func(
            RRF_ConnectionProvider connection_provider,
            string variable, Func<object, TData> convert_data = default)
        {
            return connection_provider.MemoryBuffer.ObserveGlobalModel(m => get_data(m, variable, convert_data));
        }

        private static async Task<Optional<TData>> read_variable(RRF_ConnectionProvider connection_provider, string variable, Func<object, TData> convert_data = default)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var global = await connection_provider.MemoryBuffer.GetModelDataAsync(m => m.Global, cts.Token);
            if (!global.HasValue)
                return default;
            return get_data(global.Value, variable, convert_data);
        }

        private static async Task<bool> write_variable(RRF_ConnectionProvider connection_provider, string variable, TData v, bool stored)
        {
            var connection = connection_provider.Connection;
            var s_value = sanitize_value(v);
            using var write_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await connection.PostGCodeAsync(new[] { $"set global.{variable} = {s_value}" }, write_cts.Token))
            {
                connection_provider.Flux.Messages.LogMessage($"Impossibile scrivere la variabile {variable}", "Errore durante l'esecuzione del gcode", MessageLevel.ERROR, 0);
                return false;
            }

            if (stored)
            {
                var load_var_macro = $"load_{variable}.g";
                var gcode = WriteVariableString(variable, v).ToOptional();
                using var put_file_cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                if (!await connection.PutFileAsync(c => ((RRF_Connection)c).GlobalPath, load_var_macro, true, put_file_cts.Token, gcode))
                {
                    connection_provider.Flux.Messages.LogMessage($"Impossibile salvare la variabile {variable}", "Errore durante la scrittura del file", MessageLevel.ERROR, 0);
                    return false;
                }
            }

            return true;
        }

        public async Task<bool> CreateVariableAsync(CancellationToken ct)
        {
            var gcode = WriteVariableString(Variable, DefaultValue);

            return await ConnectionProvider.PutFileAsync(
                c => ((RRF_Connection)c).GlobalPath,
                LoadVariableMacro, true,
                ct, gcode.ToOptional());
        }

        private static IEnumerable<string> WriteVariableString(string variable, TData value)
        {
            var s_value = sanitize_value(value);
            yield return $"if (!exists(global.{variable}))";
            yield return $"    global {variable} = {s_value}";
        }
    }
}
