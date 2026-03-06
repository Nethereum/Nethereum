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
- Nethereum.RPC - Standard debug tracing DTOs
- Microsoft.Data.Sqlite - SQLite storage provider

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
var node = new DevChainNode("./mychain/chain.db", persistDb: true);
await node.StartAsync(fundedAddresses);
// DB survives restart
```

### Convenience Factory

```csharp
var node = await DevChainNode.CreateAndStartAsync(
    fundedAddresses,
    new DevChainConfig { ChainId = 31337 }
);
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

```csharp
public class DevChainConfig
{
    public int ChainId { get; set; } = 1337;
    public long BlockGasLimit { get; set; } = 30_000_000;
    public bool AutoMine { get; set; } = true;
    public long BlockTime { get; set; } = 0;           // 0 = instant
    public int MaxTransactionsPerBlock { get; set; } = 100;
    public BigInteger BaseFee { get; set; } = 1_000_000_000; // 1 gwei
    public BigInteger InitialBalance { get; set; };

    // Forking
    public string ForkUrl { get; set; }
    public long? ForkBlockNumber { get; set; }

    // Auto-mine batching
    public int AutoMineBatchSize { get; set; } = 1;
    public int AutoMineBatchTimeoutMs { get; set; } = 100;
}

// Built-in presets
var config = DevChainConfig.Default;   // ChainId 1337
var config = DevChainConfig.Hardhat;   // ChainId 31337
var config = DevChainConfig.Anvil;     // ChainId 31337
```

## Core Features

### Transaction Processing

```csharp
// Send raw transaction
var txHash = await node.SendRawTransactionAsync(signedTxBytes);

// Get receipt
var receipt = await node.GetTransactionReceiptAsync(txHash);
```

### Contract Execution

```csharp
using Nethereum.RPC.Eth.DTOs;

var callInput = new CallInput
{
    From = "0x1234...",
    To = contractAddress,
    Data = "0x..."
};

// Execute without state change
var result = await node.CallAsync(callInput);

// Estimate gas
var gasEstimate = await node.EstimateGasAsync(callInput);
```

### State Access

```csharp
var balance = await node.GetBalanceAsync(address);
var code = await node.GetCodeAsync(contractAddress);
var storage = await node.GetStorageAtAsync(contractAddress, slot);
var nonce = await node.GetTransactionCountAsync(address);
```

### Account Management

Modify account state for testing:

```csharp
await node.SetBalanceAsync(address, newBalance);
await node.SetCodeAsync(address, bytecode);
await node.SetStorageAtAsync(address, slot, value);
await node.SetNonceAsync(address, nonce);
```

### Snapshots

```csharp
// Take snapshot
var snapshotId = await node.SnapshotAsync();

// Execute transactions...

// Revert all changes
await node.RevertToSnapshotAsync(snapshotId);
```

### Block Mining

```csharp
// Manual mining
await node.MineBlockAsync();

// Time manipulation
node.Config.NextBlockTimestamp = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();
await node.MineBlockAsync();
```

## Debug/Trace APIs

### debug_traceTransaction

Trace a mined transaction step-by-step:

```csharp
using Nethereum.RPC.DebugNode.Tracers;

var config = new OpcodeTracerConfigDto
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
var stateOverrides = new Dictionary<string, StateOverrideDto>
{
    [address] = new StateOverrideDto
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
| `anvil_impersonateAccount` | `hardhat_impersonateAccount` |
| `anvil_stopImpersonatingAccount` | `hardhat_stopImpersonatingAccount` |
| `anvil_mine` | `evm_mine` |
| `anvil_snapshot` | `evm_snapshot` |
| `anvil_revert` | `evm_revert` |

## Usage with RPC Server

DevChain is typically used with `Nethereum.DevChain.Server`:

```csharp
using Nethereum.DevChain;
using Nethereum.DevChain.Rpc;
using Nethereum.CoreChain.Rpc;

var node = new DevChainNode(config);
await node.StartAsync(fundedAddresses);

var registry = new RpcHandlerRegistry();
registry.AddStandardHandlers();
registry.AddDevHandlers();
registry.AddAnvilAliases();

var context = new RpcContext(node, chainId, services);
var dispatcher = new RpcDispatcher(registry, context);

var response = await dispatcher.DispatchAsync(request);
```

## Forking

Fork from live Ethereum networks to test against real state:

```csharp
var config = new DevChainConfig
{
    ForkUrl = "https://eth.llamarpc.com",
    ForkBlockNumber = 19000000
};

var node = await DevChainNode.CreateAndStartAsync(fundedAddresses, config);

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
