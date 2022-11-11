using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.RPC.Reactive.Eth.Transactions;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.WebSocketsStreamingTest
{

    internal class ExamplePendingTransactionsWithTransactionsUsingSameClient
    {
        private readonly string url;
        StreamingWebSocketClient client;

        public ExamplePendingTransactionsWithTransactionsUsingSameClient(string url)
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
            
            var pendingTransactionsSubscription = new EthNewPendingTransactionObservableSubscription(client);

            pendingTransactionsSubscription.GetSubscribeResponseAsObservable().SelectMany(async subscriptionId =>
            {
                Console.WriteLine("Pending transactions subscription Id: " + subscriptionId);
                return Task.CompletedTask;
            }).Subscribe();



            pendingTransactionsSubscription.GetSubscriptionDataResponsesAsObservable().Subscribe(
                transactionHash =>
                {
                    //Console.WriteLine("New Pending TransactionHash: " + transactionHash);
                    var transactionByHash = new EthGetTransactionByHashObservableHandler(client);
                    var txnSub = transactionByHash.GetResponseAsObservable().Subscribe(
                            transaction => 
                            {
                                    if (transaction != null)
                                    {
                                        Console.WriteLine("TransactionHash: " + transaction.TransactionHash + " Transaction From: " + transaction.From + " to :" + transaction.To);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Null transaction response, you could try again ... ");
                                    }
                            }, exception =>
                            {
                                Console.WriteLine("Error transaction by hash:" + exception.Message);
                            }
                         );
                
                    transactionByHash.SendRequestAsync(transactionHash).Wait();
                
                }
                , exception =>
                {

                    Console.WriteLine("Pending transactions error info:" + exception.Message);
                });

               pendingTransactionsSubscription.GetUnsubscribeResponseAsObservable().Subscribe(response =>
                            Console.WriteLine("Pending transactions unsubscribe result: " + response));



            await client.StartAsync();

            await pendingTransactionsSubscription.SubscribeAsync();

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
