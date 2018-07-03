using System.Net.Http.Headers;
using Common.Logging;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;

namespace Nethereum.Geth
{
    public class Web3Geth : Web3.Web3
    {
        public Web3Geth(IClient client) : base(client)
        {
        }

        public Web3Geth(string url = @"http://localhost:8545/", ILog log = null, AuthenticationHeaderValue authenticationHeader = null) : base(url, log, authenticationHeader)
        {
        }

        public Web3Geth(IAccount account, IClient client) : base(account, client)
        {
        }

        public Web3Geth(IAccount account, string url = @"http://localhost:8545/", ILog log = null, AuthenticationHeaderValue authenticationHeader = null) : base(account, url, log, authenticationHeader)
        {
        }

        public AdminApiService Admin { get; private set; }

        public DebugApiService Debug { get; private set; }

        public MinerApiService Miner { get; private set; }

        public GethEthApiService GethEth { get; private set; }

        protected override void InitialiseInnerServices()
        {
            base.InitialiseInnerServices();
            Miner = new MinerApiService(Client);
            Debug = new DebugApiService(Client);
            Admin = new AdminApiService(Client);
            GethEth = new GethEthApiService(Client);
        }
    }
}