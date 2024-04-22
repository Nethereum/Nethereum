using Nethereum.ABI.FunctionEncoding.Attributes;
using static Nethereum.Mud.Contracts.Tables.World.SystemsTableRecord;

namespace Nethereum.Mud.Contracts.Tables.World
{
    public class SystemsTableRecord : TableRecord<SystemsKey, SystemsValue>
    {
        public SystemsTableRecord() : base("world", "Systems")
        {
        }

        public class SystemsKey
        {
            [Parameter("bytes32", "systemId", 1)]
            public byte[] SystemId { get; set; }
        }

        public class SystemsValue
        {
            [Parameter("address", "system", 1)]
            public string System { get; set; }
            [Parameter("bool", "publicAccess", 2)]
            public bool PublicAccess { get; set; }
        }
    }

}

