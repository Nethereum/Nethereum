using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.XUnitEthereumClients;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{

    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EthCoinBaseTester : RPCRequestTester<string>, IRPCRequestTester
    {
        public EthCoinBaseTester(
            EthereumClientIntegrationFixture ethereumClientIntegrationFixture) : 
            base(ethereumClientIntegrationFixture, TestSettings.GethLocalSettings)
        {
        }

        [Fact]
        public async void ShouldReturnCoinBaseAccount()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<string> ExecuteAsync(IClient client)
        {
            var ethCoinBase = new EthCoinBase(client);
            return await ethCoinBase.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(EthCoinBase);
        }
    }
}