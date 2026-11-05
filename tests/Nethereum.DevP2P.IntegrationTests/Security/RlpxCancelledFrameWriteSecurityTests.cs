using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [Collection(HeavySecurityCollection.Name)]
    public class RlpxCancelledFrameWriteSecurityTests
    {
        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_FrameWriteCancelledByTheCallerHalfWayThroughTheFrame_When_TheCancellationIsObserved_Then_ItIsTornDownBecauseTheStreamCanNoLongerBeFramed()
        {
            var serverKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig { ClientId = "Nethereum/security", HandshakeTimeoutMs = 5000, PingIntervalMs = 600000 };
            var listener = new RlpxListener(serverKey, config);
            var accepted = new TaskCompletionSource<RlpxConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PeerAccepted += (_, c) => accepted.TrySetResult(c);
            listener.Start(0, IPAddress.Loopback);
            var attacker = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix(), receiveBufferSize: 1024);
            try
            {
                await attacker.SendFrameAsync(0, HelloMessageEncoder.Encode(RawRlpxPeer.HelloFor(attacker.Key)));
                await attacker.ReadFrameAsync();
                var serverConn = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));

                var payload = new byte[15 * 1024 * 1024];
                new Random(3).NextBytes(payload);
                var cancelled = false;
                for (var i = 0; i < 40 && !cancelled; i++)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    try { await serverConn.SendMessageAsync(0x40, payload, cts.Token); }
                    catch (Exception ex) when (ex is OperationCanceledException || ex is System.IO.IOException) { cancelled = true; }
                }
                Assert.True(cancelled, "the write never blocked; the half-written frame could not be provoked");

                var deadline = DateTime.UtcNow.AddSeconds(3);
                while (DateTime.UtcNow < deadline && !serverConn.IsDisconnected)
                    await Task.Delay(100);

                Assert.True(serverConn.IsDisconnected,
                    "a frame was abandoned half written and the connection stayed up; every later frame is misparsed by the peer");
            }
            finally
            {
                attacker.Dispose();
                await listener.StopAsync();
            }
        }
    }
}
