using Nethereum.JsonRpc.Client;
using Nethereum.Pantheon.RPC.Miner;
using Nethereum.RPC;

namespace Nethereum.Pantheon
{
    public interface IMinerApiService
    {
        IMinerStart Start { get; }
        IMinerStop Stop { get; }
    }

    public class MinerApiService : RpcClientWrapper, IMinerApiService
    {
        public MinerApiService(IClient client) : base(client)
        {
            Start = new MinerStart(client);
            Stop = new MinerStop(client);
        }

        public IMinerStart Start { get; }
        public IMinerStop Stop { get; }
    }
}