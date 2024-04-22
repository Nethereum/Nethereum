using Nethereum.ABI.FunctionEncoding.Attributes;
using static Nethereum.Mud.Contracts.Tables.World.SystemRegistryTableRecord;

namespace Nethereum.Mud.Contracts.Tables.World
{
    public class SystemRegistryTableRecord : TableRecord<SystemRegistryKey, SystemRegistryValue>
    {
        public SystemRegistryTableRecord() : base("world", "SystemRegistry")
        {
        }

        public class SystemRegistryKey
        {
            [Parameter("address", "system", 1)]
            public string System { get; set; }
        }

        public class SystemRegistryValue
        {
            [Parameter("bytes32", "systemId", 1)]
            public byte[] SystemId { get; set; }
        }
    }

}

