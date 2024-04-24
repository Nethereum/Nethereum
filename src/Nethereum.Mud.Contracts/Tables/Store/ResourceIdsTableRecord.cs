using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Mud.EncodingDecoding;
using static Nethereum.Mud.Contracts.Tables.Store.ResourceIdsTableRecord;

namespace Nethereum.Mud.Contracts.Tables.Store
{
    public class ResourceIdsTableRecord : TableRecord<ResourceIdsKey, ResourceIdsValue>
    {
        public ResourceIdsTableRecord() : base("store", "ResourceIds")
        {
        }

        public class ResourceIdsKey
        {
            [Parameter("bytes32", "resourceId", 1)]
            public byte[] ResourceId { get; set; }

            public Resource GetResourceIdResource()
            {
                return ResourceEncoder.Decode(ResourceId);
            }
        }

        public class ResourceIdsValue
        {
            [Parameter("bool", "exists", 1)]
            public bool Exists { get; set; }
        }
    }
}
