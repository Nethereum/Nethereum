using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Mining;

namespace Nethereum.Web3
{
    public class EthApiMiningService : RpcClientWrapper
    {
        public EthApiMiningService(IClient client) : base(client)
        {
            SubmitHashrate = new EthSubmitHashrate(client);
            SubmitWork = new EthSubmitWork(client);
            GetWork = new EthGetWork(client);
            Hashrate = new EthHashrate(client);
            IsMining = new EthMining(client);
        }

        public EthSubmitHashrate SubmitHashrate { get; private set; }
        public EthSubmitWork SubmitWork { get; private set; }

        public EthGetWork GetWork { get; private set; }
        public EthHashrate Hashrate { get; private set; }
        public EthMining IsMining { get; private set; }
    }
}