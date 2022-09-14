using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;
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
    public interface IOSAI_ObservableVariable
    {
        IOSAI_Address Address { get; }
    }
    public interface IOSAI_AsyncVariable : IFLUX_AsyncVariable
    {
        OSAI_ReadPriority Priority { get; }
    }

    public class OSAI_AsyncVariable<TRData, TWData> : FLUX_AsyncVariable<OSAI_ConnectionProvider, OSAI_Connection, TRData, TWData>, IOSAI_AsyncVariable
    {
        public OSAI_ReadPriority Priority { get; }
        public OSAI_AsyncVariable(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_ReadPriority priority,
            Func<OSAI_Connection, Task<Optional<TRData>>> read_func = null,
            Func<OSAI_Connection, TWData, Task<bool>> write_func = null, VariableUnit unit = default)
            : base(connection_provider, name, read_func, write_func, unit)
        {
            Priority = priority;
        }
    }
    public class OSAI_ObservableVariable<TRData, TWData> : FLUX_ObservableVariable<OSAI_ConnectionProvider, OSAI_Connection, TRData, TWData>, IOSAI_ObservableVariable
    {
        public IOSAI_Address Address { get; }
        public override string Group { get; }
        public OSAI_ObservableVariable(
            OSAI_ConnectionProvider connection_provider,
            string name,
            IOSAI_Address address,
            Func<OSAI_MemoryBuffer, IObservable<Optional<TRData>>> observe_func,
            Func<OSAI_Connection, Task<Optional<TRData>>> read_func = null,
            Func<OSAI_Connection, TWData, Task<bool>> write_func = null, VariableUnit unit = default)
            : base(connection_provider, name, c => observe_value(c, observe_func), read_func, write_func, unit)
        {
            Address = address;
            Group = $"{address.VarCode}";
        }
        static IObservable<Optional<TRData>> observe_value(OSAI_Connection connection, Func<OSAI_MemoryBuffer, IObservable<Optional<TRData>>> get_data)
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
                read_func: c => c.ReadBoolAsync(address),
                write_func: (c, v) => c.WriteVariableAsync(address, v),
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
                read_func: c => c.ReadUshortAsync(address),
                write_func: (c, v) => c.WriteVariableAsync(address, v),
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
                read_func: c => c.ReadShortAsync(address),
                write_func: (c, v) => c.WriteVariableAsync(address, v), 
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
                read_func: c => c.ReadDoubleAsync(address),
                write_func: (c, v) => c.WriteVariableAsync(address, v), 
                unit)
        {
        }
    }

    public class OSAI_VariableText : OSAI_AsyncVariable<string, string>
    {
        public override string Group { get; }
        public OSAI_VariableText(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            OSAI_ReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, priority, 
                  read_func: c => c.ReadTextAsync(address),
                  write_func: (c, v) => c.WriteVariableAsync(address, v),
                  unit)
        {
            Group = $"{address.VarCode}";
        }
    }

    public class OSAI_VariableTemp : OSAI_AsyncVariable<FLUX_Temp, double>
    {
        public override string Group { get; }
        public OSAI_VariableTemp(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            OSAI_ReadPriority priority,
            Func<double, string> get_temp_gcode,
            VariableUnit unit = default)
            : base(connection_provider, name, priority,
                  read_func: c => read_async(c, address),
                  write_func: (c,v) => write_async(c, v, get_temp_gcode),
                  unit)
        {
            Group = $"{address.VarCode}";
        }

        static async Task<Optional<FLUX_Temp>> read_async(OSAI_Connection connection, OSAI_IndexAddress address)
        {
            var data = await connection.ReadTextAsync(address);
            if (!data.HasValue)
                return default;

            var datas = data.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            
            var target = datas.Length > 0 ? double.Parse($"{datas[0]:0.00}", NumberStyles.Float, CultureInfo.InvariantCulture) : 0;
            var current = datas.Length > 1 ? double.Parse($"{datas[1]:0.00}", NumberStyles.Float, CultureInfo.InvariantCulture) : 0;
            return new FLUX_Temp(current, target);
        }
        static async Task<bool> write_async(OSAI_Connection connection, double temp, Func<double, string> get_temp_gcode)
        {
            using var put_write_temp_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await connection.ExecuteParamacroAsync(new[] { get_temp_gcode(temp) }, put_write_temp_cts.Token, false);
        }
    }

    public abstract class OSAI_VariableNamed<TRData, TWData> : OSAI_AsyncVariable<TRData, TWData>
    {
        public override string Group { get; }
        public OSAI_VariableNamed(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_NamedAddress address,
            OSAI_ReadPriority priority,
            Func<OSAI_Connection, Task<Optional<TRData>>> read_func = null,
            Func<OSAI_Connection, TWData, Task<bool>> write_func = null,
            VariableUnit unit = default)
            : base(connection_provider, name, priority, read_func, write_func, unit)
        {
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
                  read_func: c => c.ReadNamedDoubleAsync(address),
                  write_func: (c, v) => c.WriteVariableAsync(address, v),
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
                read_func: c => c.ReadNamedShortAsync(address),
                write_func: (c, v) => c.WriteVariableAsync(address, v),
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
                read_func: c => c.ReadNamedUShortAsync(address),
                write_func: (c, v) => c.WriteVariableAsync(address, v),
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
                read_func: c => c.ReadNamedShortAsync(address)
                    .ConvertAsync(q => (QueuePosition)q),
                write_func: (c, v) => c.WriteVariableAsync(address, v),
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
                read_func: c => c.ReadNamedStringAsync(address, lenght),
                write_func: (c, v) => c.WriteVariableAsync(address, v, lenght),
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
                read_func: c => c.ReadNamedBoolAsync(address),
                write_func: (c, v) => c.WriteVariableAsync(address, v),
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
                read_func: c => c.ReadUshortAsync(address)
                    .ConvertAsync(get_pressure),
                unit: unit)
        {
        }
        static TSensor SensorInstance { get; } = new TSensor();
        static Pressure get_pressure(ushort value) => SensorInstance.ValueFunc(value / 200.0);     
    }
}
