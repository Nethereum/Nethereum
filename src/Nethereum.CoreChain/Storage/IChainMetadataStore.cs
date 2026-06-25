using System;

namespace Nethereum.CoreChain.Storage
{
    /// <summary>
    /// Sync-pipeline and audit metadata for a chain (mainnet replay,
    /// AppChain follower, audit-replay tools, wallet history rebuild,
    /// future Engine-API EL). NOT tied to DevP2P — every consumer that
    /// replays blocks into state needs the same primitives:
    /// <list type="bullet">
    ///   <item>Resume-from-last-committed-block (sync loop survives restart).</item>
    ///   <item>"Did we already seed genesis?" (idempotent bootstrap).</item>
    ///   <item>Audit checkpoints (block N, stateRoot, blockHash) so future
    ///     replays can verify agreement with canonical without re-validating
    ///     every earlier block.</item>
    /// </list>
    /// Implementations: in-memory (volatile, tests/smoke runs) and RocksDB
    /// (persistent, resumable).
    /// </summary>
    public interface IChainMetadataStore
    {
        /// <summary>
        /// Highest block number whose post-state has been durably committed
        /// (0 = nothing past genesis). Resume picks up at <c>GetLastBlock() + 1</c>.
        /// </summary>
        ulong GetLastBlock();

        /// <summary>32-byte hash of the last committed block, or null.</summary>
        byte[] GetLastBlockHash();

        /// <summary>
        /// Atomically write last-block + last-block-hash and flush durably.
        /// Call at the end of every flush window (e.g. every N blocks) and on
        /// graceful shutdown.
        /// </summary>
        void Commit(ulong lastBlock, byte[] lastBlockHash);

        /// <summary>
        /// Highest block number whose <em>header</em> has been downloaded
        /// and durably written to <see cref="IBlockStore"/> (0 = nothing
        /// past genesis). Always &gt;= <see cref="GetLastBlock"/> when the
        /// pipelined sync is healthy — the HeaderFetcher stage runs ahead of
        /// the BodyFetcher and Executor. Resume at startup reads this to
        /// know where to restart header download.
        /// </summary>
        ulong GetLastFetchedHeader();

        /// <summary>
        /// Atomically advance (or set) the last-fetched-header cursor. Called
        /// by the HeaderFetcher stage at the end of each persisted batch.
        /// </summary>
        void SetLastFetchedHeader(ulong blockNumber);

        /// <summary>
        /// Highest block number whose <em>body</em> (transactions + uncles
        /// + withdrawals) has been downloaded and durably written to
        /// <see cref="ITransactionStore"/> / <see cref="IUncleStore"/> (0 =
        /// nothing past genesis). Always &lt;= <see cref="GetLastFetchedHeader"/>
        /// (you need the header's txRoot/uncleHash to validate the body)
        /// and always &gt;= <see cref="GetLastBlock"/> (executor runs after
        /// body fetch). Resume reads this to restart body download.
        /// </summary>
        ulong GetLastFetchedBody();

        /// <summary>
        /// Atomically advance (or set) the last-fetched-body cursor. Called
        /// by the BodyFetcher stage at the end of each persisted batch.
        /// </summary>
        void SetLastFetchedBody(ulong blockNumber);

        /// <summary>
        /// Advance both cursors in one atomic write. Equivalent to
        /// <see cref="SetLastFetchedHeader"/> followed by
        /// <see cref="SetLastFetchedBody"/> but with no possibility of an
        /// observer (or a kill-and-restart) seeing one cursor advanced and
        /// the other not. Used by the post-Phase-1 fast path where header
        /// and body arrived together in the same backfill batch.
        /// </summary>
        void SetLastFetchedHeaderAndBody(ulong headerBlock, ulong bodyBlock);

        /// <summary>
        /// Highest block number whose receipts have been validated + persisted
        /// via the post-sync <c>ReceiptBackfillService</c> scrub job (0 = never
        /// run, or starts from genesis). The scrub re-fetches receipts via
        /// DevP2P, validates against <c>header.ReceiptHash</c>, and overwrites
        /// any pre-existing entries with correctly-computed metadata
        /// (<c>contractAddress</c>, <c>gasUsed</c>, <c>effectiveGasPrice</c>).
        /// Independent from <see cref="GetLastFetchedHeader"/> /
        /// <see cref="GetLastFetchedBody"/> — those track the initial sync
        /// fetcher; this tracks the receipt-scrub catch-up.
        /// </summary>
        ulong GetReceiptBackfillCursor();

        /// <summary>
        /// Atomically advance the receipt-backfill cursor. Called by
        /// <c>ReceiptBackfillService</c> after each successfully-persisted
        /// block range.
        /// </summary>
        void SetReceiptBackfillCursor(ulong blockNumber);

        /// <summary>True after <see cref="MarkGenesisLoaded"/> has been called.</summary>
        bool IsGenesisLoaded();

        /// <summary>Idempotent flag — set after the genesis alloc has been seeded.</summary>
        void MarkGenesisLoaded();

        /// <summary>
        /// Persist an audit checkpoint stamping <paramref name="stateRoot"/> and
        /// <paramref name="blockHash"/> at <paramref name="blockNumber"/>.
        /// Future tools (audit replay, AppChain anchoring proof producers,
        /// wallet history rebuild) can read it back via
        /// <see cref="GetCheckpoint"/> and verify byte-identity without
        /// replaying earlier history.
        /// </summary>
        void SaveCheckpoint(ulong blockNumber, byte[] stateRoot, byte[] blockHash);

        /// <summary>
        /// Highest block number for which a checkpoint exists (0 = none).
        /// </summary>
        ulong GetLatestCheckpoint();

        /// <summary>
        /// Read back a previously-saved checkpoint, or null if no checkpoint
        /// was saved at <paramref name="blockNumber"/>.
        /// </summary>
        ChainCheckpoint? GetCheckpoint(ulong blockNumber);

        /// <summary>
        /// Highest checkpoint at or below <paramref name="upToBlock"/>, or null
        /// if none exists. Powers rewind/time-travel: callers ask "give me the
        /// most recent durable state before block N" without scanning all
        /// recorded checkpoints.
        /// </summary>
        ChainCheckpoint? GetNearestCheckpointAtOrBefore(ulong upToBlock);

        /// <summary>
        /// Atomic rewind: rolls <see cref="GetLastBlock"/> back to the latest
        /// checkpoint at or below <paramref name="targetBlock"/>, so subsequent
        /// resume picks up at <c>checkpoint.BlockNumber + 1</c>. Underlying
        /// trie nodes are NOT deleted — this assumes they are still present in
        /// the state store (true when checkpoints record stateRoots actively
        /// referenced by the trie). Also clamps
        /// <see cref="GetLastFetchedHeader"/> and
        /// <see cref="GetLastFetchedBody"/> down to the rewind target if they
        /// are higher — anything past the rewind block is on a potentially
        /// wrong fork and must be re-fetched. Throws if no checkpoint exists
        /// at or below <paramref name="targetBlock"/>. Returns the checkpoint
        /// rewound to. Core platform primitive — enables forensics, audit
        /// replay, eth_call at historical block, and HA standby state
        /// verification.
        /// </summary>
        ChainCheckpoint RewindToCheckpointAtOrBefore(ulong targetBlock);

        /// <summary>
        /// Enumerate every persisted checkpoint block number, ascending.
        /// Used by selective-pruning tools that keep coarse anchors (e.g.
        /// every 1M blocks) and drop fine ones to free space — with full
        /// chain data, any state can be rebuilt by re-executing from the
        /// nearest remaining checkpoint, so fine-grained checkpoints are a
        /// speed cache, not a source of truth.
        /// </summary>
        System.Collections.Generic.IReadOnlyList<ulong> ListCheckpointBlockNumbers();

        /// <summary>
        /// Drop the checkpoint at <paramref name="blockNumber"/>. No-op if no
        /// checkpoint was recorded there.
        /// </summary>
        void DeleteCheckpoint(ulong blockNumber);

        /// <summary>
        /// Drop every checkpoint strictly above <paramref name="targetBlock"/>
        /// and return the count removed. Used by rewind paths after a
        /// state-root divergence or canonical-confirmed reorg: checkpoints
        /// recorded above the rewind target reference a potentially-wrong
        /// fork's state root, so <see cref="GetNearestCheckpointAtOrBefore"/>
        /// must not be able to return them on a subsequent rewind attempt.
        /// Idempotent; safe to call repeatedly. Returns 0 when nothing was
        /// removed.
        /// </summary>
        int DeleteCheckpointsAbove(ulong targetBlock);

        /// <summary>
        /// Reset metadata for a state-rebuild cycle: clear last_block,
        /// last_block_hash, genesis_loaded, and every recorded checkpoint.
        /// Headers + transactions + uncles in the other stores are untouched.
        /// Pair with state-store CF wiping and a subsequent
        /// <c>--re-execute-from 1</c> run to rebuild the state trie on a
        /// fixed EVM from purely-local chain data.
        /// </summary>
        void ResetForStateRebuild();

        SnapSyncState GetSnapSyncState();
        void SaveSnapSyncState(SnapSyncState state);
        void ClearSnapSyncState();
    }

    /// <summary>
    /// A stamped (blockNumber, stateRoot, blockHash, unix-timestamp) tuple
    /// recorded at sync time and consumed at audit/replay time.
    /// </summary>
    public readonly struct ChainCheckpoint
    {
        public ulong BlockNumber { get; }
        public byte[] StateRoot { get; }
        public byte[] BlockHash { get; }
        public ulong UnixTimestamp { get; }

        public ChainCheckpoint(ulong blockNumber, byte[] stateRoot, byte[] blockHash, ulong unixTimestamp)
        {
            BlockNumber = blockNumber;
            StateRoot = stateRoot ?? throw new ArgumentNullException(nameof(stateRoot));
            BlockHash = blockHash ?? throw new ArgumentNullException(nameof(blockHash));
            UnixTimestamp = unixTimestamp;
        }
    }
}
