using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample
{
    public class EthCompileSolidityTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethCompileSolidty = new EthCompileSolidity();
            var contractCode = "contract test { function multiply(uint a) returns(uint d) { return a * 7; } }";
            return await ethCompileSolidty.SendRequestAsync(client, contractCode);
        }

        public Type GetRequestType()
        {
            return typeof(EthCompileSolidity);
        }
    }
}