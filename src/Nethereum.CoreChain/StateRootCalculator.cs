using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.Nodes;

namespace Nethereum.CoreChain
{
    public class StateRootCalculator
    {
        private readonly IHashProvider _hashProvider;

        public StateRootCalculator() : this(Sha3KeccackHashProvider.Instance)
        {
        }

        public StateRootCalculator(IHashProvider hashProvider)
        {
            _hashProvider = hashProvider;
        }

        public async Task<byte[]> ComputeStateRootAsync(IStateStore stateStore, ITrieNodeStore trieNodeStore = null)
        {
            var accounts = await stateStore.GetAllAccountsAsync();
            if (accounts.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var store = AsNodeStore(trieNodeStore);
            var stateTrie = new PatriciaTrie(store, _hashProvider);

            foreach (var kvp in accounts)
            {
                var address = kvp.Key;
                var hashedKey = StateKeys.AccountKey(address);
                var storage = await stateStore.GetAllStorageAsync(address);

                PutAccount(stateTrie, hashedKey, kvp.Value, NonZeroValues(storage), store, trieNodeStore);
            }

            return RootOf(stateTrie, trieNodeStore);
        }

        public byte[] ComputeStateRoot(IDictionary<string, Account> accounts, IDictionary<string, IDictionary<BigInteger, byte[]>> storageByAddress = null, ITrieNodeStore trieNodeStore = null)
        {
            if (accounts == null || accounts.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var store = AsNodeStore(trieNodeStore);
            var stateTrie = new PatriciaTrie(store, _hashProvider);

            foreach (var kvp in accounts)
            {
                var address = kvp.Key;
                var hashedKey = StateKeys.AccountKey(address);

                PutAccount(stateTrie, hashedKey, kvp.Value, HashedStorageOf(storageByAddress, address), store, trieNodeStore);
            }

            return RootOf(stateTrie, trieNodeStore);
        }

        private void PutAccount(
            PatriciaTrie stateTrie, byte[] hashedKey, Account account,
            IEnumerable<KeyValuePair<byte[], byte[]>> hashedStorage,
            ITrieNodeStore store, ITrieNodeStore trieNodeStore)
        {
            var accountForTrie = new Account
            {
                Nonce = account.Nonce,
                Balance = account.Balance,
                CodeHash = account.CodeHash ?? DefaultValues.EMPTY_DATA_HASH
            };

            accountForTrie.StateRoot = StorageRootOf(hashedStorage.ToList(), store, trieNodeStore);

            var encodedAccount = AccountEncoder.Current.Encode(accountForTrie);
            stateTrie.Put(hashedKey, encodedAccount);
        }

        private byte[] StorageRootOf(
            List<KeyValuePair<byte[], byte[]>> filteredStorage, ITrieNodeStore store, ITrieNodeStore trieNodeStore)
        {
            if (filteredStorage.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var storageTrie = new PatriciaTrie(store, _hashProvider);
            foreach (var storageKvp in filteredStorage)
            {
                var trimmedValue = StateRootAccountRules.TrimLeadingZeros(storageKvp.Value);
                var encodedValue = RLP.RLP.EncodeElement(trimmedValue);
                storageTrie.Put(storageKvp.Key, encodedValue);
            }

            if (trieNodeStore != null)
            {
                storageTrie.SaveNodesToStorage();
            }

            return storageTrie.Root.GetHash();
        }

        private static byte[] RootOf(PatriciaTrie stateTrie, ITrieNodeStore trieNodeStore)
        {
            if (stateTrie.Root is EmptyNode)
                return DefaultValues.EMPTY_TRIE_HASH;

            if (trieNodeStore != null)
            {
                stateTrie.SaveNodesToStorage();
            }

            return stateTrie.Root.GetHash();
        }

        private static IEnumerable<KeyValuePair<byte[], byte[]>> HashedStorageOf(
            IDictionary<string, IDictionary<BigInteger, byte[]>> storageByAddress, string address)
        {
            IDictionary<BigInteger, byte[]> storage = null;
            storageByAddress?.TryGetValue(address, out storage);

            if (storage == null)
                return Enumerable.Empty<KeyValuePair<byte[], byte[]>>();

            return NonZeroValues(storage).Select(kvp =>
                new KeyValuePair<byte[], byte[]>(StateKeys.StorageSlotKey(kvp.Key), kvp.Value));
        }

        private static IEnumerable<KeyValuePair<TKey, byte[]>> NonZeroValues<TKey>(IEnumerable<KeyValuePair<TKey, byte[]>> storage)
            => storage.Where(kvp =>
                kvp.Value != null &&
                kvp.Value.Length > 0 &&
                !kvp.Value.All(b => b == 0));

        private static ITrieNodeStore AsNodeStore(ITrieNodeStore nodeStore) => nodeStore;
    }
}
