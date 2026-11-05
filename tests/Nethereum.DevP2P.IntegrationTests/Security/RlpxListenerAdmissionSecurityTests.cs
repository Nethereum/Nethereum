using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [Collection(HeavySecurityCollection.Name)]
    public class RlpxListenerAdmissionSecurityTests
    {
        private const int GethDefaultMaxPendingPeers = 50;

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_SilentConnectionsFromManyDistinctSubnets_When_ListenerAccepts_Then_PendingHandshakesStayWithinMaxPendingPeers()
        {
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/security",
                HandshakeTimeoutMs = 30000
            };
            var listener = new RlpxListener(EthECKey.GenerateKey(), config);
            listener.Start(0, IPAddress.Loopback);
            var sockets = new List<TcpClient>();
            try
            {
                for (var n = 1; n <= 150; n++)
                {
                    var tcp = new TcpClient(new IPEndPoint(IPAddress.Parse("127.0." + n + ".1"), 0));
                    await tcp.ConnectAsync(IPAddress.Loopback, listener.Port);
                    sockets.Add(tcp);
                }

                await Task.Delay(1500);

                Assert.True(listener.PendingHandshakeCount <= GethDefaultMaxPendingPeers,
                    $"pending handshakes {listener.PendingHandshakeCount} exceed the geth MaxPendingPeers default of {GethDefaultMaxPendingPeers} (p2p/server.go:49)");
            }
            finally
            {
                foreach (var s in sockets) { try { s.Dispose(); } catch { } }
                await listener.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_OneIpOpeningManyCompletedConnections_When_ListenerAdmits_Then_EstablishedPeersFromThatIpStayWithinMaxInboundPerIp()
        {
            const int perIp = 2;
            var serverKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/security",
                MaxPeers = 20,
                MaxInboundPerIP = perIp,
                IsLanAddress = _ => false,
                MaxInboundPerSubnet = 0,
                HandshakeTimeoutMs = 5000,
                ConnectTimeoutMs = 5000
            };
            var listener = new RlpxListener(serverKey, config);
            var admitted = new List<RlpxConnection>();
            listener.PeerAccepted += (_, c) => { lock (admitted) admitted.Add(c); };
            listener.Start(0, IPAddress.Loopback);
            var clients = new List<RlpxConnection>();
            try
            {
                for (var i = 0; i < 6; i++)
                {
                    var client = new RlpxConnection(EthECKey.GenerateKey(), config);
                    clients.Add(client);
                    try { await client.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix()); }
                    catch { }
                    await Task.Delay(100);
                }
                await Task.Delay(500);

                int established;
                lock (admitted) established = admitted.FindAll(c => c.IsConnected).Count;
                Assert.True(established <= perIp,
                    $"one IP holds {established} established inbound peers; MaxInboundPerIP is {perIp}");
            }
            finally
            {
                foreach (var c in clients) { try { c.Dispose(); } catch { } }
                await listener.StopAsync();
            }
        }
    }
}
