using System;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3.Accounts;

namespace Nethereum.Parity.Reactive.ConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            
            var client = new StreamingWebSocketClient("ws://127.0.0.1:8546");
            var accountBalanceSubscription = new ParityPubSubObservableSubscription<HexBigInteger>(client);
            var ethBalanceRequest = new EthGetBalance().BuildRequest("0x12890d2cce102216644c59daE5baed380d84830c", BlockParameter.CreateLatest());

            accountBalanceSubscription.GetSubscriptionDataResponsesAsObservable().Subscribe(newBalance =>
                Console.WriteLine("New Balance: " + newBalance.Value.ToString()));

            client.Start().Wait();

            accountBalanceSubscription.SubscribeAsync(ethBalanceRequest).Wait();
            // do transfer 

            var web3 = new Web3.Web3(new Account("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7"));

            for (int i = 0; i < 10; i++)
            {
                web3.Eth.GetEtherTransferService()
                    .TransferEtherAndWaitForReceiptAsync("0x12890d2cce102216644c59daE5baed380d848306", 10).Wait();

            }

        }
    }
}
