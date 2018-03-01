using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Compilation;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthCompileSolidityTester : RPCRequestTester<JToken>, IRPCRequestTester
    {
        [Fact]
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