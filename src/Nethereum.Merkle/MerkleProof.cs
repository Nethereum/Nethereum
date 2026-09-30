using Nethereum.Documentation;
using System.Collections.Generic;

namespace Nethereum.Merkle
{
    [NethereumDocExample(DocSection.SmartContracts, "incremental-merkle-tree", "A proof that carries sibling hashes and their direction")]
    public class MerkleProof
    {
        public List<byte[]> ProofNodes { get; set; } = new List<byte[]>();
        public List<int> PathIndices { get; set; } = new List<int>();
    }

}
