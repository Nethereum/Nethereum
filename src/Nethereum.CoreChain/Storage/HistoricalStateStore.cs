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
    public class HistoricalStateStore : IStateStore, IHistoricalStateProvider, ISnapFlatStateWriter, IFlatCacheInvalidatable, IStateReadStats
    {
        private readonly IStateStore _inner;
        public IStateStore Inner => _inner;

        public long AccountReads => (_inner as IStateReadStats)?.AccountReads ?? 0;
        public long StorageReads => (_inner as IStateReadStats)?.StorageReads ?? 0;
        private readonly IStateDiffStore _diffStore;
        private readonly HistoricalStateOptions _options;
        public HistoricalStateOptions Options => _options;
        private BigInteger? _currentBlockNumber;
        private BlockJournal _currentJournal;
        private long _blocksSinceLastPrune;
        private int _blocksBufferedSinceFlush;

        public IStateStore InnerStateStore => _inner;

        public IStateDiffStore DiffStore => _diffStore;

        public BigInteger? CurrentBufferedBlock => _currentBlockNumber;

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

        private ISnapFlatStateWriter InnerFlatWriter
            => _inner as ISnapFlatStateWriter
               ?? throw new NotSupportedException(
                   $"{_inner.GetType().Name} is not an ISnapFlatStateWriter; snap-flat rows written through HistoricalStateStore would be silently discarded.");

        public Task<Account> GetAccountByHashAsync(byte[] accountHash)
            => InnerFlatWriter.GetAccountByHashAsync(accountHash);

        public Task SaveAccountByHashAsync(byte[] accountHash, Account account)
            => InnerFlatWriter.SaveAccountByHashAsync(accountHash, account);

        public Task DeleteAccountByHashAsync(byte[] accountHash)
            => InnerFlatWriter.DeleteAccountByHashAsync(accountHash);

        public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value)
            => InnerFlatWriter.SaveStorageByHashAsync(accountHash, slotKeccak, value);

        public void SetCurrentBlockNumber(BigInteger blockNumber)
        {
            _currentBlockNumber = blockNumber;
            _currentJournal = new BlockJournal();
            _blocksBufferedSinceFlush++;
            (_inner as IStateWriteBuffer)?.BeginBuffering();
        }

        public async Task DrainBufferToDiskAsync()
        {
            _blocksBufferedSinceFlush = 0;
            if (_inner is IStateWriteBuffer buf)
                await buf.FlushBufferAsync().ConfigureAwait(false);
        }

        public async Task<FlatStateBatch> CaptureBufferAsync()
        {
            _blocksBufferedSinceFlush = 0;
            if (_inner is IStateWriteBuffer buf)
                return await buf.CaptureBufferAsync().ConfigureAwait(false);
            return EmptyFlatStateBatch;
        }

        private static readonly FlatStateBatch EmptyFlatStateBatch = new(null, null, null, null, null, null);

        public async Task ClearCurrentBlockNumberAsync()
        {
            await DrainBufferToDiskAsync().ConfigureAwait(false);
            await RecordBlockDiffAsync().ConfigureAwait(false);
        }

        public async Task RecordBlockDiffAsync()
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
                await _diffStore.SaveBlockDiffAsync(diff).ConfigureAwait(false);
            }

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

            _currentBlockNumber = null;
            _currentJournal = null;
        }

        public async Task RevertCurrentBlockAsync()
        {
            if (_blocksBufferedSinceFlush > 1)
            {
                throw new NotSupportedException(
                    "mid-window revert requires K=1 or 4b-iv; restart to resume from the durable cursor");
            }

            if (_inner is IStateWriteBuffer buf)
            {
                await buf.DiscardBufferAsync().ConfigureAwait(false);
                _currentBlockNumber = null;
                _currentJournal = null;
                _blocksBufferedSinceFlush = 0;
                return;
            }

            var journal = _currentJournal;
            if (journal == null)
            {
                _currentBlockNumber = null;
                _blocksBufferedSinceFlush = 0;
                return;
            }

            foreach (var kv in journal.AccountPreValues)
            {
                if (kv.Value == null)
                    await _inner.DeleteAccountAsync(kv.Key).ConfigureAwait(false);
                else
                    await _inner.SaveAccountAsync(kv.Key, kv.Value).ConfigureAwait(false);
            }

            foreach (var entry in journal.StoragePreValues.Values)
            {
                await _inner.SaveStorageByKeccakAsync(
                        entry.Address, entry.SlotKey, entry.PreValue ?? System.Array.Empty<byte>())
                    .ConfigureAwait(false);
            }

            _currentBlockNumber = null;
            _currentJournal = null;
            _blocksBufferedSinceFlush = 0;
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
                    journal.AccountPreValues[normalizedAddress] = preValue.Clone();
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
                    journal.AccountPreValues[normalizedAddress] = preValue.Clone();
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
                        Address = normalizedAddress,
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
                var allStorage = await _inner.GetAllStorageAsync(address).ConfigureAwait(false);
                foreach (var kvp in allStorage)
                {
                    var storageKey = GetStorageKey(normalizedAddress, kvp.Key);
                    if (!journal.StoragePreValues.ContainsKey(storageKey))
                    {
                        journal.StoragePreValues[storageKey] = new StorageJournalEntry
                        {
                            Address = normalizedAddress,
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

        public void ClearCache() => (_inner as IFlatCacheInvalidatable)?.ClearCache();

        private static string NormalizeAddress(string address)
        {
            return AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();
        }

        private static string GetStorageKey(string normalizedAddress, byte[] slotKey)
        {
            return $"{normalizedAddress}:{slotKey.ToHex()}";
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
        public string Address { get; set; }
        public byte[] SlotKey { get; set; }
        public byte[] PreValue { get; set; }
    }
}
