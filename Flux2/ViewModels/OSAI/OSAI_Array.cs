using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using OSAI;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public interface IOSAI_Array : IOSAI_VariableBase, IFLUX_Array
    {
        new IObservableCache<IOSAI_Variable, VariableUnit> Variables { get; }
    }

    public interface IOSAI_Array<TRData, TWData> : IOSAI_Array, IFLUX_Array<TRData, TWData>
    {
        new IObservableCache<IOSAI_Variable<TRData, TWData>, VariableUnit> Variables { get; }
    }

    public abstract class OSAI_Array<TVariable, TAddress, TRData, TWData> : FLUX_Array<TRData, TWData>, IOSAI_Array<TRData, TWData>, IEnumerable<TVariable>
        where TVariable : IOSAI_Variable<TRData, TWData, TAddress>
        where TAddress : IOSAI_Address<TAddress>
    {
        public bool IsArray => true;
        public bool FromArray => false;
        public IObservable<Optional<OSAI_Connection>> Connection { get; }
        public override string Group => $"{LogicalAddress.VarCode}";

        public TAddress LogicalAddress { get; }
        IOSAI_Address IOSAI_VariableBase.LogicalAddress => LogicalAddress;

        public new ISourceCache<TVariable, VariableUnit> Variables { get; }

        private IObservableCache<IOSAI_Variable<TRData, TWData>, VariableUnit> _InnerVariables1;
        IObservableCache<IOSAI_Variable<TRData, TWData>, VariableUnit> IOSAI_Array<TRData, TWData>.Variables
        {
            get
            {
                if (_InnerVariables1 == default)
                {
                    _InnerVariables1 = Variables.Connect()
                        .Transform(v => (IOSAI_Variable<TRData, TWData>)v)
                        .AsObservableCache();
                }
                return _InnerVariables1;
            }
        }
        private IObservableCache<IOSAI_Variable, VariableUnit> _InnerVariables2;
        IObservableCache<IOSAI_Variable, VariableUnit> IOSAI_Array.Variables
        {
            get
            {
                if (_InnerVariables2 == default)
                {
                    _InnerVariables2 = Variables.Connect()
                        .Transform(v => (IOSAI_Variable)v)
                        .AsObservableCache();
                }
                return _InnerVariables2;
            }
        }

        public OSAI_Array(
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            ushort count,
            TAddress s_logical_address,
            FluxMemReadPriority priority,
            Func<string, TAddress, FluxMemReadPriority, VariableUnit, ushort, Optional<TAddress>, TVariable> create_var,
            Optional<TAddress> s_physical_address = default,
            Optional<IEnumerable<VariableUnit>> custom_unit = default)
            :base(name, count, priority, custom_unit)
        {

            Connection = connection;
            LogicalAddress = s_logical_address;

            Variables = new SourceCache<TVariable, VariableUnit>(v => v.Unit.ValueOr(() => ""));
            base.Variables = Variables.Connect()
                .Transform(v => (IFLUX_Variable<TRData, TWData>)v)
                .AsObservableCache();

            TAddress i_logical_address = s_logical_address;
            Optional<TAddress> i_physical_address = s_physical_address;

            for (ushort i = 0; i < count; i++)
            {
                var unit = GetArrayUnit(i);

                var i_name = $"{name} {unit}";
                var i_var = create_var(i_name, i_logical_address, priority, unit, i, i_physical_address);

                i_logical_address = i_logical_address.Increment();
                i_physical_address = i_physical_address.Convert(a => a.Increment());

                Variables.AddOrUpdate(i_var);
            }
        }

        public TVariable this[int position] => Variables.Items.ElementAt(position);

        public IEnumerator<TVariable> GetEnumerator() => Variables.Items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override VariableUnit GetArrayUnit(ushort position)
        {
            if (CustomUnit.HasValue)
            {
                var unit = CustomUnit.Value.ElementAtOrDefault(position);
                if (unit != default)
                    return unit;
            }
            return $"{position + 1}";
        }
    }

    public class OSAI_ArrayBool : OSAI_Array<OSAI_VariableBool, OSAI_BitIndexAddress, bool, bool>
    {
        public OSAI_ArrayBool(
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            ushort count,
            OSAI_BitIndexAddress s_logical_address,
            FluxMemReadPriority priority,
            Optional<OSAI_BitIndexAddress> s_physical_address = default,
            Optional<IEnumerable<VariableUnit>> custom_unit = default)
            : base(connection, name, count, s_logical_address, priority, (name, l_addr, p, unit, i, p_addr) => new OSAI_VariableBool(connection, name, l_addr, p, p_addr, unit), s_physical_address, custom_unit)
        {
        }
    }

    public class OSAI_ArrayWord : OSAI_Array<OSAI_VariableWord, OSAI_IndexAddress, ushort, ushort>
    {
        public OSAI_ArrayWord(
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            ushort count,
            OSAI_IndexAddress s_logical_address,
            FluxMemReadPriority priority,
            Optional<OSAI_IndexAddress> s_physical_address = default,
            Optional<IEnumerable<VariableUnit>> custom_unit = default)
            : base(connection, name, count, s_logical_address, priority, (name, l_addr, p, unit, i, p_addr) => new OSAI_VariableWord(connection, name, l_addr, p, p_addr, unit), s_physical_address, custom_unit)
        {
        }
    }

    public class OSAI_ArrayDouble : OSAI_Array<OSAI_VariableDouble, OSAI_IndexAddress, double, double>
    {
        public OSAI_ArrayDouble(
            IObservable<Optional<OSAI_Connection>> connection, 
            string name,
            ushort count,
            OSAI_IndexAddress s_logical_address,
            FluxMemReadPriority priority,
            Optional<OSAI_IndexAddress> s_physical_address = default,
            Optional<IEnumerable<VariableUnit>> custom_unit = default)
            : base(connection, name, count, s_logical_address, priority, (name, l_addr, p, unit, i, p_addr) => new OSAI_VariableDouble(connection, name, l_addr, p, p_addr, unit), s_physical_address, custom_unit)
        {
        }
    }

    public class OSAI_ArrayText : OSAI_Array<OSAI_VariableText, OSAI_IndexAddress, string, string>
    {
        public OSAI_ArrayText(
            IObservable<Optional<OSAI_Connection>> connection, 
            string name,
            ushort count,
            OSAI_IndexAddress s_logical_address,
            FluxMemReadPriority priority,
            Optional<IEnumerable<VariableUnit>> custom_unit = default)
            : base(connection, name, count, s_logical_address, priority, (name, l_addr, p, unit, i, p_addr) => new OSAI_VariableText(connection, name, l_addr, p, unit), default, custom_unit)
        {
        }
    }

    public class OSAI_ArrayTemp : OSAI_Array<OSAI_VariableTemp, OSAI_IndexAddress, FLUX_Temp, double>
    {
        public OSAI_ArrayTemp(
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            ushort count,
            OSAI_IndexAddress s_logical_address,
            FluxMemReadPriority priority,
            Func<ushort, double, string> write_temp,
            Optional<IEnumerable<VariableUnit>> custom_unit = default)
            : base(connection, name, count, s_logical_address, priority, (name, l_addr, p, unit, i, p_addr) => new OSAI_VariableTemp(connection, name, l_addr, p, t => write_temp(i, t), unit), default, custom_unit)
        {
        }
    }

    public class OSAI_ArrayNamed<TVariable, TRData, TWData> : OSAI_Array<TVariable, OSAI_NamedAddress, TRData, TWData>
        where TVariable : OSAI_VariableNamed<TRData, TWData>
    {
        public OSAI_ArrayNamed(
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            ushort count,
            OSAI_NamedAddress s_logical_address,
            FluxMemReadPriority priority,
            Func<string, OSAI_NamedAddress, FluxMemReadPriority, VariableUnit, TVariable> create_var,
            Optional<IEnumerable<VariableUnit>> custom_unit = default)
            : base(connection, name, count, s_logical_address, priority, (name, l_addr, p, unit, i, p_addr) => create_var(name, l_addr, p, unit), default, custom_unit)
        {
        }
    }

    public class OSAI_ArrayNamedDouble : OSAI_ArrayNamed<OSAI_VariableNamedDouble, double, double>
    {
        public OSAI_ArrayNamedDouble(
            IObservable<Optional<OSAI_Connection>> connection,
            string name,
            ushort count,
            OSAI_NamedAddress s_logical_address,
            FluxMemReadPriority priority,
            Optional<IEnumerable<VariableUnit>> custom_unit = default)
            : base(connection, name, count, s_logical_address, priority, (name, l_addr, p, unit) => new OSAI_VariableNamedDouble(connection, name, l_addr, p, unit), custom_unit)
        {
        }
    }
}
