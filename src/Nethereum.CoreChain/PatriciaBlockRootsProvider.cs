using System.Collections.Generic;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util.HashProviders;

namespace Nethereum.CoreChain
{
    /// <summary>
    /// Default RLP + Patricia-trie <see cref="IBlockRootsProvider"/>. Encodes
    /// each transaction via the supplied <see cref="IBlockEncodingProvider"/>
    /// (default RLP) and defers to the shared <see cref="RootCalculator"/> for
    /// the Patricia-trie merkleisation. Optionally accepts an
    /// <see cref="IHashProvider"/> to drive the trie with Poseidon (EIP-7864
    /// binary-trie mode with Poseidon hash) instead of Keccak.
    /// </summary>
    public class PatriciaBlockRootsProvider : IBlockRootsProvider
    {
        public static PatriciaBlockRootsProvider Instance { get; } = new PatriciaBlockRootsProvider();

        private readonly RootCalculator _rootCalculator;
        private readonly IBlockEncodingProvider _encodingProvider;
        private readonly ITrieNodeStore _trieNodeStore;

        public PatriciaBlockRootsProvider(
            IBlockEncodingProvider encodingProvider = null,
            IHashProvider hashProvider = null,
            ITrieNodeStore trieNodeStore = null)
        {
            _encodingProvider = encodingProvider ?? RlpBlockEncodingProvider.Instance;
            _rootCalculator = hashProvider != null ? new RootCalculator(hashProvider) : new RootCalculator();
            _trieNodeStore = trieNodeStore;
        }

        public byte[] CalculateTransactionsRoot(IList<ISignedTransaction> transactions)
        {
            if (transactions == null || transactions.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var encoded = new List<byte[]>(transactions.Count);
            foreach (var tx in transactions)
                encoded.Add(_encodingProvider.EncodeTransaction(tx));

            return _rootCalculator.CalculateTransactionsRoot(encoded, _trieNodeStore);
        }

        public byte[] CalculateReceiptsRoot(IList<Receipt> receipts)
        {
            if (receipts == null || receipts.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            return _rootCalculator.CalculateReceiptsRoot(receipts, _trieNodeStore);
        }
    }
}
