using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Compilation;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthCompileSolidityTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
        {
            var ethCompileSolidty = new EthCompileSolidity(client);
            var contractCode = "contract test { function multiply(uint a) returns(uint d) { return a * 7; } }";
            return await ethCompileSolidty.SendRequestAsync(contractCode);
        }

        public Type GetRequestType()
        {
            return typeof (EthCompileSolidity);
        }
    }
}