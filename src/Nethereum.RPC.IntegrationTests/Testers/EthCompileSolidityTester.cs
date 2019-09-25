using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Compilation;
using Nethereum.XUnitEthereumClients;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EthCompileSolidityTester : RPCRequestTester<JToken>, IRPCRequestTester
    {
        public EthCompileSolidityTester(
            EthereumClientIntegrationFixture ethereumClientIntegrationFixture) : 
            base(ethereumClientIntegrationFixture, TestSettingsCategory.localTestNet)
        {
        }

        [Fact(Skip = "Geth removed support for this method in 2016")]
        public async void ShouldReturnJTokenObject()
        {
            var result = await ExecuteAsync();
            //parity returns the bytecode, geth retuns the whole json
            Assert.NotNull(result);
        }


        public override async Task<JToken> ExecuteAsync(IClient client)
        {
            var ethCompileSolidty = new EthCompileSolidity(client);
            var contractCode = "contract Test {}";
            return await ethCompileSolidty.SendRequestAsync(contractCode);
        }

        public override Type GetRequestType()
        {
            return typeof (EthCompileSolidity);
        }
    }
}