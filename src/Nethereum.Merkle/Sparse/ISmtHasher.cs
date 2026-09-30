
using Nethereum.Documentation;

namespace Nethereum.Merkle.Sparse
{
    [NethereumDocExample(DocSection.SmartContracts, "sparse-merkle-tree", "The hashing strategy a sparse binary Merkle tree is built on")]
    public interface ISmtHasher
    {
        bool MsbFirst { get; }
        bool UseFixedEmptyHash { get; }
        bool CollapseSingleLeaf { get; }
        byte[] EmptyLeaf { get; }
        byte[] HashLeaf(byte[] path, byte[] valueBytes);
        byte[] HashNode(byte[] leftHash, byte[] rightHash);
    }
}
