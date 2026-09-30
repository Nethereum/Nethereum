using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.AppChain.Server.Hosting;
using Nethereum.ChainNode.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.NodeDb;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Publish;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class ChainNodeHostingCoreDefaultsTests
    {
        private static byte[] Make32(byte fill)
        {
            var bytes = new byte[32];
            for (var i = 0; i < 32; i++) bytes[i] = (byte)(fill ^ i);
            return bytes;
        }

        private static async Task<InMemoryChainStoreBundle> GenesisBundleAsync(byte[] genesisHash)
        {
            var bundle = InMemoryChainStoreBundle.Open();
            await bundle.Blocks.SaveAsync(new BlockHeader { BlockNumber = 0, Timestamp = 1000 }, genesisHash);
            return bundle;
        }

        private static DefaultChainProfile ProfileFor(ulong networkId, byte[] genesisHash, IChainStoreBundle bundle) =>
            new DefaultChainProfile(
                networkId, genesisHash,
                ChainForkSchedule.Running((long)networkId, HardforkName.Amsterdam),
                Array.Empty<string>(), bundle: bundle);

        private static PeerListenerOptions ListenerOptionsOf(PeerListener listener) =>
            (PeerListenerOptions)typeof(PeerListener)
                .GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(listener)!;

        private static PeerPoolOptions PoolOptionsOf(PeerPoolManager pool) =>
            (PeerPoolOptions)typeof(PeerPoolManager)
                .GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(pool)!;

        [Fact]
        public async Task Given_DefaultChainNodeSyncConfig_When_ChainNodeSyncStackStarts_Then_BlockSourceIsPlainDevP2PPullOnly()
        {
            var genesisHash = Make32(0x11);
            var bundle = await GenesisBundleAsync(genesisHash);
            var profile = ProfileFor(990101, genesisHash, bundle);

            var config = new ChainNodeConfig();
            config.Storage.InMemory = true;

            var stack = await ChainNodeSyncStack.StartAsync(profile, bundle, config, loggerFactory: null);
            try
            {
                Assert.False(config.Sync.EnablePushedBlocks);
                Assert.IsType<DevP2PBlockSource>(stack.BlockSource);
                Assert.Null(stack.PushedBlocks);
            }
            finally
            {
                await stack.DisposeAsync();
                await bundle.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_EnablePushedBlocksTrue_When_ChainNodeSyncStackStarts_Then_BlockSourceIsCatchUpThenFollow()
        {
            var genesisHash = Make32(0x12);
            var bundle = await GenesisBundleAsync(genesisHash);
            var profile = ProfileFor(990102, genesisHash, bundle);

            var config = new ChainNodeConfig();
            config.Storage.InMemory = true;
            config.Sync.EnablePushedBlocks = true;

            var stack = await ChainNodeSyncStack.StartAsync(profile, bundle, config, loggerFactory: null);
            try
            {
                Assert.IsType<CatchUpThenFollowBlockSource>(stack.BlockSource);
                Assert.NotNull(stack.PushedBlocks);
            }
            finally
            {
                await stack.DisposeAsync();
                await bundle.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_DefaultChainNodeMempoolConfig_When_ChainNodeServeListenerStarts_Then_OnTrustedTransactionsReceivedIsNull()
        {
            var genesisHash = Make32(0x21);
            var bundle = await GenesisBundleAsync(genesisHash);
            var profile = ProfileFor(990201, genesisHash, bundle);

            var config = new ChainNodeConfig();
            config.Network.ListenPort = 0;
            config.Network.BindAddress = IPAddress.Loopback;
            config.Network.NodeKeyHex = EthECKey.GenerateKey().GetPrivateKey();

            var mempool = ChainNodeMempool.Create(profile, bundle, config, new Eth68PeerPool(), loggerFactory: null);

            var listener = await ChainNodeServeListener.StartAsync(
                profile, bundle, config, mempool, callbacks: null, loggerFactory: null);
            try
            {
                Assert.False(config.Mempool.EnableTrustedPeerAdmission);
                var options = ListenerOptionsOf(listener);
                Assert.Null(options.OnTrustedTransactionsReceived);
                Assert.Null(options.OnTrustedTransactionsReceivedFrom);
            }
            finally
            {
                await listener.DisposeAsync();
                mempool.Dispose();
                await bundle.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_EnableTrustedPeerAdmissionTrueAndATrustedNodeId_When_ChainNodeServeListenerStarts_Then_ItAdmits()
        {
            var genesisHash = Make32(0x22);
            var bundle = await GenesisBundleAsync(genesisHash);
            var profile = ProfileFor(990202, genesisHash, bundle);

            var config = new ChainNodeConfig();
            config.Network.ListenPort = 0;
            config.Network.BindAddress = IPAddress.Loopback;
            config.Network.NodeKeyHex = EthECKey.GenerateKey().GetPrivateKey();
            config.Network.TrustedNodeIds = new[] { EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex() };
            config.Mempool.EnableTrustedPeerAdmission = true;

            var mempool = ChainNodeMempool.Create(profile, bundle, config, new Eth68PeerPool(), loggerFactory: null);

            var listener = await ChainNodeServeListener.StartAsync(
                profile, bundle, config, mempool, callbacks: null, loggerFactory: null);
            try
            {
                Assert.NotEmpty(config.ResolveTrustedNodeIds());
                var options = ListenerOptionsOf(listener);
                Assert.NotNull(options.OnTrustedTransactionsReceived);
            }
            finally
            {
                await listener.DisposeAsync();
                mempool.Dispose();
                await bundle.DisposeAsync();
            }
        }

        [Fact]
        public void Given_AppChainServerConfigDefaultNode_When_Composed_Then_PushAndTrustedAdmissionAndFlooringAreAllTrue()
        {
            var config = new AppChainServerConfig();

            Assert.True(config.Node.Sync.EnablePushedBlocks);
            Assert.True(config.Node.Sync.FloorTargetPeerCountByDialPool);
            Assert.True(config.Node.Mempool.EnableTrustedPeerAdmission);
        }

        [Fact]
        public async Task Given_ChainNodeSyncStackWithNoDataDirectory_When_Started_Then_ItStartsCleanly()
        {
            var genesisHash = Make32(0x31);
            var bundle = await GenesisBundleAsync(genesisHash);
            var profile = ProfileFor(990301, genesisHash, bundle);

            var config = new ChainNodeConfig();
            config.Storage.InMemory = true;
            config.Storage.DataDirectory = null!;

            var stack = await ChainNodeSyncStack.StartAsync(profile, bundle, config, loggerFactory: null);
            try
            {
                Assert.NotNull(stack.Pool);
            }
            finally
            {
                await stack.DisposeAsync();
                await bundle.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_AMinPeerLatestBlockFactory_When_ChainNodeSyncStackStarts_Then_ItIsAwaitedAndUsedInsteadOfTheStaticConfigValue()
        {
            var genesisHash = Make32(0x41);
            var bundle = await GenesisBundleAsync(genesisHash);
            var profile = ProfileFor(990401, genesisHash, bundle);

            var config = new ChainNodeConfig();
            config.Storage.InMemory = true;
            config.Sync.MinPeerLatestBlock = 5;

            var stack = await ChainNodeSyncStack.StartAsync(
                profile, bundle, config, loggerFactory: null, tipFactory: null, ct: default,
                minPeerLatestBlockFactory: _ => Task.FromResult(42UL));
            try
            {
                var options = PoolOptionsOf(stack.Pool);
                Assert.Equal(42UL, options.MinPeerLatestBlock);
            }
            finally
            {
                await stack.DisposeAsync();
                await bundle.DisposeAsync();
            }
        }

        private sealed class RecordingHandshakeWorker : IPeerHandshakeWorker
        {
            private readonly ConcurrentQueue<string> _order = new();

            public IReadOnlyList<string> Order => _order.ToArray();

            public Task<IEthPeer> HandshakeAsync(
                string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
            {
                _order.Enqueue(enode);
                return Task.FromException<IEthPeer>(
                    new InvalidOperationException("this handshake worker never actually connects"));
            }
        }

        private sealed class RecordingProfile : IChainProfile
        {
            private readonly RecordingHandshakeWorker _worker;

            public RecordingProfile(ulong networkId, byte[] genesisHash, RecordingHandshakeWorker worker)
            {
                NetworkId = networkId;
                GenesisHash = genesisHash;
                _worker = worker;
            }

            public ulong NetworkId { get; }

            public byte[] GenesisHash { get; }

            public (ulong[] BlockHeights, ulong[] Timestamps) ForkThresholds => (Array.Empty<ulong>(), Array.Empty<ulong>());

            public IReadOnlyList<string> Bootnodes => Array.Empty<string>();

            public IPeerHandshakeWorker CreateHandshakeWorker(ILoggerFactory loggerFactory, bool advertiseSnap2) => _worker;
        }

        private static async Task<IReadOnlyList<string>> WaitForBothDialsAsync(
            RecordingHandshakeWorker worker, string first, string second, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var order = worker.Order;
                if (order.Contains(first) && order.Contains(second)) return order;
                await Task.Delay(50);
            }
            return worker.Order;
        }

        private static async Task<IReadOnlyList<string>> WaitForAllDialsAsync(
            RecordingHandshakeWorker worker, IReadOnlyList<string> enodes, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var order = worker.Order;
                if (enodes.All(order.Contains)) return order;
                await Task.Delay(50);
            }
            return worker.Order;
        }

        private static HashSet<string> TrustedDialKeysOf(PeerPoolManager pool) =>
            (HashSet<string>)typeof(PeerPoolManager)
                .GetField("_trustedDialKeys", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(pool)!;

        private sealed class BootnodesProfile : IChainProfile
        {
            private readonly RecordingHandshakeWorker _worker;

            public BootnodesProfile(
                ulong networkId, byte[] genesisHash, IReadOnlyList<string> bootnodes, RecordingHandshakeWorker worker)
            {
                NetworkId = networkId;
                GenesisHash = genesisHash;
                Bootnodes = bootnodes;
                _worker = worker;
            }

            public ulong NetworkId { get; }

            public byte[] GenesisHash { get; }

            public (ulong[] BlockHeights, ulong[] Timestamps) ForkThresholds => (Array.Empty<ulong>(), Array.Empty<ulong>());

            public IReadOnlyList<string> Bootnodes { get; }

            public IPeerHandshakeWorker CreateHandshakeWorker(ILoggerFactory loggerFactory, bool advertiseSnap2) => _worker;
        }

        [Fact]
        public async Task Given_NetworkTrustedBootnodesConfigured_When_ChainNodeSyncStackStarts_Then_OnlyThoseAndOperatorTrustedPeersAreTrustedButEveryBootnodeIsStillDialed()
        {
            var genesisHash = Make32(0x61);
            var bundle = await GenesisBundleAsync(genesisHash);

            var operatorPeer = $"enode://{EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex()}@127.0.0.1:30601";
            var curatedBootnode = $"enode://{EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex()}@127.0.0.1:30602";
            var ordinaryBootnodeA = $"enode://{EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex()}@127.0.0.1:30603";
            var ordinaryBootnodeB = $"enode://{EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex()}@127.0.0.1:30604";

            var worker = new RecordingHandshakeWorker();
            var profile = new BootnodesProfile(
                990601, genesisHash,
                bootnodes: new[] { curatedBootnode, ordinaryBootnodeA, ordinaryBootnodeB },
                worker);

            var config = new ChainNodeConfig();
            config.Storage.InMemory = true;
            config.Network.TrustedPeers = new[] { operatorPeer };
            config.Network.TrustedBootnodes = new[] { curatedBootnode };
            config.Network.MaxConcurrentDials = 4;
            config.Network.DialBudgetPerSecond = 0;

            var stack = await ChainNodeSyncStack.StartAsync(profile, bundle, config, loggerFactory: null);
            try
            {
                var allTargets = new[] { operatorPeer, curatedBootnode, ordinaryBootnodeA, ordinaryBootnodeB };
                var dialed = await WaitForAllDialsAsync(worker, allTargets, TimeSpan.FromSeconds(10));

                Assert.True(allTargets.All(dialed.Contains),
                    $"expected every dial target to be enqueued as a candidate regardless of trust; dialed=[{string.Join(", ", dialed)}]");

                var trustedDialKeys = TrustedDialKeysOf(stack.Pool);
                Assert.Contains(operatorPeer, trustedDialKeys);
                Assert.Contains(curatedBootnode, trustedDialKeys);
                Assert.DoesNotContain(ordinaryBootnodeA, trustedDialKeys);
                Assert.DoesNotContain(ordinaryBootnodeB, trustedDialKeys);
            }
            finally
            {
                await stack.DisposeAsync();
                await bundle.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_AnAppChainProfileWithNoNetworkTrustedBootnodesConfigured_When_ChainNodeSyncStackStarts_Then_ItsValidatorMeshStaysTrustedViaResolveDialEnodes()
        {
            var genesisHash = Make32(0x62);
            var bundle = await GenesisBundleAsync(genesisHash);

            var validatorA = $"enode://{EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex()}@127.0.0.1:30611";
            var validatorB = $"enode://{EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex()}@127.0.0.1:30612";

            var config = new ChainNodeConfig();
            config.Storage.InMemory = true;
            config.Network.TrustedPeers = new[] { validatorA, validatorB };

            var profile = new DefaultChainProfile(
                990602, genesisHash,
                ChainForkSchedule.Running(990602, HardforkName.Amsterdam),
                config.ResolveDialEnodes(), bundle: bundle);

            var stack = await ChainNodeSyncStack.StartAsync(profile, bundle, config, loggerFactory: null);
            try
            {
                Assert.Empty(config.Network.TrustedBootnodes);

                var trustedDialKeys = TrustedDialKeysOf(stack.Pool);
                Assert.Contains(validatorA, trustedDialKeys);
                Assert.Contains(validatorB, trustedDialKeys);
            }
            finally
            {
                await stack.DisposeAsync();
                await bundle.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_APeerCacheFileWithAPreferredEnode_When_Started_Then_ThatEnodeIsEnqueuedBeforeOtherBootnodes()
        {
            var dataDir = Path.Combine(Path.GetTempPath(), "s2a_peercache_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dataDir);
            try
            {
                var cachedEnode = $"enode://{EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex()}@127.0.0.1:30401";
                var otherEnode = $"enode://{EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex()}@127.0.0.1:30402";

                var peerCachePath = Path.Combine(dataDir, "peer-cache.json");
                var seedCache = new PersistentPeerCache(peerCachePath, _ => { });
                seedCache.RecordSuccess(cachedEnode);
                seedCache.Save();

                var genesisHash = Make32(0x51);
                var bundle = await GenesisBundleAsync(genesisHash);

                var worker = new RecordingHandshakeWorker();
                var profile = new RecordingProfile(990501, genesisHash, worker);

                var config = new ChainNodeConfig();
                config.Storage.InMemory = false;
                config.Storage.DataDirectory = dataDir;
                config.Network.TrustedPeers = new[] { otherEnode };
                config.Network.MaxConcurrentDials = 1;
                config.Network.DialBudgetPerSecond = 0;

                var stack = await ChainNodeSyncStack.StartAsync(profile, bundle, config, loggerFactory: null);
                try
                {
                    var dialed = await WaitForBothDialsAsync(worker, cachedEnode, otherEnode, TimeSpan.FromSeconds(10));

                    var cachedIndex = dialed.ToList().IndexOf(cachedEnode);
                    var otherIndex = dialed.ToList().IndexOf(otherEnode);

                    Assert.True(cachedIndex >= 0, "the peer-cache-preferred enode was never dialed");
                    Assert.True(otherIndex >= 0, "the configured trusted peer was never dialed");
                    Assert.True(cachedIndex < otherIndex,
                        $"expected the peer-cache enode to be dialed first; order was [{string.Join(", ", dialed)}]");
                }
                finally
                {
                    await stack.DisposeAsync();
                    await bundle.DisposeAsync();
                }
            }
            finally
            {
                Directory.Delete(dataDir, recursive: true);
            }
        }

        [Fact]
        public async Task Given_ADevP2PPeersListWithOneMalformedEnode_When_TheNodeStarts_Then_AWarningIsLoggedNamingTheBadEntry()
        {
            var genesisHash = Make32(0x70);
            var bundle = await GenesisBundleAsync(genesisHash);

            var validPeer = $"enode://{EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex()}@127.0.0.1:30701";
            const string malformedPeer = "enode://not-a-real-pubkey@127.0.0.1:30702";

            var worker = new RecordingHandshakeWorker();
            var profile = new RecordingProfile(990701, genesisHash, worker);

            var config = new ChainNodeConfig();
            config.Storage.InMemory = true;
            config.Network.TrustedPeers = new[] { validPeer, malformedPeer };
            config.Network.MaxConcurrentDials = 2;
            config.Network.DialBudgetPerSecond = 0;

            var loggerFactory = new RecordingLoggerFactory();

            var stack = await ChainNodeSyncStack.StartAsync(profile, bundle, config, loggerFactory: loggerFactory);
            try
            {
                Assert.Contains(loggerFactory.Warnings, w => w.Contains(malformedPeer));
            }
            finally
            {
                await stack.DisposeAsync();
                await bundle.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_ADevP2PPeersListWithOneMalformedEnode_When_TheNodeStarts_Then_TheOtherValidEnodesAreStillDialled()
        {
            var genesisHash = Make32(0x71);
            var bundle = await GenesisBundleAsync(genesisHash);

            var validPeer = $"enode://{EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex()}@127.0.0.1:30711";
            const string malformedPeer = "enode://not-a-real-pubkey@127.0.0.1:30712";

            var worker = new RecordingHandshakeWorker();
            var profile = new RecordingProfile(990702, genesisHash, worker);

            var config = new ChainNodeConfig();
            config.Storage.InMemory = true;
            config.Network.TrustedPeers = new[] { validPeer, malformedPeer };
            config.Network.MaxConcurrentDials = 2;
            config.Network.DialBudgetPerSecond = 0;

            var loggerFactory = new RecordingLoggerFactory();

            var stack = await ChainNodeSyncStack.StartAsync(profile, bundle, config, loggerFactory: loggerFactory);
            try
            {
                var dialed = await WaitForAllDialsAsync(worker, new[] { validPeer, malformedPeer }, TimeSpan.FromSeconds(10));

                Assert.Contains(validPeer, dialed);
                Assert.Contains(malformedPeer, dialed);
            }
            finally
            {
                await stack.DisposeAsync();
                await bundle.DisposeAsync();
            }
        }

        private sealed class RecordingLoggerFactory : ILoggerFactory
        {
            public readonly ConcurrentQueue<string> Warnings = new();

            public ILogger CreateLogger(string categoryName) => new RecordingLogger(Warnings);

            public void AddProvider(ILoggerProvider provider)
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class RecordingLogger : ILogger
        {
            private readonly ConcurrentQueue<string> _warnings;

            public RecordingLogger(ConcurrentQueue<string> warnings)
            {
                _warnings = warnings;
            }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter)
            {
                if (logLevel == LogLevel.Warning)
                    _warnings.Enqueue(formatter(state, exception));
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();

                public void Dispose()
                {
                }
            }
        }
    }
}
