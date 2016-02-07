using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.Blocks;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthBlockNumberTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethBlockNumber = new EthBlockNumber(client);
            return await ethBlockNumber.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof(EthBlockNumber);
        }
    }
}
        