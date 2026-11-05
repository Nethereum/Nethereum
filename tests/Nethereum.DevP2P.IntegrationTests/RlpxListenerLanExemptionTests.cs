using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests
{
    public class RlpxListenerLanExemptionTests
    {
        private const int PerIp = 2;
        private const int Clients = 8;

        private static DevP2PConfig NewConfig() => new DevP2PConfig
        {
            ClientId = "Nethereum/test",
            MaxPeers = Clients + 2,
            MaxInboundPerIP = PerIp,
            MaxInboundPerSubnet = 0,
            HandshakeTimeoutMs = 5000,
            ConnectTimeoutMs = 5000
        };

        [Fact]
        public async Task Given_LoopbackPeerOpeningSequentialConnections_When_PastMaxInboundPerIp_Then_AllAreEstablished()
        {
            var serverKey = EthECKey.GenerateKey();
            var config = NewConfig();
            var listener = new RlpxListener(serverKey, config);
            var admitted = new List<RlpxConnection>();
            listener.PeerAccepted += (_, c) => { lock (admitted) admitted.Add(c); };
            listener.Start(0, IPAddress.Loopback);
            var clients = new List<RlpxConnection>();
            try
            {
                for (var i = 0; i < Clients; i++)
                {
                    var client = new RlpxConnection(EthECKey.GenerateKey(), config);
                    clients.Add(client);
                    await client.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
                }
                await Task.Delay(500);

                int established;
                lock (admitted) established = admitted.Count(c => c.IsConnected);
                Assert.Equal(Clients, established);
            }
            finally
            {
                foreach (var c in clients) { try { c.Dispose(); } catch { } }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_LoopbackPeerOpeningConcurrentConnections_When_PastMaxInboundPerIp_Then_AllAreEstablished()
        {
            var serverKey = EthECKey.GenerateKey();
            var config = NewConfig();
            var listener = new RlpxListener(serverKey, config);
            var admitted = new List<RlpxConnection>();
            listener.PeerAccepted += (_, c) => { lock (admitted) admitted.Add(c); };
            listener.Start(0, IPAddress.Loopback);
            var clients = Enumerable.Range(0, Clients)
                .Select(_ => new RlpxConnection(EthECKey.GenerateKey(), config)).ToList();
            try
            {
                await Task.WhenAll(clients.Select(c =>
                    c.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix())));
                await Task.Delay(500);

                int established;
                lock (admitted) established = admitted.Count(c => c.IsConnected);
                Assert.Equal(Clients, established);
            }
            finally
            {
                foreach (var c in clients) { try { c.Dispose(); } catch { } }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_LoopbackPeerAtMaxPeers_When_AnotherConnects_Then_MaxPeersStillApplies()
        {
            var serverKey = EthECKey.GenerateKey();
            var config = NewConfig();
            config.MaxPeers = 3;
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
                lock (admitted) established = admitted.Count(c => c.IsConnected);
                Assert.True(established <= 3, $"{established} established, MaxPeers is 3");
            }
            finally
            {
                foreach (var c in clients) { try { c.Dispose(); } catch { } }
                await listener.StopAsync();
            }
        }
    }
}
