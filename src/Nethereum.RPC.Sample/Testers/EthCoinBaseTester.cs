using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthCoinBaseTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethCoinBase = new EthCoinBase(client);
            return await ethCoinBase.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof (EthCoinBase);
        }
    }
}