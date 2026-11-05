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
    public class RlpxStatusHandshakeDeadlineTests
    {
        private const int StatusHandshakeTimeoutMs = 1_500;

        private static DevP2PConfig BuildConfig(int pingIntervalMs) => new DevP2PConfig
        {
            ClientId = "Nethereum/status-deadline-test",
            HandshakeTimeoutMs = 30_000,
            ConnectTimeoutMs = 30_000,
            RequestTimeoutMs = 30_000,
            ReadTimeoutMs = 30_000,
            StatusHandshakeTimeoutMs = StatusHandshakeTimeoutMs,
            PingIntervalMs = pingIntervalMs
        };

        private static async Task<(RlpxListener Listener, RlpxConnection Server, RlpxConnection Client)> ConnectOverLoopbackAsync()
        {
            var serverKey = EthECKey.GenerateKey();
            var listener = new RlpxListener(serverKey, BuildConfig(60_000));
            var accepted = new TaskCompletionSource<RlpxConnection>();
            listener.PeerAccepted += (_, conn) => accepted.TrySetResult(conn);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var client = new RlpxConnection(EthECKey.GenerateKey(), BuildConfig(300));
            await client.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
            var server = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            return (listener, server, client);
        }

        [Fact]
        public void Given_ADefaultConfig_Then_TheDeadlinesAreGeths()
        {
            var config = new DevP2PConfig();

            Assert.Equal(5_000, config.HandshakeTimeoutMs);
            Assert.Equal(5_000, config.StatusHandshakeTimeoutMs);
            Assert.Equal(30_000, config.ReadTimeoutMs);
            Assert.Equal(15_000, config.PingIntervalMs);
        }

        [Fact]
        public async Task Given_APeerThatPingsButNeverSendsStatus_When_ReceivingStatus_Then_TheReceiveFailsAtTheStatusDeadline()
        {
            var (listener, server, client) = await ConnectOverLoopbackAsync();
            try
            {
                client.StartDispatching();
                var started = DateTime.UtcNow;
                var receive = server.ReceiveStatusAsync();

                var completed = await Task.WhenAny(receive, Task.Delay(StatusHandshakeTimeoutMs * 4));

                Assert.Same(receive, completed);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await receive);
                Assert.True(DateTime.UtcNow - started < TimeSpan.FromMilliseconds(StatusHandshakeTimeoutMs * 2));
            }
            finally
            {
                try { await client.DisconnectAsync(); } catch { }
                try { await listener.StopAsync(); } catch { }
            }
        }

        [Fact]
        public async Task Given_APeerThatSendsStatusBeforeTheDeadline_When_ReceivingStatus_Then_TheStatusIsReturned()
        {
            var (listener, server, client) = await ConnectOverLoopbackAsync();
            try
            {
                var receive = server.ReceiveStatusAsync();

                await Task.Delay(StatusHandshakeTimeoutMs / 3);
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
    }
}
