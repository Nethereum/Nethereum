using System.Collections.Generic;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util.HashProviders;

namespace Nethereum.CoreChain
{
    public class RootCalculator
    {
        private readonly IHashProvider _hashProvider;

        public RootCalculator() : this(new Sha3KeccackHashProvider())
        {
        }

        public RootCalculator(IHashProvider hashProvider)
        {
            _hashProvider = hashProvider;
        }

        public byte[] CalculateTransactionsRoot(IList<byte[]> encodedTransactions, ITrieNodeStore nodeStore = null)
        {
            if (encodedTransactions == null || encodedTransactions.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var trie = new PatriciaTrie(_hashProvider);
            var storage = nodeStore ?? new InMemoryTrieNodeStore();

            for (int i = 0; i < encodedTransactions.Count; i++)
            {
                var key = GetIndexKey(i);
                trie.Put(key, encodedTransactions[i], storage);
            }

            return trie.Root.GetHash();
        }

        public byte[] CalculateReceiptsRoot(IList<Receipt> receipts, ITrieNodeStore nodeStore = null)
        {
            if (receipts == null || receipts.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var trie = new PatriciaTrie(_hashProvider);
            var storage = nodeStore ?? new InMemoryTrieNodeStore();

            for (int i = 0; i < receipts.Count; i++)
            {
                var key = GetIndexKey(i);
                var encodedReceipt = ReceiptEncoder.Current.Encode(receipts[i]);
                trie.Put(key, encodedReceipt, storage);
            }

            return trie.Root.GetHash();
        }

        public byte[] CalculateReceiptsRootFromEncoded(IList<byte[]> encodedReceipts, ITrieNodeStore nodeStore = null)
        {
            if (encodedReceipts == null || encodedReceipts.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var trie = new PatriciaTrie(_hashProvider);
            var storage = nodeStore ?? new InMemoryTrieNodeStore();

            for (int i = 0; i < encodedReceipts.Count; i++)
            {
                var key = GetIndexKey(i);
                trie.Put(key, encodedReceipts[i], storage);
            }

            return trie.Root.GetHash();
        }

        public byte[] CalculateStateRoot(IDictionary<byte[], Account> accounts, ITrieNodeStore nodeStore = null)
        {
            if (accounts == null || accounts.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var trie = new PatriciaTrie(_hashProvider);
            var storage = nodeStore ?? new InMemoryTrieNodeStore();

            foreach (var kvp in accounts)
            {
                var addressHash = kvp.Key;
                var encodedAccount = AccountEncoder.Current.Encode(kvp.Value);
                trie.Put(addressHash, encodedAccount, storage);
            }

            return trie.Root.GetHash();
        }

        public byte[] CalculateStorageRoot(IDictionary<byte[], byte[]> storageSlots, ITrieNodeStore nodeStore = null)
        {
            if (storageSlots == null || storageSlots.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var trie = new PatriciaTrie(_hashProvider);
            var storage = nodeStore ?? new InMemoryTrieNodeStore();

            foreach (var kvp in storageSlots)
            {
                var keyHash = kvp.Key;
                var value = RLP.RLP.EncodeElement(kvp.Value);
                trie.Put(keyHash, value, storage);
            }

            return trie.Root.GetHash();
        }

        public BlockRoots CalculateBlockRoots(
            IDictionary<byte[], Account> accounts,
            IList<byte[]> encodedTransactions,
            IList<Receipt> receipts,
            ITrieNodeStore stateStore = null,
            ITrieNodeStore txStore = null,
            ITrieNodeStore receiptStore = null)
        {
            return new BlockRoots(
                CalculateStateRoot(accounts, stateStore),
                CalculateTransactionsRoot(encodedTransactions, txStore),
                CalculateReceiptsRoot(receipts, receiptStore)
            );
        }

        private byte[] GetIndexKey(int index)
        {
            return index.ToBytesForRLPEncoding();
        }
    }
}
