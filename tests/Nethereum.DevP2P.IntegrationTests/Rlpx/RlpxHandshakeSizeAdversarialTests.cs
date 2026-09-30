using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests.Rlpx
{
    public class RlpxHandshakeSizeAdversarialTests
    {
        private readonly ITestOutputHelper _output;

        public RlpxHandshakeSizeAdversarialTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Given_ForgedAuthSize0xFFFFAndTruncatedBody_When_ListenerAccepts_Then_ClosedWithoutOom()
        {
            var serverKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/adversarial-test",
                HandshakeTimeoutMs = 5000
            };

            var listener = new RlpxListener(serverKey, config);
            Exception observedFailure = null;
            var failureTcs = new TaskCompletionSource<Exception>();
            listener.PeerFailed += (_, e) =>
            {
                observedFailure = e.Exception;
                failureTcs.TrySetResult(e.Exception);
                _output.WriteLine($"Listener failure [{e.Phase}]: {e.Exception.GetType().Name}: {e.Exception.Message}");
            };

            listener.Start(port: 0, bindAddress: IPAddress.Loopback);
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, listener.Port);

                var forgedPrefix = new byte[] { 0xFF, 0xFF };
                await client.GetStream().WriteAsync(forgedPrefix, 0, forgedPrefix.Length);

                var garbage = new byte[64];
                await client.GetStream().WriteAsync(garbage, 0, garbage.Length);

                var failureTask = failureTcs.Task;
                var completed = await Task.WhenAny(failureTask, Task.Delay(TimeSpan.FromSeconds(5)));
                Assert.True(completed == failureTask,
                    "Listener did not surface a failure within 5s — size check may not be guarding allocation");

                Assert.NotNull(observedFailure);
                var msg = observedFailure.Message ?? string.Empty;
                Assert.True(msg.Contains("out of range") || msg.Contains("65535"),
                    $"Expected size-bound rejection, got: {observedFailure.GetType().Name}: {msg}");

                Assert.IsNotType<OutOfMemoryException>(observedFailure);
            }
            finally
            {
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_HonestEip8Auth_When_ListenerAccepts_Then_HandshakeCompletes()
        {
            var serverKey = EthECKey.GenerateKey();
            var clientKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/adversarial-test",
                HandshakeTimeoutMs = 5000,
                ConnectTimeoutMs = 5000
            };

            var listener = new RlpxListener(serverKey, config);
            var acceptedTcs = new TaskCompletionSource<RlpxConnection>();
            listener.PeerAccepted += (_, conn) => acceptedTcs.TrySetResult(conn);
            listener.PeerFailed += (_, e) =>
                _output.WriteLine($"unexpected failure: {e.Phase}: {e.Exception.Message}");

            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var clientConn = new RlpxConnection(clientKey, config);
            try
            {
                await clientConn.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
                var serverConn = await acceptedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.True(clientConn.IsConnected);
                Assert.True(serverConn.IsConnected);
                Assert.NotEmpty(clientConn.SharedCapabilities);
                await clientConn.DisconnectAsync();
            }
            finally
            {
                await listener.StopAsync();
            }
        }
    }
}
