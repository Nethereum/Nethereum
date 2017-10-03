using System;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.IpcClient;


namespace Nethereum.Web3.Tests
{
    public class ClientFactory
    {
        public static IClient GetClient()
        {
//#if NET462
            var client = new IpcClient("geth.ipc");
            return client;
//#else      
//           return new RpcClient(new Uri("http://localhost:8545/"));
//#endif
           
        }
    }
}
