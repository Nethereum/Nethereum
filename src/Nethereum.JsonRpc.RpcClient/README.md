# Nethereum.JsonRpc.RpcClient

Production-ready HTTP/HTTPS JSON-RPC client for Ethereum node communication.

## Overview

Nethereum.JsonRpc.RpcClient provides the **standard HTTP/HTTPS transport implementation** for communicating with Ethereum nodes via JSON-RPC. This is the most commonly used RPC client in Nethereum, offering robust connection management, automatic retries, authentication support, and production-tested reliability.

**Key Features:**
- HTTP/HTTPS transport with HttpClient
- Connection pooling and lifecycle management
- Basic authentication support (username/password)
- Configurable timeouts
- Automatic HttpClient rotation (for older .NET versions)
- Thread-safe concurrent requests
- Production-tested reliability
- Support for custom HttpClientHandler

**Use Cases:**
- Connecting to Ethereum nodes (Geth, Erigon, Besu, Nethermind)
- Querying blockchain data
- Sending transactions
- Contract interactions
- Load balancer/proxy integration
- Production dApp backends

## Installation

```bash
dotnet add package Nethereum.JsonRpc.RpcClient
```

This is typically the **default RPC client** used by Nethereum.Web3:

```bash
dotnet add package Nethereum.Web3
```

## Dependencies

**Nethereum:**
- **Nethereum.JsonRpc.Client** - Core RPC abstraction (which provides JSON serialization support via Newtonsoft.Json or System.Text.Json)

**Framework:**
- **System.Net.Http** - HTTP/HTTPS communication (built-in .NET library)

## Quick Start

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;

// Connect to local node
var client = new RpcClient(new Uri("http://localhost:8545"));

// Use with RPC services
var ethBlockNumber = new EthBlockNumber(client);
var blockNumber = await ethBlockNumber.SendRequestAsync();

Console.WriteLine($"Current block: {blockNumber.Value}");
```

## Usage Examples

### Example 1: Basic Connection to Ethereum Node

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.Hex.HexTypes;

// Local Geth/Erigon/Besu node
var client = new RpcClient(new Uri("http://localhost:8545"));

// Infura
var infuraClient = new RpcClient(
    new Uri("https://mainnet.infura.io/v3/YOUR_PROJECT_ID")
);

// Alchemy
var alchemyClient = new RpcClient(
    new Uri("https://eth-mainnet.g.alchemy.com/v2/YOUR_API_KEY")
);

// Use with any RPC service
var ethChainId = new EthChainId(client);
HexBigInteger chainId = await ethChainId.SendRequestAsync();

Console.WriteLine($"Chain ID: {chainId.Value}");
// Output: Chain ID: 1 (mainnet)
```

### Example 2: HTTP Basic Authentication

```csharp
using Nethereum.JsonRpc.Client;
using System.Net.Http.Headers;
using System.Text;

// Option 1: URL-based authentication (simplest)
var client = new RpcClient(
    new Uri("http://username:password@localhost:8545")
);

// Option 2: Explicit AuthenticationHeaderValue
var credentials = Convert.ToBase64String(
    Encoding.UTF8.GetBytes("admin:secretpassword")
);
var authHeader = new AuthenticationHeaderValue("Basic", credentials);

var authenticatedClient = new RpcClient(
    new Uri("http://localhost:8545"),
    authHeaderValue: authHeader
);

// Use authenticated client
var ethAccounts = new EthAccounts(authenticatedClient);
var accounts = await ethAccounts.SendRequestAsync();

Console.WriteLine($"Accounts: {string.Join(", ", accounts)}");
```

### Example 3: Custom Connection Timeout

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;

var client = new RpcClient(new Uri("http://localhost:8545"));

// Default timeout is 120 seconds (2 minutes)
Console.WriteLine($"Default timeout: {client.ConnectionTimeout.TotalSeconds}s");

// Set custom timeout for slow networks
client.ConnectionTimeout = TimeSpan.FromSeconds(30);

try
{
    var ethGasPrice = new EthGasPrice(client);
    var gasPrice = await ethGasPrice.SendRequestAsync();
    Console.WriteLine($"Gas price: {gasPrice.Value} wei");
}
catch (RpcClientTimeoutException ex)
{
    Console.WriteLine($"Request timed out after 30 seconds: {ex.Message}");
}
```

### Example 4: Using Custom HttpClient for Advanced Configuration

```csharp
using Nethereum.JsonRpc.Client;
using System.Net.Http;

// Create custom HttpClient with specific settings
var httpClient = new HttpClient(new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10),
    MaxConnectionsPerServer = 50,
    EnableMultipleHttp2Connections = true
})
{
    Timeout = TimeSpan.FromSeconds(60)
};

// Create RpcClient with custom HttpClient
var client = new RpcClient(
    new Uri("http://localhost:8545"),
    httpClient: httpClient
);

var ethBlockNumber = new EthBlockNumber(client);
var blockNumber = await ethBlockNumber.SendRequestAsync();

Console.WriteLine($"Block: {blockNumber.Value}");
```

### Example 5: Custom HttpClientHandler for Proxy Support

```csharp
using Nethereum.JsonRpc.Client;
using System.Net;
using System.Net.Http;

// Configure proxy
var handler = new HttpClientHandler
{
    Proxy = new WebProxy("http://proxy.example.com:8080")
    {
        Credentials = new NetworkCredential("proxyuser", "proxypass")
    },
    UseProxy = true,
    MaxConnectionsPerServer = 20
};

// Create client with proxy handler
var client = new RpcClient(
    new Uri("http://localhost:8545"),
    httpClientHandler: handler
);

var ethChainId = new EthChainId(client);
var chainId = await ethChainId.SendRequestAsync();

Console.WriteLine($"Chain ID (via proxy): {chainId.Value}");
```

### Example 6: Multiple Concurrent Requests (Thread Safety)

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;

var client = new RpcClient(new Uri("http://localhost:8545"));

// RpcClient is thread-safe - can handle concurrent requests
var tasks = new List<Task>
{
    Task.Run(async () =>
    {
        var ethBlockNumber = new EthBlockNumber(client);
        var block = await ethBlockNumber.SendRequestAsync();
        Console.WriteLine($"Task 1 - Block: {block.Value}");
    }),

    Task.Run(async () =>
    {
        var ethGasPrice = new EthGasPrice(client);
        var gasPrice = await ethGasPrice.SendRequestAsync();
        Console.WriteLine($"Task 2 - Gas Price: {gasPrice.Value}");
    }),

    Task.Run(async () =>
    {
        var ethChainId = new EthChainId(client);
        var chainId = await ethChainId.SendRequestAsync();
        Console.WriteLine($"Task 3 - Chain ID: {chainId.Value}");
    })
};

await Task.WhenAll(tasks);
Console.WriteLine("All requests completed successfully");
```

### Example 7: Error Handling and Retry Logic

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Polly;

var client = new RpcClient(new Uri("http://localhost:8545"));
client.ConnectionTimeout = TimeSpan.FromSeconds(10);

// Define retry policy with Polly
var retryPolicy = Policy
    .Handle<RpcClientTimeoutException>()
    .Or<RpcClientUnknownException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (exception, timeSpan, retryCount, context) =>
        {
            Console.WriteLine($"Retry {retryCount} after {timeSpan.TotalSeconds}s due to: {exception.Message}");
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
catch (RpcResponseException ex)
{
    Console.WriteLine($"RPC Error {ex.RpcError.Code}: {ex.RpcError.Message}");
}
catch (RpcClientTimeoutException ex)
{
    Console.WriteLine($"Timeout after retries: {ex.Message}");
}
catch (RpcClientUnknownException ex)
{
    Console.WriteLine($"Network error: {ex.Message}");
}
```

### Example 8: Load Balancing Across Multiple Nodes

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;

public class LoadBalancedRpcClient
{
    private readonly List<RpcClient> _clients;
    private int _currentIndex = 0;
    private readonly object _lock = new object();

    public LoadBalancedRpcClient(params string[] nodeUrls)
    {
        _clients = nodeUrls.Select(url => new RpcClient(new Uri(url))).ToList();
    }

    public RpcClient GetNextClient()
    {
        lock (_lock)
        {
            var client = _clients[_currentIndex];
            _currentIndex = (_currentIndex + 1) % _clients.Count;
            return client;
        }
    }
}

// Usage
var loadBalancer = new LoadBalancedRpcClient(
    "http://node1.example.com:8545",
    "http://node2.example.com:8545",
    "http://node3.example.com:8545"
);

// Round-robin requests
for (int i = 0; i < 10; i++)
{
    var client = loadBalancer.GetNextClient();
    var ethBlockNumber = new EthBlockNumber(client);
    var block = await ethBlockNumber.SendRequestAsync();
    Console.WriteLine($"Request {i + 1} - Block: {block.Value}");
}
```

### Example 9: Using with Nethereum.Web3

```csharp
using Nethereum.Web3;
using Nethereum.JsonRpc.Client;

// Option 1: Web3 creates RpcClient internally (simplest)
var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_PROJECT_ID");

// Option 2: Create custom RpcClient first
var client = new RpcClient(new Uri("http://localhost:8545"));
client.ConnectionTimeout = TimeSpan.FromSeconds(60);

var web3WithCustomClient = new Web3(client);

// Use Web3 normally
var balance = await web3WithCustomClient.Eth.GetBalance.SendRequestAsync("0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb");
Console.WriteLine($"Balance: {Web3.Convert.FromWei(balance)} ETH");

var blockNumber = await web3WithCustomClient.Eth.Blocks.GetBlockNumber.SendRequestAsync();
Console.WriteLine($"Block: {blockNumber.Value}");
```

## API Reference

### RpcClient Constructor Overloads

```csharp
// Basic constructor
public RpcClient(Uri baseUrl,
    AuthenticationHeaderValue authHeaderValue = null,
    JsonSerializerSettings jsonSerializerSettings = null,
    HttpClientHandler httpClientHandler = null,
    ILogger log = null)

// With custom HttpClient
public RpcClient(Uri baseUrl,
    HttpClient httpClient,
    AuthenticationHeaderValue authHeaderValue = null,
    JsonSerializerSettings jsonSerializerSettings = null,
    ILogger log = null)
```

### Properties

```csharp
public static int MaximumConnectionsPerServer { get; set; } = 20;
public TimeSpan ConnectionTimeout { get; set; } // Default: 120 seconds
public RequestInterceptor OverridingRequestInterceptor { get; set; }
```

### Key Methods (Inherited from ClientBase)

```csharp
Task<T> SendRequestAsync<T>(RpcRequest request, string route = null);
Task<T> SendRequestAsync<T>(string method, string route = null, params object[] paramList);
Task<RpcRequestResponseBatch> SendBatchRequestAsync(RpcRequestResponseBatch batch);
Task<RpcResponseMessage> SendAsync(RpcRequestMessage request, string route = null);
```

## Important Notes

### Connection Management

**HttpClient Rotation:**
- On older .NET Framework, RpcClient automatically rotates HttpClient instances every 60 seconds
- On .NET Core 2.1+, uses SocketsHttpHandler with connection pooling
- Connection lifetime: 10 minutes
- Idle timeout: 5 minutes
- Max connections per server: 20 (configurable via `MaximumConnectionsPerServer`)

**Best Practices:**
- Reuse RpcClient instances (don't create per request)
- Set appropriate timeouts based on network conditions
- Use connection pooling for high-traffic applications

### Thread Safety

- **Thread-safe** after initialization
- Safe to call from multiple threads concurrently
- Connection pooling handles concurrent requests efficiently
- Lock-free for read operations

### Performance

| Operation | Latency | Notes |
|-----------|---------|-------|
| **Local node** | 1-10ms | Localhost Geth/Erigon |
| **Cloud provider** | 50-200ms | Infura, Alchemy, QuickNode |
| **Slow network** | 200-500ms | High latency regions |

**Optimization Tips:**
- Enable HTTP/2 with `EnableMultipleHttp2Connections`
- Use batch requests for multiple calls
- Tune `MaxConnectionsPerServer` for high throughput
- Consider WebSocket client for subscriptions

### Error Handling

| Exception | Cause | Retry? |
|-----------|-------|--------|
| **RpcClientTimeoutException** | Request exceeded ConnectionTimeout | Yes (with backoff) |
| **RpcClientUnknownException** | Network/HTTP errors | Yes (transient) |
| **RpcResponseException** | JSON-RPC error from node | Depends on error code |
| **HttpRequestException** | DNS, connection failures | Yes (with backoff) |

### Authentication

Supports **HTTP Basic Authentication**:
- URL-based: `http://user:pass@localhost:8545`
- Header-based: `AuthenticationHeaderValue("Basic", base64Credentials)`
- Automatically extracted from URI if present

### JSON Serialization

Uses **Newtonsoft.Json** with default settings optimized for Ethereum:
- Proper handling of `0x` hex prefixes
- BigInteger serialization
- Block/transaction DTOs

For **System.Text.Json**, use `Nethereum.JsonRpc.SystemTextJsonRpcClient` instead.

### Logging

Supports **Microsoft.Extensions.Logging**:

```csharp
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<RpcClient>();

var client = new RpcClient(
    new Uri("http://localhost:8545"),
    log: logger
);
```

Logs include:
- Request JSON payloads
- Response JSON payloads
- Exception details
- Performance metrics

## Related Packages

### Alternative Transports
- **Nethereum.JsonRpc.SystemTextJsonRpcClient** - HTTP with System.Text.Json
- **Nethereum.JsonRpc.WebSocketClient** - WebSocket transport
- **Nethereum.JsonRpc.IpcClient** - IPC transport (Unix sockets, named pipes)

### Higher-Level APIs
- **Nethereum.Web3** - Complete Web3 API (uses RpcClient internally)
- **Nethereum.RPC** - Typed RPC services

### Core Dependencies
- **Nethereum.JsonRpc.Client** - Abstraction layer

## Common Ethereum Node Providers

| Provider | URL Format | Notes |
|----------|------------|-------|
| **Infura** | `https://mainnet.infura.io/v3/PROJECT_ID` | Free tier available |
| **Alchemy** | `https://eth-mainnet.g.alchemy.com/v2/API_KEY` | Enhanced APIs |
| **QuickNode** | `https://your-endpoint.quiknode.pro/token/` | Global network |
| **Local Geth** | `http://localhost:8545` | Full node |
| **Local Erigon** | `http://localhost:8545` | Archive node |

## Additional Resources

- [Ethereum JSON-RPC Specification](https://ethereum.org/en/developers/docs/apis/json-rpc/)
- [Nethereum Documentation](http://docs.nethereum.com/)
- [HttpClient Best Practices](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines)
- [SocketsHttpHandler Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler)
