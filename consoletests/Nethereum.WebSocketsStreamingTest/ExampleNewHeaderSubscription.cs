using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.WebSocketsStreamingTest
{
    internal class ExampleNewHeaderSubscription
    {
        private readonly string url;
        StreamingWebSocketClient client;

        public ExampleNewHeaderSubscription(string url)
        {
            this.url = url;
        }
        public async Task SubscribeAndRunAsync()
        {
            if (client == null)
            {
                client = new StreamingWebSocketClient(url);
                client.Error += Client_Error;
            }

            var blockHeaderSubscription = new EthNewBlockHeadersObservableSubscription(client);

            blockHeaderSubscription.GetSubscribeResponseAsObservable().Subscribe(subscriptionId =>
                Console.WriteLine("Block Header subscription Id: " + subscriptionId));

            blockHeaderSubscription.GetSubscriptionDataResponsesAsObservable().Subscribe(block =>
                Console.WriteLine("New Block: " + block.BlockHash)
                , exception =>
                {
                    Console.WriteLine("BlockHeaderSubscription error info:" + exception.Message);
                });

            blockHeaderSubscription.GetUnsubscribeResponseAsObservable().Subscribe(response =>
                            Console.WriteLine("Block Header unsubscribe result: " + response));


            await client.StartAsync();

            await blockHeaderSubscription.SubscribeAsync();


            Thread.Sleep(20000);

            await blockHeaderSubscription.UnsubscribeAsync();

            Console.WriteLine("Finished after 20000 miliseconds");
        }

        private async void Client_Error(object sender, Exception ex)
        {
            Console.WriteLine("Client Error restarting...");
            // ((StreamingWebSocketClient)sender).Error -= Client_Error;
            ((StreamingWebSocketClient)sender).StopAsync().Wait();
            //Restart everything
            await SubscribeAndRunAsync();
        }
    }
}
