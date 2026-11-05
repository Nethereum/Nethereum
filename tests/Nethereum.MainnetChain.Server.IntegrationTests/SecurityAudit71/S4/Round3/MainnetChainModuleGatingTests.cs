using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Rpc;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class MainnetChainModuleGatingTests
    {
        [Theory]
        [InlineData("web3")]
        [InlineData("net,web3")]
        [InlineData("net")]
        [Trait("Finding", "S4-R3-02")]
        public void Given_AMainnetChainServerWithoutTheEthModule_When_ItsRpcRegistryIsInspected_Then_NoEthMethodIsRegistered(string modules)
        {
            var services = new ServiceCollection();
            services.AddMainnetChainServer(new MainnetChainServerConfig { RpcModules = modules });
            using var provider = services.BuildServiceProvider();

            var registry = provider.GetRequiredService<RpcHandlerRegistry>();

            var ethMethods = registry.GetAllMethodNames()
                .Where(name => name.StartsWith("eth_", StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.Empty(ethMethods);
        }
    }
}
