using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    public class HistoricalStateStore : IStateStore, IHistoricalStateProvider
    {
        private readonly IStateStore _inner;
        private readonly IStateDiffStore _diffStore;
        private readonly HistoricalStateOptions _options;
        private BigInteger? _currentBlockNumber;
        private BlockJournal _currentJournal;
        private long _blocksSinceLastPrune;

        public HistoricalStateStore(IStateStore inner)
            : this(inner, new InMemoryStateDiffStore(), HistoricalStateOptions.Default)
        {
        }

        public HistoricalStateStore(IStateStore inner, IStateDiffStore diffStore, HistoricalStateOptions options)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _diffStore = diffStore ?? throw new ArgumentNullException(nameof(diffStore));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public void SetCurrentBlockNumber(BigInteger blockNumber)
        {
            _currentBlockNumber = blockNumber;
            _currentJournal = new BlockJournal();
        }

        public async Task ClearCurrentBlockNumberAsync()
        {
            var blockNumber = _currentBlockNumber;
            var journal = _currentJournal;

            if (!blockNumber.HasValue || journal == null)
            {
                _currentBlockNumber = null;
                _currentJournal = null;
                return;
            }

            var diff = journal.ToBlockStateDiff(blockNumber.Value);
            if (diff.AccountDiffs.Count > 0 || diff.StorageDiffs.Count > 0)
            {
                // Persist BEFORE nulling the in-memory state. If
                // SaveBlockDiffAsync throws, _currentJournal stays armed so
                // a retry (or a hosted-error path) can re-flush. Otherwise
                // a transient I/O error would silently lose the journal
                // entry for the block.
                await _diffStore.SaveBlockDiffAsync(diff).ConfigureAwait(false);
            }

            // Pruning runs as its own write — it's a side effect of the
            // retention policy, not part of the current block's atomic
            // unit. Worst-case interruption leaves a few extra old
            // entries until the next prune cycle.
            if (_options.EnablePruning && _options.MaxHistoryBlocks > 0)
            {
                _blocksSinceLastPrune++;
                if (_blocksSinceLastPrune >= _options.PruningIntervalBlocks)
                {
                    _blocksSinceLastPrune = 0;
                    var pruneBelow = blockNumber.Value - _options.MaxHistoryBlocks;
                    if (pruneBelow > 0)
                    {
                        await _diffStore.DeleteDiffsBelowBlockAsync(pruneBelow).ConfigureAwait(false);
                    }
                }
            }

            // Only NOW null — diff is durable. Crash before this leaves
            // _currentJournal armed; retry re-flushes successfully.
            _currentBlockNumber = null;
            _currentJournal = null;
        }

        public async Task<Account> GetAccountAtBlockAsync(string address, BigInteger blockNumber)
        {
            var normalizedAddress = NormalizeAddress(address);

            await ThrowIfBlockOutOfRangeAsync(blockNumber).ConfigureAwait(false);

            var journal = _currentJournal;
            var journalBlock = _currentBlockNumber;
            if (journal != null && journalBlock.HasValue && journalBlock.Value > blockNumber)
            {
                if (journal.AccountPreValues.TryGetValue(normalizedAddress, out var journalPreValue))
                {
                    var (persistedFound, persistedPreValue) = await _diffStore
                        .GetFirstAccountPreValueAfterBlockAsync(normalizedAddress, blockNumber).ConfigureAwait(false);

                    if (persistedFound)
                        return persistedPreValue;

                    return journalPreValue;
                }
            }

            var (found, preValue) = await _diffStore
                .GetFirstAccountPreValueAfterBlockAsync(normalizedAddress, blockNumber).ConfigureAwait(false);

            if (found)
                return preValue;

            return await _inner.GetAccountAsync(address).ConfigureAwait(false);
        }

        public async Task<byte[]> GetStorageAtBlockAsync(string address, BigInteger slot, BigInteger blockNumber)
        {
            var normalizedAddress = NormalizeAddress(address);

            await ThrowIfBlockOutOfRangeAsync(blockNumber).ConfigureAwait(false);

            var journal = _currentJournal;
            var journalBlock = _currentBlockNumber;
            if (journal != null && journalBlock.HasValue && journalBlock.Value > blockNumber)
            {
                var storageKey = GetStorageKey(normalizedAddress, StateKeys.StorageSlotKey(slot));
                if (journal.StoragePreValues.TryGetValue(storageKey, out var journalPreValue))
                {
                    var (persistedFound, persistedPreValue) = await _diffStore
                        .GetFirstStoragePreValueAfterBlockAsync(normalizedAddress, slot, blockNumber).ConfigureAwait(false);

                    if (persistedFound)
                        return persistedPreValue;

                    return journalPreValue.PreValue;
                }
            }

            var (found, preValue) = await _diffStore
                .GetFirstStoragePreValueAfterBlockAsync(normalizedAddress, slot, blockNumber).ConfigureAwait(false);

            if (found)
                return preValue;

            return await _inner.GetStorageAsync(address, slot).ConfigureAwait(false);
        }

        private async Task ThrowIfBlockOutOfRangeAsync(BigInteger blockNumber)
        {
            if (_options.MaxHistoryBlocks <= 0)
                return;

            var newest = await _diffStore.GetNewestDiffBlockAsync().ConfigureAwait(false);
            if (newest.HasValue)
            {
                var lowerBound = newest.Value - _options.MaxHistoryBlocks;
                if (lowerBound > 0 && blockNumber < lowerBound)
                {
                    throw new HistoricalStateNotAvailableException(blockNumber, lowerBound);
                }
            }
        }

        public async Task PurgeDiffsAboveBlockAsync(BigInteger blockNumber)
        {
            await _diffStore.DeleteDiffsAboveBlockAsync(blockNumber).ConfigureAwait(false);
        }

        #region IStateStore delegation with journal recording

        public Task<Account> GetAccountAsync(string address)
        {
            return _inner.GetAccountAsync(address);
        }

        public async Task SaveAccountAsync(string address, Account account)
        {
            var journal = _currentJournal;
            if (journal != null)
            {
                var normalizedAddress = NormalizeAddress(address);
                if (!journal.AccountPreValues.ContainsKey(normalizedAddress))
                {
                    var preValue = await _inner.GetAccountAsync(address).ConfigureAwait(false);
                    journal.AccountPreValues[normalizedAddress] = CloneAccount(preValue);
                }
            }

            await _inner.SaveAccountAsync(address, account).ConfigureAwait(false);
        }

        public Task<bool> AccountExistsAsync(string address)
        {
            return _inner.AccountExistsAsync(address);
        }

        public async Task DeleteAccountAsync(string address)
        {
            var journal = _currentJournal;
            if (journal != null)
            {
                var normalizedAddress = NormalizeAddress(address);
                if (!journal.AccountPreValues.ContainsKey(normalizedAddress))
                {
                    var preValue = await _inner.GetAccountAsync(address).ConfigureAwait(false);
                    journal.AccountPreValues[normalizedAddress] = CloneAccount(preValue);
                }
            }

            await _inner.DeleteAccountAsync(address).ConfigureAwait(false);
        }

        public Task<Dictionary<string, Account>> GetAllAccountsAsync()
        {
            return _inner.GetAllAccountsAsync();
        }

        public System.Collections.Generic.IAsyncEnumerable<System.Collections.Generic.KeyValuePair<string, Account>> StreamAccountsAsync()
        {
            return _inner.StreamAccountsAsync();
        }

        public Task<byte[]> GetStorageAsync(string address, BigInteger slot)
        {
            return _inner.GetStorageAsync(address, slot);
        }

        public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value)
            => _inner.SaveStorageByKeccakAsync(address, slotKeccak, value);

        public async Task SaveStorageAsync(string address, BigInteger slot, byte[] value)
        {
            var journal = _currentJournal;
            if (journal != null)
            {
                var normalizedAddress = NormalizeAddress(address);
                var slotKey = StateKeys.StorageSlotKey(slot);
                var storageKey = GetStorageKey(normalizedAddress, slotKey);
                if (!journal.StoragePreValues.ContainsKey(storageKey))
                {
                    var preValue = await _inner.GetStorageAsync(address, slot).ConfigureAwait(false);
                    journal.StoragePreValues[storageKey] = new StorageJournalEntry
                    {
                        SlotKey = slotKey,
                        PreValue = (byte[])preValue?.Clone()
                    };
                }
            }

            await _inner.SaveStorageAsync(address, slot, value).ConfigureAwait(false);
        }

        public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address)
        {
            return _inner.GetAllStorageAsync(address);
        }

        public async Task ClearStorageAsync(string address)
        {
            var journal = _currentJournal;
            if (journal != null)
            {
                var normalizedAddress = NormalizeAddress(address);
                // Iterate the keccak-keyed storage; each entry already carries
                // the canonical storage-trie path (Yellow Paper §4.1) the journal
                // needs.
                var allStorage = await _inner.GetAllStorageAsync(address).ConfigureAwait(false);
                foreach (var kvp in allStorage)
                {
                    var storageKey = GetStorageKey(normalizedAddress, kvp.Key);
                    if (!journal.StoragePreValues.ContainsKey(storageKey))
                    {
                        journal.StoragePreValues[storageKey] = new StorageJournalEntry
                        {
                            SlotKey = kvp.Key,
                            PreValue = (byte[])kvp.Value?.Clone()
                        };
                    }
                }
            }

            await _inner.ClearStorageAsync(address).ConfigureAwait(false);
        }

        public Task<byte[]> GetCodeAsync(byte[] codeHash)
        {
            return _inner.GetCodeAsync(codeHash);
        }

        public Task SaveCodeAsync(byte[] codeHash, byte[] code)
        {
            return _inner.SaveCodeAsync(codeHash, code);
        }

        public Task<IStateSnapshot> CreateSnapshotAsync()
        {
            return _inner.CreateSnapshotAsync();
        }

        public Task CommitSnapshotAsync(IStateSnapshot snapshot)
        {
            return _inner.CommitSnapshotAsync(snapshot);
        }

        public Task RevertSnapshotAsync(IStateSnapshot snapshot)
        {
            return _inner.RevertSnapshotAsync(snapshot);
        }

        public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync()
        {
            return _inner.GetDirtyAccountAddressesAsync();
        }

        public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address)
        {
            return _inner.GetDirtyStorageSlotsAsync(address);
        }

        public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync()
        {
            return _inner.GetStorageClearedAddressesAsync();
        }

        public Task ClearDirtyTrackingAsync()
        {
            return _inner.ClearDirtyTrackingAsync();
        }

        #endregion

        private static string NormalizeAddress(string address)
        {
            return AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();
        }

        // Journal storage key = normalized address + ':' + hex(keccak(slot)).
        // Encoded as a string so it composes into ConcurrentDictionary's tuple
        // member equality; the storage CF rekey (R1/R2) keeps slots as
        // keccak(slot) bytes throughout, so this is the canonical journal shape.
        private static string GetStorageKey(string normalizedAddress, byte[] slotKey)
        {
            return $"{normalizedAddress}:{slotKey.ToHex()}";
        }

        private static Account CloneAccount(Account account)
        {
            if (account == null) return null;
            return new Account
            {
                Nonce = account.Nonce,
                Balance = account.Balance,
                StateRoot = (byte[])account.StateRoot?.Clone(),
                CodeHash = (byte[])account.CodeHash?.Clone()
            };
        }

    }

    internal class BlockJournal
    {
        public ConcurrentDictionary<string, Account> AccountPreValues { get; } = new();
        public ConcurrentDictionary<string, StorageJournalEntry> StoragePreValues { get; } = new();

        public BlockStateDiff ToBlockStateDiff(BigInteger blockNumber)
        {
            var diff = new BlockStateDiff { BlockNumber = blockNumber };

            foreach (var kvp in AccountPreValues)
            {
                diff.AccountDiffs.Add(new AccountDiffEntry
                {
                    Address = kvp.Key,
                    PreValue = kvp.Value
                });
            }

            foreach (var kvp in StoragePreValues)
            {
                var parts = kvp.Key.Split(':');
                diff.StorageDiffs.Add(new StorageDiffEntry
                {
                    Address = parts[0],
                    SlotKey = kvp.Value.SlotKey,
                    PreValue = kvp.Value.PreValue
                });
            }

            return diff;
        }
    }

    internal class StorageJournalEntry
    {
        // Yellow Paper §4.1 storage-trie path: keccak(slot).
        public byte[] SlotKey { get; set; }
        public byte[] PreValue { get; set; }
    }
}
