namespace Nethereum.Mud
{
    public class Resource
    {
        //bytes2 constant RESOURCE_TABLE = "tb";
        public static readonly byte[] RESOURCE_TABLE = { 0x74, 0x62 };
        //bytes2 constant RESOURCE_OFFCHAIN_TABLE = "ot";
        public static readonly byte[] RESOURCE_OFFCHAIN_TABLE = { 0x6f, 0x74 };
        // Resource that identifies a namespace, a container belonging to a
        // specific address (not necessarily the original deployer of the World).
        // A namespace can include tables and systems.
        //bytes2 constant RESOURCE_NAMESPACE = "ns";
        public static readonly byte[] RESOURCE_NAMESPACE = { 0x6e, 0x73 };
        // Resource that identifies a system, a contract used to manipulate
        // the state.
        //bytes2 constant RESOURCE_SYSTEM = "sy";
        public static readonly byte[] RESOURCE_SYSTEM = { 0x73, 0x79 };

        public byte[] ResourceId { get; set; }
        public string Namespace { get; set; } = String.Empty;
        public string Name { get; set; } 
        public bool IsNamespace => ResourceId[0] == RESOURCE_NAMESPACE[0] && ResourceId[1] == RESOURCE_NAMESPACE[1];
        public bool IsTable => ResourceId[0] == RESOURCE_TABLE[0] && ResourceId[1] == RESOURCE_TABLE[1];    
        public bool IsOffchainTable => ResourceId[0] == RESOURCE_OFFCHAIN_TABLE[0] && ResourceId[1] == RESOURCE_OFFCHAIN_TABLE[1];
        public bool IsSystem => ResourceId[0] == RESOURCE_SYSTEM[0] && ResourceId[1] == RESOURCE_SYSTEM[1];
        public bool IsRoot => string.IsNullOrEmpty(Namespace);

        public void SetAsNamespace()
        {
            ResourceId = RESOURCE_NAMESPACE;
        }

        public void SetAsTable()
        {
            ResourceId = RESOURCE_TABLE;
        }
        public void SetAsOffchainTable()
        {
            ResourceId = RESOURCE_OFFCHAIN_TABLE;
        }
        public void SetAsSystem()
        {
            ResourceId = RESOURCE_SYSTEM;
        }
        public void SetAsRoot()
        {
            Namespace = string.Empty;
        }   
    }
}
