using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class RRF_ArrayObjectModel<TModel, TRData, TWData> : FLUX_Array<TRData, TWData>
    {
        public override string Group => "ObjectModel";
        public RRF_ArrayObjectModel(
            string name,
            VariableUnits variable_units,
            Func<VariableUnit, Optional<RRF_VariableObjectModel<TModel, TRData, TWData>>> get_variable)
            : base(name)
        {
            var source_cache = (SourceCache<IFLUX_Variable<TRData, TWData>, VariableUnit>)Variables;
            foreach (var unit in variable_units)
            {
                var variable = get_variable(unit.Key);
                if (!variable.HasValue)
                    continue;
                source_cache.AddOrUpdate(variable.Value);
            }
        }
    }

    public class RRF_ArrayVariableGlobalModel<TData> : FLUX_ObservableVariable<RRF_ConnectionProvider, TData, TData>, IRRF_VariableGlobalModel
    {
        public bool Stored { get; }
        public string Variable { get; }
        public TData DefaultValue { get; }
        public override string Group => "Global";
        public string LoadVariableMacro => $"load_{Variable}_{Unit.Address}.g";

        public RRF_ArrayVariableGlobalModel(
            RRF_ConnectionProvider connection_provider,
            string name,
            VariableUnit unit,
            bool stored,
            TData default_value,
            Func<object, TData> convert_data = default)
            : base(
                connection_provider,
                $"{name} {unit.Alias}",
                observe_func: c => observe_func(c, name.ToLower(), unit, convert_data),
                read_func: c => read_variable(c, name.ToLower(), unit, convert_data),
                write_func: (c, v) => write_variable(c, name.ToLower(), unit, v, stored),
                unit: unit)
        {
            Stored = stored;
            Variable = name.ToLower();
            DefaultValue = default_value;
        }

        static string sanitize_value(TData value)
        {
            return typeof(TData) == typeof(string) || typeof(TData) == typeof(CardId) ? $"\"{value}\"" : $"{value:0.00}"
                 .ToLower()
                 .Replace(',', '.');
        }

        static IObservable<Optional<TData>> observe_func(RRF_ConnectionProvider connection, string variable, VariableUnit unit, Func<object, TData> convert_data)
        {
            return connection.MemoryBuffer
                .ObserveGlobalModel(m => get_data(m, variable, unit, convert_data));
        }

        static Optional<TData> get_data(RRF_ObjectModelGlobal global, string variable, VariableUnit unit, Func<object, TData> convert_data)
        {
            return global.Lookup($"{variable}_{unit.Address}")
                .Convert(v => convert_data != null ? convert_data(v) : v.ToObject<TData>());
        }

        static async Task<ValueResult<TData>> read_variable(RRF_ConnectionProvider connection, string variable, VariableUnit unit, Func<object, TData> convert_data = default)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var global = await connection.MemoryBuffer.GetModelDataAsync(m => m.Global, cts.Token);
            if (!global.HasValue)
                return default;
            return get_data(global.Value, variable, unit, convert_data);
        }

        static async Task<bool> write_variable(RRF_ConnectionProvider connection_provider, string variable, VariableUnit unit, TData v, bool stored)
        {
            var connection = connection_provider.Connection;
            var s_value = sanitize_value(v);
            using var write_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await connection.PostGCodeAsync(new[] { $"set global.{variable}_{unit.Address} = {s_value}" }, write_cts.Token))
            {
                connection_provider.Flux.Messages.LogMessage($"Impossibile scrivere la variabile {variable}_{unit.Address}", "Errore durante l'esecuzione del gcode", MessageLevel.ERROR, 0);
                return false;
            }

            if (stored)
            {
                var load_var_macro = $"load_{variable}_{unit.Address}.g";
                var gcode = WriteVariableString(variable, unit, v).ToOptional();
                using var put_file_cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                if (!await connection_provider.PutFileAsync(c => ((RRF_Connection)c).GlobalPath, load_var_macro, true, put_file_cts.Token, gcode))
                {
                    connection_provider.Flux.Messages.LogMessage($"Impossibile salvare la variabile {variable}_{unit.Address}", "Errore durante la scrittura del file", MessageLevel.ERROR, 0);
                    return false;
                }
            }

            return true;
        }

        public async Task<bool> CreateVariableAsync(CancellationToken ct)
        {
            var gcode = WriteVariableString(Variable, Unit, DefaultValue);

            return await ConnectionProvider.PutFileAsync(
                c => ((RRF_Connection)c).GlobalPath,
                LoadVariableMacro, true,
                ct, gcode.ToOptional());
        }
        private static IEnumerable<string> WriteVariableString(string variable, VariableUnit unit, TData value)
        {
            var s_value = sanitize_value(value);
            yield return $"if (!exists(global.{variable}_{unit.Address}))";
            yield return $"    global {variable}_{unit.Address} = {s_value}";
            yield return $"else";
            yield return $"    set global.{variable}_{unit.Address} = {s_value}";
        }
    }

    public class RRF_ArrayGlobalModel<TData> : FLUX_Array<TData, TData>
    {
        public override string Group => "Global";

        public RRF_ArrayGlobalModel(
            RRF_ConnectionProvider connection_provider,
            string variable,
            bool stored,
            TData default_value,
            VariableUnits variable_units,
            Func<object, TData> convert_data = default)
            : base(variable)
        {
            var source_cache = (SourceCache<IFLUX_Variable<TData, TData>, VariableUnit>)Variables;
            foreach (var unit in variable_units)
                source_cache.AddOrUpdate(get_variable(unit.Key));

            RRF_ArrayVariableGlobalModel<TData> get_variable(VariableUnit unit) => new RRF_ArrayVariableGlobalModel<TData>(connection_provider, variable, unit, stored, default_value, convert_data);
        }
    }
}
