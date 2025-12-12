# Nethereum.RPC.Reactive

**Nethereum.RPC.Reactive** provides Reactive Extensions (Rx.NET) support for Ethereum RPC operations. It enables reactive programming patterns for monitoring blockchain events, streaming blocks and transactions, and building real-time Ethereum applications using observables.

## Features

- **WebSocket Subscriptions** - eth_subscribe support for real-time events
  - New block headers (`eth_subscribe` + `newHeads`)
  - Pending transactions (`eth_subscribe` + `newPendingTransactions`)
  - Event logs (`eth_subscribe` + `logs`)
- **Polling-Based Streams** - Reactive streams using HTTP/RPC polling
  - Block streams with configurable polling intervals
  - Transaction streams
  - Pending transaction streams
- **Observable Operators** - Rx.NET operators for blockchain data
- **Flexible Polling** - Custom polling strategies
- **Event Processing** - LINQ-style queries over blockchain events
- **Backpressure Handling** - Built-in support for managing event flow

## Installation

```bash
dotnet add package Nethereum.RPC.Reactive
```

## Dependencies

- `Nethereum.RPC` - Core RPC functionality
- `System.Reactive` (4.1.3+) - Reactive Extensions

## Quick Start

### WebSocket Subscriptions (Real-time)

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.RPC.Eth.DTOs;

var client = new StreamingWebSocketClient("ws://127.0.0.1:8546");

// Subscribe to new block headers
var subscription = new EthNewBlockHeadersObservableSubscription(client);

subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(block =>
{
    Console.WriteLine($"New block: {block.Number.Value}");
    Console.WriteLine($"Block hash: {block.BlockHash}");
    Console.WriteLine($"Timestamp: {block.Timestamp.Value}");
});

// Handle subscription confirmation
subscription.GetSubscribeResponseAsObservable().Subscribe(subscriptionId =>
{
    Console.WriteLine($"Subscription ID: {subscriptionId}");
});

await client.StartAsync();
await subscription.SubscribeAsync();

// Keep running...
await Task.Delay(Timeout.Infinite);
```

### Polling-Based Streams (HTTP/RPC)

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Reactive.Polling;
using System.Reactive.Linq;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Stream new blocks as they arrive
web3.Eth.GetBlocksWithTransactionHashes()
    .Subscribe(block =>
    {
        Console.WriteLine($"New block: {block.Number}");
        Console.WriteLine($"Transaction count: {block.TransactionHashes.Length}");
    });

// Keep running...
await Task.Delay(Timeout.Infinite);
```

## Subscription Types

### EthNewBlockHeadersObservableSubscription

Subscribe to new block headers in real-time via WebSocket:

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;

var client = new StreamingWebSocketClient("ws://127.0.0.1:8546");
var subscription = new EthNewBlockHeadersObservableSubscription(client);

subscription.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(
        block => Console.WriteLine($"Block {block.Number.Value}: {block.BlockHash}"),
        error => Console.WriteLine($"Error: {error.Message}"),
        () => Console.WriteLine("Subscription completed")
    );

await client.StartAsync();
await subscription.SubscribeAsync();
```

### EthNewPendingTransactionObservableSubscription

Monitor pending transactions in the mempool:

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;

var client = new StreamingWebSocketClient("ws://127.0.0.1:8546");
var subscription = new EthNewPendingTransactionObservableSubscription(client);

subscription.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(txHash =>
    {
        Console.WriteLine($"Pending transaction: {txHash}");
    });

await client.StartAsync();
await subscription.SubscribeAsync();
```

### EthLogsObservableSubscription

Subscribe to event logs with filters:

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.RPC.Eth.DTOs;

var client = new StreamingWebSocketClient("ws://127.0.0.1:8546");
var subscription = new EthLogsObservableSubscription(client);

// Filter for specific contract events
var filterInput = new NewFilterInput
{
    Address = new[] { "0x6B175474E89094C44Da98b954EedeAC495271d0F" }, // DAI contract
    Topics = new[] { "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef" } // Transfer event
};

subscription.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(log =>
    {
        Console.WriteLine($"Log from block: {log.BlockNumber.Value}");
        Console.WriteLine($"Transaction: {log.TransactionHash}");
    });

await client.StartAsync();
await subscription.SubscribeAsync(filterInput);
```

## Polling Extensions

### Block Streaming

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Reactive.Polling;
using System.Reactive.Linq;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Stream blocks with transaction hashes
web3.Eth.GetBlocksWithTransactionHashes()
    .Subscribe(block =>
    {
        Console.WriteLine($"Block: {block.Number}");
        Console.WriteLine($"Transactions: {block.TransactionHashes.Length}");
    });

// Stream blocks with full transaction details
web3.Eth.GetBlocksWithTransactions()
    .Subscribe(block =>
    {
        Console.WriteLine($"Block: {block.Number}");
        foreach (var tx in block.Transactions)
        {
            Console.WriteLine($"  From: {tx.From} To: {tx.To} Value: {tx.Value}");
        }
    });

// Stream specific block range
web3.Eth.GetBlocksWithTransactionHashes(
    start: new BlockParameter(18000000),
    end: new BlockParameter(18000100)
).Subscribe(block =>
{
    Console.WriteLine($"Historical block: {block.Number}");
});
```

**From:** `src/Nethereum.RPC.Reactive/Polling/BlockStreamExtensions.cs:10`

### Custom Polling Interval

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Reactive.Polling;
using System.Reactive.Linq;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Custom poller - check every 5 seconds
var customPoller = Observable
    .Timer(TimeSpan.Zero, TimeSpan.FromSeconds(5))
    .Select(_ => Unit.Default);

web3.Eth.GetBlocksWithTransactionHashes(poller: customPoller)
    .Subscribe(block =>
    {
        Console.WriteLine($"Block (5s interval): {block.Number}");
    });
```

**From:** `src/Nethereum.RPC.Reactive/Polling/PollingExtensions.cs:15`

## Examples

### Example 1: Real-time Block Monitor with Error Handling

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using System.Reactive.Linq;

public class BlockMonitor
{
    private StreamingWebSocketClient client;
    private EthNewBlockHeadersObservableSubscription subscription;

    public async Task StartAsync(string wsUrl)
    {
        client = new StreamingWebSocketClient(wsUrl);
        client.Error += Client_Error;

        subscription = new EthNewBlockHeadersObservableSubscription(client);

        // Subscribe to blocks
        subscription.GetSubscriptionDataResponsesAsObservable()
            .Subscribe(
                onNext: block =>
                {
                    Console.WriteLine($"⛏️  Block {block.Number.Value}");
                    Console.WriteLine($"   Hash: {block.BlockHash}");
                    Console.WriteLine($"   Time: {DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value)}");
                    Console.WriteLine($"   Transactions: {block.TransactionHashes?.Length ?? 0}");
                },
                onError: error =>
                {
                    Console.WriteLine($"❌ Subscription error: {error.Message}");
                },
                onCompleted: () =>
                {
                    Console.WriteLine("Subscription completed");
                }
            );

        // Log subscription confirmation
        subscription.GetSubscribeResponseAsObservable()
            .Subscribe(subscriptionId =>
            {
                Console.WriteLine($"✅ Subscribed with ID: {subscriptionId}");
            });

        await client.StartAsync();
        await subscription.SubscribeAsync();
    }

    private async void Client_Error(object sender, Exception ex)
    {
        Console.WriteLine($"⚠️  WebSocket error: {ex.Message}");
        Console.WriteLine("Attempting to reconnect...");

        // Stop and restart
        await ((StreamingWebSocketClient)sender).StopAsync();
        await StartAsync(((StreamingWebSocketClient)sender).Path);
    }

    public async Task StopAsync()
    {
        if (client != null)
        {
            await client.StopAsync();
        }
    }
}

// Usage
var monitor = new BlockMonitor();
await monitor.StartAsync("ws://127.0.0.1:8546");
```

**From:** `consoletests/Nethereum.Parity.Reactive.ConsoleTest/Program.cs:32`

### Example 2: Custom Account Balance Monitoring (Parity PubSub)

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.RpcStreaming;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;

// Custom PubSub subscription for account balance changes
var client = new StreamingWebSocketClient("ws://127.0.0.1:8546");
client.Error += (sender, ex) => Console.WriteLine($"Error: {ex.Message}");

var accountAddress = "0x12890d2cce102216644c59daE5baed380d84830c";

// Create balance subscription (Parity-specific)
var balanceSubscription = new ParityPubSubObservableSubscription<HexBigInteger>(client);

// Build balance query request
var ethBalanceRequest = new EthGetBalance().BuildRequest(
    accountAddress,
    BlockParameter.CreateLatest()
);

// Subscribe to balance updates
balanceSubscription.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(
        newBalance => Console.WriteLine($"💰 New Balance: {Web3.Convert.FromWei(newBalance.Value)} ETH"),
        onError => Console.WriteLine($"Error: {onError.Message}")
    );

// Log subscription confirmation
balanceSubscription.GetSubscribeResponseAsObservable()
    .Subscribe(x => Console.WriteLine($"Balance subscription ID: {x}"));

await client.StartAsync();
await balanceSubscription.SubscribeAsync(ethBalanceRequest);

// Keep monitoring
await Task.Delay(Timeout.Infinite);
```

**From:** `consoletests/Nethereum.Parity.Reactive.ConsoleTest/Program.cs:35`

### Example 3: Filtering Blocks with LINQ Operators

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Reactive.Polling;
using System.Reactive.Linq;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Only blocks with more than 100 transactions
web3.Eth.GetBlocksWithTransactionHashes()
    .Where(block => block.TransactionHashes.Length > 100)
    .Subscribe(block =>
    {
        Console.WriteLine($"Busy block: {block.Number} ({block.TransactionHashes.Length} txs)");
    });

// Calculate average transactions per block (last 10 blocks)
web3.Eth.GetBlocksWithTransactionHashes()
    .Buffer(10) // Group last 10 blocks
    .Select(blocks => blocks.Average(b => b.TransactionHashes.Length))
    .Subscribe(avgTxs =>
    {
        Console.WriteLine($"Average transactions per block (last 10): {avgTxs:F2}");
    });

// Alert on large base fee
web3.Eth.GetBlocksWithTransactionHashes()
    .Where(block => block.BaseFeePerGas?.Value > 100000000000) // > 100 gwei
    .Subscribe(block =>
    {
        Console.WriteLine($"⚠️  High base fee alert! Block {block.Number}: {block.BaseFeePerGas.Value} wei");
    });
```

### Example 4: Transaction Stream with Filtering

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Reactive.Polling;
using System.Reactive.Linq;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Monitor large ETH transfers
web3.Eth.GetBlocksWithTransactions()
    .SelectMany(block => block.Transactions) // Flatten to transaction stream
    .Where(tx => tx.Value?.Value > Web3.Convert.ToWei(100)) // > 100 ETH
    .Subscribe(tx =>
    {
        var ethValue = Web3.Convert.FromWei(tx.Value.Value);
        Console.WriteLine($"🐋 Whale transfer: {ethValue} ETH");
        Console.WriteLine($"   From: {tx.From}");
        Console.WriteLine($"   To: {tx.To}");
        Console.WriteLine($"   Tx: {tx.TransactionHash}");
    });
```

### Example 5: Pending Transaction Monitoring

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.Web3;
using System.Reactive.Linq;

var client = new StreamingWebSocketClient("ws://127.0.0.1:8546");
var web3 = new Web3(client);

var subscription = new EthNewPendingTransactionObservableSubscription(client);

subscription.GetSubscriptionDataResponsesAsObservable()
    .Buffer(TimeSpan.FromSeconds(5)) // Group pending txs every 5 seconds
    .Subscribe(async txHashes =>
    {
        Console.WriteLine($"📬 {txHashes.Count} pending transactions in last 5 seconds");

        // Get details for first pending transaction
        if (txHashes.Count > 0)
        {
            var tx = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(txHashes[0]);
            if (tx != null)
            {
                Console.WriteLine($"   Sample tx: {tx.TransactionHash}");
                Console.WriteLine($"   Gas price: {tx.GasPrice?.Value ?? 0} wei");
            }
        }
    });

await client.StartAsync();
await subscription.SubscribeAsync();
```

### Example 6: Event Log Streaming with Decoding

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;

// Transfer event DTO
[Event("Transfer")]
public class TransferEventDTO : IEventDTO
{
    [Parameter("address", "from", 1, true)]
    public string From { get; set; }

    [Parameter("address", "to", 2, true)]
    public string To { get; set; }

    [Parameter("uint256", "value", 3, false)]
    public BigInteger Value { get; set; }
}

var client = new StreamingWebSocketClient("ws://127.0.0.1:8546");
var subscription = new EthLogsObservableSubscription(client);

// Filter for USDC transfers
var filterInput = new NewFilterInput
{
    Address = new[] { "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48" }, // USDC
    Topics = new[] { Event<TransferEventDTO>.GetEventABI().Sha3Signature } // Transfer signature
};

subscription.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(log =>
    {
        try
        {
            var decoded = Event<TransferEventDTO>.DecodeEvent(log);
            var amount = Web3.Convert.FromWei(decoded.Event.Value, 6); // USDC has 6 decimals

            Console.WriteLine($"💵 USDC Transfer: {amount} USDC");
            Console.WriteLine($"   From: {decoded.Event.From}");
            Console.WriteLine($"   To: {decoded.Event.To}");
            Console.WriteLine($"   Block: {log.BlockNumber.Value}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error decoding: {ex.Message}");
        }
    });

await client.StartAsync();
await subscription.SubscribeAsync(filterInput);
```

### Example 7: Combining Multiple Observables

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using System.Reactive.Linq;

var client = new StreamingWebSocketClient("ws://127.0.0.1:8546");

var blockSubscription = new EthNewBlockHeadersObservableSubscription(client);
var pendingTxSubscription = new EthNewPendingTransactionObservableSubscription(client);

// Combine streams
var blocks = blockSubscription.GetSubscriptionDataResponsesAsObservable()
    .Select(block => $"Block: {block.Number.Value}");

var pendingTxs = pendingTxSubscription.GetSubscriptionDataResponsesAsObservable()
    .Buffer(TimeSpan.FromSeconds(10))
    .Select(txs => $"Pending: {txs.Count} txs");

// Merge and display
Observable.Merge(blocks, pendingTxs)
    .Subscribe(message => Console.WriteLine(message));

await client.StartAsync();
await blockSubscription.SubscribeAsync();
await pendingTxSubscription.SubscribeAsync();
```

### Example 8: Backpressure with Throttling

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Reactive.Polling;
using System.Reactive.Linq;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Throttle block stream to process at most 1 block per second
web3.Eth.GetBlocksWithTransactionHashes()
    .Throttle(TimeSpan.FromSeconds(1))
    .Subscribe(block =>
    {
        Console.WriteLine($"Processing block: {block.Number}");
        // Expensive processing here
    });

// Sample blocks - only process every 10th block
web3.Eth.GetBlocksWithTransactionHashes()
    .Buffer(10)
    .Select(blocks => blocks.Last()) // Take last block from each group
    .Subscribe(block =>
    {
        Console.WriteLine($"Sampled block: {block.Number}");
    });
```

### Example 9: Historical Block Range Processing

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Reactive.Polling;
using Nethereum.RPC.Eth.DTOs;
using System.Reactive.Linq;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Process blocks from 18,000,000 to 18,001,000
var startBlock = new BlockParameter(18000000);
var endBlock = new BlockParameter(18001000);

web3.Eth.GetBlocksWithTransactionHashes(startBlock, endBlock)
    .Do(block => Console.WriteLine($"Processing: {block.Number}"))
    .SelectMany(block => block.TransactionHashes) // Flatten to transaction hashes
    .Count()
    .Subscribe(totalTxs =>
    {
        Console.WriteLine($"Total transactions in range: {totalTxs}");
    });
```

### Example 10: Error Recovery and Retry

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using System.Reactive.Linq;

var client = new StreamingWebSocketClient("ws://127.0.0.1:8546");
var subscription = new EthNewBlockHeadersObservableSubscription(client);

subscription.GetSubscriptionDataResponsesAsObservable()
    .Retry(3) // Retry up to 3 times on error
    .Catch<Block, Exception>(ex =>
    {
        Console.WriteLine($"Error after retries: {ex.Message}");
        return Observable.Empty<Block>(); // Complete gracefully
    })
    .Subscribe(block =>
    {
        Console.WriteLine($"Block: {block.Number.Value}");
    });

await client.StartAsync();
await subscription.SubscribeAsync();
```

## Advanced Patterns

### Custom Observable Handlers

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Reactive.RpcStreaming;
using System.Reactive.Linq;

// Create custom observable for any RPC method
var client = new RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR-PROJECT-ID"));

var blockNumberObservable = Observable
    .Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1))
    .SelectMany(async _ =>
    {
        var ethBlockNumber = new Nethereum.RPC.Eth.Blocks.EthBlockNumber(client);
        return await ethBlockNumber.SendRequestAsync();
    });

blockNumberObservable.Subscribe(blockNumber =>
{
    Console.WriteLine($"Current block: {blockNumber.Value}");
});
```

### Stream Composition

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Reactive.Polling;
using System.Reactive.Linq;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

// Create complex stream pipelines
var largeBlockTransfers = web3.Eth.GetBlocksWithTransactions()
    .SelectMany(block => block.Transactions)
    .Where(tx => tx.Value?.Value > Web3.Convert.ToWei(10))
    .GroupBy(tx => tx.From)
    .SelectMany(group => group
        .Buffer(TimeSpan.FromMinutes(1))
        .Where(txs => txs.Count >= 3) // 3+ large transfers in 1 minute from same address
        .Select(txs => new { From = group.Key, Transactions = txs })
    );

largeBlockTransfers.Subscribe(result =>
{
    Console.WriteLine($"🚨 Suspicious activity from {result.From}");
    Console.WriteLine($"   {result.Transactions.Count} large transfers in 1 minute");
});
```

## Best Practices

1. **Use WebSocket Subscriptions for Real-time Data**: More efficient than polling
   ```csharp
   // Good - WebSocket subscription (real-time)
   var subscription = new EthNewBlockHeadersObservableSubscription(client);

   // Less efficient - HTTP polling
   web3.Eth.GetBlocksWithTransactionHashes()
   ```

2. **Handle Errors Gracefully**: Always provide error handlers
   ```csharp
   observable.Subscribe(
       onNext: data => ProcessData(data),
       onError: ex => LogError(ex),
       onCompleted: () => Console.WriteLine("Completed")
   );
   ```

3. **Dispose Subscriptions**: Clean up resources when done
   ```csharp
   var disposable = observable.Subscribe(...);
   // Later...
   disposable.Dispose();
   ```

4. **Use Appropriate Polling Intervals**: Balance freshness vs. resource usage
   ```csharp
   // For fast-changing data
   var fastPoller = Observable.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

   // For slow-changing data
   var slowPoller = Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(30));
   ```

5. **Apply Backpressure Operators**: Prevent overwhelming downstream consumers
   ```csharp
   observable
       .Throttle(TimeSpan.FromSeconds(1))  // Max 1 per second
       .Buffer(10)                          // Process in batches of 10
       .Sample(TimeSpan.FromSeconds(5))    // Sample every 5 seconds
   ```

6. **Reconnect on WebSocket Errors**: Implement automatic reconnection
   ```csharp
   client.Error += async (sender, ex) =>
   {
       await ((StreamingWebSocketClient)sender).StopAsync();
       await ReconnectAsync();
   };
   ```

## Rx.NET Operators for Blockchain Data

Common operators for blockchain data processing:

| Operator | Use Case | Example |
|----------|----------|---------|
| `Where` | Filter blocks/transactions | `Where(block => block.Number > 1000000)` |
| `Select` | Transform data | `Select(block => block.Number.Value)` |
| `SelectMany` | Flatten nested data | `SelectMany(block => block.Transactions)` |
| `Buffer` | Group items by count/time | `Buffer(TimeSpan.FromSeconds(10))` |
| `Throttle` | Rate limiting | `Throttle(TimeSpan.FromSeconds(1))` |
| `Sample` | Periodic sampling | `Sample(TimeSpan.FromSeconds(5))` |
| `Take` | Limit results | `Take(100)` // First 100 blocks |
| `Skip` | Skip initial items | `Skip(10)` // Skip first 10 |
| `Distinct` | Remove duplicates | `Distinct(tx => tx.TransactionHash)` |
| `GroupBy` | Group by key | `GroupBy(tx => tx.From)` |
| `Merge` | Combine streams | `Observable.Merge(blocks, txs)` |
| `Zip` | Combine pairwise | `blocks.Zip(receipts)` |
| `Retry` | Retry on error | `Retry(3)` |
| `Catch` | Handle errors | `Catch<T, Exception>(...)` |

## Polling vs. WebSocket Subscriptions

### When to Use Polling

- HTTP/HTTPS RPC endpoints only
- Infrequent data access
- Historical data processing
- Simple deployment (no WebSocket support needed)

### When to Use WebSocket Subscriptions

- Real-time data (blocks, transactions, events)
- High-frequency updates
- Lower latency requirements
- Reduced server load (push vs. pull)

## Troubleshooting

### Subscription Not Receiving Data

```
Observable never fires
```

**Solution**: Ensure WebSocket client is started:
```csharp
await client.StartAsync();
await subscription.SubscribeAsync();
```

### Memory Leaks

```
Memory usage grows over time
```

**Solution**: Always dispose subscriptions:
```csharp
var disposable = observable.Subscribe(...);
// Later
disposable.Dispose();
```

### Missed Blocks

```
Blocks are skipped in the stream
```

**Solution**: Adjust polling interval or use WebSocket subscriptions for guaranteed delivery.

## Related Packages

- **Nethereum.RPC** - Core RPC functionality
- **Nethereum.JsonRpc.WebSocketClient** - WebSocket client for subscriptions
- **Nethereum.Web3** - High-level Web3 API
- **System.Reactive** - Reactive Extensions library

## Additional Resources

- [Reactive Extensions (Rx.NET) Documentation](http://reactivex.io/)
- [System.Reactive on GitHub](https://github.com/dotnet/reactive)
- [Ethereum WebSocket API](https://ethereum.org/en/developers/docs/apis/json-rpc/#subscription-methods)
- [Nethereum Documentation](http://docs.nethereum.com)

## License

MIT License - see LICENSE file for details
