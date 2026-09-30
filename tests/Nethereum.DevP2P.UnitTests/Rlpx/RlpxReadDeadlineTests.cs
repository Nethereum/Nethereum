using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Common;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.UnitTests.Rlpx
{
    [Collection("RlpxLoopback")]
    public class RlpxReadDeadlineTests
    {
        private readonly ITestOutputHelper _output;
        public RlpxReadDeadlineTests(ITestOutputHelper output) { _output = output; }

        private static DevP2PConfig BuildConfig(int readTimeoutMs) => new DevP2PConfig
        {
            ClientId = "Nethereum/read-deadline-test",
            HandshakeTimeoutMs = 30_000,
            ConnectTimeoutMs = 30_000,
            RequestTimeoutMs = 30_000,
            ReadTimeoutMs = readTimeoutMs,
            PingIntervalMs = 60_000
        };

        private async Task<(RlpxListener listener, RlpxConnection serverConn, RlpxConnection clientConn)>
            HandshakeOverLoopbackAsync(int readTimeoutMs)
        {
            var serverKey = EthECKey.GenerateKey();
            var clientKey = EthECKey.GenerateKey();
            var config = BuildConfig(readTimeoutMs);

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
        public async Task Given_OnlyOnePingPerWindow_When_ReadTimeoutShorterThanWindow_Then_InnerDeadlineFires()
        {
            const int readTimeoutMs = 2_000;

            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync(readTimeoutMs);
            try
            {
                var receiveTask = serverConn.ReceiveMessageAsync();

                await Task.Delay(500);
                try { await clientConn.SendMessageAsync(P2PMessageIds.Ping, Array.Empty<byte>()); } catch { }

                var completed = await Task.WhenAny(receiveTask, Task.Delay(readTimeoutMs * 10));
                Assert.Same(receiveTask, completed);
                await Assert.ThrowsAnyAsync<Exception>(async () => await receiveTask);
            }
            finally
            {
                try { await clientConn.DisconnectAsync(); } catch { }
                try { await listener.StopAsync(); } catch { }
            }
        }

        [Fact]
        public async Task Given_RealSubProtocolMessage_When_ReceivedBeforeDeadline_Then_ReturnsToCallerWithoutTimeout()
        {
            const int readTimeoutMs = 5_000;

            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync(readTimeoutMs);
            try
            {
                var receiveTask = serverConn.ReceiveMessageAsync();

                await Task.Delay(200);
                await clientConn.SendMessageAsync(0x42, new byte[] { 0xc0 });

                var (msgId, payload) = await receiveTask.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(0x42, msgId);
                Assert.Equal(new byte[] { 0xc0 }, payload);
            }
            finally
            {
                try { await clientConn.DisconnectAsync(); } catch { }
                try { await listener.StopAsync(); } catch { }
            }
        }
    }
}
