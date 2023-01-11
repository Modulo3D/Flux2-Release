using DynamicData.Kernel;
using Modulo3DNet;
using System;
using System.Linq.Expressions;
using System.Reactive;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class OSAI_ModelBuilder
    {
        public OSAI_VariableStore VariableStore { get; }
        public OSAI_ConnectionProvider ConnectionProvider { get; }

        public OSAI_ModelBuilder(OSAI_VariableStore variable_store)
        {
            VariableStore = variable_store;
            ConnectionProvider = variable_store.ConnectionProvider;
        }

        // GP
        public void CreateVariable<TRData, TWData>(
            Expression<Func<OSAI_VariableStore, IFLUX_Variable<TRData, TWData>>> variable_expression,
            OSAI_ReadPriority priority,
            Func<OSAI_ConnectionProvider, Task<ValueResult<TRData>>> read_func = default,
            Func<OSAI_ConnectionProvider, TWData, Task<bool>> write_func = default)
        {
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_AsyncVariable<TRData, TWData>(ConnectionProvider, variable_name, priority, read_func, write_func);
            VariableStore.RegisterVariable(variable_expression, variable);
        }

        // NAMED
        public void CreateVariable(
            Expression<Func<OSAI_VariableStore, IFLUX_Variable<QueuePosition, QueuePosition>>> variable_expression,
            OSAI_NamedAddress address,
            OSAI_ReadPriority priority)
        {
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableNamedQueuePosition(ConnectionProvider, variable_name, address, priority);
            VariableStore.RegisterVariable(variable_expression, variable);
        }
        public void CreateVariable(
           Expression<Func<OSAI_VariableStore, Optional<IFLUX_Variable<QueuePosition, QueuePosition>>>> variable_expression,
           OSAI_NamedAddress address,
           OSAI_ReadPriority priority)
        {
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableNamedQueuePosition(ConnectionProvider, variable_name, address, priority);
            VariableStore.RegisterVariable(variable_expression, variable);
        }

        public void CreateVariable(
            Expression<Func<OSAI_VariableStore, IFLUX_Variable<bool, bool>>> variable_expression,
            OSAI_NamedAddress address,
            OSAI_ReadPriority priority)
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableNamedBool(ConnectionProvider, variable_name, address, priority);
            VariableStore.RegisterVariable(variable_expression, variable);
        }
        public void CreateVariable(
           Expression<Func<OSAI_VariableStore, Optional<IFLUX_Variable<bool, bool>>>> variable_expression,
           OSAI_NamedAddress address,
           OSAI_ReadPriority priority)
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableNamedBool(ConnectionProvider, variable_name, address, priority);
            VariableStore.RegisterVariable(variable_expression, variable);
        }

        public void CreateVariable(
            Expression<Func<OSAI_VariableStore, IFLUX_Variable<double, double>>> variable_expression,
            OSAI_NamedAddress address,
            OSAI_ReadPriority priority)
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableNamedDouble(ConnectionProvider, variable_name, address, priority);
            VariableStore.RegisterVariable(variable_expression, variable);
        }
        public void CreateVariable(
            Expression<Func<OSAI_VariableStore, Optional<IFLUX_Variable<double, double>>>> variable_expression,
            OSAI_NamedAddress address,
            OSAI_ReadPriority priority)
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableNamedDouble(ConnectionProvider, variable_name, address, priority);
            VariableStore.RegisterVariable(variable_expression, variable);
        }

        public void CreateArray(
            Expression<Func<OSAI_VariableStore, IFLUX_Array<double, double>>> array_expression,
            ushort count,
            OSAI_NamedAddress address,
            OSAI_ReadPriority priority,
            Optional<VariableUnits> variable_units = default)
        {
            
            var array_name = string.Join('/', array_expression.GetMembersName());
            var array = new OSAI_ArrayNamedDouble(ConnectionProvider, array_name, count, address, priority, variable_units);
            VariableStore.RegisterArray(array_expression, array);
        }
        public void CreateArray(
            Expression<Func<OSAI_VariableStore, Optional<IFLUX_Array<double, double>>>> array_expression,
            ushort count,
            OSAI_NamedAddress address,
            OSAI_ReadPriority priority,
            Optional<VariableUnits> variable_units = default)
        {
            
            var array_name = string.Join('/', array_expression.GetMembersName());
            var array = new OSAI_ArrayNamedDouble(ConnectionProvider, array_name, count, address, priority, variable_units);
            VariableStore.RegisterArray(array_expression, array);
        }

        // MW
        public void CreateVariable<TSensor>(
            Expression<Func<OSAI_VariableStore, IFLUX_Variable<Pressure, Unit>>> variable_expression,
            OSAI_IndexAddress address)
            where TSensor : AnalogSensor<Pressure>, new()
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariablePressure<TSensor>(ConnectionProvider, variable_name, address);
            VariableStore.RegisterVariable(variable_expression, variable);
        }
        public void CreateVariable<TSensor>(
            Expression<Func<OSAI_VariableStore, Optional<IFLUX_Variable<Pressure, Unit>>>> variable_expression,
            OSAI_IndexAddress address)
            where TSensor : AnalogSensor<Pressure>, new()
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariablePressure<TSensor>(ConnectionProvider, variable_name, address);
            VariableStore.RegisterVariable(variable_expression, variable);
        }

        public void CreateVariable(
            Expression<Func<OSAI_VariableStore, IFLUX_Variable<double, double>>> variable_expression,
            OSAI_IndexAddress address)
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableDouble(ConnectionProvider, variable_name, address);
            VariableStore.RegisterVariable(variable_expression, variable);
        }
        public void CreateVariable(
           Expression<Func<OSAI_VariableStore, Optional<IFLUX_Variable<double, double>>>> variable_expression,
           OSAI_IndexAddress address)
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableDouble(ConnectionProvider, variable_name, address);
            VariableStore.RegisterVariable(variable_expression, variable);
        }

        public void CreateVariable(
            Expression<Func<OSAI_VariableStore, IFLUX_Variable<ushort, ushort>>> variable_expression,
            OSAI_IndexAddress address)
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableUShort(ConnectionProvider, variable_name, address);
            VariableStore.RegisterVariable(variable_expression, variable);
        }
        public void CreateVariable(
           Expression<Func<OSAI_VariableStore, Optional<IFLUX_Variable<ushort, ushort>>>> variable_expression,
           OSAI_IndexAddress address)
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableUShort(ConnectionProvider, variable_name, address);
            VariableStore.RegisterVariable(variable_expression, variable);
        }

        public void CreateVariable(
            Expression<Func<OSAI_VariableStore, IFLUX_Variable<short, short>>> variable_expression,
            OSAI_IndexAddress address)
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableShort(ConnectionProvider, variable_name, address);
            VariableStore.RegisterVariable(variable_expression, variable);
        }
        public void CreateVariable(
           Expression<Func<OSAI_VariableStore, Optional<IFLUX_Variable<short, short>>>> variable_expression,
           OSAI_IndexAddress address)
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableShort(ConnectionProvider, variable_name, address);
            VariableStore.RegisterVariable(variable_expression, variable);
        }

        public void CreateVariable(
            Expression<Func<OSAI_VariableStore, IFLUX_Variable<ArrayIndex, ArrayIndex>>> variable_expression,
            OSAI_IndexAddress address)
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableArrayIndex(ConnectionProvider, variable_name, address);
            VariableStore.RegisterVariable(variable_expression, variable);
        }
        public void CreateVariable(
           Expression<Func<OSAI_VariableStore, Optional<IFLUX_Variable<ArrayIndex, ArrayIndex>>>> variable_expression,
           OSAI_IndexAddress address)
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableArrayIndex(ConnectionProvider, variable_name, address);
            VariableStore.RegisterVariable(variable_expression, variable);
        }

        public void CreateVariable(
            Expression<Func<OSAI_VariableStore, IFLUX_Variable<bool, bool>>> variable_expression,
            OSAI_BitIndexAddress address)
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableBool(ConnectionProvider, variable_name, address);
            VariableStore.RegisterVariable(variable_expression, variable);
        }
        public void CreateVariable(
           Expression<Func<OSAI_VariableStore, Optional<IFLUX_Variable<bool, bool>>>> variable_expression,
           OSAI_BitIndexAddress address)
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableBool(ConnectionProvider, variable_name, address);
            VariableStore.RegisterVariable(variable_expression, variable);
        }

        public void CreateArray(
            Expression<Func<OSAI_VariableStore, IFLUX_Array<double, double>>> array_expression,
            ushort count,
            OSAI_IndexAddress address,
            Optional<VariableUnits> variable_units = default)
        {
            
            var array_name = string.Join('/', array_expression.GetMembersName());
            var array = new OSAI_ArrayDouble(ConnectionProvider, array_name, count, address, variable_units);
            VariableStore.RegisterArray(array_expression, array);
        }
        public void CreateArray(
            Expression<Func<OSAI_VariableStore, Optional<IFLUX_Array<double, double>>>> array_expression,
            ushort count,
            OSAI_IndexAddress address,
            Optional<VariableUnits> variable_units = default)
        {
            
            var array_name = string.Join('/', array_expression.GetMembersName());
            var array = new OSAI_ArrayDouble(ConnectionProvider, array_name, count, address, variable_units);
            VariableStore.RegisterArray(array_expression, array);
        }

        public void CreateArray(
            Expression<Func<OSAI_VariableStore, IFLUX_Array<ushort, ushort>>> array_expression,
            ushort count,
            OSAI_IndexAddress address,
            Optional<VariableUnits> variable_units = default)
        {
            
            var array_name = string.Join('/', array_expression.GetMembersName());
            var array = new OSAI_ArrayWord(ConnectionProvider, array_name, count, address, variable_units);
            VariableStore.RegisterArray(array_expression, array);
        }
        public void CreateArray(
           Expression<Func<OSAI_VariableStore, Optional<IFLUX_Array<ushort, ushort>>>> array_expression,
            ushort count,
            OSAI_IndexAddress address,
            Optional<VariableUnits> variable_units = default)
        {
            
            var array_name = string.Join('/', array_expression.GetMembersName());
            var array = new OSAI_ArrayWord(ConnectionProvider, array_name, count, address, variable_units);
            VariableStore.RegisterArray(array_expression, array);
        }

        public void CreateArray(
            Expression<Func<OSAI_VariableStore, IFLUX_Array<bool, bool>>> array_expression,
            ushort count,
            OSAI_BitIndexAddress address,
            Optional<VariableUnits> variable_units = default)
        {
            
            var array_name = string.Join('/', array_expression.GetMembersName());
            var array = new OSAI_ArrayBool(ConnectionProvider, array_name, count, address, variable_units);
            VariableStore.RegisterArray(array_expression, array);
        }
        public void CreateArray(
           Expression<Func<OSAI_VariableStore, Optional<IFLUX_Array<bool, bool>>>> array_expression,
            ushort count,
            OSAI_BitIndexAddress address,
            Optional<VariableUnits> variable_units = default)
        {
            
            var array_name = string.Join('/', array_expression.GetMembersName());
            var array = new OSAI_ArrayBool(ConnectionProvider, array_name, count, address, variable_units);
            VariableStore.RegisterArray(array_expression, array);
        }

        // AA
        public void CreateArray(
            Expression<Func<OSAI_VariableStore, IFLUX_Array<string, string>>> array_expression,
            ushort count,
            OSAI_IndexAddress address,
            OSAI_ReadPriority priority,
            Optional<VariableUnits> variable_units = default)
        {
            
            var array_name = string.Join('/', array_expression.GetMembersName());
            var array = new OSAI_ArrayString(ConnectionProvider, array_name, count, address, priority, variable_units);
            VariableStore.RegisterArray(array_expression, array);
        }
        public void CreateArray(
           Expression<Func<OSAI_VariableStore, Optional<IFLUX_Array<string, string>>>> array_expression,
            ushort count,
            OSAI_IndexAddress address,
            OSAI_ReadPriority priority,
            Optional<VariableUnits> variable_units = default)
        {
            
            var array_name = string.Join('/', array_expression.GetMembersName());
            var array = new OSAI_ArrayString(ConnectionProvider, array_name, count, address, priority, variable_units);
            VariableStore.RegisterArray(array_expression, array);
        }

        public void CreateVariable(
            Expression<Func<OSAI_VariableStore, IFLUX_Variable<string, string>>> variable_expression,
            OSAI_IndexAddress address,
            OSAI_ReadPriority priority)
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableString(ConnectionProvider, variable_name, address, priority);
            VariableStore.RegisterVariable(variable_expression, variable);
        }
        public void CreateVariable(
            Expression<Func<OSAI_VariableStore, Optional<IFLUX_Variable<string, string>>>> variable_expression,
            OSAI_IndexAddress address,
            OSAI_ReadPriority priority)
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableString(ConnectionProvider, variable_name, address, priority);
            VariableStore.RegisterVariable(variable_expression, variable);
        }

        public void CreateArray(
            Expression<Func<OSAI_VariableStore, IFLUX_Array<FLUX_Temp, double>>> array_expression,
            ushort count,
            OSAI_IndexAddress address,
            OSAI_ReadPriority priority,
            Func<ushort, double, string> write_temp,
            Optional<VariableUnits> variable_units = default)
        {
            
            var array_name = string.Join('/', array_expression.GetMembersName());
            var array = new OSAI_ArrayTemp(ConnectionProvider, array_name, count, address, priority, write_temp, variable_units);
            VariableStore.RegisterArray(array_expression, array);
        }
        public void CreateArray(
           Expression<Func<OSAI_VariableStore, Optional<IFLUX_Array<FLUX_Temp, double>>>> array_expression,
            ushort count,
            OSAI_IndexAddress address,
            OSAI_ReadPriority priority,
            Func<ushort, double, string> write_temp,
            Optional<VariableUnits> variable_units = default)
        {
            
            var array_name = string.Join('/', array_expression.GetMembersName());
            var array = new OSAI_ArrayTemp(ConnectionProvider, array_name, count, address, priority, write_temp, variable_units);
            VariableStore.RegisterArray(array_expression, array);
        }

        public void CreateVariable(
            Expression<Func<OSAI_VariableStore, IFLUX_Variable<FLUX_Temp, double>>> variable_expression,
            OSAI_IndexAddress address,
            OSAI_ReadPriority priority,
            Func<double, string> write_temp)
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableTemp(ConnectionProvider, variable_name, address, priority, write_temp);
            VariableStore.RegisterVariable(variable_expression, variable);
        }
        public void CreateVariable(
           Expression<Func<OSAI_VariableStore, Optional<IFLUX_Variable<FLUX_Temp, double>>>> variable_expression,
           OSAI_IndexAddress address,
           OSAI_ReadPriority priority,
           Func<double, string> write_temp)
        {
            
            var variable_name = string.Join('/', variable_expression.GetMembersName());
            var variable = new OSAI_VariableTemp(ConnectionProvider, variable_name, address, priority, write_temp);
            VariableStore.RegisterVariable(variable_expression, variable);
        }
    }
}
