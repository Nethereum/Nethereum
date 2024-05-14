using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Mud.EncodingDecoding;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Nethereum.Mud.Contracts.Store.Tables.StoreHooksTableRecord;

namespace Nethereum.Mud.Contracts.Store.Tables
{
    public class StoreHooksTableRecord : TableRecord<StoreHooksKey, StoreHooksValue>
    {
        public StoreHooksTableRecord() : base("store", "StoreHooks")
        {
        }

        public class StoreHooksKey
        {
            [Parameter("bytes32", "tableId", 1)]
            public byte[] TableId { get; set; }

            public Resource GetTableIdResource()
            {
                return ResourceEncoder.Decode(TableId);
            }
        }

        public class StoreHooksValue
        {
            [Parameter("bytes21[]", "hooks", 1)]
            public List<byte[]> Hooks { get; set; }
        }
    }
}




