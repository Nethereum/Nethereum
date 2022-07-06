using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using System;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Nethereum.WebSocketsStreamingTest
{
    internal class ExampleLogsERC20Subscriptions
    {
        private readonly string url;
        StreamingWebSocketClient client;

        public ExampleLogsERC20Subscriptions(string url)
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

            //Subscribing to blockHeaders to keep it alive 
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

            //DAI contract address

            var filterTransfers = Event<TransferEventDTO>.GetEventABI().CreateFilterInput("0x6B175474E89094C44Da98b954EedeAC495271d0F");

            var ethLogsTokenTransfer = new EthLogsObservableSubscription(client);
            ethLogsTokenTransfer.GetSubscriptionDataResponsesAsObservable().Subscribe(log =>
            {
                try
                {
                    var decoded = Event<TransferEventDTO>.DecodeEvent(log);
                    if (decoded != null)
                    {
                        Console.WriteLine("Contract address: " + log.Address + " Log Transfer from:" + decoded.Event.From);
                    }
                    else
                    {
                        Console.WriteLine("Found not standard transfer log");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Log Address: " + log.Address + " is not a standard transfer log:", ex.Message);
                }
            }, exception =>
            {
                Console.WriteLine("Logs error info:" + exception.Message);
            });


            await client.StartAsync();

            await blockHeaderSubscription.SubscribeAsync();
            //don't pass any parameter if you want all
            await ethLogsTokenTransfer.SubscribeAsync(filterTransfers);

            Console.ReadLine();
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
