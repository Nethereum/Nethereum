using Nethereum.Geth.RPC.Miner;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;

namespace Nethereum.Geth
{
    public class MinerApiService : RpcClientWrapper
    {
        public MinerApiService(IClient client) : base(client)
        {
            SetGasPrice = new MinerSetGasPrice(client);
            Start = new MinerStart(client);
            Stop = new MinerStop(client);
        }

        public MinerSetGasPrice SetGasPrice { get; }
        public MinerStart Start { get; }
        public MinerStop Stop { get; }
    }
}