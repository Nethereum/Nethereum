using Nethereum.EVM;
using Nethereum.Merkle.Patricia;

namespace Nethereum.CoreChain
{
    public class PatriciaMerkleTreeBuilder : IMerkleTreeBuilder
    {
        private static readonly byte[] EMPTY_TRIE_ROOT =
            Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(
                "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421");

        private readonly PatriciaTrie _trie = new PatriciaTrie();

        public void Put(byte[] key, byte[] value)
        {
            _trie.Put(key, value);
        }

        public byte[] ComputeRoot()
        {
            return _trie.Root?.GetHash() ?? EMPTY_TRIE_ROOT;
        }
    }
}
