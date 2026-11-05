using System;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Rlpx
{
    [Collection("RlpxLoopback")]
    public class RlpxPerFrameReadDeadlineTests
    {
        private const int ReadTimeoutMs = 2_000;
        private const int QuietWindowMs = 8_000;

        private static DevP2PConfig BuildConfig(int pingIntervalMs) => new DevP2PConfig
        {
            ClientId = "Nethereum/per-frame-deadline-test",
            HandshakeTimeoutMs = 30_000,
            ConnectTimeoutMs = 30_000,
            RequestTimeoutMs = 30_000,
            ReadTimeoutMs = ReadTimeoutMs,
            PingIntervalMs = pingIntervalMs
        };

        private static async Task<(RlpxListener Listener, RlpxConnection Server, RlpxConnection Client)>
            ConnectOverLoopbackAsync(int clientPingIntervalMs)
        {
            var serverKey = EthECKey.GenerateKey();
            var listener = new RlpxListener(serverKey, BuildConfig(500));
            var accepted = new TaskCompletionSource<RlpxConnection>();
            listener.PeerAccepted += (_, conn) => accepted.TrySetResult(conn);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var client = new RlpxConnection(EthECKey.GenerateKey(), BuildConfig(clientPingIntervalMs));
            await client.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
            var server = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            return (listener, server, client);
        }

        [Fact]
        public async Task Given_AQuietPeerThatKeepsPinging_When_NoSubProtocolMessageArrivesForFourDeadlines_Then_TheReceiveStaysPendingAndReturnsTheNextMessage()
        {
            var (listener, server, client) = await ConnectOverLoopbackAsync(clientPingIntervalMs: 500);
            try
            {
                client.StartDispatching();
                var receive = server.ReceiveMessageAsync(server.FrameReadTimeout);

                var completed = await Task.WhenAny(receive, Task.Delay(QuietWindowMs));

                Assert.NotSame(receive, completed);
                Assert.True(server.IsConnected);
                Assert.True(client.IsConnected);
                Assert.True(DateTime.UtcNow - client.LastFrameReceivedUtc < TimeSpan.FromMilliseconds(ReadTimeoutMs));

                await client.SendMessageAsync(0x10, new byte[] { 0xc0 });
                var (msgId, payload) = await receive.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(0x10, msgId);
                Assert.Equal(new byte[] { 0xc0 }, payload);
            }
            finally
            {
                try { await client.DisconnectAsync(); } catch { }
                try { await listener.StopAsync(); } catch { }
            }
        }

        [Fact]
        public async Task Given_APeerThatSendsNoFrameAtAll_When_TheReadDeadlinePasses_Then_TheReceiveFailsWithinTheDeadline()
        {
            var (listener, server, client) = await ConnectOverLoopbackAsync(clientPingIntervalMs: 600_000);
            try
            {
                var started = DateTime.UtcNow;
                var receive = server.ReceiveMessageAsync(server.FrameReadTimeout);

                var completed = await Task.WhenAny(receive, Task.Delay(QuietWindowMs));

                Assert.Same(receive, completed);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await receive);
                Assert.True(DateTime.UtcNow - started < TimeSpan.FromMilliseconds(ReadTimeoutMs * 2));
            }
            finally
            {
                try { await client.DisconnectAsync(); } catch { }
                try { await listener.StopAsync(); } catch { }
            }
        }
    }
}
