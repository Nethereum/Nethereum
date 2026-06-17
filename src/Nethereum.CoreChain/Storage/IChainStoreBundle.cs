using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Storage
{
    /// <summary>
    /// Typed composition over the chain's persisted stores plus the atomic
    /// operations a node performs across them. Implementations bind the
    /// accessors to a specific backend (RocksDB, in-memory, etc.); callers
    /// receive a single bundle and never reach into backend-specific types.
    /// <para>
    /// <see cref="SaveCheckpointAsync"/> is the only path that writes a
    /// checkpoint — it pairs the metadata row with the on-disk snapshot
    /// atomically. There is deliberately no API exposed for writing one
    /// without the other.
    /// </para>
    /// </summary>
    public interface IChainStoreBundle : IAsyncDisposable, IDisposable
    {
        IStateStore         State        { get; }
        ITrieNodeStore      TrieNodes    { get; }
        IBlockStore         Blocks       { get; }
        ITransactionStore   Transactions { get; }
        IUncleStore         Uncles       { get; }
        IReceiptStore       Receipts     { get; }
        ILogStore           Logs         { get; }
        IChainMetadataStore Metadata     { get; }
        IStateDiffStore     Diffs        { get; }

        /// <summary>
        /// True when <see cref="State"/> is wrapped in
        /// <see cref="HistoricalStateStore"/> and per-block diffs are being
        /// captured. Read-only after construction.
        /// </summary>
        bool JournalEnabled { get; }

        /// <summary>
        /// Atomically persist a checkpoint pairing the metadata row
        /// (<see cref="IChainMetadataStore.SaveCheckpoint"/>) with a
        /// hard-linked snapshot of the underlying database. If snapshot
        /// creation fails, no metadata row is written. If metadata
        /// persistence fails after the snapshot is staged, the snapshot
        /// directory is rolled back. Returns the persisted checkpoint.
        /// </summary>
        Task<ChainCheckpoint> SaveCheckpointAsync(
            ulong blockNumber,
            byte[] stateRoot,
            byte[] blockHash,
            CancellationToken ct = default);

        /// <summary>
        /// Every checkpoint with both a metadata row and an on-disk snapshot,
        /// ascending. Orphaned metadata (no snapshot) and orphaned snapshots
        /// (no metadata) are filtered out — the bundle reports only
        /// checkpoints that can actually be restored.
        /// </summary>
        Task<IReadOnlyList<ChainCheckpoint>> ListCheckpointsAsync(
            CancellationToken ct = default);

        /// <summary>
        /// Replace the current database contents with the snapshot taken at
        /// <paramref name="blockNumber"/>. Caller must stop all writers
        /// before calling. Throws when no usable checkpoint exists at
        /// <paramref name="blockNumber"/>.
        /// </summary>
        Task RestoreCheckpointAsync(
            ulong blockNumber,
            CancellationToken ct = default);

        /// <summary>
        /// Delete both the metadata row and the on-disk snapshot for
        /// <paramref name="blockNumber"/>. No-op when neither exists.
        /// </summary>
        Task DeleteCheckpointAsync(
            ulong blockNumber,
            CancellationToken ct = default);

        /// <summary>
        /// Wipe state, trie, receipts, logs, blooms, state-history and
        /// related artefacts; clear the metadata cursor and every recorded
        /// checkpoint. Headers, transactions and uncles are preserved so a
        /// subsequent re-execution can rebuild state from purely-local chain
        /// data. Used to recover from EVM divergence without re-fetching
        /// blocks over the network.
        /// </summary>
        Task ResetStateOnlyAsync(CancellationToken ct = default);

        /// <summary>
        /// Absolute path the bundle uses for the snapshot of
        /// <paramref name="blockNumber"/>. Returned whether the snapshot
        /// currently exists or not — operators use it for logging and
        /// out-of-band tooling.
        /// </summary>
        string ResolveCheckpointSnapshotPath(ulong blockNumber);

        /// <summary>
        /// Export a hard-linked copy of the live database to
        /// <paramref name="outputPath"/>. <paramref name="outputPath"/> must
        /// not exist. Used by backup and forensic-replay tooling.
        /// </summary>
        Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default);
    }
}
