using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv4;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class Discv4FindNodeSecurityTests
    {
        private static byte[] SampleEnr() => new byte[] { 0xc2, 0x01, 0x02 };

        private static byte[] ExpirationElement(long unix)
            => RLP.RLP.EncodeElement(BitConverter.GetBytes(unix).Reverse().SkipWhile(b => b == 0).ToArray());

        private static Discv4Listener StartListener(List<string> errors = null, int tableSize = 0)
        {
            var key = EthECKey.GenerateKey();
            var routing = new Discv4RoutingTable(key.GetPubKeyNoPrefix());
            for (var i = 0; i < tableSize; i++)
            {
                routing.Add(new Discv4Node
                {
                    NodeId = EthECKey.GenerateKey().GetPubKeyNoPrefix(),
                    IP = IPAddress.Parse($"203.0.{i + 1}.1"),
                    UdpPort = 30303,
                    TcpPort = 30303,
                    LastSeen = DateTime.UtcNow
                });
            }
            var listener = new Discv4Listener(key, routing) { LocalEnrEncoded = SampleEnr() };
            if (errors != null)
                listener.ErrorOccurred += (_, e) => { lock (errors) errors.Add($"{e.Phase}: {e.Exception.GetType().Name}: {e.Exception.Message}"); };
            listener.Start(0, IPAddress.Loopback);
            return listener;
        }

        private static byte[] FindNodePacket(Discv4RawPeer peer)
        {
            var findNode = new Discv4FindNodeMessage { Target = EthECKey.GenerateKey().GetPubKeyNoPrefix(), Expiration = Discv4RawPeer.Future() };
            return Discv4RawPeer.BuildPacket(peer.Key, Discv4MessageType.FindNode, Discv4MessageEncoder.EncodeFindNode(findNode));
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_RoutingTableWithSixteenNodes_When_BondedPeerSendsFindNode_Then_NeighborsRepliesFitTheDatagramLimit()
        {
            var errors = new List<string>();
            using var listener = StartListener(errors, tableSize: 16);
            Assert.Equal(16, listener.Routing.Count);
            using var peer = new Discv4RawPeer();
            var target = new IPEndPoint(IPAddress.Loopback, listener.Port);
            Assert.True(await peer.BondAsync(target));

            await peer.SendAsync(FindNodePacket(peer), target);
            var received = await peer.CollectAsync(TimeSpan.FromSeconds(2));

            var neighbors = received.Where(r => r.Packet.Type == Discv4MessageType.Neighbors).ToList();
            string listenerErrors;
            lock (errors) listenerErrors = string.Join(" | ", errors);
            Assert.True(neighbors.Count > 0, $"a bonded peer's FINDNODE got no NEIGHBORS reply; listener errors: {listenerErrors}");
            Assert.All(neighbors, n => Assert.True(n.Length <= Discv4Packet.MaxPacketSize));
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_RoutingTableWithTwelveNodes_When_BondedPeerSendsFindNode_Then_NeighborsReplyArrives()
        {
            using var listener = StartListener(tableSize: 12);
            using var peer = new Discv4RawPeer();
            var target = new IPEndPoint(IPAddress.Loopback, listener.Port);
            Assert.True(await peer.BondAsync(target));

            await peer.SendAsync(FindNodePacket(peer), target);
            var received = await peer.CollectAsync(TimeSpan.FromSeconds(2));

            Assert.Contains(received, r => r.Packet.Type == Discv4MessageType.Neighbors);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_UnbondedPeer_When_ItSendsFindNode_Then_NoNeighborsAreSent()
        {
            using var listener = StartListener(tableSize: 5);
            using var peer = new Discv4RawPeer();
            var target = new IPEndPoint(IPAddress.Loopback, listener.Port);

            await peer.SendAsync(FindNodePacket(peer), target);
            var received = await peer.CollectAsync(TimeSpan.FromSeconds(1));

            Assert.DoesNotContain(received, r => r.Packet.Type == Discv4MessageType.Neighbors);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_PingDatagramLargerThanThe1280ByteLimit_When_ListenerReadsIt_Then_ItIsDiscarded()
        {
            using var listener = StartListener();
            var raised = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PingReceived += (_, _) => raised.TrySetResult(true);
            using var attacker = new Discv4RawPeer();
            var target = new IPEndPoint(IPAddress.Loopback, listener.Port);
            var ping = attacker.NewPing(target);
            var data = RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(new byte[] { 4 }),
                ping.From.Encode(),
                ping.To.Encode(),
                ExpirationElement(ping.Expiration),
                RLP.RLP.EncodeElement(new byte[2000]));
            var packet = Discv4RawPeer.BuildPacket(attacker.Key, Discv4MessageType.Ping, data);
            Assert.True(packet.Length > Discv4Packet.MaxPacketSize);

            await attacker.SendAsync(packet, target);
            var completed = await Task.WhenAny(raised.Task, Task.Delay(1000));

            Assert.False(completed == raised.Task,
                $"a {packet.Length}-byte PING was processed (discv4.md: packets over 1280 bytes are invalid; geth v4_udp.go:541 reads into a 1280-byte buffer so the hash fails)");
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_BondedPeer_When_ItSendsACurrentEnrRequest_Then_EnrResponseArrives()
        {
            using var listener = StartListener();
            using var peer = new Discv4RawPeer();
            var target = new IPEndPoint(IPAddress.Loopback, listener.Port);
            Assert.True(await peer.BondAsync(target));

            var data = RLP.RLP.EncodeList(ExpirationElement(Discv4RawPeer.Future()));
            await peer.SendAsync(Discv4RawPeer.BuildPacket(peer.Key, Discv4MessageType.EnrRequest, data), target);
            var received = await peer.CollectAsync(TimeSpan.FromSeconds(1));

            Assert.Contains(received, r => r.Packet.Type == Discv4MessageType.EnrResponse);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_BondedPeer_When_ItSendsAnExpiredEnrRequest_Then_NoEnrResponseIsSent()
        {
            using var listener = StartListener();
            using var peer = new Discv4RawPeer();
            var target = new IPEndPoint(IPAddress.Loopback, listener.Port);
            Assert.True(await peer.BondAsync(target));

            var expired = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
            var data = RLP.RLP.EncodeList(ExpirationElement(expired));
            await peer.SendAsync(Discv4RawPeer.BuildPacket(peer.Key, Discv4MessageType.EnrRequest, data), target);
            var received = await peer.CollectAsync(TimeSpan.FromSeconds(1));

            Assert.DoesNotContain(received, r => r.Packet.Type == Discv4MessageType.EnrResponse);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_BondedPeer_When_ItSendsAnEnrRequestWithGarbagePayload_Then_NoEnrResponseIsSent()
        {
            using var listener = StartListener();
            using var peer = new Discv4RawPeer();
            var target = new IPEndPoint(IPAddress.Loopback, listener.Port);
            Assert.True(await peer.BondAsync(target));

            await peer.SendAsync(Discv4RawPeer.BuildPacket(peer.Key, Discv4MessageType.EnrRequest, new byte[] { 0xff, 0xff, 0xff }), target);
            var received = await peer.CollectAsync(TimeSpan.FromSeconds(1));

            Assert.DoesNotContain(received, r => r.Packet.Type == Discv4MessageType.EnrResponse);
        }
    }
}
