using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Mud.EncodingDecoding;
using static Nethereum.Mud.Contracts.World.Tables.NamespaceOwnerTableRecord;

namespace Nethereum.Mud.Contracts.World.Tables
{
    /*
      NamespaceOwner: {
      schema: {
        namespaceId: "ResourceId",
        owner: "address",
      },
      key: ["namespaceId"],
    }*/

    public class NamespaceOwnerTableRecord : TableRecord<NamespaceOwnerKey, NamespaceOwnerValue>
    {
        public NamespaceOwnerTableRecord() : base("world", "NamespaceOwner")
        {

        }
        public class NamespaceOwnerKey
        {
            [Parameter("bytes32", "namespaceId", 1)]
            public byte[] NamespaceId { get; set; }

            public Resource GetNamespaceIdResource()
            {
                return ResourceEncoder.Decode(NamespaceId);
            }
        }


        public class NamespaceOwnerValue
        {
            [Parameter("address", 1)]
            public string Owner { get; set; }
        }
    }

}
