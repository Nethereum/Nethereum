# Nethereum.CoreChain

Core blockchain infrastructure for state management, transaction processing, and JSON-RPC handling. Provides the foundational components for building local Ethereum nodes and development chains.

## Overview

Nethereum.CoreChain provides:
- **State Management** - Account state, storage, and trie-based state roots
- **Storage Interfaces** - Block, transaction, receipt, log, and filter stores
- **RPC Framework** - Extensible JSON-RPC handler system
- **Standard RPC Handlers** - Full implementation of core Ethereum JSON-RPC methods
- **Proof Generation** - Merkle proofs for state and storage

## Installation

```bash
dotnet add package Nethereum.CoreChain
```

## Dependencies

**Package References:**
- Nethereum.Model
- Nethereum.Merkle.Patricia
- Nethereum.EVM
- Nethereum.Hex
- Nethereum.RPC
- Nethereum.Signer
- Nethereum.Util
- Nethereum.JsonRpc.Client

## Storage Interfaces

CoreChain defines interfaces for blockchain data storage:

### IBlockStore

```csharp
public interface IBlockStore
{
    Task<BlockHeader> GetBlockByNumberAsync(long blockNumber);
    Task<BlockHeader> GetBlockByHashAsync(byte[] blockHash);
    Task<BlockHeader> GetLatestBlockAsync();
    Task SaveBlockAsync(BlockHeader block);
}
```

**From:** `src/Nethereum.CoreChain/Storage/IBlockStore.cs`

### ITransactionStore

```csharp
public interface ITransactionStore
{
    Task<SignedTransaction> GetTransactionByHashAsync(byte[] txHash);
    Task<List<SignedTransaction>> GetTransactionsByBlockHashAsync(byte[] blockHash);
    Task SaveTransactionAsync(SignedTransaction tx, byte[] blockHash, int index);
}
```

**From:** `src/Nethereum.CoreChain/Storage/ITransactionStore.cs`

### IReceiptStore

```csharp
public interface IReceiptStore
{
    Task<ReceiptInfo> GetReceiptByTxHashAsync(byte[] txHash);
    Task<List<ReceiptInfo>> GetReceiptsByBlockHashAsync(byte[] blockHash);
    Task SaveReceiptAsync(ReceiptInfo receipt);
}
```

**From:** `src/Nethereum.CoreChain/Storage/IReceiptStore.cs`

### IStateStore

```csharp
public interface IStateStore
{
    Task<byte[]> GetBalanceAsync(string address, long? blockNumber = null);
    Task<byte[]> GetCodeAsync(string address, long? blockNumber = null);
    Task<byte[]> GetStorageAtAsync(string address, byte[] key, long? blockNumber = null);
    Task<byte[]> GetNonceAsync(string address, long? blockNumber = null);
    Task SaveAccountStateAsync(string address, AccountState state, long blockNumber);
}
```

**From:** `src/Nethereum.CoreChain/Storage/IStateStore.cs`

### ILogStore

```csharp
public interface ILogStore
{
    Task<List<FilteredLog>> GetLogsAsync(LogFilter filter);
    Task SaveLogsAsync(byte[] blockHash, List<FilterLog> logs);
}
```

**From:** `src/Nethereum.CoreChain/Storage/ILogStore.cs`

## In-Memory Implementations

CoreChain provides in-memory implementations for testing and development:

```csharp
using Nethereum.CoreChain.Storage.InMemory;

var blockStore = new InMemoryBlockStore();
var txStore = new InMemoryTransactionStore();
var receiptStore = new InMemoryReceiptStore();
var stateStore = new InMemoryStateStore();
var logStore = new InMemoryLogStore();
var filterStore = new InMemoryFilterStore();
```

## RPC Framework

### RpcHandlerRegistry

Central registry for RPC method handlers:

```csharp
using Nethereum.CoreChain.Rpc;

var registry = new RpcHandlerRegistry();

// Register standard handlers
registry.AddStandardHandlers();

// Register custom handler
registry.Register(new MyCustomHandler());
```

**From:** `src/Nethereum.CoreChain/Rpc/RpcHandlerRegistry.cs`

### RpcDispatcher

Routes RPC requests to handlers:

```csharp
using Nethereum.CoreChain.Rpc;

var registry = new RpcHandlerRegistry();
registry.AddStandardHandlers();

var context = new RpcContext(chainNode, chainId, services);
var dispatcher = new RpcDispatcher(registry, context);

// Single request
var response = await dispatcher.DispatchAsync(request);

// Batch request
var responses = await dispatcher.DispatchBatchAsync(requests);
```

**From:** `src/Nethereum.CoreChain/Rpc/RpcDispatcher.cs`

### RpcHandlerBase

Base class for implementing RPC handlers:

```csharp
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

public class MyHandler : RpcHandlerBase
{
    public override string MethodName => "my_method";

    public override async Task<RpcResponseMessage> HandleAsync(
        RpcRequestMessage request,
        RpcContext context)
    {
        var param = GetParam<string>(request, 0);

        // Process request...

        return Success(request.Id, result);
    }
}
```

**From:** `src/Nethereum.CoreChain/Rpc/RpcHandlerBase.cs`

### RpcContext

Execution context for RPC handlers:

```csharp
public class RpcContext
{
    public IChainNode Node { get; }
    public int ChainId { get; }
    public IServiceProvider Services { get; }
}
```

**From:** `src/Nethereum.CoreChain/Rpc/RpcContext.cs`

## Standard RPC Handlers

CoreChain implements all standard Ethereum JSON-RPC methods:

### Network Methods

| Method | Handler | Description |
|--------|---------|-------------|
| `web3_clientVersion` | Web3ClientVersionHandler | Client version string |
| `net_version` | NetVersionHandler | Network version/chain ID |
| `eth_chainId` | EthChainIdHandler | Chain ID (hex) |

### Block Methods

| Method | Handler | Description |
|--------|---------|-------------|
| `eth_blockNumber` | EthBlockNumberHandler | Latest block number |
| `eth_getBlockByHash` | EthGetBlockByHashHandler | Block by hash |
| `eth_getBlockByNumber` | EthGetBlockByNumberHandler | Block by number |
| `eth_getBlockTransactionCountByHash` | EthGetBlockTransactionCountByHashHandler | Transaction count in block |
| `eth_getBlockTransactionCountByNumber` | EthGetBlockTransactionCountByNumberHandler | Transaction count in block |
| `eth_getBlockReceipts` | EthGetBlockReceiptsHandler | All receipts in block |

### Transaction Methods

| Method | Handler | Description |
|--------|---------|-------------|
| `eth_getTransactionByHash` | EthGetTransactionByHashHandler | Transaction by hash |
| `eth_getTransactionReceipt` | EthGetTransactionReceiptHandler | Transaction receipt |
| `eth_sendRawTransaction` | EthSendRawTransactionHandler | Submit signed transaction |

### State Methods

| Method | Handler | Description |
|--------|---------|-------------|
| `eth_getBalance` | EthGetBalanceHandler | Account balance |
| `eth_getCode` | EthGetCodeHandler | Contract bytecode |
| `eth_getStorageAt` | EthGetStorageAtHandler | Storage value |
| `eth_getTransactionCount` | EthGetTransactionCountHandler | Account nonce |

### Execution Methods

| Method | Handler | Description |
|--------|---------|-------------|
| `eth_call` | EthCallHandler | Execute call without state change |
| `eth_estimateGas` | EthEstimateGasHandler | Estimate gas for transaction |

### Gas Methods

| Method | Handler | Description |
|--------|---------|-------------|
| `eth_gasPrice` | EthGasPriceHandler | Current gas price |
| `eth_maxPriorityFeePerGas` | EthMaxPriorityFeePerGasHandler | Priority fee suggestion |
| `eth_feeHistory` | EthFeeHistoryHandler | Historical fee data |

### Log Methods

| Method | Handler | Description |
|--------|---------|-------------|
| `eth_getLogs` | EthGetLogsHandler | Query logs by filter |
| `eth_newFilter` | EthNewFilterHandler | Create log filter |
| `eth_newBlockFilter` | EthNewBlockFilterHandler | Create block filter |
| `eth_getFilterChanges` | EthGetFilterChangesHandler | Get filter updates |
| `eth_getFilterLogs` | EthGetFilterLogsHandler | Get all filter logs |
| `eth_uninstallFilter` | EthUninstallFilterHandler | Remove filter |

### Proof Methods

| Method | Handler | Description |
|--------|---------|-------------|
| `eth_getProof` | EthGetProofHandler | Merkle proof for account/storage |

**From:** `src/Nethereum.CoreChain/Rpc/CoreRpcHandlerExtensions.cs`

## Usage Example

```csharp
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.JsonRpc.Client.RpcMessages;
using Microsoft.Extensions.DependencyInjection;

// Setup storage
var services = new ServiceCollection()
    .AddSingleton<IBlockStore, InMemoryBlockStore>()
    .AddSingleton<ITransactionStore, InMemoryTransactionStore>()
    .AddSingleton<IReceiptStore, InMemoryReceiptStore>()
    .AddSingleton<IStateStore, InMemoryStateStore>()
    .AddSingleton<ILogStore, InMemoryLogStore>()
    .BuildServiceProvider();

// Setup RPC
var registry = new RpcHandlerRegistry();
registry.AddStandardHandlers();

var context = new RpcContext(node, chainId: 1337, services);
var dispatcher = new RpcDispatcher(registry, context);

// Handle requests
var request = new RpcRequestMessage(1, "eth_blockNumber");
var response = await dispatcher.DispatchAsync(request);

Console.WriteLine($"Block number: {response.Result}");
```

## Chain Node Interface

CoreChain defines the `IChainNode` interface that must be implemented by chain nodes:

```csharp
public interface IChainNode
{
    IBlockStore BlockStore { get; }
    ITransactionStore TransactionStore { get; }
    IReceiptStore ReceiptStore { get; }
    IStateStore StateStore { get; }
    ILogStore LogStore { get; }
    IFilterStore FilterStore { get; }

    Task<CallResult> CallAsync(CallInput callInput, string blockParameter = "latest");
    Task<BigInteger> EstimateGasAsync(CallInput callInput);
    Task<byte[]> SendRawTransactionAsync(byte[] signedTransaction);
}
```

**From:** `src/Nethereum.CoreChain/IChainNode.cs`

## Proof Service

Generate Merkle proofs for state verification:

```csharp
using Nethereum.CoreChain.Services;

var proofService = new ProofService(stateStore, trieNodeStore);

var proof = await proofService.GetProofAsync(
    address: "0x1234...",
    storageKeys: new[] { "0x0", "0x1" },
    blockNumber: 12345
);

// proof.AccountProof - Merkle proof for account
// proof.StorageProof - Merkle proofs for storage keys
```

**From:** `src/Nethereum.CoreChain/Services/ProofService.cs`

## State Node Data Service

Connect EVM execution to CoreChain state:

```csharp
using Nethereum.CoreChain.State;
using Nethereum.EVM.BlockchainState;

var nodeDataService = new StateStoreNodeDataService(stateStore, blockNumber);
var executionState = new ExecutionStateService(nodeDataService);

// EVM can now access CoreChain state
var program = new Program(bytecode, new ProgramContext(..., executionState));
```

**From:** `src/Nethereum.CoreChain/State/StateStoreNodeDataService.cs`

## Forking Support

Fork from existing chains for testing:

```csharp
using Nethereum.CoreChain.State;

var forkingService = new ForkingNodeDataService(
    rpcClient: web3.Client,
    blockNumber: 18000000
);

// State reads fetch from forked chain
// State writes go to local storage
```

**From:** `src/Nethereum.CoreChain/State/ForkingNodeDataService.cs`

## Related Packages

- **Nethereum.DevChain** - Development chain with additional RPC handlers
- **Nethereum.DevChain.Server** - HTTP JSON-RPC server
- **Nethereum.EVM** - EVM simulator
- **Nethereum.Merkle.Patricia** - Patricia trie implementation

## Additional Resources

- [Ethereum JSON-RPC Specification](https://ethereum.github.io/execution-apis/api-documentation/)
- [Nethereum Documentation](http://docs.nethereum.com)
