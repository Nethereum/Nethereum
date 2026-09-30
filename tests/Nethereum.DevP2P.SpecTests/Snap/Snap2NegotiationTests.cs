using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Snap
{
    public class Snap2NegotiationTests
    {
        private static DevP2PConfig BuildConfig(string clientId, bool advertiseSnap2) => new DevP2PConfig
        {
            ClientId = clientId,
            HandshakeTimeoutMs = 10_000,
            ConnectTimeoutMs = 10_000,
            RequestTimeoutMs = 10_000,
            ReadTimeoutMs = 30_000,
            PingIntervalMs = 60_000,
            AdvertiseSnap2 = advertiseSnap2
        };

        [Fact]
        public void AdvertiseSnap2_DefaultsFalse()
        {
            Assert.False(new DevP2PConfig().AdvertiseSnap2);
        }

        [Fact]
        public async Task Hello_DoesNotAdvertiseSnap2_ByDefault()
        {
            var serverKey = EthECKey.GenerateKey();
            var clientKey = EthECKey.GenerateKey();

            var listener = new RlpxListener(serverKey, BuildConfig("Nethereum.Spec.Tests/snap2-hello-off-server", advertiseSnap2: false));
            var acceptedTcs = new TaskCompletionSource<RlpxConnection>();
            listener.PeerAccepted += (_, conn) => acceptedTcs.TrySetResult(conn);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var clientConn = new RlpxConnection(clientKey, BuildConfig("Nethereum.Spec.Tests/snap2-hello-off-client", advertiseSnap2: false));
            try
            {
                await clientConn.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
                var serverConn = await acceptedTcs.Task.WaitAsync(System.TimeSpan.FromSeconds(5));

                AssertSnap1OnlyNoSnap2(serverConn.RemoteHello.Capabilities);
                AssertSnap1OnlyNoSnap2(clientConn.RemoteHello.Capabilities);

                var negotiatedSnap = clientConn.SharedCapabilities.Single(c => c.Name == "snap");
                Assert.Equal(1, negotiatedSnap.Version);
                Assert.Equal(8, negotiatedSnap.Length);
            }
            finally
            {
                try { clientConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Hello_AdvertisesSnap2_WhenFlagOn()
        {
            var serverKey = EthECKey.GenerateKey();
            var clientKey = EthECKey.GenerateKey();

            var listener = new RlpxListener(serverKey, BuildConfig("Nethereum.Spec.Tests/snap2-hello-on-server", advertiseSnap2: true));
            var acceptedTcs = new TaskCompletionSource<RlpxConnection>();
            listener.PeerAccepted += (_, conn) => acceptedTcs.TrySetResult(conn);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var clientConn = new RlpxConnection(clientKey, BuildConfig("Nethereum.Spec.Tests/snap2-hello-on-client", advertiseSnap2: true));
            try
            {
                await clientConn.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
                var serverConn = await acceptedTcs.Task.WaitAsync(System.TimeSpan.FromSeconds(5));

                AssertAdvertisesSnap1AndSnap2(serverConn.RemoteHello.Capabilities);
                AssertAdvertisesSnap1AndSnap2(clientConn.RemoteHello.Capabilities);

                var negotiatedSnap = clientConn.SharedCapabilities.Single(c => c.Name == "snap");
                Assert.Equal(2, negotiatedSnap.Version);
                Assert.Equal(10, negotiatedSnap.Length);
            }
            finally
            {
                try { clientConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }

        private static void AssertSnap1OnlyNoSnap2(System.Collections.Generic.List<Nethereum.Model.P2P.P2PCapability> capabilities)
        {
            Assert.Contains(capabilities, c => c.Name == "snap" && c.Version == 1);
            Assert.DoesNotContain(capabilities, c => c.Name == "snap" && c.Version == 2);
        }

        private static void AssertAdvertisesSnap1AndSnap2(System.Collections.Generic.List<Nethereum.Model.P2P.P2PCapability> capabilities)
        {
            Assert.Contains(capabilities, c => c.Name == "snap" && c.Version == 1);
            Assert.Contains(capabilities, c => c.Name == "snap" && c.Version == 2);
        }
    }
}
