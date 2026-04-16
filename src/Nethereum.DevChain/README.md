# Nethereum.DevChain

Development blockchain with full EVM execution up to the Prague hardfork, automatic mining, SQLite storage, and extended RPC support. A local Ethereum-compatible chain for testing and development.

## Overview

Nethereum.DevChain provides a complete local blockchain environment:
- **Instant Mining** - Transactions mined immediately or on a configurable interval
- **Full EVM Execution** - EVM opcode support up to Prague via Nethereum.EVM
- **SQLite Storage** - Default lightweight storage with auto-cleanup (no native dependencies)
- **State Management** - Patricia trie-based state with snapshot/revert
- **Extended RPC** - Development, debug, and Anvil-compatible APIs
- **Transaction Tracing** - Geth-compatible debug_traceTransaction and debug_traceCall
- **Forking** - Fork from live Ethereum networks

## Installation

```bash
dotnet add package Nethereum.DevChain
```

## Dependencies

- Nethereum.CoreChain - Core blockchain infrastructure
- Nethereum.RPC.Extensions - Extended RPC utilities
- Nethereum.JsonRpc.RpcClient - JSON-RPC client
- Nethereum.Web3 - Web3 and Accounts
- Microsoft.Data.Sqlite - SQLite storage provider
- Microsoft.Extensions.Hosting.Abstractions - IHostedService support
- Microsoft.AspNetCore.App (FrameworkReference) - ASP.NET Core web extensions

## Quick Start

### Default (SQLite with auto-cleanup)

```csharp
using Nethereum.DevChain;

var node = new DevChainNode();
await node.StartAsync(new[] { "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266" });

// Use the node...

node.Dispose(); // SQLite DB deleted automatically
```

### With Custom Config

```csharp
var config = new DevChainConfig
{
    ChainId = 1337,
    BlockGasLimit = 30_000_000,
    AutoMine = true,
    InitialBalance = BigInteger.Parse("10000000000000000000000") // 10000 ETH
};

var node = new DevChainNode(config);
await node.StartAsync(fundedAddresses);
```

### Fully In-Memory

```csharp
var node = DevChainNode.CreateInMemory();
await node.StartAsync(fundedAddresses);
```

### Persistent SQLite

```csharp
var node = new DevChainNode(DevChainConfig.Default, "./mychain/chain.db", persistDb: true);
await node.StartAsync(fundedAddresses);
// DB survives restart
```

### Custom ChainId

```csharp
var node = new DevChainNode(new DevChainConfig { ChainId = 31337 });
await node.StartAsync(new[] { "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266" });
```

## Storage Architecture

DevChain uses a hybrid storage strategy by default:

| Data | Store | Reason |
|------|-------|--------|
| Blocks | SQLite | Historical, grows unbounded |
| Transactions | SQLite | Historical, grows unbounded |
| Receipts | SQLite | Historical, grows unbounded |
| Logs | SQLite | Historical, grows unbounded |
| State | In-Memory | Needs fast snapshot/revert |
| Filters | In-Memory | Transient, bounded |
| Trie Nodes | In-Memory | Needs fast access |

SQLite uses WAL journal mode for concurrent reads during block production. The database is auto-deleted on dispose unless persistence is enabled.

## Configuration

`DevChainConfig` inherits from `ChainConfig`. Properties like `ChainId`, `BlockGasLimit`, `BaseFee`, and `InitialBalance` come from the base class.

```csharp
public class DevChainConfig : ChainConfig
{
    public int ChainId { get; set; } = 1337;
    public long BlockGasLimit { get; set; } = 30_000_000;
    public bool AutoMine { get; set; } = true;
    public long BlockTime { get; set; } = 0;           // 0 = instant
    public int MaxTransactionsPerBlock { get; set; } = 100;
    public BigInteger BaseFee { get; set; } = 1_000_000_000; // 1 gwei
    public BigInteger InitialBalance { get; set; }; // Default: 10000 ETH (BigInteger.Parse("10000000000000000000000"))

    // Forking
    public string ForkUrl { get; set; }
    public long? ForkBlockNumber { get; set; }

    // Auto-mine batching
    public int AutoMineBatchSize { get; set; } = 1;
    public int AutoMineBatchTimeoutMs { get; set; } = 10;
}

// Built-in presets
var config = DevChainConfig.Default;   // ChainId 1337
var config = DevChainConfig.Hardhat;   // ChainId 31337
var config = DevChainConfig.Anvil;     // ChainId 31337
```

## Core Features

### Transaction Processing

```csharp
// Send a signed transaction
var txHash = await node.SendTransactionAsync(signedTransaction);

// Get receipt
var receipt = await node.GetTransactionReceiptAsync(txHash);

// Raw byte sending (eth_sendRawTransaction) is available via the RPC layer
```

### Contract Execution

```csharp
// Execute without state change (individual parameters)
var result = await node.CallAsync(
    to: contractAddress,
    data: dataBytes,
    from: senderAddress,
    value: null,
    gasLimit: null
);

// eth_call and eth_estimateGas are also available via the RPC layer with CallInput objects
```

### State Access

```csharp
var balance = await node.GetBalanceAsync(address);
var code = await node.GetCodeAsync(contractAddress);
var storage = await node.GetStorageAtAsync(contractAddress, slot);
var nonce = await node.GetNonceAsync(address);
```

### Account Management

Modify account state for testing:

```csharp
await node.SetBalanceAsync(address, newBalance);
await node.SetCodeAsync(address, bytecode);
await node.SetStorageAtAsync(address, slot, value);
await node.SetNonceAsync(address, nonce);
await node.SetBlockHashAsync(blockNumber, hash);
```

`SetBlockHashAsync` stamps the hash into the block store **and**
mirrors it into the EIP-2935 history contract's storage at
`0x0000F90827F1C53a10cb7A02335B175320002935`, slot `blockNumber %
8191`. That mirror keeps the BLOCKHASH opcode consistent with the
in-guest state-reader view for state tests and stateless replays
where the block-hash history is seeded explicitly rather than built
up from actual block production.

### Snapshots

```csharp
// Take snapshot (returns IStateSnapshot)
var snapshot = await node.TakeSnapshotAsync();

// Execute transactions...

// Revert all changes (takes the snapshot object)
await node.RevertToSnapshotAsync(snapshot);
```

### Block Mining

```csharp
// Manual mining
await node.MineBlockAsync();

// Time manipulation
node.DevConfig.NextBlockTimestamp = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();
await node.MineBlockAsync();
```

## Debug/Trace APIs

### debug_traceTransaction

Trace a mined transaction step-by-step:

```csharp
using Nethereum.CoreChain.Tracing;

var config = new OpcodeTraceConfig
{
    EnableMemory = true,
    DisableStack = false,
    DisableStorage = false,
    EnableReturnData = true,
    Limit = 1000
};

var trace = await node.TraceTransactionAsync(txHash, config);

foreach (var log in trace.StructLogs)
{
    Console.WriteLine($"PC: {log.Pc}, Op: {log.Op}, Gas: {log.Gas}");
}
```

### debug_traceCall

Trace a call without mining, with optional state overrides:

```csharp
using Nethereum.CoreChain.Tracing;

var stateOverrides = new Dictionary<string, StateOverride>
{
    [address] = new StateOverride
    {
        Balance = "0x1000000000000000000",
        Code = "0x...",
        State = new Dictionary<string, string>
        {
            ["0x0"] = "0x..."
        }
    }
};

var trace = await node.TraceCallAsync(callInput, config, stateOverrides);
```

## RPC Handler Extensions

Register DevChain-specific RPC handlers:

```csharp
using Nethereum.DevChain.Rpc;
using Nethereum.CoreChain.Rpc;

var registry = new RpcHandlerRegistry();
registry.AddStandardHandlers();  // Core Ethereum methods
registry.AddDevHandlers();       // Dev/debug methods
registry.AddAnvilAliases();      // Anvil compatibility
```

### Development Methods

| Method | Description |
|--------|-------------|
| `evm_mine` | Mine a block |
| `evm_snapshot` | Create state snapshot |
| `evm_revert` | Revert to snapshot |
| `evm_increaseTime` | Increase block timestamp |
| `evm_setNextBlockTimestamp` | Set next block timestamp |

### Account Management Methods

| Method | Description |
|--------|-------------|
| `hardhat_setBalance` | Set account balance |
| `hardhat_setCode` | Set contract code |
| `hardhat_setNonce` | Set account nonce |
| `hardhat_setStorageAt` | Set storage slot |
| `hardhat_impersonateAccount` | Impersonate account |
| `hardhat_stopImpersonatingAccount` | Stop impersonating |

### Debug Methods

| Method | Description |
|--------|-------------|
| `debug_traceTransaction` | Trace mined transaction |
| `debug_traceCall` | Trace call without mining |

### Anvil Aliases

All `hardhat_*` methods are also available as `anvil_*` for compatibility:

| Anvil Method | Maps To |
|-------------|---------|
| `anvil_setBalance` | `hardhat_setBalance` |
| `anvil_setCode` | `hardhat_setCode` |
| `anvil_setNonce` | `hardhat_setNonce` |
| `anvil_setStorageAt` | `hardhat_setStorageAt` |
| `anvil_mine` | `evm_mine` |
| `anvil_snapshot` | `evm_snapshot` |
| `anvil_revert` | `evm_revert` |
| `anvil_impersonateAccount` | `hardhat_impersonateAccount` |
| `anvil_stopImpersonatingAccount` | `hardhat_stopImpersonatingAccount` |

## ASP.NET Core Hosting Extensions

DevChain includes reusable extensions for hosting in any ASP.NET Core application:

```csharp
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;

var builder = WebApplication.CreateBuilder(args);

var config = new DevChainServerConfig { ChainId = 31337, Storage = "sqlite" };
builder.AddDevChainServer(config); // Registers DI services, CORS, and hosted service

var app = builder.Build();
app.MapDevChainEndpoints(); // Maps JSON-RPC POST /, health GET /, and CORS middleware
app.Run();
```

`AddDevChainServer` registers `DevChainNode`, `RpcDispatcher`, `DevAccountManager`, storage providers, CORS, and `DevChainHostedService` as singletons.

### Manual RPC Setup

For non-web scenarios, use the lower-level DI extension on `IServiceCollection`:

```csharp
using Nethereum.DevChain.Hosting;

services.AddDevChainServer(config); // IServiceCollection extension
```

## Forking

Fork from live Ethereum networks to test against real state:

```csharp
var config = new DevChainConfig
{
    ForkUrl = "https://eth.llamarpc.com",
    ForkBlockNumber = 19000000
};

var node = new DevChainNode(config);
await node.StartAsync(fundedAddresses);

// Reads hit fork source on cache miss, writes are local only
var balance = await node.GetBalanceAsync("0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045");
```

## Historical State

DevChain supports querying state at any past block when using `HistoricalStateStore`:

```csharp
// State diffs are automatically recorded per block
// Query balance at a specific block
var pastBalance = await node.GetBalanceAsync(address, blockNumber: 5);
```

## Related Packages

- **Nethereum.CoreChain** - Core blockchain infrastructure and storage interfaces
- **Nethereum.DevChain.Server** - HTTP JSON-RPC server (dotnet tool)
- **Nethereum.Aspire.DevChain** - Aspire-orchestrated variant for distributed dev environments
- **Nethereum.EVM** - EVM simulator
- **Nethereum.RPC** - Standard debug tracing DTOs

## Additional Resources

- [Ethereum JSON-RPC Specification](https://ethereum.github.io/execution-apis/api-documentation/)
- [Geth Debug API](https://geth.ethereum.org/docs/interacting-with-geth/rpc/ns-debug)
- [Nethereum Documentation](http://docs.nethereum.com)
