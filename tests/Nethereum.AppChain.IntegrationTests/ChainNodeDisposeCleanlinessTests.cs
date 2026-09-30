using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting;
using HostedNode = Nethereum.ChainNode.Hosting.ChainNode;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class ChainNodeDisposeCleanlinessTests
    {
        private const ulong TestChainId = 909192;

        private static ChainNodeConfig InMemoryConfig()
        {
            var config = new ChainNodeConfig();
            config.Storage.InMemory = true;
            config.Network.Serve = false;
            config.Network.ListenPort = 0;
            config.Network.BindAddress = System.Net.IPAddress.Loopback;
            config.Sync.Mode = SyncMode.None;
            return config;
        }

        private sealed class RecordingDefinition : IChainDefinition
        {
            public async Task EnsureGenesisAsync(IChainStoreBundle bundle, CancellationToken ct)
                => await TestGenesis.WriteAsync(bundle);

            public async Task<IChainProfile> CreateProfileAsync(IChainStoreBundle bundle)
            {
                var genesisHash = await bundle.Blocks.GetHashByNumberAsync(0);
                return new TestProfile(TestChainId, genesisHash);
            }

            public ICanonicalStateRootSource CreateTip(PeerPoolManager pool) => null;
        }

        private sealed class TestProfile : IChainProfile
        {
            public TestProfile(ulong networkId, byte[] genesisHash)
            {
                NetworkId = networkId;
                GenesisHash = genesisHash;
            }

            public ulong NetworkId { get; }

            public byte[] GenesisHash { get; }

            public (ulong[] BlockHeights, ulong[] Timestamps) ForkThresholds =>
                (Array.Empty<ulong>(), Array.Empty<ulong>());

            public IReadOnlyList<string> Bootnodes => Array.Empty<string>();

            public IPeerHandshakeWorker CreateHandshakeWorker(ILoggerFactory loggerFactory, bool advertiseSnap2) =>
                new ChainPeerHandshakeWorker(
                    GenesisHash, NetworkId, ForkThresholds, ourHead: null, logger: null, advertiseSnap2: advertiseSnap2);
        }

        private sealed class DrainRecordingBundle : IChainStoreBundle, IAtomicBlockFlush
        {
            private readonly IChainStoreBundle _inner;
            private readonly bool _throwOnDispose;

            public DrainRecordingBundle(IChainStoreBundle inner, bool throwOnDispose)
            {
                _inner = inner;
                _throwOnDispose = throwOnDispose;
            }

            public bool DrainCalled { get; private set; }

            public IStateStore State => _inner.State;
            public ITrieNodeStore TrieNodes => _inner.TrieNodes;
            public Nethereum.Merkle.Patricia.Storage.ITrieNodeStore StateTrieNodes => _inner.StateTrieNodes;
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

            public Task<ChainCheckpoint> SaveCheckpointAsync(
                ulong blockNumber, byte[] stateRoot, byte[] blockHash, CancellationToken ct = default)
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

            public Task FlushBlockAsync(FlatStateBatch flat, ulong block, byte[] hash) => Task.CompletedTask;

            public void DiscardCapturedBlock() { }

            public void ArmWithdrawals(ulong block, IList<Withdrawal> withdrawals) { }

            public bool WithdrawalsFoldedFor(ulong block) => true;

            public bool BlockOwnedByStagedFlush(ulong block) => false;

            public Task DrainAsync()
            {
                DrainCalled = true;
                return Task.CompletedTask;
            }

            public void Dispose() => _inner.Dispose();

            public async ValueTask DisposeAsync()
            {
                if (_throwOnDispose) throw new InvalidOperationException("storage close failed");
                await _inner.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_AStorageDisposeStepThrows_When_ChainNodeDisposeAsyncRuns_Then_DisposedCleanlyIsFalse()
        {
            var config = InMemoryConfig();
            var bundle = new DrainRecordingBundle(InMemoryChainStoreBundle.Open(), throwOnDispose: true);

            var node = await HostedNode.StartAsync(
                new RecordingDefinition(), config, loggerFactory: null,
                storageFactory: (_, _) => ChainNodeStorage.FromBundle(bundle));

            await node.DisposeAsync();

            Assert.True(bundle.DrainCalled, "the drain step must still run even though storage close will fail");
            Assert.False(node.DisposedCleanly);
        }

        [Fact]
        public async Task Given_AllDisposeStepsSucceed_When_ChainNodeDisposeAsyncRuns_Then_DisposedCleanlyIsTrue()
        {
            var config = InMemoryConfig();
            var bundle = new DrainRecordingBundle(InMemoryChainStoreBundle.Open(), throwOnDispose: false);

            var node = await HostedNode.StartAsync(
                new RecordingDefinition(), config, loggerFactory: null,
                storageFactory: (_, _) => ChainNodeStorage.FromBundle(bundle));

            await node.DisposeAsync();

            Assert.True(bundle.DrainCalled);
            Assert.True(node.DisposedCleanly);
        }
    }
}
