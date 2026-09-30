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
    public class Discv5PeerDiscoveryServiceTests
    {
        [Fact]
        public void Given_DialableEnr_When_Converted_Then_EnodeStringIsValid()
        {
            var key = EthECKey.GenerateKey();
            var enr = new EnrRecord { Sequence = 1 };
            enr.Pairs["id"] = Encoding.ASCII.GetBytes("v4");
            enr.Pairs["ip"] = new byte[] { 203, 0, 113, 7 };
            enr.Pairs["tcp"] = new byte[] { 0x76, 0x5F };
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

            Assert.Null(Discv5PeerDiscoveryService.ConvertEnrToEnode(enr));
        }

        [Fact]
        public void Given_LoopbackEnr_When_Converted_Then_ReturnsNull()
        {
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
            var bootKey = EthECKey.GenerateKey();
            using var boot = new Discv5Listener(bootKey);
            boot.Start(IPAddress.Loopback, port: 0);
            var bootEnr = BuildSignedEnr(bootKey, IPAddress.Loopback, (ushort)boot.Port, (ushort)boot.Port);
            boot.LocalEnrEncoded = EnrRecordEncoder.EncodeRecord(bootEnr);
            boot.LocalEnrSequence = bootEnr.Sequence;

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

            await Task.Delay(200, sanityCts.Token);

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

            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < deadline)
            {
                if (discovered.Count >= 1) break;
                await Task.Delay(50, cts.Token);
            }

            await discovery.StopAsync();
            await boot.StopAsync();
            await localListener.StopAsync();

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
