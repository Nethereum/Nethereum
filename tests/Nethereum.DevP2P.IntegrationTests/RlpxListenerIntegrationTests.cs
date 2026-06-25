using System;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    public class RlpxListenerIntegrationTests
    {
        private readonly ITestOutputHelper _output;

        public RlpxListenerIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task TwoNethereumNodes_PeerViaDevP2P_HelloAndCapabilities()
        {
            var serverKey = EthECKey.GenerateKey();
            var clientKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/test",
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000
            };

            var listener = new RlpxListener(serverKey, config);
            RlpxConnection? acceptedConnection = null;
            var acceptedTcs = new TaskCompletionSource<RlpxConnection>();
            listener.PeerAccepted += (_, conn) =>
            {
                acceptedConnection = conn;
                acceptedTcs.TrySetResult(conn);
            };
            listener.PeerFailed += (_, e) =>
                _output.WriteLine($"Listener failure [{e.Phase}]: {e.Exception.Message}");

            listener.Start(port: 0, bindAddress: IPAddress.Loopback);
            var listenerPort = listener.Port;
            Assert.True(listenerPort > 0, $"Listener port should be > 0, got {listenerPort}");
            _output.WriteLine($"Listener bound on 127.0.0.1:{listenerPort}");

            var clientConn = new RlpxConnection(clientKey, config);
            try
            {
                await clientConn.ConnectAsync("127.0.0.1", listenerPort, serverKey.GetPubKeyNoPrefix());
                var server = await acceptedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.True(clientConn.IsConnected, "Client should be connected after handshake");
                Assert.True(server.IsConnected, "Server should be connected after handshake");

                Assert.NotNull(clientConn.RemoteHello);
                Assert.NotNull(server.RemoteHello);
                Assert.Equal("Nethereum/test", clientConn.RemoteHello!.ClientId);
                Assert.Equal("Nethereum/test", server.RemoteHello!.ClientId);

                Assert.NotEmpty(clientConn.SharedCapabilities);
                Assert.NotEmpty(server.SharedCapabilities);
                Assert.Contains(clientConn.SharedCapabilities, c => c.Name == "eth" && c.Version >= 68);

                _output.WriteLine($"Client sees: {clientConn.RemoteHello.ClientId} caps={string.Join(",", clientConn.SharedCapabilities.ConvertAll(c => $"{c.Name}/{c.Version}"))}");
                _output.WriteLine($"Server sees: {server.RemoteHello.ClientId} caps={string.Join(",", server.SharedCapabilities.ConvertAll(c => $"{c.Name}/{c.Version}"))}");

                await clientConn.DisconnectAsync();
            }
            finally
            {
                await listener.StopAsync();
            }
        }
    }
}
