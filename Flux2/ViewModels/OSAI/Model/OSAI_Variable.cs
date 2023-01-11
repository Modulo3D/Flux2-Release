using DynamicData.Kernel;
using Modulo3DNet;
using System;
using System.Globalization;
using System.Reactive;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public enum OSAI_ReadPriority
    {
        LOW,
        HIGH,
        MEDIUM,
        ULTRALOW,
        ULTRAHIGH,
    }

    public interface IOSAI_Variable : IFLUX_Variable
    {
        Optional<string> Alias { get; }
    }
    public interface IOSAI_Variable<TRData, TWData> : IOSAI_Variable, IFLUX_Variable<TRData, TWData>
    {
    }
    public interface IOSAI_AddressVariable : IOSAI_Variable
    {
        IOSAI_Address Address { get; }
    }
    public interface IOSAI_AddressVariable<TRData, TWData> : IOSAI_AddressVariable, IOSAI_Variable<TRData, TWData>
    {
    }
    public interface IOSAI_ObservableVariable : IOSAI_AddressVariable
    {
    }
    public interface IOSAI_ObservableVariable<TRData, TWData> : IOSAI_ObservableVariable, IOSAI_AddressVariable<TRData, TWData>
    {
    }
    public interface IOSAI_AsyncVariable : IOSAI_Variable, IFLUX_AsyncVariable
    {
        OSAI_ReadPriority Priority { get; }
    }
    public interface IOSAI_AsyncVariable<TRData, TWData> : IOSAI_AsyncVariable, IFLUX_AsyncVariable<TRData, TWData>
    {
    }


    public class OSAI_AsyncVariable<TRData, TWData> : FLUX_AsyncVariable<OSAI_ConnectionProvider, TRData, TWData>, IOSAI_AsyncVariable<TRData, TWData>
    {
        public Optional<string> Alias { get; }
        public OSAI_ReadPriority Priority { get; }

        public OSAI_AsyncVariable(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_ReadPriority priority,
            Func<OSAI_ConnectionProvider, Task<ValueResult<TRData>>> read_func = null,
            Func<OSAI_ConnectionProvider, TWData, Task<bool>> write_func = null, VariableUnit unit = default)
            : base(connection_provider, name, read_func, write_func, unit)
        {
            Priority = priority;
        }
    }
    public class OSAI_ObservableVariable<TRData, TWData> : FLUX_ObservableVariable<OSAI_ConnectionProvider, TRData, TWData>, IOSAI_ObservableVariable<TRData, TWData>
    {
        public Optional<string> Alias { get; }
        public IOSAI_Address Address { get; }
        public override string Group { get; }
        public OSAI_ObservableVariable(
            OSAI_ConnectionProvider connection_provider,
            string name,
            IOSAI_Address address,
            Func<OSAI_MemoryBuffer, IObservable<Optional<TRData>>> observe_func,
            Func<OSAI_ConnectionProvider, Task<ValueResult<TRData>>> read_func = null,
            Func<OSAI_ConnectionProvider, TWData, Task<bool>> write_func = null, VariableUnit unit = default)
            : base(connection_provider, name, c => observe_value(c, observe_func), read_func, write_func, unit)
        {
            Address = address;
            Group = $"{address.VarCode}";
        }

        private static IObservable<Optional<TRData>> observe_value(OSAI_ConnectionProvider connection, Func<OSAI_MemoryBuffer, IObservable<Optional<TRData>>> get_data)
        {
            return get_data(connection.MemoryBuffer);
        }
    }

    public class OSAI_VariableBool : OSAI_ObservableVariable<bool, bool>
    {
        public OSAI_VariableBool(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_BitIndexAddress address,
            VariableUnit unit = default)
            : base(connection_provider, name, address,
                observe_func: m => m.ObserveWordVar(address)
                    .Convert(b => b.IsBitSet(address.BitIndex)),
                read_func: c => c.Connection.ReadBoolAsync(address),
                write_func: (c, v) => c.Connection.WriteVariableAsync(address, v),
                unit)
        {
        }
    }

    public class OSAI_VariableUShort : OSAI_ObservableVariable<ushort, ushort>
    {
        public OSAI_VariableUShort(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            VariableUnit unit = default)
            : base(connection_provider, name, address,
                observe_func: m => m.ObserveWordVar(address),
                read_func: c => c.Connection.ReadUShortAsync(address),
                write_func: (c, v) => c.Connection.WriteVariableAsync(address, v),
                unit)
        {
        }
    }

    public class OSAI_VariableShort : OSAI_ObservableVariable<short, short>
    {
        public OSAI_VariableShort(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            VariableUnit unit = default)
            : base(connection_provider, name, address,
                observe_func: m => m.ObserveWordVar(address)
                    .Convert(s => ShortConverter.Convert(s)),
                read_func: c => c.Connection.ReadShortAsync(address),
                write_func: (c, v) => c.Connection.WriteVariableAsync(address, v),
                unit)
        {
        }
    }

    public class OSAI_VariableArrayIndex : OSAI_ObservableVariable<ArrayIndex, ArrayIndex>
    {
        public OSAI_VariableArrayIndex(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            VariableUnit unit = default)
            : base(connection_provider, name, address,
                observe_func: m => m.ObserveWordVar(address)
                    .Convert(s => ShortConverter.Convert(s))
                    .Convert(s => ArrayIndex.FromArrayBase(s, connection_provider.VariableStoreBase)),
                read_func: c => c.Connection
                    .ReadShortAsync(address, a => ArrayIndex.FromArrayBase(a, connection_provider.VariableStoreBase)),
                write_func: (c, v) => c.Connection.WriteVariableAsync(address, v.GetArrayBaseIndex()),
                unit)
        {
        }
    }

    public class OSAI_VariableDouble : OSAI_ObservableVariable<double, double>
    {
        public OSAI_VariableDouble(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            VariableUnit unit = default)
            : base(connection_provider, name, address,
                observe_func: m => m.ObserveDWordVar(address),
                read_func: c => c.Connection
                    .ReadDoubleAsync(address),
                write_func: (c, v) => c.Connection
                    .WriteVariableAsync(address, v),
                unit)
        {
        }
    }

    public class OSAI_VariableText : OSAI_AsyncVariable<string, string>, IOSAI_AddressVariable
    {
        public override string Group { get; }
        public OSAI_IndexAddress Address { get; }
        IOSAI_Address IOSAI_AddressVariable.Address => Address;

        public OSAI_VariableText(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            OSAI_ReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, priority,
                  read_func: c => c.Connection.ReadTextAsync(address),
                  write_func: (c, v) => c.Connection.WriteVariableAsync(address, v),
                  unit)
        {
            Address = address;
            Group = $"{address.VarCode}";
        }
    }

    public class OSAI_VariableString : OSAI_AsyncVariable<string, string>, IOSAI_AddressVariable
    {
        public override string Group { get; }
        public OSAI_IndexAddress Address { get; }
        IOSAI_Address IOSAI_AddressVariable.Address => Address;

        public OSAI_VariableString(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            OSAI_ReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, priority,
                  read_func: c => c.Connection.ReadTextAsync(address),
                  write_func: (c, v) => c.Connection.WriteVariableAsync(address, v),
                  unit)
        {
            Address = address;
            Group = $"{address.VarCode}";
        }
    }

    public class OSAI_VariableTemp : OSAI_AsyncVariable<FLUX_Temp, double>, IOSAI_AddressVariable
    {
        public override string Group { get; }
        public OSAI_IndexAddress Address { get; }
        IOSAI_Address IOSAI_AddressVariable.Address => Address;

        public OSAI_VariableTemp(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            OSAI_ReadPriority priority,
            Func<double, string> get_temp_gcode,
            VariableUnit unit = default)
            : base(connection_provider, name, priority,
                  read_func: c => read_async(c, address),
                  write_func: (c, v) => write_async(c, v, get_temp_gcode),
                  unit)
        {
            Address = address;
            Group = $"{address.VarCode}";
        }

        private static async Task<ValueResult<FLUX_Temp>> read_async(OSAI_ConnectionProvider connection_provider, OSAI_IndexAddress address)
        {
            var connection = connection_provider.Connection;
            var data = await connection.ReadTextAsync(address);
            if (!data.HasValue)
                return default;

            var datas = data.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            var target = datas.Length > 0 ? double.Parse($"{datas[0]:0.00}", NumberStyles.Float, CultureInfo.InvariantCulture) : 0;
            var current = datas.Length > 1 ? double.Parse($"{datas[1]:0.00}", NumberStyles.Float, CultureInfo.InvariantCulture) : 0;
            return new FLUX_Temp(current, target);
        }

        private static async Task<bool> write_async(OSAI_ConnectionProvider connection_provider, double temp, Func<double, string> get_temp_gcode)
        {
            using var put_write_temp_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await connection_provider.ExecuteParamacroAsync(_ => new[] { get_temp_gcode(temp) }, put_write_temp_cts.Token, false);
        }
    }

    public abstract class OSAI_VariableNamed<TRData, TWData> : OSAI_AsyncVariable<TRData, TWData>, IOSAI_AddressVariable
    {
        public override string Group { get; }
        public OSAI_NamedAddress Address { get; }
        IOSAI_Address IOSAI_AddressVariable.Address => Address;

        public OSAI_VariableNamed(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_NamedAddress address,
            OSAI_ReadPriority priority,
            Func<OSAI_ConnectionProvider, Task<ValueResult<TRData>>> read_func = null,
            Func<OSAI_ConnectionProvider, TWData, Task<bool>> write_func = null,
            VariableUnit unit = default)
            : base(connection_provider, name, priority, read_func, write_func, unit)
        {
            Address = address;
            Group = $"{address.VarCode}";
        }
    }

    public class OSAI_VariableNamedDouble : OSAI_VariableNamed<double, double>
    {
        public OSAI_VariableNamedDouble(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_NamedAddress address,
            OSAI_ReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority,
                  read_func: c => c.Connection.ReadNamedDoubleAsync(address),
                  write_func: (c, v) => c.Connection.WriteVariableAsync(address, v),
                  unit)
        {
        }
    }

    public class OSAI_VariableNamedShort : OSAI_VariableNamed<short, short>
    {
        public OSAI_VariableNamedShort(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_NamedAddress address,
            OSAI_ReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority,
                read_func: c => c.Connection.ReadNamedShortAsync(address),
                write_func: (c, v) => c.Connection.WriteVariableAsync(address, v),
                unit)
        {
        }
    }
    public class OSAI_VariableNamedUShort : OSAI_VariableNamed<ushort, ushort>
    {
        public OSAI_VariableNamedUShort(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_NamedAddress address,
            OSAI_ReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority,
                read_func: c => c.Connection.ReadNamedUShortAsync(address),
                write_func: (c, v) => c.Connection.WriteVariableAsync(address, v),
                unit)
        {
        }
    }
    public class OSAI_VariableNamedQueuePosition : OSAI_VariableNamed<QueuePosition, QueuePosition>
    {
        public OSAI_VariableNamedQueuePosition(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_NamedAddress address,
            OSAI_ReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority,
                read_func: c => c.Connection
                    .ReadNamedShortAsync(address, s => (QueuePosition)s),
                write_func: (c, v) => c.Connection
                    .WriteVariableAsync(address, v),
                unit)
        {
        }
    }

    public class OSAI_VariableNamedString : OSAI_VariableNamed<string, string>
    {
        public OSAI_VariableNamedString(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_NamedAddress address,
            OSAI_ReadPriority priority,
            ushort lenght,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority,
                read_func: c => c.Connection.ReadNamedStringAsync(address, lenght),
                write_func: (c, v) => c.Connection.WriteVariableAsync(address, v, lenght),
                unit)
        {
        }
    }

    public class OSAI_VariableNamedBool : OSAI_VariableNamed<bool, bool>
    {
        public OSAI_VariableNamedBool(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_NamedAddress address,
            OSAI_ReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority,
                read_func: c => c.Connection.ReadNamedBoolAsync(address),
                write_func: (c, v) => c.Connection.WriteVariableAsync(address, v),
                unit)
        {
        }
    }

    public class OSAI_VariablePressure<TSensor> : OSAI_ObservableVariable<Pressure, Unit>
        where TSensor : AnalogSensor<Pressure>, new()
    {
        public OSAI_VariablePressure(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            VariableUnit unit = default)
            : base(connection_provider, name, address,
                observe_func: c => c.ObserveWordVar(address)
                    .Convert(get_pressure),
                read_func: c => c.Connection
                    .ReadUShortAsync(address, get_pressure),
                unit: unit)
        {
        }

        private static TSensor SensorInstance { get; } = new TSensor();

        private static Pressure get_pressure(ushort value)
        {
            return SensorInstance.ValueFunc(value / 200.0);
        }
    }
}
