using System;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Parity.IntegrationTests
{
    public class ClientFactory
    {
        public static IClient GetClient(TestSettings settings)
        {
            var url = settings.GetRPCUrl();
            return new RpcClient(new Uri(url));
        }
    }
}