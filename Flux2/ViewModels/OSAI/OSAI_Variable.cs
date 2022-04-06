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

    public interface IOSAI_Variable : IOSAI_VariableBase, IFLUX_Variable
    {
        Optional<IOSAI_Address> PhysicalAddress { get; }
    }

    public interface IOSAI_Variable<TRData, TWData> : IOSAI_Variable, IFLUX_Variable<TRData, TWData>
    {
    }

    public interface IOSAI_Variable<TRData, TWData, TAddress> : IOSAI_Variable<TRData, TWData>
        where TAddress : IOSAI_Address
    {
        new TAddress LogicalAddress { get; }
    }

    public abstract class OSAI_Variable<TRData, TWData, TAddress> : FLUX_Variable<TRData, TWData>, IOSAI_Variable<TRData, TWData, TAddress>
        where TAddress : IOSAI_Address<TAddress>, IOSAI_Address
    {
        private ObservableAsPropertyHelper<Optional<OSAI_Connection>> _Connection;
        public Optional<OSAI_Connection> Connection => _Connection.Value;

        public TAddress LogicalAddress { get; }
        public Optional<TAddress> PhysicalAddress { get; }
        public override string Group => $"{LogicalAddress.VarCode}";
        IOSAI_Address IOSAI_VariableBase.LogicalAddress => LogicalAddress;
        Optional<IOSAI_Address> IOSAI_Variable.PhysicalAddress => PhysicalAddress.Cast<TAddress, IOSAI_Address>();

        public OSAI_Variable(
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            TAddress logicalAddress,
            FluxMemReadPriority priority,
            Optional<TAddress> physicalAddress = default,
            Optional<VariableUnit> unit = default)
            : base(name, priority, unit)
        {
            _Connection = connection
                .ToProperty(this, v => v.Connection);

            LogicalAddress = logicalAddress;
            PhysicalAddress = physicalAddress;
        }
    }

    public class OSAI_VariableBool : OSAI_Variable<bool, bool, OSAI_BitIndexAddress>
    {
        public override bool ReadOnly => false;
        public OSAI_VariableBool(
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            OSAI_BitIndexAddress logicalAddress,
            FluxMemReadPriority priority,
            Optional<OSAI_BitIndexAddress> physicalAddress = default,
            Optional<VariableUnit> unit = default)
            : base(connection, name, logicalAddress, priority, physicalAddress, unit)
        {
            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(m => m.ObserveWordVar(this))
                .Convert(b => b.IsBitSet(logicalAddress.BitIndex))
                .BindTo(this, v => v.Value);
        }
        public override Task<bool> WriteAsync(bool value) => Connection.ConvertOrAsync(c => c.WriteVariableAsync(LogicalAddress, value), () => false);
        public override Task<Optional<bool>> ReadAsync() => Connection.ConvertAsync(c => c.ReadBoolAsync(LogicalAddress));
    }

    public class OSAI_VariableWord : OSAI_Variable<ushort, ushort, OSAI_IndexAddress>
    {
        public override bool ReadOnly => false;
        public OSAI_VariableWord(
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            OSAI_IndexAddress logicalAddress,
            FluxMemReadPriority priority,
            Optional<OSAI_IndexAddress> physicalAddress = default,
            Optional<VariableUnit> unit = default)
            : base(connection, name, logicalAddress, priority, physicalAddress, unit)
        {
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
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            OSAI_IndexAddress logicalAddress,
            FluxMemReadPriority priority,
            Optional<VariableUnit> unit = default,
            Optional<OSAI_IndexAddress> physicalAddress = default)
            : base(connection, name, logicalAddress, priority, physicalAddress, unit)
        {
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
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            OSAI_IndexAddress logicalAddress,
            FluxMemReadPriority priority,
            Optional<OSAI_IndexAddress> physicalAddress = default,
            Optional<VariableUnit> unit = default)
            : base(connection, name, logicalAddress, priority, physicalAddress, unit)
        {
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
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            OSAI_IndexAddress logicalAddress,
            FluxMemReadPriority priority,
            Optional<VariableUnit> unit = default)
            : base(connection, name, logicalAddress, priority, unit: unit) { }

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
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            OSAI_IndexAddress logicalAddress,
            FluxMemReadPriority priority,
            Func<double, string> write_temp,
            Optional<VariableUnit> unit = default)
            : base(connection, name, logicalAddress, priority, unit: unit)
        {
            WriteTemp = write_temp;
        }

        public override async Task<Optional<FLUX_Temp>> ReadAsync()
        {
            var data = await Connection.ConvertAsync(c => c.ReadTextAsync(LogicalAddress));
            if (!data.HasValue)
                return default;

            var datas = data.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var target = double.Parse($"{datas[0]:0.00}", NumberStyles.Float, CultureInfo.InvariantCulture);
            var current = double.Parse($"{datas[1]:0.00}", NumberStyles.Float, CultureInfo.InvariantCulture);
            return new FLUX_Temp(current, target);
        }
        public override async Task<bool> WriteAsync(double temp)
        {
            return await Connection.ConvertOrAsync(c =>
            {
                using var put_write_temp_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                return c.ExecuteParamacroAsync(new[] { WriteTemp(temp) }, put_write_temp_cts.Token);
            }, () => false);
        }
    }

    public abstract class OSAI_VariableNamed<TRData, TWData> : OSAI_Variable<TRData, TWData, OSAI_NamedAddress>
    {
        public OSAI_VariableNamed(
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            OSAI_NamedAddress address,
            FluxMemReadPriority priority,
            Optional<VariableUnit> unit = default)
            : base(connection, name, address, priority, unit: unit)
        {
        }
    }

    public class OSAI_VariableNamedDouble : OSAI_VariableNamed<double, double>
    {
        public override bool ReadOnly => false;
        public OSAI_VariableNamedDouble(
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            OSAI_NamedAddress address,
            FluxMemReadPriority priority,
            Optional<VariableUnit> unit = default)
            : base(connection, name, address, priority, unit) { }

        public override Task<Optional<double>> ReadAsync() => Connection.ConvertAsync(c => c.ReadNamedDoubleAsync(LogicalAddress));
        public override Task<bool> WriteAsync(double value) => Connection.ConvertOrAsync(c => c.WriteVariableAsync(LogicalAddress, value), () => false);
    }

    public class OSAI_VariableNamedShort : OSAI_VariableNamed<short, short>
    {
        public override bool ReadOnly => false;
        public OSAI_VariableNamedShort(
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            OSAI_NamedAddress address,
            FluxMemReadPriority priority,
            Optional<VariableUnit> unit = default)
            : base(connection, name, address, priority, unit)
        {
        }

        public override Task<Optional<short>> ReadAsync() => Connection.ConvertAsync(c => c.ReadNamedShortAsync(LogicalAddress));
        public override Task<bool> WriteAsync(short value) => Connection.ConvertOrAsync(c => c.WriteVariableAsync(LogicalAddress, value), () => false);
    }

    public class OSAI_VariableNamedString : OSAI_VariableNamed<string, string>
    {
        public override bool ReadOnly => false;
        public ushort Lenght { get; }

        public OSAI_VariableNamedString(
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            OSAI_NamedAddress address,
            FluxMemReadPriority priority,
            ushort lenght,
            Optional<VariableUnit> unit = default)
            : base(connection, name, address, priority, unit)
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
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            OSAI_NamedAddress address,
            FluxMemReadPriority priority,
            Optional<VariableUnit> unit = default)
            : base(connection, name, address, priority, unit) { }

        public override Task<Optional<bool>> ReadAsync() => Connection.ConvertAsync(c => c.ReadNamedBoolAsync(LogicalAddress));
        public override Task<bool> WriteAsync(bool value) => Connection.ConvertOrAsync(c => c.WriteVariableAsync(LogicalAddress, value), () => false);
    }

    public class OSAI_VariablePressure<TSensor> : OSAI_Variable<Pressure, Unit, OSAI_IndexAddress>
        where TSensor : AnalogSensor<Pressure>, new()
    {
        public override bool ReadOnly => true;
        public TSensor Sensor { get; }
        public OSAI_VariablePressure(
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            OSAI_IndexAddress logicalAddress,
            FluxMemReadPriority priority,
            Optional<OSAI_IndexAddress> physicalAddress = default,
            Optional<VariableUnit> unit = default)
            : base(connection, name, logicalAddress, priority, physicalAddress, unit)
        {
            Sensor = new TSensor();
            connection.Convert(c => c.MemoryBuffer)
                .ConvertMany(m => m.ObserveWordVar(this))
                .Convert(w => Sensor.ValueFunc(w))
                .BindTo(this, v => v.Value);
        }
        public override async Task<Optional<Pressure>> ReadAsync()
        {
            var value = await Connection.ConvertAsync(c => c.ReadUshortAsync(LogicalAddress));
            return Sensor.ValueFunc(value.ValueOr(() => (ushort)0));
        }
        public override Task<bool> WriteAsync(Unit data)
        {
            throw new NotImplementedException();
        }
    }

    public class OSAI_VariableMacro : OSAI_Variable<OSAI_Macro, Unit, OSAI_IndexAddress>
    {
        public override bool ReadOnly => true;
        public OSAI_VariableMacro(
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            OSAI_IndexAddress address,
            Optional<VariableUnit> unit = default)
            : base(connection, name, address, FluxMemReadPriority.DISABLED, unit: unit)
        {
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
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            OSAI_IndexAddress address,
            Optional<VariableUnit> unit = default)
            : base(connection, name, address, FluxMemReadPriority.DISABLED, unit: unit)
        {
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
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            OSAI_IndexAddress address,
            Optional<VariableUnit> unit = default)
            : base(connection, name, address, FluxMemReadPriority.DISABLED, unit: unit)
        {
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
    }
}
