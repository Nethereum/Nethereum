using System.Net.Http.Headers;
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
using Microsoft.Extensions.Logging;
#endif
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;

namespace Nethereum.Rsk
{
    public class Web3Rsk : Web3.Web3
    {
        public Web3Rsk(IClient client) : base(client)
        {
        }

        public Web3Rsk(string url = @"http://localhost:8545/", ILogger log = null, AuthenticationHeaderValue authenticationHeader = null) : base(url, log, authenticationHeader)
        {
        }

        public Web3Rsk(IAccount account, IClient client) : base(account, client)
        {
        }

        public Web3Rsk(IAccount account, string url = @"http://localhost:8545/", ILogger log = null, AuthenticationHeaderValue authenticationHeader = null) : base(account, url, log, authenticationHeader)
        {
        }


        public IRskEthApiService RskEth { get; private set; }
        protected override void InitialiseInnerServices()
        {
            base.InitialiseInnerServices();

            RskEth = new RskEthApiService(Client);
        }
    }
}