using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public static class RRF_StateBuilder
    {
        public class RRF_InnerStateBuilder<TState>
        {
            public IObservable<Optional<RRF_Connection>> Connection { get; }
            public Func<RRF_ObjectModel, IObservable<Optional<TState>>> GetState { get; }

            public RRF_InnerStateBuilder(IObservable<Optional<RRF_Connection>> connection, Func<RRF_ObjectModel, IObservable<Optional<TState>>> get_state)
            {
                Connection = connection;
                GetState = get_state;
            }

            public RRF_VariableObjectModel<TState, TRData, TWData> CreateVariable<TRData, TWData>(
                string name,
                Func<RRF_Connection, TState, Optional<TRData>> get_data,
                VariableUnit unit = default)
            {
                return new RRF_VariableObjectModel<TState, TRData, TWData>(Connection, name, GetState, get_data, unit);
            }
            public RRF_VariableObjectModel<TState, TRData, TWData> CreateVariable<TRData, TWData>(
                string name,
                Func<RRF_Connection, TState, Task<Optional<TRData>>> get_data,
                VariableUnit unit = default)
            {
                return new RRF_VariableObjectModel<TState, TRData, TWData>(Connection, name, GetState, get_data, unit);
            }
            public RRF_VariableObjectModel<TState, TRData, TWData> CreateVariable<TRData, TWData>(
                string name,
                Func<RRF_Connection, TState, Optional<TRData>> get_data,
                Func<RRF_Connection, TWData, bool> write_data,
                VariableUnit unit = default)
            {
                return new RRF_VariableObjectModel<TState, TRData, TWData>(Connection, name, GetState, get_data, write_data, unit);
            }
            public RRF_VariableObjectModel<TState, TRData, TWData> CreateVariable<TRData, TWData>(
                string name,
                Func<RRF_Connection, TState, Task<Optional<TRData>>> get_data,
                Func<RRF_Connection, TWData, bool> write_data,
                VariableUnit unit = default)
            {
                return new RRF_VariableObjectModel<TState, TRData, TWData>(Connection, name, GetState, get_data, write_data, unit);
            }
            public RRF_VariableObjectModel<TState, TRData, TWData> CreateVariable<TRData, TWData>(
                string name,
                Func<RRF_Connection, TState, Optional<TRData>> get_data,
                Func<RRF_Connection, TWData, Task<bool>> write_data,
                VariableUnit unit = default)
            {
                return new RRF_VariableObjectModel<TState, TRData, TWData>(Connection, name, GetState, get_data, write_data, unit);
            }
            public RRF_VariableObjectModel<TState, TRData, TWData> CreateVariable<TRData, TWData>(
                string name,
                Func<RRF_Connection, TState, Task<Optional<TRData>>> get_data,
                Func<RRF_Connection, TWData, Task<bool>> write_data,
                VariableUnit unit = default)
            {
                return new RRF_VariableObjectModel<TState, TRData, TWData>(Connection, name, GetState, get_data, write_data, unit);
            }

            public class RRF_ArrayBuilder<TVariable>
            {
                public RRF_ArrayBuilder(
                    IObservable<Optional<RRF_Connection>> connection,
                    Func<RRF_ObjectModel, IObservable<Optional<TState>>> get_state,
                    Func<TState, Optional<Dictionary<VariableUnit, TVariable>>> get_variables)
                {
                    GetVariables = get_variables;
                    GetState = get_state;
                    Connection = connection;
                }

                public IObservable<Optional<RRF_Connection>> Connection { get; }
                public Func<RRF_ObjectModel, IObservable<Optional<TState>>> GetState { get; }
                public Func<TState, Optional<Dictionary<VariableUnit, TVariable>>> GetVariables { get; }

                public RRF_ArrayObjectModel<TState, TRData, TWData> Create<TRData, TWData>(
                    string name,
                    IEnumerable<VariableUnit> variable_units,
                    Func<RRF_Connection, TVariable, Optional<TRData>> read_data)
                {
                    RRF_VariableObjectModel<TVariable, TRData, TWData> get_variable(VariableUnit unit)
                    {
                        IObservable<Optional<TVariable>> get_variable_state(RRF_ObjectModel m) => GetState(m).Convert(GetVariables).Convert(v => v.Lookup(unit));
                        return new RRF_VariableObjectModel<TVariable, TRData, TWData>(Connection, $"{name} {unit}", get_variable_state, read_data, unit: unit);
                    }

                    return new RRF_ArrayObjectModel<TState, TRData, TWData>(name, variable_units, get_variable);
                }
                public RRF_ArrayObjectModel<TState, TRData, TWData> Create<TRData, TWData>(
                    string name,
                    IEnumerable<VariableUnit> variable_units,
                    Func<RRF_Connection, TVariable, Optional<TRData>> read_data,
                    Func<RRF_Connection, TWData, VariableUnit, bool> write_data)
                {
                    RRF_VariableObjectModel<TVariable, TRData, TWData> get_variable(VariableUnit unit)
                    {
                        bool write_unit_data(RRF_Connection c, TWData d) => write_data(c, d, unit);
                        IObservable<Optional<TVariable>> get_variable_state(RRF_ObjectModel m) => GetState(m).Convert(GetVariables).Convert(v => v.Lookup(unit));
                        return new RRF_VariableObjectModel<TVariable, TRData, TWData>(Connection, $"{name} {unit}", get_variable_state, read_data, write_unit_data, unit);
                    }

                    return new RRF_ArrayObjectModel<TState, TRData, TWData>(name, variable_units, get_variable);
                }
                public RRF_ArrayObjectModel<TState, TRData, TWData> Create<TRData, TWData>(
                    string name,
                    IEnumerable<VariableUnit> variable_units,
                    Func<RRF_Connection, TVariable, Optional<TRData>> read_data,
                    Func<RRF_Connection, TWData, VariableUnit, Task<bool>> write_data)
                {
                    RRF_VariableObjectModel<TVariable, TRData, TWData> get_variable(VariableUnit unit)
                    {
                        Task<bool> write_unit_data(RRF_Connection c, TWData d) => write_data(c, d, unit);
                        IObservable<Optional<TVariable>> get_variable_state(RRF_ObjectModel m) => GetState(m).Convert(GetVariables).Convert(v => v.Lookup(unit));
                        return new RRF_VariableObjectModel<TVariable, TRData, TWData>(Connection, $"{name} {unit}", get_variable_state, read_data, write_unit_data, unit);
                    }

                    return new RRF_ArrayObjectModel<TState, TRData, TWData>(name, variable_units, get_variable);
                }
            }

            public RRF_ArrayBuilder<TList> CreateArray<TList>(Func<TState, Optional<Dictionary<VariableUnit, TList>>> get_list)
                => new RRF_ArrayBuilder<TList>(Connection, GetState, get_list);
            public RRF_ArrayBuilder<TList> CreateArray<TList>(Func<TState, Optional<Dictionary<Optional<VariableUnit>, TList>>> get_list)
                => new RRF_ArrayBuilder<TList>(Connection, GetState, s => get_list(s).Convert(d => d.ToDictionary(kvp => kvp.Key.ValueOr(() => ""), kvp => kvp.Value)));
            public RRF_ArrayBuilder<TList> CreateArray<TList, TKey>(Func<TState, Optional<Dictionary<TKey, TList>>> get_list)
                => new RRF_ArrayBuilder<TList>(Connection, GetState, s => get_list(s).Convert(d => d.ToDictionary(kvp => (VariableUnit)$"{kvp.Key}", kvp => kvp.Value)));
            public RRF_ArrayBuilder<TList> CreateArray<TList, TKey>(Func<TState, Optional<Dictionary<Optional<TKey>, TList>>> get_list)
                => new RRF_ArrayBuilder<TList>(Connection, GetState, s => get_list(s).Convert(d => d.ToDictionary(kvp => (VariableUnit)kvp.Key.ConvertOr(k => $"{k}", () => ""), kvp => kvp.Value)));
        }

        public static RRF_InnerStateBuilder<TState> Create<TState>(IObservable<Optional<RRF_Connection>> connection, Func<RRF_ObjectModel, IObservable<Optional<TState>>> get_state)
            => new RRF_InnerStateBuilder<TState>(connection, get_state);
    }

    public class RRF_VariableObjectModel<TState, TRData, TWData> : FLUX_VariableGP<RRF_Connection, TRData, TWData>
    {

        public override string Group => "ObjectModel";

        public RRF_VariableObjectModel(
            IObservable<Optional<RRF_Connection>> connection,
            string name,
            Func<RRF_ObjectModel, IObservable<Optional<TState>>> get_state,
            Func<RRF_Connection, TState, Optional<TRData>> get_data,
            VariableUnit unit = default) :
            base(connection, name, FluxMemReadPriority.DISABLED, default, unit: unit)
        {
            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveState(get_state, get_data))
                .BindTo(this, v => v.Value);
        }
        public RRF_VariableObjectModel(
            IObservable<Optional<RRF_Connection>> connection,
            string name,
            Func<RRF_ObjectModel, IObservable<Optional<TState>>> get_state,
            Func<RRF_Connection, TState, Task<Optional<TRData>>> get_data,
            VariableUnit unit = default) :
            base(connection, name, FluxMemReadPriority.DISABLED, default, unit: unit)
        {
            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveState(get_state, get_data))
                .BindTo(this, v => v.Value);
        }
        public RRF_VariableObjectModel(
            IObservable<Optional<RRF_Connection>> connection,
            string name,
            Func<RRF_ObjectModel, IObservable<Optional<TState>>> get_state,
            Func<RRF_Connection, TState, Optional<TRData>> get_data,
            Func<RRF_Connection, TWData, bool> write_data = default,
            VariableUnit unit = default) :
            base(connection, name, FluxMemReadPriority.DISABLED, unit: unit, write_func: (c, d) => Task.FromResult(write_data?.Invoke(c, d) ?? false))
        {
            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveState(get_state, get_data))
                .BindTo(this, v => v.Value);
        }
        public RRF_VariableObjectModel(
            IObservable<Optional<RRF_Connection>> connection,
            string name,
            Func<RRF_ObjectModel, IObservable<Optional<TState>>> get_state,
            Func<RRF_Connection, TState, Task<Optional<TRData>>> get_data,
            Func<RRF_Connection, TWData, bool> write_data = default,
            VariableUnit unit = default) :
            base(connection, name, FluxMemReadPriority.DISABLED, unit: unit, write_func: (c, d) => Task.FromResult(write_data?.Invoke(c, d) ?? false))
        {
            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveState(get_state, get_data))
                .BindTo(this, v => v.Value);
        }
        public RRF_VariableObjectModel(
            IObservable<Optional<RRF_Connection>> connection,
            string name,
            Func<RRF_ObjectModel, IObservable<Optional<TState>>> get_state,
            Func<RRF_Connection, TState, Optional<TRData>> get_data,
            Func<RRF_Connection, TWData, Task<bool>> write_data = default,
            VariableUnit unit = default) :
            base(connection, name, FluxMemReadPriority.DISABLED, unit: unit, write_func: write_data)
        {
            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveState(get_state, get_data))
                .BindTo(this, v => v.Value);
        }
        public RRF_VariableObjectModel(
            IObservable<Optional<RRF_Connection>> connection,
            string name,
            Func<RRF_ObjectModel, IObservable<Optional<TState>>> get_state,
            Func<RRF_Connection, TState, Task<Optional<TRData>>> get_data,
            Func<RRF_Connection, TWData, Task<bool>> write_data = default,
            VariableUnit unit = default) :
            base(connection, name, FluxMemReadPriority.DISABLED, unit: unit, write_func: write_data)
        {
            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveState(get_state, get_data))
                .BindTo(this, v => v.Value);
        }
    }

    public class RRF_ArrayObjectModel<TVariable, TRData, TWData> : FLUX_Array<TRData, TWData>
    {
        public override string Group => "ObjectModel";
        public new ISourceCache<IFLUX_Variable<TRData, TWData>, VariableUnit> Variables { get; }
        public RRF_ArrayObjectModel(string name, IEnumerable<VariableUnit> variable_units, Func<VariableUnit, IFLUX_Variable<TRData, TWData>> get_variable)
            : base(name, FluxMemReadPriority.DISABLED)
        {
            Variables = new SourceCache<IFLUX_Variable<TRData, TWData>, VariableUnit>(v => v.Unit.ValueOr(() => ""));
            base.Variables = Variables;

            foreach (var unit in variable_units)
                Variables.AddOrUpdate(get_variable(unit));
        }

        public override VariableUnit GetArrayUnit(ushort position)
        {
            return Variables.Items
                .ElementAtOrDefault(position)
                .ToOptional(v => v != null)
                .Convert(v => v.Unit)
                .ValueOr(() => $"{position}");
        }
    }

    public interface IRRF_VariableGlobalModel : IFLUX_Variable
    {
        bool Stored { get; }
        string Variable { get; }
        string LoadVariableMacro { get; }
        Task<bool> InitializeVariableAsync();
    }

    public class RRF_VariableGlobalModel<TData> : FLUX_VariableGP<RRF_Connection, TData, TData>, IRRF_VariableGlobalModel
    {
        public bool Stored { get; }
        public string Variable { get; }
        public override string Group => "Global";
        public string LoadVariableMacro => $"load_{Variable}.g";

        public RRF_VariableGlobalModel(FluxViewModel flux, IObservable<Optional<RRF_Connection>> connection, string variable, bool stored, Func<object, TData> convert_data = default)
            : base(connection, variable, FluxMemReadPriority.DISABLED, write_func: async (c, v) =>
            {

                var s_value = sanitize_value(v);
                var write_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                if (!await c.PostGCodeAsync($"set global.{variable} = {s_value}", write_cts.Token))
                {
                    flux.Messages.LogMessage("Impossibile scrivere la variabile", "Errore durante l'esecuzione del gcode", MessageLevel.ERROR, 0);
                    return false;
                }

                if (stored)
                { 
                    var load_var_macro = $"load_{variable}.g";
                    var gcode = WriteVariableString(variable, v).ToOptional();
                    var put_file_cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    if (!await c.PutFileAsync(c => ((RRF_Connection)c).GlobalPath, load_var_macro, put_file_cts.Token, gcode))
                    {
                        flux.Messages.LogMessage("Impossibile salvare la variabile", "Errore durante la scrittura del file", MessageLevel.ERROR, 0);
                        return false;
                    }
                }

                return true;
            })
        {
            Stored = stored;
            Variable = variable;
            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveGlobalState(m => get_data(m, variable, convert_data)))
                .BindTo(this, v => v.Value);
        }

        static string sanitize_value(TData value) => typeof(TData) == typeof(string) ? $"\"{value}\"" : $"{value:0.00}".ToLower().Replace(',', '.');
        Optional<TData> get_data(Optional<Dictionary<string, object>> global, string variable, Func<object, TData> convert_data) => 
            global.Convert(g => g.Lookup(variable)).Convert(v => convert_data != null ? convert_data(v) : (TData)Convert.ChangeType(v, typeof(TData), CultureInfo.InvariantCulture));

        public async Task<bool> InitializeVariableAsync() 
        {
            var gcode = WriteVariableString(Variable, default).ToOptional();
            var put_file_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await Connection.ConvertOrAsync(c => c.PutFileAsync(c => ((RRF_Connection)c).GlobalPath, LoadVariableMacro, put_file_cts.Token, gcode), () => false);
        }

        public static IEnumerable<string> WriteVariableString(string variable, TData value)
        {
            var s_value = sanitize_value(value);
            yield return $"if (!exists(global.{variable}))";
            yield return $"    global {variable} = {s_value}";
            yield return $"else";
            yield return $"    set global.{variable} = {s_value}";
        }
    }

    public class RRF_ArrayVariableGlobalModel<TData> : FLUX_VariableGP<RRF_Connection, TData, TData>, IRRF_VariableGlobalModel
    {
        public bool Stored { get; }
        public string Variable { get; }
        public override string Group => "Global";
        public string LoadVariableMacro => $"load_{Variable}_{Unit.Value.Value.ToLower()}.g";

        public RRF_ArrayVariableGlobalModel(FluxViewModel flux, IObservable<Optional<RRF_Connection>> connection, string variable, VariableUnit unit, bool stored, Func<object, TData> convert_data = default)
            : base(
                connection,
                $"{variable} {unit.Value}",
                FluxMemReadPriority.DISABLED,
                write_func: async (c, v) =>
                {
                    var lower_unit = unit.Value.ToLower();

                    var s_value = sanitize_value(v);
                    var write_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    if (!await c.PostGCodeAsync($"set global.{variable}_{lower_unit} = {s_value}", write_cts.Token))
                    {
                        flux.Messages.LogMessage("Impossibile scrivere la variabile", "Errore durante l'esecuzione del gcode", MessageLevel.ERROR, 0);
                        return false;
                    }
    
                    if (stored)
                    { 
                        var load_var_macro = $"load_{variable}_{lower_unit}.g";
                        var gcode = WriteVariableString(variable, unit, v).ToOptional();
                        var put_file_cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        if (!await c.PutFileAsync(c => ((RRF_Connection)c).GlobalPath, load_var_macro, put_file_cts.Token, gcode))
                        {
                            flux.Messages.LogMessage("Impossibile salvare la variabile", "Errore durante la scrittura del file", MessageLevel.ERROR, 0);
                            return false;
                        }
                    }

                    return true;
                },
                unit: unit)
        {
            Stored = stored;
            Variable = variable;
            var lower_unit = unit.Value.ToLower();
            connection
                .Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveGlobalState(m => get_data(m, $"{variable}_{lower_unit}", convert_data)))
                .BindTo(this, v => v.Value);
        }

        static string sanitize_value(TData value) => $"{value:0.00}".ToLower().Replace(',', '.');
        static Optional<TData> get_data(Optional<Dictionary<string, object>> global, string variable, Func<object, TData> convert_data) 
            => global.Convert(g => g.Lookup(variable)).Convert(v => convert_data != null ? convert_data(v) : (TData)Convert.ChangeType(v, typeof(TData), CultureInfo.InvariantCulture));

        public async Task<bool> InitializeVariableAsync()
        {
            var gcode = WriteVariableString(Variable, Unit.Value, default).ToOptional();
            var put_file_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await Connection.ConvertOrAsync(c => c.PutFileAsync(c => ((RRF_Connection)c).GlobalPath, LoadVariableMacro, put_file_cts.Token, gcode), () => false);
        }
        public static IEnumerable<string> WriteVariableString(string variable, VariableUnit unit, TData value)
        {
            var s_value = sanitize_value(value);
            var lower_unit = unit.Value.ToLower();
            yield return $"if (!exists(global.{variable}_{lower_unit}))";
            yield return $"    global {variable}_{lower_unit} = {s_value}";
            yield return $"else";
            yield return $"    set global.{variable}_{lower_unit} = {s_value}";
        }
    }

    public class RRF_ArrayGlobalModel<TData> : FLUX_Array<TData, TData>
    {
        public override string Group => "Global";

        public RRF_ArrayGlobalModel(FluxViewModel flux, IObservable<Optional<RRF_Connection>> connection, string name, IEnumerable<VariableUnit> variable_units, bool stored, Func<object, TData> convert_data = default)
            : base(name, FluxMemReadPriority.DISABLED)
        {
            Variables = new SourceCache<IFLUX_Variable<TData, TData>, VariableUnit>(k => k.Unit.ValueOr(() => ""));
            for (ushort position = 0; position < variable_units.Count(); position++)
            {
                var unit = variable_units.ElementAt(position);
                var variables = ((ISourceCache<IFLUX_Variable<TData, TData>, VariableUnit>)Variables);
                variables.AddOrUpdate(new RRF_ArrayVariableGlobalModel<TData>(flux, connection, name, unit, stored, convert_data));
            }
        }

        public override VariableUnit GetArrayUnit(ushort position)
        {
            return Variables.Items
                .ElementAtOrDefault(position)
                .ToOptional(v => v != null)
                .Convert(v => v.Unit)
                .ValueOr(() => $"{position}");
        }
    }
}
