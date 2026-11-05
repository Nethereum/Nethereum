using Nethereum.CoreChain.Rpc;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.SecurityAudit71.S4.Round2
{
    public class RpcModuleGatingTests
    {
        [Theory]
        [Trait("Finding", "S4-R2-03")]
        [InlineData("web3", "eth_call")]
        [InlineData("web3", "eth_sendRawTransaction")]
        [InlineData("web3", "net_version")]
        [InlineData("net,web3", "eth_getLogs")]
        [InlineData("eth", "web3_clientVersion")]
        [InlineData("eth", "net_peerCount")]
        public void Given_AModuleListThatLeavesOutANamespace_When_TheStandardHandlersAreRegistered_Then_NoMethodOfTheOmittedNamespaceIsServed(string modules, string method)
        {
            var registry = new RpcHandlerRegistry();

            registry.AddStandardHandlers(RpcModuleSet.Parse(modules));

            Assert.Null(registry.GetHandler(method));
        }
    }
}
