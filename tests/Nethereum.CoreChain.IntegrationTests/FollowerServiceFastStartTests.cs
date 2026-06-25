using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests
{
    /// <summary>
    /// D-3 — FollowerService fast-start must re-read the committed-block
    /// cursor in the same tight window as the snap-state read. Reading
    /// lastCommittedBlock at the top of RunAsync and then comparing it to
    /// the snapState pivot later is racy when the bootstrapper finishes
    /// between the two reads.
    /// </summary>
    public sealed class FollowerServiceFastStartTests
    {
        private static byte[] Fill(byte v) => Enumerable.Repeat(v, 32).ToArray();

        private static SnapSyncState BuildSnapState(SnapPhase phase, ulong pivotBlock)
            => new SnapSyncState
            {
                SchemaVersion = 1,
                Phase = phase,
                PivotBlockNumber = pivotBlock,
                PivotBlockHash = Fill((byte)(pivotBlock & 0xFF)),
                HealTargetRoot = new byte[32],
                Tasks = new List<SnapSyncAccountTask>(),
                Counters = SnapSyncCounters.Zero,
            };

        private sealed class NoopPolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong blockNumber) => false;
            public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber) => ValidationAction.Continue;
        }

        private sealed class ScriptedExecutor : IBlockExecutor
        {
            public int CallCount;
            public Task<BlockImporterResult> ProcessBlockAsync(
                BlockHeader header,
                IList<ISignedTransaction> transactions,
                IList<BlockHeader> uncles,
                IList<WithdrawalEntry> withdrawals,
                CancellationToken ct)
            {
                Interlocked.Increment(ref CallCount);
                return Task.FromResult(new BlockImporterResult
                {
                    ComputedStateRoot = header.StateRoot,
                    ExpectedStateRoot = header.StateRoot,
                    StateRootMismatch = false,
                });
            }
        }

        /// <summary>
        /// Bundle wrapper that injects a side-effect on the first GetSnapSyncState
        /// read — between the top-of-RunAsync GetLastBlock and the snap-state
        /// read, the underlying metadata is mutated to bump LastBlock past
        /// the pivot. This simulates the bootstrapper writing
        /// Phase=Complete + Metadata.Commit(pivot) atomically while the
        /// FollowerService is mid-startup.
        /// </summary>
        private sealed class RacingBundle : IChainStoreBundle
        {
            private readonly IChainStoreBundle _inner;
            private readonly Action _onFirstSnapStateRead;
            private readonly Lazy<RacingMetadata> _metadata;
            private int _fired;

            public RacingBundle(IChainStoreBundle inner, Action onFirstSnapStateRead)
            {
                _inner = inner;
                _onFirstSnapStateRead = onFirstSnapStateRead;
                _metadata = new Lazy<RacingMetadata>(
                    () => new RacingMetadata(_inner.Metadata, OnSnapRead));
            }

            public IStateStore         State        => _inner.State;
            public ITrieNodeStore      TrieNodes    => _inner.TrieNodes;
            public IBlockStore         Blocks       => _inner.Blocks;
            public ITransactionStore   Transactions => _inner.Transactions;
            public IUncleStore         Uncles       => _inner.Uncles;
            public IWithdrawalStore    Withdrawals  => _inner.Withdrawals;
            public IReceiptStore       Receipts     => _inner.Receipts;
            public ILogStore           Logs         => _inner.Logs;
            public IChainMetadataStore Metadata     => _metadata.Value;
            public IStateDiffStore     Diffs        => _inner.Diffs;
            public bool                JournalEnabled => _inner.JournalEnabled;

            public Task<ChainCheckpoint> SaveCheckpointAsync(ulong blockNumber, byte[] stateRoot, byte[] blockHash, CancellationToken ct = default)
                => _inner.SaveCheckpointAsync(blockNumber, stateRoot, blockHash, ct);
            public Task<IReadOnlyList<ChainCheckpoint>> ListCheckpointsAsync(CancellationToken ct = default)
                => _inner.ListCheckpointsAsync(ct);
            public Task RestoreCheckpointAsync(ulong blockNumber, CancellationToken ct = default)
                => _inner.RestoreCheckpointAsync(blockNumber, ct);
            public Task DeleteCheckpointAsync(ulong blockNumber, CancellationToken ct = default)
                => _inner.DeleteCheckpointAsync(blockNumber, ct);
            public Task ResetStateOnlyAsync(CancellationToken ct = default) => _inner.ResetStateOnlyAsync(ct);
            public Task ResetSnapBootstrapStateAsync(CancellationToken ct = default) => _inner.ResetSnapBootstrapStateAsync(ct);
            public string ResolveCheckpointSnapshotPath(ulong blockNumber) => _inner.ResolveCheckpointSnapshotPath(blockNumber);
            public Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default) => _inner.ExportDatabaseAsync(outputPath, ct);
            public IBundleBatch BeginBatch() => _inner.BeginBatch();
            public ValueTask DisposeAsync() => _inner.DisposeAsync();
            public void Dispose() => _inner.Dispose();

            private void OnSnapRead()
            {
                if (Interlocked.Exchange(ref _fired, 1) == 0)
                    _onFirstSnapStateRead();
            }
        }

        private sealed class RacingMetadata : IChainMetadataStore
        {
            private readonly IChainMetadataStore _inner;
            private readonly Action _onSnapRead;

            public RacingMetadata(IChainMetadataStore inner, Action onSnapRead)
            {
                _inner = inner;
                _onSnapRead = onSnapRead;
            }

            public SnapSyncState GetSnapSyncState()
            {
                _onSnapRead();
                return _inner.GetSnapSyncState();
            }

            public void SaveSnapSyncState(SnapSyncState state) => _inner.SaveSnapSyncState(state);
            public void ClearSnapSyncState() => _inner.ClearSnapSyncState();
            public HeaderSyncState GetHeaderSyncState() => _inner.GetHeaderSyncState();
            public void SaveHeaderSyncState(HeaderSyncState state) => _inner.SaveHeaderSyncState(state);

            public ulong GetLastBlock() => _inner.GetLastBlock();
            public byte[] GetLastBlockHash() => _inner.GetLastBlockHash();
            public void Commit(ulong block, byte[] hash) => _inner.Commit(block, hash);

            public ulong GetLastFetchedHeader() => _inner.GetLastFetchedHeader();
            public ulong GetLastFetchedBody() => _inner.GetLastFetchedBody();
            public void SetLastFetchedHeader(ulong block) => _inner.SetLastFetchedHeader(block);
            public void SetLastFetchedBody(ulong block) => _inner.SetLastFetchedBody(block);
            public void SetLastFetchedHeaderAndBody(ulong h, ulong b) => _inner.SetLastFetchedHeaderAndBody(h, b);

            public ulong GetReceiptBackfillCursor() => _inner.GetReceiptBackfillCursor();
            public void SetReceiptBackfillCursor(ulong block) => _inner.SetReceiptBackfillCursor(block);

            public bool IsGenesisLoaded() => _inner.IsGenesisLoaded();
            public void MarkGenesisLoaded() => _inner.MarkGenesisLoaded();

            public void SaveCheckpoint(ulong block, byte[] stateRoot, byte[] blockHash) => _inner.SaveCheckpoint(block, stateRoot, blockHash);
            public ulong GetLatestCheckpoint() => _inner.GetLatestCheckpoint();
            public ChainCheckpoint? GetCheckpoint(ulong block) => _inner.GetCheckpoint(block);
            public ChainCheckpoint? GetNearestCheckpointAtOrBefore(ulong block) => _inner.GetNearestCheckpointAtOrBefore(block);
            public ChainCheckpoint RewindToCheckpointAtOrBefore(ulong target) => _inner.RewindToCheckpointAtOrBefore(target);
            public IReadOnlyList<ulong> ListCheckpointBlockNumbers() => _inner.ListCheckpointBlockNumbers();
            public void DeleteCheckpoint(ulong block) => _inner.DeleteCheckpoint(block);
            public int DeleteCheckpointsAbove(ulong target) => _inner.DeleteCheckpointsAbove(target);

            public void ResetForStateRebuild() => _inner.ResetForStateRebuild();
        }

        [Fact]
        public async Task FastStart_UsesFreshLastBlockRead_NotStaleCapture()
        {
            // Bootstrapper races: LastBlock starts at 0, snap-state shows
            // Complete @ pivot=50. Just before FollowerService reads the
            // snap-state, the bootstrapper "commits" — bumps LastBlock to 50.
            // Without the D-3 fix, the comparison is against the stale
            // lastCommittedBlock = 0, so fast-start fires unnecessarily and
            // currentStart jumps to pivot+1. With the fix, the fresh
            // GetLastBlock() == 50 means pivot is no longer > lastBlock, so
            // fast-start is correctly skipped.
            var inner = InMemoryChainStoreBundle.Open();
            inner.Metadata.SaveSnapSyncState(BuildSnapState(SnapPhase.Complete, pivotBlock: 50));

            var bundle = new RacingBundle(inner, onFirstSnapStateRead: () =>
            {
                inner.Metadata.Commit(50UL, Fill(50));
            });

            var executor = new ScriptedExecutor();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var follower = new FollowerService();

            var options = new FollowerOptions(
                StartBlock: 1,
                CheckpointEvery: 0,
                AnchorEvery: 0,
                EndBlock: 50);

            var result = await follower.RunAsync(
                new LocalReplayBlockSource(new List<BlockBundle>()),
                bundleFactory: () => bundle,
                executorFactory: _ => executor,
                policy: new NoopPolicy(),
                canonical: null!,
                options: options,
                ct: cts.Token,
                logger: NullLogger.Instance);

            // With the fresh-read fix in place, the bootstrapper's mid-startup
            // commit is observed and fast-start is suppressed — the result
            // reports the freshly-committed block 50.
            Assert.Equal(50UL, result.LastExecutedBlock);
        }

        [Fact]
        public async Task FastStart_FiresWhenSnapPivotGenuinelyAhead()
        {
            // Sanity-check that the fix doesn't break the normal fast-start
            // path: snap-state Complete @ pivot=100, LastBlock unchanged at 0
            // throughout. Fast-start MUST still fire so the follower starts
            // executing post-pivot rather than re-running blocks 1..100.
            var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.SaveSnapSyncState(BuildSnapState(SnapPhase.Complete, pivotBlock: 100));

            var executor = new ScriptedExecutor();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var follower = new FollowerService();

            var options = new FollowerOptions(
                StartBlock: 1,
                CheckpointEvery: 0,
                AnchorEvery: 0,
                EndBlock: 100);

            var result = await follower.RunAsync(
                new LocalReplayBlockSource(new List<BlockBundle>()),
                bundleFactory: () => bundle,
                executorFactory: _ => executor,
                policy: new NoopPolicy(),
                canonical: null!,
                options: options,
                ct: cts.Token,
                logger: NullLogger.Instance);

            // pivot is genuinely past lastBlock — fast-start should snap the
            // cursor to pivot.
            Assert.Equal(100UL, result.LastExecutedBlock);
            Assert.Equal(0, executor.CallCount);
        }
    }
}
