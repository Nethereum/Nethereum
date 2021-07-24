using System;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;

namespace Nethereum.Besu.IntegrationTests
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