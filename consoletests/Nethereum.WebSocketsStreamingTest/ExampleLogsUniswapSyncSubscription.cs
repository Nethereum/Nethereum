using Nethereum.Contracts;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.WebSocketsStreamingTest
{
    internal class ExampleLogsUniswapSyncSubscription
    {
        private readonly string url;
        private readonly string rpcURl;
        StreamingWebSocketClient client;

        public ExampleLogsUniswapSyncSubscription(string url, string rpcURl)
        {
            this.url = url;
            this.rpcURl = rpcURl;
        }
        public async Task SubscribeAndRunAsync()
        {
            if (client == null)
            {
                client = new StreamingWebSocketClient(url);
                client.Error += Client_Error;
            }

            string uniSwapFactoryAddress = "0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6f";
            var web3 = new Web3.Web3(rpcURl);


            string daiAddress = "0x6b175474e89094c44da98b954eedeac495271d0f";
            string wethAddress = "0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2";

            var pairContractAddress = await web3.Eth.GetContractQueryHandler<GetPairFunction>()
                .QueryAsync<string>(uniSwapFactoryAddress,
                    new GetPairFunction() { TokenA = daiAddress, TokenB = wethAddress });

            var filter = Event<PairSyncEventDTO>.GetEventABI()
                .CreateFilterInput(new[] { pairContractAddress });

            var subscription = new EthLogsObservableSubscription(client);
            subscription.GetSubscriptionDataResponsesAsObservable().
                         Subscribe(log =>
                         {
                             try
                             {
                                 EventLog<PairSyncEventDTO> decoded = Event<PairSyncEventDTO>.DecodeEvent(log);
                                 if (decoded != null)
                                 {
                                     decimal reserve0 = Web3.Web3.Convert.FromWei(decoded.Event.Reserve0);
                                     decimal reserve1 = Web3.Web3.Convert.FromWei(decoded.Event.Reserve1);
                                     Console.WriteLine($@"Price={reserve0 / reserve1}");
                                 }
                                 else Console.WriteLine(@"Found not standard transfer log");
                             }
                             catch (Exception ex)
                             {
                                 Console.WriteLine(@"Log Address: " + log.Address + @" is not a standard transfer log:", ex.Message);
                             }
                         });

            await client.StartAsync();
            subscription.GetSubscribeResponseAsObservable().Subscribe(id => Console.WriteLine($"Subscribed with id: {id}"));
            await subscription.SubscribeAsync(filter);

            while (true) //pinging to keep alive infura
            {
                var handler = new EthBlockNumberObservableHandler(client);
                handler.GetResponseAsObservable().Subscribe(x => Console.WriteLine(x.Value));
                await handler.SendRequestAsync();
                Thread.Sleep(30000);
            }


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
