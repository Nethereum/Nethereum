using System.Net;
using System.Net.Sockets;
using Nethereum.DevP2P.Discv4;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Discv4
{
    public class Discv4BondingTests
    {
        private static byte[] PingPacket(EthECKey key, int fromPort, int toPort)
        {
            var ping = new Discv4PingMessage
            {
                Version = 4,
                From = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = (ushort)fromPort, TcpPort = (ushort)fromPort },
                To = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = (ushort)toPort, TcpPort = 0 },
                Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
            };
            return Discv4Packet.Encode(key, Discv4MessageType.Ping, Discv4MessageEncoder.EncodePing(ping));
        }

        private static byte[] FindNodePacket(EthECKey key)
        {
            var findNode = new Discv4FindNodeMessage
            {
                Target = new byte[64],
                Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
            };
            return Discv4Packet.Encode(key, Discv4MessageType.FindNode, Discv4MessageEncoder.EncodeFindNode(findNode));
        }

        private static async Task<List<Discv4MessageType>> ReceiveTypesAsync(UdpClient udp, TimeSpan quiet)
        {
            var types = new List<Discv4MessageType>();
            while (true)
            {
                var receive = udp.ReceiveAsync();
                if (await Task.WhenAny(receive, Task.Delay(quiet)) != receive) return types;
                types.Add(Discv4Packet.Decode(receive.Result.Buffer).Type);
            }
        }

        private static async Task<Discv4Packet.DecodedPacket> ReceiveBackPingAsync(UdpClient udp, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (true)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero) return null;
                var receive = udp.ReceiveAsync();
                if (await Task.WhenAny(receive, Task.Delay(remaining)) != receive) return null;
                var decoded = Discv4Packet.Decode(receive.Result.Buffer);
                if (decoded.Type == Discv4MessageType.Ping) return decoded;
            }
        }

        private static Discv4Listener StartListener()
        {
            var key = EthECKey.GenerateKey();
            var listener = new Discv4Listener(key, new Discv4RoutingTable(key.GetPubKeyNoPrefix()));
            listener.Start(0, IPAddress.Loopback);
            return listener;
        }

        [Fact]
        public async Task Given_AnUnbondedPeer_When_ItPingsThenFindsNode_Then_ItIsBackPingedAndFindNodeIsNotAnswered()
        {
            using var server = StartListener();
            using var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            var peerPort = ((IPEndPoint)peer.Client.LocalEndPoint).Port;
            var peerKey = EthECKey.GenerateKey();
            var serverEp = new IPEndPoint(IPAddress.Loopback, server.Port);

            var ping = PingPacket(peerKey, peerPort, server.Port);
            await peer.SendAsync(ping, ping.Length, serverEp);
            var findNode = FindNodePacket(peerKey);
            await peer.SendAsync(findNode, findNode.Length, serverEp);

            var types = await ReceiveTypesAsync(peer, TimeSpan.FromSeconds(2));

            Assert.Contains(Discv4MessageType.Pong, types);
            Assert.Contains(Discv4MessageType.Ping, types);
            Assert.DoesNotContain(Discv4MessageType.Neighbors, types);
        }

        [Fact]
        public async Task Given_APeerThatAnsweredTheBackPing_When_ItFindsNode_Then_NeighborsAreAnswered()
        {
            using var server = StartListener();
            using var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            var peerPort = ((IPEndPoint)peer.Client.LocalEndPoint).Port;
            var peerKey = EthECKey.GenerateKey();
            var serverEp = new IPEndPoint(IPAddress.Loopback, server.Port);

            var ping = PingPacket(peerKey, peerPort, server.Port);
            await peer.SendAsync(ping, ping.Length, serverEp);
            var backPing = await ReceiveBackPingAsync(peer, TimeSpan.FromSeconds(10));
            Assert.NotNull(backPing);
            var pong = new Discv4PongMessage
            {
                To = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = (ushort)server.Port, TcpPort = 0 },
                PingHash = backPing.Hash,
                Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
            };
            var pongPacket = Discv4Packet.Encode(peerKey, Discv4MessageType.Pong, Discv4MessageEncoder.EncodePong(pong));
            var bonded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            server.PongReceived += (_, _) => bonded.TrySetResult(true);
            await peer.SendAsync(pongPacket, pongPacket.Length, serverEp);
            Assert.Same(bonded.Task, await Task.WhenAny(bonded.Task, Task.Delay(TimeSpan.FromSeconds(10))));
            var findNode = FindNodePacket(peerKey);
            await peer.SendAsync(findNode, findNode.Length, serverEp);

            var types = await ReceiveTypesAsync(peer, TimeSpan.FromSeconds(2));

            Assert.Contains(Discv4MessageType.Neighbors, types);
        }

        [Fact]
        public async Task Given_AFloodOfPingsFromDistinctUnbondedPeers_When_TheyAreReceived_Then_BackPingsAreBoundedByTheIngressRate()
        {
            using var server = StartListener();
            server.MaxPendingPings = 8;
            using var flooder = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            var flooderPort = ((IPEndPoint)flooder.Client.LocalEndPoint).Port;
            var serverEp = new IPEndPoint(IPAddress.Loopback, server.Port);

            for (var i = 0; i < 60; i++)
            {
                var ping = PingPacket(EthECKey.GenerateKey(), flooderPort, server.Port);
                await flooder.SendAsync(ping, ping.Length, serverEp);
            }

            var types = await ReceiveTypesAsync(flooder, TimeSpan.FromSeconds(1));

            Assert.Equal(60, types.Count(t => t == Discv4MessageType.Pong));
            Assert.Equal(60, types.Count(t => t == Discv4MessageType.Ping));
        }

        [Fact]
        public async Task Given_AFloodOfPingsBeyondThePendingCap_When_AnHonestPeerPingsDuringIt_Then_ItBonds()
        {
            using var server = StartListener();
            server.MaxPendingPings = 8;
            using var flooder = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            var flooderPort = ((IPEndPoint)flooder.Client.LocalEndPoint).Port;
            using var honest = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            var honestPort = ((IPEndPoint)honest.Client.LocalEndPoint).Port;
            var honestKey = EthECKey.GenerateKey();
            var serverEp = new IPEndPoint(IPAddress.Loopback, server.Port);
            var bonded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            server.PongReceived += (_, e) =>
            {
                if (e.SourceNode.NodeId.SequenceEqual(honestKey.GetPubKeyNoPrefix())) bonded.TrySetResult(true);
            };

            for (var i = 0; i < 60; i++)
            {
                var ping = PingPacket(EthECKey.GenerateKey(), flooderPort, server.Port);
                await flooder.SendAsync(ping, ping.Length, serverEp);
            }
            var honestPing = PingPacket(honestKey, honestPort, server.Port);
            await honest.SendAsync(honestPing, honestPing.Length, serverEp);

            var backPing = await ReceiveBackPingAsync(honest, TimeSpan.FromSeconds(10));
            Assert.NotNull(backPing);
            var pong = new Discv4PongMessage
            {
                To = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = (ushort)server.Port, TcpPort = 0 },
                PingHash = backPing.Hash,
                Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
            };
            var pongPacket = Discv4Packet.Encode(honestKey, Discv4MessageType.Pong, Discv4MessageEncoder.EncodePong(pong));
            await honest.SendAsync(pongPacket, pongPacket.Length, serverEp);

            Assert.Same(bonded.Task, await Task.WhenAny(bonded.Task, Task.Delay(TimeSpan.FromSeconds(10))));
        }
    }
}
