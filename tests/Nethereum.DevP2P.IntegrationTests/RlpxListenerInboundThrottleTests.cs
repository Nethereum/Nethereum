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
    /// <see cref="DevP2PConfig.MaxInboundPerIP"/>. We bypass the real RLPx
    /// handshake — opening raw TCP sockets is enough to exercise the
    /// pre-handshake accept-time throttle. Sockets past the cap must be
    /// closed by the server before the handshake reads a byte.
    /// </summary>
    public class RlpxListenerInboundThrottleTests
    {
        private readonly ITestOutputHelper _output;

        public RlpxListenerInboundThrottleTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task InboundFromSameIp_PastMaxInboundPerIp_AreDropped()
        {
            const int cap = 2;
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/test",
                MaxInboundPerIP = cap,
                HandshakeTimeoutMs = 1500
            };

            var listener = new RlpxListener(EthECKey.GenerateKey(), config);
            int rejected = 0;
            var rejectedEvt = new ManualResetEventSlim(false);
            listener.PeerFailed += (_, e) =>
            {
                _output.WriteLine($"PeerFailed [{e.Phase}]: {e.Exception.Message}");
                if (e.Phase == "InboundPerIPCap")
                {
                    Interlocked.Increment(ref rejected);
                    rejectedEvt.Set();
                }
            };

            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var sockets = new List<TcpClient>();
            try
            {
                // Open cap + 1 connections to the same listener from loopback.
                // First `cap` must stay alive while the handshake is pending.
                // The (cap+1)-th must be closed by the listener immediately,
                // observable as a PeerFailed("InboundPerIPCap") event.
                for (int i = 0; i < cap + 1; i++)
                {
                    var tcp = new TcpClient();
                    await tcp.ConnectAsync(IPAddress.Loopback, listener.Port);
                    sockets.Add(tcp);
                }

                // The listener fires PeerFailed synchronously inside the accept
                // loop, so the rejection should land essentially immediately.
                Assert.True(
                    rejectedEvt.Wait(TimeSpan.FromSeconds(3)),
                    "Expected PeerFailed('InboundPerIPCap') to fire for the (cap+1)-th socket");
                Assert.Equal(1, Volatile.Read(ref rejected));

                // The two accepted sockets sit in handshake state; ActivePeers
                // reports the in-flight handshake count. We don't assert exact
                // value because the listener-side handshake may already be
                // racing the handshake timeout; the throttle behaviour above
                // is the contract this test pins.
                _output.WriteLine($"ActivePeers after throttle: {listener.ActivePeers}");
                _output.WriteLine($"Inbound count for 127.0.0.1: {listener.CountInboundForIp(IPAddress.Loopback)}");
            }
            finally
            {
                foreach (var s in sockets)
                {
                    try { s.Close(); } catch { }
                }
                await listener.StopAsync();
            }
        }
    }
}
