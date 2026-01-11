using Nethereum.Merkle.Patricia;

namespace Nethereum.CoreChain.Storage
{
    public interface ITrieNodeStore : ITrieStorage
    {
        bool ContainsKey(byte[] key);
        void Flush();
        void Clear();
    }
}
