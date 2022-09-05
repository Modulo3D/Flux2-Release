using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class RRF_VariableObjectModel<TModel, TRData, TWData> : FLUX_VariableGP<RRF_ConnectionProvider, RRF_Connection, TRData, TWData>
    {
        public override string Group => "ObjectModel";

        public RRF_VariableObjectModel(
            RRF_ConnectionProvider connection_provider,
            string name,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
            Func<RRF_Connection, TModel, Optional<TRData>> get_data,
            VariableUnit unit = default) :
            base(connection_provider, name, FluxMemReadPriority.DISABLED,
                read_func: c => read_variable(c, read_model, s => get_data(c, s)),
                unit: unit)
        {
            connection_provider
                .WhenAnyValue(c => c.Connection)
                .Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveModel(get_model, get_data))
                .BindTo(this, v => v.Value);
        }
        public RRF_VariableObjectModel(
            RRF_ConnectionProvider connection_provider,
            string name,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
            Func<RRF_Connection, TModel, Task<Optional<TRData>>> get_data,
            VariableUnit unit = default) :
            base(connection_provider, name, FluxMemReadPriority.DISABLED,
                read_func: c => read_variable(c, read_model, s => get_data(c, s)),
                unit: unit)
        {
            connection_provider
                .WhenAnyValue(c => c.Connection)
                .Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveModel(get_model, get_data))
                .BindTo(this, v => v.Value);
        }
        public RRF_VariableObjectModel(
            RRF_ConnectionProvider connection_provider,
            string name,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
            Func<RRF_Connection, TModel, Optional<TRData>> get_data,
            Func<RRF_Connection, TWData, bool> write_data = default,
            VariableUnit unit = default) :
            base(connection_provider, name, FluxMemReadPriority.DISABLED, unit: unit,
                read_func: c => read_variable(c, read_model, s => get_data(c, s)),
                write_func: (c, d) => Task.FromResult(write_data?.Invoke(c, d) ?? false))
        {
            connection_provider
                .WhenAnyValue(c => c.Connection)
                .Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveModel(get_model, get_data))
                .BindTo(this, v => v.Value);
        }
        public RRF_VariableObjectModel(
            RRF_ConnectionProvider connection_provider,
            string name,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
            Func<RRF_Connection, TModel, Task<Optional<TRData>>> get_data,
            Func<RRF_Connection, TWData, bool> write_data = default,
            VariableUnit unit = default) :
            base(connection_provider, name, FluxMemReadPriority.DISABLED, unit: unit,
                read_func: c => read_variable(c, read_model, s => get_data(c, s)),
                write_func: (c, d) => Task.FromResult(write_data?.Invoke(c, d) ?? false))
        {
            connection_provider
                .WhenAnyValue(c => c.Connection)
                .Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveModel(get_model, get_data))
                .BindTo(this, v => v.Value);
        }
        public RRF_VariableObjectModel(
            RRF_ConnectionProvider connection_provider,
            string name,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
            Func<RRF_Connection, TModel, Optional<TRData>> get_data,
            Func<RRF_Connection, TWData, Task<bool>> write_data = default,
            VariableUnit unit = default) :
            base(connection_provider, name, FluxMemReadPriority.DISABLED, unit: unit,
                read_func: c => read_variable(c, read_model, s => get_data(c, s)),
                write_func: write_data)
        {
            connection_provider
                .WhenAnyValue(c => c.Connection)
                .Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveModel(get_model, get_data))
                .BindTo(this, v => v.Value);
        }
        public RRF_VariableObjectModel(
            RRF_ConnectionProvider connection_provider,
            string name,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
            Func<RRF_Connection, TModel, Task<Optional<TRData>>> get_data,
            Func<RRF_Connection, TWData, Task<bool>> write_data = default,
            VariableUnit unit = default) :
            base(connection_provider, name, FluxMemReadPriority.DISABLED, unit: unit,
                read_func: c => read_variable(c, read_model, s => get_data(c, s)), 
                write_func: write_data)
        {
            connection_provider
                .WhenAnyValue(c => c.Connection)
                .Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveModel(get_model, get_data))
                .BindTo(this, v => v.Value);
        }

        static async Task<Optional<TRData>> read_variable(RRF_Connection c, Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model, Func<TModel, Optional<TRData>> get_data)
        {
            var model = await read_model(c.MemoryBuffer);
            if (!model.HasValue)
                return default;
            return get_data(model.Value);
        }
        static async Task<Optional<TRData>> read_variable(RRF_Connection c, Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model, Func<TModel, Task<Optional<TRData>>> get_data)
        {
            var model = await read_model(c.MemoryBuffer);
            if (!model.HasValue)
                return default;
            return await get_data(model.Value);
        }
    }

   

    public interface IRRF_VariableGlobalModel : IFLUX_Variable
    {
        bool Stored { get; }
        string Variable { get; }
        string LoadVariableMacro { get; }
        Task<bool> CreateVariableAsync(CancellationToken ct);
    }

    public class RRF_VariableGlobalModel<TData> : FLUX_VariableGP<RRF_ConnectionProvider, RRF_Connection, TData, TData>, IRRF_VariableGlobalModel
    {
        public bool Stored { get; }
        public string Variable { get; }
        public TData DefaultValue { get; }
        public override string Group => "Global";
        public string LoadVariableMacro => $"load_{Variable}.g";

        public RRF_VariableGlobalModel(
            RRF_ConnectionProvider connection_provider,
            string variable,
            bool stored,
            TData default_value,
            Func<object, TData> convert_data = default)
            : base(connection_provider, variable, FluxMemReadPriority.DISABLED,
                read_func: c => read_variable(c, variable, convert_data),
                write_func: (c, v) => write_variable(c, variable, v, stored))
        {
            Stored = stored;
            Variable = variable;
            DefaultValue = default_value;
            connection_provider
                .WhenAnyValue(c => c.Connection)
                .Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveGlobalModel(m => get_data(m, variable, convert_data)))
                .BindTo(this, v => v.Value);
        }

        static string sanitize_value(TData value)
        {
            return typeof(TData) == typeof(string) ? $"\"{value}\"" : $"{value:0.00}"
                .ToLower()
                .Replace(',', '.');
        }

        static Optional<TData> get_data(RRF_ObjectModelGlobal global, string variable, Func<object, TData> convert_data)
        {
            return global.Lookup(variable)
                .Convert(v => convert_data != null ? convert_data(v) : v.ToObject<TData>());
        }

        static async Task<Optional<TData>> read_variable(RRF_Connection connection, string variable, Func<object, TData> convert_data = default)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var global = await connection.MemoryBuffer.GetModelDataAsync<RRF_ObjectModelGlobal>(cts.Token);
            if (!global.HasValue)
                return default;
            return get_data(global.Value, variable, convert_data);
        }

        static async Task<bool> write_variable(RRF_Connection c, string variable, TData v, bool stored)
        {
            var s_value = sanitize_value(v);
            var write_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await c.PostGCodeAsync(new[] { $"set global.{variable} = {s_value}" }, write_cts.Token))
            {
                c.Flux.Messages.LogMessage($"Impossibile scrivere la variabile {variable}", "Errore durante l'esecuzione del gcode", MessageLevel.ERROR, 0);
                return false;
            }

            if (stored)
            {
                var load_var_macro = $"load_{variable}.g";
                var gcode = WriteVariableString(variable, v).ToOptional();
                var put_file_cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                if (!await c.PutFileAsync(c => ((RRF_Connection)c).GlobalPath, load_var_macro, put_file_cts.Token, gcode))
                {
                    c.Flux.Messages.LogMessage($"Impossibile salvare la variabile {variable}", "Errore durante la scrittura del file", MessageLevel.ERROR, 0);
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
                LoadVariableMacro,
                ct, gcode.ToOptional());
        }

        private static IEnumerable<string> WriteVariableString(string variable, TData value)
        {
            var s_value = sanitize_value(value);
            yield return $"if (!exists(global.{variable}))";
            yield return $"    global {variable} = {s_value}";
            yield return $"else";
            yield return $"    set global.{variable} = {s_value}";
        }
    }
}
