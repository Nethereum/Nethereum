using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Rlpx
{
    [Collection("RlpxLoopback")]
    public class RlpxControlFrameHelperTests
    {
        private static DevP2PConfig BuildConfig() => new DevP2PConfig
        {
            ClientId = "Nethereum/control-frame-test",
            HandshakeTimeoutMs = 30_000,
            ConnectTimeoutMs = 30_000,
            RequestTimeoutMs = 30_000,
            ReadTimeoutMs = 30_000,
            PingIntervalMs = 60_000
        };

        private async Task<(RlpxListener listener, RlpxConnection serverConn, RlpxConnection clientConn)>
            HandshakeOverLoopbackAsync()
        {
            var serverKey = EthECKey.GenerateKey();
            var clientKey = EthECKey.GenerateKey();
            var config = BuildConfig();

            var listener = new RlpxListener(serverKey, config);
            var acceptedTcs = new TaskCompletionSource<RlpxConnection>();
            listener.PeerAccepted += (_, conn) => acceptedTcs.TrySetResult(conn);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var clientConn = new RlpxConnection(clientKey, config);
            await clientConn.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
            var serverConn = await acceptedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            return (listener, serverConn, clientConn);
        }

        [Fact]
        public async Task Given_PingArrivesInReceiveLoop_When_HandledByControlFrameHelper_Then_PongRepliedAndLoopContinues()
        {
            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await clientConn.SendMessageAsync(P2PMessageIds.Ping, Array.Empty<byte>());

                var receiveTask = clientConn.ReceiveMessageAsync(cts.Token);
                await Task.Delay(500);
                Assert.False(receiveTask.IsCompleted,
                    "client ReceiveMessageAsync must not return the Pong as an app message");
            }
            finally
            {
                try { await clientConn.DisconnectAsync(); } catch { }
                try { await listener.StopAsync(); } catch { }
            }
        }

        [Fact]
        public async Task Given_DisconnectArrivesInReceiveLoop_When_HandledByControlFrameHelper_Then_ConnectionMarkedDown()
        {
            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync();
            try
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(150);
                    try { await serverConn.DisconnectAsync(DisconnectReason.ClientQuitting); } catch { }
                });

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                await Assert.ThrowsAnyAsync<Exception>(async () =>
                {
                    while (true)
                        await clientConn.ReceiveMessageAsync(cts.Token);
                });
                Assert.False(clientConn.IsConnected);
            }
            finally
            {
                try { await clientConn.DisconnectAsync(); } catch { }
                try { await listener.StopAsync(); } catch { }
            }
        }

        [Fact]
        public async Task Given_PingArrivesDuringRequest_When_HandledByControlFrameHelper_Then_PongRepliedAndRequestKeepsWaiting()
        {
            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync();
            try
            {
                using var serverRequestCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var serverRequest = serverConn.RequestAsync(
                    requestMsgId: 0x42,
                    requestPayload: new byte[] { 0xc0 },
                    expectedResponseMsgId: 0x43,
                    ct: serverRequestCts.Token);

                await clientConn.SendMessageAsync(P2PMessageIds.Ping, Array.Empty<byte>());
                await Task.Delay(500);

                Assert.False(serverRequest.IsCompleted,
                    "a Ping during RequestAsync must be handled as a control frame, not returned as the response");
            }
            finally
            {
                try { await clientConn.DisconnectAsync(); } catch { }
                try { await listener.StopAsync(); } catch { }
            }
        }
    }
}
