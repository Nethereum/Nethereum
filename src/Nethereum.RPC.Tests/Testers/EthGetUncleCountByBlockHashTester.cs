using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Eth.Uncles;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthGetUncleCountByBlockHashTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
        {
            var ethGetUncleCountByBlockHash = new EthGetUncleCountByBlockHash(client);
            return
                await
                    ethGetUncleCountByBlockHash.SendRequestAsync(
                        "0xb903239f8543d04b5dc1ba6579132b143087c68db1b2168786408fcbce568238");
        }

        public Type GetRequestType()
        {
            return typeof (EthGetUncleCountByBlockHash);
        }
    }
}