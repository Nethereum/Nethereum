using System;
using System.Collections.Generic;

namespace Nethereum.CoreChain.Storage.InMemory
{
    /// <summary>
    /// Volatile <see cref="IChainMetadataStore"/> — for tests, smoke runs, and
    /// any consumer that doesn't need durable resume/checkpoint state. Mutating
    /// operations are serialised via an internal lock so the contract matches
    /// the RocksDB implementation's WriteBatch atomicity. Without this, tests
    /// that pass on InMemory could mask race conditions that would surface on
    /// the RocksDB store.
    /// </summary>
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
                // Anything past the rewind target is on a potentially-wrong fork
                // and must be re-fetched. Clamp the pipeline cursors down.
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

        public HeaderSyncState GetHeaderSyncState()
        {
            lock (_lock) return _headerSyncState ?? HeaderSyncState.Empty;
        }

        public void SaveHeaderSyncState(HeaderSyncState state)
        {
            lock (_lock) _headerSyncState = state;
        }
    }
}
