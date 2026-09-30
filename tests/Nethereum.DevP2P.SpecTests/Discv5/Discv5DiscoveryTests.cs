using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv5
{
    public class Discv5DiscoveryTests
    {
        [Fact]
        public void StartMainnet_NullLocalKey_Throws_BeforeBindingSocket()
        {
            Assert.Throws<ArgumentNullException>(() =>
                Discv5Discovery.StartMainnet(localKey: null, enqueueEnode: _ => { }));
        }

        [Fact]
        public void StartMainnet_NullEnqueue_Throws_BeforeBindingSocket()
        {
            Assert.Throws<ArgumentNullException>(() =>
                Discv5Discovery.StartMainnet(EthECKey.GenerateKey(), enqueueEnode: null));
        }

        [Fact]
        public async Task StartMainnet_LoopbackBind_WiresAndReturnsStartedService()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var (discovery, listener) = Discv5Discovery.StartMainnet(
                EthECKey.GenerateKey(),
                enqueueEnode: _ => { },
                bindAddress: IPAddress.Loopback,
                udpPort: 0,
                ct: cts.Token);

            try
            {
                Assert.NotNull(discovery);
                Assert.NotNull(listener);
                Assert.True(listener.Port > 0, "listener should be bound to an ephemeral loopback port");
                Assert.NotNull(listener.LocalEnrEncoded);
            }
            finally
            {
                await discovery.StopAsync();
                await listener.DisposeAsync();
            }
        }
    }
}
