# Nethereum.JsonRpc.WebSocketClient

WebSocket JSON-RPC client with support for real-time subscriptions and event streaming.

## Overview

Nethereum.JsonRpc.WebSocketClient provides **WebSocket transport implementations** for Ethereum node communication, supporting both standard request/response patterns and **real-time event subscriptions**. WebSockets enable push-based notifications from the node for new blocks, pending transactions, and contract events without polling.

**Key Features:**
- **WebSocket** transport (wss:// and ws://)
- **Real-time subscriptions** (newHeads, logs, pendingTransactions, syncing)
- **Event streaming** with automatic message routing
- Request/response and streaming modes
- Custom request headers support
- Connection management and automatic reconnection
- Thread-safe subscription handling
- Production-tested reliability

**Use Cases:**
- Real-time block monitoring
- Contract event streaming
- Pending transaction monitoring
- Mempool watching (MEV, arbitrage)
- Live dashboard updates
- Blockchain indexers
- Wallet notifications

## Installation

```bash
dotnet add package Nethereum.JsonRpc.WebSocketClient
```

**Requirements:**
- Ethereum node with WebSocket support (Geth, Erigon, Infura, Alchemy)
- .NET Standard 2.0+ or .NET Core 2.1+

## Dependencies

**Nethereum:**
- **Nethereum.JsonRpc.Client** - Core RPC abstraction

**External:**
- **System.Net.WebSockets.Client** - WebSocket support
- **Newtonsoft.Json** - JSON serialization
- **Microsoft.Extensions.Logging.Abstractions** - Logging support

## Quick Start

### Basic WebSocket Client (Request/Response)

```csharp
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.RPC.Eth;

// Connect to WebSocket endpoint
var client = new WebSocketClient("ws://localhost:8546");

// Use like any other RPC client
var ethBlockNumber = new EthBlockNumber(client);
var blockNumber = await ethBlockNumber.SendRequestAsync();

Console.WriteLine($"Current block: {blockNumber.Value}");

// Always dispose when done
client.Dispose();
```

### Streaming Client (Subscriptions)

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Eth.Subscriptions;

// Create streaming client
var client = new StreamingWebSocketClient("wss://mainnet.infura.io/ws/v3/YOUR_PROJECT_ID");

// Create subscription for new blocks
var subscription = new EthNewBlockHeadersSubscription(client);

// Handle new block events
subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(block =>
{
    Console.WriteLine($"New block: {block.Number.Value}");
    Console.WriteLine($"Hash: {block.BlockHash}");
    Console.WriteLine($"Miner: {block.Miner}");
});

// Start streaming
await client.StartAsync();

// Subscribe
await subscription.SubscribeAsync();

// Keep running
Console.WriteLine("Monitoring new blocks. Press Enter to exit.");
Console.ReadLine();

// Cleanup
await subscription.UnsubscribeAsync();
await client.StopAsync();
client.Dispose();
```

## Usage Examples

### Example 1: Basic WebSocket Connection

```csharp
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.RPC.Eth;

// Local Geth/Erigon
var client = new WebSocketClient("ws://localhost:8546");

// Infura
var infuraClient = new WebSocketClient(
    "wss://mainnet.infura.io/ws/v3/YOUR_PROJECT_ID"
);

// Alchemy
var alchemyClient = new WebSocketClient(
    "wss://eth-mainnet.g.alchemy.com/v2/YOUR_API_KEY"
);

// Use with RPC services
var ethChainId = new EthChainId(client);
var chainId = await ethChainId.SendRequestAsync();

var ethGasPrice = new EthGasPrice(client);
var gasPrice = await ethGasPrice.SendRequestAsync();

Console.WriteLine($"Chain ID: {chainId.Value}");
Console.WriteLine($"Gas Price: {gasPrice.Value} wei");

// Cleanup
client.Dispose();
```

### Example 2: Real-Time Block Monitoring

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.RPC.Eth.DTOs;
using System.Reactive.Linq;

var client = new StreamingWebSocketClient("ws://localhost:8546");

// Create new block headers subscription
var subscription = new EthNewBlockHeadersSubscription(client);

// Subscribe to new blocks
subscription.GetSubscriptionDataResponsesAsObservable()
    .Subscribe(block =>
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] New Block");
        Console.WriteLine($"  Number: {block.Number.Value}");
        Console.WriteLine($"  Hash: {block.BlockHash}");
        Console.WriteLine($"  Parent: {block.ParentHash}");
        Console.WriteLine($"  Timestamp: {DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value)}");
        Console.WriteLine($"  Difficulty: {block.Difficulty.Value}");
        Console.WriteLine($"  Gas Used: {block.GasUsed.Value:N0}");
        Console.WriteLine($"  Transactions: {block.TransactionCount()}");
        Console.WriteLine();
    },
    error => Console.WriteLine($"Error: {error.Message}"));

// Start client and subscribe
await client.StartAsync();
await subscription.SubscribeAsync();

Console.WriteLine("Monitoring blocks. Press Enter to stop.");
Console.ReadLine();

// Cleanup
await subscription.UnsubscribeAsync();
await client.StopAsync();
client.Dispose();
```

### Example 3: Contract Event Streaming

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;

var client = new StreamingWebSocketClient("wss://mainnet.infura.io/ws/v3/YOUR_PROJECT_ID");

// Create logs subscription for USDC Transfer events
var transferEventSignature = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";
var usdcAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";

var filterLogs = new NewFilterInput
{
    Address = new[] { usdcAddress },
    Topics = new[] { transferEventSignature }
};

var subscription = new EthLogsSubscription(client);
await subscription.SubscribeAsync(filterLogs);

// Handle Transfer events
subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(log =>
{
    var from = "0x" + log.Topics[1].ToString().Substring(26);
    var to = "0x" + log.Topics[2].ToString().Substring(26);
    var amount = new HexBigInteger(log.Data).Value;

    Console.WriteLine($"USDC Transfer:");
    Console.WriteLine($"  From: {from}");
    Console.WriteLine($"  To: {to}");
    Console.WriteLine($"  Amount: {amount / 1000000m:N2} USDC");  // USDC has 6 decimals
    Console.WriteLine($"  Tx: {log.TransactionHash}");
    Console.WriteLine();
});

await client.StartAsync();

Console.WriteLine("Monitoring USDC transfers. Press Enter to stop.");
Console.ReadLine();

await subscription.UnsubscribeAsync();
await client.StopAsync();
client.Dispose();
```

### Example 4: Pending Transaction Monitoring (Mempool)

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.Web3;

var client = new StreamingWebSocketClient("ws://localhost:8546");

// Create pending transactions subscription
var subscription = new EthNewPendingTransactionSubscription(client);

// Handle new pending transactions
subscription.GetSubscriptionDataResponsesAsObservable()
    .Buffer(TimeSpan.FromSeconds(1))  // Batch for 1 second
    .Subscribe(async txHashes =>
    {
        if (txHashes.Count > 0)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {txHashes.Count} new pending transactions");

            // Fetch details for first transaction
            var web3 = new Web3(client);
            var txDetails = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(txHashes[0]);

            if (txDetails != null)
            {
                Console.WriteLine($"  First tx hash: {txDetails.TransactionHash}");
                Console.WriteLine($"  From: {txDetails.From}");
                Console.WriteLine($"  To: {txDetails.To}");
                Console.WriteLine($"  Value: {Web3.Convert.FromWei(txDetails.Value)} ETH");
                Console.WriteLine($"  Gas Price: {Web3.Convert.FromWei(txDetails.GasPrice, Web3.Convert.UnitConversion.Gwei)} Gwei");
            }
        }
    });

await client.StartAsync();
await subscription.SubscribeAsync();

Console.WriteLine("Monitoring mempool. Press Enter to stop.");
Console.ReadLine();

await subscription.UnsubscribeAsync();
await client.StopAsync();
client.Dispose();
```

### Example 5: Multiple Subscriptions

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;

var client = new StreamingWebSocketClient("ws://localhost:8546");

// Create multiple subscriptions
var blockSubscription = new EthNewBlockHeadersSubscription(client);
var pendingTxSubscription = new EthNewPendingTransactionSubscription(client);
var syncSubscription = new EthSyncingSubscription(client);

// Handle new blocks
blockSubscription.GetSubscriptionDataResponsesAsObservable().Subscribe(block =>
{
    Console.WriteLine($"[BLOCK] #{block.Number.Value}");
});

// Handle pending transactions (with throttling)
pendingTxSubscription.GetSubscriptionDataResponsesAsObservable()
    .Buffer(TimeSpan.FromSeconds(5))
    .Subscribe(txHashes =>
    {
        Console.WriteLine($"[MEMPOOL] {txHashes.Count} pending transactions in last 5s");
    });

// Handle sync status
syncSubscription.GetSubscriptionDataResponsesAsObservable().Subscribe(syncStatus =>
{
    if (syncStatus.IsSyncing)
    {
        Console.WriteLine($"[SYNC] Current: {syncStatus.CurrentBlock}, Highest: {syncStatus.HighestBlock}");
    }
    else
    {
        Console.WriteLine($"[SYNC] Node is synced");
    }
});

// Start client and all subscriptions
await client.StartAsync();
await blockSubscription.SubscribeAsync();
await pendingTxSubscription.SubscribeAsync();
await syncSubscription.SubscribeAsync();

Console.WriteLine("Monitoring multiple streams. Press Enter to stop.");
Console.ReadLine();

// Cleanup all subscriptions
await blockSubscription.UnsubscribeAsync();
await pendingTxSubscription.UnsubscribeAsync();
await syncSubscription.UnsubscribeAsync();
await client.StopAsync();
client.Dispose();
```

### Example 6: Custom Request Headers (Authentication)

```csharp
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.RPC.Eth;

var client = new WebSocketClient("wss://api.example.com/ws");

// Add custom headers (e.g., API key)
client.RequestHeaders.Add("X-API-Key", "your-api-key-here");
client.RequestHeaders.Add("Authorization", "Bearer your-token");

var ethBlockNumber = new EthBlockNumber(client);
var blockNumber = await ethBlockNumber.SendRequestAsync();

Console.WriteLine($"Block (authenticated): {blockNumber.Value}");

client.Dispose();
```

### Example 7: Production Reconnection Pattern (from Nethereum.WebSocketsStreamingTest)

**CRITICAL for production:** Automatic reconnection when WebSocket connection drops:

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using System.Reactive.Linq;

public class ProductionBlockMonitor
{
    private readonly string url;
    private StreamingWebSocketClient client;

    public ProductionBlockMonitor(string url)
    {
        this.url = url;
    }

    public async Task SubscribeAndRunAsync()
    {
        if (client == null)
        {
            client = new StreamingWebSocketClient(url);

            // ⭐ Production pattern: auto-reconnect on error
            client.Error += Client_Error;
        }

        var blockHeaderSubscription = new EthNewBlockHeadersObservableSubscription(client);

        // Get subscription ID when subscribed
        blockHeaderSubscription.GetSubscribeResponseAsObservable().Subscribe(subscriptionId =>
            Console.WriteLine($"Block Header subscription Id: {subscriptionId}"));

        // Process new blocks
        blockHeaderSubscription.GetSubscriptionDataResponsesAsObservable().Subscribe(
            block => Console.WriteLine($"New Block: {block.BlockHash}"),
            exception => Console.WriteLine($"BlockHeaderSubscription error info: {exception.Message}")
        );

        // Handle unsubscribe confirmation
        blockHeaderSubscription.GetUnsubscribeResponseAsObservable().Subscribe(response =>
            Console.WriteLine($"Block Header unsubscribe result: {response}"));

        await client.StartAsync();
        await blockHeaderSubscription.SubscribeAsync();

        Console.WriteLine("Monitoring blocks with auto-reconnect. Press Enter to stop.");
        Console.ReadLine();

        await blockHeaderSubscription.UnsubscribeAsync();
    }

    // ⭐ Production reconnection handler
    private async void Client_Error(object sender, Exception ex)
    {
        Console.WriteLine($"Client Error, restarting... ({ex.Message})");

        // Stop the failed connection
        await ((StreamingWebSocketClient)sender).StopAsync();

        // Restart everything
        await SubscribeAndRunAsync();
    }
}

// Usage
var monitor = new ProductionBlockMonitor("ws://localhost:8546");
await monitor.SubscribeAndRunAsync();
```

**Why this pattern works:**
- The `Client_Error` event catches all WebSocket failures
- Automatically stops the failed connection
- Recursively restarts the entire subscription flow
- Ensures continuous monitoring even through network disruptions

### Example 8: High-Frequency Event Processing with Reactive Extensions

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using System.Reactive.Linq;

var client = new StreamingWebSocketClient("ws://localhost:8546");
var subscription = new EthNewBlockHeadersSubscription(client);

// Advanced reactive processing
subscription.GetSubscriptionDataResponsesAsObservable()
    .Window(TimeSpan.FromMinutes(1))  // 1-minute windows
    .SelectMany(window => window
        .Aggregate(new
        {
            Count = 0,
            TotalGasUsed = BigInteger.Zero,
            TotalTransactions = 0
        }, (acc, block) => new
        {
            Count = acc.Count + 1,
            TotalGasUsed = acc.TotalGasUsed + block.GasUsed.Value,
            TotalTransactions = acc.TotalTransactions + (int)block.TransactionCount()
        }))
    .Subscribe(stats =>
    {
        Console.WriteLine($"=== 1-Minute Stats ===");
        Console.WriteLine($"Blocks: {stats.Count}");
        Console.WriteLine($"Avg Gas/Block: {stats.TotalGasUsed / stats.Count:N0}");
        Console.WriteLine($"Total Transactions: {stats.TotalTransactions}");
        Console.WriteLine();
    });

await client.StartAsync();
await subscription.SubscribeAsync();

Console.WriteLine("Collecting statistics. Press Enter to stop.");
Console.ReadLine();

await subscription.UnsubscribeAsync();
await client.StopAsync();
client.Dispose();
```

### Example 9: Using with Nethereum.Web3

```csharp
using Nethereum.Web3;
using Nethereum.JsonRpc.WebSocketClient;

// Create WebSocket client
var wsClient = new WebSocketClient("ws://localhost:8546");

// Use with Web3
var web3 = new Web3(wsClient);

// Standard Web3 operations over WebSocket
var balance = await web3.Eth.GetBalance.SendRequestAsync(
    "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb"
);

var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

Console.WriteLine($"Balance: {Web3.Convert.FromWei(balance)} ETH");
Console.WriteLine($"Block: {blockNumber.Value}");

// Cleanup
wsClient.Dispose();
```

## API Reference

### WebSocketClient (Basic)

```csharp
public class WebSocketClient : ClientBase, IDisposable, IClientRequestHeaderSupport
{
    public WebSocketClient(string path,
        JsonSerializerSettings jsonSerializerSettings = null,
        ILogger log = null)

    public Dictionary<string, string> RequestHeaders { get; set; }
    public TimeSpan ConnectionTimeout { get; set; }

    public Task StopAsync()
    public Task StopAsync(WebSocketCloseStatus webSocketCloseStatus, string status, CancellationToken timeOutToken)
}
```

### StreamingWebSocketClient (Subscriptions)

```csharp
public class StreamingWebSocketClient : IStreamingClient, IDisposable, IClientRequestHeaderSupport
{
    public StreamingWebSocketClient(string path,
        JsonSerializerSettings jsonSerializerSettings = null,
        ILogger log = null)

    public Dictionary<string, string> RequestHeaders { get; set; }
    public static TimeSpan ConnectionTimeout { get; set; }
    public WebSocketState WebSocketState { get; }
    public bool IsStarted { get; }

    public event WebSocketStreamingErrorEventHandler Error;

    public Task StartAsync()
    public Task StopAsync()
    public bool AddSubscription(string subscriptionId, IRpcStreamingResponseHandler handler)
    public bool RemoveSubscription(string subscriptionId)
}
```

### Available Subscriptions

| Subscription | Description |
|--------------|-------------|
| **EthNewBlockHeadersSubscription** | New block headers |
| **EthNewPendingTransactionSubscription** | Pending transactions (mempool) |
| **EthLogsSubscription** | Contract event logs |
| **EthSyncingSubscription** | Node sync status |

## Important Notes

### WebSocket Endpoints

**Common WebSocket URLs:**

| Node/Provider | WebSocket URL |
|---------------|---------------|
| **Geth (local)** | `ws://localhost:8546` |
| **Erigon (local)** | `ws://localhost:8545` |
| **Infura** | `wss://mainnet.infura.io/ws/v3/PROJECT_ID` |
| **Alchemy** | `wss://eth-mainnet.g.alchemy.com/v2/API_KEY` |
| **QuickNode** | `wss://your-endpoint.quiknode.pro/TOKEN/` |

### Starting Geth/Erigon with WebSocket

**Geth:**
```bash
geth --ws --ws.addr 0.0.0.0 --ws.port 8546 --ws.api eth,net,web3
```

**Erigon:**
```bash
erigon --ws --ws.port 8545
```

### Performance Considerations

| Subscription | Event Rate | Notes |
|--------------|------------|-------|
| **newHeads** | ~12s (mainnet) | One per block |
| **pendingTransactions** | 100-1000/s | Very high volume |
| **logs (filtered)** | Variable | Depends on filter |
| **syncing** | Rare | Only during sync |

**Tips:**
- Use `Buffer()` or throttling for high-volume subscriptions
- Filter logs as narrowly as possible (specific addresses/topics)
- Consider multiple clients for heavy workloads

### Thread Safety

- **StreamingWebSocketClient** is thread-safe for subscriptions
- Multiple subscriptions can run concurrently
- Each subscription has isolated message handling

### Connection Management

- WebSocket connections can drop - implement error handling
- Use the `Error` event to detect connection issues
- Implement reconnection logic for production apps
- Always call `Dispose()` to properly close connections

### Subscription Limits

Some providers limit concurrent subscriptions:
- **Infura**: Up to 5 subscriptions per connection
- **Alchemy**: Up to 10 subscriptions per connection
- **Local nodes**: Usually unlimited

## Related Packages

### Alternative Transports
- **Nethereum.JsonRpc.RpcClient** - HTTP/HTTPS transport
- **Nethereum.JsonRpc.IpcClient** - IPC transport
- **Nethereum.JsonRpc.SystemTextJsonRpcClient** - HTTP with System.Text.Json

### Core Dependencies
- **Nethereum.JsonRpc.Client** - Abstraction layer

### Higher-Level APIs
- **Nethereum.Web3** - Complete Web3 API
- **Nethereum.RPC.Reactive** - Reactive Extensions for subscriptions

## Additional Resources

- [Ethereum WebSocket Subscriptions](https://geth.ethereum.org/docs/rpc/pubsub)
- [Reactive Extensions (Rx.NET)](https://github.com/dotnet/reactive)
- [WebSocket Protocol](https://datatracker.ietf.org/doc/html/rfc6455)
- [Nethereum Documentation](http://docs.nethereum.com/)
- [Nethereum Reactive Documentation](http://docs.nethereum.com/en/latest/nethereum-subscriptions-streaming/)

## License

This package is part of the Nethereum project and follows the same MIT license.
