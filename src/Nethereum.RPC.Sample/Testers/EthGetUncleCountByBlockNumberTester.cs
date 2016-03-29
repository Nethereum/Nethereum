using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Uncles;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthGetUncleCountByBlockNumberTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetUncleCountByBlockNumber = new EthGetUncleCountByBlockNumber(client);
            return await ethGetUncleCountByBlockNumber.SendRequestAsync(new HexBigInteger(2));
        }

        public Type GetRequestType()
        {
            return typeof (EthGetUncleCountByBlockNumber);
        }
    }
}