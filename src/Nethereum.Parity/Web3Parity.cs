using Nethereum.JsonRpc.Client;
using Nethereum.Parity;
using Nethereum.RPC.Accounts;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

namespace Nethereum.Parity
{
    public class Web3Parity : Web3.Web3
    {
        public Web3Parity(IClient client) : base(client)
        {
        }

        public Web3Parity(string url = @"http://localhost:8545/") : base(url)
        {
        }

        public Web3Parity(IAccount account, IClient client):base(account, client)
        {
        }

        public Web3Parity(IAccount account, string url = @"http://localhost:8545/"):base(account, url)
        {
           
        }

        public AdminApiService Admin { get; private set; }
        public AccountsApiService Accounts { get; private set; }
        public BlockAuthoringApiService BlockAuthoring { get; private set; }
        public NetworkApiService Network { get; private set; }
        
        

        protected override void InitialiseInnerServices()
        {
            base.InitialiseInnerServices();
            Admin = new  AdminApiService(Client);
            Accounts = new AccountsApiService(Client);
            BlockAuthoring = new BlockAuthoringApiService(Client);
            Network = new NetworkApiService(Client);
        }
    }
}