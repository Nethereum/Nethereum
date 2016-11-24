using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Miner;

namespace Nethereum.Web3
{
    public class Miner : RpcClientWrapper
    {
        public MinerSetGasPrice SetGasPrice { get; private set; }
        public MinerStart Start { get; private set; }
        public MinerStop Stop { get; private set; }

        public Miner(IClient client) : base(client)
        {
            SetGasPrice = new MinerSetGasPrice(client);
            Start = new MinerStart(client);
            Stop = new MinerStop(client);
        }
    }
}