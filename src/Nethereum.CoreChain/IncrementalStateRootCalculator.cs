using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Prefetch;
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
    public class IncrementalStateRootCalculator : IIncrementalStateRootCalculator, IStateRootPhaseReporter, IDisposable
    {
        private readonly IndexedWorkerPool _storageWorkers;
        private readonly IStateStore _stateStore;
        private readonly ITrieNodeStore _nodeStore;
        private readonly IHashProvider _hashProvider;
        private readonly bool _emitTombstones;
        private readonly StateRootPhaseClock _phases = new();

        private PatriciaTrie _stateTrie;
        private readonly ConcurrentDictionary<EvmAddress, PatriciaTrie> _storageTries = new();
        private readonly ConcurrentDictionary<EvmAddress, byte> _modifiedStorageTries = new();
        private volatile bool _initialized;
        private volatile byte[] _cachedStateRoot;
        private volatile BlockTrieSet _blockTries;

        public IncrementalStateRootCalculator(
            IStateStore stateStore,
            ITrieNodeStore nodeStore = null,
            IHashProvider hashProvider = null,
            bool emitTombstones = false,
            int stateRootWorkers = 1)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _nodeStore = nodeStore;
            _hashProvider = hashProvider ?? Sha3KeccackHashProvider.Instance;
            _emitTombstones = emitTombstones;
            _storageWorkers = new IndexedWorkerPool(stateRootWorkers);
            StateRootWorkers = stateRootWorkers;
        }

        public ITrieNodeStore NodeStore => _nodeStore;

        public int StateRootWorkers { get; }

        public int WorkerThreadsStarted => _storageWorkers.ThreadsStarted;

        public void Dispose() => _storageWorkers.Dispose();

        private PatriciaTrie NewStateTrie()
            => new PatriciaTrie(_nodeStore, _hashProvider) { Tracer = _emitTombstones ? new TrieTracer() : null };

        private PatriciaTrie NewStateTrie(byte[] rootHash)
            => new PatriciaTrie(rootHash, _nodeStore, _hashProvider) { Tracer = _emitTombstones ? new TrieTracer() : null };

        private PatriciaTrie NewStorageTrie(EvmAddress address)
            => new PatriciaTrie(_nodeStore, GetHashedAddressKey(address), _hashProvider) { Tracer = _emitTombstones ? new TrieTracer() : null };

        private PatriciaTrie NewStorageTrie(byte[] hashedAddress, byte[] storageRoot)
            => new PatriciaTrie(storageRoot, _nodeStore, hashedAddress, _hashProvider) { Tracer = _emitTombstones ? new TrieTracer() : null };

        public BlockTrieSet BeginBlockTries(byte[] parentRoot)
        {
            if (parentRoot == null || parentRoot.Length == 0 || ByteUtil.AreEqual(parentRoot, DefaultValues.EMPTY_TRIE_HASH))
                return null;
            _blockTries = new BlockTrieSet(parentRoot, NewStateTrie(parentRoot), NewStorageTrie);
            return _blockTries;
        }

        public Task<byte[]> ComputeStateRootAsync() => ComputeStateRootAsync(null);

        public async Task<byte[]> ComputeStateRootAsync(byte[] previousStateRoot)
        {
            try
            {
                return await ComputeStateRootFromTriesAsync(previousStateRoot).ConfigureAwait(false);
            }
            finally
            {
                _blockTries = null;
            }
        }

        private async Task<byte[]> ComputeStateRootFromTriesAsync(byte[] previousStateRoot)
        {
            if (!_initialized)
            {
                var warmStarted = InitialiseTrie(previousStateRoot);
                if (!warmStarted)
                {
                    await InitializeFromFullStateAsync();
                    _initialized = true;
                    _cachedStateRoot = HashAndSaveBuiltTrie();
                    _modifiedStorageTries.Clear();
                    await _stateStore.ClearDirtyTrackingAsync();
                    return _cachedStateRoot;
                }

                _initialized = true;
                _cachedStateRoot = previousStateRoot;
            }

            AdoptPrefetchedAccountTrie();
            var hasDirtyAccounts = await UpdateFromDirtyAccountsAsync();
            await _stateStore.ClearDirtyTrackingAsync();

            if (!hasDirtyAccounts && _cachedStateRoot != null)
            {
                _modifiedStorageTries.Clear();
                return _cachedStateRoot;
            }

            _cachedStateRoot = HashUpdatedTrie();
            if (_persistOnCompute) _modifiedStorageTries.Clear();
            return _cachedStateRoot;
        }

        private void AdoptPrefetchedAccountTrie()
        {
            var prefetched = _blockTries?.TakeAccountTrie(_cachedStateRoot);
            if (prefetched != null) _stateTrie = prefetched;
        }

        private byte[] HashUpdatedTrie()
        {
            if (_stateTrie.Root is EmptyNode)
                return DefaultValues.EMPTY_TRIE_HASH;

            byte[] root;
            using (_phases.Time(StateRootPhase.AccountHashes))
                root = _stateTrie.Root.GetHash();

            if (_nodeStore != null && _persistOnCompute)
                PersistPendingNodes();
            return root;
        }

        private bool _persistOnCompute = true;
        private readonly System.Collections.Generic.Dictionary<EvmAddress, Account> _pendingFlatRootWrites = new();
        private readonly System.Collections.Generic.List<EvmAddress> _pendingStorageWipes = new();

        public async Task<byte[]> ComputeStateRootWithoutPersistAsync(byte[] previousStateRoot)
        {
            _persistOnCompute = false;
            try { return await ComputeStateRootAsync(previousStateRoot).ConfigureAwait(false); }
            finally { _persistOnCompute = true; }
        }

        public async Task PersistPendingStateAsync()
        {
            using (_phases.Time(StateRootPhase.DatabaseCommit))
            {
                foreach (var cleared in _pendingStorageWipes)
                    (_nodeStore as IContractStorageWipeable)?.DeleteRange(GetHashedAddressKey(cleared));
                _pendingStorageWipes.Clear();
            }
            if (_nodeStore != null) PersistPendingNodes();
            using (_phases.Time(StateRootPhase.DatabaseCommit))
            {
                foreach (var pending in _pendingFlatRootWrites)
                    await _stateStore.SaveAccountAsync(pending.Key, pending.Value).ConfigureAwait(false);
                _pendingFlatRootWrites.Clear();
            }
            _modifiedStorageTries.Clear();
        }

        public StateRootPhaseTimings TakePhaseTimings() => _phases.Take();

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
                using (_phases.Time(StateRootPhase.StorageCommits))
                {
                    foreach (var address in _modifiedStorageTries.Keys)
                    {
                        if (_storageTries.TryGetValue(address, out var trie))
                            trie.SaveDirtyNodesToStorageAndCollapse();
                    }
                }
                using (_phases.Time(StateRootPhase.AccountCommits))
                    _stateTrie.SaveDirtyNodesToStorageAndCollapse();
                using (_phases.Time(StateRootPhase.DatabaseCommit))
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
                await PutFullAccountAsync(EvmAddress.FromHex(kvp.Key), kvp.Value);
            }
            if (!anyAccount)
            {
                _cachedStateRoot = DefaultValues.EMPTY_TRIE_HASH;
                return _cachedStateRoot;
            }

            _initialized = true;
            _cachedStateRoot = HashAndSaveBuiltTrie();
            _modifiedStorageTries.Clear();
            return _cachedStateRoot;
        }

        private byte[] HashAndSaveBuiltTrie()
        {
            if (_stateTrie.Root is EmptyNode)
                return DefaultValues.EMPTY_TRIE_HASH;

            var root = _stateTrie.Root.GetHash();
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
            return root;
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
                await PutFullAccountAsync(EvmAddress.FromHex(kvp.Key), kvp.Value);
            }
        }

        private void InvalidateWarmState()
        {
            _blockTries = null;
            _storageTries.Clear();
            _modifiedStorageTries.Clear();
            _initialized = false;
            _cachedStateRoot = null;
        }

        private async Task<bool> UpdateFromDirtyAccountsAsync()
        {
            var dirtyAddresses = await _stateStore.GetDirtyAccountsAsync();
            if (dirtyAddresses.Count == 0)
                return false;

            await ForgetClearedStorageAsync();

            using (_phases.TimeExcluding(StateRootPhase.AccountUpdates, StateRootPhase.StorageUpdates))
            {
                var dirty = await ReadDirtyAccountsAsync(dirtyAddresses);
                UpdateStorageTries(dirty.StorageUpdates);
                await PutUpdatedAccountsAsync(dirty.Updated);
                DeleteRemovedAccounts(dirty.Deleted);
            }

            return true;
        }

        private async Task ForgetClearedStorageAsync()
        {
            var clearedAddresses = await _stateStore.GetStorageClearedAccountsAsync();
            foreach (var cleared in clearedAddresses)
            {
                _storageTries.TryRemove(cleared, out _);
                if ((_stateStore as IBaseAbsenceSource)?.WasAbsentInBase(cleared) == true) continue;
                if (_persistOnCompute)
                    (_nodeStore as IContractStorageWipeable)?.DeleteRange(GetHashedAddressKey(cleared));
                else
                    _pendingStorageWipes.Add(cleared);
            }
        }

        private async Task<DirtyAccounts> ReadDirtyAccountsAsync(IEnumerable<EvmAddress> dirtyAddresses)
        {
            var dirty = new DirtyAccounts();
            foreach (var address in dirtyAddresses)
            {
                var account = await _stateStore.GetAccountAsync(address);
                if (account == null)
                    dirty.Deleted.Add(address);
                else
                    dirty.Updated.Add(await ReadDirtyAccountAsync(address, account));
            }
            return dirty;
        }

        private async Task<DirtyAccount> ReadDirtyAccountAsync(EvmAddress address, Account account)
        {
            var accountForTrie = TrieAccountWithPendingStorageRootOf(address, account);
            var dirtySlots = await ReadDirtySlotsAsync(address);

            StorageTrieUpdate storage = null;
            if (dirtySlots.Count > 0)
                storage = new StorageTrieUpdate(StorageTrieFor(address, accountForTrie), dirtySlots);
            else if (_storageTries.TryGetValue(address, out var existingTrie) && !(existingTrie.Root is EmptyNode))
                accountForTrie.StateRoot = existingTrie.Root.GetHash();

            return new DirtyAccount(address, account, accountForTrie, storage);
        }

        private async Task<List<KeyValuePair<BigInteger, byte[]>>> ReadDirtySlotsAsync(EvmAddress address)
        {
            var dirtySlots = await _stateStore.GetDirtyStorageSlotsAsync(address);
            var slots = new List<KeyValuePair<BigInteger, byte[]>>(dirtySlots.Count);
            foreach (var slot in dirtySlots)
                slots.Add(new KeyValuePair<BigInteger, byte[]>(slot, await _stateStore.GetStorageAsync(address, slot)));
            return slots;
        }

        private void UpdateStorageTries(List<StorageTrieUpdate> updates)
        {
            using (_phases.Time(StateRootPhase.StorageUpdates))
            {
                try
                {
                    _storageWorkers.Run(updates.Count, index => updates[index].Apply());
                }
                catch
                {
                    InvalidateWarmState();
                    throw;
                }
            }
        }

        private async Task PutUpdatedAccountsAsync(List<DirtyAccount> updated)
        {
            foreach (var account in updated)
            {
                AdoptStorageRoot(account);
                await PutAccountInStateTrieAsync(account.Address, account.Source, account.ForTrie);
                _phases.Count(StateRootCounter.AccountUpdated);
            }
        }

        private void AdoptStorageRoot(DirtyAccount account)
        {
            var storage = account.Storage;
            if (storage == null) return;

            _phases.Count(StateRootCounter.StorageUpdated, storage.SlotsUpdated);
            _phases.Count(StateRootCounter.StorageDeleted, storage.SlotsDeleted);
            _modifiedStorageTries.TryAdd(account.Address, 0);

            if (storage.Emptied)
            {
                account.ForTrie.StateRoot = DefaultValues.EMPTY_TRIE_HASH;
                _storageTries.TryRemove(account.Address, out _);
            }
            else
            {
                account.ForTrie.StateRoot = storage.Root;
            }
        }

        private void DeleteRemovedAccounts(List<EvmAddress> deleted)
        {
            foreach (var address in deleted)
            {
                _stateTrie.Delete(GetHashedAddressKey(address));
                _storageTries.TryRemove(address, out _);
                _phases.Count(StateRootCounter.AccountDeleted);
            }
        }

        private static Account TrieAccountOf(Account account) => new Account
        {
            Nonce = account.Nonce,
            Balance = account.Balance,
            CodeHash = account.CodeHash ?? DefaultValues.EMPTY_DATA_HASH,
            StateRoot = account.StateRoot ?? DefaultValues.EMPTY_TRIE_HASH
        };

        private Account TrieAccountWithPendingStorageRootOf(EvmAddress address, Account account)
        {
            var accountForTrie = TrieAccountOf(account);
            if (_pendingFlatRootWrites.TryGetValue(address, out var pending))
                accountForTrie.StateRoot = pending.StateRoot;
            return accountForTrie;
        }

        private async Task PutFullAccountAsync(EvmAddress address, Account account)
        {
            var accountForTrie = TrieAccountOf(account);
            await PutAccountStorageFullAsync(address, accountForTrie);
            await PutAccountInStateTrieAsync(address, account, accountForTrie);
        }

        private async Task PutAccountInStateTrieAsync(EvmAddress address, Account account, Account accountForTrie)
        {
            _stateTrie.Put(GetHashedAddressKey(address), AccountEncoder.Current.Encode(accountForTrie));

            if (accountForTrie.StateRoot != null &&
                (account.StateRoot == null || !accountForTrie.StateRoot.SequenceEqual(account.StateRoot)))
            {
                if (_persistOnCompute)
                {
                    _pendingFlatRootWrites.Remove(address);
                    await _stateStore.SaveAccountAsync(address, accountForTrie);
                }
                else
                    _pendingFlatRootWrites[address] = accountForTrie;
            }
            else
                _pendingFlatRootWrites.Remove(address);
        }

        private async Task PutAccountStorageFullAsync(EvmAddress address, Account accountForTrie)
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
                    var trimmedValue = StateRootAccountRules.TrimLeadingZeros(kvp.Value);
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

        private PatriciaTrie StorageTrieFor(EvmAddress address, Account accountForTrie)
        {
            var hasStorage = accountForTrie.StateRoot != null
                && accountForTrie.StateRoot.Length == 32
                && !accountForTrie.StateRoot.SequenceEqual(DefaultValues.EMPTY_TRIE_HASH);
            if (!hasStorage) return _storageTries.GetOrAdd(address, a => NewStorageTrie(a));

            var prefetched = _blockTries?.TakeStorageTrie(GetHashedAddressKey(address), accountForTrie.StateRoot);
            if (prefetched != null) return _storageTries[address] = prefetched;
            return _storageTries.GetOrAdd(address, a => NewStorageTrie(GetHashedAddressKey(a), accountForTrie.StateRoot));
        }


        private sealed class DirtyAccount
        {
            public DirtyAccount(EvmAddress address, Account source, Account forTrie, StorageTrieUpdate storage)
            {
                Address = address;
                Source = source;
                ForTrie = forTrie;
                Storage = storage;
            }

            public EvmAddress Address { get; }

            public Account Source { get; }

            public Account ForTrie { get; }

            public StorageTrieUpdate Storage { get; }
        }

        private sealed class DirtyAccounts
        {
            public List<DirtyAccount> Updated { get; } = new List<DirtyAccount>();

            public List<EvmAddress> Deleted { get; } = new List<EvmAddress>();

            public List<StorageTrieUpdate> StorageUpdates
                => Updated.Where(a => a.Storage != null).Select(a => a.Storage).ToList();
        }

        private byte[] GetHashedAddressKey(EvmAddress address) => StateKeys.AccountKey(address, _stateStore);
    }
}
