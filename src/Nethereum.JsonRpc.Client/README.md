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
- **Microsoft.Extensions.Logging.Abstractions** - Logging support
- **Newtonsoft.Json** - JSON serialization

## Quick Start

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;

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

Base interface with common properties:

```csharp
public interface IBaseClient
{
    RequestInterceptor OverridingRequestInterceptor { get; set; }
    TimeSpan ConnectionTimeout { get; set; }
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

```csharp
using Nethereum.JsonRpc.Client;

var client = new RpcClient(new Uri("http://localhost:8545"));

// Create batch request
var batch = new RpcRequestResponseBatch();

// Add multiple requests
var blockNumberRequest = new RpcRequestMessage(1, "eth_blockNumber");
var chainIdRequest = new RpcRequestMessage(2, "eth_chainId");
var gasPriceRequest = new RpcRequestMessage(3, "eth_gasPrice");

batch.BatchItems.Add(new RpcRequestResponseBatchItem<string>(blockNumberRequest));
batch.BatchItems.Add(new RpcRequestResponseBatchItem<string>(chainIdRequest));
batch.BatchItems.Add(new RpcRequestResponseBatchItem<string>(gasPriceRequest));

// Send batch
var batchResult = await client.SendBatchRequestAsync(batch);

// Process results
foreach (var item in batchResult.BatchItems)
{
    if (item.HasError)
    {
        Console.WriteLine($"Request {item.RpcRequestMessage.Id} failed: {item.RpcError.Message}");
    }
    else
    {
        Console.WriteLine($"Request {item.RpcRequestMessage.Id}: {item.GetResponse<string>()}");
    }
}
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

```csharp
using Nethereum.JsonRpc.Client;

var client = new RpcClient(new Uri("http://localhost:8545"));

// Default timeout is 120 seconds
Console.WriteLine($"Default timeout: {client.ConnectionTimeout.TotalSeconds}s");

// Set custom timeout
client.ConnectionTimeout = TimeSpan.FromSeconds(10);

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
batch.BatchItems.Add(new RpcRequestResponseBatchItem<string>(
    new RpcRequestMessage(1, "eth_blockNumber")
));
batch.BatchItems.Add(new RpcRequestResponseBatchItem<string>(
    new RpcRequestMessage(2, "invalid_method") // This will fail
));
batch.BatchItems.Add(new RpcRequestResponseBatchItem<string>(
    new RpcRequestMessage(3, "eth_chainId")
));

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
        Console.WriteLine($"Request {item.RpcRequestMessage.Id} succeeded: {item.GetResponse<string>()}");
    }
}

Console.WriteLine($"Success: {successCount}, Errors: {errorCount}");
```

## API Reference

### ClientBase

Abstract base class for client implementations:

```csharp
public abstract class ClientBase : IClient
{
    public RequestInterceptor OverridingRequestInterceptor { get; set; }
    public TimeSpan ConnectionTimeout { get; set; }

    protected abstract Task<RpcResponseMessage> SendAsync(RpcRequestMessage request, string route = null);
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

Represents a JSON-RPC error:

```csharp
public class RpcError
{
    public int Code { get; set; }
    public string Message { get; set; }
    public object Data { get; set; }
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
| **Nethereum.JsonRpc.SystemTextJsonRpcClient** | HTTP (System.Text.Json) | .NET 6+ with System.Text.Json |

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

## License

This package is part of the Nethereum project and follows the same MIT license.
