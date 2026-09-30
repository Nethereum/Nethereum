
using Nethereum.Documentation;

namespace Nethereum.Merkle.Binary.Proofs
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "binary-trie-proofs", "The nodes on a key path, as served to a verifier")]
    public class BinaryTrieProof
    {
        public byte[][] Nodes { get; set; }
    }
}
