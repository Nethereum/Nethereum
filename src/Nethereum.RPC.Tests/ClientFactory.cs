using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.IpcClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.RPC.Tests.Testers;

namespace Nethereum.RPC.Tests
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
