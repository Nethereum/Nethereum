using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv4;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class Discv4PeerDiscoveryServiceSecurityTests
    {
        private static int FreeUdpPort()
        {
            using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)probe.Client.LocalEndPoint).Port;
        }

        private static Discv4Neighbor Neighbor(string ip, ushort udp, ushort tcp) => new Discv4Neighbor
        {
            IP = IPAddress.Parse(ip),
            UdpPort = udp,
            TcpPort = tcp,
            NodeId = EthECKey.GenerateKey().GetPubKeyNoPrefix()
        };

        private static byte[] NeighborsPacket(EthECKey key, params Discv4Neighbor[] nodes)
        {
            var msg = new Discv4NeighborsMessage { Expiration = Discv4RawPeer.Future() };
            msg.Nodes.AddRange(nodes);
            return Discv4RawPeer.BuildPacket(key, Discv4MessageType.Neighbors, Discv4MessageEncoder.EncodeNeighbors(msg));
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_UnsolicitedNeighborsFromAnyKey_When_DiscoveryServiceReceivesThem_Then_NothingIsHarvested()
        {
            var port = FreeUdpPort();
            using var service = new PeerDiscoveryService(_ => { });
            service.Start(port);
            using var attacker = new Discv4RawPeer();

            await attacker.SendAsync(NeighborsPacket(attacker.Key, Neighbor("8.8.4.4", 30303, 30303), Neighbor("1.1.1.1", 30303, 30303)),
                new IPEndPoint(IPAddress.Loopback, port));
            await Task.Delay(500);

            var harvested = await service.DiscoverAsync(Array.Empty<string>(), TimeSpan.FromSeconds(1), CancellationToken.None);
            Assert.True(harvested.Count == 0,
                $"{harvested.Count} enodes harvested from a NEIGHBORS packet that answers no FINDNODE of ours (geth v4_udp.go:786 errUnsolicitedReply)");
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_SolicitedNeighborsWithSpecialAndLowPortEntries_When_DiscoveryServiceHarvests_Then_OnlyRoutableNodesAreKept()
        {
            using var dht = new Discv4RawPeer();
            var servicePort = FreeUdpPort();
            using var service = new PeerDiscoveryService(_ => { });
            service.Start(servicePort);
            var seed = $"enode://{Convert.ToHexString(dht.NodeId).ToLowerInvariant()}@127.0.0.1:{dht.Endpoint.Port}";

            var good = Neighbor("8.8.4.4", 30303, 30303);
            var multicast = Neighbor("224.0.0.251", 30303, 30303);
            var thisNet = Neighbor("0.1.2.3", 30303, 30303);
            var broadcast = Neighbor("255.255.255.255", 30303, 30303);
            var lowPort = Neighbor("9.9.9.9", 80, 80);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var discover = service.DiscoverAsync(new[] { seed }, TimeSpan.FromSeconds(3), cts.Token);
            var answered = false;
            var from = new IPEndPoint(IPAddress.Loopback, servicePort);
            while (!answered)
            {
                var got = await dht.ReceiveAsync(TimeSpan.FromSeconds(5));
                Assert.NotNull(got);
                if (got.Value.Packet.Type == Discv4MessageType.Ping)
                    await dht.SendPongAsync(from, got.Value.Packet.Hash);
                else if (got.Value.Packet.Type == Discv4MessageType.FindNode)
                {
                    await dht.SendAsync(NeighborsPacket(dht.Key, good, multicast, thisNet, broadcast, lowPort), from);
                    answered = true;
                }
            }
            var harvested = await discover;

            var kept = harvested.Select(e => EnodeUrl.Parse(e)).Select(e => $"{e.Host}:{e.Port}").ToList();
            Assert.Contains("8.8.4.4:30303", kept);
            Assert.DoesNotContain(kept, h => h.StartsWith("224.", StringComparison.Ordinal) || h.StartsWith("0.", StringComparison.Ordinal)
                || h.StartsWith("255.", StringComparison.Ordinal) || h == "9.9.9.9:80");
        }
    }
}
