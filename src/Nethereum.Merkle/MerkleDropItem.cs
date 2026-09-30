using Nethereum.Documentation;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.Merkle
{
    [Struct("MerkleDropItem")]
    [NethereumDocExample(DocSection.SmartContracts, "merkle-tree", "The address and amount leaf of a token airdrop tree")]
    public class MerkleDropItem
    {
        [Parameter("address", "address")]
        public string Address { get; set; }

        [Parameter("uint256", "amount", 2)]
        public BigInteger Amount { get; set; }
    }

}
