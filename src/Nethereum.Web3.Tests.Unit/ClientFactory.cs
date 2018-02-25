using System;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Web3.Tests.Unit
{
    public class ClientFactory
    {
        public static IClient GetClient()
        {
            return new RpcClient(new Uri("http://not.relevant.for.unit.tests/"));
        }
    }
}
