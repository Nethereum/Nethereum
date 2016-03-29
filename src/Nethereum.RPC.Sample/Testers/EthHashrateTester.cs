using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Mining;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthHashrateTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethHashrate = new EthHashrate(client);
            return await ethHashrate.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof (EthHashrate);
        }
    }
}