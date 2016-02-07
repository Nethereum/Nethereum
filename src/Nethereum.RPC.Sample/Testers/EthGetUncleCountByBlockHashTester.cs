using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.Uncles;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthGetUncleCountByBlockHashTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetUncleCountByBlockHash = new EthGetUncleCountByBlockHash(client);
            return await ethGetUncleCountByBlockHash.SendRequestAsync( "0xb903239f8543d04b5dc1ba6579132b143087c68db1b2168786408fcbce568238");
        }

        public Type GetRequestType()
        {
            return typeof(EthGetUncleCountByBlockHash);
        }
    }
}
        