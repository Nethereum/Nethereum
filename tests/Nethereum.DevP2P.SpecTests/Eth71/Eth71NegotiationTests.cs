using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Eth71
{
    public class Eth71NegotiationTests
    {
        private static DevP2PConfig BuildConfig(string clientId) => new DevP2PConfig
        {
            ClientId = clientId,
            HandshakeTimeoutMs = 10_000,
            ConnectTimeoutMs = 10_000,
            RequestTimeoutMs = 10_000,
            ReadTimeoutMs = 30_000,
            PingIntervalMs = 60_000
        };

        [Fact]
        public async Task Hello_AdvertisesEth71()
        {
            var serverKey = EthECKey.GenerateKey();
            var clientKey = EthECKey.GenerateKey();

            var listener = new RlpxListener(serverKey, BuildConfig("Nethereum.Spec.Tests/eth71-hello-server"));
            var acceptedTcs = new TaskCompletionSource<RlpxConnection>();
            listener.PeerAccepted += (_, conn) => acceptedTcs.TrySetResult(conn);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var clientConn = new RlpxConnection(clientKey, BuildConfig("Nethereum.Spec.Tests/eth71-hello-client"));
            try
            {
                await clientConn.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
                var serverConn = await acceptedTcs.Task.WaitAsync(System.TimeSpan.FromSeconds(5));

                AssertAdvertisesEth71AndOlderVersions(serverConn.RemoteHello.Capabilities);
                AssertAdvertisesEth71AndOlderVersions(clientConn.RemoteHello.Capabilities);

                var negotiatedEth = clientConn.SharedCapabilities.Single(c => c.Name == "eth");
                Assert.Equal(71, negotiatedEth.Version);
                Assert.Equal(20, negotiatedEth.Length);
            }
            finally
            {
                try { clientConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }

        private static void AssertAdvertisesEth71AndOlderVersions(System.Collections.Generic.List<Nethereum.Model.P2P.P2PCapability> capabilities)
        {
            Assert.Contains(capabilities, c => c.Name == "eth" && c.Version == 68);
            Assert.Contains(capabilities, c => c.Name == "eth" && c.Version == 69);
            Assert.Contains(capabilities, c => c.Name == "eth" && c.Version == 71);
            Assert.Contains(capabilities, c => c.Name == "snap" && c.Version == 1);
        }
    }
}
