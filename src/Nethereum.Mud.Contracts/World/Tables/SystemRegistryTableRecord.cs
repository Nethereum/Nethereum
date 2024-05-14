using Nethereum.ABI.FunctionEncoding.Attributes;

using Nethereum.Mud.EncodingDecoding;
using static Nethereum.Mud.Contracts.World.Tables.SystemRegistryTableRecord;


namespace Nethereum.Mud.Contracts.World.Tables
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

            public Resource GetSystemIdResource()
            {
                return ResourceEncoder.Decode(SystemId);
            }
        }
    }

}

