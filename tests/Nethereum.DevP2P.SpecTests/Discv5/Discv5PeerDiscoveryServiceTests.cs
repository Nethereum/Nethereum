using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv5;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv5
{
    /// <summary>
    /// End-to-end orchestrator tests using two real <see cref="Discv5Listener"/>
    /// instances on localhost — the "bootnode" listener pre-seeds its routing
    /// table with a third synthetic peer; the discovery service bonds with the
    /// bootnode, walks its table, and the synthetic peer must surface via the
    /// enode callback.
    /// </summary>
    public class Discv5PeerDiscoveryServiceTests
    {
        [Fact]
        public void Given_DialableEnr_When_Converted_Then_EnodeStringIsValid()
        {
            var key = EthECKey.GenerateKey();
            var enr = new EnrRecord { Sequence = 1 };
            enr.Pairs["id"] = Encoding.ASCII.GetBytes("v4");
            enr.Pairs["ip"] = new byte[] { 203, 0, 113, 7 };
            enr.Pairs["tcp"] = new byte[] { 0x76, 0x5F };  // 30303
            enr.Pairs["udp"] = new byte[] { 0x76, 0x5F };
            enr.Pairs["secp256k1"] = key.GetPubKey(compresseed: true);
            EnrRecordSigner.Sign(enr, key);

            var enode = Discv5PeerDiscoveryService.ConvertEnrToEnode(enr);

            Assert.NotNull(enode);
            Assert.StartsWith("enode://", enode);
            Assert.Contains("@203.0.113.7:30303", enode);
        }

        [Fact]
        public void Given_EnrWithoutSecp256k1_When_Converted_Then_ReturnsNull()
        {
            var enr = new EnrRecord { Sequence = 1 };
            enr.Pairs["id"] = Encoding.ASCII.GetBytes("v4");
            enr.Pairs["ip"] = new byte[] { 203, 0, 113, 7 };
            enr.Pairs["tcp"] = new byte[] { 0x76, 0x5F };
            // No secp256k1 — not dialable.

            Assert.Null(Discv5PeerDiscoveryService.ConvertEnrToEnode(enr));
        }

        [Fact]
        public void Given_LoopbackEnr_When_Converted_Then_ReturnsNull()
        {
            // The pool should never receive loopback enodes — discovery
            // filters them so the dial loop doesn't churn against itself.
            var key = EthECKey.GenerateKey();
            var enr = new EnrRecord { Sequence = 1 };
            enr.Pairs["id"] = Encoding.ASCII.GetBytes("v4");
            enr.Pairs["ip"] = IPAddress.Loopback.GetAddressBytes();
            enr.Pairs["tcp"] = new byte[] { 0x76, 0x5F };
            enr.Pairs["secp256k1"] = key.GetPubKey(compresseed: true);
            EnrRecordSigner.Sign(enr, key);

            Assert.Null(Discv5PeerDiscoveryService.ConvertEnrToEnode(enr));
        }

        [Fact]
        public async Task Given_BootnodeWithSeededRoutingTable_When_DiscoveryRuns_Then_EnodeCallbackInvoked()
        {
            // Bootnode listener — discovery will PING + FINDNODE this peer.
            var bootKey = EthECKey.GenerateKey();
            using var boot = new Discv5Listener(bootKey);
            boot.Start(IPAddress.Loopback, port: 0);
            var bootEnr = BuildSignedEnr(bootKey, IPAddress.Loopback, (ushort)boot.Port, (ushort)boot.Port);
            boot.LocalEnrEncoded = EnrRecordEncoder.EncodeRecord(bootEnr);
            boot.LocalEnrSequence = bootEnr.Sequence;

            // Seed the bootnode's routing table with a synthetic peer ENR at
            // distance 256 from the bootnode — guarantees WalkDistances {256, 255, 254}
            // covers it. The probability of a random key falling in bucket 256 is
            // ~99%, but searching deterministically removes flake risk.
            var syntheticIp = new IPAddress(new byte[] { 203, 0, 113, 42 });
            const ushort syntheticPort = 30444;
            var bootNodeId = boot.NodeId;
            EthECKey syntheticKey = null;
            byte[] syntheticNodeId = null;
            for (int attempt = 0; attempt < 100; attempt++)
            {
                var candidate = EthECKey.GenerateKey();
                var candidateId = Discv5Crypto.ComputeNodeId(candidate.GetPubKey(compresseed: true));
                if (Discv5RoutingTable.LogDistance(bootNodeId, candidateId) == 256)
                {
                    syntheticKey = candidate;
                    syntheticNodeId = candidateId;
                    break;
                }
            }
            Assert.NotNull(syntheticKey);
            var syntheticEnr = BuildSignedEnr(syntheticKey, syntheticIp, syntheticPort, syntheticPort);
            var syntheticEnrEncoded = EnrRecordEncoder.EncodeRecord(syntheticEnr);
            boot.Routing.Upsert(new Discv5RoutingTable.Entry
            {
                NodeId = syntheticNodeId,
                Address = new IPEndPoint(syntheticIp, syntheticPort),
                EnrEncoded = syntheticEnrEncoded,
            });

            // Discovery client listener.
            var localKey = EthECKey.GenerateKey();
            using var localListener = new Discv5Listener(localKey);
            localListener.Start(IPAddress.Loopback, port: 0);
            var localEnr = BuildSignedEnr(localKey, IPAddress.Loopback, (ushort)localListener.Port, (ushort)localListener.Port);
            localListener.LocalEnrEncoded = EnrRecordEncoder.EncodeRecord(localEnr);
            localListener.LocalEnrSequence = localEnr.Sequence;

            var discovered = new ConcurrentBag<string>();
            var diagnostics = new ConcurrentBag<string>();
            var bootnodes = new List<(EnrRecord, IPEndPoint)>
            {
                (bootEnr, new IPEndPoint(IPAddress.Loopback, boot.Port)),
            };

            using var discovery = new Discv5PeerDiscoveryService(
                localListener,
                enode => discovered.Add(enode),
                bootnodes,
                msg => diagnostics.Add(msg),
                walkInterval: TimeSpan.FromSeconds(60));

            // Sanity check — first inspect what's actually in the bootnode's
            // routing table at distance 256, then call PING and FINDNODE directly
            // to validate the loopback path before exercising the orchestrator.
            var atDist256 = boot.Routing.AtDistance(256);
            Assert.Single(atDist256);

            using var sanityCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var bootEndpoint = new IPEndPoint(IPAddress.Loopback, boot.Port);
            var bootCompressed = bootEnr.Secp256k1;
            var bootPeerNodeId = Discv5Crypto.ComputeNodeId(bootCompressed);
            var pong = await localListener.SendPingAsync(
                bootEndpoint, bootPeerNodeId, bootCompressed,
                TimeSpan.FromSeconds(3), sanityCts.Token);
            Assert.NotNull(pong);

            // Wait briefly for the bootnode-side session to fully settle after
            // the initial Ping (it has to process WHOAREYOU + handshake reply).
            await Task.Delay(200, sanityCts.Token);

            // Try FINDNODE(distance=0) first — bootnode should return its own ENR.
            var selfEnrs = await localListener.SendFindNodeAsync(
                bootEndpoint, bootPeerNodeId, bootCompressed,
                new uint[] { 0 },
                TimeSpan.FromSeconds(5), sanityCts.Token);
            Assert.True(selfEnrs.Count > 0,
                $"FINDNODE(0) returned no ENRs. boot.LocalEnrEncoded null? {boot.LocalEnrEncoded == null}; boot routing count={boot.Routing.Count}; bootnode pong-counter={pong.EnrSeq}");

            var enrs = await localListener.SendFindNodeAsync(
                bootEndpoint, bootPeerNodeId, bootCompressed,
                Discv5PeerDiscoveryService.WalkDistances,
                TimeSpan.FromSeconds(3), sanityCts.Token);
            Assert.NotNull(enrs);
            Assert.NotEmpty(enrs);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await discovery.StartAsync(cts.Token);

            // Wait for the bootnode bond + harvest to enqueue our synthetic peer.
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < deadline)
            {
                if (discovered.Count >= 1) break;
                await Task.Delay(50, cts.Token);
            }

            await discovery.StopAsync();
            await boot.StopAsync();
            await localListener.StopAsync();

            // Synthetic peer's IP must be present in at least one enqueued enode.
            var diagnosticsText = string.Join("\n", diagnostics);
            Assert.True(discovered.Count >= 1,
                $"Expected at least 1 discovered enode, got 0. Routing-table walk failed.\nDiagnostics:\n{diagnosticsText}\nDirect FINDNODE returned {enrs.Count} ENRs.");
            Assert.Contains(discovered, e => e.Contains("203.0.113.42:30444"));
        }

        private static EnrRecord BuildSignedEnr(EthECKey key, IPAddress ip, ushort tcp, ushort udp)
        {
            var enr = new EnrRecord { Sequence = 1 };
            enr.Pairs["id"] = Encoding.ASCII.GetBytes("v4");
            enr.Pairs["ip"] = ip.GetAddressBytes();
            enr.Pairs["tcp"] = new[] { (byte)((tcp >> 8) & 0xff), (byte)(tcp & 0xff) };
            enr.Pairs["udp"] = new[] { (byte)((udp >> 8) & 0xff), (byte)(udp & 0xff) };
            enr.Pairs["secp256k1"] = key.GetPubKey(compresseed: true);
            EnrRecordSigner.Sign(enr, key);
            return enr;
        }
    }
}
