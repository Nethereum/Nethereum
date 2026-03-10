---
name: realtime-streaming
description: Stream real-time blockchain data with Nethereum. Use when the user asks about WebSocket subscriptions, new block headers, pending transactions, event log streaming, Rx observables, DEX monitoring, or live token transfer tracking.
user-invocable: true
---

# Real-Time Blockchain Streaming

NuGet: `Nethereum.RPC.Reactive`, `Nethereum.JsonRpc.WebSocketStreamingClient`

Two approaches: WebSocket (sub-second latency) or HTTP polling (works everywhere).

## Connect

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;

var client = new StreamingWebSocketClient("wss://mainnet.infura.io/ws/v3/YOUR_KEY");
await client.StartAsync();
```

All subscriptions share the single connection.

## New Block Headers

Fires every time a new block is mined (~12s on mainnet). Headers include number, timestamp, gas — but not full transactions.

```csharp
var subscription = new EthNewBlockHeadersObservableSubscription(client);

subscription.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(block =>
    {
        Console.WriteLine($"Block {block.Number} — {block.Timestamp}");
        Console.WriteLine($"  Gas used: {block.GasUsed}");
    });

await subscription.SubscribeAsync();
```

## Pending Transactions

Returns tx hashes only (not full objects). Very high volume on mainnet.

```csharp
var subscription = new EthNewPendingTransactionObservableSubscription(client);

subscription.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(txHash =>
    {
        Console.WriteLine($"Pending tx: {txHash}");
    });

await subscription.SubscribeAsync();
```

## ERC-20 Transfer Event Streaming

Use typed event DTOs for automatic filter creation and decoding:

```csharp
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;

var filterTransfers = Event<TransferEventDTO>.GetEventABI()
    .CreateFilterInput("0x6B175474E89094C44Da98b954EedeAC495271d0F");

var subscription = new EthLogsObservableSubscription(client);

subscription.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(log =>
    {
        var decoded = Event<TransferEventDTO>.DecodeEvent(log);
        if (decoded != null)
        {
            Console.WriteLine($"Transfer: {decoded.Event.From} → {decoded.Event.To}");
            Console.WriteLine($"  Value: {Web3.Convert.FromWei(decoded.Event.Value)}");
        }
    });

await subscription.SubscribeAsync(filterTransfers);
```

`DecodeEvent` returns `null` for non-matching logs — safe on multi-event streams.

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

var pairAddress = "0xa478c2975ab1ea89e8196811f51a7b7ade33eb11"; // DAI-ETH
var filter = Event<SwapEventDTO>.GetEventABI().CreateFilterInput(pairAddress);

var subscription = new EthLogsObservableSubscription(client);
subscription.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(log =>
    {
        var swap = log.DecodeEvent<SwapEventDTO>();
        if (swap != null)
        {
            var amount0Out = UnitConversion.Convert.FromWei(swap.Event.Amount0Out);
            var amount1In  = UnitConversion.Convert.FromWei(swap.Event.Amount1In);

            if (swap.Event.Amount0In == 0 && swap.Event.Amount1Out == 0 && amount1In > 0)
            {
                var price = amount0Out / amount1In;
                Console.WriteLine($"Sell ETH — Price: {price:F4} DAI/ETH");
            }
        }
    });

await subscription.SubscribeAsync(filter);
```

## Pending Transaction Enrichment

Fetch full transaction details from pending hashes:

```csharp
using Nethereum.RPC.Reactive.Eth.Transactions;

var pendingSub = new EthNewPendingTransactionObservableSubscription(client);

pendingSub.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(txHash =>
    {
        var txByHash = new EthGetTransactionByHashObservableHandler(client);
        txByHash.GetResponseAsObservable().Subscribe(tx =>
        {
            if (tx != null)
                Console.WriteLine($"Pending: {tx.From} → {tx.To} ({Web3.Convert.FromWei(tx.Value)} ETH)");
        });
        txByHash.SendRequestAsync(txHash).Wait();
    });

await pendingSub.SubscribeAsync();
```

## Event Logs (Generic)

```csharp
using Nethereum.RPC.Eth.DTOs;

var filter = new NewFilterInput
{
    Address = new[] { "0xContractAddress..." },
    Topics = new[] { /* event signature hash */ }
};

var subscription = new EthLogsObservableSubscription(client);

subscription.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(log =>
    {
        Console.WriteLine($"Log from {log.Address} in block {log.BlockNumber}");
    });

await subscription.SubscribeAsync(filter);
```

## Connection Management

### Error Handling and Reconnection

```csharp
client.Error += async (sender, ex) =>
{
    Console.WriteLine($"WebSocket error: {ex.Message}");
    await ((StreamingWebSocketClient)sender).StopAsync();
    await ReconnectAndSubscribeAsync();
};
```

### Keep-Alive Pinging

Most providers close idle connections after 1-2 minutes:

```csharp
_ = Task.Run(async () =>
{
    while (true)
    {
        var handler = new EthBlockNumberObservableHandler(client);
        handler.GetResponseAsObservable()
            .Subscribe(x => Console.WriteLine($"Keepalive — block {x.Value}"));
        await handler.SendRequestAsync();
        await Task.Delay(30000);
    }
});
```

## Rx Operators

```csharp
using System.Reactive.Linq;

subscription.GetSubscriptionDataResponsesAsObservable()
    .Where(block => block.GasUsed > 15_000_000)
    .Select(block => new { block.Number, block.GasUsed })
    .Subscribe(b => Console.WriteLine($"High-gas block: {b.Number}"));
```

## Polling (HTTP Fallback)

```csharp
using Nethereum.Web3;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");

var blockStream = web3.Eth.Blocks
    .GetBlockWithTransactionsByNumber
    .CreateObservable(intervalMs: 2000);

blockStream.Subscribe(block =>
{
    Console.WriteLine($"Block {block.Number} with {block.Transactions.Length} txs");
});
```

## WebSocket vs Polling

- **WebSocket**: Sub-second latency, requires WSS endpoint, persistent connection, no missed events
- **Polling** (HTTP): Works everywhere, configurable interval, higher latency, possible gaps between polls
