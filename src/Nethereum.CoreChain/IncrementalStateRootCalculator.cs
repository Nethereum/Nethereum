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
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.Nodes;

namespace Nethereum.CoreChain
{
    public class IncrementalStateRootCalculator : IIncrementalStateRootCalculator
    {
        private readonly IStateStore _stateStore;
        private readonly ITrieNodeStore _nodeStore;
        private readonly IHashProvider _hashProvider;
        private readonly bool _emitTombstones;
        private readonly Sha3Keccack _sha3 = new();

        private PatriciaTrie _stateTrie;
        private readonly ConcurrentDictionary<string, PatriciaTrie> _storageTries = new();
        private readonly ConcurrentDictionary<string, byte> _modifiedStorageTries = new();
        private volatile bool _initialized;
        private volatile byte[] _cachedStateRoot;

        public IncrementalStateRootCalculator(
            IStateStore stateStore,
            ITrieNodeStore nodeStore = null,
            IHashProvider hashProvider = null,
            bool emitTombstones = false)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _nodeStore = nodeStore;
            _hashProvider = hashProvider ?? Sha3KeccackHashProvider.Instance;
            _emitTombstones = emitTombstones;
        }

        private PatriciaTrie NewStateTrie()
            => new PatriciaTrie(_nodeStore, _hashProvider) { Tracer = _emitTombstones ? new TrieTracer() : null };

        private PatriciaTrie NewStateTrie(byte[] rootHash)
            => new PatriciaTrie(rootHash, _nodeStore, _hashProvider) { Tracer = _emitTombstones ? new TrieTracer() : null };

        private PatriciaTrie NewStorageTrie(string address)
            => new PatriciaTrie(_nodeStore, GetHashedAddressKey(address), _hashProvider) { Tracer = _emitTombstones ? new TrieTracer() : null };

        private PatriciaTrie NewStorageTrie(string address, byte[] storageRoot)
            => new PatriciaTrie(storageRoot, _nodeStore, GetHashedAddressKey(address), _hashProvider) { Tracer = _emitTombstones ? new TrieTracer() : null };

        public Task<byte[]> ComputeStateRootAsync() => ComputeStateRootAsync(null);

        public async Task<byte[]> ComputeStateRootAsync(byte[] previousStateRoot)
        {
            if (!_initialized)
            {
                var warmStarted = InitialiseTrie(previousStateRoot);
                if (!warmStarted)
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

                        if (_nodeStore != null)
                        {
                            foreach (var address in _modifiedStorageTries.Keys)
                            {
                                if (_storageTries.TryGetValue(address, out var trie))
                                    trie.SaveNodesToStorage();
                            }
                            _stateTrie.SaveNodesToStorage();
                            (_nodeStore as ITrieWriteBuffer)?.FlushBuffer();
                        }
                    }

                    _modifiedStorageTries.Clear();
                    await _stateStore.ClearDirtyTrackingAsync();
                    return _cachedStateRoot;
                }

                _initialized = true;
                _cachedStateRoot = previousStateRoot;
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

                if (_nodeStore != null && _persistOnCompute)
                    PersistPendingNodes();
            }

            if (_persistOnCompute) _modifiedStorageTries.Clear();
            return _cachedStateRoot;
        }

        private bool _persistOnCompute = true;
        private readonly System.Collections.Generic.List<(string Address, Account Account)> _pendingFlatRootWrites = new();
        private readonly System.Collections.Generic.List<string> _pendingStorageWipes = new();

        public async Task<byte[]> ComputeStateRootWithoutPersistAsync(byte[] previousStateRoot)
        {
            _persistOnCompute = false;
            try { return await ComputeStateRootAsync(previousStateRoot).ConfigureAwait(false); }
            finally { _persistOnCompute = true; }
        }

        public async Task PersistPendingStateAsync()
        {
            foreach (var cleared in _pendingStorageWipes)
                (_nodeStore as IContractStorageWipeable)?.DeleteRange(GetHashedAddressKey(cleared));
            _pendingStorageWipes.Clear();
            if (_nodeStore != null) PersistPendingNodes();
            foreach (var (address, account) in _pendingFlatRootWrites)
                await _stateStore.SaveAccountAsync(address, account).ConfigureAwait(false);
            _pendingFlatRootWrites.Clear();
            _modifiedStorageTries.Clear();
        }

        public void DiscardPendingState()
        {
            _pendingFlatRootWrites.Clear();
            _pendingStorageWipes.Clear();
            InvalidateWarmState();
        }

        private void PersistPendingNodes()
        {
            try
            {
                foreach (var address in _modifiedStorageTries.Keys)
                {
                    if (_storageTries.TryGetValue(address, out var trie))
                        trie.SaveDirtyNodesToStorageAndCollapse();
                }
                _stateTrie.SaveDirtyNodesToStorageAndCollapse();
                (_nodeStore as ITrieWriteBuffer)?.FlushBuffer();
            }
            catch
            {
                InvalidateWarmState();
                throw;
            }
        }

        public async Task<byte[]> ComputeFullStateRootAsync()
        {
            _stateTrie = NewStateTrie();
            _storageTries.Clear();
            _initialized = false;

            bool anyAccount = false;
            await foreach (var kvp in _stateStore.StreamAccountsAsync().ConfigureAwait(false))
            {
                anyAccount = true;
                await PutAccountInTrieAsync(kvp.Key, kvp.Value, useAllStorage: true);
            }
            if (!anyAccount)
            {
                _cachedStateRoot = DefaultValues.EMPTY_TRIE_HASH;
                return _cachedStateRoot;
            }

            _initialized = true;

            if (_stateTrie.Root is EmptyNode)
            {
                _cachedStateRoot = DefaultValues.EMPTY_TRIE_HASH;
            }
            else
            {
                _cachedStateRoot = _stateTrie.Root.GetHash();

                if (_nodeStore != null)
                {
                    foreach (var address in _modifiedStorageTries.Keys)
                    {
                        if (_storageTries.TryGetValue(address, out var trie))
                            trie.SaveNodesToStorage();
                    }
                    _stateTrie.SaveNodesToStorage();
                    (_nodeStore as ITrieWriteBuffer)?.FlushBuffer();
                }
            }

            _modifiedStorageTries.Clear();
            return _cachedStateRoot;
        }

        private bool InitialiseTrie(byte[] previousStateRoot)
        {
            if (previousStateRoot == null
                || previousStateRoot.Length == 0
                || ByteUtil.AreEqual(previousStateRoot, DefaultValues.EMPTY_TRIE_HASH))
            {
                return false;
            }

            _stateTrie = NewStateTrie(previousStateRoot);
            _storageTries.Clear();
            return true;
        }

        private async Task InitializeFromFullStateAsync()
        {
            _stateTrie = NewStateTrie();
            _storageTries.Clear();

            await foreach (var kvp in _stateStore.StreamAccountsAsync().ConfigureAwait(false))
            {
                await PutAccountInTrieAsync(kvp.Key, kvp.Value, useAllStorage: true);
            }
        }

        private void InvalidateWarmState()
        {
            _storageTries.Clear();
            _modifiedStorageTries.Clear();
            _initialized = false;
            _cachedStateRoot = null;
        }

        private async Task<bool> UpdateFromDirtyAccountsAsync()
        {
            var dirtyAddresses = await _stateStore.GetDirtyAccountAddressesAsync();
            if (dirtyAddresses.Count == 0)
                return false;

            var clearedAddresses = await _stateStore.GetStorageClearedAddressesAsync();
            foreach (var cleared in clearedAddresses)
            {
                _storageTries.TryRemove(cleared, out _);
                if (_persistOnCompute)
                    (_nodeStore as IContractStorageWipeable)?.DeleteRange(GetHashedAddressKey(cleared));
                else
                    _pendingStorageWipes.Add(cleared);
            }

            foreach (var address in dirtyAddresses)
            {
                var account = await _stateStore.GetAccountAsync(address);
                if (account == null)
                {
                    var hashedKey = GetHashedAddressKey(address);
                    _stateTrie.Delete(hashedKey);
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
                CodeHash = account.CodeHash ?? DefaultValues.EMPTY_DATA_HASH,
                StateRoot = account.StateRoot ?? DefaultValues.EMPTY_TRIE_HASH
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
            _stateTrie.Put(hashedKey, encodedAccount);

            if (accountForTrie.StateRoot != null &&
                (account.StateRoot == null || !accountForTrie.StateRoot.SequenceEqual(account.StateRoot)))
            {
                if (_persistOnCompute)
                    await _stateStore.SaveAccountAsync(address, accountForTrie);
                else
                    _pendingFlatRootWrites.Add((address, accountForTrie));
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
                var storageTrie = _storageTries.GetOrAdd(address, a => NewStorageTrie(a));

                foreach (var kvp in filteredStorage)
                {
                    var trimmedValue = TrimLeadingZeros(kvp.Value);
                    var encodedValue = RLP.RLP.EncodeElement(trimmedValue);
                    storageTrie.Put(kvp.Key, encodedValue);
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
                var storageTrie = _storageTries.GetOrAdd(address, a =>
                    accountForTrie.StateRoot != null
                    && accountForTrie.StateRoot.Length == 32
                    && !accountForTrie.StateRoot.SequenceEqual(DefaultValues.EMPTY_TRIE_HASH)
                        ? NewStorageTrie(a, accountForTrie.StateRoot)
                        : NewStorageTrie(a));

                foreach (var slot in dirtySlots)
                {
                    var value = await _stateStore.GetStorageAsync(address, slot);
                    var hashedSlot = GetHashedSlotKey(slot);

                    if (value == null || value.Length == 0 || value.All(b => b == 0))
                    {
                        storageTrie.Delete(hashedSlot);
                    }
                    else
                    {
                        var trimmedValue = TrimLeadingZeros(value);
                        var encodedValue = RLP.RLP.EncodeElement(trimmedValue);
                        storageTrie.Put(hashedSlot, encodedValue);
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
            }
        }

        private byte[] GetHashedAddressKey(string address)
        {
            var evmAddress = EvmAddress.FromHex(address);

            if (_stateStore is IAddressHashCache addressHashCache)
            {
                if (addressHashCache.TryGetAddressHash(evmAddress, out var cached))
                    return cached;

                var computed = _sha3.CalculateHash(evmAddress.ToByteArray());
                addressHashCache.SetAddressHash(evmAddress, computed);
                return computed;
            }

            return _sha3.CalculateHash(evmAddress.ToByteArray());
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
