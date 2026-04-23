# Nethereum.CoreChain

Core blockchain infrastructure for state management, block production, transaction processing, and JSON-RPC handling. Provides the foundational components for building local Ethereum nodes and development chains. Supports EVM execution up to the Prague hardfork.

## Overview

Nethereum.CoreChain provides:
- **Block Production** - Create and validate blocks from pending transactions
- **State Management** - Account state, storage, and trie-based state roots
- **Storage Interfaces** - Pluggable block, transaction, receipt, log, state, and filter stores
- **RPC Framework** - Extensible JSON-RPC handler system with dispatcher and registry
- **Standard RPC Handlers** - Full implementation of Ethereum JSON-RPC methods
- **Proof Generation** - Merkle proofs for account and storage verification (eth_getProof)
- **Forking Support** - Fork state from remote RPC endpoints

## Installation

```bash
dotnet add package Nethereum.CoreChain
```

## Dependencies

- Nethereum.Model
- Nethereum.Merkle.Patricia
- Nethereum.EVM
- Nethereum.Hex
- Nethereum.RPC
- Nethereum.Signer
- Nethereum.Util
- Nethereum.JsonRpc.Client
- Microsoft.Extensions.Logging.Abstractions

## Storage Interfaces

CoreChain defines pluggable interfaces for all blockchain data. Implementations include in-memory (built-in), SQLite (Nethereum.DevChain), and RocksDB (Nethereum.CoreChain.RocksDB).

### IBlockStore

```csharp
public interface IBlockStore
{
    Task<BlockHeader> GetByHashAsync(byte[] hash);
    Task<BlockHeader> GetByNumberAsync(BigInteger number);
    Task<BlockHeader> GetLatestAsync();
    Task<BigInteger> GetHeightAsync();
    Task SaveAsync(BlockHeader header, byte[] blockHash);
    Task<bool> ExistsAsync(byte[] hash);
    Task<byte[]> GetHashByNumberAsync(BigInteger number);
    Task UpdateBlockHashAsync(BigInteger blockNumber, byte[] newHash);
    Task DeleteByNumberAsync(BigInteger blockNumber);
}
```

### ITransactionStore

```csharp
public interface ITransactionStore
{
    Task<ISignedTransaction> GetByHashAsync(byte[] txHash);
    Task<List<ISignedTransaction>> GetByBlockHashAsync(byte[] blockHash);
    Task<List<byte[]>> GetHashesByBlockHashAsync(byte[] blockHash);
    Task<List<ISignedTransaction>> GetByBlockNumberAsync(BigInteger blockNumber);
    Task SaveAsync(ISignedTransaction tx, byte[] blockHash, int txIndex, BigInteger blockNumber);
    Task<TransactionLocation> GetLocationAsync(byte[] txHash);
    Task DeleteByBlockNumberAsync(BigInteger blockNumber);
}

public class TransactionLocation
{
    public byte[] BlockHash { get; set; }
    public BigInteger BlockNumber { get; set; }
    public int TransactionIndex { get; set; }
}
```

### IReceiptStore

```csharp
public interface IReceiptStore
{
    Task SaveAsync(Receipt receipt, byte[] txHash, byte[] blockHash,
                   BigInteger blockNumber, int txIndex, BigInteger gasUsed,
                   string contractAddress, BigInteger effectiveGasPrice);
    Task<ReceiptInfo> GetByTxHashAsync(byte[] txHash);
    Task<List<ReceiptInfo>> GetByBlockHashAsync(byte[] blockHash);
    Task<List<ReceiptInfo>> GetByBlockNumberAsync(BigInteger blockNumber);
    Task DeleteByBlockNumberAsync(BigInteger blockNumber);
}
```

### IStateStore

```csharp
public interface IStateStore
{
    // Account state
    Task<AccountState> GetAccountAsync(string address);
    Task SaveAccountAsync(string address, AccountState state);
    Task<List<string>> GetAllAccountsAsync();

    // Storage slots
    Task<byte[]> GetStorageAsync(string address, byte[] key);
    Task SaveStorageAsync(string address, byte[] key, byte[] value);

    // Code
    Task<byte[]> GetCodeByHashAsync(byte[] codeHash);
    Task SaveCodeAsync(byte[] codeHash, byte[] code);

    // Snapshots
    Task<int> TakeSnapshotAsync();
    Task RevertToSnapshotAsync(int snapshotId);
}
```

### ILogStore

```csharp
public interface ILogStore
{
    Task SaveLogsAsync(List<Log> logs, byte[] txHash, byte[] blockHash,
                       BigInteger blockNumber, int txIndex);
    Task SaveBlockBloomAsync(BigInteger blockNumber, byte[] bloom);
    Task<List<FilteredLog>> GetLogsAsync(LogFilter filter);
    Task<List<FilteredLog>> GetLogsByTxHashAsync(byte[] txHash);
    Task<List<FilteredLog>> GetLogsByBlockHashAsync(byte[] blockHash);
    Task<List<FilteredLog>> GetLogsByBlockNumberAsync(BigInteger blockNumber);
    Task DeleteByBlockNumberAsync(BigInteger blockNumber);
}
```

## In-Memory Implementations

CoreChain provides in-memory implementations for all storage interfaces:

```csharp
using Nethereum.CoreChain.Storage.InMemory;

var blockStore = new InMemoryBlockStore();
var txStore = new InMemoryTransactionStore(blockStore);
var receiptStore = new InMemoryReceiptStore();
var logStore = new InMemoryLogStore();
var stateStore = new InMemoryStateStore();
var filterStore = new InMemoryFilterStore();
var trieNodeStore = new InMemoryTrieNodeStore();
```

## Block Production

The `BlockProducer` creates blocks from pending transactions with full EVM execution:

```csharp
using Nethereum.CoreChain;

var producer = new BlockProducer(
    blockStore, transactionStore, receiptStore,
    logStore, stateStore, trieNodeStore, chainConfig);

var result = await producer.ProduceBlockAsync(pendingTransactions);

// Result contains:
// - Block header with state root, receipts root, transactions root
// - Processed transactions and receipts
// - Gas used and logs bloom
```

Before executing transactions in a block, the producer runs the
pre-block system calls required by recent forks:

- **EIP-4788** (Cancun+) — if the block carries a
  `ParentBeaconBlockRoot`, stamp it into the beacon-roots contract at
  `0x000F3df6D732807Ef1319fB7B8bB8522d0Beac02`.
- **EIP-2935** (Prague+) — stamp the parent block's hash into the
  history contract at
  `0x0000F90827F1C53a10cb7A02335B175320002935`, slot
  `parentBlockNumber % 8191`. This is what the BLOCKHASH opcode reads
  from (no separate block-hash channel — the ancestor history lives in
  ordinary storage and is covered by the storage witness).

## Hardfork Configuration

`TransactionProcessor` (invoked by `BlockProducer`) requires a
`HardforkConfig` at construction. Use `ChainConfig.GetHardforkConfig()`
to resolve from your chain configuration:

```csharp
using Nethereum.EVM;
using Nethereum.EVM.Precompiles;   // DefaultMainnetHardforkRegistry

var hardforkConfig = chainConfig.GetHardforkConfig();
var txProcessor    = new TransactionProcessor(
    stateStore, blockStore, chainConfig, txVerifier, hardforkConfig);
```

For multi-chain scenarios (forked mainnet replay, L2 with mainnet
activations, historical block replay), resolve the fork per-block
via `ChainActivationsRegistry` and pick the `HardforkConfig` from
`DefaultMainnetHardforkRegistry`:

```csharp
var fork = ChainActivationsRegistry.Instance.ResolveAt(
    chainId: 1, blockNumber: 19_000_000, timestamp: 1_710_000_000);
var cfg  = DefaultMainnetHardforkRegistry.Instance.Get(fork);
```

## State-Root and Block-Root Calculators

`Nethereum.CoreChain` ships the concrete implementations of
`IStateRootCalculator` and `IBlockRootCalculator` declared in
`Nethereum.EVM.Core`:

- `PatriciaStateRootCalculator` — computes the MPT state root over
  an `IStateStore`. Used by witness verification and block production.
- `BinaryIncrementalStateRootCalculator` — EIP-7864 binary trie state
  root with incremental dirty-account updates and hash caching.
  Supports Blake3 and Poseidon (BN254 Montgomery) hash providers.
- `PatriciaBlockRootCalculator` — transactions-root / receipts-root /
  withdrawals-root computation over RLP-encoded items.
- `PatriciaMerkleTreeBuilder` — shared trie-building helper.
- `StatelessStateRootCalculator` — witness-only variant that avoids
  touching the full state store; useful inside stateless verifiers.
- `WitnessProofVerifier` — validates account and storage Merkle
  proofs against a supplied pre-state root.
- `BinaryProofService` — generates and verifies binary trie Merkle
  proofs for accounts and storage slots (`IProofService`).
  Returns `BinaryAccountProofResult` / `BinaryStorageProofResult`.

## RPC Framework

### RpcHandlerRegistry

Central registry for RPC method handlers:

```csharp
using Nethereum.CoreChain.Rpc;

var registry = new RpcHandlerRegistry();

// Register all standard Ethereum handlers
registry.AddStandardHandlers();

// Register custom handler
registry.Register(new MyCustomHandler());

// Override an existing handler
registry.Override(new MyCustomEthCallHandler());
```

### RpcDispatcher

Routes JSON-RPC requests to registered handlers:

```csharp
var context = new RpcContext(chainNode, chainId, serviceProvider);
var dispatcher = new RpcDispatcher(registry, context, logger);

// Single request
var response = await dispatcher.DispatchAsync(request);

// Batch request
var responses = await dispatcher.DispatchBatchAsync(requests);
```

### Custom RPC Handler

```csharp
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

public class MyHandler : RpcHandlerBase
{
    public override string MethodName => "my_method";

    public override async Task<RpcResponseMessage> HandleAsync(
        RpcRequestMessage request, RpcContext context)
    {
        var param = GetParam<string>(request, 0);
        var result = await DoWorkAsync(param, context.Node);
        return Success(request.Id, result);
    }
}
```

## Standard RPC Handlers

CoreChain implements the full Ethereum JSON-RPC specification:

### Network

| Method | Description |
|--------|-------------|
| `web3_clientVersion` | Client version string |
| `web3_sha3` | Keccak-256 hash |
| `net_version` | Network version |
| `net_listening` | Listening status |
| `net_peerCount` | Peer count |
| `eth_chainId` | Chain ID (hex) |
| `eth_syncing` | Sync status |
| `eth_mining` | Mining status |
| `eth_coinbase` | Coinbase address |

### Blocks

| Method | Description |
|--------|-------------|
| `eth_blockNumber` | Latest block number |
| `eth_getBlockByHash` | Block by hash |
| `eth_getBlockByNumber` | Block by number |
| `eth_getBlockTransactionCountByHash` | Transaction count in block |
| `eth_getBlockTransactionCountByNumber` | Transaction count in block |
| `eth_getBlockReceipts` | All receipts in block |

### Transactions

| Method | Description |
|--------|-------------|
| `eth_sendRawTransaction` | Submit signed transaction |
| `eth_getTransactionByHash` | Transaction by hash |
| `eth_getTransactionByBlockHashAndIndex` | Transaction by block and index |
| `eth_getTransactionReceipt` | Transaction receipt |

### State

| Method | Description |
|--------|-------------|
| `eth_getBalance` | Account balance |
| `eth_getCode` | Contract bytecode |
| `eth_getStorageAt` | Storage slot value |
| `eth_getTransactionCount` | Account nonce |

### Execution

| Method | Description |
|--------|-------------|
| `eth_call` | Execute call (read-only) |
| `eth_estimateGas` | Estimate gas |
| `eth_createAccessList` | Generate access list |

### Gas and Fees

| Method | Description |
|--------|-------------|
| `eth_gasPrice` | Current gas price |
| `eth_maxPriorityFeePerGas` | Priority fee suggestion |
| `eth_feeHistory` | Historical fee data |

### Logs and Filters

| Method | Description |
|--------|-------------|
| `eth_getLogs` | Query logs by filter |
| `eth_newFilter` | Create log filter |
| `eth_newBlockFilter` | Create block filter |
| `eth_getFilterChanges` | Get filter updates |
| `eth_getFilterLogs` | Get all filter logs |
| `eth_uninstallFilter` | Remove filter |

### Proofs

| Method | Description |
|--------|-------------|
| `eth_getProof` | Merkle proof for account and storage |

## Chain Node Interface

CoreChain defines the `IChainNode` interface for chain implementations:

```csharp
public interface IChainNode
{
    IBlockStore BlockStore { get; }
    ITransactionStore TransactionStore { get; }
    IReceiptStore ReceiptStore { get; }
    IStateStore StateStore { get; }
    ILogStore LogStore { get; }
    IFilterStore FilterStore { get; }
    ITrieNodeStore TrieNodeStore { get; }
    DevChainConfig Config { get; }

    Task<CallResult> CallAsync(CallInput callInput, string blockParameter = "latest");
    Task<BigInteger> EstimateGasAsync(CallInput callInput);
    Task<byte[]> SendRawTransactionAsync(byte[] signedTransaction);
    // ... block, transaction, and state accessors
}
```

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
```

## Forking Support

Fork state from a remote chain for local testing:

```csharp
using Nethereum.CoreChain.State;

var forkingService = new ForkingNodeDataService(
    rpcClient: web3.Client,
    blockNumber: 18000000
);

// Reads fetch from fork source, writes go to local state
```

## Related Packages

- **Nethereum.DevChain** - Development chain with mining, tracing, and SQLite storage
- **Nethereum.DevChain.Server** - HTTP JSON-RPC server (dotnet tool)
- **Nethereum.CoreChain.RocksDB** - RocksDB storage adapter for production use
- **Nethereum.EVM** - EVM simulator
- **Nethereum.Merkle.Patricia** - Patricia trie implementation

## Additional Resources

- [Ethereum JSON-RPC Specification](https://ethereum.github.io/execution-apis/api-documentation/)
- [Nethereum Documentation](http://docs.nethereum.com)
