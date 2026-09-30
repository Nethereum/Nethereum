using System;
using System.Collections.Generic;

using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public sealed class InMemoryChainMetadataStore : IChainMetadataStore
    {
        private readonly object _lock = new();
        private ulong _lastBlock;
        private byte[] _lastBlockHash;
        private ulong _lastFetchedHeader;
        private ulong _lastFetchedBody;
        private ulong _receiptBackfillCursor;
        private bool _genesisLoaded;
        private readonly Dictionary<ulong, ChainCheckpoint> _checkpoints = new();
        private ulong _latestCheckpoint;
        private SnapSyncState _snapSyncState;
        private HeaderSyncState _headerSyncState;
        private readonly Dictionary<string, DeferredStorageDebt> _deferredStorageDebts = new();

        public ulong GetLastBlock() { lock (_lock) return _lastBlock; }
        public byte[] GetLastBlockHash() { lock (_lock) return _lastBlockHash; }
        public ulong GetLastFetchedHeader() { lock (_lock) return _lastFetchedHeader; }
        public ulong GetLastFetchedBody() { lock (_lock) return _lastFetchedBody; }

        public void SetLastFetchedHeader(ulong blockNumber) { lock (_lock) _lastFetchedHeader = blockNumber; }
        public void SetLastFetchedBody(ulong blockNumber) { lock (_lock) _lastFetchedBody = blockNumber; }

        public void SetLastFetchedHeaderAndBody(ulong headerBlock, ulong bodyBlock)
        {
            lock (_lock)
            {
                _lastFetchedHeader = headerBlock;
                _lastFetchedBody = bodyBlock;
            }
        }

        public ulong GetReceiptBackfillCursor() { lock (_lock) return _receiptBackfillCursor; }
        public void SetReceiptBackfillCursor(ulong blockNumber) { lock (_lock) _receiptBackfillCursor = blockNumber; }

        public void Commit(ulong lastBlock, byte[] lastBlockHash)
        {
            lock (_lock)
            {
                _lastBlock = lastBlock;
                if (lastBlockHash != null && lastBlockHash.Length == 32)
                    _lastBlockHash = lastBlockHash;
            }
        }

        private ulong _durableStateBlock;
        private byte[] _durableStateHash;

        public ulong GetDurableStateBlock() { lock (_lock) return _durableStateBlock; }

        public void CommitDurableState(ulong block, byte[] hash)
        {
            lock (_lock)
            {
                _durableStateBlock = block;
                if (hash != null && hash.Length == 32)
                    _durableStateHash = hash;
            }
        }

        public bool IsGenesisLoaded() { lock (_lock) return _genesisLoaded; }
        public void MarkGenesisLoaded() { lock (_lock) _genesisLoaded = true; }

        public void SaveCheckpoint(ulong blockNumber, byte[] stateRoot, byte[] blockHash)
        {
            if (stateRoot == null || stateRoot.Length != 32) throw new ArgumentException("stateRoot must be 32 bytes", nameof(stateRoot));
            if (blockHash == null || blockHash.Length != 32) throw new ArgumentException("blockHash must be 32 bytes", nameof(blockHash));
            lock (_lock)
            {
                _checkpoints[blockNumber] = new ChainCheckpoint(
                    blockNumber, stateRoot, blockHash,
                    (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                if (blockNumber > _latestCheckpoint) _latestCheckpoint = blockNumber;
            }
        }

        public ulong GetLatestCheckpoint() { lock (_lock) return _latestCheckpoint; }

        public ChainCheckpoint? GetCheckpoint(ulong blockNumber)
        {
            lock (_lock)
                return _checkpoints.TryGetValue(blockNumber, out var cp) ? cp : null;
        }

        public ChainCheckpoint? GetNearestCheckpointAtOrBefore(ulong upToBlock)
        {
            lock (_lock)
            {
                ulong best = 0;
                bool found = false;
                foreach (var bn in _checkpoints.Keys)
                {
                    if (bn <= upToBlock && (!found || bn > best))
                    {
                        best = bn;
                        found = true;
                    }
                }
                return found ? _checkpoints[best] : (ChainCheckpoint?)null;
            }
        }

        public ChainCheckpoint RewindToCheckpointAtOrBefore(ulong targetBlock)
        {
            lock (_lock)
            {
                var cp = GetNearestCheckpointAtOrBeforeUnlocked(targetBlock)
                    ?? throw new InvalidOperationException(
                        $"No checkpoint at or below block {targetBlock:N0}; cannot rewind.");
                _lastBlock = cp.BlockNumber;
                _lastBlockHash = cp.BlockHash;
                if (_lastFetchedHeader > cp.BlockNumber) _lastFetchedHeader = cp.BlockNumber;
                if (_lastFetchedBody > cp.BlockNumber) _lastFetchedBody = cp.BlockNumber;
                return cp;
            }
        }

        private ChainCheckpoint? GetNearestCheckpointAtOrBeforeUnlocked(ulong upToBlock)
        {
            ulong best = 0;
            bool found = false;
            foreach (var bn in _checkpoints.Keys)
            {
                if (bn <= upToBlock && (!found || bn > best))
                {
                    best = bn;
                    found = true;
                }
            }
            return found ? _checkpoints[best] : (ChainCheckpoint?)null;
        }

        public System.Collections.Generic.IReadOnlyList<ulong> ListCheckpointBlockNumbers()
        {
            lock (_lock)
            {
                var list = new List<ulong>(_checkpoints.Keys);
                list.Sort();
                return list;
            }
        }

        public void DeleteCheckpoint(ulong blockNumber)
        {
            lock (_lock)
            {
                _checkpoints.Remove(blockNumber);
                if (_latestCheckpoint == blockNumber)
                {
                    ulong newLatest = 0;
                    foreach (var bn in _checkpoints.Keys)
                        if (bn > newLatest) newLatest = bn;
                    _latestCheckpoint = newLatest;
                }
            }
        }

        public int DeleteCheckpointsAbove(ulong targetBlock)
        {
            lock (_lock)
            {
                var toRemove = new List<ulong>();
                foreach (var bn in _checkpoints.Keys)
                    if (bn > targetBlock) toRemove.Add(bn);
                foreach (var bn in toRemove) _checkpoints.Remove(bn);
                if (_latestCheckpoint > targetBlock)
                {
                    ulong newLatest = 0;
                    foreach (var bn in _checkpoints.Keys)
                        if (bn > newLatest) newLatest = bn;
                    _latestCheckpoint = newLatest;
                }
                return toRemove.Count;
            }
        }

        public void ResetForStateRebuild()
        {
            lock (_lock)
            {
                _lastBlock = 0;
                _lastBlockHash = null;
                _durableStateBlock = 0;
                _durableStateHash = null;
                _lastFetchedHeader = 0;
                _lastFetchedBody = 0;
                _genesisLoaded = false;
                _checkpoints.Clear();
                _latestCheckpoint = 0;
                _snapSyncState = null;
            }
        }

        public SnapSyncState GetSnapSyncState()
        {
            lock (_lock) return _snapSyncState;
        }

        public void SaveSnapSyncState(SnapSyncState state)
        {
            lock (_lock) _snapSyncState = state;
        }

        public void ClearSnapSyncState()
        {
            lock (_lock) _snapSyncState = null;
        }

        public void ClearCommittedHead()
        {
            lock (_lock) { _lastBlock = 0; _lastBlockHash = null; _durableStateBlock = 0; _durableStateHash = null; }
        }

        private byte[] _lightClientStateBlob;

        public byte[] GetLightClientStateBlob()
        {
            lock (_lock) return _lightClientStateBlob;
        }

        public void SaveLightClientStateBlob(byte[] blob)
        {
            if (blob == null || blob.Length == 0) throw new ArgumentException("blob required", nameof(blob));
            lock (_lock) _lightClientStateBlob = blob;
        }

        public HeaderSyncState GetHeaderSyncState()
        {
            lock (_lock) return _headerSyncState ?? HeaderSyncState.Empty;
        }

        public void SaveHeaderSyncState(HeaderSyncState state)
        {
            lock (_lock) _headerSyncState = state;
        }

        private byte[] _deferredHealAccountsBlob;

        public byte[] GetDeferredHealAccountsBlob()
        {
            lock (_lock) return _deferredHealAccountsBlob;
        }

        public void SaveDeferredHealAccountsBlob(byte[] blob)
        {
            if (blob == null || blob.Length == 0) throw new ArgumentException("blob required", nameof(blob));
            lock (_lock) _deferredHealAccountsBlob = blob;
        }

        public void ClearDeferredHealAccountsBlob()
        {
            lock (_lock) _deferredHealAccountsBlob = null;
        }

        private byte[] _deferredHealCodeBlob;

        public byte[] GetDeferredHealCodeBlob()
        {
            lock (_lock) return _deferredHealCodeBlob;
        }

        public void SaveDeferredHealCodeBlob(byte[] blob)
        {
            if (blob == null || blob.Length == 0) throw new ArgumentException("blob required", nameof(blob));
            lock (_lock) _deferredHealCodeBlob = blob;
        }

        public void ClearDeferredHealCodeBlob()
        {
            lock (_lock) _deferredHealCodeBlob = null;
        }

        public void UpsertDeferredStorageDebt(DeferredStorageDebt debt)
        {
            DeferredStorageDebt.Validate(debt);
            lock (_lock)
            {
                _deferredStorageDebts[DeferredStorageDebtKey(debt.AccountHash, debt.DiscoveredStorageRoot)] = debt.Clone();
            }
        }

        public IReadOnlyList<DeferredStorageDebt> ListOpenDeferredStorageDebts(int max = int.MaxValue)
        {
            if (max <= 0) return Array.Empty<DeferredStorageDebt>();
            lock (_lock)
            {
                var result = new List<DeferredStorageDebt>();
                foreach (var debt in _deferredStorageDebts.Values)
                {
                    if (!debt.IsOpen) continue;
                    result.Add(debt.Clone());
                    if (result.Count >= max) break;
                }
                return result;
            }
        }

        public ulong CountOpenDeferredStorageDebts()
        {
            lock (_lock)
            {
                ulong count = 0;
                foreach (var debt in _deferredStorageDebts.Values)
                    if (debt.IsOpen) count++;
                return count;
            }
        }

        public void ClearDeferredStorageDebt(byte[] accountHash, byte[] discoveredStorageRoot)
        {
            lock (_lock)
            {
                _deferredStorageDebts.Remove(DeferredStorageDebtKey(accountHash, discoveredStorageRoot));
            }
        }

        public void ClearAllDeferredStorageDebts()
        {
            lock (_lock)
            {
                _deferredStorageDebts.Clear();
            }
        }
        private static string DeferredStorageDebtKey(byte[] accountHash, byte[] discoveredStorageRoot)
        {
            if (accountHash == null || accountHash.Length != 32) throw new ArgumentException("accountHash must be 32 bytes", nameof(accountHash));
            if (discoveredStorageRoot == null || discoveredStorageRoot.Length != 32) throw new ArgumentException("discoveredStorageRoot must be 32 bytes", nameof(discoveredStorageRoot));
            return ByteUtil.Merge(accountHash, discoveredStorageRoot).ToHex();
        }
    }
}
