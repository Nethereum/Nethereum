using System.Reflection;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.CoreChain.UnitTests.Rpc.TestSupport;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class Web3ClientVersionHandlerTests
    {
        [Fact]
        public async Task Given_TheClientVersionHandler_When_Called_Then_ItReportsTheAssemblyVersion()
        {
            var node = new FakeRpcChainNode();
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new Web3ClientVersionHandler();

            var response = await handler.HandleAsync(new RpcRequestMessage(1, "web3_clientVersion"), context);

            var informationalVersion = typeof(Web3ClientVersionHandler).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var plusIndex = informationalVersion?.IndexOf('+') ?? -1;
            var expectedVersion = plusIndex >= 0 ? informationalVersion.Substring(0, plusIndex) : informationalVersion;

            Assert.Equal("Nethereum/v" + expectedVersion + "/dotnet", response.ResultNewtonsoft);
        }

        [Fact]
        public async Task Given_TheClientVersionHandler_When_Called_Then_ItDoesNotReportTheHardcodedPlaceholderOrBuildMetadata()
        {
            var node = new FakeRpcChainNode();
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new Web3ClientVersionHandler();

            var response = await handler.HandleAsync(new RpcRequestMessage(1, "web3_clientVersion"), context);
            var result = (string)response.ResultNewtonsoft;

            Assert.DoesNotContain("v1.0.0", result);
            Assert.DoesNotContain("+", result);
        }
    }
}
