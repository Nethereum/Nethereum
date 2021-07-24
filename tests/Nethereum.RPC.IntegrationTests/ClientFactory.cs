using Nethereum.JsonRpc.Client;
//using Nethereum.JsonRpc.IpcClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Tests.Testers;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.JsonRpc.WebSocketStreamingClient;

namespace Nethereum.RPC.Tests
{
    public class ClientFactory
    {
        public static IClient GetClient(TestSettings settings)
        {
           var url = settings.GetRPCUrl();
           return new RpcClient(new Uri(url)); 
        }

        //TODO:Subscriptions
        public static IStreamingClient GetStreamingClient(TestSettings settings)
        {
            var url = settings.GetLiveWSRpcUrl();
            return new StreamingWebSocketClient(url);
        }
    }
}
