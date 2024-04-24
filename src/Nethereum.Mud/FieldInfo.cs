using Nethereum.ABI;


namespace Nethereum.Mud
{
    public class FieldInfo
    {
        public string Name { get; set; }
        public string Type { get; private set; }
        public ABIType ABIType { get; private set; }
        public int Order { get; private set; }
        public bool IsKey { get; set; }

        public FieldInfo(string type, bool isKey = false, string name = null, int order = 1)
        {
            Name = name;
            Type = type;
            Order = order;
            IsKey = isKey;
            ABIType =  ABIType.CreateABIType(type);
        }

        public FieldInfo(ABIType abiType, bool isKey = false, string name = null, int order = 1)
        {
            Name = name;
            Type = abiType.Name;
            Order = order;
            IsKey = isKey;
            ABIType = abiType;
        }

    }
}
