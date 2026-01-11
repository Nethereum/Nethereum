# Nethereum.DevChain

Development blockchain with full EVM execution, automatic mining, and extended RPC support. A local Ethereum-compatible chain for testing and development.

## Overview

Nethereum.DevChain provides a complete local blockchain environment:
- **Instant Mining** - Transactions are mined immediately
- **Full EVM Execution** - Complete EVM opcode support via Nethereum.EVM
- **State Management** - Patricia trie-based state with snapshots
- **Extended RPC** - Development and debug APIs beyond standard Ethereum
- **Transaction Tracing** - Geth-compatible debug_traceTransaction and debug_traceCall

## Installation

```bash
dotnet add package Nethereum.DevChain
```

## Dependencies

**Package References:**
- Nethereum.CoreChain - Core blockchain infrastructure
- Nethereum.RPC.Extensions - Extended RPC utilities
- Nethereum.JsonRpc.RpcClient - JSON-RPC client
- Nethereum.Geth - Geth-compatible debug DTOs

## DevChainNode

The main class for running a development chain:

```csharp
using Nethereum.DevChain;

var config = new DevChainConfig
{
    ChainId = 1337,
    GasLimit = 30000000,
    BaseFee = 1000000000
};

var node = new DevChainNode(config);

// Start with funded accounts
var fundedAddresses = new[] { "0x1234...", "0x5678..." };
await node.StartAsync(fundedAddresses);
```

**From:** `src/Nethereum.DevChain/DevChainNode.cs`

## Core Features

### Transaction Processing

Submit and execute transactions:

```csharp
// Send raw transaction
var txHash = await node.SendRawTransactionAsync(signedTxBytes);

// Wait for receipt
var receipt = await node.GetTransactionReceiptAsync(txHash);
```

### Contract Execution

Execute contract calls with full EVM tracing:

```csharp
using Nethereum.RPC.Eth.DTOs;

var callInput = new CallInput
{
    From = "0x1234...",
    To = contractAddress,
    Data = "0x..." // Encoded function call
};

// Execute without state change
var result = await node.CallAsync(callInput);

// Estimate gas
var gasEstimate = await node.EstimateGasAsync(callInput);
```

### State Access

```csharp
// Get account balance
var balance = await node.GetBalanceAsync(address);

// Get contract code
var code = await node.GetCodeAsync(contractAddress);

// Get storage at slot
var storage = await node.GetStorageAtAsync(contractAddress, slot);

// Get transaction count (nonce)
var nonce = await node.GetTransactionCountAsync(address);
```

### Block Management

```csharp
// Get latest block
var block = await node.GetLatestBlockAsync();

// Get block by number
var block = await node.GetBlockByNumberAsync(blockNumber);

// Get block by hash
var block = await node.GetBlockByHashAsync(blockHash);
```

## Debug/Trace APIs

DevChain supports Geth-compatible debug APIs for transaction tracing:

### debug_traceTransaction

Trace a mined transaction step-by-step:

```csharp
using Nethereum.Geth.RPC.Debug.Tracers;

var config = new OpcodeTracerConfigDto
{
    EnableMemory = true,
    DisableStack = false,
    DisableStorage = false,
    EnableReturnData = true,
    Limit = 1000 // Max trace steps
};

var trace = await node.TraceTransactionAsync(txHash, config);

// Access trace results
Console.WriteLine($"Gas used: {trace.Gas}");
Console.WriteLine($"Failed: {trace.Failed}");
Console.WriteLine($"Return value: {trace.ReturnValue}");

foreach (var log in trace.StructLogs)
{
    Console.WriteLine($"PC: {log.Pc}, Op: {log.Op}, Gas: {log.Gas}");
    Console.WriteLine($"Stack: {string.Join(", ", log.Stack)}");
}
```

**From:** `src/Nethereum.DevChain/DevChainNode.cs`

### debug_traceCall

Trace a call without mining:

```csharp
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Geth.RPC.Debug.DTOs;

var callInput = new CallInput
{
    From = "0x1234...",
    To = contractAddress,
    Data = "0x18160ddd" // totalSupply()
};

// With state overrides
var stateOverrides = new Dictionary<string, StateOverrideDto>
{
    [address] = new StateOverrideDto
    {
        Balance = "0x1000000000000000000", // 1 ETH
        Code = "0x...", // Override contract code
        State = new Dictionary<string, string>
        {
            ["0x0"] = "0x..." // Override storage
        }
    }
};

var trace = await node.TraceCallAsync(callInput, config, stateOverrides);
```

**From:** `src/Nethereum.DevChain/DevChainNode.cs`

## RPC Handler Extensions

Register DevChain-specific RPC handlers:

```csharp
using Nethereum.DevChain.Rpc;
using Nethereum.CoreChain.Rpc;

var registry = new RpcHandlerRegistry();

// Standard Ethereum handlers
registry.AddStandardHandlers();

// DevChain handlers (evm_*, hardhat_*, etc.)
registry.AddDevHandlers();

// Debug handlers (debug_traceTransaction, debug_traceCall)
registry.AddDebugHandlers();
```

**From:** `src/Nethereum.DevChain/Rpc/DevRpcHandlerExtensions.cs`

### Available Dev Methods

| Method | Description |
|--------|-------------|
| `evm_mine` | Mine a block |
| `evm_snapshot` | Create state snapshot |
| `evm_revert` | Revert to snapshot |
| `evm_increaseTime` | Increase block timestamp |
| `evm_setNextBlockTimestamp` | Set next block timestamp |

### Available Debug Methods

| Method | Description |
|--------|-------------|
| `debug_traceTransaction` | Trace mined transaction |
| `debug_traceCall` | Trace call without mining |

## Configuration

```csharp
public class DevChainConfig
{
    public int ChainId { get; set; } = 1337;
    public long GasLimit { get; set; } = 30000000;
    public BigInteger BaseFee { get; set; } = 1000000000; // 1 gwei
    public BigInteger InitialBalance { get; set; } = BigInteger.Parse("10000000000000000000000"); // 10000 ETH
}
```

**From:** `src/Nethereum.DevChain/DevChainConfig.cs`

## Transaction Processing

The `TransactionProcessor` handles transaction execution:

```csharp
var processor = new TransactionProcessor(
    stateStore,
    trieNodeStore,
    chainConfig
);

var result = await processor.ProcessTransactionAsync(
    signedTransaction,
    blockContext
);

// Result contains:
// - Success/failure status
// - Gas used
// - Logs emitted
// - Contract address (for deployments)
// - Return data
// - Revert reason (if failed)
```

**From:** `src/Nethereum.DevChain/TransactionProcessor.cs`

## Block Management

The `BlockManager` handles block creation and storage:

```csharp
var blockManager = new BlockManager(
    blockStore,
    transactionStore,
    receiptStore,
    trieNodeStore
);

// Create and mine a block with transactions
var block = await blockManager.CreateBlockAsync(
    parentHash,
    transactions,
    receipts,
    stateRoot,
    receiptsRoot
);
```

**From:** `src/Nethereum.DevChain/BlockManager.cs`

## Usage with RPC Server

DevChain is typically used with `Nethereum.DevChain.Server`:

```csharp
using Nethereum.DevChain;
using Nethereum.DevChain.Rpc;
using Nethereum.CoreChain.Rpc;

// Create node
var node = new DevChainNode(config);
await node.StartAsync(fundedAddresses);

// Setup RPC
var registry = new RpcHandlerRegistry();
registry.AddStandardHandlers();
registry.AddDevHandlers();
registry.AddDebugHandlers();

var context = new RpcContext(node, chainId, services);
var dispatcher = new RpcDispatcher(registry, context);

// Handle RPC requests
var request = new RpcRequestMessage(1, "eth_blockNumber");
var response = await dispatcher.DispatchAsync(request);
```

## Trace Converter

Convert internal EVM traces to Geth-compatible format:

```csharp
using Nethereum.DevChain.Tracing;
using Nethereum.EVM;
using Nethereum.Geth.RPC.Debug.Tracers;

// After EVM execution with tracing enabled
var program = await simulator.ExecuteAsync(program, traceEnabled: true);

// Convert to Geth format
var config = new OpcodeTracerConfigDto
{
    EnableMemory = true,
    DisableStack = false,
    DisableStorage = false,
    Limit = 100
};

var opcodeResponse = TraceConverter.ConvertToOpcodeResponse(program, config);

// Response contains:
// - Gas used
// - Failed flag
// - Return value
// - StructLogs array (Geth-compatible)
```

**From:** `src/Nethereum.DevChain/Tracing/TraceConverter.cs`

## Related Packages

- **Nethereum.CoreChain** - Core blockchain infrastructure
- **Nethereum.DevChain.Server** - HTTP JSON-RPC server (dotnet tool)
- **Nethereum.EVM** - EVM simulator
- **Nethereum.Geth** - Geth-compatible DTOs for tracing

## Example: Complete Test Setup

```csharp
using Nethereum.DevChain;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

// Start dev chain
var node = new DevChainNode(new DevChainConfig { ChainId = 1337 });
var accounts = new[] { "0x1234..." };
await node.StartAsync(accounts);

// Connect Web3 client
var account = new Account("private_key", 1337);
var web3 = new Web3(account, "http://localhost:8545");

// Deploy and interact with contracts
var deploymentReceipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
    abi, bytecode, account.Address
);

var contract = web3.Eth.GetContract(abi, deploymentReceipt.ContractAddress);
var result = await contract.GetFunction("myFunction").CallAsync<int>();
```

## Additional Resources

- [Ethereum JSON-RPC Specification](https://ethereum.github.io/execution-apis/api-documentation/)
- [Geth Debug API](https://geth.ethereum.org/docs/interacting-with-geth/rpc/ns-debug)
- [Nethereum Documentation](http://docs.nethereum.com)
