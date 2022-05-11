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
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public static class RRF_ModelBuilder
    {
        public class RRF_InnerModelBuilder<TModel>
        {
            public IObservable<Optional<RRF_Connection>> Connection { get; }
            public Func<RRF_MemoryBuffer, Task<Optional<TModel>>> ReadModel { get; }
            public Func<RRF_ObjectModel, IObservable<Optional<TModel>>> GetModel { get; }

            public RRF_InnerModelBuilder(
                IObservable<Optional<RRF_Connection>> connection,
                Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
                Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model)
            {
                GetModel = get_model;
                ReadModel = read_model;
                Connection = connection;
            }

            public RRF_VariableObjectModel<TModel, TRData, TWData> CreateVariable<TRData, TWData>(
                string name,
                Func<RRF_Connection, TModel, Optional<TRData>> get_data,
                VariableUnit unit = default)
            {
                return new RRF_VariableObjectModel<TModel, TRData, TWData>(Connection, name, ReadModel, GetModel, get_data, unit);
            }
            public RRF_VariableObjectModel<TModel, TRData, TWData> CreateVariable<TRData, TWData>(
                string name,
                Func<RRF_Connection, TModel, Task<Optional<TRData>>> get_data,
                VariableUnit unit = default)
            {
                return new RRF_VariableObjectModel<TModel, TRData, TWData>(Connection, name, ReadModel, GetModel, get_data, unit);
            }
            public RRF_VariableObjectModel<TModel, TRData, TWData> CreateVariable<TRData, TWData>(
                string name,
                Func<RRF_Connection, TModel, Optional<TRData>> get_data,
                Func<RRF_Connection, TWData, bool> write_data,
                VariableUnit unit = default)
            {
                return new RRF_VariableObjectModel<TModel, TRData, TWData>(Connection, name, ReadModel, GetModel, get_data, write_data, unit);
            }
            public RRF_VariableObjectModel<TModel, TRData, TWData> CreateVariable<TRData, TWData>(
                string name,
                Func<RRF_Connection, TModel, Task<Optional<TRData>>> get_data,
                Func<RRF_Connection, TWData, bool> write_data,
                VariableUnit unit = default)
            {
                return new RRF_VariableObjectModel<TModel, TRData, TWData>(Connection, name, ReadModel, GetModel, get_data, write_data, unit);
            }
            public RRF_VariableObjectModel<TModel, TRData, TWData> CreateVariable<TRData, TWData>(
                string name,
                Func<RRF_Connection, TModel, Optional<TRData>> get_data,
                Func<RRF_Connection, TWData, Task<bool>> write_data,
                VariableUnit unit = default)
            {
                return new RRF_VariableObjectModel<TModel, TRData, TWData>(Connection, name, ReadModel, GetModel, get_data, write_data, unit);
            }
            public RRF_VariableObjectModel<TModel, TRData, TWData> CreateVariable<TRData, TWData>(
                string name,
                Func<RRF_Connection, TModel, Task<Optional<TRData>>> get_data,
                Func<RRF_Connection, TWData, Task<bool>> write_data,
                VariableUnit unit = default)
            {
                return new RRF_VariableObjectModel<TModel, TRData, TWData>(Connection, name, ReadModel, GetModel, get_data, write_data, unit);
            }

            public class RRF_ArrayBuilder<TVariable>
            {
                public RRF_ArrayBuilder(
                    IObservable<Optional<RRF_Connection>> connection,
                    Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
                    Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
                    Func<TModel, Optional<Dictionary<VariableUnit, TVariable>>> get_variables)
                {
                    GetModel = get_model;
                    ReadModel = read_model;
                    Connection = connection;
                    GetVariables = get_variables;
                }

                public IObservable<Optional<RRF_Connection>> Connection { get; }
                public Func<RRF_MemoryBuffer, Task<Optional<TModel>>> ReadModel { get; }
                public Func<RRF_ObjectModel, IObservable<Optional<TModel>>> GetModel { get; }
                public Func<TModel, Optional<Dictionary<VariableUnit, TVariable>>> GetVariables { get; }

                public RRF_ArrayObjectModel<TModel, TRData, TWData> Create<TRData, TWData>(
                    string name,
                    IObservable<IEnumerable<VariableUnit>> variable_units,
                    Func<RRF_Connection, TVariable, Optional<TRData>> get_data)
                {
                    RRF_VariableObjectModel<TModel, TRData, TWData> get_variable(VariableUnit unit)
                    {
                        Optional<TRData> get_variable(RRF_Connection c, TModel m) => GetVariables(m).Convert(v => v.Lookup(unit)).Convert(m => get_data(c, m));
                        return new RRF_VariableObjectModel<TModel, TRData, TWData>(Connection, $"{name} {unit}", ReadModel, GetModel, get_variable, unit: unit);
                    }

                    return new RRF_ArrayObjectModel<TModel, TRData, TWData>(name, variable_units, get_variable);
                }
                public RRF_ArrayObjectModel<TModel, TRData, TWData> Create<TRData, TWData>(
                    string name,
                    IObservable<IEnumerable<VariableUnit>> variable_units,
                    Func<RRF_Connection, TVariable, Optional<TRData>> get_data,
                    Func<RRF_Connection, TWData, VariableUnit, bool> write_data)
                {
                    RRF_VariableObjectModel<TModel, TRData, TWData> get_variable(VariableUnit unit)
                    {
                        bool write_unit_data(RRF_Connection c, TWData d) => write_data(c, d, unit);
                        Optional<TRData> get_variable(RRF_Connection c, TModel m) => GetVariables(m).Convert(v => v.Lookup(unit)).Convert(m => get_data(c, m));
                        return new RRF_VariableObjectModel<TModel, TRData, TWData>(Connection, $"{name} {unit}", ReadModel, GetModel, get_variable, write_unit_data, unit);
                    }

                    return new RRF_ArrayObjectModel<TModel, TRData, TWData>(name, variable_units, get_variable);
                }
                public RRF_ArrayObjectModel<TModel, TRData, TWData> Create<TRData, TWData>(
                    string name,
                    IObservable<IEnumerable<VariableUnit>> variable_units,
                    Func<RRF_Connection, TVariable, Optional<TRData>> get_data,
                    Func<RRF_Connection, TWData, VariableUnit, Task<bool>> write_data)
                {
                    RRF_VariableObjectModel<TModel, TRData, TWData> get_variable(VariableUnit unit)
                    {
                        Task<bool> write_unit_data(RRF_Connection c, TWData d) => write_data(c, d, unit);
                        Optional<TRData> get_variable(RRF_Connection c, TModel m) => GetVariables(m).Convert(v => v.Lookup(unit)).Convert(m => get_data(c, m));
                        return new RRF_VariableObjectModel<TModel, TRData, TWData>(Connection, $"{name} {unit}", ReadModel, GetModel, get_variable, write_unit_data, unit);
                    }

                    return new RRF_ArrayObjectModel<TModel, TRData, TWData>(name, variable_units, get_variable);
                }
            }

            public RRF_ArrayBuilder<TList> CreateArray<TList>(Func<TModel, Optional<Dictionary<VariableUnit, TList>>> get_list)
                => new RRF_ArrayBuilder<TList>(Connection, ReadModel, GetModel, get_list);
            public RRF_ArrayBuilder<TList> CreateArray<TList>(Func<TModel, Optional<Dictionary<Optional<VariableUnit>, TList>>> get_list)
                => new RRF_ArrayBuilder<TList>(Connection, ReadModel, GetModel, s => get_list(s).Convert(d => d.ToDictionary(kvp => kvp.Key.ValueOr(() => ""), kvp => kvp.Value)));
            public RRF_ArrayBuilder<TList> CreateArray<TList, TKey>(Func<TModel, Optional<Dictionary<TKey, TList>>> get_list)
                => new RRF_ArrayBuilder<TList>(Connection, ReadModel, GetModel, s => get_list(s).Convert(d => d.ToDictionary(kvp => (VariableUnit)$"{kvp.Key}", kvp => kvp.Value)));
            public RRF_ArrayBuilder<TList> CreateArray<TList, TKey>(Func<TModel, Optional<Dictionary<Optional<TKey>, TList>>> get_list)
                => new RRF_ArrayBuilder<TList>(Connection, ReadModel, GetModel, s => get_list(s).Convert(d => d.ToDictionary(kvp => (VariableUnit)kvp.Key.ConvertOr(k => $"{k}", () => ""), kvp => kvp.Value)));
        }

        public static RRF_InnerModelBuilder<TModel> Create<TModel>(
            IObservable<Optional<RRF_Connection>> connection,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model)
        { 
            return new RRF_InnerModelBuilder<TModel>(connection, read_model, get_model);
        }

        public static RRF_InnerModelBuilder<TModel> Create<TModel>(
            IObservable<Optional<RRF_Connection>> connection,
            Expression<Func<RRF_ObjectModel, Optional<TModel>>> model_expr,
            TimeSpan read_timeout)
        {
            return new RRF_InnerModelBuilder<TModel>(connection, read_model, m => m.WhenAnyValue(model_expr));
            async Task<Optional<TModel>> read_model(RRF_MemoryBuffer buffer)
            {
                var cts = new CancellationTokenSource(read_timeout);
                return await buffer.GetModelDataAsync(model_expr, cts.Token);
            }
        }

        public static RRF_InnerModelBuilder<(TModel1, TModel2)> Create<TModel1, TModel2>(
            IObservable<Optional<RRF_Connection>> connection,
            Expression<Func<RRF_ObjectModel, Optional<TModel1>>> model_expr_1,
            Expression<Func<RRF_ObjectModel, Optional<TModel2>>> model_expr_2,
            TimeSpan read_timeout)
        {
            return new RRF_InnerModelBuilder<(TModel1, TModel2)>(connection, read_models, observe_models);
            IObservable<Optional<(TModel1, TModel2)>> observe_models(RRF_ObjectModel model)
            {
                var m1 = model.WhenAnyValue(model_expr_1);
                var m2 = model.WhenAnyValue(model_expr_2);
                return Observable.CombineLatest(m1, m2, combine_models);
            }
            async Task<Optional<(TModel1, TModel2)>> read_models(RRF_MemoryBuffer buffer)
            {
                var cts = new CancellationTokenSource(read_timeout);
                var m1 = await buffer.GetModelDataAsync(model_expr_1, cts.Token);
                var m2 = await buffer.GetModelDataAsync(model_expr_2, cts.Token);
                return combine_models(m1, m2);
            }
            Optional<(TModel1, TModel2)> combine_models(Optional<TModel1> m1, Optional<TModel2> m2)
            { 
                return m1.Convert(m1 => m2.Convert(m2 => (m1, m2)));
            }
        }

        public static RRF_InnerModelBuilder<(TModel1, TModel2, TModel3)> Create<TModel1, TModel2, TModel3>(
            IObservable<Optional<RRF_Connection>> connection,
            Expression<Func<RRF_ObjectModel, Optional<TModel1>>> model_expr_1,
            Expression<Func<RRF_ObjectModel, Optional<TModel2>>> model_expr_2,
            Expression<Func<RRF_ObjectModel, Optional<TModel3>>> model_expr_3,
            TimeSpan read_timeout)
        {
            return new RRF_InnerModelBuilder<(TModel1, TModel2, TModel3)>(connection, read_models, observe_models);
            IObservable<Optional<(TModel1, TModel2, TModel3)>> observe_models(RRF_ObjectModel model)
            {
                var m1 = model.WhenAnyValue(model_expr_1);
                var m2 = model.WhenAnyValue(model_expr_2);
                var m3 = model.WhenAnyValue(model_expr_3);
                return Observable.CombineLatest(m1, m2, m3, combine_models);
            }
            async Task<Optional<(TModel1, TModel2, TModel3)>> read_models(RRF_MemoryBuffer buffer)
            {
                var cts = new CancellationTokenSource(read_timeout);
                var m1 = await buffer.GetModelDataAsync(model_expr_1, cts.Token);
                var m2 = await buffer.GetModelDataAsync(model_expr_2, cts.Token);
                var m3 = await buffer.GetModelDataAsync(model_expr_3, cts.Token);
                return combine_models(m1, m2, m3);
            }
            Optional<(TModel1, TModel2, TModel3)> combine_models(Optional<TModel1> m1, Optional<TModel2> m2, Optional<TModel3> m3)
            {
                return m1.Convert(m1 => m2.Convert(m2 => m3.Convert(m3 => (m1, m2, m3))));
            }
        }

        public static RRF_InnerModelBuilder<(TModel1, TModel2, TModel3, TModel4)> Create<TModel1, TModel2, TModel3, TModel4>(
             IObservable<Optional<RRF_Connection>> connection,
             Expression<Func<RRF_ObjectModel, Optional<TModel1>>> model_expr_1,
             Expression<Func<RRF_ObjectModel, Optional<TModel2>>> model_expr_2,
             Expression<Func<RRF_ObjectModel, Optional<TModel3>>> model_expr_3,
             Expression<Func<RRF_ObjectModel, Optional<TModel4>>> model_expr_4,
             TimeSpan read_timeout)
        {
            return new RRF_InnerModelBuilder<(TModel1, TModel2, TModel3, TModel4)>(connection, read_models, observe_models);
            IObservable<Optional<(TModel1, TModel2, TModel3, TModel4)>> observe_models(RRF_ObjectModel model)
            {
                var m1 = model.WhenAnyValue(model_expr_1);
                var m2 = model.WhenAnyValue(model_expr_2);
                var m3 = model.WhenAnyValue(model_expr_3);
                var m4 = model.WhenAnyValue(model_expr_4);
                return Observable.CombineLatest(m1, m2, m3, m4, combine_models);
            }
            async Task<Optional<(TModel1, TModel2, TModel3, TModel4)>> read_models(RRF_MemoryBuffer buffer)
            {
                var cts = new CancellationTokenSource(read_timeout);
                var m1 = await buffer.GetModelDataAsync(model_expr_1, cts.Token);
                var m2 = await buffer.GetModelDataAsync(model_expr_2, cts.Token);
                var m3 = await buffer.GetModelDataAsync(model_expr_3, cts.Token);
                var m4 = await buffer.GetModelDataAsync(model_expr_4, cts.Token);
                return combine_models(m1, m2, m3, m4);
            }
            Optional<(TModel1, TModel2, TModel3, TModel4)> combine_models(Optional<TModel1> m1, Optional<TModel2> m2, Optional<TModel3> m3, Optional<TModel4> m4)
            {
                return m1.Convert(m1 => m2.Convert(m2 => m3.Convert(m3 => m4.Convert(m4 => (m1, m2, m3, m4)))));
            }
        }
    }

    public class RRF_VariableObjectModel<TModel, TRData, TWData> : FLUX_VariableGP<RRF_Connection, TRData, TWData>
    {
        public override string Group => "ObjectModel";

        public RRF_VariableObjectModel(
            IObservable<Optional<RRF_Connection>> connection,
            string name,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
            Func<RRF_Connection, TModel, Optional<TRData>> get_data,
            VariableUnit unit = default) :
            base(connection, name, FluxMemReadPriority.DISABLED,
                read_func: c => read_variable(c, read_model, s => get_data(c, s)),
                unit: unit)
        {
            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveModel(get_model, get_data))
                .BindTo(this, v => v.Value);
        }
        public RRF_VariableObjectModel(
            IObservable<Optional<RRF_Connection>> connection,
            string name,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
            Func<RRF_Connection, TModel, Task<Optional<TRData>>> get_data,
            VariableUnit unit = default) :
            base(connection, name, FluxMemReadPriority.DISABLED,
                read_func: c => read_variable(c, read_model, s => get_data(c, s)),
                unit: unit)
        {
            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveModel(get_model, get_data))
                .BindTo(this, v => v.Value);
        }
        public RRF_VariableObjectModel(
            IObservable<Optional<RRF_Connection>> connection,
            string name,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
            Func<RRF_Connection, TModel, Optional<TRData>> get_data,
            Func<RRF_Connection, TWData, bool> write_data = default,
            VariableUnit unit = default) :
            base(connection, name, FluxMemReadPriority.DISABLED, unit: unit,
                read_func: c => read_variable(c, read_model, s => get_data(c, s)),
                write_func: (c, d) => Task.FromResult(write_data?.Invoke(c, d) ?? false))
        {
            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveModel(get_model, get_data))
                .BindTo(this, v => v.Value);
        }
        public RRF_VariableObjectModel(
            IObservable<Optional<RRF_Connection>> connection,
            string name,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
            Func<RRF_Connection, TModel, Task<Optional<TRData>>> get_data,
            Func<RRF_Connection, TWData, bool> write_data = default,
            VariableUnit unit = default) :
            base(connection, name, FluxMemReadPriority.DISABLED, unit: unit,
                read_func: c => read_variable(c, read_model, s => get_data(c, s)),
                write_func: (c, d) => Task.FromResult(write_data?.Invoke(c, d) ?? false))
        {
            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveModel(get_model, get_data))
                .BindTo(this, v => v.Value);
        }
        public RRF_VariableObjectModel(
            IObservable<Optional<RRF_Connection>> connection,
            string name,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
            Func<RRF_Connection, TModel, Optional<TRData>> get_data,
            Func<RRF_Connection, TWData, Task<bool>> write_data = default,
            VariableUnit unit = default) :
            base(connection, name, FluxMemReadPriority.DISABLED, unit: unit,
                read_func: c => read_variable(c, read_model, s => get_data(c, s)),
                write_func: write_data)
        {
            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveModel(get_model, get_data))
                .BindTo(this, v => v.Value);
        }
        public RRF_VariableObjectModel(
            IObservable<Optional<RRF_Connection>> connection,
            string name,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
            Func<RRF_Connection, TModel, Task<Optional<TRData>>> get_data,
            Func<RRF_Connection, TWData, Task<bool>> write_data = default,
            VariableUnit unit = default) :
            base(connection, name, FluxMemReadPriority.DISABLED, unit: unit,
                read_func: c => read_variable(c, read_model, s => get_data(c, s)), 
                write_func: write_data)
        {
            connection.Convert(c => c.MemoryBuffer)
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

    public class RRF_ArrayObjectModel<TVariable, TRData, TWData> : FLUX_Array<TRData, TWData>
    {
        public override string Group => "ObjectModel";
        public RRF_ArrayObjectModel(string name, IObservable<IEnumerable<VariableUnit>> variable_units, Func<VariableUnit, IFLUX_Variable<TRData, TWData>> get_variable)
            : base(name, FluxMemReadPriority.DISABLED)
        {
            Variables = ObservableChangeSet.Create<IFLUX_Variable<TRData, TWData>, VariableUnit>(v =>
            {
                return variable_units.Subscribe(unit =>
                {
                    var variables = unit.Select(get_variable);
                    v.EditDiff(variables, (v1, v2) => v1.Name == v2.Name);
                });
            }, v => v.Unit.ValueOr(() => ""))
            .AsObservableCache();
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
        Task<bool> CreateVariableAsync(CancellationToken ct);
    }

    public class RRF_VariableGlobalModel<TData> : FLUX_VariableGP<RRF_Connection, TData, TData>, IRRF_VariableGlobalModel
    {
        public bool Stored { get; }
        public string Variable { get; }
        public Optional<TData> DefaultValue { get; }
        public override string Group => "Global";
        public string LoadVariableMacro => $"load_{Variable}.g";

        public RRF_VariableGlobalModel(
            IObservable<Optional<RRF_Connection>> connection,
            string variable,
            bool stored,
            Optional<TData> default_value = default,
            Func<object, TData> convert_data = default)
            : base(connection, variable, FluxMemReadPriority.DISABLED,
                read_func: c => read_variable(c, variable, convert_data),
                write_func: (c, v) => write_variable(c, variable, v, stored))
        {
            Stored = stored;
            Variable = variable;
            DefaultValue = default_value;
            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveGlobalModel(m => get_data(m, variable, convert_data)))
                .BindTo(this, v => v.Value);
        }

        static string sanitize_value(TData value)
        {
            return typeof(TData) == typeof(string) ? $"\"{value}\"" : $"{value:0.00}"
                .ToLower()
                .Replace(',', '.');
        }

        static Optional<TData> get_data(RRF_GlobalModel global, string variable, Func<object, TData> convert_data)
        {
            return global.Lookup(variable)
                .Convert(v => convert_data != null ? convert_data(v) : v.ToObject<TData>());
        }

        static async Task<Optional<TData>> read_variable(RRF_Connection connection, string variable, Func<object, TData> convert_data = default)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var global = await connection.MemoryBuffer.GetModelDataAsync<RRF_GlobalModel>(cts.Token);
            if (!global.HasValue)
                return default;
            return get_data(global.Value, variable, convert_data);
        }

        static async Task<bool> write_variable(RRF_Connection c, string variable, TData v, bool stored)
        {
            var s_value = sanitize_value(v);
            var write_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await c.PostGCodeAsync($"set global.{variable} = {s_value}", write_cts.Token))
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
            if (!Connection.HasValue)
                return false;

            var gcode = WriteVariableString(Variable, DefaultValue.ValueOrDefault());

            return await Connection.Value.PutFileAsync(
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

    public class RRF_ArrayVariableGlobalModel<TData> : FLUX_VariableGP<RRF_Connection, TData, TData>, IRRF_VariableGlobalModel
    {
        public bool Stored { get; }
        public string Variable { get; }
        public Optional<TData> DefaultValue { get; }
        public override string Group => "Global";
        public string LoadVariableMacro => $"load_{Variable}_{Unit.Value.Value.ToLower()}.g";

        public RRF_ArrayVariableGlobalModel(
            IObservable<Optional<RRF_Connection>> connection,
            string variable,
            VariableUnit unit, 
            bool stored,
            Optional<TData> default_value = default,
            Func<object, TData> convert_data = default)
            : base(
                connection,
                $"{variable} {unit.Value}",
                FluxMemReadPriority.DISABLED,
                read_func: c => read_variable(c, variable, unit, convert_data),
                write_func: (c, v) => write_variable(c, variable, unit, v, stored),
                unit: unit)
        {
            Stored = stored;
            Variable = variable;
            DefaultValue = default_value;
            connection
                .Convert(c => c.MemoryBuffer)
                .ConvertMany(c => c.ObserveGlobalModel(m => get_data(m, variable, unit, convert_data)))
                .BindTo(this, v => v.Value);
        }

        static string sanitize_value(TData value)
        {
            return $"{value:0.00}"
                .ToLower()
                .Replace(',', '.');
        }

        static Optional<TData> get_data(RRF_GlobalModel global, string variable, VariableUnit unit, Func<object, TData> convert_data)
        {
            var lower_unit = unit.Value.ToLower();
            return global.Lookup($"{variable}_{lower_unit}")
                .Convert(v => convert_data != null ? convert_data(v) : v.ToObject<TData>());
        }

        static async Task<Optional<TData>> read_variable(RRF_Connection connection, string variable, VariableUnit unit, Func<object, TData> convert_data = default)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var global = await connection.MemoryBuffer.GetModelDataAsync<RRF_GlobalModel>(cts.Token);
            if (!global.HasValue)
                return default;
            return get_data(global.Value, variable, unit, convert_data);
        }

        static async Task<bool> write_variable(RRF_Connection c, string variable, VariableUnit unit, TData v, bool stored)
        {
            var lower_unit = unit.Value.ToLower();

            var s_value = sanitize_value(v);
            var write_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await c.PostGCodeAsync($"set global.{variable}_{lower_unit} = {s_value}", write_cts.Token))
            {
                c.Flux.Messages.LogMessage($"Impossibile scrivere la variabile {variable}_{lower_unit}", "Errore durante l'esecuzione del gcode", MessageLevel.ERROR, 0);
                return false;
            }

            if (stored)
            {
                var load_var_macro = $"load_{variable}_{lower_unit}.g";
                var gcode = WriteVariableString(variable, unit, v).ToOptional();
                var put_file_cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                if (!await c.PutFileAsync(c => ((RRF_Connection)c).GlobalPath, load_var_macro, put_file_cts.Token, gcode))
                {
                    c.Flux.Messages.LogMessage($"Impossibile salvare la variabile {variable}_{lower_unit}", "Errore durante la scrittura del file", MessageLevel.ERROR, 0);
                    return false;
                }
            }

            return true;
        }

        public async Task<bool> CreateVariableAsync(CancellationToken ct)
        {
            if (!Connection.HasValue)
                return false;

            var gcode = WriteVariableString(Variable, Unit.Value, DefaultValue.ValueOrDefault());

            return await Connection.Value.PutFileAsync(
                c => ((RRF_Connection)c).GlobalPath,
                LoadVariableMacro, 
                ct, gcode.ToOptional());
        }
        private static IEnumerable<string> WriteVariableString(string variable, VariableUnit unit, TData value)
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

        public RRF_ArrayGlobalModel(
            IObservable<Optional<RRF_Connection>> connection, 
            string name,
            IObservable<IEnumerable<VariableUnit>> variable_units,
            bool stored,
            Optional<TData> default_value = default,
            Func<object, TData> convert_data = default)
            : base(name, FluxMemReadPriority.DISABLED)
        {
            Variables = ObservableChangeSet.Create<IFLUX_Variable<TData, TData>, VariableUnit>(v =>
            {
                return variable_units.Subscribe(unit =>
                {
                    var variables = unit.Select(get_variable);
                    v.EditDiff(variables, (v1, v2) => v1.Name == v2.Name);
                });
            }, v => v.Unit.ValueOr(() => ""))
            .AsObservableCache();

            RRF_ArrayVariableGlobalModel<TData> get_variable(VariableUnit unit) => new RRF_ArrayVariableGlobalModel<TData>(connection, name, unit, stored, default_value, convert_data);
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
