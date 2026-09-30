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
                for (int i = 0; i < cap + 1; i++)
                {
                    var tcp = new TcpClient();
                    await tcp.ConnectAsync(IPAddress.Loopback, listener.Port);
                    sockets.Add(tcp);
                }

                Assert.True(
                    rejectedEvt.Wait(TimeSpan.FromSeconds(3)),
                    "Expected PeerFailed('InboundPerIPCap') to fire for the (cap+1)-th socket");
                Assert.Equal(1, Volatile.Read(ref rejected));

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
