using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.Merkle
{
    [Struct("MerkleDropItem")]
    public class MerkleDropItem
    {
        [Parameter("address", "address")]
        public string Address { get; set; }

        [Parameter("uint256", "amount", 2)]
        public BigInteger Amount { get; set; }
    }

}
