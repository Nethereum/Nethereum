using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Mining;

namespace Nethereum.RPC.Eth.Services
{
    public class EthApiMiningService : RpcClientWrapper, IEthApiMiningService
    {
        public EthApiMiningService(IClient client) : base(client)
        {
            SubmitHashrate = new EthSubmitHashrate(client);
            SubmitWork = new EthSubmitWork(client);
            GetWork = new EthGetWork(client);
            Hashrate = new EthHashrate(client);
            IsMining = new EthMining(client);
        }

        public IEthSubmitHashrate SubmitHashrate { get; private set; }
        public IEthSubmitWork SubmitWork { get; private set; }

        public IEthGetWork GetWork { get; private set; }
        public IEthHashrate Hashrate { get; private set; }
        public IEthMining IsMining { get; private set; }
    }
}