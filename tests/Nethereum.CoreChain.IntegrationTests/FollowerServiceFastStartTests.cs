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
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.IntegrationTests
{
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
                IList<Withdrawal> withdrawals,
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
            public Nethereum.Merkle.Patricia.Storage.ITrieNodeStore StateTrieNodes => _inner.StateTrieNodes;
            public NodeCommitBlockContext NodeCommitBlockSource => _inner.NodeCommitBlockSource;
            public IBlockStore         Blocks       => _inner.Blocks;
            public ITransactionStore   Transactions => _inner.Transactions;
            public IUncleStore         Uncles       => _inner.Uncles;
            public IWithdrawalStore    Withdrawals  => _inner.Withdrawals;
            public IBlockAccessListStore BlockAccessLists => _inner.BlockAccessLists;
            public IReceiptStore       Receipts     => _inner.Receipts;
            public ILogStore           Logs         => _inner.Logs;
            public IChainMetadataStore Metadata     => _metadata.Value;
            public IStateDiffStore     Diffs        => _inner.Diffs;
            public bool                JournalEnabled => _inner.JournalEnabled;
            public long FreezerHead => _inner.FreezerHead;
            public long ByHashIndexedHead => _inner.ByHashIndexedHead;
            public long LogIndexRenderedHead => _inner.LogIndexRenderedHead;
            public long LogRenderProgressBlock => _inner.LogRenderProgressBlock;

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
            public void ClearCommittedHead() => _inner.ClearCommittedHead();
            public HeaderSyncState GetHeaderSyncState() => _inner.GetHeaderSyncState();
            public void SaveHeaderSyncState(HeaderSyncState state) => _inner.SaveHeaderSyncState(state);

            public ulong GetLastBlock() => _inner.GetLastBlock();
            public byte[] GetLastBlockHash() => _inner.GetLastBlockHash();
            public void Commit(ulong block, byte[] hash) => _inner.Commit(block, hash);
            public ulong GetDurableStateBlock() => _inner.GetDurableStateBlock();
            public void CommitDurableState(ulong block, byte[] hash) => _inner.CommitDurableState(block, hash);

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

            public byte[] GetLightClientStateBlob() => _inner.GetLightClientStateBlob();
            public void SaveLightClientStateBlob(byte[] blob) => _inner.SaveLightClientStateBlob(blob);

            public byte[] GetDeferredHealAccountsBlob() => _inner.GetDeferredHealAccountsBlob();
            public void SaveDeferredHealAccountsBlob(byte[] blob) => _inner.SaveDeferredHealAccountsBlob(blob);
            public void ClearDeferredHealAccountsBlob() => _inner.ClearDeferredHealAccountsBlob();
            public byte[] GetDeferredHealCodeBlob() => _inner.GetDeferredHealCodeBlob();
            public void SaveDeferredHealCodeBlob(byte[] blob) => _inner.SaveDeferredHealCodeBlob(blob);
            public void ClearDeferredHealCodeBlob() => _inner.ClearDeferredHealCodeBlob();

            public void UpsertDeferredStorageDebt(DeferredStorageDebt debt) => _inner.UpsertDeferredStorageDebt(debt);
            public IReadOnlyList<DeferredStorageDebt> ListOpenDeferredStorageDebts(int max = int.MaxValue) => _inner.ListOpenDeferredStorageDebts(max);
            public ulong CountOpenDeferredStorageDebts() => _inner.CountOpenDeferredStorageDebts();
            public void ClearDeferredStorageDebt(byte[] accountHash, byte[] discoveredStorageRoot) => _inner.ClearDeferredStorageDebt(accountHash, discoveredStorageRoot);
            public void ClearAllDeferredStorageDebts() => _inner.ClearAllDeferredStorageDebts();
        }

        [Fact]
        public async Task FastStart_UsesFreshLastBlockRead_NotStaleCapture()
        {
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

            Assert.Equal(50UL, result.LastExecutedBlock);
        }

        [Fact]
        public async Task FastStart_FiresWhenSnapPivotGenuinelyAhead()
        {
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

            Assert.Equal(100UL, result.LastExecutedBlock);
            Assert.Equal(0, executor.CallCount);
        }
    }
}
