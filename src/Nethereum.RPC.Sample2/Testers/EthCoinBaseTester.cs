
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthCoinBaseTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethCoinBase = new EthCoinBase();
            return await ethCoinBase.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(EthCoinBase);
        }
    }
}
        