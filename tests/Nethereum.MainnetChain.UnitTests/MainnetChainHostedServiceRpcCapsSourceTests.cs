using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class MainnetChainHostedServiceRpcCapsSourceTests
    {
        private static MainnetChainServerConfig FlatConfigWith(int maxLogBlockRange, int maxLogResults, long gasCap) =>
            new MainnetChainServerConfig
            {
                RpcMaxLogBlockRange = maxLogBlockRange,
                RpcMaxLogResults = maxLogResults,
                RpcGasCap = gasCap,
            };

        [Fact]
        public void Given_MainnetChainServerConfigWithNonDefaultRpcCaps_When_TheFollowerNodeIsBuilt_Then_ChainConfigRpcCapsComeFromTheMappedChainNodeConfig()
        {
            var flatConfig = FlatConfigWith(111, 222, 333);
            var mappedConfig = flatConfig.ToChainNodeConfig();

            var caps = MainnetChainHostedService.ResolveRpcCaps(mappedConfig, flatConfig);

            Assert.Equal(111, caps.MaxLogBlockRange);
            Assert.Equal(222, caps.MaxLogResults);
            Assert.Equal(333, caps.GasCap);
        }

        [Fact]
        public void Given_TheMappedChainNodeConfigRpcCapsDivergeFromTheFlatConfig_When_TheFollowerNodeIsBuilt_Then_TheMappedValueWins()
        {
            var flatConfig = FlatConfigWith(111, 222, 333);
            var mappedConfig = flatConfig.ToChainNodeConfig();
            mappedConfig.Rpc.MaxLogBlockRange = 999;
            mappedConfig.Rpc.MaxLogResults = 888;
            mappedConfig.Rpc.GasCap = 777;

            var caps = MainnetChainHostedService.ResolveRpcCaps(mappedConfig, flatConfig);

            Assert.Equal(999, caps.MaxLogBlockRange);
            Assert.Equal(888, caps.MaxLogResults);
            Assert.Equal(777, caps.GasCap);
        }

        [Fact]
        public void Given_NoMappedChainNodeConfig_When_TheFollowerNodeIsBuilt_Then_TheFlatConfigIsUsed()
        {
            var flatConfig = FlatConfigWith(111, 222, 333);

            var caps = MainnetChainHostedService.ResolveRpcCaps(null, flatConfig);

            Assert.Equal(111, caps.MaxLogBlockRange);
            Assert.Equal(222, caps.MaxLogResults);
            Assert.Equal(333, caps.GasCap);
        }
    }
}
