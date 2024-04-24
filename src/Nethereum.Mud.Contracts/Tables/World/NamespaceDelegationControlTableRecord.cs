using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Mud.EncodingDecoding;
using static Nethereum.Mud.Contracts.Tables.World.NamespaceDelegationControlTableRecord;

namespace Nethereum.Mud.Contracts.Tables.World
{
    public class NamespaceDelegationControlTableRecord : TableRecord<NamespaceDelegationControlKey, NamespaceDelegationControlValue>
    {
        public NamespaceDelegationControlTableRecord() : base("world", "NamespaceDelegationControl")
        {
        }

        public class NamespaceDelegationControlKey
        {
            [Parameter("bytes32", "namespaceId", 1)]
            public byte[] NamespaceId { get; set; }

            public Resource GetNamespaceIdResource()
            {
                return ResourceEncoder.Decode(NamespaceId);
            }
        }

        public class NamespaceDelegationControlValue
        {
            [Parameter("bytes32", "delegationControlId", 1)]
            public byte[] DelegationControlId { get; set; }
        }
    }

}

