
using Nethereum.Documentation;

namespace Nethereum.Merkle.Patricia.Storage
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "key-path-storage", "Reattach a trie to a root read straight out of a path store")]
    public interface IRawNodeReader
    {
        byte[] TryGetRawNode(byte[] owner, byte[] path);
    }
}
