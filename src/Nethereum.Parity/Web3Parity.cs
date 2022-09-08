using System.Net.Http.Headers;
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
using Microsoft.Extensions.Logging;
#endif
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;

namespace Nethereum.Parity
{
    public class Web3Parity : Web3.Web3, IWeb3Parity
    {
        public Web3Parity(IClient client) : base(client)
        {
        }

        public Web3Parity(string url = @"http://localhost:8545/", ILogger log = null, AuthenticationHeaderValue authenticationHeader = null) : base(url, log, authenticationHeader)
        {
        }


        public Web3Parity(IAccount account, IClient client) : base(account, client)
        {
        }

        public Web3Parity(IAccount account, string url = @"http://localhost:8545/", ILogger log = null, AuthenticationHeaderValue authenticationHeader = null) : base(account, url, log, authenticationHeader)
        {
        }

        public IAdminApiService Admin { get; private set; }
        public IAccountsApiService Accounts { get; private set; }
        public IBlockAuthoringApiService BlockAuthoring { get; private set; }
        public INetworkApiService Network { get; private set; }
        public ITraceApiService Trace { get; private set; }


        protected override void InitialiseInnerServices()
        {
            base.InitialiseInnerServices();
            Admin = new AdminApiService(Client);
            Accounts = new AccountsApiService(Client);
            BlockAuthoring = new BlockAuthoringApiService(Client);
            Network = new NetworkApiService(Client);
            Trace = new TraceApiService(Client);
        }
    }
}