using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth
{
    public class Web3Geth : Web3.Web3
    {
        public Web3Geth(IClient client) : base(client)
        {
        }

        public Web3Geth(string url = @"http://localhost:8545/") : base(url)
        {
        }

        public AdminApiService Admin { get; private set; }

        public DebugApiService Debug { get; private set; }

        public MinerApiService Miner { get; private set; }

        protected override void InitialiseInnerServices()
        {
            base.InitialiseInnerServices();
            Miner = new MinerApiService(Client);
            Debug = new DebugApiService(Client);
            Admin = new AdminApiService(Client);
        }
    }
}