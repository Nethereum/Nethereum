using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    /// <summary>
    /// Buffered write scope over <see cref="IChainStoreBundle"/> that enforces
    /// the cursor-trails-data invariant: the metadata cursor (executor head,
    /// last-fetched header/body, snap-sync state row) NEVER advances ahead of
    /// the data rows it indexes. Staged writes are split into two phases by
    /// <see cref="CommitAsync"/>:
    /// <list type="number">
    /// <item><description>
    /// <b>Phase 1 — data ops.</b> <see cref="PutHeader"/>,
    /// <see cref="PutUncles"/>, <see cref="PutTransactions"/>,
    /// <see cref="PutReceipt"/> are replayed one-by-one against the underlying
    /// async stores. Each store's own write semantics apply — these ops are
    /// NOT atomic with each other on RocksDB. Per-row durability matches the
    /// un-batched write path.
    /// </description></item>
    /// <item><description>
    /// <b>Phase 2 — metadata batch.</b> Cursor advances
    /// (<see cref="SetLastFetchedHeader"/>, <see cref="SetLastFetchedBody"/>,
    /// <see cref="SetLastFetchedHeaderAndBody"/>, <see cref="Commit"/>) and
    /// <see cref="SaveSnapSyncState"/> are accumulated into a single RocksDB
    /// <c>WriteBatch</c> committed with WAL fsync. On in-memory bundles the
    /// metadata phase runs under the bundle's commit lock so readers observe
    /// either pre- or post-batch metadata.
    /// </description></item>
    /// </list>
    /// <para>
    /// <b>Crash semantics.</b> Process kill during Phase 1 leaves a subset of
    /// data rows durable (header, body, receipts may have landed individually)
    /// but the metadata cursor is unchanged — those rows are orphans, no
    /// cursor points at them. On restart the next fetch round re-fetches the
    /// same blocks; the data stores are idempotent on key so re-writes are
    /// safe. Process kill during Phase 2 leaves the metadata WriteBatch
    /// un-applied (RocksDB drops a half-written WriteBatch on WAL replay), so
    /// the cursor still does not advance and the same idempotent re-fetch
    /// path runs. The cursor-trails-data invariant suffices for resume:
    /// orphaned data rows are detectable by their absent cursor.
    /// </para>
    /// <para>
    /// Methods are NOT thread-safe — a batch is owned by one logical writer.
    /// Use a separate batch per worker if you need concurrent enqueue.
    /// </para>
    /// </summary>
    public interface IBundleBatch : IDisposable
    {
        /// <summary>
        /// Stage a block-header write (<see cref="IBlockStore.SaveAsync"/>).
        /// </summary>
        void PutHeader(BlockHeader header, byte[] blockHash);

        /// <summary>
        /// Stage uncles for <paramref name="blockHash"/>
        /// (<see cref="IUncleStore.SaveAsync"/>).
        /// </summary>
        void PutUncles(byte[] blockHash, IList<BlockHeader> uncles);

        /// <summary>
        /// Stage every transaction in <paramref name="transactions"/> under
        /// <paramref name="blockHash"/> at <paramref name="blockNumber"/>.
        /// </summary>
        void PutTransactions(
            byte[] blockHash,
            BigInteger blockNumber,
            IList<Nethereum.Model.ISignedTransaction> transactions);

        /// <summary>
        /// Stage a single receipt write
        /// (<see cref="IReceiptStore.SaveAsync"/>).
        /// </summary>
        void PutReceipt(
            Receipt receipt,
            byte[] txHash,
            byte[] blockHash,
            BigInteger blockNumber,
            int txIndex,
            BigInteger gasUsed,
            string contractAddress,
            BigInteger effectiveGasPrice);

        /// <summary>
        /// Advance the last-fetched-header cursor
        /// (<see cref="IChainMetadataStore.SetLastFetchedHeader"/>).
        /// </summary>
        void SetLastFetchedHeader(ulong blockNumber);

        /// <summary>
        /// Advance the last-fetched-body cursor
        /// (<see cref="IChainMetadataStore.SetLastFetchedBody"/>).
        /// </summary>
        void SetLastFetchedBody(ulong blockNumber);

        /// <summary>
        /// Both cursors set in one buffered op — equivalent to
        /// <see cref="SetLastFetchedHeader"/> followed by
        /// <see cref="SetLastFetchedBody"/>, but with no possibility of
        /// either reader observing the desync window.
        /// </summary>
        void SetLastFetchedHeaderAndBody(ulong headerBlock, ulong bodyBlock);

        /// <summary>
        /// Stage an executor cursor advance
        /// (<see cref="IChainMetadataStore.Commit"/>).
        /// </summary>
        void Commit(ulong lastBlock, byte[] lastBlockHash);

        /// <summary>
        /// Stage a snap-sync-state row write
        /// (<see cref="IChainMetadataStore.SaveSnapSyncState"/>).
        /// </summary>
        void SaveSnapSyncState(SnapSyncState state);

        /// <summary>
        /// Drain the batch in two phases enforcing the cursor-trails-data
        /// invariant. <b>Phase 1</b> applies every buffered data op
        /// (<see cref="PutHeader"/>, <see cref="PutUncles"/>,
        /// <see cref="PutTransactions"/>, <see cref="PutReceipt"/>) against the
        /// underlying async stores one at a time — these writes are NOT atomic
        /// with each other; each store's own write semantics apply, and a
        /// crash here may leave a partial subset of data rows durable.
        /// <b>Phase 2</b> commits every buffered metadata op
        /// (cursor advances + <see cref="SaveSnapSyncState"/>) as a single
        /// RocksDB <c>WriteBatch</c> with WAL fsync, or under the bundle's
        /// commit lock for in-memory. Either the metadata flips entirely or
        /// not at all.
        /// <para>
        /// Because Phase 2 runs after Phase 1 completes, the cursor never
        /// references rows that aren't on disk yet. Orphaned data rows from a
        /// Phase-1 crash are re-written idempotently on restart when the
        /// stale cursor triggers the same fetch round again.
        /// </para>
        /// Throws <see cref="InvalidOperationException"/> if the batch was
        /// already committed or discarded.
        /// </summary>
        Task CommitAsync(CancellationToken ct = default);

        /// <summary>
        /// Drop every buffered write without persisting anything. Idempotent
        /// after a prior commit (no-op). Safe to call from a finally block.
        /// </summary>
        void Discard();
    }
}
