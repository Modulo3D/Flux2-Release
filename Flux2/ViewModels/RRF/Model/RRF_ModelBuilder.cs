﻿using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
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
            public RRF_VariableStoreBase VariableStore { get; }
            public RRF_ConnectionProvider ConnectionProvider { get; }
            public Func<RRF_MemoryBuffer, Task<Optional<TModel>>> ReadModel { get; }
            public Func<RRF_ObjectModel, IObservable<Optional<TModel>>> GetModel { get; }

            public RRF_InnerModelBuilder(
                RRF_VariableStoreBase variable_store,
                Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
                Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model)
            {
                ReadModel = read_model;
                VariableStore = variable_store;
                GetModel = r => get_model(r).DistinctUntilChanged();
                ConnectionProvider = variable_store.ConnectionProvider;
            }

            public void CreateVariable<TRData, TWData>(
                Expression<Func<RRF_VariableStoreBase, IFLUX_Variable<TRData, TWData>>> variable_expression,
                Func<RRF_ConnectionProvider, TModel, Optional<TRData>> get_data)
            {
                
                var variable_name = string.Join('/', variable_expression.GetMembersName());
                var variable = new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, variable_name, ReadModel, GetModel, get_data);
                VariableStore.RegisterVariable(variable_expression, variable);
            }

            public void CreateVariable<TRData, TWData>(
                Expression<Func<RRF_VariableStoreBase, IFLUX_Variable<TRData, TWData>>> variable_expression,
                Func<RRF_ConnectionProvider, TModel, Task<Optional<TRData>>> get_data)
            {
                
                var variable_name = string.Join('/', variable_expression.GetMembersName());
                var variable = new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, variable_name, ReadModel, GetModel, get_data);
                VariableStore.RegisterVariable(variable_expression, variable);
            }
            public void CreateVariable<TRData, TWData>(
                Expression<Func<RRF_VariableStoreBase, IFLUX_Variable<TRData, TWData>>> variable_expression,
                Func<RRF_ConnectionProvider, TModel, Task<TRData>> get_data)
            {
                
                var variable_name = string.Join('/', variable_expression.GetMembersName());
                var variable = new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, variable_name, ReadModel, GetModel, async (c, m) => await get_data(c, m));
                VariableStore.RegisterVariable(variable_expression, variable);
            }
            public void CreateVariable<TRData, TWData>(
                Expression<Func<RRF_VariableStoreBase, IFLUX_Variable<TRData, TWData>>> variable_expression,
                Func<RRF_ConnectionProvider, TModel, Optional<TRData>> get_data,
                Func<RRF_ConnectionProvider, TWData, CancellationToken, Task<bool>> write_data = default)
            {
                
                var variable_name = string.Join('/', variable_expression.GetMembersName());
                var variable = new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, variable_name, ReadModel, GetModel, get_data, write_data);
                VariableStore.RegisterVariable(variable_expression, variable);
            }

            public void CreateVariable<TRData, TWData>(
                Expression<Func<RRF_VariableStoreBase, IFLUX_Variable<TRData, TWData>>> variable_expression,
                Func<RRF_ConnectionProvider, TModel, Task<Optional<TRData>>> get_data,
                Func<RRF_ConnectionProvider, TWData, CancellationToken, Task<bool>> write_data = default)
            {
                
                var variable_name = string.Join('/', variable_expression.GetMembersName());
                var variable = new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, variable_name, ReadModel, GetModel, get_data, write_data);
                VariableStore.RegisterVariable(variable_expression, variable);
            }

            public void CreateVariable<TRData, TWData>(
                Expression<Func<RRF_VariableStoreBase, Optional<IFLUX_Variable<TRData, TWData>>>> variable_expression,
                Func<RRF_ConnectionProvider, TModel, Optional<TRData>> get_data)
            {
                
                var variable_name = string.Join('/', variable_expression.GetMembersName());
                var variable = new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, variable_name, ReadModel, GetModel, get_data);
                VariableStore.RegisterVariable(variable_expression, variable);
            }

            public void CreateVariable<TRData, TWData>(
                Expression<Func<RRF_VariableStoreBase, Optional<IFLUX_Variable<TRData, TWData>>>> variable_expression,
                Func<RRF_ConnectionProvider, TModel, Task<Optional<TRData>>> get_data)
            {
                
                var variable_name = string.Join('/', variable_expression.GetMembersName());
                var variable = new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, variable_name, ReadModel, GetModel, get_data);
                VariableStore.RegisterVariable(variable_expression, variable);
            }

            public void CreateVariable<TRData, TWData>(
                Expression<Func<RRF_VariableStoreBase, Optional<IFLUX_Variable<TRData, TWData>>>> variable_expression,
                Func<RRF_ConnectionProvider, TModel, Optional<TRData>> get_data,
                Func<RRF_ConnectionProvider, TWData, CancellationToken, Task<bool>> write_data = default)
            {
                
                var variable_name = string.Join('/', variable_expression.GetMembersName());
                var variable = new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, variable_name, ReadModel, GetModel, get_data, write_data);
                VariableStore.RegisterVariable(variable_expression, variable);
            }

            public void CreateVariable<TRData, TWData>(
                Expression<Func<RRF_VariableStoreBase, Optional<IFLUX_Variable<TRData, TWData>>>> variable_expression,
                Func<RRF_ConnectionProvider, TModel, Task<Optional<TRData>>> get_data,
                Func<RRF_ConnectionProvider, TWData, CancellationToken, Task<bool>> write_data = default)
            {
                
                var variable_name = string.Join('/', variable_expression.GetMembersName());
                var variable = new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, variable_name, ReadModel, GetModel, get_data, write_data);
                VariableStore.RegisterVariable(variable_expression, variable);
            }

            public class RRF_ArrayBuilder<TList>
            {
                public VariableUnits Units { get; }

                public RRF_ConnectionProvider ConnectionProvider { get; }
                public Func<TModel, Optional<List<TList>>> GetVariables { get; }
                public Func<RRF_MemoryBuffer, Task<Optional<TModel>>> ReadModel { get; }
                public Func<RRF_ObjectModel, IObservable<Optional<TModel>>> GetModel { get; }
                public RRF_VariableStoreBase VariableStore { get; }

                public RRF_ArrayBuilder(
                    RRF_VariableStoreBase variable_store,
                    Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
                    Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model,
                    Func<TModel, Optional<List<TList>>> get_list,
                    VariableUnits units)
                {
                    Units = units;
                    GetModel = get_model;
                    ReadModel = read_model;
                    GetVariables = get_list;
                    VariableStore = variable_store;
                    ConnectionProvider = variable_store.ConnectionProvider;
                }

                public void CreateArray<TRData, TWData>(
                    Expression<Func<RRF_VariableStoreBase, IFLUX_Array<TRData, TWData>>> array_expression,
                    Func<RRF_ConnectionProvider, TList, Optional<TRData>> get_data,
                    params VariableRange[] variables_range)
                {
                    
                    var array_name = string.Join('/', array_expression.GetMembersName());
                    Optional<RRF_VariableObjectModel<TModel, TRData, TWData>> get_variable(VariableUnit unit)
                    {
                        Optional<TList> get_value(List<TList> list) => list.ElementAtOrDefault(unit.Address);
                        Optional<TRData> get_variable(RRF_ConnectionProvider c, TModel m) => GetVariables(m).Convert(get_value).Convert(m => get_data(c, m));
                        return new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, $"{array_name} {unit.Alias}", ReadModel, GetModel, get_variable, unit: unit);
                    }

                    var variable_units = variables_range.Length == 0 ? Units : variables_range
                        .SelectMany(r => r.Aliases)
                        .Select(a => Units.Lookup(a))
                        .Where(u => u.HasValue)
                        .Select(u => u.Value)
                        .ToArray();

                    var array = new RRF_ArrayObjectModel<TModel, TRData, TWData>(array_name, variable_units, get_variable);
                    VariableStore.RegisterArray(array_expression, array);
                }
                public void CreateArray<TRData, TWData>(
                    Expression<Func<RRF_VariableStoreBase, Optional<IFLUX_Array<TRData, TWData>>>> array_expression,
                    Func<RRF_ConnectionProvider, TList, Optional<TRData>> get_data,
                    params VariableRange[] variables_range)
                {
                    
                    var array_name = string.Join('/', array_expression.GetMembersName());
                    Optional<RRF_VariableObjectModel<TModel, TRData, TWData>> get_variable(VariableUnit unit)
                    {
                        Optional<TList> get_value(List<TList> list) => list.ElementAtOrDefault(unit.Address);
                        Optional<TRData> get_variable(RRF_ConnectionProvider c, TModel m) => GetVariables(m).Convert(get_value).Convert(m => get_data(c, m));
                        return new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, $"{array_name} {unit.Alias}", ReadModel, GetModel, get_variable, unit: unit);
                    }

                    var variable_units = variables_range.Length == 0 ? Units : variables_range
                        .SelectMany(r => r.Aliases)
                        .Select(a => Units.Lookup(a))
                        .Where(u => u.HasValue)
                        .Select(u => u.Value)
                        .ToArray();

                    var array = new RRF_ArrayObjectModel<TModel, TRData, TWData>(array_name, variable_units, get_variable);
                    VariableStore.RegisterArray(array_expression, array);
                }
                public void CreateArray<TRData, TWData>(
                    Expression<Func<RRF_VariableStoreBase, IFLUX_Array<TRData, TWData>>> array_expression,
                    Func<RRF_ConnectionProvider, TList, Optional<TRData>> get_data,
                    Func<RRF_ConnectionProvider, TWData, VariableUnit, CancellationToken, Task<bool>> write_data,
                    params VariableRange[] variables_range)
                {
                    
                    var array_name = string.Join('/', array_expression.GetMembersName());
                    Optional<RRF_VariableObjectModel<TModel, TRData, TWData>> get_variable(VariableUnit unit)
                    {
                        Optional<TList> get_value(List<TList> list) => list.ElementAtOrDefault(unit.Address);
                        Task<bool> write_unit_data(RRF_ConnectionProvider c, TWData d, CancellationToken ct) => write_data(c, d, unit, ct);
                        Optional<TRData> get_variable(RRF_ConnectionProvider c, TModel m) => GetVariables(m).Convert(get_value).Convert(m => get_data(c, m));
                        return new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, $"{array_name} {unit.Alias}", ReadModel, GetModel, get_variable, write_unit_data, unit);
                    }

                    var variable_units = variables_range.Length == 0 ? Units : variables_range
                        .SelectMany(r => r.Aliases)
                        .Select(a => Units.Lookup(a))
                        .Where(u => u.HasValue)
                        .Select(u => u.Value)
                        .ToArray();

                    var array = new RRF_ArrayObjectModel<TModel, TRData, TWData>(array_name, variable_units, get_variable);
                    VariableStore.RegisterArray(array_expression, array);
                }
                public void CreateArray<TRData, TWData>(
                    Expression<Func<RRF_VariableStoreBase, Optional<IFLUX_Array<TRData, TWData>>>> array_expression,
                    Func<RRF_ConnectionProvider, TList, Optional<TRData>> get_data,
                    Func<RRF_ConnectionProvider, TWData, VariableUnit, CancellationToken, Task<bool>> write_data,
                    params VariableRange[] variables_range)
                {
                    
                    var array_name = string.Join('/', array_expression.GetMembersName());
                    Optional<RRF_VariableObjectModel<TModel, TRData, TWData>> get_variable(VariableUnit unit)
                    {
                        Optional<TList> get_value(List<TList> list) => list.ElementAtOrDefault(unit.Address);
                        Task<bool> write_unit_data(RRF_ConnectionProvider c, TWData d, CancellationToken ct) => write_data(c, d, unit, ct);
                        Optional<TRData> get_variable(RRF_ConnectionProvider c, TModel m) => GetVariables(m).Convert(get_value).Convert(m => get_data(c, m));
                        return new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, $"{array_name} {unit.Alias}", ReadModel, GetModel, get_variable, write_unit_data, unit);
                    }

                    var variable_units = variables_range.Length == 0 ? Units : variables_range
                        .SelectMany(r => r.Aliases)
                        .Select(a => Units.Lookup(a))
                        .Where(u => u.HasValue)
                        .Select(u => u.Value)
                        .ToArray();

                    var array = new RRF_ArrayObjectModel<TModel, TRData, TWData>(array_name, variable_units, get_variable);
                    VariableStore.RegisterArray(array_expression, array);
                }

                public void CreateVariable<TRData, TWData>(
                    Expression<Func<RRF_VariableStoreBase, Optional<IFLUX_Variable<TRData, TWData>>>> variable_expression,
                    Func<RRF_ConnectionProvider, TList, Optional<TRData>> get_data,
                    VariableAlias alias)
                {
                    
                    var variable_name = string.Join('/', variable_expression.GetMembersName());
                    Optional<RRF_VariableObjectModel<TModel, TRData, TWData>> get_variable(VariableUnit unit)
                    {
                        Optional<TList> get_value(List<TList> list) => list.ElementAtOrDefault(unit.Address);
                        Optional<TRData> get_variable(RRF_ConnectionProvider c, TModel m) => GetVariables(m).Convert(get_value).Convert(m => get_data(c, m));
                        return new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, variable_name, ReadModel, GetModel, get_variable, unit: unit);
                    }

                    var unit = Units.Lookup(alias);
                    if (!unit.HasValue)
                        return;

                    var variable = get_variable(unit.Value);
                    if (!variable.HasValue)
                        return;

                    VariableStore.RegisterVariable(variable_expression, variable.Value);
                }

                public void CreateVariable<TRData, TWData>(
                    Expression<Func<RRF_VariableStoreBase, Optional<IFLUX_Variable<TRData, TWData>>>> variable_expression,
                    Func<RRF_ConnectionProvider, TList, Optional<TRData>> get_data,
                    Func<RRF_ConnectionProvider, TWData, VariableUnit, CancellationToken, Task<bool>> write_data,
                    VariableAlias alias)
                {
                    
                    var variable_name = string.Join('/', variable_expression.GetMembersName());
                    Optional<RRF_VariableObjectModel<TModel, TRData, TWData>> get_variable(VariableUnit unit)
                    {
                        Optional<TList> get_value(List<TList> list) => list.ElementAtOrDefault(unit.Address);
                        Task<bool> write_unit_data(RRF_ConnectionProvider c, TWData d, CancellationToken ct) => write_data(c, d, unit, ct);
                        Optional<TRData> get_variable(RRF_ConnectionProvider c, TModel m) => GetVariables(m).Convert(get_value).Convert(m => get_data(c, m));
                        return new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, variable_name, ReadModel, GetModel, get_variable, write_unit_data, unit);
                    }

                    var unit = Units.Lookup(alias);
                    if (!unit.HasValue)
                        return;

                    var variable = get_variable(unit.Value);
                    if (!variable.HasValue)
                        return;

                    VariableStore.RegisterVariable(variable_expression, variable.Value);
                }

                public void CreateVariable<TRData, TWData>(
                    Expression<Func<RRF_VariableStoreBase, Optional<IFLUX_Variable<TRData, TWData>>>> variable_expression,
                    Func<RRF_ConnectionProvider, List<TList>, Optional<TRData>> get_data)
                {
                    
                    var variable_name = string.Join('/', variable_expression.GetMembersName());
                    Optional<RRF_VariableObjectModel<TModel, TRData, TWData>> get_variable()
                    {
                        Optional<TRData> get_variable(RRF_ConnectionProvider c, TModel m) => GetVariables(m).Convert(m => get_data(c, m));
                        return new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, variable_name, ReadModel, GetModel, get_variable);
                    }

                    var variable = get_variable();
                    if (!variable.HasValue)
                        return;

                    VariableStore.RegisterVariable(variable_expression, variable.Value);
                }

                public void CreateVariable<TRData, TWData>(
                    Expression<Func<RRF_VariableStoreBase, IFLUX_Variable<TRData, TWData>>> variable_expression,
                    Func<RRF_ConnectionProvider, List<TList>, Optional<TRData>> get_data)
                {
                    
                    var variable_name = string.Join('/', variable_expression.GetMembersName());
                    Optional<RRF_VariableObjectModel<TModel, TRData, TWData>> get_variable()
                    {
                        Optional<TRData> get_variable(RRF_ConnectionProvider c, TModel m) => GetVariables(m).Convert(m => get_data(c, m));
                        return new RRF_VariableObjectModel<TModel, TRData, TWData>(ConnectionProvider, variable_name, ReadModel, GetModel, get_variable);
                    }

                    var variable = get_variable();
                    if (!variable.HasValue)
                        return;

                    VariableStore.RegisterVariable(variable_expression, variable.Value);
                }
            }
            public RRF_ArrayBuilder<TList> CreateArray<TList>(Func<TModel, Optional<List<TList>>> get_list, VariableUnits units)
            {
                return new RRF_ArrayBuilder<TList>(VariableStore, ReadModel, GetModel, get_list, units);
            }
        }

        public static RRF_InnerModelBuilder<TModel> CreateModel<TModel>(
            RRF_VariableStoreBase variable_store,
            Func<RRF_MemoryBuffer, Task<Optional<TModel>>> read_model,
            Func<RRF_ObjectModel, IObservable<Optional<TModel>>> get_model)
        {
            return new RRF_InnerModelBuilder<TModel>(variable_store, read_model, get_model);
        }

        public static RRF_InnerModelBuilder<TModel> CreateModel<TModel>(
            RRF_VariableStoreBase variable_store,
            Expression<Func<RRF_ObjectModel, Optional<TModel>>> model_expr,
            TimeSpan read_timeout)
        {
            return new RRF_InnerModelBuilder<TModel>(variable_store, read_model, m => m.WhenAnyValue(model_expr));
            async Task<Optional<TModel>> read_model(RRF_MemoryBuffer buffer)
            {
                using var cts = new CancellationTokenSource(read_timeout);
                return await buffer.GetModelDataAsync(model_expr, cts.Token);
            }
        }

        public static RRF_InnerModelBuilder<(TModel1, TModel2)> CreateModel<TModel1, TModel2>(
            RRF_VariableStoreBase variable_store,
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
                using var cts = new CancellationTokenSource(read_timeout);
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
            RRF_VariableStoreBase variable_store,
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
                using var cts = new CancellationTokenSource(read_timeout);
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
             RRF_VariableStoreBase variable_store,
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
                using var cts = new CancellationTokenSource(read_timeout);
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

    public class RRF_GlobalModelBuilder
    {
        public class RRF_InnerGlobalModelBuilder
        {
            private RRF_VariableStoreBase VariableStore { get; }
            public RRF_ConnectionProvider ConnectionProvider { get; }
            public RRF_InnerGlobalModelBuilder(
                RRF_VariableStoreBase variable_store)
            {
                VariableStore = variable_store;
                ConnectionProvider = variable_store.ConnectionProvider;
            }

            public void CreateVariable<TData>(
                Expression<Func<RRF_VariableStoreBase, IFLUX_Variable<TData, TData>>> variable_expression,
                bool stored,
                TData default_value,
                Func<object, TData> convert_value = default)
            {
                
                var variable_name = string.Join('/', variable_expression.GetMembersName());
                var variable = new RRF_VariableGlobalModel<TData>(ConnectionProvider, variable_name, stored, default_value, convert_value);
                VariableStore.RegisterVariable(variable_expression, variable);
            }
            public void CreateVariable<TData>(
                Expression<Func<RRF_VariableStoreBase, Optional<IFLUX_Variable<TData, TData>>>> variable_expression,
                bool stored,
                TData default_value,
                Func<object, TData> convert_value = default)
            {
                
                var variable_name = string.Join('/', variable_expression.GetMembersName());
                var variable = new RRF_VariableGlobalModel<TData>(ConnectionProvider, variable_name, stored, default_value, convert_value);
                VariableStore.RegisterVariable(variable_expression, variable);
            }

            public void CreateArray<TData>(
                Expression<Func<RRF_VariableStoreBase, IFLUX_Array<TData, TData>>> array_expression,
                bool stored,
                TData default_value,
                params VariableRange[] variables_range)
            {
                var variable_units = new VariableUnits(variables_range);
                
                var array_name = string.Join('/', array_expression.GetMembersName());
                var array = new RRF_ArrayGlobalModel<TData>(ConnectionProvider, array_name, stored, default_value, variable_units);
                VariableStore.RegisterArray(array_expression, array);
            }

            public void CreateArray<TData>(
                Expression<Func<RRF_VariableStoreBase, Optional<IFLUX_Array<TData, TData>>>> array_expression,
                bool stored,
                TData default_value,
                params VariableRange[] variables_range)
            {
                var variable_units = new VariableUnits(variables_range);
                
                var array_name = string.Join('/', array_expression.GetMembersName());
                var array = new RRF_ArrayGlobalModel<TData>(ConnectionProvider, array_name, stored, default_value, variable_units);
                VariableStore.RegisterArray(array_expression, array);
            }

            public void CreateArray<TData>(
                Expression<Func<RRF_VariableStoreBase, IFLUX_Array<TData, TData>>> array_expression,
                bool stored,
                TData default_value,
                Func<object, TData> convert_value,
                params VariableRange[] variables_range)
            {
                var variable_units = new VariableUnits(variables_range);
                
                var array_name = string.Join('/', array_expression.GetMembersName());
                var array = new RRF_ArrayGlobalModel<TData>(ConnectionProvider, array_name, stored, default_value, variable_units, convert_value);
                VariableStore.RegisterArray(array_expression, array);
            }

            public void CreateArray<TData>(
                Expression<Func<RRF_VariableStoreBase, Optional<IFLUX_Array<TData, TData>>>> array_expression,
                bool stored,
                TData default_value,
                Func<object, TData> convert_value = default,
                params VariableRange[] variables_range)
            {
                var variable_units = new VariableUnits(variables_range);
                
                var array_name = string.Join('/', array_expression.GetMembersName());
                var array = new RRF_ArrayGlobalModel<TData>(ConnectionProvider, array_name, stored, default_value, variable_units, convert_value);
                VariableStore.RegisterArray(array_expression, array);
            }
        }

        public static RRF_InnerGlobalModelBuilder CreateModel(RRF_VariableStoreBase variable_store)
        {
            return new RRF_InnerGlobalModelBuilder(variable_store);
        }
    }
}
