using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv5;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class Discv5NodesSecurityTests
    {
        private static byte[] Ping() => Discv5MessageEncoder.EncodePing(new Discv5PingMessage { RequestId = new byte[] { 1 }, EnrSeq = 1 });

        private static byte[] UnsignedEnr(EthECKey key, string ip, int port)
        {
            var enr = new EnrRecord { Sequence = 1, Signature = RandomNumberGenerator.GetBytes(64) };
            enr.Pairs["id"] = new[] { (byte)'v', (byte)'4' };
            enr.Pairs["secp256k1"] = key.GetPubKey(true);
            enr.Pairs["ip"] = IPAddress.Parse(ip).GetAddressBytes();
            enr.Pairs["udp"] = new[] { (byte)(port >> 8), (byte)port };
            enr.Pairs["tcp"] = new[] { (byte)(port >> 8), (byte)port };
            return EnrRecordEncoder.EncodeRecord(enr);
        }

        private static async Task<(Discv5Listener Listener, Discv5RawPeer Peer)> SessionAsync()
        {
            var key = EthECKey.GenerateKey();
            var listener = new Discv5Listener(key);
            listener.Start(IPAddress.Loopback, 0);
            listener.LocalEnrEncoded = Discv5RawPeer.SignEnr(key, IPAddress.Loopback, listener.Port, 1);
            var peer = new Discv5RawPeer(new IPEndPoint(IPAddress.Loopback, listener.Port), key);
            Assert.NotNull(await peer.EstablishSessionAsync(Ping()));
            return (listener, peer);
        }

        private static byte[] NodesMessage(params byte[][] records)
        {
            var nodes = new Discv5NodesMessage { RequestId = new byte[] { 9 }, Total = 1 };
            nodes.Records.AddRange(records);
            return Discv5MessageEncoder.EncodeNodes(nodes);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_AuthenticatedPeer_When_ItSendsUnsolicitedNodesWithAnUnsignedEnr_Then_TheRoutingTableDoesNotImportIt()
        {
            var (listener, peer) = await SessionAsync();
            await using var _ = listener;
            using var __ = peer;
            var forged = UnsignedEnr(EthECKey.GenerateKey(), "8.8.4.4", 30303);

            await peer.SendMessageAsync(NodesMessage(forged));
            await Task.Delay(500);

            Assert.DoesNotContain(listener.Routing.Snapshot(), e => e.EnrEncoded.SequenceEqual(forged));
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_AuthenticatedPeer_When_ItSendsNodesNoFindNodeAskedFor_Then_TheRoutingTableDoesNotImportTheRecords()
        {
            var (listener, peer) = await SessionAsync();
            await using var _ = listener;
            using var __ = peer;
            var sybil = Discv5RawPeer.SignEnr(EthECKey.GenerateKey(), IPAddress.Parse("8.8.4.4"), 30303, 1, tcpPort: 30303);

            await peer.SendMessageAsync(NodesMessage(sybil));
            await Task.Delay(500);

            Assert.DoesNotContain(listener.Routing.Snapshot(), e => e.EnrEncoded.SequenceEqual(sybil));
        }

        private static (EthECKey Key, byte[] NodeId) KeyAtDistance256(byte[] localId)
        {
            while (true)
            {
                var key = EthECKey.GenerateKey();
                var id = Discv5Crypto.ComputeNodeId(key.GetPubKeyNoPrefix());
                if (Discv5RoutingTable.LogDistance(localId, id) == 256) return (key, id);
            }
        }

        private static async Task<List<string>> HarvestFromServerHoldingAsync(bool signed)
        {
            var sKey = EthECKey.GenerateKey();
            await using var server = new Discv5Listener(sKey);
            server.Start(IPAddress.Loopback, 0);
            server.LocalEnrEncoded = Discv5RawPeer.SignEnr(sKey, IPAddress.Loopback, server.Port, 1);

            var (fKey, fId) = KeyAtDistance256(server.NodeId);
            var record = signed
                ? Discv5RawPeer.SignEnr(fKey, IPAddress.Parse("8.8.4.4"), 30303, 1, tcpPort: 30303)
                : UnsignedEnr(fKey, "8.8.4.4", 30303);
            server.Routing.Upsert(new Discv5RoutingTable.Entry
            {
                NodeId = fId,
                Address = new IPEndPoint(IPAddress.Parse("8.8.4.4"), 30303),
                EnrEncoded = record
            });

            var cKey = EthECKey.GenerateKey();
            await using var client = new Discv5Listener(cKey);
            client.Start(IPAddress.Loopback, 0);
            client.LocalEnrEncoded = Discv5RawPeer.SignEnr(cKey, IPAddress.Loopback, client.Port, 1);

            var enqueued = new ConcurrentQueue<string>();
            var bootnodes = new List<(EnrRecord Enr, IPEndPoint Endpoint)>
            {
                (EnrRecordEncoder.Decode(server.LocalEnrEncoded), new IPEndPoint(IPAddress.Loopback, server.Port))
            };
            await using var discovery = new Discv5PeerDiscoveryService(client, enqueued.Enqueue, bootnodes, null, TimeSpan.FromMinutes(5));
            await discovery.StartAsync(CancellationToken.None);

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(6);
            while (DateTime.UtcNow < deadline && !enqueued.Any(e => e.Contains("8.8.4.4")))
                await Task.Delay(50);
            return enqueued.ToList();
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_FindNodeAnswerContainingAnUnsignedEnr_When_DiscoveryHarvestsIt_Then_NoEnodeIsQueuedForDialling()
        {
            var enqueued = await HarvestFromServerHoldingAsync(signed: false);

            Assert.DoesNotContain(enqueued, e => e.Contains("8.8.4.4"));
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_FindNodeAnswerContainingAProperlySignedEnr_When_DiscoveryHarvestsIt_Then_TheEnodeIsQueued()
        {
            var enqueued = await HarvestFromServerHoldingAsync(signed: true);

            Assert.Contains(enqueued, e => e.Contains("8.8.4.4:30303"));
        }
    }
}
