
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthHashrateTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethHashrate = new EthHashrate();
            return await ethHashrate.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(EthHashrate);
        }
    }
}
        