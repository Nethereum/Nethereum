using Nethereum.Contracts;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Reactive.Linq;
using System.Threading;
using Nethereum.Contracts.Extensions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Eth.Subscriptions;
using Nethereum.RPC.Reactive.Eth;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.RPC.Reactive.Extensions;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.RPC.Web3;
using Nethereum.Hex.HexTypes;
using System.Threading.Tasks;
using Nethereum.RPC.Reactive.Eth.Transactions;
using System.Collections.Concurrent;

namespace Nethereum.WebSocketsStreamingTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var url = "wss://mainnet.infura.io/ws/v3/206cfadcef274b49a3a15c45c285211c";
            //var example = new ExamplePendingTransactionsWithTransactionsUsingSameClient(url);
            ///var example = new ExamplePendingTransactionsWithTransactionsUsingClientAndConcurrentQueue(url);
            //var example = new ExampleNewHeaderSubscription(url);
            var example = new ExampleNormalRPCCalls(url);
            //var example = new ExampleLogsSubscriptions(url);
            
            await example.SubscribeAndRunAsync();

            Console.ReadLine();
        }

      


    }
}
