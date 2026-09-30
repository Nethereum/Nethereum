
using Nethereum.Documentation;

namespace Nethereum.Merkle.Patricia.Storage
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "key-path-storage", "The raw hash-to-blob byte store behind a content-addressed trie store")]
    public interface INodeBlobStore
    {
        void Put(byte[] key, byte[] value);
        byte[] Get(byte[] key);
        void Delete(byte[] key);
    }
}
