# Nethereum.JsonRpc.IpcClient

High-performance IPC (Inter-Process Communication) JSON-RPC client for local Ethereum node communication.

## Overview

Nethereum.JsonRpc.IpcClient provides **IPC transport implementations** for communicating with local Ethereum nodes via **Named Pipes (Windows)** and **Unix Domain Sockets (Linux/macOS)**. IPC offers **significantly lower latency** than HTTP for local node communication, making it ideal for high-performance applications running on the same machine as the Ethereum node.

**Key Features:**
- **Named Pipes** support (Windows)
- **Unix Domain Sockets** support (Linux, macOS)
- **Ultra-low latency** (~1ms vs ~5ms HTTP)
- Automatic connection management and retry
- Thread-safe request handling
- Production-tested reliability
- Compatible with Geth, Erigon, Besu IPC endpoints

**Use Cases:**
- Local node communication (same machine)
- High-frequency trading / MEV bots
- Low-latency blockchain indexers
- Real-time event processing
- Production node operators
- Development and testing with local nodes

## Installation

```bash
dotnet add package Nethereum.JsonRpc.IpcClient
```

**Platform Support:**
- **Windows**: Named Pipes (`IpcClient`)
- **Linux/macOS**: Unix Domain Sockets (`UnixIpcClient`)

## Dependencies

**Nethereum:**
- **Nethereum.JsonRpc.Client** - Core RPC abstraction (provides JSON serialization and logging support)

**External:**
- **System.IO.Pipes** (v4.3.0) - Named pipes support

## Quick Start

### Windows (Named Pipes)

```csharp
using Nethereum.JsonRpc.IpcClient;
using Nethereum.RPC.Eth;

// Connect to Geth IPC endpoint (Windows)
var client = new IpcClient(@"\\.\pipe\geth.ipc");

// Query blockchain with ultra-low latency
var ethBlockNumber = new EthBlockNumber(client);
var blockNumber = await ethBlockNumber.SendRequestAsync();

Console.WriteLine($"Current block: {blockNumber.Value}");
// Typical latency: ~1ms (vs ~5ms for HTTP localhost)
```

### Linux/macOS (Unix Domain Sockets)

```csharp
using Nethereum.JsonRpc.IpcClient;
using Nethereum.RPC.Eth;

// Connect to Geth IPC endpoint (Linux/macOS)
var client = new UnixIpcClient("/home/user/.ethereum/geth.ipc");

// Query blockchain
var ethChainId = new EthChainId(client);
var chainId = await ethChainId.SendRequestAsync();

Console.WriteLine($"Chain ID: {chainId.Value}");
```

## Usage Examples

### Example 1: Connecting to Geth IPC Endpoints

```csharp
using Nethereum.JsonRpc.IpcClient;
using Nethereum.RPC.Eth;
using System.Runtime.InteropServices;

// Platform-specific IPC path detection
IClient client;

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    // Windows: Named pipe
    client = new IpcClient(@"\\.\pipe\geth.ipc");
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    // Linux: Unix socket
    client = new UnixIpcClient("/home/user/.ethereum/geth.ipc");
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    // macOS: Unix socket
    client = new UnixIpcClient("/Users/user/Library/Ethereum/geth.ipc");
}
else
{
    throw new PlatformNotSupportedException();
}

// Use with RPC services
var ethBlockNumber = new EthBlockNumber(client);
var blockNumber = await ethBlockNumber.SendRequestAsync();

Console.WriteLine($"Block: {blockNumber.Value}");
```

### Example 2: Custom Connection Timeout

```csharp
using Nethereum.JsonRpc.IpcClient;
using Nethereum.RPC.Eth;

var client = new IpcClient(@"\\.\pipe\geth.ipc");

// Default timeout is 120 seconds
Console.WriteLine($"Default timeout: {client.ConnectionTimeout.TotalSeconds}s");

// Set custom timeout
client.ConnectionTimeout = TimeSpan.FromSeconds(10);

try
{
    var ethAccounts = new EthAccounts(client);
    var accounts = await ethAccounts.SendRequestAsync();
    Console.WriteLine($"Accounts: {string.Join(", ", accounts)}");
}
catch (RpcClientTimeoutException ex)
{
    Console.WriteLine($"IPC connection timed out: {ex.Message}");
}
```

### Example 3: Logging with Microsoft.Extensions.Logging

```csharp
using Nethereum.JsonRpc.IpcClient;
using Microsoft.Extensions.Logging;
using Nethereum.RPC.Eth;

// Create logger
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

var logger = loggerFactory.CreateLogger<IpcClient>();

// Create client with logging
var client = new UnixIpcClient(
    "/home/user/.ethereum/geth.ipc",
    jsonSerializerSettings: null,
    log: logger
);

// All requests are logged
var ethGasPrice = new EthGasPrice(client);
var gasPrice = await ethGasPrice.SendRequestAsync();
// Console output: Sending request: {"jsonrpc":"2.0","method":"eth_gasPrice","params":[],"id":1}
// Console output: Received response: {"jsonrpc":"2.0","result":"0x...","id":1}
```

### Example 4: Using with Nethereum.Web3

```csharp
using Nethereum.Web3;
using Nethereum.JsonRpc.IpcClient;
using System.Runtime.InteropServices;

// Create IPC client
IClient ipcClient = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ? new IpcClient(@"\\.\pipe\geth.ipc")
    : new UnixIpcClient("/home/user/.ethereum/geth.ipc") as IClient;

// Use with Web3
var web3 = new Web3(ipcClient);

// Ultra-fast local queries
var balance = await web3.Eth.GetBalance.SendRequestAsync(
    "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb"
);

var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

Console.WriteLine($"Balance: {Web3.Convert.FromWei(balance)} ETH");
Console.WriteLine($"Block: {blockNumber.Value}");
```

### Example 5: High-Frequency Request Pattern (MEV Bot)

```csharp
using Nethereum.JsonRpc.IpcClient;
using Nethereum.RPC.Eth;
using System.Diagnostics;

var client = new UnixIpcClient("/home/user/.ethereum/geth.ipc");
client.ConnectionTimeout = TimeSpan.FromSeconds(5);

// High-frequency block monitoring with minimal latency
var ethBlockNumber = new EthBlockNumber(client);

var lastBlock = BigInteger.Zero;
while (true)
{
    var sw = Stopwatch.StartNew();
    var currentBlock = await ethBlockNumber.SendRequestAsync();
    sw.Stop();

    if (currentBlock.Value > lastBlock)
    {
        Console.WriteLine($"New block {currentBlock.Value} detected in {sw.ElapsedMilliseconds}ms");
        lastBlock = currentBlock.Value;

        // Execute time-sensitive logic here (MEV, arbitrage, etc.)
    }

    await Task.Delay(100); // Poll every 100ms
}
// Typical latency: 1-2ms (IPC) vs 5-10ms (HTTP localhost)
```

### Example 6: Connection Error Handling and Retry

```csharp
using Nethereum.JsonRpc.IpcClient;
using Nethereum.RPC.Eth;
using Polly;

var client = new IpcClient(@"\\.\pipe\geth.ipc");
client.ConnectionTimeout = TimeSpan.FromSeconds(10);

// Define retry policy for IPC connection failures
var retryPolicy = Policy
    .Handle<RpcClientTimeoutException>()
    .Or<RpcClientUnknownException>()
    .Or<IOException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (exception, timeSpan, retryCount, context) =>
        {
            Console.WriteLine($"IPC retry {retryCount} after {timeSpan.TotalSeconds}s: {exception.Message}");
        }
    );

try
{
    var blockNumber = await retryPolicy.ExecuteAsync(async () =>
    {
        var ethBlockNumber = new EthBlockNumber(client);
        return await ethBlockNumber.SendRequestAsync();
    });

    Console.WriteLine($"Success! Block: {blockNumber.Value}");
}
catch (RpcClientTimeoutException ex)
{
    Console.WriteLine($"IPC timeout after retries: {ex.Message}");
    Console.WriteLine("Is Geth running? Check IPC path.");
}
catch (RpcClientUnknownException ex)
{
    Console.WriteLine($"IPC connection error: {ex.Message}");
    Console.WriteLine($"IPC path: {client.IpcPath}");
}
```

### Example 7: Erigon IPC Connection

```csharp
using Nethereum.JsonRpc.IpcClient;
using Nethereum.RPC.Eth;

// Erigon default IPC paths
var client = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ? new IpcClient(@"\\.\pipe\erigon.ipc")
    : new UnixIpcClient("/home/user/.local/share/erigon/erigon.ipc") as IClient;

// Erigon-specific RPC methods work over IPC
var ethBlockNumber = new EthBlockNumber(client);
var blockNumber = await ethBlockNumber.SendRequestAsync();

Console.WriteLine($"Erigon block: {blockNumber.Value}");
```

### Example 8: Custom JsonSerializerSettings

```csharp
using Nethereum.JsonRpc.IpcClient;
using Newtonsoft.Json;
using Nethereum.RPC.Eth;

// Create custom serializer settings
var settings = new JsonSerializerSettings
{
    NullValueHandling = NullValueHandling.Ignore,
    Formatting = Formatting.None,
    DateTimeZoneHandling = DateTimeZoneHandling.Utc
};

// Create client with custom settings
var client = new IpcClient(
    @"\\.\pipe\geth.ipc",
    jsonSerializerSettings: settings
);

var ethChainId = new EthChainId(client);
var chainId = await ethChainId.SendRequestAsync();

Console.WriteLine($"Chain ID: {chainId.Value}");
```

### Example 9: Proper Disposal Pattern

```csharp
using Nethereum.JsonRpc.IpcClient;
using Nethereum.RPC.Eth;

// IpcClient implements IDisposable - always dispose properly
using (var client = new UnixIpcClient("/home/user/.ethereum/geth.ipc"))
{
    var ethBlockNumber = new EthBlockNumber(client);
    var blockNumber = await ethBlockNumber.SendRequestAsync();

    Console.WriteLine($"Block: {blockNumber.Value}");

    // Client automatically disposed and connection closed
}

// For long-running applications, reuse the client
var persistentClient = new IpcClient(@"\\.\pipe\geth.ipc");
try
{
    // Use throughout application lifetime
    while (true)
    {
        var ethBlockNumber = new EthBlockNumber(persistentClient);
        var block = await ethBlockNumber.SendRequestAsync();
        await Task.Delay(1000);
    }
}
finally
{
    persistentClient.Dispose();
}
```

## API Reference

### IpcClient (Windows - Named Pipes)

```csharp
public class IpcClient : IpcClientBase, IDisposable
{
    public IpcClient(string ipcPath,
        JsonSerializerSettings jsonSerializerSettings = null,
        ILogger log = null)
}
```

**Parameters:**
- `ipcPath`: Named pipe path (e.g., `\\.\pipe\geth.ipc`)
- `jsonSerializerSettings`: Optional custom JSON settings
- `log`: Optional logger instance

### UnixIpcClient (Linux/macOS - Unix Domain Sockets)

```csharp
public class UnixIpcClient : IpcClientBase, IDisposable
{
    public UnixIpcClient(string ipcPath,
        JsonSerializerSettings jsonSerializerSettings = null,
        ILogger log = null)
}
```

**Parameters:**
- `ipcPath`: Unix socket path (e.g., `/home/user/.ethereum/geth.ipc`)
- `jsonSerializerSettings`: Optional custom JSON settings
- `log`: Optional logger instance

### Properties

```csharp
public TimeSpan ConnectionTimeout { get; set; } // Default: 120 seconds
public string IpcPath { get; }
public int ForceCompleteReadTotalMiliseconds { get; set; } // Default: 2000
```

### Key Methods (Inherited from ClientBase)

```csharp
public override Task<RpcResponseMessage> SendAsync(RpcRequestMessage request, string route = null)
public void Dispose()
```

## Important Notes

### Common IPC Paths

**Geth:**
| Platform | Default IPC Path |
|----------|------------------|
| **Windows** | `\\.\pipe\geth.ipc` |
| **Linux** | `/home/user/.ethereum/geth.ipc` |
| **macOS** | `/Users/user/Library/Ethereum/geth.ipc` |

**Erigon:**
| Platform | Default IPC Path |
|----------|------------------|
| **Windows** | `\\.\pipe\erigon.ipc` |
| **Linux** | `/home/user/.local/share/erigon/erigon.ipc` |
| **macOS** | `/Users/user/Library/Erigon/erigon.ipc` |

**Besu:**
| Platform | Default IPC Path |
|----------|------------------|
| **Windows** | Not officially supported |
| **Linux** | `/tmp/besu.ipc` |
| **macOS** | `/tmp/besu.ipc` |

### Performance Comparison

| Transport | Latency (localhost) | Use Case |
|-----------|---------------------|----------|
| **IPC** | 0.5-2ms | Local node, high-frequency |
| HTTP | 3-10ms | Local node, standard |
| HTTPS (remote) | 50-200ms | Cloud providers |

**IPC is ~5x faster than HTTP for local communication.**

### Thread Safety

- **NOT thread-safe** - uses internal locking for single connection
- For concurrent requests, create multiple client instances
- Each instance maintains its own IPC connection
- Safe to use from single thread or with external synchronization

### Batch Requests

IPC clients support batch requests via inherited `SendBatchRequestAsync`:

```csharp
var batch = new RpcRequestResponseBatch();
batch.BatchItems.Add(new RpcRequestResponseBatchItem<string>(
    new RpcRequestMessage(1, "eth_blockNumber")
));
batch.BatchItems.Add(new RpcRequestResponseBatchItem<string>(
    new RpcRequestMessage(2, "eth_chainId")
));

var result = await client.SendBatchRequestAsync(batch);
```

However, **IPC is already very fast** - batching provides less benefit than with HTTP.

### Error Handling

| Exception | Cause | Solution |
|-----------|-------|----------|
| **RpcClientTimeoutException** | Connection timeout | Check node is running, verify IPC path |
| **RpcClientUnknownException** | IPC communication error | Verify IPC path, check permissions |
| **IOException** | Pipe/socket error | Restart node, check file system |

### Limitations

- **Single connection per client** - use multiple instances for concurrency
- **No subscription support** - use WebSocketClient for `eth_subscribe`
- **Local only** - IPC cannot communicate with remote nodes
- **Platform-specific** - Named Pipes (Windows) vs Unix Sockets (Linux/macOS)

### When to Use IPC vs HTTP vs WebSocket

**Use IPC when:**
- Running on same machine as node
- Ultra-low latency required (<2ms)
- High-frequency requests (MEV, indexing)
- Production node operator

**Use HTTP when:**
- Connecting to remote node
- Simple request/response pattern
- Standard latency acceptable (5-10ms)

**Use WebSocket when:**
- Need real-time subscriptions (`eth_subscribe`)
- Event streaming required
- Push notifications from node

## Related Packages

### Alternative Transports
- **Nethereum.JsonRpc.RpcClient** - HTTP/HTTPS transport
- **Nethereum.JsonRpc.WebSocketClient** - WebSocket transport (subscriptions)
- **Nethereum.JsonRpc.SystemTextJsonRpcClient** - HTTP with System.Text.Json

### Core Dependencies
- **Nethereum.JsonRpc.Client** - Abstraction layer

### Higher-Level APIs
- **Nethereum.Web3** - Complete Web3 API

## Starting Geth/Erigon with IPC

### Geth

```bash
# Linux/macOS
geth --ipcpath /home/user/.ethereum/geth.ipc

# Windows
geth --ipcpath \\.\pipe\geth.ipc

# Default IPC is enabled automatically
geth --http --http.api eth,net,web3
```

### Erigon

```bash
# Linux
erigon --private.api.addr /home/user/.local/share/erigon/erigon.ipc

# Default IPC is enabled
erigon
```

## Additional Resources

- [Geth IPC Documentation](https://geth.ethereum.org/docs/rpc/ipc)
- [Named Pipes Documentation](https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipes)
- [Unix Domain Sockets](https://en.wikipedia.org/wiki/Unix_domain_socket)
- [Nethereum Documentation](http://docs.nethereum.com/)
