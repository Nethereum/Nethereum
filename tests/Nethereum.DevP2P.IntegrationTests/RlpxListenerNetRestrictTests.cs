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
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    /// <summary>
    /// Verifies that <see cref="RlpxListener"/> enforces
    /// <see cref="DevP2PConfig.NetRestrict"/> on inbound TCP. Mirrors geth's
    /// <c>p2p/server.go</c> NetRestrict gate: a connection from an IP outside
    /// every configured CIDR is closed before the per-IP throttle reserves a
    /// slot. The default empty list must preserve unfiltered behaviour so
    /// existing peer admission tests stay green.
    /// </summary>
    public class RlpxListenerNetRestrictTests
    {
        private readonly ITestOutputHelper _output;

        public RlpxListenerNetRestrictTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Given_NetRestrictAllowsOnly10Slash8_When_LoopbackConnects_Then_RejectedPreHandshake()
        {
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/test",
                HandshakeTimeoutMs = 1500
            };
            config.NetRestrict.Add("10.0.0.0/8");

            var listener = new RlpxListener(EthECKey.GenerateKey(), config);
            int rejected = 0;
            int perIpRejected = 0;
            var rejectedEvt = new ManualResetEventSlim(false);
            listener.PeerFailed += (_, e) =>
            {
                _output.WriteLine($"PeerFailed [{e.Phase}]: {e.Exception.Message}");
                if (e.Phase == "InboundNetRestrict")
                {
                    Interlocked.Increment(ref rejected);
                    rejectedEvt.Set();
                }
                else if (e.Phase == "InboundPerIPCap")
                {
                    Interlocked.Increment(ref perIpRejected);
                }
            };

            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var sockets = new List<TcpClient>();
            try
            {
                var tcp = new TcpClient();
                await tcp.ConnectAsync(IPAddress.Loopback, listener.Port);
                sockets.Add(tcp);

                Assert.True(
                    rejectedEvt.Wait(TimeSpan.FromSeconds(3)),
                    "Expected PeerFailed('InboundNetRestrict') to fire when loopback hits a 10.0.0.0/8-only allow-list");
                Assert.Equal(1, Volatile.Read(ref rejected));
                Assert.Equal(0, Volatile.Read(ref perIpRejected));
                Assert.Equal(0, listener.CountInboundForIp(IPAddress.Loopback));
            }
            finally
            {
                foreach (var s in sockets) { try { s.Close(); } catch { } }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_NetRestrictAllowsLoopback_When_LoopbackConnects_Then_PassesGate()
        {
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/test",
                HandshakeTimeoutMs = 1500
            };
            config.NetRestrict.Add("127.0.0.0/8");

            var listener = new RlpxListener(EthECKey.GenerateKey(), config);
            int netRestrictRejected = 0;
            listener.PeerFailed += (_, e) =>
            {
                _output.WriteLine($"PeerFailed [{e.Phase}]: {e.Exception.Message}");
                if (e.Phase == "InboundNetRestrict")
                    Interlocked.Increment(ref netRestrictRejected);
            };

            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var sockets = new List<TcpClient>();
            try
            {
                var tcp = new TcpClient();
                await tcp.ConnectAsync(IPAddress.Loopback, listener.Port);
                sockets.Add(tcp);

                await Task.Delay(500);

                Assert.Equal(0, Volatile.Read(ref netRestrictRejected));
                Assert.Equal(1, listener.CountInboundForIp(IPAddress.Loopback));
            }
            finally
            {
                foreach (var s in sockets) { try { s.Close(); } catch { } }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_EmptyNetRestrict_When_LoopbackConnects_Then_NoNetRestrictRejection()
        {
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/test",
                HandshakeTimeoutMs = 1500
            };

            var listener = new RlpxListener(EthECKey.GenerateKey(), config);
            int netRestrictRejected = 0;
            listener.PeerFailed += (_, e) =>
            {
                _output.WriteLine($"PeerFailed [{e.Phase}]: {e.Exception.Message}");
                if (e.Phase == "InboundNetRestrict")
                    Interlocked.Increment(ref netRestrictRejected);
            };

            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var sockets = new List<TcpClient>();
            try
            {
                var tcp = new TcpClient();
                await tcp.ConnectAsync(IPAddress.Loopback, listener.Port);
                sockets.Add(tcp);

                await Task.Delay(500);

                Assert.Equal(0, Volatile.Read(ref netRestrictRejected));
                Assert.Equal(1, listener.CountInboundForIp(IPAddress.Loopback));
            }
            finally
            {
                foreach (var s in sockets) { try { s.Close(); } catch { } }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_NetRestrictBlocksHost_When_OutboundDialAttempted_Then_RlpxNetRestrictedExceptionThrown()
        {
            var clientKey = EthECKey.GenerateKey();
            var serverKey = EthECKey.GenerateKey();
            var clientConfig = new DevP2PConfig
            {
                ClientId = "Nethereum/test",
                ConnectTimeoutMs = 2000,
                HandshakeTimeoutMs = 2000
            };
            clientConfig.NetRestrict.Add("10.0.0.0/8");

            var serverConfig = new DevP2PConfig { ClientId = "Nethereum/test" };
            var listener = new RlpxListener(serverKey, serverConfig);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            try
            {
                var conn = new RlpxConnection(clientKey, clientConfig);
                var ex = await Assert.ThrowsAsync<RlpxNetRestrictedException>(async () =>
                    await conn.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix()));

                _output.WriteLine($"Outbound dial rejected: {ex.Message}");
                Assert.Equal("127.0.0.1", ex.Host);
                Assert.NotEmpty(ex.ResolvedAddresses);
            }
            finally
            {
                await listener.StopAsync();
            }
        }
    }
}
