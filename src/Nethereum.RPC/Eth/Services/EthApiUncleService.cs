using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Uncles;

namespace Nethereum.RPC.Eth.Services
{
    public class EthApiUncleService : RpcClientWrapper
    {
        public EthApiUncleService(IClient client) : base(client)
        {
            GetUncleCountByBlockHash = new EthGetUncleCountByBlockHash(client);
            GetUncleCountByBlockNumber = new EthGetUncleCountByBlockNumber(client);
            GetUncleByBlockHashAndIndex = new EthGetUncleByBlockHashAndIndex(client);
            GetUncleByBlockNumberAndIndex = new EthGetUncleByBlockNumberAndIndex(client);
        }

        public EthGetUncleCountByBlockHash GetUncleCountByBlockHash { get; private set; }
        public EthGetUncleCountByBlockNumber GetUncleCountByBlockNumber { get; private set; }
        public EthGetUncleByBlockHashAndIndex GetUncleByBlockHashAndIndex { get; private set; }
        public EthGetUncleByBlockNumberAndIndex GetUncleByBlockNumberAndIndex { get; private set; }
    }
}