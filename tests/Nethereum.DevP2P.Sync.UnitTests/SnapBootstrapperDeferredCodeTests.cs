using System;
using System.Collections.Generic;
using System.Linq;
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
    public class SnapBootstrapperDeferredCodeTests
    {
        [Fact]
        public void SnapRootMismatchException_CarriesDeferredCodeHashes()
        {
            var codeHash = Sha3Keccack.Current.CalculateHash(new byte[] { 0x60, 0x00, 0xf3 });
            var ex = new SnapSyncClient.SnapRootMismatchException(
                "mismatch",
                Array.Empty<SnapSyncClient.AccountNeedingHeal>(),
                new[] { codeHash });
            Assert.Single(ex.CodeHashesNeedingHeal);
            Assert.Equal(codeHash, ex.CodeHashesNeedingHeal[0]);
        }

        [Fact]
        public async Task InMemoryDeferredList_NonFlatBundle_BackstopHealsCodeAndClearsBlob()
        {
            var code = new byte[] { 0x60, 0x2A, 0xf3 };
            var codeHash = Sha3Keccack.Current.CalculateHash(code);
            using var bundle = new PlainBundle();
            var scheduler = new CodeScheduler(code);

            await InvokeFetchMissingBytecodeAsync(bundle, scheduler, phase2DeferredCode: new[] { codeHash });

            Assert.Equal(code, await bundle.State.GetCodeAsync(codeHash));
            Assert.Null(bundle.Metadata.GetDeferredHealCodeBlob());
        }

        [Fact]
        public async Task PersistedBlobOnly_ResumeWithNoInMemoryList_BackstopHealsFromDurableBlob()
        {
            var code = new byte[] { 0x60, 0x07, 0xf3 };
            var codeHash = Sha3Keccack.Current.CalculateHash(code);
            using var bundle = new PlainBundle();
            bundle.Metadata.SaveDeferredHealCodeBlob(DeferredHealCodeCodec.Encode(new[] { codeHash }));
            var scheduler = new CodeScheduler(code);

            await InvokeFetchMissingBytecodeAsync(bundle, scheduler, phase2DeferredCode: null);

            Assert.Equal(code, await bundle.State.GetCodeAsync(codeHash));
            Assert.Null(bundle.Metadata.GetDeferredHealCodeBlob());
        }

        [Fact]
        public async Task DeferredCode_NoPeerServes_BlocksCommitAndKeepsDurableBlob()
        {
            var code = new byte[] { 0x60, 0x09, 0xf3 };
            var codeHash = Sha3Keccack.Current.CalculateHash(code);
            using var bundle = new PlainBundle();
            var scheduler = new CodeScheduler();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => InvokeFetchMissingBytecodeAsync(bundle, scheduler, phase2DeferredCode: new[] { codeHash }));

            Assert.Contains("bytecode", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(await bundle.State.GetCodeAsync(codeHash));
            var persisted = DeferredHealCodeCodec.Decode(bundle.Metadata.GetDeferredHealCodeBlob());
            Assert.Single(persisted);
            Assert.Equal(codeHash, persisted[0]);
        }


        private static Task InvokeFetchMissingBytecodeAsync(
            IChainStoreBundle bundle, IFetchRequestScheduler scheduler, IReadOnlyList<byte[]> phase2DeferredCode)
            => SnapBootstrapper.FetchMissingBytecodeAsync(
                bundle, scheduler, null, Hash(0x01), NullLogger.Instance, CancellationToken.None, phase2DeferredCode);

        private static byte[] Hash(byte value)
        {
            var h = new byte[32];
            h[31] = value;
            return h;
        }

        private sealed class CodeScheduler : IFetchRequestScheduler
        {
            private readonly List<byte[]> _codes;
            public CodeScheduler(params byte[][] codes) => _codes = codes.ToList();

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

        private sealed class PlainBundle : IChainStoreBundle
        {
            private readonly InMemoryChainStoreBundle _inner = InMemoryChainStoreBundle.Open();

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

            public Task<ChainCheckpoint> SaveCheckpointAsync(ulong blockNumber, byte[] stateRoot, byte[] blockHash, CancellationToken ct = default) => _inner.SaveCheckpointAsync(blockNumber, stateRoot, blockHash, ct);
            public Task<IReadOnlyList<ChainCheckpoint>> ListCheckpointsAsync(CancellationToken ct = default) => _inner.ListCheckpointsAsync(ct);
            public Task RestoreCheckpointAsync(ulong blockNumber, CancellationToken ct = default) => _inner.RestoreCheckpointAsync(blockNumber, ct);
            public Task DeleteCheckpointAsync(ulong blockNumber, CancellationToken ct = default) => _inner.DeleteCheckpointAsync(blockNumber, ct);
            public Task ResetStateOnlyAsync(CancellationToken ct = default) => _inner.ResetStateOnlyAsync(ct);
            public Task ResetSnapBootstrapStateAsync(CancellationToken ct = default) => _inner.ResetSnapBootstrapStateAsync(ct);
            public string ResolveCheckpointSnapshotPath(ulong blockNumber) => _inner.ResolveCheckpointSnapshotPath(blockNumber);
            public Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default) => _inner.ExportDatabaseAsync(outputPath, ct);
            public IBundleBatch BeginBatch() => _inner.BeginBatch();
            public void Dispose() => _inner.Dispose();
            public ValueTask DisposeAsync() => _inner.DisposeAsync();
        }
    }
}
