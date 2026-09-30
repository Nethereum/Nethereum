
using Nethereum.Documentation;

namespace Nethereum.Merkle.Binary.Storage
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "binary-trie-storage", "The plain blob store a binary trie persists into")]
    public interface IBinaryTrieStorage
    {
        void Put(byte[] key, byte[] value);
        byte[] Get(byte[] key);
        void Delete(byte[] key);
    }
}
