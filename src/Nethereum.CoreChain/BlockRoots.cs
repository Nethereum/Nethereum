using Nethereum.Model;

namespace Nethereum.CoreChain
{
    public class BlockRoots
    {
        public byte[] StateRoot { get; set; } = DefaultValues.EMPTY_TRIE_HASH;
        public byte[] TransactionsRoot { get; set; } = DefaultValues.EMPTY_TRIE_HASH;
        public byte[] ReceiptsRoot { get; set; } = DefaultValues.EMPTY_TRIE_HASH;

        public static BlockRoots Empty => new BlockRoots();

        public BlockRoots() { }

        public BlockRoots(byte[] stateRoot, byte[] transactionsRoot, byte[] receiptsRoot)
        {
            StateRoot = stateRoot ?? DefaultValues.EMPTY_TRIE_HASH;
            TransactionsRoot = transactionsRoot ?? DefaultValues.EMPTY_TRIE_HASH;
            ReceiptsRoot = receiptsRoot ?? DefaultValues.EMPTY_TRIE_HASH;
        }
    }
}
