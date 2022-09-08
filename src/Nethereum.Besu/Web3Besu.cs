using System.Net.Http.Headers;
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
using Microsoft.Extensions.Logging;
#endif
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;

namespace Nethereum.Besu
{
    public class Web3Besu : Web3.Web3
    {
        public Web3Besu(IClient client) : base(client)
        {
        }

        public Web3Besu(string url = @"http://localhost:8545/", ILogger log = null,
            AuthenticationHeaderValue authenticationHeader = null) : base(url, log, authenticationHeader)
        {
        }

        public Web3Besu(IAccount account, IClient client) : base(account, client)
        {
        }

        public Web3Besu(IAccount account, string url = @"http://localhost:8545/", ILogger log = null,
            AuthenticationHeaderValue authenticationHeader = null) : base(account, url, log, authenticationHeader)
        {
        }

        public IAdminApiService Admin { get; private set; }

        public IDebugApiService DebugBesu { get; private set; }

        public IMinerApiService Miner { get; private set; }

        public ICliqueApiService Clique { get; private set; }

        public IIbftApiService Ibft { get; private set; }

        public IPermissioningApiService Permissioning { get; private set; }

        public IEeaApiService Eea { get; private set; }

        public ITxPoolApiService TxPool { get; private set; }


        protected override void InitialiseInnerServices()
        {
            base.InitialiseInnerServices();
            Miner = new MinerApiService(Client);
            DebugBesu = new DebugApiService(Client);
            Admin = new AdminApiService(Client);
            Clique = new CliqueApiService(Client);
            Ibft = new IbftApiService(Client);
            Permissioning = new PermissioningApiService(Client);
            Eea = new EeaApiService(Client);
            TxPool = new TxPoolApiService(Client);
        }
    }
}