using System.Net.Http.Headers;
using Common.Logging;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;

namespace Nethereum.Geth
{
    public class Web3Geth : Web3.Web3, IWeb3Geth
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

        public IAdminApiService Admin { get; private set; }

        public IDebugApiService GethDebug { get; private set; }

        public IMinerApiService Miner { get; private set; }

        public IGethEthApiService GethEth { get; private set; }

        public ITxnPoolApiService TxnPool { get; private set; }

        protected override void InitialiseInnerServices()
        {
            base.InitialiseInnerServices();
            Miner = new MinerApiService(Client);
            GethDebug = new DebugApiService(Client);
            Admin = new AdminApiService(Client);
            GethEth = new GethEthApiService(Client);
            TxnPool = new TxnPoolApiService(Client);
        }
    }
}