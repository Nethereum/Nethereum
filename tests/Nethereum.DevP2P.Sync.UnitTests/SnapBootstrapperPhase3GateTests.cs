using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperPhase3GateTests
    {
        [Fact]
        public void Given_AMalformedLegacyDebtRow_When_TheGateReportIsBuilt_Then_ItIsIgnoredAndFinalizeIsAllowed()
        {
            using var inner = InMemoryChainStoreBundle.Open();
            using var bundle = new MetadataOverrideBundle(inner, new CountOnlyMetadataStore(1UL));

            var report = SnapBootstrapper.BuildStorageCompletenessGateReport(bundle);

            Assert.True(report.CanFinalize);
            Assert.False(report.DeferredHealBlobPresent);
        }

        private sealed class MetadataOverrideBundle : IChainStoreBundle
        {
            private readonly IChainStoreBundle _inner;

            public MetadataOverrideBundle(IChainStoreBundle inner, IChainMetadataStore metadata)
            {
                _inner = inner;
                Metadata = metadata;
            }

            public IStateStore State => _inner.State;
            public ITrieNodeStore TrieNodes => _inner.TrieNodes;
            public ITrieNodeStore StateTrieNodes => _inner.StateTrieNodes;
            public NodeCommitBlockContext NodeCommitBlockSource => _inner.NodeCommitBlockSource;
            public IBlockStore Blocks => _inner.Blocks;
            public ITransactionStore Transactions => _inner.Transactions;
            public IUncleStore Uncles => _inner.Uncles;
            public IWithdrawalStore Withdrawals => _inner.Withdrawals;
            public IBlockAccessListStore BlockAccessLists => _inner.BlockAccessLists;
            public IReceiptStore Receipts => _inner.Receipts;
            public ILogStore Logs => _inner.Logs;
            public IChainMetadataStore Metadata { get; }
            public IStateDiffStore Diffs => _inner.Diffs;
            public bool JournalEnabled => _inner.JournalEnabled;
            public long FreezerHead => _inner.FreezerHead;
            public long ByHashIndexedHead => _inner.ByHashIndexedHead;
            public long LogIndexRenderedHead => _inner.LogIndexRenderedHead;
            public long LogRenderProgressBlock => _inner.LogRenderProgressBlock;
            public Task<ChainCheckpoint> SaveCheckpointAsync(ulong blockNumber, byte[] stateRoot, byte[] blockHash, CancellationToken ct = default) => _inner.SaveCheckpointAsync(blockNumber, stateRoot, blockHash, ct);
            public Task<IReadOnlyList<ChainCheckpoint>> ListCheckpointsAsync(CancellationToken ct = default) => _inner.ListCheckpointsAsync(ct);
            public Task RestoreCheckpointAsync(ulong blockNumber, CancellationToken ct = default) => _inner.RestoreCheckpointAsync(blockNumber, ct);
            public Task DeleteCheckpointAsync(ulong blockNumber, CancellationToken ct = default) => _inner.DeleteCheckpointAsync(blockNumber, ct);
            public Task ResetStateOnlyAsync(CancellationToken ct = default) => _inner.ResetStateOnlyAsync(ct);
            public Task ResetSnapBootstrapStateAsync(CancellationToken ct = default) => _inner.ResetSnapBootstrapStateAsync(ct);
            public string ResolveCheckpointSnapshotPath(ulong blockNumber) => _inner.ResolveCheckpointSnapshotPath(blockNumber);
            public Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default) => _inner.ExportDatabaseAsync(outputPath, ct);
            public IBundleBatch BeginBatch() => _inner.BeginBatch();
            public void Dispose() { }
            public ValueTask DisposeAsync() => default;
        }

        private sealed class CountOnlyMetadataStore : IChainMetadataStore
        {
            private readonly ulong _openDebtCount;
            public CountOnlyMetadataStore(ulong openDebtCount) => _openDebtCount = openDebtCount;
            public byte[] GetDeferredHealAccountsBlob() => null;
            public byte[] GetDeferredHealCodeBlob() => null;
            public void SaveDeferredHealCodeBlob(byte[] blob) { }
            public void ClearDeferredHealCodeBlob() { }
            public IReadOnlyList<DeferredStorageDebt> ListOpenDeferredStorageDebts(int max = int.MaxValue) => Array.Empty<DeferredStorageDebt>();
            public ulong CountOpenDeferredStorageDebts() => _openDebtCount;
            public ulong GetLastBlock() => throw new NotImplementedException();
            public byte[] GetLastBlockHash() => throw new NotImplementedException();
            public void Commit(ulong lastBlock, byte[] lastBlockHash) => throw new NotImplementedException();
            public ulong GetDurableStateBlock() => throw new NotImplementedException();
            public void CommitDurableState(ulong block, byte[] hash) => throw new NotImplementedException();
            public ulong GetLastFetchedHeader() => throw new NotImplementedException();
            public void SetLastFetchedHeader(ulong blockNumber) => throw new NotImplementedException();
            public ulong GetLastFetchedBody() => throw new NotImplementedException();
            public void SetLastFetchedBody(ulong blockNumber) => throw new NotImplementedException();
            public void SetLastFetchedHeaderAndBody(ulong headerBlock, ulong bodyBlock) => throw new NotImplementedException();
            public ulong GetReceiptBackfillCursor() => throw new NotImplementedException();
            public void SetReceiptBackfillCursor(ulong blockNumber) => throw new NotImplementedException();
            public bool IsGenesisLoaded() => throw new NotImplementedException();
            public void MarkGenesisLoaded() => throw new NotImplementedException();
            public void SaveCheckpoint(ulong blockNumber, byte[] stateRoot, byte[] blockHash) => throw new NotImplementedException();
            public ulong GetLatestCheckpoint() => throw new NotImplementedException();
            public ChainCheckpoint? GetCheckpoint(ulong blockNumber) => throw new NotImplementedException();
            public ChainCheckpoint? GetNearestCheckpointAtOrBefore(ulong upToBlock) => throw new NotImplementedException();
            public ChainCheckpoint RewindToCheckpointAtOrBefore(ulong targetBlock) => throw new NotImplementedException();
            public IReadOnlyList<ulong> ListCheckpointBlockNumbers() => throw new NotImplementedException();
            public void DeleteCheckpoint(ulong blockNumber) => throw new NotImplementedException();
            public int DeleteCheckpointsAbove(ulong targetBlock) => throw new NotImplementedException();
            public void ResetForStateRebuild() => throw new NotImplementedException();
            public SnapSyncState GetSnapSyncState() => throw new NotImplementedException();
            public void SaveSnapSyncState(SnapSyncState state) => throw new NotImplementedException();
            public void ClearSnapSyncState() => throw new NotImplementedException();
            public void ClearCommittedHead() => throw new NotImplementedException();
            public byte[] GetLightClientStateBlob() => throw new NotImplementedException();
            public void SaveLightClientStateBlob(byte[] blob) => throw new NotImplementedException();
            public HeaderSyncState GetHeaderSyncState() => throw new NotImplementedException();
            public void SaveHeaderSyncState(HeaderSyncState state) => throw new NotImplementedException();
            public void SaveDeferredHealAccountsBlob(byte[] blob) => throw new NotImplementedException();
            public void ClearDeferredHealAccountsBlob() => throw new NotImplementedException();
            public void UpsertDeferredStorageDebt(DeferredStorageDebt debt) => throw new NotImplementedException();
            public void ClearDeferredStorageDebt(byte[] accountHash, byte[] discoveredStorageRoot) => throw new NotImplementedException();
            public void ClearAllDeferredStorageDebts() => throw new NotImplementedException();
        }
    }
}
