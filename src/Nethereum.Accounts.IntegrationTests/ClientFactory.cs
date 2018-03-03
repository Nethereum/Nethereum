using System;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Accounts.IntegrationTests
{
    public class ClientFactory
    {
        public static IClient GetClient()
        {
            return new RpcClient(new Uri("http://localhost:8545"));
        }
    }
}