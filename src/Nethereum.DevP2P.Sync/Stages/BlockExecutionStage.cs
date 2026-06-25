using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.Stages
{
    /// <summary>
    /// Executor stage: runs one block through
    /// <see cref="BlockImporter"/> and, on a root match, advances the
    /// canonical-head cursor via <see cref="IChainMetadataStore.Commit"/>.
    ///
    /// <para>
    /// State CFs commit per-tx inside <c>ImportAsync</c> (the EVM's
    /// <c>ExecutionStateService</c> flushes to <see cref="IStateStore"/> at
    /// end-of-tx). Journal pre-values commit at end-of-block via
    /// <see cref="HistoricalStateStore"/>. Each store handles its own
    /// atomicity internally — there is no cross-store transaction.
    /// </para>
    ///
    /// <para>
    /// Reusable across consumers: SyncNode mainnet replay validator,
    /// AppChain follower, audit-replay tools, future RPC-source validation
    /// runs, HA standby cold-start replay. The fetcher
    /// stages produce input triples; this stage consumes them.
    /// </para>
    /// </summary>
    public sealed class BlockExecutionStage
    {
        private readonly BlockImporter _importer;
        private readonly IChainMetadataStore _metadataStore;

        public BlockExecutionStage(
            BlockImporter importer,
            IChainMetadataStore metadataStore)
        {
            _importer = importer ?? throw new ArgumentNullException(nameof(importer));
            _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
        }

        /// <summary>
        /// Execute one block, advance the canonical-head cursor on match.
        /// On mismatch the cursor stays at the prior block; state CFs may
        /// contain partial writes from the failed run, which the caller
        /// handles via journal-rewind or snapshot restore.
        /// </summary>
        public async Task<BlockExecutionOutcome> ExecuteOneAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader> uncles,
            IList<WithdrawalEntry> withdrawals,
            byte[] blockHash,
            CancellationToken ct)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (blockHash == null || blockHash.Length != 32)
                throw new ArgumentException("blockHash must be exactly 32 bytes (Keccak-256 digest).", nameof(blockHash));

            var result = await _importer.ImportAsync(
                header,
                transactions ?? new List<ISignedTransaction>(),
                uncles ?? new List<BlockHeader>(),
                withdrawals,
                ct).ConfigureAwait(false);

            if (result.RootMatches)
            {
                _metadataStore.Commit((ulong)header.BlockNumber, blockHash);
                return new BlockExecutionOutcome(
                    Result: result,
                    BlockNumber: (ulong)header.BlockNumber,
                    BlockHash: blockHash,
                    Committed: true);
            }

            return new BlockExecutionOutcome(
                Result: result,
                BlockNumber: (ulong)header.BlockNumber,
                BlockHash: blockHash,
                Committed: false);
        }
    }

    /// <summary>
    /// Per-block result the caller inspects to drive stats, fixture
    /// emission, auto-rewind, divergence diagnosis, etc.
    /// </summary>
    public sealed record BlockExecutionOutcome(
        BlockImporterResult Result,
        ulong BlockNumber,
        byte[] BlockHash,
        bool Committed);
}
