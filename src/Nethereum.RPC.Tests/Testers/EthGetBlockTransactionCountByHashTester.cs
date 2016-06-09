using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthGetBlockTransactionCountByHashTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
        {
            var ethGetBlockTransactionCountByHash = new EthGetBlockTransactionCountByHash(client);
            return
                await
                    ethGetBlockTransactionCountByHash.SendRequestAsync(
                        "0xb903239f8543d04b5dc1ba6579132b143087c68db1b2168786408fcbce568238");
        }

        public Type GetRequestType()
        {
            return typeof (EthGetBlockTransactionCountByHash);
        }
    }
}