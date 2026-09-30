using System.Collections.Generic;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util.HashProviders;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain
{
    public class RootCalculator
    {
        private readonly IHashProvider _hashProvider;

        public RootCalculator() : this(Sha3KeccackHashProvider.Instance)
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

            var storage = AsNodeStore(nodeStore);
            var trie = new PatriciaTrie(storage, _hashProvider);

            for (int i = 0; i < encodedTransactions.Count; i++)
            {
                var key = GetIndexKey(i);
                trie.Put(key, encodedTransactions[i]);
            }

            if (nodeStore != null)
            {
                trie.SaveNodesToStorage();
            }

            return trie.Root.GetHash();
        }

        public byte[] CalculateReceiptsRoot(IList<Receipt> receipts, ITrieNodeStore nodeStore = null)
        {
            if (receipts == null || receipts.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var storage = AsNodeStore(nodeStore);
            var trie = new PatriciaTrie(storage, _hashProvider);

            for (int i = 0; i < receipts.Count; i++)
            {
                var key = GetIndexKey(i);
                byte[] encodedReceipt;
                if (receipts[i].TransactionType > 0)
                {
                    encodedReceipt = ReceiptEncoder.Current.EncodeTyped(receipts[i], receipts[i].TransactionType);
                }
                else
                {
                    encodedReceipt = ReceiptEncoder.Current.Encode(receipts[i]);
                }
                trie.Put(key, encodedReceipt);
            }

            if (nodeStore != null)
            {
                trie.SaveNodesToStorage();
            }

            return trie.Root.GetHash();
        }

        public byte[] CalculateWithdrawalsRoot(IList<Withdrawal> withdrawals, ITrieNodeStore nodeStore = null)
        {
            if (withdrawals == null || withdrawals.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var storage = AsNodeStore(nodeStore);
            var trie = new PatriciaTrie(storage, _hashProvider);

            for (int i = 0; i < withdrawals.Count; i++)
            {
                var key = GetIndexKey(i);
                trie.Put(key, WithdrawalEncoder.Current.Encode(withdrawals[i]));
            }

            if (nodeStore != null)
            {
                trie.SaveNodesToStorage();
            }

            return trie.Root.GetHash();
        }

        public byte[] CalculateReceiptsRootFromEncoded(IList<byte[]> encodedReceipts, ITrieNodeStore nodeStore = null)
        {
            if (encodedReceipts == null || encodedReceipts.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var storage = AsNodeStore(nodeStore);
            var trie = new PatriciaTrie(storage, _hashProvider);

            for (int i = 0; i < encodedReceipts.Count; i++)
            {
                var key = GetIndexKey(i);
                trie.Put(key, encodedReceipts[i]);
            }

            if (nodeStore != null)
            {
                trie.SaveNodesToStorage();
            }

            return trie.Root.GetHash();
        }

        public byte[] CalculateStateRoot(IDictionary<byte[], Account> accounts, ITrieNodeStore nodeStore = null)
        {
            if (accounts == null || accounts.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var storage = AsNodeStore(nodeStore);
            var trie = new PatriciaTrie(storage, _hashProvider);

            foreach (var kvp in accounts)
            {
                var addressHash = kvp.Key;
                var encodedAccount = AccountEncoder.Current.Encode(kvp.Value);
                trie.Put(addressHash, encodedAccount);
            }

            if (nodeStore != null)
            {
                trie.SaveNodesToStorage();
            }

            return trie.Root.GetHash();
        }

        public byte[] CalculateStorageRoot(IDictionary<byte[], byte[]> storageSlots, ITrieNodeStore nodeStore = null)
        {
            if (storageSlots == null || storageSlots.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var storage = AsNodeStore(nodeStore);
            var trie = new PatriciaTrie(storage, _hashProvider);

            foreach (var kvp in storageSlots)
            {
                var keyHash = kvp.Key;
                var value = RLP.RLP.EncodeElement(kvp.Value);
                trie.Put(keyHash, value);
            }

            if (nodeStore != null)
            {
                trie.SaveNodesToStorage();
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
            return RLP.RLP.EncodeElement(index.ToBytesForRLPEncoding());
        }

        private static ITrieNodeStore AsNodeStore(ITrieNodeStore nodeStore)
            => nodeStore ?? new InMemoryContentNodeStore();
    }
}
