using Nethereum.EVM;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    public class PatriciaMerkleTreeBuilder : IMerkleTreeBuilder
    {
        private readonly PatriciaTrie _trie = new PatriciaTrie();

        public void Put(byte[] key, byte[] value)
        {
            _trie.Put(key, value);
        }

        public byte[] ComputeRoot()
        {
            return _trie.Root?.GetHash() ?? DefaultValues.EMPTY_TRIE_HASH;
        }
    }
}
