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
    public interface IOSAI_VariableBase : IFLUX_VariableBase
    {
        IOSAI_Address LogicalAddress { get; }
    }

    public abstract class OSAI_Variable<TRData, TWData, TAddress> : FLUX_Variable<TRData, TWData>, IOSAI_VariableBase
        where TAddress : IOSAI_Address<TAddress>, IOSAI_Address
    {
        private ObservableAsPropertyHelper<Optional<OSAI_Connection>> _Connection;
        public Optional<OSAI_Connection> Connection => _Connection.Value;

        public TAddress LogicalAddress { get; }
        public Optional<TAddress> PhysicalAddress { get; }
        public override string Group => $"{LogicalAddress.VarCode}";
        IOSAI_Address IOSAI_VariableBase.LogicalAddress => LogicalAddress;

        public OSAI_Variable(
            OSAI_ConnectionProvider connection_provider,
            string name,
            TAddress address,
            FluxMemReadPriority priority,
            VariableUnit unit = default)
            : base(name, priority, unit)
        {
            _Connection = connection_provider
                .WhenAnyValue(p => p.Connection)
                .ToProperty(this, v => v.Connection);

            LogicalAddress = address;
        }
    }

    public class OSAI_VariableGP<TRData, TWData> : FLUX_VariableGP<OSAI_ConnectionProvider, OSAI_Connection, TRData, TWData>
    {
        public OSAI_VariableGP(
            OSAI_ConnectionProvider connection_provider,
            string name,
            FluxMemReadPriority priority,
            Func<OSAI_Connection, Task<Optional<TRData>>> read_func = null,
            Func<OSAI_Connection, TWData, Task<bool>> write_func = null, VariableUnit unit = default)
            : base(connection_provider, name, priority, read_func, write_func, unit)
        {
        }
    }

    public class OSAI_VariableBool : OSAI_Variable<bool, bool, OSAI_BitIndexAddress>
    {
        public override bool ReadOnly => false;
        public OSAI_VariableBool(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_BitIndexAddress address,
            FluxMemReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority, unit)
        {
            var connection = connection_provider
                .WhenAnyValue(c => c.Connection);
           
            connection
                .Convert(c => c.MemoryBuffer)
                .ConvertMany(m => m.ObserveWordVar(this))
                .Convert(b => b.IsBitSet(address.BitIndex))
                .BindTo(this, v => v.Value);
        }
        public override Task<bool> WriteAsync(bool value) => Connection.ConvertOrAsync(c => c.WriteVariableAsync(LogicalAddress, value), () => false);
        public override Task<Optional<bool>> ReadAsync() => Connection.ConvertAsync(c => c.ReadBoolAsync(LogicalAddress));
    }

    public class OSAI_VariableWord : OSAI_Variable<ushort, ushort, OSAI_IndexAddress>
    {
        public override bool ReadOnly => false;
        public OSAI_VariableWord(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            FluxMemReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority, unit)
        {
            var connection = connection_provider
                .WhenAnyValue(c => c.Connection);

            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(m => m.ObserveWordVar(this))
                .BindTo(this, v => v.Value);
        }
        public override Task<bool> WriteAsync(ushort value) => Connection.ConvertOrAsync(c => c.WriteVariableAsync(LogicalAddress, value), () => false);
        public override Task<Optional<ushort>> ReadAsync() => Connection.ConvertAsync(c => c.ReadUshortAsync(LogicalAddress));
    }

    public class OSAI_VariableShort : OSAI_Variable<short, short, OSAI_IndexAddress>
    {
        public override bool ReadOnly => false;
        public OSAI_VariableShort(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            FluxMemReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority, unit)
        {
            var connection = connection_provider
                .WhenAnyValue(c => c.Connection);

            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(m => m.ObserveWordVar(this))
                .Convert(s => ShortConverter.Convert(s))
                .BindTo(this, v => v.Value);
        }
        public override Task<bool> WriteAsync(short value) => Connection.ConvertOrAsync(c => c.WriteVariableAsync(LogicalAddress, value), () => false);
        public override Task<Optional<short>> ReadAsync() => Connection.ConvertAsync(c => c.ReadShortAsync(LogicalAddress));
    }

    public class OSAI_VariableDouble : OSAI_Variable<double, double, OSAI_IndexAddress>
    {
        public override bool ReadOnly => false;
        public OSAI_VariableDouble(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            FluxMemReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority, unit)
        {
            var connection = connection_provider
                .WhenAnyValue(c => c.Connection);

            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(m => m.ObserveDWordVar(this))
                .BindTo(this, v => v.Value);
        }
        public override Task<bool> WriteAsync(double value) => Connection.ConvertOrAsync(c => c.WriteVariableAsync(LogicalAddress, value), () => false);
        public override Task<Optional<double>> ReadAsync() => Connection.ConvertAsync(c => c.ReadDoubleAsync(LogicalAddress));
    }

    public class OSAI_VariableText : OSAI_Variable<string, string, OSAI_IndexAddress>
    {
        public override bool ReadOnly => false;
        public OSAI_VariableText(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            FluxMemReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority, unit: unit) { }

        public override async Task<bool> UpdateAsync()
        {
            var read_result = await Connection.ConvertAsync(c => c.ReadTextAsync(LogicalAddress));
            if (read_result.HasValue == false)
                return false;
            Value = read_result.Value;
            return true;
        }
        public override Task<bool> WriteAsync(string value) => Connection.ConvertOrAsync(c => c.WriteVariableAsync(LogicalAddress, value), () => false);
        public override Task<Optional<string>> ReadAsync() => Connection.ConvertAsync(c => c.ReadTextAsync(LogicalAddress));
    }

    public class OSAI_VariableTemp : OSAI_Variable<FLUX_Temp, double, OSAI_IndexAddress>
    {
        public Func<double, string> WriteTemp { get; }
        public override bool ReadOnly => true;

        public OSAI_VariableTemp(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            FluxMemReadPriority priority,
            Func<double, string> write_temp,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority, unit: unit)
        {
            WriteTemp = write_temp;
        }

        public override async Task<Optional<FLUX_Temp>> ReadAsync()
        {
            var data = await Connection.ConvertAsync(c => c.ReadTextAsync(LogicalAddress));
            if (!data.HasValue)
                return default;

            var datas = data.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            
            var target = datas.Length > 0 ? double.Parse($"{datas[0]:0.00}", NumberStyles.Float, CultureInfo.InvariantCulture) : 0;
            var current = datas.Length > 1 ? double.Parse($"{datas[1]:0.00}", NumberStyles.Float, CultureInfo.InvariantCulture) : 0;
            return new FLUX_Temp(current, target);
        }
        public override async Task<bool> WriteAsync(double temp)
        {
            return await Connection.ConvertOrAsync(c =>
            {
                using var put_write_temp_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                return c.ExecuteParamacroAsync(new[] { WriteTemp(temp) }, put_write_temp_cts.Token, false);
            }, () => false);
        }
    }

    public abstract class OSAI_VariableNamed<TRData, TWData> : OSAI_Variable<TRData, TWData, OSAI_NamedAddress>
    {
        public OSAI_VariableNamed(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_NamedAddress address,
            FluxMemReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority, unit: unit)
        {
        }
    }

    public class OSAI_VariableNamedDouble : OSAI_VariableNamed<double, double>
    {
        public override bool ReadOnly => false;
        public OSAI_VariableNamedDouble(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_NamedAddress address,
            FluxMemReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority, unit) { }

        public override Task<Optional<double>> ReadAsync() => Connection.ConvertAsync(c => c.ReadNamedDoubleAsync(LogicalAddress));
        public override Task<bool> WriteAsync(double value) => Connection.ConvertOrAsync(c => c.WriteVariableAsync(LogicalAddress, value), () => false);
    }

    public class OSAI_VariableNamedShort : OSAI_VariableNamed<short, short>
    {
        public override bool ReadOnly => false;
        public OSAI_VariableNamedShort(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_NamedAddress address,
            FluxMemReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority, unit)
        {
        }

        public override Task<Optional<short>> ReadAsync() => Connection.ConvertAsync(c => c.ReadNamedShortAsync(LogicalAddress));
        public override Task<bool> WriteAsync(short value) => Connection.ConvertOrAsync(c => c.WriteVariableAsync(LogicalAddress, value), () => false);
    }
    public class OSAI_VariableNamedUShort : OSAI_VariableNamed<ushort, ushort>
    {
        public override bool ReadOnly => false;
        public OSAI_VariableNamedUShort(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_NamedAddress address,
            FluxMemReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority, unit)
        {
        }

        public override Task<Optional<ushort>> ReadAsync() => Connection.ConvertAsync(c => c.ReadNamedUShortAsync(LogicalAddress));
        public override Task<bool> WriteAsync(ushort value) => Connection.ConvertOrAsync(c => c.WriteVariableAsync(LogicalAddress, value), () => false);
    }
    public class OSAI_VariableNamedQueuePosition : OSAI_VariableNamed<QueuePosition, QueuePosition>
    {
        public override bool ReadOnly => false;
        public OSAI_VariableNamedQueuePosition(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_NamedAddress address,
            FluxMemReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority, unit)
        {
        }

        public override Task<bool> WriteAsync(QueuePosition value) => Connection.ConvertOrAsync(c => c.WriteVariableAsync(LogicalAddress, value), () => false);
        public override Task<Optional<QueuePosition>> ReadAsync() => Connection.ConvertAsync(c => c.ReadNamedShortAsync(LogicalAddress)).ConvertAsync(q => (QueuePosition)q);
    }

    public class OSAI_VariableNamedString : OSAI_VariableNamed<string, string>
    {
        public override bool ReadOnly => false;
        public ushort Lenght { get; }

        public OSAI_VariableNamedString(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_NamedAddress address,
            FluxMemReadPriority priority,
            ushort lenght,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority, unit)
        {
            Lenght = lenght;
        }

        public override Task<Optional<string>> ReadAsync() => Connection.ConvertAsync(c => c.ReadNamedStringAsync(LogicalAddress, Lenght));
        public override Task<bool> WriteAsync(string value) => Connection.ConvertOrAsync(c => c.WriteVariableAsync(LogicalAddress, value, Lenght), () => false);
    }

    public class OSAI_VariableNamedBool : OSAI_VariableNamed<bool, bool>
    {
        public override bool ReadOnly => false;
        public OSAI_VariableNamedBool(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_NamedAddress address,
            FluxMemReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority, unit) { }

        public override Task<Optional<bool>> ReadAsync() => Connection.ConvertAsync(c => c.ReadNamedBoolAsync(LogicalAddress));
        public override Task<bool> WriteAsync(bool value) => Connection.ConvertOrAsync(c => c.WriteVariableAsync(LogicalAddress, value), () => false);
    }

    public class OSAI_VariablePressure<TSensor> : OSAI_Variable<Pressure, Unit, OSAI_IndexAddress>
        where TSensor : AnalogSensor<Pressure>, new()
    {
        public override bool ReadOnly => true;
        public TSensor Sensor { get; }
        public OSAI_VariablePressure(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            FluxMemReadPriority priority,
            VariableUnit unit = default)
            : base(connection_provider, name, address, priority, unit)
        {
            Sensor = new TSensor();

            var connection = connection_provider
                .WhenAnyValue(c => c.Connection);

            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(m => m.ObserveWordVar(this))
                .Convert(w => Sensor.ValueFunc(w / 200.0))
                .BindTo(this, v => v.Value);
        }
        public override async Task<Optional<Pressure>> ReadAsync()
        {
            var value = await Connection.ConvertAsync(c => c.ReadUshortAsync(LogicalAddress));
            return Sensor.ValueFunc(value.ConvertOr(v => v / 200.0, () => (ushort)0));
        }
        public override Task<bool> WriteAsync(Unit data)
        {
            throw new NotImplementedException();
        }
    }

    /*public class OSAI_VariableMacro : OSAI_Variable<OSAI_Macro, Unit, OSAI_IndexAddress>
    {
        public override bool ReadOnly => true;
        public OSAI_VariableMacro(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            VariableUnit unit = default)
            : base(connection_provider, name, address, FluxMemReadPriority.DISABLED, unit: unit)
        {
            var connection = connection_provider
                .WhenAnyValue(c => c.Connection);

            Observable.CombineLatest(
                connection.ConvertMany(c => c.ObserveVariable(m => m.PROCESS_STATUS)),
                connection.ConvertMany(c => c.ObserveVariable(m => m.PART_PROGRAM)),
                connection.Convert(c => c.MemoryBuffer).ConvertMany(m => m.ObserveWordVar(this)),
                (status, program, word) =>
                {
                    if (!word.HasValue || !status.HasValue || status.Value == FLUX_ProcessStatus.IDLE)
                        return default;

                    var gmacro_nr = IntConverter.Convert(word.Value);
                    if (gmacro_nr == 0)
                    {
                        if (!program.HasValue)
                            return OSAI_Macro.GCODE_OR_MCODE;
                        return OSAI_Macro.PROGRAM;
                    }

                    return ((OSAI_Macro)gmacro_nr).ToOptional();
                })
               .BindTo(this, v => v.Value);
        }
        public override Task<Optional<OSAI_Macro>> ReadAsync()
        {
            return Task.FromResult(Value);
        }
        public override Task<bool> WriteAsync(Unit data)
        {
            throw new NotImplementedException();
        }
    }

    public class OSAI_VariableMCode : OSAI_Variable<OSAI_MCode, Unit, OSAI_IndexAddress>
    {
        public override bool ReadOnly => true;
        public OSAI_VariableMCode(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            VariableUnit unit = default)
            : base(connection_provider, name, address, FluxMemReadPriority.DISABLED, unit: unit)
        {
            var connection = connection_provider
                .WhenAnyValue(c => c.Connection);

            Observable.CombineLatest(
                connection.ConvertMany(c => c.ObserveVariable(m => m.PROCESS_STATUS)),
                connection.Convert(c => c.MemoryBuffer).ConvertMany(m => m.ObserveWordVar(this)),
                (status, word) =>
                {
                    if (!word.HasValue || !status.HasValue || status.Value == FLUX_ProcessStatus.IDLE)
                        return default;
                    return ((OSAI_MCode)IntConverter.Convert(word.Value)).ToOptional();
                })
               .BindTo(this, v => v.Value);
        }
        public override Task<Optional<OSAI_MCode>> ReadAsync()
        {
            return Task.FromResult(Value);
        }
        public override Task<bool> WriteAsync(Unit data)
        {
            throw new NotImplementedException();
        }
    }

    public class OSAI_VariableGCode : OSAI_Variable<OSAI_GCode, Unit, OSAI_IndexAddress>
    {
        public override bool ReadOnly => true;
        public OSAI_VariableGCode(
            OSAI_ConnectionProvider connection_provider,
            string name,
            OSAI_IndexAddress address,
            VariableUnit unit = default)
            : base(connection_provider, name, address, FluxMemReadPriority.DISABLED, unit: unit)
        {
            var connection = connection_provider
                .WhenAnyValue(c => c.Connection);

            Observable.CombineLatest(
                connection.ConvertMany(c => c.ObserveVariable(m => m.RUNNING_MCODE)),
                connection.ConvertMany(c => c.ObserveVariable(m => m.PROCESS_STATUS)),
                connection.Convert(c => c.MemoryBuffer).ConvertMany(m => m.ObserveWordVar(this)),
                (mcode, status, word) =>
                {
                    if (!word.HasValue || !status.HasValue || status.Value == FLUX_ProcessStatus.IDLE ||
                        !mcode.HasValue || mcode.HasValue && mcode.Value != OSAI_MCode.GCODE)
                        return default;

                    return ((OSAI_GCode)word.Value.GetBitSet()).ToOptional();
                })
               .BindTo(this, v => v.Value);
        }
        public override Task<Optional<OSAI_GCode>> ReadAsync()
        {
            return Task.FromResult(Value);
        }
        public override Task<bool> WriteAsync(Unit data)
        {
            throw new NotImplementedException();
        }
    }*/
}
