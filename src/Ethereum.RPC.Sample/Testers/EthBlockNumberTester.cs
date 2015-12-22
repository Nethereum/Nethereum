
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthBlockNumberTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethBlockNumber = new EthBlockNumber();
            return await ethBlockNumber.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(EthBlockNumber);
        }
    }
}
        