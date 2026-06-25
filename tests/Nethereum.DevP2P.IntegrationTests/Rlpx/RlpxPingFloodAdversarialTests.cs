using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests.Rlpx
{
    /// <summary>
    /// End-to-end adversarial tests for the per-peer Ping/Pong budget
    /// (<see cref="PingBurstCapacity"/> burst with 1 token/s refill) and for the
    /// removal of the inner read-deadline reset on Ping/Pong. A real loopback
    /// RLPx listener is used, a client connection completes the handshake, and
    /// the client uses <c>SendMessageAsync</c> to drive Ping/Pong traffic.
    /// </summary>
    public class RlpxPingFloodAdversarialTests
    {
        private const int PingBurstCapacity = 4;
        private const int PingWindowSeconds = 5;

        private readonly ITestOutputHelper _output;
        public RlpxPingFloodAdversarialTests(ITestOutputHelper output) { _output = output; }

        private static DevP2PConfig BuildConfig(int readTimeoutMs = 30_000) => new DevP2PConfig
        {
            ClientId = "Nethereum/ping-flood-test",
            HandshakeTimeoutMs = 5_000,
            ConnectTimeoutMs = 5_000,
            RequestTimeoutMs = 5_000,
            ReadTimeoutMs = readTimeoutMs,
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
            listener.PeerFailed += (_, e) =>
                _output.WriteLine($"server-side: {e.Phase}: {e.Exception.GetType().Name}: {e.Exception.Message}");
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var clientConn = new RlpxConnection(clientKey, config);
            await clientConn.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
            var serverConn = await acceptedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            return (listener, serverConn, clientConn);
        }

        [Fact]
        public async Task Given_PeerSendsFourPingsInOneSecond_When_BudgetOk_Then_ConnectionSurvives()
        {
            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync();
            try
            {
                Assert.True(clientConn.IsConnected);
                Assert.True(serverConn.IsConnected);

                for (int i = 0; i < PingBurstCapacity; i++)
                {
                    await clientConn.SendMessageAsync(P2PMessageIds.Ping, Array.Empty<byte>());
                }

                // Server consumes the four pings + its own Pong replies; if the
                // burst is within budget the connection stays open and the
                // server-side ReceiveMessageAsync just keeps reading.
                await Task.Delay(500);

                Assert.True(serverConn.IsConnected,
                    "burst within MaxPingsPerWindow must not trigger disconnect");
                Assert.True(clientConn.IsConnected,
                    "client side must observe peer still online after honest burst");
            }
            finally
            {
                try { await clientConn.DisconnectAsync(); } catch { }
                try { await listener.StopAsync(); } catch { }
            }
        }

        [Fact]
        public async Task Given_PeerFloodsPingsAboveBurst_When_BudgetExceeded_Then_ServerDisconnects()
        {
            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync();
            try
            {
                var serverReceiveTask = Task.Run(async () =>
                {
                    try
                    {
                        while (serverConn.IsConnected)
                        {
                            await serverConn.ReceiveMessageAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"server-side ReceiveMessageAsync threw: {ex.GetType().Name}: {ex.Message}");
                    }
                });

                int overBurst = PingBurstCapacity + 4;
                for (int i = 0; i < overBurst; i++)
                {
                    try { await clientConn.SendMessageAsync(P2PMessageIds.Ping, Array.Empty<byte>()); }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"client send #{i + 1} failed: {ex.GetType().Name}: {ex.Message}");
                        break;
                    }
                }

                // Allow time for the server to process the burst, disconnect, and
                // the client to observe the connection going down.
                var sw = Stopwatch.StartNew();
                while (sw.Elapsed < TimeSpan.FromSeconds(10) && serverConn.IsConnected)
                {
                    await Task.Delay(100);
                }

                Assert.False(serverConn.IsConnected,
                    "server must disconnect a peer that exceeds the Ping burst budget");
            }
            finally
            {
                try { await clientConn.DisconnectAsync(); } catch { }
                try { await listener.StopAsync(); } catch { }
            }
        }

        [Fact]
        public async Task Given_PeerPingsOncePerWindow_When_BudgetEvaluated_Then_ConnectionStaysAliveAcrossWindows()
        {
            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync();
            try
            {
                var serverReceiveTask = Task.Run(async () =>
                {
                    try
                    {
                        while (serverConn.IsConnected)
                            await serverConn.ReceiveMessageAsync();
                    }
                    catch { }
                });

                // Send one ping then wait long enough that the bucket is full
                // again before the next - mirrors an honest peer pinging slower
                // than the budget refill rate.
                for (int round = 0; round < 3; round++)
                {
                    await clientConn.SendMessageAsync(P2PMessageIds.Ping, Array.Empty<byte>());
                    await Task.Delay(TimeSpan.FromSeconds(PingWindowSeconds + 1));
                    Assert.True(serverConn.IsConnected,
                        $"server must remain connected after honest ping round {round + 1}");
                }
            }
            finally
            {
                try { await clientConn.DisconnectAsync(); } catch { }
                try { await listener.StopAsync(); } catch { }
            }
        }
    }
}
