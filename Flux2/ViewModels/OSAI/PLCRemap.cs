namespace Flux.ViewModels
{
    public enum PLCRemapType
    {
        INPUT,
        OUTPUT
    }

    public enum PLCRemapVarType
    {
        BOOL = 0,
        BYTE = 1,
        WORD = 2,
        DWORD = 3
    }

    public enum PLCRemapMode
    {
        IGNORE = 0,
        COPY = 1,
        TRUE = 2,
        FALSE = 3,
        NEGATE = 4,
        VALUE = 5,
    }

    /*public class PLCRemap : ReactiveObject
    {
        private IPLCIndexAddress _LogicalAddress;
        public IPLCIndexAddress LogicalAddress
        {
            get => _LogicalAddress;
            set => this.RaiseAndSetIfChanged(ref _LogicalAddress, value);
        }

        private IPLCIndexAddress _PhysicalAddress;
        public IPLCIndexAddress PhysicalAddress
        {
            get => _PhysicalAddress;
            set => this.RaiseAndSetIfChanged(ref _PhysicalAddress, value);
        }

        private PLCRemapVarType _VarType;
        public PLCRemapVarType VarType
        {
            get => _VarType;
            set => this.RaiseAndSetIfChanged(ref _VarType, value);
        }

        private PLCRemapType _Type;
        public PLCRemapType Type
        {
            get => _Type;
            set => this.RaiseAndSetIfChanged(ref _Type, value);
        }

        private PLCRemapMode _Mode;
        public PLCRemapMode Mode
        {
            get => _Mode;
            set => this.RaiseAndSetIfChanged(ref _Mode, value);
        }

        private uint _RiseTime;
        public uint RiseTime
        {
            get => _RiseTime;
            set => this.RaiseAndSetIfChanged(ref _RiseTime, value);
        }

        private uint _FallTime;
        public uint FallTime
        {
            get => _FallTime;
            set => this.RaiseAndSetIfChanged(ref _FallTime, value);
        }

        private uint _Value;
        public uint Value
        {
            get => _Value;
            set => this.RaiseAndSetIfChanged(ref _Value, value);
        }

        private uint _IOMsgId;
        public uint IOMsgId
        {
            get => _IOMsgId;
            set => this.RaiseAndSetIfChanged(ref _IOMsgId, value);
        }

        private byte _IOClass;
        public byte IOClass
        {
            get => _IOClass;
            set => this.RaiseAndSetIfChanged(ref _IOClass, value);
        }

        public PLCRemap(
            IPLCIndexAddress logicalAddress,
            IPLCIndexAddress physicalAddress,
            PLCRemapVarType varType,
            PLCRemapType type,
            PLCRemapMode mode,
            uint fallTime,
            uint riseTime,
            uint value,
            uint ioMsgId,
            byte iOClass)
        {
            LogicalAddress = logicalAddress;
            PhysicalAddress = physicalAddress;
            VarType = varType;
            Type = type;
            Mode = mode;
            FallTime = fallTime;
            RiseTime = riseTime;
            Value = value;
            IOMsgId = ioMsgId;
            IOClass = iOClass;
        }

        public PLCRemap(REMAPDEF remap, PLCRemapType type)
        {
            var logical_var_code = (PLCRemapVarType)remap.VarType switch
            {
                PLCRemapVarType.DWORD => VARCODE.MD_CODE,
                _ => VARCODE.MW_CODE
            };

            var address_divider = (PLCRemapVarType)remap.VarType switch
            {
                PLCRemapVarType.BYTE => 1,
                PLCRemapVarType.DWORD => 4,
                _ => 2
            };

            var physical_var_code = type switch
            {
                PLCRemapType.INPUT => VARCODE.IW_CODE,
                PLCRemapType.OUTPUT => VARCODE.OW_CODE,
                _ => VARCODE.IW_CODE
            };

            var logical_index = (ushort)(remap.LogicAddr / address_divider);
            var logical_bit = (ushort)(((remap.LogicAddr % address_divider) * 8) + remap.LogicBit);

            IPLCIndexAddress logical_address = (PLCRemapVarType)remap.VarType switch
            {
                PLCRemapVarType.BYTE => new PLCShortAddress(logical_var_code, logical_index),
                PLCRemapVarType.DWORD => new PLCIndexAddress(logical_var_code, logical_index),
                PLCRemapVarType.WORD => new PLCBitAddress(logical_var_code, logical_index),
                PLCRemapVarType.BOOL => new PLCBitIndexAddress(logical_var_code, logical_index, logical_bit),
                _ => throw new Exception()
            };

            var physical_index = (ushort)(remap.PhysAddr / address_divider);
            var physical_bit = (ushort)(((remap.PhysAddr % address_divider) * 8) + remap.PhysBit);

            IPLCIndexAddress physical_address = (PLCRemapVarType)remap.VarType switch
            {
                PLCRemapVarType.BYTE => new PLCShortAddress(physical_var_code, physical_index),
                PLCRemapVarType.DWORD => new PLCIndexAddress(physical_var_code, physical_index),
                PLCRemapVarType.WORD => new PLCBitAddress(physical_var_code, physical_index),
                PLCRemapVarType.BOOL => new PLCBitIndexAddress(physical_var_code, physical_index, physical_bit),
                _ => throw new Exception()
            };

            LogicalAddress = logical_address;
            PhysicalAddress = physical_address;
            VarType = (PLCRemapVarType)remap.VarType;
            Mode = (PLCRemapMode)remap.Mode;
            RiseTime = remap.RiseTime;
            FallTime = remap.FallTime;
            IOMsgId = remap.IOMsgId;
            IOClass = remap.IOClass;
            Value = remap.VarType;
            Type = type;
        }

        public REMAPDEF GetRemapDef()
        {
            var address_divider = VarType switch
            {
                PLCRemapVarType.BYTE => 1,
                PLCRemapVarType.DWORD => 4,
                _ => 2
            };

            var logical_index = (ushort)(LogicalAddress.Index * address_divider);
            var logical_bit = (ushort)((LogicalAddress.BitIndex - (logical_index % address_divider) * 8));

            var physical_index = (ushort)(PhysicalAddress.Index * address_divider);
            var physical_bit = (ushort)((PhysicalAddress.BitIndex - (physical_index % address_divider) * 8));

            return new REMAPDEF()
            {
                FallTime = FallTime,
                RiseTime = RiseTime,
                LogicAddr = logical_index,
                LogicBit = (byte)logical_bit,
                PhysAddr = physical_index,
                PhysBit = (byte)physical_bit,
                Mode = (byte)Mode,
                VarType = (byte)VarType,
                IOClass = IOClass,
                IOMsgId = IOMsgId,
                Value = Value,
            };
        }
    }*/

}
