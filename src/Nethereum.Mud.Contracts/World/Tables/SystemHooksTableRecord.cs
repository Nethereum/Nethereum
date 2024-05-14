using Nethereum.ABI.FunctionEncoding.Attributes;

using static Nethereum.Mud.Contracts.World.Tables.SystemHooksTableRecord;
using Nethereum.Mud.EncodingDecoding;
using System.Collections.Generic;

namespace Nethereum.Mud.Contracts.World.Tables
{
    public class SystemHooksTableRecord : TableRecord<SystemHooksKey, SystemHooksValue>
    {
        public SystemHooksTableRecord() : base("world", "SystemHooks")
        {
        }

        public class SystemHooksKey
        {
            [Parameter("bytes32", "systemId", 1)]
            public byte[] SystemId { get; set; }

            public Resource GetSystemIdResource()
            {
                return ResourceEncoder.Decode(SystemId);
            }
        }


        public class SystemHooksValue
        {
            [Parameter("bytes21[]", "value", 1)]
            public List<byte[]> Value { get; set; }
        }
    }

}

