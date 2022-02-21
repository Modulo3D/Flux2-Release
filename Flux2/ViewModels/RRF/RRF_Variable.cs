using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
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

    public interface IRRF_VariableBaseGlobalModel
    {
        bool Stored { get; }
        string Variable { get; }
        string CreateVariableName { get; }
        string InitializeVariableString { get; }
        IEnumerable<string> CreateVariableString { get; }
        Optional<RRF_Connection> Connection { get; }
    }

    public class RRF_VariableGlobalModel<TData> : FLUX_VariableGP<RRF_Connection, TData, TData>, IRRF_VariableBaseGlobalModel
    {
        public bool Stored { get; }
        public string Variable { get; }
        public override string Group => "Global";

        public IEnumerable<string> CreateVariableString
        {
            get
            {
                if (Stored)
                {
                    yield return "; Read variables";
                    yield return $"M98 P\"/sys/global/read_{Variable}.g\"";
                }

                yield return "";
                yield return "; Get variable";
                yield return $"var {Variable} = exists(param.S) ? param.S : (exists(global.{Variable}) ? global.{Variable} : {(typeof(TData) == typeof(string ) ? "\"\"" : sanitize_value(default(TData)))})";

                yield return "";
                yield return "; Set variable";

                yield return $"if !exists(global.{Variable})";
                yield return $"    global {Variable} = var.{Variable}";
                yield return $"else";
                yield return $"    set global.{Variable} = var.{Variable}";

                if (Stored)
                {
                    yield return "";
                    yield return "; Store variable";

                    yield return $"echo >\"/sys/global/read_{Variable}.g\" \"if (!exists(global.{Variable}))\"";
                    yield return $"echo >>\"/sys/global/read_{Variable}.g\" \"  global {Variable} = \"^{(typeof(TData) == typeof(string) ? "\"\"\"\"^" : "")}var.{Variable}{(typeof(TData) == typeof(string) ? "^\"\"\"\"" : "")}";
                    yield return $"echo >>\"/sys/global/read_{Variable}.g\" \"else\"";
                    yield return $"echo >>\"/sys/global/read_{Variable}.g\" \" set global.{Variable} = \"^{(typeof(TData) == typeof(string) ? "\"\"\"\"^" : "")}var.{Variable}{(typeof(TData) == typeof(string) ? "^\"\"\"\"" : "")}";
                    yield return $"echo >>\"/sys/global/read_{Variable}.g\" \"\"";
                    yield return $"";
                }
            }
        }
        public string CreateVariableName => $"write_{Variable}.g";
        public string InitializeVariableString => $"M98 P\"/sys/global/write_{Variable}.g\"";

        public RRF_VariableGlobalModel(IObservable<Optional<RRF_Connection>> connection, string variable, bool stored, Func<object, TData> convert_data = default)
            : base(connection, variable, FluxMemReadPriority.DISABLED, write_func: async (c, v) =>
            {
                var gcode = $"M98 P\"/sys/global/write_{variable}.g\" S{(typeof(TData) == typeof(string) ? $"\"{sanitize_value(v)}\"" : sanitize_value(v))}";

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var result = await c.PostGCodeAsync(gcode, cts.Token);

                return result;
            })
        {
            Stored = stored;
            Variable = variable;
            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveGlobalState(m => get_data(m, variable, convert_data)))
                .BindTo(this, v => v.Value);
        }

        static string sanitize_value(TData value) => $"{value:0.00}".ToLower().Replace(',', '.');
        Optional<TData> get_data(Optional<Dictionary<string, object>> global, string variable, Func<object, TData> convert_data) => 
            global.Convert(g => g.Lookup(variable)).Convert(v => convert_data != null ? convert_data(v) : (TData)Convert.ChangeType(v, typeof(TData)));
    }

    public class RRF_ArrayVariableGlobalModel<TData> : FLUX_VariableGP<RRF_Connection, TData, TData>
    {
        public override string Group => "Global";
        public RRF_ArrayVariableGlobalModel(RRF_ArrayGlobalModel<TData> array, VariableUnit unit, Func<object, TData> convert_data = default)
            : base(
                  array.WhenAnyValue(a => a.Connection),
                  $"{array.Name} {unit.Value}",
                  FluxMemReadPriority.DISABLED,
                  write_func: async (c, v) =>
                  {
                      var gcode = $"M98 P\"/sys/global/write_{array.Variable}.g\" T\"{unit.Value.ToUpper()}\" S{(typeof(TData) == typeof(string) ? $"\"{sanitize_value(v)}\"" : sanitize_value(v))}";

                      var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                      var result = await c.PostGCodeAsync(gcode, cts.Token);

                      return result;
                  },
                  unit: unit)
        {
            var lower_unit = unit.Value.ToLower();
            array.WhenAnyValue(a => a.Connection)
                .Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveGlobalState(m => get_data(m, $"{array.Variable}_{lower_unit}", convert_data)))
                .BindTo(this, v => v.Value);
        }

        static string sanitize_value(TData value) => $"{value:0.00}".ToLower().Replace(',', '.');
        static Optional<TData> get_data(Optional<Dictionary<string, object>> global, string variable, Func<object, TData> convert_data) 
            => global.Convert(g => g.Lookup(variable)).Convert(v => convert_data != null ? convert_data(v) : (TData)Convert.ChangeType(v, typeof(TData)));
    }

    public class RRF_ArrayGlobalModel<TData> : FLUX_Array<TData, TData>, IRRF_VariableBaseGlobalModel
    {
        public bool Stored { get; }
        public string Variable { get; }
        public override string Group => "Global";
        private ObservableAsPropertyHelper<Optional<RRF_Connection>> _Connection;
        public Optional<RRF_Connection> Connection => _Connection.Value;

        public IEnumerable<string> CreateVariableString
        {
            get
            {
                if (Stored)
                {
                    yield return "; Read variables";
                    yield return $"M98 P\"/sys/global/read_{Variable}.g\"";
                    yield return "";
                }

                yield return "; Get variables";
                for (ushort position = 0; position < Variables.Count; position++)
                {
                    var unit = GetArrayUnit(position);
                    var lower_unit = unit.Value.ToLower();
                    var upper_unit = unit.Value.ToUpper();
                    yield return $"var {Variable}_{lower_unit} = (exists(param.T) && param.T == \"{upper_unit}\" && exists(param.S)) ? param.S : (exists(global.{Variable}_{lower_unit}) ? global.{Variable}_{lower_unit} : {(typeof(TData) == typeof(string) ? "\"\"" : sanitize_value(default(TData)))})";
                }

                yield return "";
                yield return "; Set variables";

                for (ushort position = 0; position < Variables.Count; position++)
                {
                    var unit = GetArrayUnit(position);
                    var lower_unit = unit.Value.ToLower();
                    yield return $"if !exists(global.{Variable}_{lower_unit})";
                    yield return $"     global {Variable}_{lower_unit} = var.{Variable}_{lower_unit}";
                    yield return $"else";
                    yield return $"     set global.{Variable}_{lower_unit} = var.{Variable}_{lower_unit}";
                }

                if (Stored)
                {
                    yield return "";
                    yield return "; Store variables";

                    for (ushort position = 0; position < Variables.Count; position++)
                    {
                        var unit = GetArrayUnit(position);
                        var lower_unit = unit.Value.ToLower();
                        yield return $"echo {(position == 0 ? ">" : ">>")}\"/sys/global/read_{Variable}.g\" \"if (!exists(global.{Variable}_{lower_unit}))\"";
                        yield return $"echo >>\"/sys/global/read_{Variable}.g\" \" global {Variable}_{lower_unit} = \"^{(typeof(TData) == typeof(string) ? "\"\"\"\"^" : "")}var.{Variable}_{lower_unit}{(typeof(TData) == typeof(string) ? "^\"\"\"\"" : "")}";
                        yield return $"echo >>\"/sys/global/read_{Variable}.g\" \"else\"";
                        yield return $"echo >>\"/sys/global/read_{Variable}.g\" \" set global.{Variable}_{lower_unit} = \"^{(typeof(TData) == typeof(string) ? "\"\"\"\"^" : "")}var.{Variable}_{lower_unit}{(typeof(TData) == typeof(string) ? "^\"\"\"\"" : "")}";
                        yield return $"echo >>\"/sys/global/read_{Variable}.g\" \"\"";
                        yield return $"";
                    }
                }

            }
        }
        public string CreateVariableName => $"write_{Variable}.g";
        public string InitializeVariableString => $"M98 P\"/sys/global/write_{Variable}.g\"";

        public RRF_ArrayGlobalModel(IObservable<Optional<RRF_Connection>> connection, string variable, IEnumerable<VariableUnit> variable_units, bool stored, Func<object, TData> convert_data = default)
            : base(variable, FluxMemReadPriority.DISABLED)
        {
            Stored = stored;
            Variable = variable;
            _Connection = connection
                .ToProperty(this, v => v.Connection);
            Variables = new SourceCache<IFLUX_Variable<TData, TData>, VariableUnit>(k => k.Unit.ValueOr(() => ""));
            for (ushort position = 0; position < variable_units.Count(); position++)
            {
                var unit = variable_units.ElementAt(position);
                var variables = ((ISourceCache<IFLUX_Variable<TData, TData>, VariableUnit>)Variables);
                variables.AddOrUpdate(new RRF_ArrayVariableGlobalModel<TData>(this, unit, convert_data));
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

        static string sanitize_value(TData value) => $"{value:0.00}".ToLower().Replace(',', '.');
    }
}
