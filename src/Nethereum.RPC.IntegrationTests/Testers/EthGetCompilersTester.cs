using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Compilation;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EthGetCompilersTester : RPCRequestTester<string[]>
    {
        public EthGetCompilersTester(
            EthereumClientIntegrationFixture ethereumClientIntegrationFixture) : 
            base(ethereumClientIntegrationFixture, TestSettingsCategory.localTestNet)
        {
        }

        [Fact(Skip = "Geth removed support for this method in 2016")]
        public async void ShouldReturnCompilers()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
            //we need at least solidity configured
            Assert.True(result.Contains("Solidity"));

        }

        public override async Task<string[]> ExecuteAsync(IClient client)
        {
            var ethGetCompilers = new EthGetCompilers(client);
            return await ethGetCompilers.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(EthGetCompilers);
        }
    }
}