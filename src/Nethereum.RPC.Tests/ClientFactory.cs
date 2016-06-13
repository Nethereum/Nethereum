using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.IpcClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.RPC.Tests
{
    public class ClientFactory
    {
        public static IClient GetClient()
        {
            //var client = new IpcClient("./geth.ipc");
            //return new RpcClient(new Uri("https://eth2.augur.net")); 
            return new RpcClient(new Uri("http://localhost:8545/"));
            
        }
    }
}
