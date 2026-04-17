using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.CoreChain
{
    public class IncrementalStateRootCalculator : IIncrementalStateRootCalculator
    {
        private readonly IStateStore _stateStore;
        private readonly ITrieNodeStore _trieNodeStore;
        private readonly IHashProvider _hashProvider;
        private readonly Sha3Keccack _sha3 = new();

        private PatriciaTrie _stateTrie;
        private readonly ConcurrentDictionary<string, PatriciaTrie> _storageTries = new();
        private readonly ConcurrentDictionary<string, byte> _modifiedStorageTries = new();
        private volatile bool _initialized;
        private volatile byte[] _cachedStateRoot;

        public IncrementalStateRootCalculator(
            IStateStore stateStore,
            ITrieNodeStore trieNodeStore = null,
            IHashProvider hashProvider = null)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _trieNodeStore = trieNodeStore;
            _hashProvider = hashProvider ?? new Sha3KeccackHashProvider();
        }

        public async Task<byte[]> ComputeStateRootAsync()
        {
            if (!_initialized)
            {
                await InitializeFromFullStateAsync();
                _initialized = true;

                if (_stateTrie.Root is EmptyNode)
                {
                    _cachedStateRoot = DefaultValues.EMPTY_TRIE_HASH;
                }
                else
                {
                    _cachedStateRoot = _stateTrie.Root.GetHash();

                    if (_trieNodeStore != null)
                    {
                        foreach (var address in _modifiedStorageTries.Keys)
                        {
                            if (_storageTries.TryGetValue(address, out var trie))
                                trie.SaveNodesToStorage(_trieNodeStore);
                        }
                        _stateTrie.SaveNodesToStorage(_trieNodeStore);
                    }
                }

                _modifiedStorageTries.Clear();
                await _stateStore.ClearDirtyTrackingAsync();
                return _cachedStateRoot;
            }

            var hasDirtyAccounts = await UpdateFromDirtyAccountsAsync();
            await _stateStore.ClearDirtyTrackingAsync();

            if (!hasDirtyAccounts && _cachedStateRoot != null)
            {
                _modifiedStorageTries.Clear();
                return _cachedStateRoot;
            }

            if (_stateTrie.Root is EmptyNode)
            {
                _cachedStateRoot = DefaultValues.EMPTY_TRIE_HASH;
            }
            else
            {
                _cachedStateRoot = _stateTrie.Root.GetHash();

                if (_trieNodeStore != null)
                {
                    foreach (var address in _modifiedStorageTries.Keys)
                    {
                        if (_storageTries.TryGetValue(address, out var trie))
                            trie.SaveDirtyNodesToStorage(_trieNodeStore);
                    }
                    _stateTrie.SaveDirtyNodesToStorage(_trieNodeStore);
                }
            }

            _modifiedStorageTries.Clear();
            return _cachedStateRoot;
        }

        public async Task<byte[]> ComputeFullStateRootAsync()
        {
            _stateTrie = new PatriciaTrie(_hashProvider);
            _storageTries.Clear();
            _initialized = false;

            var accounts = await _stateStore.GetAllAccountsAsync();
            if (accounts.Count == 0)
            {
                _cachedStateRoot = DefaultValues.EMPTY_TRIE_HASH;
                return _cachedStateRoot;
            }

            foreach (var kvp in accounts)
            {
                await PutAccountInTrieAsync(kvp.Key, kvp.Value, useAllStorage: true);
            }

            _initialized = true;

            if (_stateTrie.Root is EmptyNode)
            {
                _cachedStateRoot = DefaultValues.EMPTY_TRIE_HASH;
            }
            else
            {
                _cachedStateRoot = _stateTrie.Root.GetHash();

                if (_trieNodeStore != null)
                {
                    foreach (var address in _modifiedStorageTries.Keys)
                    {
                        if (_storageTries.TryGetValue(address, out var trie))
                            trie.SaveNodesToStorage(_trieNodeStore);
                    }
                    _stateTrie.SaveNodesToStorage(_trieNodeStore);
                }
            }

            _modifiedStorageTries.Clear();
            return _cachedStateRoot;
        }

        public void Reset()
        {
            _stateTrie = null;
            _storageTries.Clear();
            _modifiedStorageTries.Clear();
            _initialized = false;
            _cachedStateRoot = null;
        }

        private async Task InitializeFromFullStateAsync()
        {
            _stateTrie = new PatriciaTrie(_hashProvider);
            _storageTries.Clear();

            var accounts = await _stateStore.GetAllAccountsAsync();
            if (accounts.Count == 0)
                return;

            foreach (var kvp in accounts)
            {
                await PutAccountInTrieAsync(kvp.Key, kvp.Value, useAllStorage: true);
            }
        }

        private async Task<bool> UpdateFromDirtyAccountsAsync()
        {
            var dirtyAddresses = await _stateStore.GetDirtyAccountAddressesAsync();
            if (dirtyAddresses.Count == 0)
                return false;

            foreach (var address in dirtyAddresses)
            {
                var account = await _stateStore.GetAccountAsync(address);
                if (account == null)
                {
                    var hashedKey = GetHashedAddressKey(address);
                    _stateTrie.Delete(hashedKey, _trieNodeStore);
                    _storageTries.TryRemove(address, out _);
                }
                else
                {
                    await PutAccountInTrieAsync(address, account, useAllStorage: false);
                }
            }

            return true;
        }

        private async Task PutAccountInTrieAsync(string address, Account account, bool useAllStorage)
        {
            var hashedKey = GetHashedAddressKey(address);

            var accountForTrie = new Account
            {
                Nonce = account.Nonce,
                Balance = account.Balance,
                CodeHash = account.CodeHash ?? DefaultValues.EMPTY_DATA_HASH
            };

            if (useAllStorage)
            {
                await PutAccountStorageFullAsync(address, accountForTrie);
            }
            else
            {
                await PutAccountStorageIncrementalAsync(address, accountForTrie);
            }

            var encodedAccount = AccountEncoder.Current.Encode(accountForTrie);
            _stateTrie.Put(hashedKey, encodedAccount, _trieNodeStore);

            if (accountForTrie.StateRoot != null)
            {
                account.StateRoot = accountForTrie.StateRoot;
                await _stateStore.SaveAccountAsync(address, account);
            }
        }

        private async Task PutAccountStorageFullAsync(string address, Account accountForTrie)
        {
            var storage = await _stateStore.GetAllStorageAsync(address);

            var filteredStorage = storage.Where(kvp =>
                kvp.Value != null &&
                kvp.Value.Length > 0 &&
                !kvp.Value.All(b => b == 0)).ToDictionary(k => k.Key, v => v.Value);

            if (filteredStorage.Count > 0)
            {
                var storageTrie = _storageTries.GetOrAdd(address, _ => new PatriciaTrie(_hashProvider));

                foreach (var kvp in filteredStorage)
                {
                    var hashedSlot = GetHashedSlotKey(kvp.Key);
                    var trimmedValue = TrimLeadingZeros(kvp.Value);
                    var encodedValue = RLP.RLP.EncodeElement(trimmedValue);
                    storageTrie.Put(hashedSlot, encodedValue, _trieNodeStore);
                }

                _modifiedStorageTries.TryAdd(address, 0);

                accountForTrie.StateRoot = storageTrie.Root.GetHash();
            }
            else
            {
                accountForTrie.StateRoot = DefaultValues.EMPTY_TRIE_HASH;
                _storageTries.TryRemove(address, out _);
            }
        }

        private async Task PutAccountStorageIncrementalAsync(string address, Account accountForTrie)
        {
            var dirtySlots = await _stateStore.GetDirtyStorageSlotsAsync(address);

            if (dirtySlots.Count > 0)
            {
                var storageTrie = _storageTries.GetOrAdd(address, _ => new PatriciaTrie(_hashProvider));

                foreach (var slot in dirtySlots)
                {
                    var value = await _stateStore.GetStorageAsync(address, slot);
                    var hashedSlot = GetHashedSlotKey(slot);

                    if (value == null || value.Length == 0 || value.All(b => b == 0))
                    {
                        storageTrie.Delete(hashedSlot, _trieNodeStore);
                    }
                    else
                    {
                        var trimmedValue = TrimLeadingZeros(value);
                        var encodedValue = RLP.RLP.EncodeElement(trimmedValue);
                        storageTrie.Put(hashedSlot, encodedValue, _trieNodeStore);
                    }
                }

                _modifiedStorageTries.TryAdd(address, 0);

                if (storageTrie.Root is EmptyNode)
                {
                    accountForTrie.StateRoot = DefaultValues.EMPTY_TRIE_HASH;
                    _storageTries.TryRemove(address, out _);
                }
                else
                {
                    accountForTrie.StateRoot = storageTrie.Root.GetHash();
                }
            }
            else
            {
                if (_storageTries.TryGetValue(address, out var existingTrie) && !(existingTrie.Root is EmptyNode))
                {
                    accountForTrie.StateRoot = existingTrie.Root.GetHash();
                }
                else
                {
                    accountForTrie.StateRoot = DefaultValues.EMPTY_TRIE_HASH;
                }
            }
        }

        private byte[] GetHashedAddressKey(string address)
        {
            var addressBytes = AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray();
            return _sha3.CalculateHash(addressBytes);
        }

        private byte[] GetHashedSlotKey(BigInteger slot)
        {
            var slotBytes = slot.ToBytesForRLPEncoding().PadBytes(32);
            return _sha3.CalculateHash(slotBytes);
        }

        private static byte[] TrimLeadingZeros(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return new byte[0];

            var firstNonZero = 0;
            while (firstNonZero < bytes.Length && bytes[firstNonZero] == 0)
                firstNonZero++;

            if (firstNonZero == bytes.Length)
                return new byte[0];

            var result = new byte[bytes.Length - firstNonZero];
            Array.Copy(bytes, firstNonZero, result, 0, result.Length);
            return result;
        }
    }
}
