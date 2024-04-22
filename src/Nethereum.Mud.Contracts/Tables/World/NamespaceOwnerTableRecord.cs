using Nethereum.ABI.FunctionEncoding.Attributes;
using static Nethereum.Mud.Contracts.Tables.World.NamespaceOwnerTableRecord;

namespace Nethereum.Mud.Contracts.Tables.World
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
        }

        public class NamespaceOwnerValue
        {
            [Parameter("address", 1)]
            public string Owner { get; set; }
        }
    }

    }
