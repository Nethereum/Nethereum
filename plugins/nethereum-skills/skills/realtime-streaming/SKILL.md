---
name: realtime-streaming
description: Stream real-time blockchain data with Nethereum. Use when the user asks about WebSocket subscriptions, new block headers, pending transactions, event log streaming, or Rx observables.
user-invocable: true
---

# Real-Time Blockchain Streaming

NuGet: `Nethereum.RPC.Reactive`, `Nethereum.JsonRpc.WebSocketStreamingClient`

## Connect

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
var client = new StreamingWebSocketClient("wss://mainnet.infura.io/ws/v3/YOUR_KEY");
await client.StartAsync();
```

## New Block Headers

```csharp
using Nethereum.RPC.Reactive.Eth.Subscriptions;

var sub = new EthNewBlockHeadersObservableSubscription(client);
sub.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(block => Console.WriteLine($"Block {block.Number}"));
await sub.SubscribeAsync();
```

## Pending Transactions

```csharp
var sub = new EthNewPendingTransactionObservableSubscription(client);
sub.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(txHash => Console.WriteLine($"Pending: {txHash}"));
await sub.SubscribeAsync();
```

## ERC20 Transfer Event Streaming

Stream decoded ERC20 Transfer events from a specific contract (e.g. DAI):

```csharp
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.RPC.Reactive.Eth.Subscriptions;

var filterTransfers = Event<TransferEventDTO>.GetEventABI()
    .CreateFilterInput("0x6B175474E89094C44Da98b954EedeAC495271d0F");

var sub = new EthLogsObservableSubscription(client);
sub.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(log =>
    {
        var decoded = Event<TransferEventDTO>.DecodeEvent(log);
        if (decoded != null)
            Console.WriteLine($"Transfer from {decoded.Event.From} value {decoded.Event.Value}");
    });
await sub.SubscribeAsync(filterTransfers);
```

## DEX Trade Monitoring (Uniswap Swaps)

```csharp
[Event("Swap")]
public class SwapEventDTO : IEventDTO
{
    [Parameter("address", "sender", 1, true)] public string Sender { get; set; }
    [Parameter("uint256", "amount0In", 2)]    public BigInteger Amount0In { get; set; }
    [Parameter("uint256", "amount1In", 3)]    public BigInteger Amount1In { get; set; }
    [Parameter("uint256", "amount0Out", 4)]   public BigInteger Amount0Out { get; set; }
    [Parameter("uint256", "amount1Out", 5)]   public BigInteger Amount1Out { get; set; }
    [Parameter("address", "to", 6, true)]     public string To { get; set; }
}

var filter = Event<SwapEventDTO>.GetEventABI().CreateFilterInput(pairAddress);
var sub = new EthLogsObservableSubscription(client);
sub.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(log =>
    {
        var swap = log.DecodeEvent<SwapEventDTO>();
        var price = UnitConversion.Convert.FromWei(swap.Event.Amount0Out)
                  / UnitConversion.Convert.FromWei(swap.Event.Amount1In);
        Console.WriteLine($"Price: {price:F4}");
    });
await sub.SubscribeAsync(filter);
```

## Pending Transaction Enrichment

Subscribe to pending tx hashes, then fetch full transaction details:

```csharp
using Nethereum.RPC.Reactive.Eth.Transactions;

var sub = new EthNewPendingTransactionObservableSubscription(client);
sub.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(txHash =>
    {
        var txByHash = new EthGetTransactionByHashObservableHandler(client);
        txByHash.GetResponseAsObservable().Subscribe(tx =>
        {
            if (tx != null)
                Console.WriteLine($"{tx.TransactionHash} from {tx.From} to {tx.To}");
        });
        txByHash.SendRequestAsync(txHash).Wait();
    });
await sub.SubscribeAsync();
```

## Event Logs (Generic)

```csharp
using Nethereum.RPC.Eth.DTOs;

var filter = new NewFilterInput
{
    Address = new[] { "0xContractAddress..." },
    Topics = new[] { /* event signature */ }
};

var sub = new EthLogsObservableSubscription(client);
sub.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(log => Console.WriteLine($"Log from {log.Address}"));
await sub.SubscribeAsync(filter);
```

## Reconnection Pattern

```csharp
client.Error += async (sender, ex) =>
{
    Console.WriteLine("Client error, restarting...");
    ((StreamingWebSocketClient)sender).StopAsync().Wait();
    await SubscribeAndRunAsync();
};
```

## Keep-Alive Pinging

For hosted providers, send periodic calls to keep the WebSocket alive:

```csharp
while (true)
{
    var handler = new EthBlockNumberObservableHandler(client);
    handler.GetResponseAsObservable()
        .Subscribe(x => Console.WriteLine($"Block: {x.Value}"));
    await handler.SendRequestAsync();
    Thread.Sleep(30000);
}
```

## Rx Operators

```csharp
using System.Reactive.Linq;

sub.GetSubscriptionDataResponsesAsObservable()
    .Where(block => block.GasUsed > 15_000_000)
    .Subscribe(b => Console.WriteLine($"High-gas: {b.Number}"));
```

## WebSocket vs Polling

- **WebSocket**: Sub-second latency, requires WSS endpoint, persistent connection
- **Polling** (HTTP): Works everywhere, configurable interval, higher latency
