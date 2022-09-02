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
    public static class RRF_ModelBuilder<TRRF_VariableStore>
        where TRRF_VariableStore : IFLUX_VariableStore<TRRF_VariableStore, RRF_ConnectionProvider>
    {
        public class RRF_InnerModelBuilder<TModel>
        {
            public RRF_ConnectionProvider ConnectionProvider { get; }
            public Func<RRF_MemoryBuffer, Task<Optional<TModel>>> ReadModel { get; }
            public Func<RRF_ObjectModel, IObservable<Optional<TModel>>> GetModel { get; }
            public IFLUX_VariableStore<TRRF_VariableStore, RRF_ConnectionProvider> VariableStore { get; }

            public RRF_InnerModelBuilder(
                IFLUX_VariableStore<TRRF_VariableStore, RRF_ConnectionProvider> variable_store,
                Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
                Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model)
            {
                GetModel = get_model;
                ReadModel = read_model;
                VariableStore = variable_store;
                ConnectionProvider = variable_store.ConnectionProvider;
            }

            public RRF_VariableObjectModel<TModel, TRData, Unit> CreateVariable<TRData>(
                string name,
                Func<RRF_Connection, TModel, TRData> get_data,
                VariableUnit unit = default)
            {
                var variable = new RRF_VariableObjectModel<TModel, TRData, Unit>(ConnectionProvider, name, ReadModel, GetModel, (c, m) => get_data(c, m).ToOptional(), unit);
                return VariableStore.RegisterVariable(variable);
            }
            public RRF_VariableObjectModel<TModel, TRData, Unit> CreateVariable<TRData>(
                string name,
                Func<RRF_Connection, TModel, Optional<TRData>> get_data,
                VariableUnit unit = default)
            {
                var variable = new RRF_VariableObjectModel<TModel, TRData, Unit>(ConnectionProvider, name, ReadModel, GetModel, get_data, unit);
                return VariableStore.RegisterVariable(variable);
            }
            public RRF_VariableObjectModel<TModel, TRData, Unit> CreateVariable<TRData>(
                string name,
                Func<RRF_Connection, TModel, Task<Optional<TRData>>> get_data,
                VariableUnit unit = default)
            {
                var variable = new RRF_VariableObjectModel<TModel, TRData, Unit>(ConnectionProvider, name, ReadModel, GetModel, get_data, unit);
                return VariableStore.RegisterVariable(variable);
            }

            public RRF_VariableObjectModel<TModel, TRData, TWData> CreateVariable<TRData, TWData>(
                string name,
                Func<RRF_Connection, TModel, Optional<TRData>> get_data,
                VariableUnit unit = default)
            {
                var variable = new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, name, ReadModel, GetModel, get_data, unit);
                return VariableStore.RegisterVariable(variable);   
            }
            public RRF_VariableObjectModel<TModel, TRData, TWData> CreateVariable<TRData, TWData>(
                string name,
                Func<RRF_Connection, TModel, Task<Optional<TRData>>> get_data,
                VariableUnit unit = default)
            {
                var variable = new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, name, ReadModel, GetModel, get_data, unit);
                return VariableStore.RegisterVariable(variable);
            }
            public RRF_VariableObjectModel<TModel, TRData, TWData> CreateVariable<TRData, TWData>(
                string name,
                Func<RRF_Connection, TModel, Optional<TRData>> get_data,
                Func<RRF_Connection, TWData, bool> write_data,
                VariableUnit unit = default)
            {
                var variable = new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, name, ReadModel, GetModel, get_data, write_data, unit);
                return VariableStore.RegisterVariable(variable);
            }
            public RRF_VariableObjectModel<TModel, TRData, TWData> CreateVariable<TRData, TWData>(
                string name,
                Func<RRF_Connection, TModel, Task<Optional<TRData>>> get_data,
                Func<RRF_Connection, TWData, bool> write_data,
                VariableUnit unit = default)
            {
                var variable = new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, name, ReadModel, GetModel, get_data, write_data, unit);
                return VariableStore.RegisterVariable(variable);
            }
            public RRF_VariableObjectModel<TModel, TRData, TWData> CreateVariable<TRData, TWData>(
                string name,
                Func<RRF_Connection, TModel, Optional<TRData>> get_data,
                Func<RRF_Connection, TWData, Task<bool>> write_data,
                VariableUnit unit = default)
            {
                var variable = new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, name, ReadModel, GetModel, get_data, write_data, unit);
                return VariableStore.RegisterVariable(variable);
            }

            internal RRF_InnerModelBuilder<RRF_ObjectModelState>.RRF_ArrayBuilder<RRF_ObjectModelGpOut> CreateArray(Func<RRF_ObjectModelState, Optional<List<RRF_ObjectModelGpOut>>> p, object gpoutUnits)
            {
                throw new NotImplementedException();
            }

            internal RRF_InnerModelBuilder<RRF_ObjectModelSensors>.RRF_ArrayBuilder<RRF_ObjectModelGpIn> CreateArray(Func<RRF_ObjectModelSensors, Optional<List<RRF_ObjectModelGpIn>>> p, object gpinUnits)
            {
                throw new NotImplementedException();
            }

            public RRF_VariableObjectModel<TModel, TRData, TWData> CreateVariable<TRData, TWData>(
                string name,
                Func<RRF_Connection, TModel, Task<Optional<TRData>>> get_data,
                Func<RRF_Connection, TWData, Task<bool>> write_data,
                VariableUnit unit = default)
            {
                var variable = new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, name, ReadModel, GetModel, get_data, write_data, unit);
                return VariableStore.RegisterVariable(variable);
            }

            public class RRF_ArrayBuilder<TList>
            {
                private Dictionary<VariableAlias, VariableUnit> Units { get; }

                public RRF_ConnectionProvider ConnectionProvider { get; }
                public Func<TModel, Optional<List<TList>>> GetVariables { get; }
                public Func<RRF_MemoryBuffer, Task<Optional<TModel>>> ReadModel { get; }
                public Func<RRF_ObjectModel, IObservable<Optional<TModel>>> GetModel { get; }
                public IFLUX_VariableStore<TRRF_VariableStore, RRF_ConnectionProvider> VariableStore { get; }

                public RRF_ArrayBuilder(
                    IFLUX_VariableStore<TRRF_VariableStore, RRF_ConnectionProvider> variable_store,
                    Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
                    Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
                    Func<TModel, Optional<List<TList>>> get_list,
                    Dictionary<VariableAlias, VariableUnit> units)
                {
                    Units = units;
                    GetModel = get_model;
                    ReadModel = read_model;
                    GetVariables = get_list;
                    VariableStore = variable_store;
                    ConnectionProvider = variable_store.ConnectionProvider;
                }

                public RRF_ArrayObjectModel<TModel, TRData, Unit> CreateArray<TRData>(
                    string name,
                    Func<RRF_Connection, TList, Optional<TRData>> get_data)
                {
                    var variable_range = VariableRange.Range(0, (ushort)Units.Count);
                    return CreateArray(name, get_data, variable_range);
                }
                public RRF_ArrayObjectModel<TModel, TRData, TWData> CreateArray<TRData, TWData>(
                    string name,
                    Func<RRF_Connection, TList, Optional<TRData>> get_data)
                {
                    var variable_range = VariableRange.Range(0, (ushort)Units.Count);
                    return CreateArray<TRData, TWData>(name, get_data, variable_range);
                }
                public RRF_ArrayObjectModel<TModel, TRData, TWData> CreateArray<TRData, TWData>(
                    string name,
                    Func<RRF_Connection, TList, Optional<TRData>> get_data,
                    Func<RRF_Connection, TWData, VariableUnit, bool> write_data)
                {
                    var variable_range = VariableRange.Range(0, (ushort)Units.Count);
                    return CreateArray(name, get_data, write_data, variable_range);
                }
                public RRF_ArrayObjectModel<TModel, TRData, TWData> CreateArray<TRData, TWData>(
                    string name,
                    Func<RRF_Connection, TList, Optional<TRData>> get_data,
                    Func<RRF_Connection, TWData, VariableUnit, Task<bool>> write_data)
                {
                    var variable_range = VariableRange.Range(0, (ushort)Units.Count);
                    return CreateArray(name, get_data, write_data, variable_range);
                }

                public RRF_ArrayObjectModel<TModel, TRData, Unit> CreateArray<TRData>(
                    string name,
                    Func<RRF_Connection, TList, Optional<TRData>> get_data,
                    VariableRange variables_range)
                {
                    Optional<IFLUX_Variable<TRData, Unit>> get_variable(VariableAlias alias)
                    {
                        var unit = Units.Lookup(alias);
                        if (!unit.HasValue)
                            return default;


                        Optional<TList> get_value(List<TList> list) => list.ElementAtOrDefault(unit.Value.Address);
                        Optional<TRData> get_variable(RRF_Connection c, TModel m) => GetVariables(m).Convert(get_value).Convert(m => get_data(c, m));
                        return new RRF_VariableObjectModel<TModel, TRData, Unit>(ConnectionProvider, $"{name} {alias}", ReadModel, GetModel, get_variable, unit.Value);
                    }

                    var variable_units = Units.Skip(variables_range.Start).Take(variables_range.Count).ToDictionary();
                    var array = new RRF_ArrayObjectModel<TModel, TRData, Unit>(name, variable_units, get_variable);
                    return VariableStore.RegisterArray(array);
                }
                public RRF_ArrayObjectModel<TModel, TRData, TWData> CreateArray<TRData, TWData>(
                    string name,
                    Func<RRF_Connection, TList, Optional<TRData>> get_data,
                    VariableRange variables_range)
                {
                    Optional<IFLUX_Variable<TRData, TWData>> get_variable(VariableAlias alias)
                    {
                        var unit = Units.Lookup(alias);
                        if (!unit.HasValue)
                            return default;

                        Optional<TList> get_value(List<TList> list) => list.ElementAtOrDefault(unit.Value.Address);
                        Optional<TRData> get_variable(RRF_Connection c, TModel m) => GetVariables(m).Convert(get_value).Convert(m => get_data(c, m));
                        return new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, $"{name} {alias}", ReadModel, GetModel, get_variable, unit.Value);
                    }

                    var variable_units = Units.Skip(variables_range.Start).Take(variables_range.Count).ToDictionary();
                    var array = new RRF_ArrayObjectModel<TModel, TRData, TWData>(name, variable_units, get_variable);
                    return VariableStore.RegisterArray(array);
                }
                public RRF_ArrayObjectModel<TModel, TRData, TWData> CreateArray<TRData, TWData>(
                    string name,
                    Func<RRF_Connection, TList, Optional<TRData>> get_data,
                    Func<RRF_Connection, TWData, VariableUnit, bool> write_data,
                    VariableRange variables_range)
                {
                    Optional<IFLUX_Variable<TRData, TWData>> get_variable(VariableAlias alias)
                    {
                        var unit = Units.Lookup(alias);
                        if (!unit.HasValue)
                            return default;

                        bool write_unit_data(RRF_Connection c, TWData d) => write_data(c, d, unit.Value);
                        Optional<TList> get_value(List<TList> list) => list.ElementAtOrDefault(unit.Value.Address);
                        Optional<TRData> get_variable(RRF_Connection c, TModel m) => GetVariables(m).Convert(get_value).Convert(m => get_data(c, m));
                        return new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, $"{name} {alias}", ReadModel, GetModel, get_variable, write_unit_data, unit.Value);
                    }

                    var variable_units = Units.Skip(variables_range.Start).Take(variables_range.Count).ToDictionary();
                    var array = new RRF_ArrayObjectModel<TModel, TRData, TWData>(name, variable_units, get_variable);
                    return VariableStore.RegisterArray(array);
                }
                public RRF_ArrayObjectModel<TModel, TRData, TWData> CreateArray<TRData, TWData>(
                    string name,
                    Func<RRF_Connection, TList, Optional<TRData>> get_data,
                    Func<RRF_Connection, TWData, VariableUnit, Task<bool>> write_data,
                    VariableRange variables_range)
                {
                    Optional<IFLUX_Variable<TRData, TWData>> get_variable(VariableAlias alias)
                    {
                        var unit = Units.Lookup(alias);
                        if (!unit.HasValue)
                            return default;

                        Task<bool> write_unit_data(RRF_Connection c, TWData d) => write_data(c, d, unit.Value);
                        Optional<TList> get_value(List<TList> list) => list.ElementAtOrDefault(unit.Value.Address);
                        Optional<TRData> get_variable(RRF_Connection c, TModel m) => GetVariables(m).Convert(get_value).Convert(m => get_data(c, m));
                        return new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, $"{name} {alias}", ReadModel, GetModel, get_variable, write_unit_data, unit.Value);
                    }

                    var variable_units = Units.Skip(variables_range.Start).Take(variables_range.Count).ToDictionary();
                    var array = new RRF_ArrayObjectModel<TModel, TRData, TWData>(name, variable_units, get_variable);
                    return VariableStore.RegisterArray(array);
                }

                public Optional<IFLUX_Variable<TRData, Unit>> CreateVariable<TRData>(string name,
                    Func<RRF_Connection, TList, Optional<TRData>> get_data,
                    VariableAlias alias)
                {
                    Optional<IFLUX_Variable<TRData, Unit>> get_variable(VariableAlias alias)
                    {
                        var unit = Units.Lookup(alias);
                        if (!unit.HasValue)
                            return default;

                        Optional<TList> get_value(List<TList> list) => list.ElementAtOrDefault(unit.Value.Address);
                        Optional<TRData> get_variable(RRF_Connection c, TModel m) => GetVariables(m).Convert(get_value).Convert(m => get_data(c, m));
                        return new RRF_VariableObjectModel<TModel, TRData, Unit>(ConnectionProvider, $"{name} {alias}", ReadModel, GetModel, get_variable, unit.Value);
                    }

                    var variable = get_variable(alias);
                    if (!variable.HasValue)
                        return default;

                    return (RRF_VariableObjectModel<TModel, TRData, Unit>)VariableStore.RegisterVariable(variable.Value);
                }

                public Optional<IFLUX_Variable<TRData, TWData>> CreateVariable<TRData, TWData>(string name,
                    Func<RRF_Connection, TList, Optional<TRData>> get_data,
                    Func<RRF_Connection, TWData, VariableUnit, Task<bool>> write_data, 
                    VariableAlias alias)
                {
                    Optional<IFLUX_Variable<TRData, TWData>> get_variable(VariableAlias alias)
                    {
                        var unit = Units.Lookup(alias);
                        if (!unit.HasValue)
                            return default;

                        Task<bool> write_unit_data(RRF_Connection c, TWData d) => write_data(c, d, unit.Value);
                        Optional<TList> get_value(List<TList> list) => list.ElementAtOrDefault(unit.Value.Address);
                        Optional<TRData> get_variable(RRF_Connection c, TModel m) => GetVariables(m).Convert(get_value).Convert(m => get_data(c, m));
                        return new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, $"{name} {alias}", ReadModel, GetModel, get_variable, write_unit_data, unit.Value);
                    }

                    var variable = get_variable(alias);
                    if (!variable.HasValue)
                        return default;

                    return (RRF_VariableObjectModel<TModel, TRData, TWData>)VariableStore.RegisterVariable(variable.Value);
                }
            }
            public RRF_ArrayBuilder<TList> CreateArray<TList>(Func<TModel, Optional<List<TList>>> get_list, Dictionary<VariableAlias, VariableUnit> units)
            {
                return new RRF_ArrayBuilder<TList>(VariableStore, ReadModel, GetModel, get_list, units);
            }
        }

        public static RRF_InnerModelBuilder<TModel> CreateModel<TModel>(
            IFLUX_VariableStore<TRRF_VariableStore, RRF_ConnectionProvider> variable_store,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model)
        { 
            return new RRF_InnerModelBuilder<TModel>(variable_store, read_model, get_model);
        }

        public static RRF_InnerModelBuilder<TModel> CreateModel<TModel>(
            IFLUX_VariableStore<TRRF_VariableStore, RRF_ConnectionProvider> variable_store,
            Expression<Func<RRF_ObjectModel, Optional<TModel>>> model_expr,
            TimeSpan read_timeout)
        {
            return new RRF_InnerModelBuilder<TModel>(variable_store, read_model, m => m.WhenAnyValue(model_expr));
            async Task<Optional<TModel>> read_model(RRF_MemoryBuffer buffer)
            {
                var cts = new CancellationTokenSource(read_timeout);
                return await buffer.GetModelDataAsync(model_expr, cts.Token);
            }
        }

        public static RRF_InnerModelBuilder<(TModel1, TModel2)> CreateModel<TModel1, TModel2>(
            IFLUX_VariableStore<TRRF_VariableStore, RRF_ConnectionProvider> variable_store,
            Expression<Func<RRF_ObjectModel, Optional<TModel1>>> model_expr_1,
            Expression<Func<RRF_ObjectModel, Optional<TModel2>>> model_expr_2,
            TimeSpan read_timeout)
        {
            return new RRF_InnerModelBuilder<(TModel1, TModel2)>(variable_store, read_models, observe_models);
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

        public static RRF_InnerModelBuilder<(TModel1, TModel2, TModel3)> CreateModel<TModel1, TModel2, TModel3>(
            IFLUX_VariableStore<TRRF_VariableStore, RRF_ConnectionProvider> variable_store,
            Expression<Func<RRF_ObjectModel, Optional<TModel1>>> model_expr_1,
            Expression<Func<RRF_ObjectModel, Optional<TModel2>>> model_expr_2,
            Expression<Func<RRF_ObjectModel, Optional<TModel3>>> model_expr_3,
            TimeSpan read_timeout)
        {
            return new RRF_InnerModelBuilder<(TModel1, TModel2, TModel3)>(variable_store, read_models, observe_models);
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

        public static RRF_InnerModelBuilder<(TModel1, TModel2, TModel3, TModel4)> CreateModel<TModel1, TModel2, TModel3, TModel4>(
             IFLUX_VariableStore<TRRF_VariableStore, RRF_ConnectionProvider> variable_store,
             Expression<Func<RRF_ObjectModel, Optional<TModel1>>> model_expr_1,
             Expression<Func<RRF_ObjectModel, Optional<TModel2>>> model_expr_2,
             Expression<Func<RRF_ObjectModel, Optional<TModel3>>> model_expr_3,
             Expression<Func<RRF_ObjectModel, Optional<TModel4>>> model_expr_4,
             TimeSpan read_timeout)
        {
            return new RRF_InnerModelBuilder<(TModel1, TModel2, TModel3, TModel4)>(variable_store, read_models, observe_models);
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

    public class RRF_ArrayObjectModel<TVariable, TRData, TWData> : FLUX_Array<TRData, TWData>
    {
        public override string Group => "ObjectModel";
        public RRF_ArrayObjectModel(
            string name,
            Dictionary<VariableAlias, VariableUnit> variable_units, 
            Func<VariableAlias, Optional<IFLUX_Variable<TRData, TWData>>> get_variable)
            : base(name, FluxMemReadPriority.DISABLED)
        {
            var source_cache = (SourceCache<IFLUX_Variable<TRData, TWData>, VariableAlias>)Variables;
            foreach (var unit in variable_units)
            {
                var variable = get_variable(unit.Key);
                if (!variable.HasValue)
                    continue;
                source_cache.AddOrUpdate(variable.Value);
            }
        }

        public override Optional<VariableUnit> GetArrayUnit(ushort position)
        {
            return Variables.Items
                .ElementAtOrDefault(position)
                .ToOptional(v => v != null)
                .Convert(v => v.Unit);
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

    public class RRF_ArrayVariableGlobalModel<TData> : FLUX_VariableGP<RRF_ConnectionProvider, RRF_Connection, TData, TData>, IRRF_VariableGlobalModel
    {
        public bool Stored { get; }
        public string Variable { get; }
        public TData DefaultValue { get; }
        public override string Group => "Global";
        public string LoadVariableMacro => $"load_{Variable}_{Unit}.g";

        public RRF_ArrayVariableGlobalModel(
            RRF_ConnectionProvider connection_provider,
            string variable,
            VariableUnit unit, 
            bool stored,
            TData default_value,
            Func<object, TData> convert_data = default)
            : base(
                connection_provider,
                $"{variable} {unit.Alias}",
                FluxMemReadPriority.DISABLED,
                read_func: c => read_variable(c, variable, unit, convert_data),
                write_func: (c, v) => write_variable(c, variable, unit, v, stored),
                unit: unit)
        {
            Stored = stored;
            Variable = variable;
            DefaultValue = default_value;
            connection_provider.WhenAnyValue(c => c.Connection)
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

        static Optional<TData> get_data(RRF_ObjectModelGlobal global, string variable, VariableUnit unit, Func<object, TData> convert_data)
        {
            return global.Lookup($"{variable}_{unit}")
                .Convert(v => convert_data != null ? convert_data(v) : v.ToObject<TData>());
        }

        static async Task<Optional<TData>> read_variable(RRF_Connection connection, string variable, VariableUnit unit, Func<object, TData> convert_data = default)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var global = await connection.MemoryBuffer.GetModelDataAsync<RRF_ObjectModelGlobal>(cts.Token);
            if (!global.HasValue)
                return default;
            return get_data(global.Value, variable, unit, convert_data);
        }

        static async Task<bool> write_variable(RRF_Connection c, string variable, VariableUnit unit, TData v, bool stored)
        {
            var s_value = sanitize_value(v);
            var write_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!await c.PostGCodeAsync(new[] { $"set global.{variable}_{unit} = {s_value}" }, write_cts.Token))
            {
                c.Flux.Messages.LogMessage($"Impossibile scrivere la variabile {variable}_{unit}", "Errore durante l'esecuzione del gcode", MessageLevel.ERROR, 0);
                return false;
            }

            if (stored)
            {
                var load_var_macro = $"load_{variable}_{unit}.g";
                var gcode = WriteVariableString(variable, unit, v).ToOptional();
                var put_file_cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                if (!await c.PutFileAsync(c => ((RRF_Connection)c).GlobalPath, load_var_macro, put_file_cts.Token, gcode))
                {
                    c.Flux.Messages.LogMessage($"Impossibile salvare la variabile {variable}_{unit}", "Errore durante la scrittura del file", MessageLevel.ERROR, 0);
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
                LoadVariableMacro, 
                ct, gcode.ToOptional());
        }
        private static IEnumerable<string> WriteVariableString(string variable, VariableUnit unit, TData value)
        {
            var s_value = sanitize_value(value);
            yield return $"if (!exists(global.{variable}_{unit}))";
            yield return $"    global {variable}_{unit} = {s_value}";
            yield return $"else";
            yield return $"    set global.{variable}_{unit} = {s_value}";
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
            Dictionary<VariableAlias, VariableUnit> variable_units,
            Func<object, TData> convert_data = default)
            : base(variable, FluxMemReadPriority.DISABLED)
        {
            var source_cache = (SourceCache<IFLUX_Variable<TData, TData>, VariableAlias>)Variables;
            foreach (var unit in variable_units)
                source_cache.AddOrUpdate(get_variable(unit.Value));

            RRF_ArrayVariableGlobalModel<TData> get_variable(VariableUnit unit) => new RRF_ArrayVariableGlobalModel<TData>(connection_provider, variable, unit, stored, default_value, convert_data);
        }

        public override Optional<VariableUnit> GetArrayUnit(ushort position)
        {
            return Variables.Items
                .ElementAtOrDefault(position)
                .ToOptional(v => v != null)
                .Convert(v => v.Unit);
        }
    }

    public class RRF_GlobalModelBuilder<TRRF_VariableStore>
        where TRRF_VariableStore : IFLUX_VariableStore<TRRF_VariableStore, RRF_ConnectionProvider>
    {
        public class RRF_InnerGlobalModelBuilder
        {
            IFLUX_VariableStore<TRRF_VariableStore, RRF_ConnectionProvider> VariableStore { get; }
            public RRF_ConnectionProvider ConnectionProvider { get; }
            public RRF_InnerGlobalModelBuilder(
                IFLUX_VariableStore<TRRF_VariableStore, RRF_ConnectionProvider> variable_store)
            {
                VariableStore = variable_store;
                ConnectionProvider = variable_store.ConnectionProvider; 
            }

            public RRF_VariableGlobalModel<TData> CreateVariable<TData>(string name, bool stored, TData default_value, Func<object, TData> convert_value = default) 
            {
                var variable = new RRF_VariableGlobalModel<TData>(ConnectionProvider, name, stored, default_value, convert_value);
                return VariableStore.RegisterVariable(variable);
            }

            public RRF_ArrayGlobalModel<TData> CreateArray<TData>(string name, bool stored, TData default_value, Dictionary<VariableAlias, VariableUnit> variable_units, Func<object, TData> convert_value = default)
            {
                var variable = new RRF_ArrayGlobalModel<TData>(ConnectionProvider, name, stored, default_value, variable_units, convert_value);
                return VariableStore.RegisterArray(variable);
            }
        }

        public static RRF_InnerGlobalModelBuilder CreateModel(IFLUX_VariableStore<TRRF_VariableStore, RRF_ConnectionProvider> variable_store)
        {
            return new RRF_InnerGlobalModelBuilder(variable_store);
        }
    }
}
