using Nethereum.ABI.FunctionEncoding.Attributes;
using static Nethereum.Mud.Contracts.World.Tables.BalancesTableRecord;
using Nethereum.Mud.EncodingDecoding;
using System.Numerics;

namespace Nethereum.Mud.Contracts.World.Tables
{
    public class BalancesTableRecord : TableRecord<BalancesKey, BalancesValue>
    {
        public BalancesTableRecord() : base("world", "Balances")
        {
        }

        public class BalancesKey
        {
            [Parameter("bytes32", "namespaceId", 1)]
            public byte[] NamespaceId { get; set; }

            public Resource GetNamespaceIdResource()
            {
                return ResourceEncoder.Decode(NamespaceId);
            }
        }

        public class BalancesValue
        {
            [Parameter("uint256", "balance", 1)]
            public BigInteger Balance { get; set; }
        }
    }




}

