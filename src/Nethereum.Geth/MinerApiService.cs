using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Miner;

namespace Nethereum.Web3
{
    public class MinerApiService : RpcClientWrapper
    {
        public MinerApiService(IClient client) : base(client)
        {
            SetGasPrice = new MinerSetGasPrice(client);
            Start = new MinerStart(client);
            Stop = new MinerStop(client);
        }

        public MinerSetGasPrice SetGasPrice { get; private set; }
        public MinerStart Start { get; private set; }
        public MinerStop Stop { get; private set; }
    }
}