# Nethereum.JsonRpc.Client

Core JSON-RPC abstraction layer for Ethereum node communication.

## Overview

Nethereum.JsonRpc.Client provides the **fundamental abstraction layer** for all JSON-RPC communication with Ethereum nodes. It defines the core interfaces and base classes that all RPC client implementations must implement, enabling pluggable transport mechanisms (HTTP, WebSocket, IPC) while maintaining consistent error handling, request interception, and batch processing.

**Key Features:**
- Transport-agnostic RPC abstraction (HTTP, WebSocket, IPC)
- Request/response message handling
- Batch request support
- Request interception for logging/monitoring
- Consistent error handling across transports
- Basic authentication support
- Streaming/subscription support

**Use Cases:**
- Building custom RPC client implementations
- Implementing request logging and monitoring
- Creating custom transport mechanisms
- Testing and mocking RPC communication
- Building middleware for RPC calls

## Installation

```bash
dotnet add package Nethereum.JsonRpc.Client
```

**Note:** This is typically used as a dependency by concrete client implementations. Most users will use:
- **Nethereum.JsonRpc.RpcClient** - HTTP/HTTPS client
- **Nethereum.JsonRpc.WebSocketClient** - WebSocket client
- **Nethereum.JsonRpc.IpcClient** - IPC client

## Dependencies

**Nethereum:**
- **Nethereum.Hex** - Hex encoding/decoding utilities

**External:**
- **Microsoft.Extensions.Logging.Abstractions** (v6.0.0+) - Logging support (conditional dependency for modern frameworks)

**JSON Serialization:**
- **Newtonsoft.Json** (`[11.0.2,14)`) - real dependency, used for request/response message handling and `RpcError.Data`
- **System.Text.Json** support is provided by the separate `Nethereum.JsonRpc.SystemTextJsonRpcClient` package, not by this one

## Quick Start

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;

// Use concrete implementation (RpcClient)
var client = new RpcClient(new Uri("http://localhost:8545"));

// Use through higher-level RPC services
var ethBlockNumber = new EthBlockNumber(client);
var blockNumber = await ethBlockNumber.SendRequestAsync();

Console.WriteLine($"Current block: {blockNumber.Value}");
```

## Core Interfaces

### IClient

Main interface for JSON-RPC communication:

```csharp
public interface IClient : IBaseClient
{
    // Send single request
    Task<T> SendRequestAsync<T>(RpcRequest request, string route = null);
    Task<T> SendRequestAsync<T>(string method, string route = null, params object[] paramList);

    // Send batch request
    Task<RpcRequestResponseBatch> SendBatchRequestAsync(RpcRequestResponseBatch rpcRequestResponseBatch);

    // Low-level message sending
    Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string route = null);
}
```

### IBaseClient

Base interface with common properties (`IBaseClient.cs:6-15`). Note `ConnectionTimeout` is **not** part of this interface - it lives on the static `ClientBase.ConnectionTimeout` property instead:

```csharp
public interface IBaseClient
{
    RequestInterceptor OverridingRequestInterceptor { get; set; }
    T DecodeResult<T>(RpcResponseMessage rpcResponseMessage);

    Task SendRequestAsync(RpcRequest request, string route = null);
    Task SendRequestAsync(string method, string route = null, params object[] paramList);
}
```

## Usage Examples

### Example 1: Basic RPC Requests

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.Hex.HexTypes;

// Create HTTP client
var client = new RpcClient(new Uri("http://localhost:8545"));

// Use with RPC services
var ethChainId = new EthChainId(client);
HexBigInteger chainId = await ethChainId.SendRequestAsync();

var ethAccounts = new EthAccounts(client);
string[] accounts = await ethAccounts.SendRequestAsync();

Console.WriteLine($"Chain ID: {chainId.Value}");
Console.WriteLine($"Accounts: {string.Join(", ", accounts)}");
```

### Example 2: Direct Method Calls

```csharp
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

var client = new RpcClient(new Uri("http://localhost:8545"));

// Direct JSON-RPC method call
var blockNumber = await client.SendRequestAsync<string>("eth_blockNumber");
Console.WriteLine($"Block: {blockNumber}");

// With parameters
var balance = await client.SendRequestAsync<string>(
    "eth_getBalance",
    null, // route
    "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb", // address
    "latest" // block parameter
);
Console.WriteLine($"Balance: {balance}");
```

### Example 3: Batch Requests

`RpcRequestResponseBatchItem<TRequestHandler, TResponse>` is constructed from a request handler and an `RpcRequest` (`RpcRequestResponseBatchItem.cs:7-13`), and its decoded result is read from `.Response` - there is no `GetResponse<T>()` method:

```csharp
using Nethereum.JsonRpc.Client;

var client = new RpcClient(new Uri("http://localhost:8545"));

// Create batch request
var batch = new RpcRequestResponseBatch();

// Build one handler per method, then wrap each in a batch item
var blockNumberHandler = new RpcRequestResponseHandlerNoParam<string>(client, "eth_blockNumber");
var chainIdHandler = new RpcRequestResponseHandlerNoParam<string>(client, "eth_chainId");
var gasPriceHandler = new RpcRequestResponseHandlerNoParam<string>(client, "eth_gasPrice");

var blockNumberItem = new RpcRequestResponseBatchItem<RpcRequestResponseHandlerNoParam<string>, string>(
    blockNumberHandler, blockNumberHandler.BuildRequest(1));
var chainIdItem = new RpcRequestResponseBatchItem<RpcRequestResponseHandlerNoParam<string>, string>(
    chainIdHandler, chainIdHandler.BuildRequest(2));
var gasPriceItem = new RpcRequestResponseBatchItem<RpcRequestResponseHandlerNoParam<string>, string>(
    gasPriceHandler, gasPriceHandler.BuildRequest(3));

batch.BatchItems.Add(blockNumberItem);
batch.BatchItems.Add(chainIdItem);
batch.BatchItems.Add(gasPriceItem);

// Send batch
await client.SendBatchRequestAsync(batch);

// Process results - each typed item exposes .Response directly
foreach (var item in new IRpcRequestResponseBatchItem[] { blockNumberItem, chainIdItem, gasPriceItem })
{
    if (item.HasError)
        Console.WriteLine($"Request {item.RpcRequestMessage.Id} failed: {item.RpcError.Message}");
    else
        Console.WriteLine($"Request {item.RpcRequestMessage.Id}: {item.RawResponse}");
}

// Or read the strongly-typed result directly from a known item:
Console.WriteLine($"Block number: {blockNumberItem.Response}");
```

### Example 4: Request Interception for Logging

```csharp
using Nethereum.JsonRpc.Client;
using System.Diagnostics;

// Custom request interceptor
public class LoggingInterceptor : RequestInterceptor
{
    public override async Task<object> InterceptSendRequestAsync<T>(
        Func<RpcRequest, string, Task<T>> interceptedSendRequestAsync,
        RpcRequest request,
        string route = null)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"[REQUEST] {request.Method} - Params: {string.Join(", ", request.RawParameters ?? Array.Empty<object>())}");

        try
        {
            var result = await base.InterceptSendRequestAsync(interceptedSendRequestAsync, request, route);
            sw.Stop();
            Console.WriteLine($"[RESPONSE] {request.Method} - {sw.ElapsedMilliseconds}ms");
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            Console.WriteLine($"[ERROR] {request.Method} - {sw.ElapsedMilliseconds}ms - {ex.Message}");
            throw;
        }
    }
}

// Usage
var client = new RpcClient(new Uri("http://localhost:8545"));
client.OverridingRequestInterceptor = new LoggingInterceptor();

var ethBlockNumber = new EthBlockNumber(client);
var blockNumber = await ethBlockNumber.SendRequestAsync();
// Output: [REQUEST] eth_blockNumber - Params:
// Output: [RESPONSE] eth_blockNumber - 45ms
```

### Example 5: Error Handling

```csharp
using Nethereum.JsonRpc.Client;

var client = new RpcClient(new Uri("http://localhost:8545"));

try
{
    // Invalid method call
    var result = await client.SendRequestAsync<string>("invalid_method");
}
catch (RpcResponseException ex)
{
    // Standard RPC error
    Console.WriteLine($"RPC Error {ex.RpcError.Code}: {ex.RpcError.Message}");
    if (ex.RpcError.Data != null)
    {
        Console.WriteLine($"Error data: {ex.RpcError.Data}");
    }
}
catch (RpcClientTimeoutException ex)
{
    // Request timeout
    Console.WriteLine($"Request timed out: {ex.Message}");
}
catch (RpcClientUnknownException ex)
{
    // Network or other errors
    Console.WriteLine($"Unknown error: {ex.Message}");
    Console.WriteLine($"Inner exception: {ex.InnerException?.Message}");
}
```

### Example 6: Authentication with Basic Auth

```csharp
using Nethereum.JsonRpc.Client;
using System.Net.Http.Headers;
using System.Text;

// Option 1: URL-based authentication
var clientWithAuth = new RpcClient(
    new Uri("http://username:password@localhost:8545")
);

// Option 2: Explicit AuthenticationHeaderValue
var credentials = Convert.ToBase64String(
    Encoding.UTF8.GetBytes("username:password")
);
var authHeader = new AuthenticationHeaderValue("Basic", credentials);

var client = new RpcClient(
    new Uri("http://localhost:8545"),
    authHeaderValue: authHeader
);

var blockNumber = await client.SendRequestAsync<string>("eth_blockNumber");
Console.WriteLine($"Authenticated request successful: {blockNumber}");
```

### Example 7: Custom Connection Timeout

`ConnectionTimeout` is `static` on `ClientBase` (`ClientBase.cs:10`) - it applies process-wide to every client instance, and its default is 20 seconds, not 120:

```csharp
using Nethereum.JsonRpc.Client;

var client = new RpcClient(new Uri("http://localhost:8545"));

// Default timeout is 20 seconds, shared by every ClientBase-derived client in the process
Console.WriteLine($"Default timeout: {ClientBase.ConnectionTimeout.TotalSeconds}s");

// Set custom timeout - this changes it for ALL clients, not just this instance
ClientBase.ConnectionTimeout = TimeSpan.FromSeconds(10);

try
{
    var ethBlockNumber = new EthBlockNumber(client);
    var blockNumber = await ethBlockNumber.SendRequestAsync();
}
catch (RpcClientTimeoutException ex)
{
    Console.WriteLine($"Request timed out after 10 seconds: {ex.Message}");
}
```

### Example 8: Building RPC Requests

```csharp
using Nethereum.JsonRpc.Client;

// Using RpcRequestBuilder
var builder = new RpcRequestBuilder("eth_getBlockByNumber");

// Build request with parameters
var request = builder.BuildRequest(
    id: 1,
    paramList: new object[] { "0x1b4", true }
);

Console.WriteLine($"Method: {request.Method}");
Console.WriteLine($"ID: {request.Id}");
Console.WriteLine($"Params: {string.Join(", ", request.RawParameters)}");

// Send using client
var client = new RpcClient(new Uri("http://localhost:8545"));
var result = await client.SendRequestAsync<object>(request);
```

### Example 9: Partial Batch Success Handling

```csharp
using Nethereum.JsonRpc.Client;

var client = new RpcClient(new Uri("http://localhost:8545"));

var batch = new RpcRequestResponseBatch();

// Add valid and invalid requests
var blockNumberHandler = new RpcRequestResponseHandlerNoParam<string>(client, "eth_blockNumber");
var invalidHandler = new RpcRequestResponseHandlerNoParam<string>(client, "invalid_method"); // This will fail
var chainIdHandler = new RpcRequestResponseHandlerNoParam<string>(client, "eth_chainId");

batch.BatchItems.Add(blockNumberHandler.CreateBatchItem(1));
batch.BatchItems.Add(invalidHandler.CreateBatchItem(2));
batch.BatchItems.Add(chainIdHandler.CreateBatchItem(3));

// Accept partial success
batch.AcceptPartiallySuccessful = true;

var result = await client.SendBatchRequestAsync(batch);

// Process mixed results
int successCount = 0;
int errorCount = 0;

foreach (var item in result.BatchItems)
{
    if (item.HasError)
    {
        errorCount++;
        Console.WriteLine($"Request {item.RpcRequestMessage.Id} failed: {item.RpcError.Message}");
    }
    else
    {
        successCount++;
        Console.WriteLine($"Request {item.RpcRequestMessage.Id} succeeded: {item.RawResponse}");
    }
}

Console.WriteLine($"Success: {successCount}, Errors: {errorCount}");
```

## API Reference

### ClientBase

Abstract base class for client implementations (`ClientBase.cs:8-100`). `ConnectionTimeout` is `static` - one value shared by every client instance in the process, defaulting to 20 seconds. `SendAsync(RpcRequestMessage, string)` is `public abstract` (implemented by each transport); only the batch overload is `protected abstract`:

```csharp
public abstract class ClientBase : IClient
{
    public static TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(20.0);
    public RequestInterceptor OverridingRequestInterceptor { get; set; }

    public abstract Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string route = null);
    protected abstract Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests);

    protected void HandleRpcError(RpcResponseMessage response, string reqMsg);
}
```

### RpcRequest

Represents a JSON-RPC request:

```csharp
public class RpcRequest
{
    public object Id { get; set; }
    public string Method { get; private set; }
    public object[] RawParameters { get; private set; }
}
```

### RpcError

Represents a JSON-RPC error. Properties are set only through the constructor (`RpcError.cs`):

```csharp
public class RpcError
{
    public RpcError(int code, string message, object data = null);

    public int Code { get; private set; }
    public string Message { get; private set; }
    public object Data { get; private set; }

    public string GetDataAsString();
}
```

## Important Notes

### Transport Implementations

This package provides abstractions only. Use concrete implementations:

| Package | Transport | Use Case |
|---------|-----------|----------|
| **Nethereum.JsonRpc.RpcClient** | HTTP/HTTPS | Standard node communication |
| **Nethereum.JsonRpc.WebSocketClient** | WebSocket | Real-time subscriptions |
| **Nethereum.JsonRpc.IpcClient** | IPC | Local node communication |
| **Nethereum.JsonRpc.SystemTextJsonRpcClient** | HTTP (System.Text.Json) | .NET 9 (net9.0 only) with System.Text.Json |

### Error Types

| Exception | Description |
|-----------|-------------|
| **RpcResponseException** | Standard JSON-RPC error response |
| **RpcClientTimeoutException** | Request exceeded ConnectionTimeout |
| **RpcClientUnknownException** | Network or other unexpected errors |

### Batch Requests

- Batch requests improve performance by reducing network round trips
- Partial success requires `AcceptPartiallySuccessful = true`
- Each item in the batch has independent error handling
- Request IDs must be unique within a batch

### Request Interception

Use `RequestInterceptor` for:
- Logging all RPC requests/responses
- Performance monitoring
- Request/response transformation
- Caching layer implementation
- Authentication injection

### Thread Safety

- `IClient` implementations should be thread-safe after initialization
- Connection pooling is managed by concrete implementations (e.g., RpcClient)
- Request interceptors must be thread-safe if used concurrently

### The Streaming Namespace and Custom Client Extension Points

`Nethereum.JsonRpc.Client.Streaming` provides the abstractions that streaming transports (e.g. `Nethereum.JsonRpc.WebSocketClient`) build on:

- **`IStreamingClient`** - `IsStarted`, `AddSubscription`/`RemoveSubscription`, `SendRequestAsync(RpcRequest, IRpcStreamingResponseHandler, string)`, `StartAsync`/`StopAsync`
- **`IRpcStreamingResponseHandler`** - handles an incoming streamed response for a given request/subscription id
- **`IRpcStreamingSubscriptionHandler`**, **`IUnsubscribeSubscriptionRpcRequestBuilder`** - subscription lifecycle contracts
- **`SubscriptionState`**, **`StreamingEventArgs`** - subscription bookkeeping types

To build a **custom RPC client**, derive from `ClientBase` and implement `SendAsync(RpcRequestMessage, string)` and `SendAsync(RpcRequestMessage[])`. For custom typed RPC calls against any `IClient`, use the extension points this package already provides instead of writing a new handler from scratch:

```csharp
// For a method that takes parameters
public class MyCustomMethod : RpcRequestResponseHandler<string>
{
    public MyCustomMethod(IClient client) : base(client, "my_customMethod") { }

    public Task<string> SendRequestAsync(object id, params object[] paramList)
        => base.SendRequestAsync(id, paramList);
}

// For a method that takes no parameters
public class MyCustomNoParamMethod : RpcRequestResponseHandlerNoParam<string>
{
    public MyCustomNoParamMethod(IClient client) : base(client, "my_noParamMethod") { }
}
```

Both `RpcRequestResponseHandler<TResponse>` and `RpcRequestResponseHandlerNoParam<TResponse>` implement `IRpcRequestHandler<TResponse>`, decode responses via `Client.DecodeResult<TResponse>`, and (for the no-param case) expose `CreateBatchItem(object id)` to participate in batch requests.

## Related Packages

### Core Abstractions
- **Nethereum.JsonRpc.Client** - This package (abstraction layer)

### Concrete Implementations
- **Nethereum.JsonRpc.RpcClient** - HTTP/HTTPS client (Newtonsoft.Json)
- **Nethereum.JsonRpc.SystemTextJsonRpcClient** - HTTP client (System.Text.Json)
- **Nethereum.JsonRpc.WebSocketClient** - WebSocket client
- **Nethereum.JsonRpc.WebSocketStreamingClient** - Streaming WebSocket client
- **Nethereum.JsonRpc.IpcClient** - IPC client

### Used By
- **Nethereum.RPC** - High-level RPC services
- **Nethereum.Web3** - Complete Web3 API
- All Nethereum client implementations

## Additional Resources

- [Ethereum JSON-RPC Specification](https://ethereum.org/en/developers/docs/apis/json-rpc/)
- [JSON-RPC 2.0 Specification](https://www.jsonrpc.org/specification)
- [Nethereum Documentation](http://docs.nethereum.com/)
- [Nethereum RPC Services](http://docs.nethereum.com/en/latest/nethereum-rpc/)
