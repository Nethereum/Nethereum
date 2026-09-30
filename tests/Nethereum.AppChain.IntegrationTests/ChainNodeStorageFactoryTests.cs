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
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class ChainNodeStorageFactoryTests
    {
        private const ulong TestChainId = 909191;

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

        private sealed class DisposeTrackingBundle : IChainStoreBundle
        {
            private readonly IChainStoreBundle _inner;

            public DisposeTrackingBundle(IChainStoreBundle inner) => _inner = inner;

            public bool Disposed { get; private set; }

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

            public void Dispose()
            {
                Disposed = true;
                _inner.Dispose();
            }

            public async ValueTask DisposeAsync()
            {
                Disposed = true;
                await _inner.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_NoStorageFactory_When_TheNodeStarts_Then_ItUsesChainNodeStorageOpen()
        {
            var config = InMemoryConfig();

            await using var node = await HostedNode.StartAsync(
                new RecordingDefinition(), config, loggerFactory: null);

            Assert.NotNull(node.Bundle);
            Assert.Null(node.Storage.Manager);
            Assert.NotNull(await node.Bundle.Blocks.GetHashByNumberAsync(0));
        }

        [Fact]
        public async Task Given_AStorageFactoryWrappingAnAlreadyOpenBundle_When_TheNodeStarts_Then_TheNodeExposesThatExactBundle()
        {
            var config = InMemoryConfig();
            var preOpenedBundle = InMemoryChainStoreBundle.Open();

            await using var node = await HostedNode.StartAsync(
                new RecordingDefinition(), config, loggerFactory: null,
                storageFactory: (_, _) => ChainNodeStorage.FromBundle(preOpenedBundle));

            Assert.Same(preOpenedBundle, node.Bundle);
        }

        [Fact]
        public async Task Given_ChainNodeStorageFromBundle_When_WrappingAnAlreadyOpenBundleWithNoManager_Then_DisposeAsyncDoesNotThrow()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var storage = ChainNodeStorage.FromBundle(bundle, manager: null);

            var exception = await Record.ExceptionAsync(async () => await storage.DisposeAsync());

            Assert.Null(exception);
        }

        [Fact]
        public async Task Given_ChainNodeStorageFromBundle_When_Disposed_Then_TheWrappedBundleItselfIsDisposed()
        {
            var tracked = new DisposeTrackingBundle(InMemoryChainStoreBundle.Open());
            var storage = ChainNodeStorage.FromBundle(tracked, manager: null);

            await storage.DisposeAsync();

            Assert.True(tracked.Disposed, "FromBundle's DisposeAsync must dispose the exact bundle instance it was given");
        }
    }
}
