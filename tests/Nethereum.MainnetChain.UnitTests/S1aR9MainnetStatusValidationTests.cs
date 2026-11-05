using Nethereum.MainnetChain.Configuration;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class S1aR9MainnetStatusValidationTests
    {
        [Fact]
        public void S1aR9_13_Given_TheDefaultMainnetServerConfig_When_ItIsMappedToAChainNodeConfig_Then_TheServeListenerChecksInboundStatusesAgainstTheMainnetChain()
        {
            var node = new MainnetChainServerConfig().ToChainNodeConfig();

            Assert.False(
                node.Network.MirrorRemoteStatus,
                "MirrorRemoteStatus=true makes the serve listener skip the network id, genesis and fork id checks of EthStatusValidator, so a peer of any chain stays registered and served");
        }
    }
}
