using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
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
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class ChainNodePhaseSplitTests
    {
        private const ulong TestChainId = 909291;

        private static int ReserveFreeLoopbackPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static ChainNodeConfig ServingAndSyncingConfig(int listenPort)
        {
            var config = new ChainNodeConfig();
            config.Storage.InMemory = true;
            config.Network.Serve = true;
            config.Network.ListenPort = listenPort;
            config.Network.BindAddress = IPAddress.Loopback;
            config.Network.NodeKeyHex = EthECKey.GenerateKey().GetPrivateKey();
            config.Sync.Mode = SyncMode.ForwardExecute;
            return config;
        }

        private static ChainNodeConfig ComposedOnlyConfig()
        {
            var config = new ChainNodeConfig();
            config.Storage.InMemory = true;
            config.Network.Serve = false;
            config.Sync.Mode = SyncMode.None;
            return config;
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

        private sealed class ListenerProbeDefinition : IChainDefinition
        {
            private readonly int _listenPort;

            public ListenerProbeDefinition(int listenPort) => _listenPort = listenPort;

            public bool ListenerWasAcceptingConnectionsWhenSyncStarted { get; private set; }

            public async Task EnsureGenesisAsync(IChainStoreBundle bundle, CancellationToken ct)
                => await TestGenesis.WriteAsync(bundle);

            public async Task<IChainProfile> CreateProfileAsync(IChainStoreBundle bundle)
            {
                var genesisHash = await bundle.Blocks.GetHashByNumberAsync(0);
                return new TestProfile(TestChainId, genesisHash);
            }

            public ICanonicalStateRootSource CreateTip(PeerPoolManager pool)
            {
                ListenerWasAcceptingConnectionsWhenSyncStarted = CanConnectToLoopback(_listenPort);
                return null;
            }

            private static bool CanConnectToLoopback(int port)
            {
                try
                {
                    using var client = new TcpClient();
                    client.Connect(IPAddress.Loopback, port);
                    return client.Connected;
                }
                catch
                {
                    return false;
                }
            }
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

        private sealed class ThrowingDrainBundle : IChainStoreBundle, IAtomicBlockFlush
        {
            private readonly IChainStoreBundle _inner;

            public ThrowingDrainBundle(IChainStoreBundle inner) => _inner = inner;

            public bool Disposed { get; private set; }

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

            public bool WithdrawalsFoldedFor(ulong block) => false;

            public bool BlockOwnedByStagedFlush(ulong block) => false;

            public Task DrainAsync() => throw new InvalidOperationException("drain deliberately fails for the resilience test");

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

        private sealed class RecordingLogger : ILogger
        {
            private readonly List<(LogLevel Level, Exception Exception)> _entries;

            public RecordingLogger(List<(LogLevel Level, Exception Exception)> entries) => _entries = entries;

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter)
                => _entries.Add((logLevel, exception));

            private sealed class NoopScope : IDisposable
            {
                public static readonly NoopScope Instance = new NoopScope();
                public void Dispose() { }
            }
        }

        private sealed class RecordingLoggerFactory : ILoggerFactory
        {
            public List<(LogLevel Level, Exception Exception)> Entries { get; } = new();

            public ILogger CreateLogger(string categoryName) => new RecordingLogger(Entries);

            public void AddProvider(ILoggerProvider provider) { }

            public void Dispose() { }
        }

        [Fact]
        public async Task Given_ChainNodeStartAsync_When_Composed_Then_ListenerStartsBeforeSync()
        {
            var port = ReserveFreeLoopbackPort();
            var config = ServingAndSyncingConfig(port);
            var definition = new ListenerProbeDefinition(port);

            await using var node = await HostedNode.StartAsync(definition, config, loggerFactory: null);

            Assert.True(
                definition.ListenerWasAcceptingConnectionsWhenSyncStarted,
                "the listener must already be accepting inbound connections by the time the sync stack starts, matching today's listener-before-sync order");
        }

        [Fact]
        public async Task Given_ChainNodeComposeAsync_When_ComposedAlone_Then_ListenerAndSyncAreBothNull()
        {
            var config = ServingAndSyncingConfig(ReserveFreeLoopbackPort());

            await using var node = await HostedNode.ComposeAsync(new RecordingDefinition(), config, loggerFactory: null);

            Assert.Null(node.Listener);
            Assert.Null(node.Sync);
        }

        [Fact]
        public async Task Given_ComposedNodeStartsServingThenSyncing_When_BothPhasesRun_Then_ListenerAndSyncAreBothSet()
        {
            var port = ReserveFreeLoopbackPort();
            var config = ServingAndSyncingConfig(port);

            await using var node = await HostedNode.ComposeAsync(new RecordingDefinition(), config, loggerFactory: null);
            Assert.Null(node.Listener);
            Assert.Null(node.Sync);

            await node.StartServingAsync();
            Assert.NotNull(node.Listener);
            Assert.Null(node.Sync);

            await node.StartSyncAsync();
            Assert.NotNull(node.Sync);
        }

        [Fact]
        public async Task Given_OneDisposeStepThrows_When_ChainNodeDisposeAsyncRuns_Then_TheRemainingStepsStillRun()
        {
            var config = ComposedOnlyConfig();
            var trackedBundle = new ThrowingDrainBundle(InMemoryChainStoreBundle.Open());

            var node = await HostedNode.StartAsync(
                new RecordingDefinition(), config, loggerFactory: null,
                storageFactory: (_, _) => ChainNodeStorage.FromBundle(trackedBundle));

            await node.DisposeAsync();

            Assert.True(trackedBundle.Disposed, "storage disposal must still run after an earlier dispose step throws");
        }

        [Fact]
        public async Task Given_OneDisposeStepThrows_When_ChainNodeDisposeAsyncRuns_Then_TheExceptionIsLoggedNotSwallowedSilently()
        {
            var config = ComposedOnlyConfig();
            var trackedBundle = new ThrowingDrainBundle(InMemoryChainStoreBundle.Open());
            var recordingLoggerFactory = new RecordingLoggerFactory();

            var node = await HostedNode.StartAsync(
                new RecordingDefinition(), config, loggerFactory: recordingLoggerFactory,
                storageFactory: (_, _) => ChainNodeStorage.FromBundle(trackedBundle));

            await node.DisposeAsync();

            Assert.Contains(
                recordingLoggerFactory.Entries,
                entry => entry.Level == LogLevel.Error && entry.Exception is InvalidOperationException);
        }
    }
}
