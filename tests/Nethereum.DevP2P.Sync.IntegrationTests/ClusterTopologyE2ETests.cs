using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.IntegrationTests.Harness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.Sync.IntegrationTests
{
    public class ClusterTopologyE2ETests
    {
        private readonly ITestOutputHelper _output;

        public ClusterTopologyE2ETests(ITestOutputHelper output) => _output = output;

        [Fact]
        public async Task Given_AThreeNodeCluster_When_Started_Then_EveryNodeHoldsASessionToEveryOtherNode()
        {
            var nodes = (await DevChainNetwork.TrustedClusterAsync(3)).ToList();
            try
            {
                foreach (var node in nodes)
                {
                    var peers = nodes.Where(other => !ReferenceEquals(other, node)).ToList();

                    foreach (var peer in peers)
                    {
                        var connected = await DevChainNetwork.WaitUntilAsync(
                            () => node.OutboundSessionTo(peer) != null,
                            TimeSpan.FromSeconds(15));

                        Assert.True(connected,
                            $"node {nodes.IndexOf(node)} has no outbound session to node {nodes.IndexOf(peer)}");
                    }
                }

                var sessions = nodes.Sum(n => nodes.Count(other =>
                    !ReferenceEquals(other, n) && n.OutboundSessionTo(other) != null));

                Assert.Equal(nodes.Count * (nodes.Count - 1), sessions);
                _output.WriteLine($"full mesh: {sessions} outbound sessions across {nodes.Count} nodes");
            }
            finally
            {
                foreach (var node in nodes) await node.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_AThreeNodeCluster_When_OneNodeSealsABlock_Then_EveryOtherNodeReceivesThatBlock()
        {
            var nodes = (await DevChainNetwork.TrustedClusterAsync(3)).ToList();
            try
            {
                var sealer = nodes[0];
                var others = nodes.Skip(1).ToList();
                var producer = await DevChainProducer.AttachAsync(sealer);

                var produced = await producer.ProduceAsync();
                Assert.NotNull(produced.BlockHash);
                _output.WriteLine($"node 0 sealed block {produced.Header.BlockNumber} hash=0x{produced.BlockHash.ToHex()}");

                foreach (var node in others)
                {
                    var received = await DevChainNetwork.WaitUntilAsync(
                        () => node.ObservedBlocks.Any(b =>
                            ByteUtil.AreEqual(BlockHashOf(b), produced.BlockHash)),
                        TimeSpan.FromSeconds(15));

                    Assert.True(received,
                        $"node {nodes.IndexOf(node)} never received the block sealed by node 0 " +
                        $"(observed {node.ObservedBlocks.Count} block(s))");
                }

                _output.WriteLine($"all {others.Count} sibling(s) received the sealed block");
            }
            finally
            {
                foreach (var node in nodes) await node.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_ThreeNodesWhoseOnlyLinkIsNetworkTrustedPeers_When_TheyStart_Then_EachHoldsAnOutboundSessionToEveryOther()
        {
            var nodes = (await DevChainNetwork.ConfiguredClusterAsync(3)).ToList();
            try
            {
                foreach (var node in nodes)
                {
                    var peers = nodes.Where(other => !ReferenceEquals(other, node)).ToList();

                    foreach (var peer in peers)
                    {
                        var connected = await DevChainNetwork.WaitUntilAsync(
                            () => node.OutboundSessionTo(peer) != null,
                            TimeSpan.FromSeconds(15));

                        Assert.True(connected,
                            $"node {nodes.IndexOf(node)} has no outbound session to node {nodes.IndexOf(peer)} " +
                            "(configured only via Network.TrustedPeers — nothing in this test dials)");
                    }
                }

                var sessions = nodes.Sum(n => nodes.Count(other =>
                    !ReferenceEquals(other, n) && n.OutboundSessionTo(other) != null));

                Assert.Equal(nodes.Count * (nodes.Count - 1), sessions);
                _output.WriteLine($"config-driven full mesh: {sessions} outbound sessions across {nodes.Count} nodes");
            }
            finally
            {
                foreach (var node in nodes) await node.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_ThreeNodesWithTrustedNodeIdsButNoTrustedPeers_When_TheyStart_Then_NoSessionForms()
        {
            var keys = Enumerable.Range(0, 3).Select(_ => Nethereum.Signer.EthECKey.GenerateKey()).ToList();
            var nodeIds = keys.Select(DevChainNode.NodeIdOf).ToList();

            var nodes = keys
                .Select((key, index) => DevChainNode.CreateConfigured(
                    key,
                    listenPort: 0,
                    trustedPeers: Array.Empty<string>(),
                    trustedNodeIds: nodeIds.Where((_, other) => other != index).ToArray()))
                .ToList();

            try
            {
                foreach (var node in nodes)
                    await node.StartAsync();

                foreach (var node in nodes)
                {
                    var peers = nodes.Where(other => !ReferenceEquals(other, node)).ToList();

                    var neverConnected = await DevChainNetwork.StaysFalseAsync(
                        () => peers.Any(peer => node.OutboundSessionTo(peer) != null),
                        TimeSpan.FromSeconds(5));

                    Assert.True(neverConnected,
                        $"node {nodes.IndexOf(node)} formed an outbound session despite an empty " +
                        "Network.TrustedPeers — trust (TrustedNodeIds) must not, by itself, dial anyone");
                }
            }
            finally
            {
                foreach (var node in nodes) await node.DisposeAsync();
            }
        }

        private static byte[] BlockHashOf(Nethereum.Model.P2P.NewBlockMessage message) =>
            Nethereum.CoreChain.BlockHashCalculator.ForHeader(message.Header);
    }
}
