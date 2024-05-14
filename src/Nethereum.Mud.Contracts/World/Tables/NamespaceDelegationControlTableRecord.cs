using Nethereum.ABI.FunctionEncoding.Attributes;
using static Nethereum.Mud.Contracts.World.Tables.NamespaceDelegationControlTableRecord;
using Nethereum.Mud.EncodingDecoding;

namespace Nethereum.Mud.Contracts.World.Tables
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

