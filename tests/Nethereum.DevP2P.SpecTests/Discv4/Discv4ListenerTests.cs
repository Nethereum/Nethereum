using System;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv4;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv4
{
    public class Discv4ListenerTests
    {
        [Fact]
        public async Task TwoListeners_PingPongRoundTrip_BothLearnEachOther()
        {
            var keyA = EthECKey.GenerateKey();
            var keyB = EthECKey.GenerateKey();

            var tableA = new Discv4RoutingTable(keyA.GetPubKeyNoPrefix());
            var tableB = new Discv4RoutingTable(keyB.GetPubKeyNoPrefix());

            using var nodeA = new Discv4Listener(keyA, tableA);
            using var nodeB = new Discv4Listener(keyB, tableB);

            nodeA.Start(udpPort: 0, bindAddress: IPAddress.Loopback);
            nodeB.Start(udpPort: 0, bindAddress: IPAddress.Loopback);

            var pongTcs = new TaskCompletionSource<bool>();
            nodeA.PongReceived += (_, _) => pongTcs.TrySetResult(true);

            byte[] pingHashFromB = null;
            var pingTcs = new TaskCompletionSource<bool>();
            nodeB.PingReceived += async (_, e) =>
            {
                pingHashFromB = e.PingHash;
                var pong = new Discv4PongMessage
                {
                    To = new Discv4Endpoint
                    {
                        IP = e.Sender.Address,
                        UdpPort = (ushort)e.Sender.Port,
                        TcpPort = 0
                    },
                    PingHash = e.PingHash,
                    Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
                };
                await nodeB.SendPongAsync(e.Sender, pong);
                pingTcs.TrySetResult(true);
            };

            var ping = new Discv4PingMessage
            {
                Version = 4,
                From = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = (ushort)nodeA.Port, TcpPort = 30303 },
                To = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = (ushort)nodeB.Port, TcpPort = 0 },
                Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
            };
            await nodeA.SendPingAsync(new IPEndPoint(IPAddress.Loopback, nodeB.Port), ping);

            var pingGot = await Task.WhenAny(pingTcs.Task, Task.Delay(3000));
            Assert.Same(pingTcs.Task, pingGot);
            Assert.NotNull(pingHashFromB);
            Assert.Equal(32, pingHashFromB.Length);

            var pongGot = await Task.WhenAny(pongTcs.Task, Task.Delay(3000));
            Assert.Same(pongTcs.Task, pongGot);

            Assert.True(tableA.Count >= 1, "Node A should have learned about Node B from the pong");
            Assert.True(tableB.Count >= 1, "Node B should have learned about Node A from the ping");

            await nodeA.StopAsync();
            await nodeB.StopAsync();
        }

        [Fact]
        public async Task FindNode_OnReceive_RaisesEvent()
        {
            var keyA = EthECKey.GenerateKey();
            var keyB = EthECKey.GenerateKey();

            var tableA = new Discv4RoutingTable(keyA.GetPubKeyNoPrefix());
            var tableB = new Discv4RoutingTable(keyB.GetPubKeyNoPrefix());

            using var nodeA = new Discv4Listener(keyA, tableA);
            using var nodeB = new Discv4Listener(keyB, tableB);

            nodeA.Start(udpPort: 0);
            nodeB.Start(udpPort: 0);

            var findNodeTcs = new TaskCompletionSource<Discv4FindNodeMessage>();
            nodeB.FindNodeReceived += (_, e) => findNodeTcs.TrySetResult(e.FindNode);

            var target = new byte[64];
            new Random(0x42).NextBytes(target);

            await nodeA.SendFindNodeAsync(
                new IPEndPoint(IPAddress.Loopback, nodeB.Port),
                new Discv4FindNodeMessage
                {
                    Target = target,
                    Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
                });

            var got = await Task.WhenAny(findNodeTcs.Task, Task.Delay(3000));
            Assert.Same(findNodeTcs.Task, got);

            var received = findNodeTcs.Task.Result;
            Assert.Equal(target.ToHex(), received.Target.ToHex());

            await nodeA.StopAsync();
            await nodeB.StopAsync();
        }

        [Fact]
        public async Task ExpiredPing_IsDropped_NoEventRaised()
        {
            var keyA = EthECKey.GenerateKey();
            var keyB = EthECKey.GenerateKey();

            var tableA = new Discv4RoutingTable(keyA.GetPubKeyNoPrefix());
            var tableB = new Discv4RoutingTable(keyB.GetPubKeyNoPrefix());

            using var nodeA = new Discv4Listener(keyA, tableA);
            using var nodeB = new Discv4Listener(keyB, tableB);

            nodeA.Start();
            nodeB.Start();

            var pingTcs = new TaskCompletionSource<bool>();
            nodeB.PingReceived += (_, _) => pingTcs.TrySetResult(true);

            var expiredPing = new Discv4PingMessage
            {
                Version = 4,
                From = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = (ushort)nodeA.Port },
                To = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = (ushort)nodeB.Port },
                Expiration = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds()
            };
            await nodeA.SendPingAsync(new IPEndPoint(IPAddress.Loopback, nodeB.Port), expiredPing);

            await Task.Delay(500);
            Assert.False(pingTcs.Task.IsCompleted, "Expired ping must not raise PingReceived");
            Assert.Equal(0, tableB.Count);

            await nodeA.StopAsync();
            await nodeB.StopAsync();
        }
    }
}
