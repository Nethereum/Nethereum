using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.RPC.Reactive.Eth.Transactions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.WebSocketsStreamingTest
{
    internal class ExamplePendingTransactionsWithTransactionsUsingClientAndConcurrentQueue
    {
        private readonly string url;
        StreamingWebSocketClient client;

        public ExamplePendingTransactionsWithTransactionsUsingClientAndConcurrentQueue(string url)
        {
            this.url = url;
        }

        public void SetupNewConcurrentQueue(int numberOfClients)
        {
            ConcurrentQueueTransactions = new ConcurrentQueue<StreamingWebSocketClient>();
            for (int i = 0; i < numberOfClients; i++)
            {
                EnqueueNewClient();
            }
        }

        public ConcurrentQueue<StreamingWebSocketClient> ConcurrentQueueTransactions;

        public StreamingWebSocketClient GetNextWebSocketStreamingClient()
        {
           
            StreamingWebSocketClient returnClient;
            while (ConcurrentQueueTransactions.TryDequeue(out returnClient) == false) ;
            return returnClient;
        }

        public void EnqueueNewClient()
        {
            ConcurrentQueueTransactions.Enqueue(CreateNewClientToQueue());
        }

        public StreamingWebSocketClient CreateNewClientToQueue()
        {
            var client = new StreamingWebSocketClient("wss://mainnet.infura.io/ws/v3/206cfadcef274b49a3a15c45c285211c");
            client.Error += ErrorQueue;
            client.StartAsync().Wait();
            return client;
        }

        private void ErrorQueue(object sender, Exception ex)
        {

            Console.WriteLine("Error on client Queue");
            EnqueueNewClient();
        }
        public async Task SubscribeAndRunAsync()
        {
            if (client == null)
            {
                client = new StreamingWebSocketClient(url);
                client.Error += Client_Error;
            }

            if (ConcurrentQueueTransactions == null) SetupNewConcurrentQueue(20);
            
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
                    var currentClient = GetNextWebSocketStreamingClient();
                    var transactionByHash = new EthGetTransactionByHashObservableHandler(currentClient);
                    var txnSub = transactionByHash.GetResponseAsObservable().Subscribe(
                            
                            transaction => 
                            {
                                    ConcurrentQueueTransactions.Enqueue(currentClient);
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
                                 currentClient.StopAsync().Wait();
                                 currentClient = null;
                                 EnqueueNewClient();
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
