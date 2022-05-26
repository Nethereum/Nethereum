using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Eth;
using System;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Nethereum.WebSocketsStreamingTest
{
    internal class ExampleNormalRPCCalls
    {
        private readonly string url;
        StreamingWebSocketClient client;

        public ExampleNormalRPCCalls(string url)
        {
            this.url = url;
        }
        public async Task SubscribeAndRunAsync()
        {
            if (client == null)
            {
                client = new StreamingWebSocketClient(url);
                
            }

            var ethGetBalance = new EthGetBalanceObservableHandler(client);
            var subs = ethGetBalance.GetResponseAsObservable().Subscribe(balance =>
                            Console.WriteLine("Balance: " + balance.Value.ToString()));

            var ethBlockNumber = new EthBlockNumberObservableHandler(client);
            ethBlockNumber.GetResponseAsObservable().Subscribe(blockNumber =>
                                Console.WriteLine("Block number: " + blockNumber.Value.ToString()));



            await client.StartAsync();

            await ethGetBalance.SendRequestAsync("0x742d35cc6634c0532925a3b844bc454e4438f44e", BlockParameter.CreateLatest());

            await ethBlockNumber.SendRequestAsync();

            Console.ReadLine();
        }

    }
}
