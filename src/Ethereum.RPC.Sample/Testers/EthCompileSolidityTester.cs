using System;
using edjCase.JsonRpc.Client;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample
{
    public class EthCompileSolidityTester : IRPCRequestTester
    {
        public dynamic ExecuteTest(RpcClient client)
        {
            var ethCompileSolidty = new EthCompileSolidity();
            var contractCode = "contract test { function multiply(uint a) returns(uint d) { return a * 7; } }";
            return ethCompileSolidty.SendRequestAsync(client, contractCode).Result;
        }

        public Type GetRequestType()
        {
            return typeof(EthCompileSolidity);
        }
    }
}