using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        public async Task Given_PersistedMissingCodeAndPeerReturnsNone_When_FetchMissingBytecode_Then_BlocksAndKeepsInventory()
        {
            var code = new byte[] { 0x60, 0x00, 0xf3 };
            var codeHash = Sha3Keccack.Current.CalculateHash(code);
            using var bundle = new BytecodeInventoryBundle(codeHash);
            var scheduler = new BytecodeScheduler(Array.Empty<byte[]>());

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => InvokeFetchMissingBytecodeAsync(bundle, scheduler));

            Assert.Contains("bytecode", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(bundle.GetPersistedMissingCode());
            Assert.Null(await bundle.State.GetCodeAsync(codeHash));
        }

        [Fact]
        public async Task Given_PersistedMissingCodeAndPeerReturnsCode_When_FetchMissingBytecode_Then_WritesCodeAndClearsInventory()
        {
            var code = new byte[] { 0x60, 0x00, 0xf3 };
            var codeHash = Sha3Keccack.Current.CalculateHash(code);
            using var bundle = new BytecodeInventoryBundle(codeHash);
            var scheduler = new BytecodeScheduler(code);

            await InvokeFetchMissingBytecodeAsync(bundle, scheduler);

            Assert.Empty(bundle.GetPersistedMissingCode());
            Assert.Equal(code, await bundle.State.GetCodeAsync(codeHash));
        }

        [Fact]
        public async Task Given_UnloadableStateTrie_When_EnsuringBytecodeCompleteness_Then_BlocksPivotCommit()
        {
            using var bundle = new BytecodeInventoryBundle();
            var scheduler = new BytecodeScheduler();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => InvokeEnsureBytecodeCompleteAsync(bundle, scheduler, Hash(0x99)));

            Assert.Contains("could not load the state trie", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        [Fact]
        public void Given_MetadataCountSeesMalformedDebt_When_GateReportBuilt_Then_FinalizationIsBlocked()
        {
            using var inner = InMemoryChainStoreBundle.Open();
            using var bundle = new MetadataOverrideBundle(inner, new CountOnlyMetadataStore(1UL));

            var report = InvokeBuildGateReport(bundle);
            var reportType = report.GetType();

            Assert.False((bool)reportType.GetProperty("CanFinalize")!.GetValue(report)!);
            Assert.Equal(1UL, reportType.GetProperty("OpenDeferredStorageDebts")!.GetValue(report));
            Assert.Equal(1UL, reportType.GetProperty("MalformedDeferredStorageDebtRows")!.GetValue(report));
            Assert.Contains("malformed_debts=1", report.ToString());
        }

        private static async Task InvokeFetchMissingBytecodeAsync(BytecodeInventoryBundle bundle, IFetchRequestScheduler scheduler)
        {
            var method = typeof(SnapBootstrapper).GetMethod(
                "FetchMissingBytecodeAsync",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            var task = (Task)method!.Invoke(null, new object[]
            {
                bundle,
                scheduler,
                null,
                Hash(0x01),
                NullLogger.Instance,
                CancellationToken.None,
                null,
            })!;
            await task.ConfigureAwait(false);
        }

        private static async Task InvokeEnsureBytecodeCompleteAsync(
            BytecodeInventoryBundle bundle,
            IFetchRequestScheduler scheduler,
            byte[] stateRoot)
        {
            var method = typeof(SnapBootstrapper).GetMethod(
                "EnsureBytecodeCompleteAsync",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            var task = (Task)method!.Invoke(null, new object[]
            {
                bundle,
                scheduler,
                stateRoot,
                NullLogger.Instance,
                CancellationToken.None,
            })!;
            await task.ConfigureAwait(false);
        }
        private static object InvokeBuildGateReport(IChainStoreBundle bundle)
        {
            return SnapBootstrapper.BuildStorageCompletenessGateReport(bundle);
        }

        private sealed class BytecodeScheduler : IFetchRequestScheduler
        {
            private readonly List<byte[]> _codes;

            public BytecodeScheduler(params byte[][] codes)
            {
                _codes = codes.ToList();
            }

            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => Task.FromResult(new ByteCodesMessage { Codes = _codes });

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false) => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        private sealed class BytecodeInventoryBundle : IChainStoreBundle, IFlatStateReconciler
        {
            private readonly InMemoryChainStoreBundle _inner = InMemoryChainStoreBundle.Open();
            private readonly List<byte[]> _missing;

            public BytecodeInventoryBundle(params byte[][] missing)
            {
                _missing = missing.Select(h => h.ToArray()).ToList();
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
            public IChainMetadataStore Metadata => _inner.Metadata;
            public IStateDiffStore Diffs => _inner.Diffs;
            public bool JournalEnabled => _inner.JournalEnabled;
            public long FreezerHead => _inner.FreezerHead;
            public long ByHashIndexedHead => _inner.ByHashIndexedHead;
            public long LogIndexRenderedHead => _inner.LogIndexRenderedHead;
            public long LogRenderProgressBlock => _inner.LogRenderProgressBlock;

            public Task<ChainCheckpoint> SaveCheckpointAsync(ulong blockNumber, byte[] stateRoot, byte[] blockHash, CancellationToken ct = default)
                => _inner.SaveCheckpointAsync(blockNumber, stateRoot, blockHash, ct);
            public Task<IReadOnlyList<ChainCheckpoint>> ListCheckpointsAsync(CancellationToken ct = default) => _inner.ListCheckpointsAsync(ct);
            public Task RestoreCheckpointAsync(ulong blockNumber, CancellationToken ct = default) => _inner.RestoreCheckpointAsync(blockNumber, ct);
            public Task DeleteCheckpointAsync(ulong blockNumber, CancellationToken ct = default) => _inner.DeleteCheckpointAsync(blockNumber, ct);
            public Task ResetStateOnlyAsync(CancellationToken ct = default) => _inner.ResetStateOnlyAsync(ct);
            public Task ResetSnapBootstrapStateAsync(CancellationToken ct = default) => _inner.ResetSnapBootstrapStateAsync(ct);
            public string ResolveCheckpointSnapshotPath(ulong blockNumber) => _inner.ResolveCheckpointSnapshotPath(blockNumber);
            public Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default) => _inner.ExportDatabaseAsync(outputPath, ct);
            public IBundleBatch BeginBatch() => _inner.BeginBatch();

            public Task<FlatStateReconcileResult> ReconcileFlatStateAsync(byte[] stateRoot, Action<string> progress, CancellationToken ct)
                => Task.FromResult(EmptyReconcileResult());
            public Task<FlatStateReconcileResult> VerifyFlatStateAsync(byte[] stateRoot, Action<string> progress, CancellationToken ct, long sampleAccountsPerShard = 0)
                => Task.FromResult(EmptyReconcileResult());
            public IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> GetPersistedDamage()
                => Array.Empty<(byte[], byte[])>();
            public void ClearPersistedDamage() { }
            public IReadOnlyList<byte[]> GetPersistedMissingCode() => _missing.Select(h => h.ToArray()).ToList();
            public void ClearPersistedMissingCode() => _missing.Clear();

            public void Dispose() => _inner.Dispose();
            public ValueTask DisposeAsync() => _inner.DisposeAsync();
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

        private static FlatStateReconcileResult EmptyReconcileResult()
            => new FlatStateReconcileResult(0, 0, 0, 0, 0, 0, 0, 0);

        private static byte[] Hash(byte value)
        {
            var hash = new byte[32];
            hash[31] = value;
            return hash;
        }
    }
}
