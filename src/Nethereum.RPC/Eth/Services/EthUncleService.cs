using edjCase.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.Uncles;

namespace Nethereum.Web3
{
    public class EthUncleService : RpcClientWrapper
    {
        public EthGetUncleCountByBlockHash GetUncleCountByBlockHash { get; private set; }
        public EthGetUncleCountByBlockNumber GetUncleCountByBlockNumber { get; private set; }

        public EthUncleService(RpcClient client) : base(client)
        {
            GetUncleCountByBlockHash = new EthGetUncleCountByBlockHash(client);
            GetUncleCountByBlockNumber = new EthGetUncleCountByBlockNumber(client);
        }
    }
}