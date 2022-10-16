using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public abstract class OSAI_Array<TVariable, TAddress, TRData, TWData> : FLUX_Array<TRData, TWData>
        where TVariable : IFLUX_Variable<TRData, TWData>
        where TAddress : IOSAI_Address<TAddress>
    {
        public override string Group { get; }

        public OSAI_Array(
            string name,
            ushort count,
            TAddress address,
            Func<string, TAddress, VariableUnit, ushort, TVariable> create_var,
            Optional<VariableUnits> custom_unit = default)
            : base(name)
        {
            Group = $"{address.VarCode}";
            TAddress cur_address = address;

            var source_cache = (SourceCache<IFLUX_Variable<TRData, TWData>, VariableAlias>)Variables;
            for (ushort i = 0; i < count; i++)
            {
                var v_unit = custom_unit
                    .Convert(u => u.ElementAtOrDefault(i))
                    .Convert(u => u.Value)
                    .ValueOr(() => new VariableUnit((ushort)(i + 1)));

                var i_name = $"{name} {v_unit}";
                var i_var = create_var(i_name, cur_address, v_unit, i);

                cur_address = cur_address.Increment();

                source_cache.AddOrUpdate(i_var);
            }
        }
    }

    public class OSAI_ArrayBool : OSAI_Array<OSAI_VariableBool, OSAI_BitIndexAddress, bool, bool>
    {
        public OSAI_ArrayBool(
            OSAI_ConnectionProvider connection_provider,
            string name,
            ushort count,
            OSAI_BitIndexAddress address,
            Optional<VariableUnits> custom_unit = default)
            : base(name, count, address, (name, addr, unit, i) => new OSAI_VariableBool(connection_provider, name, addr, unit), custom_unit)
        {
        }
    }

    public class OSAI_ArrayWord : OSAI_Array<OSAI_VariableUShort, OSAI_IndexAddress, ushort, ushort>
    {
        public OSAI_ArrayWord(
            OSAI_ConnectionProvider connection_provider,
            string name,
            ushort count,
            OSAI_IndexAddress address,
            Optional<VariableUnits> custom_unit = default)
            : base(name, count, address, (name, addr, unit, i) => new OSAI_VariableUShort(connection_provider, name, addr, unit), custom_unit)
        {
        }
    }

    public class OSAI_ArrayDouble : OSAI_Array<OSAI_VariableDouble, OSAI_IndexAddress, double, double>
    {
        public OSAI_ArrayDouble(
            OSAI_ConnectionProvider connection_provider,
            string name,
            ushort count,
            OSAI_IndexAddress address,
            Optional<VariableUnits> custom_unit = default)
            : base(name, count, address, (name, addr, unit, i) => new OSAI_VariableDouble(connection_provider, name, addr, unit), custom_unit)
        {
        }
    }

    public class OSAI_ArrayText : OSAI_Array<OSAI_VariableText, OSAI_IndexAddress, string, string>
    {
        public OSAI_ReadPriority Priority { get; }
        public OSAI_ArrayText(
            OSAI_ConnectionProvider connection_provider,
            string name,
            ushort count,
            OSAI_IndexAddress address,
            OSAI_ReadPriority priority,
            Optional<VariableUnits> custom_unit = default)
            : base(name, count, address, (name, addr, unit, i) => new OSAI_VariableText(connection_provider, name, addr, priority, unit), custom_unit)
        {
            Priority = priority;
        }
    }

    public class OSAI_ArrayTemp : OSAI_Array<OSAI_VariableTemp, OSAI_IndexAddress, FLUX_Temp, double>
    {
        public OSAI_ReadPriority Priority { get; }
        public OSAI_ArrayTemp(
            OSAI_ConnectionProvider connection_provider,
            string name,
            ushort count,
            OSAI_IndexAddress address,
            OSAI_ReadPriority priority,
            Func<ushort, double, string> write_temp,
            Optional<VariableUnits> custom_unit = default)
            : base(name, count, address, (name, addr, unit, i) => new OSAI_VariableTemp(connection_provider, name, addr, priority, t => write_temp(i, t), unit), custom_unit)
        {
            Priority = priority;
        }
    }

    public class OSAI_ArrayNamed<TVariable, TRData, TWData> : OSAI_Array<TVariable, OSAI_NamedAddress, TRData, TWData>
        where TVariable : OSAI_VariableNamed<TRData, TWData>
    {
        public OSAI_ReadPriority Priority { get; }
        public OSAI_ArrayNamed(
            string name,
            ushort count,
            OSAI_NamedAddress address,
            OSAI_ReadPriority priority,
            Func<string, OSAI_NamedAddress, OSAI_ReadPriority, VariableUnit, TVariable> create_var,
            Optional<VariableUnits> custom_unit = default)
            : base(name, count, address, (name, addr, unit, i) => create_var(name, addr, priority, unit), custom_unit)
        {
            Priority = priority;
        }
    }

    public class OSAI_ArrayNamedDouble : OSAI_ArrayNamed<OSAI_VariableNamedDouble, double, double>
    {
        public OSAI_ArrayNamedDouble(
            OSAI_ConnectionProvider connection_provider,
            string name,
            ushort count,
            OSAI_NamedAddress address,
            OSAI_ReadPriority priority,
            Optional<VariableUnits> custom_unit = default)
            : base(name, count, address, priority, (name, addr, p, unit) => new OSAI_VariableNamedDouble(connection_provider, name, addr, p, unit), custom_unit)
        {
        }
    }
}
