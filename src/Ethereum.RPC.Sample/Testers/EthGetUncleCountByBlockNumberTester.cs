
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthGetUncleCountByBlockNumberTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetUncleCountByBlockNumber = new EthGetUncleCountByBlockNumber();
            return await ethGetUncleCountByBlockNumber.SendRequestAsync(client, new HexBigInteger(2));
        }

        public Type GetRequestType()
        {
            return typeof(EthGetUncleCountByBlockNumber);
        }
    }
}
        