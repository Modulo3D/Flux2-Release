using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
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

            public class RRF_ArrayBuilder<TList>
            {
                public RRF_ArrayBuilder(
                    IObservable<Optional<RRF_Connection>> connection,
                    string name,
                    ushort start,
                    ushort count,
                    Func<RRF_ObjectModel, IObservable<Optional<TState>>> get_state,
                    Func<TState, Optional<List<TList>>> get_list)
                {
                    Name = name;
                    Start = start;
                    Count = count;
                    GetList = get_list;
                    GetState = get_state;
                    Connection = connection;
                }

                public string Name { get; }
                public ushort Start { get; }
                public ushort Count { get; }
                public Func<TState, Optional<List<TList>>> GetList { get; }
                public IObservable<Optional<RRF_Connection>> Connection { get; }
                public Func<RRF_ObjectModel, IObservable<Optional<TState>>> GetState { get; }

                public RRF_ArrayObjectModel<TState, TRData, TWData> Create<TRData, TWData>(
                    Func<RRF_Connection, TList, Optional<TRData>> read_data,
                    Optional<IEnumerable<VariableUnit>> custom_unit = default)
                {
                    return new RRF_ArrayObjectModel<TState, TRData, TWData>(Name, Start, Count, t => create_variable(t), custom_unit);
                    RRF_VariableObjectModel<TState, TRData, TWData> create_variable((string name, VariableUnit unit, ushort position) t)
                    {
                        Optional<TRData> get_data(RRF_Connection c, TState m) => GetList(m).Convert(l => read_data(c, l[t.position]));
                        return new RRF_VariableObjectModel<TState, TRData, TWData>(Connection, t.name, GetState, get_data, unit: t.unit);
                    }
                }
                public RRF_ArrayObjectModel<TState, TRData, TWData> Create<TRData, TWData>(
                    Func<RRF_Connection, TList, Optional<TRData>> read_data,
                    Func<RRF_Connection, TWData, bool> write_data,
                    Optional<IEnumerable<VariableUnit>> custom_unit = default)
                {
                    return new RRF_ArrayObjectModel<TState, TRData, TWData>(Name, Start, Count, t => create_variable(t), custom_unit);
                    RRF_VariableObjectModel<TState, TRData, TWData> create_variable((string name, VariableUnit unit, ushort position) t)
                    {
                        Optional<TRData> get_data(RRF_Connection c, TState m) => GetList(m).Convert(l => read_data(c, l[t.position]));
                        return new RRF_VariableObjectModel<TState, TRData, TWData>(Connection, t.name, GetState, get_data, (c, d) => write_data?.Invoke(c, d) ?? false, t.unit);
                    }
                }
                public RRF_ArrayObjectModel<TState, TRData, TWData> Create<TRData, TWData>(
                    Func<RRF_Connection, TList, Optional<TRData>> read_data,
                    Func<RRF_Connection, TWData, (string name, VariableUnit unit, ushort position), bool> write_data,
                    Optional<IEnumerable<VariableUnit>> custom_unit = default)
                {
                    return new RRF_ArrayObjectModel<TState, TRData, TWData>(Name, Start, Count, t => create_variable(t), custom_unit);
                    RRF_VariableObjectModel<TState, TRData, TWData> create_variable((string name, VariableUnit unit, ushort position) t)
                    {
                        Optional<TRData> get_data(RRF_Connection c, TState m) => GetList(m).Convert(l => read_data(c, l[t.position]));
                        return new RRF_VariableObjectModel<TState, TRData, TWData>(Connection, t.name, GetState, get_data, (c, d) => write_data?.Invoke(c, d, t) ?? false, t.unit);
                    }
                }
                public RRF_ArrayObjectModel<TState, TRData, TWData> Create<TRData, TWData>(
                    Func<RRF_Connection, TList, Optional<TRData>> read_data,
                    Func<RRF_Connection, TWData, Task<bool>> write_data,
                    Optional<IEnumerable<VariableUnit>> custom_unit = default)
                {
                    return new RRF_ArrayObjectModel<TState, TRData, TWData>(Name, Start, Count, t => create_variable(t), custom_unit);
                    RRF_VariableObjectModel<TState, TRData, TWData> create_variable((string name, VariableUnit unit, ushort position) t)
                    {
                        Optional<TRData> get_data(RRF_Connection c, TState m) => GetList(m).Convert(l => read_data(c, l[t.position]));
                        return new RRF_VariableObjectModel<TState, TRData, TWData>(Connection, t.name, GetState, get_data, (c, d) => write_data?.Invoke(c, d), t.unit);
                    }
                }
                public RRF_ArrayObjectModel<TState, TRData, TWData> Create<TRData, TWData>(
                    Func<RRF_Connection, TList, Optional<TRData>> read_data,
                    Func<RRF_Connection, TWData, (string name, VariableUnit unit, ushort position), Task<bool>> write_data,
                    Optional<IEnumerable<VariableUnit>> custom_unit = default)
                {
                    return new RRF_ArrayObjectModel<TState, TRData, TWData>(Name, Start, Count, t => create_variable(t), custom_unit);
                    RRF_VariableObjectModel<TState, TRData, TWData> create_variable((string name, VariableUnit unit, ushort position) t)
                    {
                        Optional<TRData> get_data(RRF_Connection c, TState m) => GetList(m).Convert(l => read_data(c, l[t.position]));
                        return new RRF_VariableObjectModel<TState, TRData, TWData>(Connection, t.name, GetState, get_data, (c, d) => write_data?.Invoke(c, d, t), t.unit);
                    }
                }
            }
            public RRF_ArrayBuilder<TList> CreateArray<TList>(string name, ushort start, ushort count, Func<TState, Optional<List<TList>>> get_list)
                => new RRF_ArrayBuilder<TList>(Connection, name, start, count, GetState, get_list);
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

    public class RRF_ArrayObjectModel<TState, TRData, TWData> : FLUX_Array<TRData, TWData>
    {
        public override string Group => "ObjectModel";
        public ushort Start { get; }
        new public ISourceCache<IFLUX_Variable<TRData, TWData>, VariableUnit> Variables { get; }
        public RRF_ArrayObjectModel(string name, ushort start, ushort count, Func<(string name, VariableUnit unit, ushort position), RRF_VariableObjectModel<TState, TRData, TWData>> get_variables, Optional<IEnumerable<VariableUnit>> custom_unit = default)
            : base(name, count, FluxMemReadPriority.DISABLED, custom_unit)
        {
            Variables = new SourceCache<IFLUX_Variable<TRData, TWData>, VariableUnit>(k => k.Unit.ValueOr(() => ""));
            base.Variables = Variables;
            Start = start;

            ushort unit_pos = 0;
            for (ushort position = Start; position < Start + count; position++)
            {
                var unit = GetArrayUnit(unit_pos++);
                Variables.AddOrUpdate(get_variables(($"{name} {unit}", unit, position)));
            }
        }
        public override VariableUnit GetArrayUnit(ushort position)
        {
            if (CustomUnit.HasValue)
            {
                var unit = CustomUnit.Value.ElementAtOrDefault(position);
                if (unit != null)
                    return unit;
            }
            return $"{position}";
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
                yield return $"var {Variable} = exists(param.S) ? param.S : (exists(global.{Variable}) ? global.{Variable} : {sanitize_value(default(TData))})";

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
                    yield return $"echo >>\"/sys/global/read_{Variable}.g\" \"  global {Variable} = \"^var.{Variable}";
                    yield return $"echo >>\"/sys/global/read_{Variable}.g\" \"else\"";
                    yield return $"echo >>\"/sys/global/read_{Variable}.g\" \" set global.{Variable} = \"^var.{Variable}";
                    yield return $"echo >>\"/sys/global/read_{Variable}.g\" \"\"";
                    yield return $"";
                }
            }
        }
        public string CreateVariableName => $"write_{Variable}.g";
        public string InitializeVariableString => $"M98 P\"/sys/global/write_{Variable}.g\"";

        public RRF_VariableGlobalModel(IObservable<Optional<RRF_Connection>> connection, string variable, bool stored, Optional<string> unit = default)
            : base(connection, variable, FluxMemReadPriority.DISABLED, write_func: async (c, v) =>
            {
                var gcode = $"M98 P\"/sys/global/write_{variable}.g\"{(unit.HasValue ? $" T\"{unit}\" " : " ")}S{sanitize_value(v)}";
                var result = await c.PostGCodeAsync(gcode, false, TimeSpan.FromSeconds(5));
                return result;
            })
        {
            Stored = stored;
            Variable = variable;
            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveGlobalState(m => get_data(m, variable)))
                .BindTo(this, v => v.Value);
        }

        static string sanitize_value(TData value) => $"{value:0.00}".ToLower().Replace(',', '.');
        static Optional<TData> get_data(Optional<Dictionary<string, object>> global, string variable) => global.Convert(g => g.Lookup(variable)).Convert(v => (TData)Convert.ChangeType(v, typeof(TData)));
    }

    public class RRF_ArrayVariableGlobalModel<TData> : FLUX_VariableGP<RRF_Connection, TData, TData>
    {
        public override string Group => "Global";
        public RRF_ArrayVariableGlobalModel(RRF_ArrayGlobalModel<TData> array, VariableUnit unit)
            : base(
                  array.WhenAnyValue(a => a.Connection),
                  $"{array.Name} {unit.Value}",
                  FluxMemReadPriority.DISABLED,
                  write_func: async (c, v) =>
                  {
                      var gcode = $"M98 P\"/sys/global/write_{array.Variable}.g\" T\"{unit.Value.ToUpper()}\" S{sanitize_value(v)}";
                      var result = await c.PostGCodeAsync(gcode, false, TimeSpan.FromSeconds(5));
                      return result;
                  },
                  unit: unit)
        {
            var lower_unit = unit.Value.ToLower();
            array.WhenAnyValue(a => a.Connection)
                .Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveGlobalState(m => get_data(m, $"{array.Variable}_{lower_unit}")))
                .BindTo(this, v => v.Value);
        }

        static string sanitize_value(TData value) => $"{value:0.00}".ToLower().Replace(',', '.');
        static Optional<TData> get_data(Optional<Dictionary<string, object>> global, string variable) => global.Convert(g => g.Lookup(variable)).Convert(v => (TData)Convert.ChangeType(v, typeof(TData)));
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
                for (ushort position = 0; position < Count; position++)
                {
                    var unit = GetArrayUnit(position);
                    var lower_unit = unit.Value.ToLower();
                    var upper_unit = unit.Value.ToUpper();
                    yield return $"var {Variable}_{lower_unit} = (exists(param.T) && param.T == \"{upper_unit}\" && exists(param.S)) ? param.S : (exists(global.{Variable}_{lower_unit}) ? global.{Variable}_{lower_unit} : {sanitize_value(default(TData))})";
                }

                yield return "";
                yield return "; Set variables";

                for (ushort position = 0; position < Count; position++)
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

                    for (ushort position = 0; position < Count; position++)
                    {
                        var unit = GetArrayUnit(position);
                        var lower_unit = unit.Value.ToLower();
                        yield return $"echo {(position == 0 ? ">" : ">>")}\"/sys/global/read_{Variable}.g\" \"if (!exists(global.{Variable}_{lower_unit}))\"";
                        yield return $"echo >>\"/sys/global/read_{Variable}.g\" \" global {Variable}_{lower_unit} = \"^var.{Variable}_{lower_unit}";
                        yield return $"echo >>\"/sys/global/read_{Variable}.g\" \"else\"";
                        yield return $"echo >>\"/sys/global/read_{Variable}.g\" \" set global.{Variable}_{lower_unit} = \"^var.{Variable}_{lower_unit}";
                        yield return $"echo >>\"/sys/global/read_{Variable}.g\" \"\"";
                        yield return $"";
                    }
                }

            }
        }
        public string CreateVariableName => $"write_{Variable}.g";
        public string InitializeVariableString => $"M98 P\"/sys/global/write_{Variable}.g\"";

        public RRF_ArrayGlobalModel(IObservable<Optional<RRF_Connection>> connection, string variable, ushort count, bool stored, Optional<IEnumerable<VariableUnit>> custom_unit = default)
            : base(variable, count, FluxMemReadPriority.DISABLED, custom_unit)
        {
            Stored = stored;
            Variable = variable;
            _Connection = connection
                .ToProperty(this, v => v.Connection);
            Variables = new SourceCache<IFLUX_Variable<TData, TData>, VariableUnit>(k => k.Unit.ValueOr(() => ""));
            for (ushort position = 0; position < count; position++)
            {
                var unit = GetArrayUnit(position);
                var variables = ((ISourceCache<IFLUX_Variable<TData, TData>, VariableUnit>)Variables);
                variables.AddOrUpdate(new RRF_ArrayVariableGlobalModel<TData>(this, unit));
            }
        }

        public override VariableUnit GetArrayUnit(ushort position)
        {
            if (CustomUnit.HasValue)
            {
                var unit = CustomUnit.Value.ElementAtOrDefault(position);
                if (unit != default)
                    return unit;
            }
            return $"{position}";
        }

        static string sanitize_value(TData value) => $"{value:0.00}".ToLower().Replace(',', '.');
    }
}
