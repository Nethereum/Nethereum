using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class Discv5FindNodeSecurityTests
    {
        private static async Task<(Discv5Listener Listener, Discv5RawPeer Peer)> SessionAsync()
        {
            var key = EthECKey.GenerateKey();
            var listener = new Discv5Listener(key);
            listener.Start(IPAddress.Loopback, 0);
            listener.LocalEnrEncoded = Discv5RawPeer.SignEnr(key, IPAddress.Loopback, listener.Port, 1);
            var peer = new Discv5RawPeer(new IPEndPoint(IPAddress.Loopback, listener.Port), key);
            var ping = Discv5MessageEncoder.EncodePing(new Discv5PingMessage { RequestId = new byte[] { 1 }, EnrSeq = 1 });
            Assert.NotNull(await peer.EstablishSessionAsync(ping));
            return (listener, peer);
        }

        private static async Task<int> RecordsReturnedFor(Discv5RawPeer peer, params ulong[] distances)
        {
            var request = new Discv5FindNodeMessage { RequestId = new byte[] { 7 } };
            request.Distances.AddRange(distances);
            await peer.SendMessageAsync(Discv5MessageEncoder.EncodeFindNode(request));
            var messages = await peer.CollectMessagesAsync(TimeSpan.FromSeconds(1));
            return messages
                .Select(Discv5MessageEncoder.Unpack)
                .Where(m => m.type == Discv5MessageType.Nodes)
                .Sum(m => Discv5MessageEncoder.DecodeNodes(m.body).Records.Count);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_Session_When_FindNodeAsksForDistanceZero_Then_ExactlyTheLocalRecordIsReturned()
        {
            var (listener, peer) = await SessionAsync();
            await using var _ = listener;
            using var __ = peer;

            Assert.Equal(1, await RecordsReturnedFor(peer, 0));
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_Session_When_FindNodeAsksForADistanceAbove256_Then_NoRecordIsReturned()
        {
            var (listener, peer) = await SessionAsync();
            await using var _ = listener;
            using var __ = peer;

            var nodeKey = EthECKey.GenerateKey();
            var nodeId = Discv5Crypto.ComputeNodeId(nodeKey.GetPubKeyNoPrefix());
            while (Discv5RoutingTable.LogDistance(listener.NodeId, nodeId) != 256)
            {
                nodeKey = EthECKey.GenerateKey();
                nodeId = Discv5Crypto.ComputeNodeId(nodeKey.GetPubKeyNoPrefix());
            }
            listener.Routing.Upsert(new Discv5RoutingTable.Entry
            {
                NodeId = nodeId,
                Address = new IPEndPoint(IPAddress.Parse("8.8.4.4"), 30303),
                EnrEncoded = Discv5RawPeer.SignEnr(nodeKey, IPAddress.Parse("8.8.4.4"), 30303, 1)
            });

            var returned = await RecordsReturnedFor(peer, 4294967296UL + 256UL);

            Assert.True(returned == 0,
                $"distance 2^32+256 was truncated to 256 and answered with {returned} record(s) (geth v5_udp.go collectTableNodes skips dist > 256)");
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_Session_When_FindNodeRepeatsTheSameDistanceManyTimes_Then_EachRecordIsReturnedOnce()
        {
            var (listener, peer) = await SessionAsync();
            await using var _ = listener;
            using var __ = peer;

            var returned = await RecordsReturnedFor(peer, Enumerable.Repeat(0UL, 40).ToArray());

            Assert.True(returned <= 1,
                $"40 copies of distance 0 returned {returned} records (geth v5_udp.go collectTableNodes ignores duplicate distances)");
        }
    }
}
