namespace Flux.ViewModels
{
    public interface IOSAI_Address
    {
        ushort Index { get; }
        OSAI_VARCODE VarCode { get; }
        IOSAI_Address Increment();
    }

    public interface IOSAI_Address<out TAddress>
        : IOSAI_Address where TAddress : IOSAI_Address<TAddress>
    {
        new TAddress Increment();
    }

    public abstract class OSAI_Address<TAddress> : IOSAI_Address<TAddress>
        where TAddress : IOSAI_Address<TAddress>
    {
        public ushort Index { get; }
        public string Group { get; }
        public OSAI_VARCODE VarCode { get; }

        public OSAI_Address(OSAI_VARCODE varCode, ushort index)
        {
            Index = index;
            VarCode = varCode;
            Group = $"{VarCode}";
        }

        public abstract TAddress Increment();
        IOSAI_Address IOSAI_Address.Increment() => Increment();
        public override string ToString() => $"{VarCode}";
    }

    public class OSAI_IndexAddress : OSAI_Address<OSAI_IndexAddress>
    {
        public OSAI_IndexAddress(OSAI_VARCODE varCode, ushort index) : base(varCode, index)
        {
        }
        public override OSAI_IndexAddress Increment() => new OSAI_IndexAddress(VarCode, (ushort)(Index + 1));
        public override string ToString() => $"{VarCode} {Index}";
    }

    public class OSAI_BitIndexAddress : OSAI_Address<OSAI_BitIndexAddress>
    {
        public ushort BitIndex { get; }
        public OSAI_BitIndexAddress(OSAI_VARCODE varCode, ushort index, ushort bitIndex) : base(varCode, index)
        {
            BitIndex = bitIndex;
        }
        public override OSAI_BitIndexAddress Increment() => new OSAI_BitIndexAddress(VarCode, Index, (ushort)(BitIndex + 1));
        public override string ToString() => $"{VarCode} {Index}_{BitIndex}";
    }

    public class OSAI_NamedAddress : OSAI_Address<OSAI_NamedAddress>
    {
        public string Name { get; }
        public OSAI_NamedAddress(string name, ushort index) : base(OSAI_VARCODE.NAMED, index)
        {
            Name = name;
        }

        public OSAI_NamedAddress(string name) : this(name, 0)
        {
        }

        public override string ToString() => Name;
        public override OSAI_NamedAddress Increment() => new OSAI_NamedAddress(Name, (ushort)(Index + 1));

        public static implicit operator OSAI_NamedAddress(string name) => new OSAI_NamedAddress(name);
    }
}
