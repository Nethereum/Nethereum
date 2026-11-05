using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Server;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests
{
    public class AppChainFollowerLatencyTests
    {
        private const int Blocks = 40;
        private static readonly TimeSpan PerBlockTimeout = TimeSpan.FromSeconds(10);

        private readonly ITestOutputHelper _output;

        public AppChainFollowerLatencyTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public async Task Given_AnAppChainSequencerAndAFollower_When_TheSequencerProducesBlocksOneByOne_Then_EachBlockReachesTheFollowerWithinTheTimeout()
        {
            var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning));
            var chainId = new BigInteger(420450_000 + Environment.TickCount % 1000);
            var ownerKey = EthECKey.GenerateKey();
            var sequencerKey = EthECKey.GenerateKey();
            var producerNodeKey = EthECKey.GenerateKey();
            var followerNodeKey = EthECKey.GenerateKey();
            var ports = ReserveFreeLoopbackPorts(2);

            AppChainComposedNode? producer = null;
            AppChainComposedNode? follower = null;
            try
            {
                producer = await AppChainComposition.ComposeAsync(
                    BuildConfig(chainId, ownerKey, sequencerKey, true, producerNodeKey, ports[0], Array.Empty<string>()),
                    loggerFactory, CancellationToken.None);
                follower = await AppChainComposition.ComposeAsync(
                    BuildConfig(chainId, ownerKey, sequencerKey, false, followerNodeKey, ports[1],
                        new[] { EnodeOf(producerNodeKey, ports[0]) }),
                    loggerFactory, CancellationToken.None);

                Assert.True(await WaitUntilAsync(
                    () => Task.FromResult((follower.ChainNode.Sync?.Pool.ActivePeers.Count ?? 0) >= 1),
                    TimeSpan.FromSeconds(30)), "the follower never peered with the sequencer");

                await producer.Sequencer!.ProduceBlockAsync();
                Assert.True(await WaitUntilAsync(
                    async () => await follower.Bundle.Blocks.GetHeightAsync() >= 1, PerBlockTimeout),
                    "the follower never imported the first block");

                var latenciesMs = new List<double>();
                for (var n = 2; n <= Blocks + 1; n++)
                {
                    await producer.Sequencer!.ProduceBlockAsync();
                    var sw = Stopwatch.StartNew();
                    var deadline = DateTime.UtcNow + PerBlockTimeout;
                    while (await follower.Bundle.Blocks.GetHeightAsync() < n && DateTime.UtcNow < deadline)
                        await Task.Yield();

                    Assert.True(await follower.Bundle.Blocks.GetHeightAsync() >= n, $"block {n} never reached the follower");
                    latenciesMs.Add(sw.Elapsed.TotalMilliseconds);
                }

                var sorted = latenciesMs.OrderBy(x => x).ToList();
                _output.WriteLine(
                    $"follower latency over {sorted.Count} blocks: min={sorted[0]:F1}ms median={sorted[sorted.Count / 2]:F1}ms " +
                    $"p95={sorted[(int)(sorted.Count * 0.95)]:F1}ms max={sorted[^1]:F1}ms mean={sorted.Average():F1}ms");
            }
            finally
            {
                if (follower != null) await follower.DisposeAsync();
                if (producer != null) await producer.DisposeAsync();
            }
        }

        private static AppChainServerConfig BuildConfig(
            BigInteger chainId, EthECKey ownerKey, EthECKey sequencerKey, bool hasSigningKey,
            EthECKey nodeKey, int listenPort, string[] trustedPeers)
        {
            var config = new AppChainServerConfig { ChainId = chainId, ChainName = "AppChainFollowerLatencyE2E" };
            config.Genesis.Owner.PrivateKey = ownerKey.GetPrivateKey();
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

        private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await condition().ConfigureAwait(false)) return true;
                await Task.Delay(100).ConfigureAwait(false);
            }
            return await condition().ConfigureAwait(false);
        }
    }
}
