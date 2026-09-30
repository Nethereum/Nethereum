using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    public class RlpxListenerMaxPeersTests
    {
        private readonly ITestOutputHelper _output;

        public RlpxListenerMaxPeersTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Given_MaxPeersReached_When_UntrustedPeerConnects_Then_RejectedWithTooManyPeers()
        {
            var serverKey = EthECKey.GenerateKey();
            var firstClientKey = EthECKey.GenerateKey();
            var secondClientKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/test",
                MaxPeers = 1,
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000
            };

            var listener = new RlpxListener(serverKey, config);
            var firstAccepted = new TaskCompletionSource<RlpxConnection>();
            var maxPeersRejected = new TaskCompletionSource<RlpxListenerErrorEventArgs>();
            int peerAcceptedCount = 0;

            listener.PeerAccepted += (_, conn) =>
            {
                Interlocked.Increment(ref peerAcceptedCount);
                firstAccepted.TrySetResult(conn);
            };
            listener.PeerFailed += (_, e) =>
            {
                _output.WriteLine($"PeerFailed [{e.Phase}]: {e.Exception.Message}");
                if (e.Phase == "MaxPeers") maxPeersRejected.TrySetResult(e);
            };

            listener.Start(port: 0, bindAddress: IPAddress.Loopback);
            try
            {
                var firstClient = new RlpxConnection(firstClientKey, config);
                await firstClient.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
                await firstAccepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(1, listener.ActivePeers);

                var secondClient = new RlpxConnection(secondClientKey, config);
                var secondClientDisconnected = new TaskCompletionSource<object>();
                secondClient.Disconnected += (_, _) => secondClientDisconnected.TrySetResult(null);
                await secondClient.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());

                var rejection = await maxPeersRejected.Task.WaitAsync(TimeSpan.FromSeconds(3));
                Assert.Contains("MaxPeers 1", rejection.Exception.Message);

                Assert.Equal(1, peerAcceptedCount);
                Assert.Equal(1, listener.ActivePeers);

                await firstClient.DisconnectAsync();
            }
            finally
            {
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_MaxPeersReached_When_TrustedPeerConnects_Then_Admitted()
        {
            var serverKey = EthECKey.GenerateKey();
            var firstClientKey = EthECKey.GenerateKey();
            var trustedKey = EthECKey.GenerateKey();

            var serverConfig = new DevP2PConfig
            {
                ClientId = "Nethereum/test",
                MaxPeers = 1,
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000,
                TrustedNodeIds = new[] { trustedKey.GetPubKeyNoPrefix().ToHex() }
            };
            var clientConfig = new DevP2PConfig
            {
                ClientId = "Nethereum/test",
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000
            };

            var listener = new RlpxListener(serverKey, serverConfig);
            int peerAcceptedCount = 0;
            var firstAccepted = new TaskCompletionSource<RlpxConnection>();
            var trustedAccepted = new TaskCompletionSource<RlpxConnection>();

            listener.PeerAccepted += (_, conn) =>
            {
                int n = Interlocked.Increment(ref peerAcceptedCount);
                if (n == 1) firstAccepted.TrySetResult(conn);
                else trustedAccepted.TrySetResult(conn);
            };
            listener.PeerFailed += (_, e) =>
                _output.WriteLine($"PeerFailed [{e.Phase}]: {e.Exception.Message}");

            listener.Start(port: 0, bindAddress: IPAddress.Loopback);
            try
            {
                var firstClient = new RlpxConnection(firstClientKey, clientConfig);
                await firstClient.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
                await firstAccepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(1, listener.ActivePeers);

                var trustedClient = new RlpxConnection(trustedKey, clientConfig);
                await trustedClient.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
                await trustedAccepted.Task.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.Equal(2, listener.ActivePeers);
                Assert.Equal(2, peerAcceptedCount);
                Assert.True(listener.IsTrustedNodeId(trustedKey.GetPubKeyNoPrefix()));
                Assert.False(listener.IsTrustedNodeId(firstClientKey.GetPubKeyNoPrefix()));

                await firstClient.DisconnectAsync();
                await trustedClient.DisconnectAsync();
            }
            finally
            {
                await listener.StopAsync();
            }
        }
    }
}
