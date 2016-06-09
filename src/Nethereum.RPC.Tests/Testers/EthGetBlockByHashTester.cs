using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthGetBlockByHashTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
        {
            var ethGetBlockByHash = new EthGetBlockWithTransactionsByHash(client);
            return
                await
                    ethGetBlockByHash.SendRequestAsync(
                        "0xe670ec64341771606e55d6b4ca35a1a6b75ee3d5145a99d05921026d1527331");
        }

        public Type GetRequestType()
        {
            return typeof (EthGetBlockWithTransactionsByHash);
        }
    }
}