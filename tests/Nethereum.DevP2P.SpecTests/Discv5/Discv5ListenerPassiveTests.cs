using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv5;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv5
{
    /// <summary>
    /// Passive-listener reachability + routing-table writability checks for
    /// Stage B. The full handshake -> session-established -> routing-table-upsert
    /// path is exercised end-to-end by the integration conformance suite
    /// (Devp2pDiscv5ConformanceTests) which drives a real initiator against us.
    /// </summary>
    public class Discv5ListenerPassiveTests
    {
        [Fact]
        public async Task Given_UndecryptableOrdinaryPacket_When_Received_Then_ListenerRepliesWithWhoAreYou()
        {
            var localKey = EthECKey.GenerateKey();
            using var listener = new Discv5Listener(localKey);
            listener.Start(IPAddress.Loopback, port: 0);

            // Send a deliberately-undecryptable ordinary packet from a
            // fresh UdpClient. The session manager has no entry for our
            // pretend node id so it must reply with a WHOAREYOU challenge.
            using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            var fakeRemoteId = new byte[32];
            for (int i = 0; i < fakeRemoteId.Length; i++) fakeRemoteId[i] = (byte)(0xAA ^ i);

            var header = new Discv5Packet.Header
            {
                Flag = Discv5Packet.PacketFlag.Ordinary,
                Nonce = new byte[Discv5Packet.NonceLength],
                AuthData = fakeRemoteId,
            };
            var junkCiphertext = new byte[32];
            for (int i = 0; i < junkCiphertext.Length; i++) junkCiphertext[i] = 0xCC;
            var maskingIv = new byte[Discv5Packet.MaskingIvLength];
            for (int i = 0; i < maskingIv.Length; i++) maskingIv[i] = 0x55;

            var packet = Discv5Packet.EncodePacket(maskingIv, header, destNodeId: listener.NodeId, encryptedMessage: junkCiphertext);
            await client.SendAsync(packet, packet.Length, new IPEndPoint(IPAddress.Loopback, listener.Port));

            var receiveTask = client.ReceiveAsync();
            var completed = await Task.WhenAny(receiveTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.True(ReferenceEquals(completed, receiveTask),
                "Listener did not reply to undecryptable ordinary packet within 2s — responder loop is not reachable.");

            var reply = await receiveTask;
            var (_, replyHeader, _, _) = Discv5Packet.DecodePacket(reply.Buffer, fakeRemoteId);
            Assert.Equal(Discv5Packet.PacketFlag.WhoAreYou, replyHeader.Flag);

            await listener.StopAsync();
        }

        [Fact]
        public void Given_DnsResolvedEntry_When_Upserted_Then_RoutingTableCountIncreases()
        {
            // Stage B's DNS-seed loop writes RoutingTable entries directly from
            // resolved ENRs (no handshake needed). This isolates the writability
            // path the seed loop depends on.
            var localKey = EthECKey.GenerateKey();
            using var listener = new Discv5Listener(localKey);
            listener.Start(IPAddress.Loopback, port: 0);

            Assert.Equal(0, listener.Routing.Count);

            // Build a peer node id that is intentionally different from local
            // so the routing table accepts it (distance 0 = self is filtered).
            var peerNodeId = new byte[32];
            for (int i = 0; i < peerNodeId.Length; i++) peerNodeId[i] = (byte)(listener.NodeId[i] ^ 0xFF);

            listener.Routing.Upsert(new Discv5RoutingTable.Entry
            {
                NodeId = peerNodeId,
                Address = new IPEndPoint(IPAddress.Parse("203.0.113.1"), 30303),
                EnrEncoded = new byte[] { 0x00 }, // opaque placeholder — Upsert doesn't validate
            });

            Assert.True(listener.Routing.Count > 0,
                "DNS-seed Upsert path failed — routing table did not record the entry.");
        }
    }
}
