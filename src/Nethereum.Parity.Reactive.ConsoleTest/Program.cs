using System;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Parity.Reactive.ConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            
            var client = new StreamingWebSocketClient("ws://127.0.0.1:8546");
            var accountBalanceSubscription = new ParityPubSubObservableSubscription<HexBigInteger>(client);
            var ethBalanceRequest = new EthGetBalance().BuildRequest("0x", BlockParameter.CreateLatest());

            accountBalanceSubscription.GetSubscriptionDataResponsesAsObservable().Subscribe(newBalance =>
                Console.WriteLine("New Balance: " + newBalance.Value.ToString()));

            accountBalanceSubscription.SubscribeAsync(ethBalanceRequest).Wait();
            // do transfer 

            
        }
    }
}
