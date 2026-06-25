using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB
{
    /// <summary>
    /// RocksDB-backed <see cref="IBundleBatch"/>. Data writes (headers, bodies,
    /// receipts, uncles, transactions) buffer as deferred awaitables against
    /// the bundle's existing stores; metadata writes (cursors, snap-state
    /// rows) buffer into a single <see cref="WriteBatch"/> committed atomically
    /// with WAL fsync at <see cref="CommitAsync"/>. The metadata write is
    /// always the last operation of the batch, enforcing the invariant that
    /// the cursor never advances ahead of the data it references.
    /// <para>
    /// On process kill before <see cref="CommitAsync"/> returns, the metadata
    /// row is unchanged and any data writes that did land are orphaned (no
    /// cursor points at them). On kill after <see cref="CommitAsync"/>
    /// returns, both metadata and data are durable.
    /// </para>
    /// </summary>
    internal sealed class RocksDbBundleBatch : IBundleBatch
    {
        private readonly IChainStoreBundle _bundle;
        private readonly RocksDbManager _rocks;
        private readonly ISnapSyncStateEncoder _snapSyncEncoder;
        private readonly bool _syncFsync;

        // Data-store operations replayed against the existing async stores.
        // Receipts + uncles + transactions land before the metadata WriteBatch.
        private readonly List<Func<Task>> _dataOps = new();

        // Metadata operations buffered into a real WriteBatch committed last,
        // atomically with WAL fsync.
        private readonly List<Action<WriteBatch, ColumnFamilyHandle>> _metaOps = new();

        private bool _completed;

        public RocksDbBundleBatch(
            IChainStoreBundle bundle,
            RocksDbManager rocks,
            ISnapSyncStateEncoder snapSyncEncoder,
            bool syncFsync)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _rocks = rocks ?? throw new ArgumentNullException(nameof(rocks));
            _snapSyncEncoder = snapSyncEncoder ?? SnapSyncStateRlpEncoder.Instance;
            _syncFsync = syncFsync;
        }

        public void PutHeader(BlockHeader header, byte[] blockHash)
        {
            EnsureOpen();
            _dataOps.Add(() => _bundle.Blocks.SaveAsync(header, blockHash));
        }

        public void PutUncles(byte[] blockHash, IList<BlockHeader> uncles)
        {
            EnsureOpen();
            _dataOps.Add(() => _bundle.Uncles.SaveAsync(blockHash, uncles ?? new List<BlockHeader>()));
        }

        public void PutTransactions(
            byte[] blockHash,
            BigInteger blockNumber,
            IList<ISignedTransaction> transactions)
        {
            EnsureOpen();
            if (transactions == null) return;
            for (int i = 0; i < transactions.Count; i++)
            {
                var tx = transactions[i];
                int index = i;
                _dataOps.Add(() => _bundle.Transactions.SaveAsync(tx, blockHash, index, blockNumber));
            }
        }

        public void PutReceipt(
            Receipt receipt,
            byte[] txHash,
            byte[] blockHash,
            BigInteger blockNumber,
            int txIndex,
            BigInteger gasUsed,
            string contractAddress,
            BigInteger effectiveGasPrice)
        {
            EnsureOpen();
            _dataOps.Add(() => _bundle.Receipts.SaveAsync(
                receipt, txHash, blockHash, blockNumber, txIndex,
                gasUsed, contractAddress, effectiveGasPrice));
        }

        public void SetLastFetchedHeader(ulong blockNumber)
        {
            EnsureOpen();
            _metaOps.Add((batch, cf) =>
                batch.Put(RocksDbChainMetadataStore.MetaKeys.LastFetchedHeader, RocksDbManager.Write64BE(blockNumber), cf));
        }

        public void SetLastFetchedBody(ulong blockNumber)
        {
            EnsureOpen();
            _metaOps.Add((batch, cf) =>
                batch.Put(RocksDbChainMetadataStore.MetaKeys.LastFetchedBody, RocksDbManager.Write64BE(blockNumber), cf));
        }

        public void SetLastFetchedHeaderAndBody(ulong headerBlock, ulong bodyBlock)
        {
            EnsureOpen();
            _metaOps.Add((batch, cf) =>
            {
                batch.Put(RocksDbChainMetadataStore.MetaKeys.LastFetchedHeader, RocksDbManager.Write64BE(headerBlock), cf);
                batch.Put(RocksDbChainMetadataStore.MetaKeys.LastFetchedBody, RocksDbManager.Write64BE(bodyBlock), cf);
            });
        }

        public void Commit(ulong lastBlock, byte[] lastBlockHash)
        {
            EnsureOpen();
            _metaOps.Add((batch, cf) =>
            {
                batch.Put(RocksDbChainMetadataStore.MetaKeys.LastBlock, RocksDbManager.Write64BE(lastBlock), cf);
                if (lastBlockHash != null && lastBlockHash.Length == 32)
                    batch.Put(RocksDbChainMetadataStore.MetaKeys.LastBlockHash, lastBlockHash, cf);
            });
        }

        public void SaveSnapSyncState(SnapSyncState state)
        {
            EnsureOpen();
            if (state == null) throw new ArgumentNullException(nameof(state));
            var blob = _snapSyncEncoder.Encode(state);
            _metaOps.Add((batch, cf) =>
                batch.Put(RocksDbChainMetadataStore.MetaKeys.SnapSyncState, blob, cf));
        }

        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (_completed) throw new InvalidOperationException("Bundle batch already completed.");
            ct.ThrowIfCancellationRequested();

            // Phase 1 — drain data writes. These land via the existing async
            // stores' WAL; per-row durability matches the un-batched path.
            // If we crash mid-phase, the metadata cursor is unchanged and the
            // partial data rows are orphaned (no cursor points at them).
            foreach (var op in _dataOps)
            {
                ct.ThrowIfCancellationRequested();
                await op().ConfigureAwait(false);
            }

            // Phase 2 — metadata WriteBatch lands atomically with WAL fsync
            // when _syncFsync is true. This is the moment the batch becomes
            // visible: cursor + snap-state row + checkpoints either all flip
            // or none do.
            if (_metaOps.Count > 0)
            {
                var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
                using var batch = _rocks.CreateWriteBatch();
                foreach (var op in _metaOps) op(batch, cf);
                var opts = _syncFsync ? new WriteOptions().SetSync(true) : null;
                _rocks.Write(batch, opts);
            }

            _dataOps.Clear();
            _metaOps.Clear();
            _completed = true;
        }

        public void Discard()
        {
            _dataOps.Clear();
            _metaOps.Clear();
            _completed = true;
        }

        public void Dispose()
        {
            if (!_completed) Discard();
        }

        private void EnsureOpen()
        {
            if (_completed) throw new InvalidOperationException("Bundle batch already committed or discarded.");
        }
    }
}
