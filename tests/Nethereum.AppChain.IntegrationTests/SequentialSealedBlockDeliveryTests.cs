using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Server;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests
{
    public class SequentialSealedBlockDeliveryTests
    {
        private readonly ITestOutputHelper _output;

        public SequentialSealedBlockDeliveryTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Given_TwoConnectedNodesOverOneLiveFollowerSession_When_TheProducerSealsSeveralBlocksInSequence_Then_TheFollowerReachesEveryHeight()
        {
            const int blocksToProduce = 6;
            var perBlockTimeout = TimeSpan.FromSeconds(20);

            var signRecoverableBeforeTest = EthECKey.SignRecoverable;
            var log = new List<string>();

            try
            {
                var chainId = new BigInteger(420420_700 + Environment.TickCount % 1000);
                var genesisOwnerKey = EthECKey.GenerateKey();
                var sequencerKey = EthECKey.GenerateKey();

                var producerNodeKey = EthECKey.GenerateKey();
                var followerNodeKey = EthECKey.GenerateKey();
                var ports = ReserveFreeLoopbackPorts(2);
                var producerPort = ports[0];
                var followerPort = ports[1];
                var producerEnode = EnodeOf(producerNodeKey, producerPort);

                var producerConfig = BuildConfig(
                    chainId, genesisOwnerKey, sequencerKey, hasSigningKey: true,
                    producerNodeKey, producerPort, trustedPeers: Array.Empty<string>());

                var followerConfig = BuildConfig(
                    chainId, genesisOwnerKey, sequencerKey, hasSigningKey: false,
                    followerNodeKey, followerPort, trustedPeers: new[] { producerEnode });

                var loggerFactory = NullLoggerFactory.Instance;
                var lifetime = new CancellationTokenSource();

                await using var producer = await AppChainComposition.ComposeAsync(producerConfig, loggerFactory, lifetime.Token);
                await using var follower = await AppChainComposition.ComposeAsync(followerConfig, loggerFactory, lifetime.Token);

                Assert.NotNull(producer.Sequencer);
                Assert.Null(follower.Sequencer);

                var sealedAt = new Dictionary<ulong, (int SyncPoolPeers, int BroadcastPoolPeers)>();
                producer.Sequencer!.BlockProduced += (_, result) =>
                {
                    var height = (ulong)result.Header.BlockNumber;
                    var syncPeers = producer.ChainNode.Sync?.Pool?.ActivePeers?.Count ?? -1;
                    var broadcastPeers = producer.ChainNode.Mempool?.BroadcastPool?.Count ?? -1;
                    sealedAt[height] = (syncPeers, broadcastPeers);
                };

                var peered = await WaitUntilAsync(
                    () => (producer.ChainNode.Mempool?.BroadcastPool?.Count ?? 0) >= 1
                          && (follower.ChainNode.Sync?.Pool?.ActivePeers?.Count ?? 0) >= 1,
                    TimeSpan.FromSeconds(45));
                Assert.True(
                    peered,
                    "producer and follower never connected to each other " +
                    $"(producerBroadcastPoolPeers={producer.ChainNode.Mempool?.BroadcastPool?.Count ?? -1}, " +
                    $"producerSyncPoolPeers={producer.ChainNode.Sync?.Pool?.ActivePeers?.Count ?? -1}, " +
                    $"followerSyncPoolPeers={follower.ChainNode.Sync?.Pool?.ActivePeers?.Count ?? -1}, " +
                    $"followerBroadcastPoolPeers={follower.ChainNode.Mempool?.BroadcastPool?.Count ?? -1}, " +
                    $"producerListening={producer.ChainNode.Listener != null}, " +
                    $"followerListening={follower.ChainNode.Listener != null})");

                var producedHashes = new Dictionary<ulong, byte[]>();
                var reachedHeights = new HashSet<ulong>();

                for (var i = 1; i <= blocksToProduce; i++)
                {
                    var height = (ulong)i;
                    var sealedHash = await producer.Sequencer!.ProduceBlockAsync();
                    producedHashes[height] = sealedHash;

                    var (sealSyncPeers, sealBroadcastPeers) = sealedAt.TryGetValue(height, out var seal)
                        ? seal
                        : (-1, -1);

                    var reached = await WaitUntilAsync(
                        async () => await follower.Bundle.Blocks.GetHeightAsync() >= new BigInteger(height),
                        perBlockTimeout);

                    var followerSyncPeers = follower.ChainNode.Sync?.Pool?.ActivePeers?.Count ?? -1;
                    var followerBroadcastPeers = follower.ChainNode.Mempool?.BroadcastPool?.Count ?? -1;
                    var followerHeight = await follower.Bundle.Blocks.GetHeightAsync();

                    if (reached) reachedHeights.Add(height);

                    log.Add(
                        $"height={height} sealedHash={sealedHash.ToHex()} " +
                        $"producerPeersAtSeal(sync={sealSyncPeers},broadcast={sealBroadcastPeers}) " +
                        $"followerReached={reached} followerHeightNow={followerHeight} " +
                        $"followerPeersNow(sync={followerSyncPeers},broadcast={followerBroadcastPeers})");
                }

                _output.WriteLine("Per-block delivery log:");
                foreach (var line in log) _output.WriteLine(line);

                var missed = new List<ulong>();
                for (var i = 1; i <= blocksToProduce; i++)
                {
                    var height = (ulong)i;
                    if (!reachedHeights.Contains(height)) { missed.Add(height); continue; }

                    var followerHash = await follower.Bundle.Blocks.GetHashByNumberAsync(new BigInteger(height));
                    if (followerHash == null || !ByteUtil.AreEqual(followerHash, producedHashes[height]))
                        missed.Add(height);
                }

                Assert.True(
                    missed.Count == 0,
                    $"follower never reached (or mismatched) heights [{string.Join(",", missed)}] " +
                    $"of {blocksToProduce} sequential blocks over one live follower session. " +
                    "Full per-block log written to test output.");

                lifetime.Cancel();
            }
            finally
            {
                EthECKey.SignRecoverable = signRecoverableBeforeTest;
            }
        }

        [Fact(Skip = "DEFECT-APPCHAIN-FOLLOWER-IMPORTS-AT-A-HARDCODED-FORK: the follower activates prague regardless of the chain's pinned fork, so every cross-signer import is wrong-fork")]
        public async Task Given_AThreeNodeCliqueFullMesh_When_ABlockIsMissed_Then_TheFollowerPipelineIsObservedViaLogs()
        {
            const int size = 3;
            const int blocksToProduce = 5;
            var perBlockTimeout = TimeSpan.FromSeconds(15);

            var signRecoverableBeforeTest = EthECKey.SignRecoverable;
            var log = new List<string>();

            try
            {
                var chainId = new BigInteger(420420_900 + Environment.TickCount % 1000);
                var lifetime = new CancellationTokenSource();

                var genesisOwnerKey = EthECKey.GenerateKey();
                var sharedSequencerKey = EthECKey.GenerateKey();
                var cliqueKeys = Enumerable.Range(0, size).Select(_ => EthECKey.GenerateKey()).ToList();
                var initialSigners = cliqueKeys.Select(k => k.GetPublicAddress()).ToArray();
                var nodeKeys = Enumerable.Range(0, size).Select(_ => EthECKey.GenerateKey()).ToList();
                var ports = ReserveFreeLoopbackPorts(size);
                var enodes = nodeKeys.Select((key, index) => EnodeOf(key, ports[index])).ToList();

                var nodes = new List<Nethereum.AppChain.Server.AppChainComposedNode>();
                for (var i = 0; i < size; i++)
                {
                    var trustedPeers = Enumerable.Range(0, size)
                        .Where(other => other != i)
                        .Select(other => enodes[other])
                        .ToArray();

                    var config = new AppChainServerConfig
                    {
                        ChainId = chainId,
                        ChainName = "SequentialSealedBlockDeliveryCliqueE2E"
                    };
                    config.Genesis.Owner.PrivateKey = genesisOwnerKey.GetPrivateKey();
                    config.Consensus.Sequencer.PrivateKey = sharedSequencerKey.GetPrivateKey();
                    config.Consensus.Mode = AppChainConsensusMode.Clique;
                    config.Consensus.Clique.Signer.PrivateKey = cliqueKeys[i].GetPrivateKey();
                    config.Consensus.Clique.InitialSigners = initialSigners;
                    config.Consensus.Clique.PeriodSeconds = 1;
                    config.Consensus.Clique.EpochLength = 30000;
                    config.Consensus.AllowEmptyBlocks = true;
                    config.Consensus.BlockTimeMs = 0;
                    config.Consensus.BlockProductionMode = BlockProductionMode.OnDemand;
                    config.Node.Storage.InMemory = true;
                    config.Node.Network.Serve = true;
                    config.Node.Network.NodeKeyHex = nodeKeys[i].GetPrivateKey();
                    config.Node.Network.ListenPort = ports[i];
                    config.Node.Network.BindAddress = IPAddress.Loopback;
                    config.Node.Network.TrustedPeers = trustedPeers;
                    config.Node.Network.TargetPeerCount = trustedPeers.Length;
                    config.Node.Network.DialBudgetPerSecond = 0;
                    config.Node.Network.MaxPeersPerIPv4Subnet = 0;
                    config.Node.Network.MaxPeersPerIPv6Subnet = 0;
                    config.Node.Network.MaxInboundPeers = size + 1;
                    config.Node.Network.MaxInboundPerIP = size + 1;
                    config.Node.Sync.Mode = SyncMode.ForwardExecute;
                    config.Mud.DeployWorld = false;

                    var nodeLoggerFactory = new TestOutputLoggerFactory(_output, $"node{i}");
                    nodes.Add(await AppChainComposition.ComposeAsync(config, nodeLoggerFactory, lifetime.Token));
                }

                try
                {
                    var sealedAt = new Dictionary<(int Node, ulong Height), (int SyncPoolPeers, int BroadcastPoolPeers)>();
                    for (var i = 0; i < size; i++)
                    {
                        var nodeIndex = i;
                        nodes[nodeIndex].Sequencer!.BlockProduced += (_, result) =>
                        {
                            var height = (ulong)result.Header.BlockNumber;
                            var syncPeers = nodes[nodeIndex].ChainNode.Sync?.Pool?.ActivePeers?.Count ?? -1;
                            var broadcastPeers = nodes[nodeIndex].ChainNode.Mempool?.BroadcastPool?.Count ?? -1;
                            sealedAt[(nodeIndex, height)] = (syncPeers, broadcastPeers);
                        };
                    }

                    var pushesReceived = new ConcurrentBag<(int Node, ulong Height, string FromEnode, DateTime AtUtc)>();
                    for (var i = 0; i < size; i++)
                    {
                        var nodeIndex = i;
                        var pool = nodes[nodeIndex].ChainNode.Sync?.Pool;
                        if (pool == null) continue;

                        void WireSession(SyncPeerSession session) =>
                            session.NewBlockReceived += (_, msg) =>
                                pushesReceived.Add((
                                    nodeIndex,
                                    (ulong)msg.Header.BlockNumber,
                                    session.PeerEnode,
                                    DateTime.UtcNow));

                        foreach (var existing in pool.ActivePeers.OfType<SyncPeerSession>())
                            WireSession(existing);
                        pool.PeerAdded += (_, added) =>
                        {
                            if (added is SyncPeerSession session) WireSession(session);
                        };
                    }

                    var expectedPeers = size - 1;
                    var peered = await WaitUntilAsync(
                        () => nodes.All(n => (n.ChainNode.Sync?.Pool?.ActivePeers?.Count ?? 0) >= expectedPeers),
                        TimeSpan.FromSeconds(45));
                    Assert.True(
                        peered,
                        "cluster never reached full mesh: " +
                        string.Join(", ", nodes.Select((n, i) =>
                            $"node{i}.syncPeers={n.ChainNode.Sync?.Pool?.ActivePeers?.Count ?? -1}")));

                    var reachedHeights = new HashSet<(int Node, ulong Height)>();

                    for (var b = 1; b <= blocksToProduce; b++)
                    {
                        var height = (ulong)b;
                        var producerIndex = (b - 1) % size;
                        var sealedHash = await nodes[producerIndex].Sequencer!.ProduceBlockAsync();

                        for (var n = 0; n < size; n++)
                        {
                            var reached = await WaitUntilAsync(
                                async () => await nodes[n].Bundle.Blocks.GetHeightAsync() >= new BigInteger(height),
                                perBlockTimeout);

                            var nodeHeight = await nodes[n].Bundle.Blocks.GetHeightAsync();
                            var nodeHash = reached
                                ? await nodes[n].Bundle.Blocks.GetHashByNumberAsync(new BigInteger(height))
                                : null;
                            var matches = reached && nodeHash != null && ByteUtil.AreEqual(nodeHash, sealedHash);

                            if (matches) reachedHeights.Add((n, height));

                            var (sealSyncPeers, sealBroadcastPeers) = sealedAt.TryGetValue((producerIndex, height), out var seal)
                                ? seal
                                : (-1, -1);
                            var nodeSyncPeersNow = nodes[n].ChainNode.Sync?.Pool?.ActivePeers?.Count ?? -1;
                            var nodeBroadcastPeersNow = nodes[n].ChainNode.Mempool?.BroadcastPool?.Count ?? -1;
                            var nodePendingCount = nodes[n].ChainNode.Sync?.PushedBlocks?.PendingCount ?? -1;
                            var nodeCaughtUp = (nodes[n].ChainNode.Sync?.BlockSource as CatchUpThenFollowBlockSource)?.CaughtUp;
                            var nodePushesSoFar = pushesReceived.Count(p => p.Node == n);
                            var nodePushesThisHeight = pushesReceived.Where(p => p.Node == n && p.Height == height)
                                .Select(p => p.FromEnode).ToList();

                            log.Add(
                                $"height={height} producer=node{producerIndex} sealedHash={sealedHash.ToHex()} " +
                                $"producerPeersAtSeal(sync={sealSyncPeers},broadcast={sealBroadcastPeers}) " +
                                $"observer=node{n} reached={reached} matches={matches} nodeHeightNow={nodeHeight} " +
                                $"nodePeersNow(sync={nodeSyncPeersNow},broadcast={nodeBroadcastPeersNow}) " +
                                $"pendingCount={nodePendingCount} caughtUp={nodeCaughtUp} " +
                                $"pushesReceivedSoFar={nodePushesSoFar} pushesAtThisHeight=[{string.Join(",", nodePushesThisHeight)}]");
                        }
                    }

                    _output.WriteLine("Per-block, per-node delivery log:");
                    foreach (var line in log) _output.WriteLine(line);

                    _output.WriteLine("");
                    _output.WriteLine("Raw push-received log (node, height, fromEnode, atUtc):");
                    foreach (var p in pushesReceived.OrderBy(p => p.AtUtc))
                        _output.WriteLine($"node{p.Node} height={p.Height} from={p.FromEnode} at={p.AtUtc:O}");

                    _output.WriteLine("");
                    for (var n = 0; n < size; n++)
                    {
                        var heights = pushesReceived.Where(p => p.Node == n).Select(p => p.Height).OrderBy(h => h).ToList();
                        _output.WriteLine($"node{n} total pushes received={heights.Count} heights=[{string.Join(",", heights)}]");
                    }

                    var missed = new List<string>();
                    for (var b = 1; b <= blocksToProduce; b++)
                        for (var n = 0; n < size; n++)
                            if (!reachedHeights.Contains((n, (ulong)b)))
                                missed.Add($"node{n}@height{b}");

                    Assert.True(
                        missed.Count == 0,
                        $"[{string.Join(",", missed)}] never reached (or mismatched) their expected height " +
                        $"across {blocksToProduce} sequential blocks over one live follower session in a " +
                        $"{size}-node full-mesh Clique cluster. Full per-block/per-node log written to test output.");
                }
                finally
                {
                    lifetime.Cancel();
                    foreach (var node in nodes)
                        try { await node.DisposeAsync(); } catch { }
                }
            }
            finally
            {
                EthECKey.SignRecoverable = signRecoverableBeforeTest;
            }
        }

        private static AppChainServerConfig BuildConfig(
            BigInteger chainId, EthECKey genesisOwnerKey, EthECKey sequencerKey, bool hasSigningKey,
            EthECKey nodeKey, int listenPort, string[] trustedPeers)
        {
            var config = new AppChainServerConfig
            {
                ChainId = chainId,
                ChainName = "SequentialSealedBlockDeliveryE2E"
            };
            config.Genesis.Owner.PrivateKey = genesisOwnerKey.GetPrivateKey();
            config.Consensus.Mode = AppChainConsensusMode.SingleSequencer;
            config.Consensus.AllowEmptyBlocks = true;
            config.Consensus.BlockTimeMs = 0;
            config.Consensus.BlockProductionMode = BlockProductionMode.OnDemand;
            if (hasSigningKey)
                config.Consensus.Sequencer.PrivateKey = sequencerKey.GetPrivateKey();
            else
                config.Consensus.Sequencer.Address = sequencerKey.GetPublicAddress();

            config.Node.Storage.InMemory = true;
            config.Node.Network.Serve = true;
            config.Node.Network.NodeKeyHex = nodeKey.GetPrivateKey();
            config.Node.Network.ListenPort = listenPort;
            config.Node.Network.BindAddress = IPAddress.Loopback;
            config.Node.Network.TrustedPeers = trustedPeers;
            config.Node.Network.TargetPeerCount = Math.Max(1, trustedPeers.Length);
            config.Node.Network.DialBudgetPerSecond = 0;
            config.Node.Network.MaxPeersPerIPv4Subnet = 0;
            config.Node.Network.MaxPeersPerIPv6Subnet = 0;
            config.Node.Network.MaxInboundPeers = 4;
            config.Node.Network.MaxInboundPerIP = 4;
            config.Node.Sync.Mode = SyncMode.ForwardExecute;
            config.Mud.DeployWorld = false;

            return config;
        }

        private static string EnodeOf(EthECKey key, int port) =>
            $"enode://{key.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{port}";

        private static IReadOnlyList<int> ReserveFreeLoopbackPorts(int count)
        {
            var ports = new List<int>();
            for (var i = 0; i < count; i++)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                ports.Add(((IPEndPoint)listener.LocalEndpoint).Port);
                listener.Stop();
            }
            return ports;
        }

        private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                await Task.Delay(200);
            }
            return condition();
        }

        private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await condition().ConfigureAwait(false)) return true;
                await Task.Delay(200).ConfigureAwait(false);
            }
            return await condition().ConfigureAwait(false);
        }

        private sealed class TestOutputLoggerFactory : Microsoft.Extensions.Logging.ILoggerFactory
        {
            private readonly ITestOutputHelper _out;
            private readonly string _tag;

            public TestOutputLoggerFactory(ITestOutputHelper output, string tag)
            {
                _out = output;
                _tag = tag;
            }

            public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) =>
                new TestOutputLogger(_out, _tag, categoryName);

            public void AddProvider(Microsoft.Extensions.Logging.ILoggerProvider provider) { }

            public void Dispose() { }
        }

        private sealed class TestOutputLogger : Microsoft.Extensions.Logging.ILogger
        {
            private readonly ITestOutputHelper _out;
            private readonly string _tag;
            private readonly string _category;

            public TestOutputLogger(ITestOutputHelper output, string tag, string category)
            {
                _out = output;
                _tag = tag;
                _category = category;
            }

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) =>
                logLevel >= Microsoft.Extensions.Logging.LogLevel.Information;

            public void Log<TState>(
                Microsoft.Extensions.Logging.LogLevel logLevel,
                Microsoft.Extensions.Logging.EventId eventId,
                TState state,
                Exception exception,
                Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;
                try
                {
                    var message = $"[{_tag}][{logLevel}][{_category}] {formatter(state, exception)}";
                    if (exception != null) message += " EXCEPTION=" + exception;
                    _out.WriteLine(message);
                }
                catch { }
            }
        }
    }
}
