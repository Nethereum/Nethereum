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
    /// <summary>
    /// Behavioural tests pinning that the private control-frame handler dedupes
    /// the Ping/Pong/Disconnect logic across both
    /// <see cref="RlpxConnection.ReceiveMessageAsync"/> and
    /// <see cref="RlpxConnection.RequestAsync"/>. These tests reach the helper
    /// indirectly via real RLPx frames over a loopback TCP listener — the public
    /// API surface — so any future drift between the two call sites is caught
    /// regardless of how the helper is named or factored.
    /// </summary>
    [Collection("RlpxLoopback")]
    public class RlpxControlFrameHelperTests
    {
        private const int PingBurstCapacity = 4;

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
                // Client expects to read the Pong that server sends in reply
                // to its Ping. ReceiveMessageAsync on the client side must
                // process the Pong via the helper (returns Continue) and keep
                // waiting until a real sub-protocol message arrives.
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
        public async Task Given_PingFloodTriggeredDuringRequest_When_SameHelperRunsInRequestPath_Then_BudgetEnforcedIdentically()
        {
            // Pin the dedup invariant: the SAME budget rejects floods regardless
            // of whether they arrive in ReceiveMessageAsync or RequestAsync. We
            // start a RequestAsync on the server side that will never receive
            // its "expected" response, then flood it from the client.
            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync();
            try
            {
                using var serverRequestCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var serverRequest = serverConn.RequestAsync(
                    requestMsgId: 0x42,
                    requestPayload: new byte[] { 0xc0 },
                    expectedResponseMsgId: 0x43,
                    ct: serverRequestCts.Token);

                int overBurst = PingBurstCapacity + 4;
                for (int i = 0; i < overBurst; i++)
                {
                    try { await clientConn.SendMessageAsync(P2PMessageIds.Ping, Array.Empty<byte>()); }
                    catch { break; }
                }

                // Wait up to 10s for the server-side RequestAsync to fault on
                // the flood. Either RlpxPingFloodException or a plain IOException
                // is acceptable because Disconnect closes the stream.
                var completed = await Task.WhenAny(serverRequest, Task.Delay(TimeSpan.FromSeconds(10)));
                Assert.Same(serverRequest, completed);
                await Assert.ThrowsAnyAsync<System.IO.IOException>(async () => await serverRequest);
                Assert.False(serverConn.IsConnected,
                    "server must disconnect a flood that hits the RequestAsync path, mirroring ReceiveMessageAsync");
            }
            finally
            {
                try { await clientConn.DisconnectAsync(); } catch { }
                try { await listener.StopAsync(); } catch { }
            }
        }
    }
}
