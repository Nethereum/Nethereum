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

        public Task<byte[]> ComputeStateRootAsync() => ComputeStateRootAsync(null);

        public async Task<byte[]> ComputeStateRootAsync(byte[] previousStateRoot)
        {
            if (!_initialized)
            {
                var warmStarted = InitialiseTrie(previousStateRoot);
                if (!warmStarted)
                {
                    // No prior root in hand — rebuild from flat state. Used by
                    // genesis init and AppChain / DevChain test paths.
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

                // Warm-started from previousStateRoot — trie loads its nodes
                // lazily through _trieNodeStore. Fall through to apply this
                // block's dirty accounts on top.
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

        private bool InitialiseTrie(byte[] previousStateRoot)
        {
            if (previousStateRoot == null
                || previousStateRoot.Length == 0
                || ByteUtil.AreEqual(previousStateRoot, DefaultValues.EMPTY_TRIE_HASH))
            {
                return false;
            }

            _stateTrie = new PatriciaTrie(previousStateRoot, _hashProvider);
            _storageTries.Clear();
            return true;
        }

        private async Task InitializeFromFullStateAsync()
        {
            _stateTrie = new PatriciaTrie(_hashProvider);
            _storageTries.Clear();

            // Stream — never materialise the full account set. Mainnet state at
            // ~20M accounts × ~120 bytes/account would be ~2.4 GB in a single
            // Dictionary, and that's before the storage tries each touched
            // contract pulls in. First call after restart triggered the OOM.
            await foreach (var kvp in _stateStore.StreamAccountsAsync().ConfigureAwait(false))
            {
                await PutAccountInTrieAsync(kvp.Key, kvp.Value, useAllStorage: true);
            }
        }

        private async Task<bool> UpdateFromDirtyAccountsAsync()
        {
            var dirtyAddresses = await _stateStore.GetDirtyAccountAddressesAsync();
            if (dirtyAddresses.Count == 0)
                return false;

            // Drop cached storage tries for any address whose storage was fully
            // wiped via ClearStorageAsync since the last commit. Without this,
            // a SELFDESTRUCT followed by same-block re-materialisation
            // (pre-EIP-158 empty-account carve-out) keeps the stale storage trie
            // and the re-materialised empty leaf inherits a non-empty storageRoot.
            // First mainnet hit: block 116,525 — `0x4d95fbaf…` Killer pattern.
            var clearedAddresses = await _stateStore.GetStorageClearedAddressesAsync();
            foreach (var cleared in clearedAddresses)
            {
                _storageTries.TryRemove(cleared, out _);
            }

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

            // Initialise StateRoot from the persisted account's storage root.
            // PutAccountStorageIncrementalAsync's no-dirty-slot + cache-miss
            // path (lines 305-314) otherwise falls through to EMPTY_TRIE_HASH,
            // which is correct ONLY for EOAs and freshly-created empty
            // contracts — but WRONG for any pre-existing contract whose
            // storage cache wasn't populated (e.g. after auto-rewind + fresh
            // calculator init, or post-snapshot resume). Block 1,149,150 was
            // the first observed live-mainnet symptom: SLOAD-only contract
            // call where the account is dirtied for balance unchanged but
            // the persisted storage root gets clobbered to EMPTY_TRIE_HASH.
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
            _stateTrie.Put(hashedKey, encodedAccount, _trieNodeStore);

            // Only persist the recomputed StateRoot if it actually differs from
            // the stored account's value. SaveAccountAsync issues an INSERT OR
            // REPLACE against the accounts table, and InitializeFromFullStateAsync
            // calls this while iterating a live StreamAccountsAsync cursor over
            // the same table — writing back unnecessarily keeps the SQLite WAL
            // open without ever checkpointing, growing at ~140 MB/s and OOM'ing
            // a DevChain run inside a single block. For EOAs and freshly-funded
            // accounts both sides resolve to EMPTY_TRIE_HASH, so no save is
            // needed; the persist matters only for contracts whose storage trie
            // root genuinely changed (see commits #186/#187 for the SLOAD-only
            // slot poisoning regression that required this branch).
            if (accountForTrie.StateRoot != null &&
                (account.StateRoot == null || !accountForTrie.StateRoot.SequenceEqual(account.StateRoot)))
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
                    // Key is already keccak(slot) — the storage CF is keccak-keyed.
                    var trimmedValue = TrimLeadingZeros(kvp.Value);
                    var encodedValue = RLP.RLP.EncodeElement(trimmedValue);
                    storageTrie.Put(kvp.Key, encodedValue, _trieNodeStore);
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
                // No dirty storage slots for this address. The storage trie
                // is unchanged. Prefer the in-memory cache (in case of an
                // earlier intra-tx mutation that didn't dirty the slot but
                // did populate the cache). Cache miss = preserve the
                // accountForTrie.StateRoot we initialised from the persisted
                // account at PutAccountInTrieAsync entry — do NOT clobber it
                // with EMPTY_TRIE_HASH, which would wipe contract storage
                // roots for any pre-existing contract on the first access
                // after a fresh calculator init (e.g. post auto-rewind).
                if (_storageTries.TryGetValue(address, out var existingTrie) && !(existingTrie.Root is EmptyNode))
                {
                    accountForTrie.StateRoot = existingTrie.Root.GetHash();
                }
                // else: preserve the persisted StateRoot from the initialiser.
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
