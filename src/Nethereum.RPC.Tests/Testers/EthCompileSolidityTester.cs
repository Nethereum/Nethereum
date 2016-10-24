using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Compilation;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthCompileSolidityTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldReturnJTokenObject()
        {
            var result = await ExecuteAsync(ClientFactory.GetClient());
            Assert.NotNull(result);
        }


        public override async Task<JObject> ExecuteAsync(IClient client)
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