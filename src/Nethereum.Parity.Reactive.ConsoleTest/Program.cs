using System;
using System.Reactive.Linq;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.IpcClient;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Polling;
using Nethereum.Web3.Accounts;
using Nethereum.Web3.Accounts.Managed;

namespace Nethereum.Parity.Reactive.ConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {

            var account = new ManagedAccount("0x12890d2cce102216644c59daE5baed380d84830c", "password");
            var clientws = new WebSocketClient("ws://127.0.0.1:8546");
            var web3ws = new Web3.Web3(account, clientws);
            var res =  web3ws.Eth.Blocks.GetBlockNumber.SendRequestAsync().Result; //task cancelled exception



            var clientipc = new IpcClient("jsonrpc.ipc");
            var web3ipc = new Web3.Web3(account, clientipc);
            var resIpc = web3ws.Eth.Blocks.GetBlockNumber.SendRequestAsync().Result; 


            var client = new StreamingWebSocketClient("ws://127.0.0.1:8546");
            client.Error += Client_Error;

            var accountBalanceSubscription = new ParityPubSubObservableSubscription<HexBigInteger>(client);
            var ethBalanceRequest = new EthGetBalance().BuildRequest("0x12890d2cce102216644c59daE5baed380d84830c", BlockParameter.CreateLatest());

            accountBalanceSubscription.GetSubscriptionDataResponsesAsObservable().Subscribe(newBalance =>
                Console.WriteLine("New Balance: " + newBalance.Value.ToString()), onError => Console.WriteLine("Error:" + onError.Message));

            accountBalanceSubscription.GetSubscribeResponseAsObservable()
                .Subscribe(x => Console.WriteLine("SubscriptionId:" + x));


            client.StartAsync().Wait();

            accountBalanceSubscription.SubscribeAsync(ethBalanceRequest).Wait();
            // do transfer 

            var web3 = new Web3.Web3(new Account("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7"));

            for (int i = 0; i < 10; i++)
            {
                web3.Eth.GetEtherTransferService()
                    .TransferEtherAndWaitForReceiptAsync("0x12890d2cce102216644c59daE5baed380d848306", 10).Wait();

            }

            client.StopAsync().Wait();

            var accountBalanceSubscription2 = new ParityPubSubObservableSubscription<HexBigInteger>(client);
            var ethBalanceRequest2 = new EthGetBalance().BuildRequest("0x12890d2cce102216644c59daE5baed380d84830c", BlockParameter.CreateLatest());

            accountBalanceSubscription2.GetSubscriptionDataResponsesAsObservable().Subscribe(newBalance =>
                Console.WriteLine("New Balance: " + newBalance.Value.ToString()));

            accountBalanceSubscription2.GetSubscribeResponseAsObservable()
                .Subscribe(x => Console.WriteLine("SubscriptionId:" + x));

            client.StartAsync().Wait();

            accountBalanceSubscription2.SubscribeAsync(ethBalanceRequest2).Wait();

            for (int i = 0; i < 10; i++)
            {
                web3.Eth.GetEtherTransferService()
                    .TransferEtherAndWaitForReceiptAsync("0x12890d2cce102216644c59daE5baed380d848306", 10).Wait();

            }
        }

        private static void Client_Error(object sender, Exception ex)
        {
           Console.WriteLine("Error :" + ex.Message);
        }
    }
}
