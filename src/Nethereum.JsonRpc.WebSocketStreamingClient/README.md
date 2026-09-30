# Nethereum.JsonRpc.WebSocketStreamingClient

Reactive Extensions (Rx.NET) wrapper for Ethereum WebSocket streaming providing IObservable-based subscriptions and request/response handlers.

## Overview

Nethereum.JsonRpc.WebSocketStreamingClient provides IObservable wrappers around WebSocket streaming functionality, enabling reactive programming patterns for Ethereum real-time data streams. This package combines Nethereum's `StreamingWebSocketClient` (from `Nethereum.JsonRpc.WebSocketClient`) with System.Reactive (Rx.NET) to provide a powerful, composable API for handling asynchronous event streams from Ethereum nodes.

**What is Rx.NET?**

Reactive Extensions (Rx.NET) is a library for composing asynchronous and event-based programs using observable sequences. It treats asynchronous data streams as first-class citizens, allowing you to query, filter, transform, and combine them using LINQ-style operators.

**Why Use Observables for Ethereum Streaming?**

Traditional event-based patterns can become complex when you need to:
- Filter events based on criteria
- Combine multiple event streams
- Throttle or debounce rapid events
- Apply backpressure to handle event overflow
- Implement timeout and retry logic
- Transform and aggregate events over time windows

Rx.NET solves these problems with a composable, declarative API.

**Key Features:**
- IObservable wrappers for eth_subscribe subscriptions (newHeads, logs, newPendingTransactions)
- IObservable wrappers for request/response observable handlers (eth_blockNumber, eth_getBalance): send one request, emit the response, complete
- Automatic subscription lifecycle management (subscribe, unsubscribe)
- Error handling through Rx error channels
- Full System.Reactive operator support (Where, Select, Buffer, Throttle, etc.)
- Type-safe subscription responses (Block, FilterLog, string)

## Installation

```bash
dotnet add package Nethereum.JsonRpc.WebSocketStreamingClient
dotnet add package Nethereum.JsonRpc.WebSocketClient
```

Or via Package Manager Console:

```powershell
Install-Package Nethereum.JsonRpc.WebSocketStreamingClient
Install-Package Nethereum.JsonRpc.WebSocketClient
```

## Dependencies

**Package References:**
- System.Reactive 4.1.2

**Project References:**
- Nethereum.Hex
- Nethereum.JsonRpc.Client
- Nethereum.RPC

**Target Framework:**
- netstandard2.0

## Architecture

```
┌──────────────────────────────────────────────────────┐
│  Your Application                                    │
│  (LINQ-style Rx operators)                           │
└──────────────────────────────────────────────────────┘
                        │
                        │ subscribes to
                        ▼
┌──────────────────────────────────────────────────────┐
│  Observable Subscription Handlers                    │
│  - EthLogsObservableSubscription                     │
│  - EthNewBlockHeadersObservableSubscription          │
│  - EthNewPendingTransactionObservableSubscription    │
└──────────────────────────────────────────────────────┘
                        │
                        │ wraps
                        ▼
┌──────────────────────────────────────────────────────┐
│  RpcStreamingSubscriptionObservableHandler<T>        │
│  - Subject<string> SubscribeResponseSubject          │
│  - Subject<T> SubscriptionDataResponseSubject        │
│  - Subject<bool> UnsubscribeResponseSubject          │
└──────────────────────────────────────────────────────┘
                        │
                        │ uses
                        ▼
┌──────────────────────────────────────────────────────┐
│  IStreamingClient (WebSocket)                        │
│  - SendRequestAsync / Subscribe / Unsubscribe        │
└──────────────────────────────────────────────────────┘
                        │
                        ▼
                Ethereum Node WebSocket Endpoint
                (ws://localhost:8546)
```

## Key Concepts

### Observable Subscriptions vs Request/Response Handlers

**Subscription-Based (eth_subscribe):**

True server push - node sends events as they occur:
- **EthLogsObservableSubscription** - Contract event logs matching filter criteria
- **EthNewBlockHeadersObservableSubscription** - New block headers
- **EthNewPendingTransactionObservableSubscription** - New pending transactions

**Request/Response Observable Handlers:**

Send one request, emit the response, complete:
- **EthBlockNumberObservableHandler** - Latest block number
- **EthGetBalanceObservableHandler** - Account balance

### Observable Subjects

Each subscription handler exposes three observable subjects:

```csharp
IObservable<string> GetSubscribeResponseAsObservable()
// Emits subscription ID when eth_subscribe succeeds
// Completes immediately after emitting one value

IObservable<TResponse> GetSubscriptionDataResponsesAsObservable()
// Emits stream of subscription data (blocks, logs, etc.)
// Continues emitting until unsubscribe

IObservable<bool> GetUnsubscribeResponseAsObservable()
// Emits true when eth_unsubscribe succeeds
// Completes after emitting, also completes data stream
```

### Error Handling

Rx.NET propagates errors through the observable pipeline:

```csharp
subscription.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(
        onNext: block => Console.WriteLine($"Block: {block.Number}"),
        onError: ex => Console.WriteLine($"Error: {ex.Message}"),
        onCompleted: () => Console.WriteLine("Subscription completed")
    );
```

Errors automatically trigger `OnError` and complete the observable sequence.

## Quick Start

### 1. Create WebSocket Client

```csharp
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.JsonRpc.WebSocketStreamingClient;

var wsClient = new StreamingWebSocketClient("ws://localhost:8546");
await wsClient.StartAsync();
```

### 2. Subscribe to New Block Headers

```csharp
var blockHeaderSubscription = new EthNewBlockHeadersObservableSubscription(wsClient);

// Subscribe to the observable stream
var subscription = blockHeaderSubscription
    .GetSubscriptionDataResponsesAsObservable()
    .Subscribe(block =>
    {
        Console.WriteLine($"New Block #{block.Number.Value}");
        Console.WriteLine($"Hash: {block.BlockHash}");
        Console.WriteLine($"Timestamp: {block.Timestamp.Value}");
    });

// Start the subscription
await blockHeaderSubscription.SubscribeAsync();

// Let it run...
await Task.Delay(60000);

// Clean up
await blockHeaderSubscription.UnsubscribeAsync();
subscription.Dispose();
```

### 3. Filter and Transform Events

```csharp
var logsSubscription = new EthLogsObservableSubscription(wsClient);

// Subscribe with Rx operators
var subscription = logsSubscription
    .GetSubscriptionDataResponsesAsObservable()
    .Where(log => log.Topics.Length > 0)  // Filter: only logs with topics
    .Select(log => new
    {
        ContractAddress = log.Address,
        EventSignature = log.Topics[0],
        BlockNumber = log.BlockNumber.Value
    })
    .Subscribe(evt =>
    {
        Console.WriteLine($"Event from {evt.ContractAddress}");
        Console.WriteLine($"Signature: {evt.EventSignature}");
        Console.WriteLine($"Block: {evt.BlockNumber}");
    });

// Subscribe to specific contract events
var filter = new NewFilterInput
{
    Address = new[] { "0xYourContractAddress" },
    Topics = new[] { "0xYourEventSignature" }
};

await logsSubscription.SubscribeAsync(filter);
```

## Usage Examples

### Example 1: Monitor New Blocks with Throttling

```csharp
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using System.Reactive.Linq;

var wsClient = new StreamingWebSocketClient("ws://localhost:8546");
await wsClient.StartAsync();

var blockSubscription = new EthNewBlockHeadersObservableSubscription(wsClient);

// Throttle to max 1 block per 2 seconds (prevents flooding)
var subscription = blockSubscription
    .GetSubscriptionDataResponsesAsObservable()
    .Throttle(TimeSpan.FromSeconds(2))
    .Subscribe(block =>
    {
        Console.WriteLine($"Block #{block.Number.Value} at {DateTime.Now}");
        // newHeads only delivers headers - Block has no Transactions/TransactionCount()
        // (that extension only exists on BlockWithTransactions / BlockWithTransactionHashes)
        Console.WriteLine($"Gas Used: {block.GasUsed.Value}");
    });

await blockSubscription.SubscribeAsync();

// Run for 5 minutes
await Task.Delay(TimeSpan.FromMinutes(5));

await blockSubscription.UnsubscribeAsync();
subscription.Dispose();
await wsClient.StopAsync();
```

### Example 2: Contract Event Logs with Buffering

```csharp
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.DTOs;
using System.Reactive.Linq;

var wsClient = new StreamingWebSocketClient("ws://localhost:8546");
await wsClient.StartAsync();

var logsSubscription = new EthLogsObservableSubscription(wsClient);

// Buffer logs into batches of 10, then process as group
var subscription = logsSubscription
    .GetSubscriptionDataResponsesAsObservable()
    .Buffer(10)
    .Subscribe(logBatch =>
    {
        Console.WriteLine($"Processing batch of {logBatch.Count} logs:");
        foreach (var log in logBatch)
        {
            Console.WriteLine($"  - {log.Address} in block {log.BlockNumber.Value}");
        }
    });

// Subscribe to ERC-20 Transfer events
var transferSignature = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";
var filter = new NewFilterInput
{
    Topics = new[] { transferSignature }
};

await logsSubscription.SubscribeAsync(filter);

// Run for 10 minutes
await Task.Delay(TimeSpan.FromMinutes(10));

await logsSubscription.UnsubscribeAsync();
subscription.Dispose();
await wsClient.StopAsync();
```

### Example 3: Real-World ERC-20 Transfer Monitoring

Based on Nethereum console test example for monitoring DAI transfers:

```csharp
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.Web3;
using System.Reactive.Linq;

var wsClient = new StreamingWebSocketClient("wss://mainnet.infura.io/ws/v3/YOUR-PROJECT-ID");
await wsClient.StartAsync();

// DAI contract address
var daiAddress = "0x6B175474E89094C44Da98b954EedeAC495271d0F";

// Create filter for Transfer events from DAI contract
var filterTransfers = Event<TransferEventDTO>.GetEventABI().CreateFilterInput(daiAddress);

var ethLogsTokenTransfer = new EthLogsObservableSubscription(wsClient);

ethLogsTokenTransfer.GetSubscriptionDataResponsesAsObservable().Subscribe(log =>
{
    try
    {
        // Decode the Transfer event
        var decoded = Event<TransferEventDTO>.DecodeEvent(log);
        if (decoded != null)
        {
            Console.WriteLine($"DAI Transfer:");
            Console.WriteLine($"  From: {decoded.Event.From}");
            Console.WriteLine($"  To: {decoded.Event.To}");
            Console.WriteLine($"  Value: {Web3.Convert.FromWei(decoded.Event.Value)} DAI");
            Console.WriteLine($"  Block: {log.BlockNumber.Value}");
            Console.WriteLine($"  Tx: {log.TransactionHash}");
        }
        else
        {
            Console.WriteLine("Found non-standard transfer log");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error decoding log: {ex.Message}");
    }
},
onError: exception =>
{
    Console.WriteLine($"Logs subscription error: {exception.Message}");
});

await ethLogsTokenTransfer.SubscribeAsync(filterTransfers);

// Monitor indefinitely
Console.WriteLine("Monitoring DAI transfers. Press Ctrl+C to exit.");
await Task.Delay(Timeout.Infinite);
```

### Example 4: Combining Multiple Streams

```csharp
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using System.Reactive.Linq;

var wsClient = new StreamingWebSocketClient("ws://localhost:8546");
await wsClient.StartAsync();

var blockSubscription = new EthNewBlockHeadersObservableSubscription(wsClient);
var txSubscription = new EthNewPendingTransactionObservableSubscription(wsClient);

// Combine streams: emit tuple of (block count, tx count) every 10 seconds
var blockStream = blockSubscription
    .GetSubscriptionDataResponsesAsObservable()
    .Select(_ => 1);

var txStream = txSubscription
    .GetSubscriptionDataResponsesAsObservable()
    .Select(_ => 1);

var combined = Observable.CombineLatest(
    blockStream.Buffer(TimeSpan.FromSeconds(10)).Select(buf => buf.Count),
    txStream.Buffer(TimeSpan.FromSeconds(10)).Select(buf => buf.Count),
    (blockCount, txCount) => new { BlockCount = blockCount, TxCount = txCount }
);

var subscription = combined.Subscribe(stats =>
{
    Console.WriteLine($"Last 10s: {stats.BlockCount} blocks, {stats.TxCount} pending txs");
});

await blockSubscription.SubscribeAsync();
await txSubscription.SubscribeAsync();

// Monitor for 5 minutes
await Task.Delay(TimeSpan.FromMinutes(5));

await blockSubscription.UnsubscribeAsync();
await txSubscription.UnsubscribeAsync();
subscription.Dispose();
await wsClient.StopAsync();
```

### Example 5: Timeout and Retry Logic

```csharp
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using System.Reactive.Linq;

var wsClient = new StreamingWebSocketClient("ws://localhost:8546");
await wsClient.StartAsync();

var blockSubscription = new EthNewBlockHeadersObservableSubscription(wsClient);

// Timeout if no block received within 30 seconds
var subscription = blockSubscription
    .GetSubscriptionDataResponsesAsObservable()
    .Timeout(TimeSpan.FromSeconds(30))
    .Retry(3)  // Retry up to 3 times on timeout
    .Subscribe(
        onNext: block => Console.WriteLine($"Block #{block.Number.Value}"),
        onError: ex => Console.WriteLine($"Failed after retries: {ex.Message}"),
        onCompleted: () => Console.WriteLine("Stream completed")
    );

await blockSubscription.SubscribeAsync();
```

### Example 6: Windowing and Aggregation

```csharp
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using System.Reactive.Linq;

var wsClient = new StreamingWebSocketClient("ws://localhost:8546");
await wsClient.StartAsync();

var blockSubscription = new EthNewBlockHeadersObservableSubscription(wsClient);

// Calculate average gas used per block over 1-minute windows
var subscription = blockSubscription
    .GetSubscriptionDataResponsesAsObservable()
    .Window(TimeSpan.FromMinutes(1))
    .SelectMany(window => window
        .Select(block => (double)block.GasUsed.Value)
        .Average()
    )
    .Subscribe(avgGas =>
    {
        Console.WriteLine($"Average gas used (last minute): {avgGas:N0}");
    });

await blockSubscription.SubscribeAsync();

// Monitor for 30 minutes
await Task.Delay(TimeSpan.FromMinutes(30));

await blockSubscription.UnsubscribeAsync();
subscription.Dispose();
await wsClient.StopAsync();
```

### Example 7: Filtering Logs by Multiple Criteria

```csharp
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.DTOs;
using System.Reactive.Linq;
using Nethereum.Hex.HexTypes;

var wsClient = new StreamingWebSocketClient("ws://localhost:8546");
await wsClient.StartAsync();

var logsSubscription = new EthLogsObservableSubscription(wsClient);

// Filter for high-value ERC-20 transfers (value in log.Data; ERC-721 Transfer has a fourth topic, the tokenId)
var subscription = logsSubscription
    .GetSubscriptionDataResponsesAsObservable()
    .Where(log => log.Topics != null && log.Topics.Length == 3)
    .Where(log =>
    {
        var value = new HexBigInteger(log.Data).Value;
        return value > 1000000000000000000; // > 1 token (18 decimals)
    })
    .Subscribe(log =>
    {
        Console.WriteLine($"High-value transfer detected:");
        Console.WriteLine($"  Contract: {log.Address}");
        Console.WriteLine($"  Block: {log.BlockNumber.Value}");
        Console.WriteLine($"  Tx: {log.TransactionHash}");
    });

// Subscribe to ERC-20 Transfer events
var transferSignature = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";
var filter = new NewFilterInput
{
    Topics = new[] { transferSignature }
};

await logsSubscription.SubscribeAsync(filter);
```

### Example 8: Subscribe to Subscription ID

```csharp
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.JsonRpc.WebSocketStreamingClient;

var wsClient = new StreamingWebSocketClient("ws://localhost:8546");
await wsClient.StartAsync();

var blockSubscription = new EthNewBlockHeadersObservableSubscription(wsClient);

// Track subscription lifecycle
var subscribeSubscription = blockSubscription
    .GetSubscribeResponseAsObservable()
    .Subscribe(subscriptionId =>
    {
        Console.WriteLine($"Subscription created with ID: {subscriptionId}");
    });

var unsubscribeSubscription = blockSubscription
    .GetUnsubscribeResponseAsObservable()
    .Subscribe(success =>
    {
        Console.WriteLine($"Unsubscribed successfully: {success}");
    });

var dataSubscription = blockSubscription
    .GetSubscriptionDataResponsesAsObservable()
    .Subscribe(block =>
    {
        Console.WriteLine($"Block #{block.Number.Value}");
    });

await blockSubscription.SubscribeAsync();
await Task.Delay(30000);
await blockSubscription.UnsubscribeAsync();

// Wait for all observables to complete
await Task.Delay(1000);

subscribeSubscription.Dispose();
unsubscribeSubscription.Dispose();
dataSubscription.Dispose();
await wsClient.StopAsync();
```

### Example 9: Error Handling with OnError

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using System.Reactive.Linq;

var wsClient = new StreamingWebSocketClient("ws://localhost:8546");
await wsClient.StartAsync();

var blockSubscription = new EthNewBlockHeadersObservableSubscription(wsClient);

var subscription = blockSubscription
    .GetSubscriptionDataResponsesAsObservable()
    .Subscribe(
        onNext: block =>
        {
            Console.WriteLine($"Block #{block.Number.Value}");
        },
        onError: ex =>
        {
            if (ex is RpcResponseException rpcEx)
            {
                Console.WriteLine($"RPC Error {rpcEx.RpcError.Code}: {rpcEx.RpcError.Message}");
            }
            else if (ex is TimeoutException)
            {
                Console.WriteLine("Subscription timed out");
            }
            else
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }
        },
        onCompleted: () =>
        {
            Console.WriteLine("Subscription completed successfully");
        }
    );

await blockSubscription.SubscribeAsync();

// If an error occurs, OnError will be called and stream completes
await Task.Delay(60000);

await blockSubscription.UnsubscribeAsync();
subscription.Dispose();
await wsClient.StopAsync();
```

## API Reference

### EthLogsObservableSubscription

Observable subscription for contract event logs.

```csharp
public class EthLogsObservableSubscription : RpcStreamingSubscriptionObservableHandler<FilterLog>
{
    // Constructor
    public EthLogsObservableSubscription(IStreamingClient client);

    // Subscribe Methods
    public Task SubscribeAsync(object id = null);
    public Task SubscribeAsync(NewFilterInput filterInput, object id = null);
    public RpcRequest BuildRequest(NewFilterInput filterInput, object id = null);

    // Observable Streams (inherited)
    public IObservable<string> GetSubscribeResponseAsObservable();
    public IObservable<FilterLog> GetSubscriptionDataResponsesAsObservable();
    public IObservable<bool> GetUnsubscribeResponseAsObservable();

    // Lifecycle (inherited)
    public Task UnsubscribeAsync();
}
```

### EthNewBlockHeadersObservableSubscription

Observable subscription for new block headers.

```csharp
public class EthNewBlockHeadersObservableSubscription : RpcStreamingSubscriptionObservableHandler<Block>
{
    // Constructor
    public EthNewBlockHeadersObservableSubscription(IStreamingClient client);

    // Subscribe Methods
    public Task SubscribeAsync(object id = null);
    public RpcRequest BuildRequest(object id);

    // Observable Streams (inherited)
    public IObservable<string> GetSubscribeResponseAsObservable();
    public IObservable<Block> GetSubscriptionDataResponsesAsObservable();
    public IObservable<bool> GetUnsubscribeResponseAsObservable();

    // Lifecycle (inherited)
    public Task UnsubscribeAsync();
}
```

### EthNewPendingTransactionObservableSubscription

Observable subscription for new pending transactions.

```csharp
public class EthNewPendingTransactionObservableSubscription : RpcStreamingSubscriptionObservableHandler<string>
{
    // Constructor
    public EthNewPendingTransactionObservableSubscription(IStreamingClient client);

    // Subscribe Methods
    public Task SubscribeAsync(object id = null);

    // Observable Streams (inherited)
    public IObservable<string> GetSubscribeResponseAsObservable();
    public IObservable<string> GetSubscriptionDataResponsesAsObservable();  // Emits transaction hashes
    public IObservable<bool> GetUnsubscribeResponseAsObservable();
}
```

### EthBlockNumberObservableHandler

Observable handler for eth_blockNumber (sends one request, emits the response, completes).

```csharp
public class EthBlockNumberObservableHandler : RpcStreamingResponseNoParamsObservableHandler<HexBigInteger, EthBlockNumber>
{
    // Constructor
    public EthBlockNumberObservableHandler(IStreamingClient streamingClient);

    // Observable Stream (inherited)
    public IObservable<HexBigInteger> GetResponseAsObservable();

    // Request Methods (inherited)
    public Task SendRequestAsync(object id = null);
}
```

### EthGetBalanceObservableHandler

Observable handler for eth_getBalance (sends one request, emits the response, completes).

```csharp
public class EthGetBalanceObservableHandler : RpcStreamingResponseParamsObservableHandler<HexBigInteger, EthGetBalance>
{
    // Constructor
    public EthGetBalanceObservableHandler(IStreamingClient streamingClient);

    // Observable Stream (inherited)
    public IObservable<HexBigInteger> GetResponseAsObservable();

    // Request Methods (inherited)
    public Task SendRequestAsync(string address, BlockParameter block, object id = null);
}
```

### RpcStreamingSubscriptionObservableHandler<T>

Base class for subscription-based observable handlers. It is a **concrete** class (not `abstract`), and `SubscribeAsync(RpcRequest)` - inherited from `RpcStreamingSubscriptionHandler<T>` - is `protected`, not `public`; each derived subscription (e.g. `EthLogsObservableSubscription`) exposes its own public `SubscribeAsync(...)` overload that calls it:

```csharp
public class RpcStreamingSubscriptionObservableHandler<TSubscriptionDataResponse> : RpcStreamingSubscriptionHandler<TSubscriptionDataResponse>
{
    // Observables
    public IObservable<string> GetSubscribeResponseAsObservable();
    public IObservable<TSubscriptionDataResponse> GetSubscriptionDataResponsesAsObservable();
    public IObservable<bool> GetUnsubscribeResponseAsObservable();

    // Lifecycle
    protected Task SubscribeAsync(RpcRequest request); // inherited, protected
    public Task UnsubscribeAsync();

    // Internal Subjects
    protected Subject<string> SubscribeResponseSubject { get; set; }
    protected Subject<bool> UnsubscribeResponseSubject { get; set; }
    protected Subject<TSubscriptionDataResponse> SubscriptionDataResponseSubject { get; set; }
}
```

### RpcStreamingResponseObservableHandler<T>

Base class for request/response observable handlers (send one request, emit the response, complete). Also a **concrete** class, not `abstract`:

```csharp
public class RpcStreamingResponseObservableHandler<TResponse> : RpcStreamingRequestResponseHandler<TResponse>
{
    // Observable
    public IObservable<TResponse> GetResponseAsObservable();

    // Internal Subject
    protected Subject<TResponse> ResponseSubject { get; set; }
}
```

## Important Notes

### WebSocket Connection Required

All observable handlers require an active IStreamingClient connection:

```csharp
var wsClient = new StreamingWebSocketClient("ws://localhost:8546");
await wsClient.StartAsync();  // MUST call before creating subscriptions

// Use subscriptions...

await wsClient.StopAsync();  // Clean up when done
```

### Observable Lifecycle

Subscriptions auto-complete when unsubscribed:

```csharp
var dataStream = subscription.GetSubscriptionDataResponsesAsObservable();
var disposable = dataStream.Subscribe(data => { /* ... */ });

// Later...
await subscription.UnsubscribeAsync();  // Triggers OnCompleted, disposes dataStream

disposable.Dispose();  // Clean up subscription
```

### Dispose Pattern

Always dispose subscriptions to prevent memory leaks:

```csharp
var disposable = observable.Subscribe(/* ... */);

// When done:
disposable.Dispose();
```

Or use `using`:

```csharp
using (var disposable = observable.Subscribe(/* ... */))
{
    // Subscription active
}
// Automatically disposed
```

### Cold vs Hot Observables

These are **hot observables** backed by a `Subject`; all `Subscribe` calls share the single `eth_subscribe` made by `SubscribeAsync`:

```csharp
var blockSub = new EthNewBlockHeadersObservableSubscription(client);
var observable = blockSub.GetSubscriptionDataResponsesAsObservable();

// Subscribing to the observable does not send eth_subscribe
var sub1 = observable.Subscribe(block => Console.WriteLine("Sub1: " + block.Number));
await blockSub.SubscribeAsync();  // Creates subscription

// Second subscription to same observable does NOT create new eth_subscribe
var sub2 = observable.Subscribe(block => Console.WriteLine("Sub2: " + block.Number));
```

Both subscribers receive same data stream from single eth_subscribe.

### Subscription ID Tracking

Use subscribe response observable to track subscription IDs:

```csharp
string subscriptionId = null;
subscription.GetSubscribeResponseAsObservable()
    .Subscribe(id => subscriptionId = id);

await subscription.SubscribeAsync();

// subscriptionId now contains the eth_subscribe response
Console.WriteLine($"Subscription ID: {subscriptionId}");
```

### System.Reactive Operators

All standard Rx operators work:

```csharp
observable
    .Where(block => block.Number.Value % 100 == 0)  // Filter
    .Select(block => block.Number.Value)            // Transform
    .Buffer(10)                                     // Batch
    .Throttle(TimeSpan.FromSeconds(1))             // Rate limit
    .Timeout(TimeSpan.FromSeconds(30))             // Timeout
    .Retry(3)                                       // Retry on error
    .Subscribe(/* ... */);
```

See System.Reactive documentation for complete operator list.

## Related Packages

### Dependencies
- **Nethereum.JsonRpc.Client** - streaming abstractions (`IStreamingClient`); `StreamingWebSocketClient` is in `Nethereum.JsonRpc.WebSocketClient`
- **Nethereum.RPC** - RPC request builders and DTOs
- **Nethereum.Hex** - Hex encoding/decoding
- **System.Reactive** - Reactive Extensions for .NET

### Alternative Approaches
- **Nethereum.RPC.Reactive** - the **same** Rx.NET-based approach as this package (it depends on `System.Reactive` 4.1.3 and provides its own `EthNewBlockHeadersObservableSubscription`/`EthLogsObservableSubscription`/`EthNewPendingTransactionObservableSubscription` under `Nethereum.RPC.Reactive.Eth.Subscriptions`, reusable against any `IStreamingClient`). For **event-based** (non-Rx) subscriptions, use the plain handlers in `Nethereum.RPC.Eth.Subscriptions` (e.g. `EthNewBlockHeadersSubscription`), which expose C# `event`s instead of `IObservable`.