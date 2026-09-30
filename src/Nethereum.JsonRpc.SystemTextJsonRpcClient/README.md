# Nethereum.JsonRpc.SystemTextJsonRpcClient

Modern, lightweight, and AOT-friendly HTTP/HTTPS JSON-RPC client using System.Text.Json.

## Overview

Nethereum.JsonRpc.SystemTextJsonRpcClient provides a **modern, high-performance HTTP/HTTPS transport implementation** for Ethereum node communication using **System.Text.Json** instead of Newtonsoft.Json for the wire payloads. This package targets **net9.0 only** and supports **AOT (Ahead-of-Time) compilation** through source generators, making it ideal for modern cloud-native applications, serverless functions, and performance-critical scenarios.

**Key Features:**
- **System.Text.Json** serialization (faster, lower memory)
- **AOT compilation** support (Native AOT, ReadyToRun)
- **Source-generated JSON** serialization (zero reflection) for a curated set of RPC DTOs
- **Bearer token** authentication
- **net9.0 only** - it is not multi-targeted like most other Nethereum packages
- Production-ready connection management

**Use Cases:**
- Modern .NET 9 applications
- Cloud-native and serverless deployments
- Native AOT applications (smaller, faster startup)
- Performance-critical blockchain indexers
- Azure Functions / AWS Lambda
- Microservices and containers

## Installation

```bash
dotnet add package Nethereum.JsonRpc.SystemTextJsonRpcClient
```

**Requirements:**
- **net9.0** - this is the only target framework the package ships (`Nethereum.JsonRpc.SystemTextJsonRpcClient.csproj`: `<TargetFrameworks>net9.0</TargetFrameworks>`)
- For other .NET versions, use `Nethereum.JsonRpc.RpcClient` (Newtonsoft.Json, targets `net451;net461;netstandard2.0;net6.0;net8.0;net9.0;net10.0`)

## Dependencies

**Nethereum:**
- **Nethereum.JsonRpc.Client** - Core RPC abstraction
- **Nethereum.Hex** - Hex encoding/decoding
- **Nethereum.RPC** - RPC DTOs and services

**External:**
- **System.Text.Json** (built-in to .NET 9)
- **Microsoft.Extensions.Logging.Abstractions** - Logging support
- **Newtonsoft.Json** (`[11.0.2,14)`) - a **real transitive dependency**, pulled in by `Nethereum.JsonRpc.Client`/`Nethereum.RPC`; it is not "minimal dependencies" in the sense of avoiding Newtonsoft.Json entirely, only the RPC *wire payloads* in this package avoid it

## Quick Start

### Using SimpleRpcClient (Recommended for Simple Scenarios)

```csharp
using Nethereum.JsonRpc.SystemTextJsonRpcClient;
using Nethereum.Web3;

// SimpleRpcClient - most straightforward API for AOT
var client = new SimpleRpcClient("https://eth.drpc.org");
var web3 = new Web3(client);

var balance = await web3.Eth.GetBalance.SendRequestAsync("0x742d35Cc6634C0532925a3b844Bc454e4438f44e");
Console.WriteLine($"Balance: {balance}");
```

### Using RpcClient (Full Control)

```csharp
using Nethereum.JsonRpc.SystemTextJsonRpcClient;
using Nethereum.RPC.Eth.Blocks;

// RpcClient - full constructor (uses default NethereumRpcJsonContext for AOT)
var client = new RpcClient("http://localhost:8545");

// Query blockchain
var ethBlockNumber = new EthBlockNumber(client);
var blockNumber = await ethBlockNumber.SendRequestAsync();

Console.WriteLine($"Current block: {blockNumber.Value}");
```

## Usage Examples

### Example 1: Basic Connection with AOT Support

```csharp
using Nethereum.JsonRpc.SystemTextJsonRpcClient;
using Nethereum.RPC.Eth;

// Uses NethereumRpcJsonContext.Default for AOT compilation
var client = new RpcClient("http://localhost:8545");

// All common Ethereum RPC types are AOT-compatible
var ethChainId = new EthChainId(client);
var chainId = await ethChainId.SendRequestAsync();

var ethGasPrice = new EthGasPrice(client);
var gasPrice = await ethGasPrice.SendRequestAsync();

Console.WriteLine($"Chain ID: {chainId.Value}");
Console.WriteLine($"Gas Price: {gasPrice.Value} wei");
```

### Example 2: Bearer Token Authentication (API Keys)

```csharp
using Nethereum.JsonRpc.SystemTextJsonRpcClient;
using Nethereum.RPC.Eth.Blocks;

// Create client
var client = new RpcClient("https://api.example.com/rpc");

// Add Bearer token (common for cloud providers)
client.AddBearerToken("your-api-key-here");

// Use normally
var ethBlockNumber = new EthBlockNumber(client);
var blockNumber = await ethBlockNumber.SendRequestAsync();

Console.WriteLine($"Block (authenticated): {blockNumber.Value}");
```

### Example 3: Custom Logging with Microsoft.Extensions.Logging

```csharp
using Nethereum.JsonRpc.SystemTextJsonRpcClient;
using Microsoft.Extensions.Logging;
using Nethereum.RPC.Eth;

// Create logger
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

var logger = loggerFactory.CreateLogger<RpcClient>();

// Create client with logging
var client = new RpcClient(
    new Uri("http://localhost:8545"),
    logger: logger
);

// All requests are logged
var ethAccounts = new EthAccounts(client);
var accounts = await ethAccounts.SendRequestAsync();
// Console output: Sending request: {"jsonrpc":"2.0","method":"eth_accounts","params":[],"id":1}
// Console output: Received response: {"jsonrpc":"2.0","result":["0x..."],"id":1}
```

### Example 4: Custom JsonSerializerOptions

```csharp
using Nethereum.JsonRpc.SystemTextJsonRpcClient;
using System.Text.Json;
using System.Text.Json.Serialization;

// Create custom serializer options
var serializerOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    NumberHandling = JsonNumberHandling.AllowReadingFromString
};

// Create client with custom options
var client = new RpcClient(
    new Uri("http://localhost:8545"),
    logger: null,
    authHeader: null,
    handler: null,
    context: NethereumRpcJsonContext.Default,
    serializerOptions: serializerOptions
);

var ethChainId = new EthChainId(client);
var chainId = await ethChainId.SendRequestAsync();

Console.WriteLine($"Chain ID: {chainId.Value}");
```

### Example 5: Custom HttpMessageHandler for HTTP/2 and Connection Pooling

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.SystemTextJsonRpcClient;
using Nethereum.RPC.Eth.Blocks;
using System.Net.Http;

// Create custom SocketsHttpHandler with HTTP/2
var handler = new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
    MaxConnectionsPerServer = 50,
    EnableMultipleHttp2Connections = true,
    AllowAutoRedirect = false
};

// Create client with custom handler
var client = new RpcClient(
    new Uri("https://mainnet.infura.io/v3/YOUR_PROJECT_ID"),
    logger: null,
    authHeader: null,
    handler: handler,
    context: NethereumRpcJsonContext.Default
);

ClientBase.ConnectionTimeout = TimeSpan.FromSeconds(30); // static on ClientBase - `client.ConnectionTimeout = ...` does not compile (CS0176)

var ethBlockNumber = new EthBlockNumber(client);
var blockNumber = await ethBlockNumber.SendRequestAsync();

Console.WriteLine($"Block (HTTP/2): {blockNumber.Value}");
```

### Example 6: Basic Authentication (Username/Password)

```csharp
using Nethereum.JsonRpc.SystemTextJsonRpcClient;
using System.Net.Http.Headers;
using System.Text;

// Option 1: URL-based authentication
var client1 = new RpcClient("http://user:pass@localhost:8545");

// Option 2: Explicit AuthenticationHeaderValue
var credentials = Convert.ToBase64String(
    Encoding.UTF8.GetBytes("admin:secretpassword")
);
var authHeader = new AuthenticationHeaderValue("Basic", credentials);

var client2 = new RpcClient(
    new Uri("http://localhost:8545"),
    logger: null,
    authHeader: authHeader
);

var ethAccounts = new EthAccounts(client2);
var accounts = await ethAccounts.SendRequestAsync();

Console.WriteLine($"Accounts: {string.Join(", ", accounts)}");
```

### Example 7: Using with Nethereum.Web3

```csharp
using Nethereum.Web3;
using Nethereum.JsonRpc.SystemTextJsonRpcClient;

// Create SystemTextJson RPC client
var rpcClient = new RpcClient("http://localhost:8545");

// Use with Web3
var web3 = new Web3(rpcClient);

// Query blockchain
var balance = await web3.Eth.GetBalance.SendRequestAsync(
    "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb"
);

var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

Console.WriteLine($"Balance: {Web3.Convert.FromWei(balance)} ETH");
Console.WriteLine($"Block: {blockNumber.Value}");
```

### Example 8: Azure Functions / Serverless Example

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Nethereum.JsonRpc.SystemTextJsonRpcClient;
using Nethereum.RPC.Eth.Blocks;

public class BlockNumberFunction
{
    private readonly RpcClient _rpcClient;
    private readonly ILogger<BlockNumberFunction> _logger;

    public BlockNumberFunction(ILogger<BlockNumberFunction> logger)
    {
        _logger = logger;
        // Reuse client across function invocations
        _rpcClient = new RpcClient(
            Environment.GetEnvironmentVariable("ETHEREUM_RPC_URL")!,
            logger: logger
        );
    }

    [Function("GetBlockNumber")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Getting current block number");

        var ethBlockNumber = new EthBlockNumber(_rpcClient);
        var blockNumber = await ethBlockNumber.SendRequestAsync();

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { blockNumber = blockNumber.Value });

        return response;
    }
}
```

### Example 9: Native AOT Publishing Configuration

**Program.cs:**
```csharp
using Nethereum.JsonRpc.SystemTextJsonRpcClient;
using Nethereum.RPC.Eth.Blocks;

var client = new RpcClient("http://localhost:8545");

var ethBlockNumber = new EthBlockNumber(client);
var blockNumber = await ethBlockNumber.SendRequestAsync();

Console.WriteLine($"Block: {blockNumber.Value}");
```

**Project file (.csproj):**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Nethereum.JsonRpc.SystemTextJsonRpcClient" Version="*" />
    <PackageReference Include="Nethereum.RPC" Version="*" />
  </ItemGroup>
</Project>
```

**Publish:**
```bash
dotnet publish -c Release -r linux-x64
# Creates fully native executable with no runtime dependencies
```

### Example 10: Complete AOT Application with ABI Deserialization (from Nethereum.AOTSigningTest)

**CRITICAL for AOT:** You must configure ABI deserialization to use System.Text.Json:

```csharp
using Nethereum.JsonRpc.SystemTextJsonRpcClient;
using Nethereum.Web3;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

// CRITICAL: Enable System.Text.Json for ABI deserialization (required for AOT)
Nethereum.ABI.ABIDeserialisation.AbiDeserializationSettings.UseSystemTextJson = true;

var client = new SimpleRpcClient("https://eth.drpc.org");
var web3 = new Web3(client);

// Query balance
var balance = await web3.Eth.GetBalance.SendRequestAsync("0x742d35Cc6634C0532925a3b844Bc454e4438f44e");
Console.WriteLine($"Balance: {balance}");

// Get block with transactions
var block = await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest());
Console.WriteLine($"Block Number: {block.Number}");
Console.WriteLine($"Block Hash: {block.BlockHash}");
Console.WriteLine($"Block Transactions: {block.Transactions.Length}");

// ERC20 token balance
var tokenBalance = await web3.Eth.ERC20.GetContractService("0x9f8f72aa9304c8b593d555f12ef6589cc3a579a2")
                                      .BalanceOfQueryAsync("0x8ee7d9235e01e6b42345120b5d270bdb763624c7");
Console.WriteLine($"Token Balance: {Web3.Convert.FromWei(tokenBalance, 18)}");

// Transaction receipt with event decoding
var txnReceipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(
    "0x654288d8536948f30131a769043754bb9af2f5164c6668414751bcfa75c7f4e0"
);

if (txnReceipt != null)
{
    // Decode all Transfer events from receipt (AOT-compatible)
    var events = txnReceipt.DecodeAllEvents<TransferEventDTO>();
    foreach (var evt in events)
    {
        Console.WriteLine($"Transfer: {evt.Event.From} → {evt.Event.To}, Amount: {evt.Event.Value}");
    }
}

// Event definition (works with AOT)
[Event("Transfer")]
public class TransferEventDTO : IEventDTO
{
    [Parameter("address", "_from", 1, true)]
    public string From { get; set; }

    [Parameter("address", "_to", 2, true)]
    public string To { get; set; }

    [Parameter("uint256", "_value", 3, false)]
    public BigInteger Value { get; set; }
}
```

**Why this matters for AOT:**
- `UseSystemTextJson = true` ensures ABI encoding/decoding uses source-generated serialization
- Without this setting, ABI deserialization will use reflection and **fail at runtime with AOT**
- This applies to:
  - `DecodeAllEvents<T>()`
  - `CallDeserializingToObjectAsync<T>()`
  - Contract function outputs
  - Event DTOs

## API Reference

### RpcClient Constructor Overloads

```csharp
// Simple constructor (uses NethereumRpcJsonContext.Default)
public RpcClient(string url, ILogger? logger = null)

// Full constructor
public RpcClient(
    Uri baseUrl,
    ILogger? logger = null,
    AuthenticationHeaderValue? authHeader = null,
    HttpMessageHandler? handler = null,
    JsonSerializerContext? context = null,
    JsonSerializerOptions? serializerOptions = null)
```

### Key Methods

```csharp
// Add Bearer token authentication
public void AddBearerToken(string token)

// Inherited from ClientBase
public override T DecodeResult<T>(RpcResponseMessage rpcResponseMessage)
public override Task<RpcResponseMessage> SendAsync(RpcRequestMessage request, string? route = null)
protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests)
```

### SimpleRpcClient

A lighter alternative to `RpcClient` for callers that already own an `HttpClient` (`SimpleRpcClient.cs:15-57`):

```csharp
public class SimpleRpcClient : ClientBase
{
    // Full constructor
    public SimpleRpcClient(
        Uri baseUrl,
        HttpClient httpClient,
        JsonSerializerOptions serializerOptions = null,
        JsonSerializerContext context = null)

    // Simple constructor (uses NethereumRpcJsonContext.Default, creates its own HttpClient)
    public SimpleRpcClient(string url)
}
```

### RpcHttpHandlerFactory

Builds the default `SocketsHttpHandler` used by `RpcClient` when no `HttpMessageHandler` is supplied (`RpcHttpHandlerFactory.cs`):

```csharp
public static class RpcHttpHandlerFactory
{
    public static int MaxConnectionsPerServer { get; set; } = 20;
    public static TimeSpan ConnectionLifetime { get; set; } = TimeSpan.FromMinutes(10);
    public static TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(5);

    public static SocketsHttpHandler Create();
}
```

### Properties

`ConnectionTimeout` is inherited from `ClientBase` and is `static` - shared process-wide across every `ClientBase`-derived client, including this one:

```csharp
public static TimeSpan ConnectionTimeout { get; set; } // Default: 20 seconds
public RequestInterceptor? OverridingRequestInterceptor { get; set; }
```

## Important Notes

### System.Text.Json vs Newtonsoft.Json

| Feature | SystemTextJsonRpcClient | RpcClient (Newtonsoft) |
|---------|------------------------|------------------------|
| **Target Framework** | net9.0 only | net451;net461;netstandard2.0;net6.0;net8.0;net9.0;net10.0 |
| **RPC payload serialization** | System.Text.Json | Newtonsoft.Json |
| **AOT Support** | Yes (source generators) | No |
| **Authentication** | Bearer via AddBearerToken; Basic via URL user-info or header | Basic only |

**Note:** No benchmark numbers are published for this comparison - verify performance/memory claims yourself for your workload before relying on them.

### When to Use This Package

**Use SystemTextJsonRpcClient when:**
- Building .NET 9 applications
- Using Native AOT compilation
- Cloud-native/serverless (Azure Functions, AWS Lambda)
- Need Bearer token authentication
- Want source-generated (reflection-free) serialization for the curated RPC DTO set

**Use RpcClient (Newtonsoft.Json) when:**
- Need .NET Standard 2.0 / .NET Framework support, or any target other than net9.0
- Using older or LTS .NET versions
- Legacy codebases with Newtonsoft.Json
- Need Unity support

### AOT Compilation Benefits

**Native AOT advantages:**
- **Fast startup**: no JIT compilation at startup
- **Small binaries**: self-contained native executable
- **Lower memory**: No JIT overhead
- **Predictable performance**: No tier-0/tier-1 compilation delays
- **Container-friendly**: Smaller Docker images

**Limitations:**
- Reflection is limited (use source generators)
- Some dynamic features unavailable
- Platform-specific binaries required

### JsonSourceGenerator Support

`NethereumRpcJsonContext` (`NethereumRpcJsonContext.cs`) is a **curated** `JsonSerializerContext` - it does not cover every type in `Nethereum.RPC.Eth.DTOs`, only the types the attributes list explicitly, including:
- Block, BlockWithTransactions, BlockWithTransactionHashes, Transaction, TransactionInput, TransactionReceipt
- HexBigInteger, HexUTF8String
- FilterLog, CallInput, NewFilterInput, NewSubscriptionInput
- EthSimulateInput/Result types, AccountAccess/AccessList (BAL) types, AccountProof/StorageProof
- Engine API types (ExecutionPayloadV1-V4, ForkchoiceStateV1, PayloadAttributesV1-V4, etc.)
- TxPool DTOs (TxPoolContentResponse, PendingTransactionInfo, etc.)

If you need a type this context does not cover, pass your own `JsonSerializerContext`/`JsonSerializerOptions` via the `RpcClient`/`SimpleRpcClient` constructors, or fall back to reflection-based `System.Text.Json` (not AOT-safe).

### Thread Safety

- **Thread-safe** after initialization
- Safe for concurrent requests from multiple threads
- HttpClient connection pooling handles concurrency

### Error Handling

Same exceptions as `Nethereum.JsonRpc.RpcClient`:

| Exception | Description |
|-----------|-------------|
| **RpcClientTimeoutException** | Request exceeded ConnectionTimeout |
| **RpcClientUnknownException** | Network/HTTP errors |
| **RpcResponseException** | JSON-RPC error from node |

## Related Packages

### Alternative Transports
- **Nethereum.JsonRpc.RpcClient** - HTTP client (Newtonsoft.Json, broader compatibility)
- **Nethereum.JsonRpc.WebSocketClient** - WebSocket transport
- **Nethereum.JsonRpc.IpcClient** - IPC transport

### Core Dependencies
- **Nethereum.JsonRpc.Client** - Abstraction layer
- **Nethereum.RPC** - RPC services and DTOs

### Higher-Level APIs
- **Nethereum.Web3** - Complete Web3 API

## Common Cloud Providers

| Provider | URL Format | Authentication |
|----------|------------|----------------|
| **Infura** | `https://mainnet.infura.io/v3/PROJECT_ID` | URL-based |
| **Alchemy** | `https://eth-mainnet.g.alchemy.com/v2/API_KEY` | URL-based |
| **QuickNode** | `https://your-endpoint.quiknode.pro/TOKEN/` | URL-based |
| **BlockPI** | `https://ethereum.blockpi.network/v1/rpc/API_KEY` | URL-based |
| **Ankr** | `https://rpc.ankr.com/eth/API_KEY` | Bearer token |

## Additional Resources

- [System.Text.Json Documentation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview)
- [Native AOT Deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [Source Generators](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)
- [Nethereum Documentation](http://docs.nethereum.com/)

## License

This package is part of the Nethereum project and follows the same MIT license.
