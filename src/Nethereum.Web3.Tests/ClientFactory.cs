using System;
using Nethereum.JsonRpc.Client;
#if NET451
using Nethereum.JsonRpc.IpcClient;
#endif

namespace Nethereum.Web3.Tests
{
    public class ClientFactory
    {
        public static IClient GetClient()
        {
#if NET451
            var client = new IpcClient("geth.ipc");
            return client;
#else      
           return new RpcClient(new Uri("http://localhost:8545/"));
#endif
           
        }
    }
}
