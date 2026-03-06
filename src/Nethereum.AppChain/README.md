# Nethereum.AppChain

> **PREVIEW** — This package is in preview. APIs may change between releases.

A Nethereum AppChain is a lightweight, domain-specific extension layer for Ethereum L1/L2.

It is a chain — with blocks, transactions, a full EVM, and cryptographic state roots — but purpose-built for your domain.

You control the operation. You define the logic. Your users interact with it like any Ethereum network.

The difference is that it does not exist in isolation.

It extends Ethereum through bidirectional messaging:

- L1/L2 events can trigger AppChain logic
- AppChain execution can trigger L1/L2 contracts

State roots are periodically anchored to Ethereum, making the full history tamper-evident and independently verifiable.

This makes it an extension of Ethereum — not a departure from it.

Core settlement state — assets, identity, governance — remains on L1/L2. The AppChain manages structured, high-frequency, domain-specific state that does not belong on L1/L2 but still requires:

- Public readability
- Cryptographic verifiability
- Independent synchronisation

A business extends its Ethereum presence with an AppChain the same way it extends its storefront with a backend. The difference is this backend is public. Anyone can read the state, verify the logic, sync the history, and check the anchoring. The business operates it, the public verifies it.

## Built on Nethereum. Fully Ethereum-compatible.

A Nethereum AppChain runs a full EVM and exposes a standard Ethereum JSON-RPC interface.

- Deploy contracts with Hardhat, Foundry, or Remix
- Interact using ethers.js, web3.js, viem, or Nethereum
- Use standard wallets and tooling

If you can deploy to Sepolia, you can deploy to your AppChain. Zero new tooling.

## Overview

This package provides the foundational `IAppChain` interface and implementation. It manages the chain lifecycle from genesis block initialisation through block and state queries, with pluggable storage backends.

Genesis block construction includes account pre-funding, CREATE2 factory deployment (EIP-1014), and optional MUD World framework deployment. All storage operations are delegated through interfaces, enabling in-memory, RocksDB, or custom implementations.

AppChain serves as the foundation that Sequencer and Sync packages build upon, providing the shared chain state that both producers and followers access.

### Key Features

- **IAppChain Abstraction**: Unified interface for block, state, and transaction queries
- **Genesis Block Builder**: Configurable genesis with pre-funded accounts and contract deployments
- **CREATE2 Factory**: Pre-deploys canonical CREATE2 factory at `0x4e59b44847b379578588920cA78FbF26c0B4956C`
- **MUD World Integration**: Optional deployment of MUD framework contracts during genesis
- **Pluggable Storage**: Supports InMemory, RocksDB, or custom `IBlockStore`/`IStateStore` implementations
- **Lazy Initialization**: Validates existing genesis on first use, allows pre-populated state

## Installation

```bash
dotnet add package Nethereum.AppChain
```

### Dependencies

- **Nethereum.CoreChain** - Storage abstractions (`IBlockStore`, `IStateStore`), state root calculation, block header encoding
- **Nethereum.Model** - `BlockHeader`, transactions, receipts, and account structures
- **Nethereum.Util** - Keccak hashing and address utilities
- **Nethereum.RLP** - RLP encoding for state root computation

## Key Concepts

### IAppChain Interface

The central abstraction representing a running chain. Provides access to all storage layers and common query methods:

```csharp
public interface IAppChain
{
    AppChainConfig Config { get; }
    IBlockStore Blocks { get; }
    IStateStore State { get; }
    ITransactionStore Transactions { get; }
    IReceiptStore Receipts { get; }
    ILogStore Logs { get; }

    Task InitializeAsync();
    Task<BigInteger> GetBlockNumberAsync();
    Task<BigInteger> GetBalanceAsync(string address);
    Task<BigInteger> GetNonceAsync(string address);
    Task<byte[]> GetCodeAsync(string address);
}
```

### Genesis Block Construction

`AppChainGenesisBuilder` constructs the genesis block by applying pre-funded accounts to the initial state, computing the state root via Patricia trie, and encoding the block header:

```csharp
var builder = new AppChainGenesisBuilder(stateStore, trieNodeStore);
builder.AddPrefundedAccount(ownerAddress, initialBalance);
var genesis = await builder.BuildGenesisBlockAsync(chainConfig);
```

### CREATE2 Factory

The canonical CREATE2 factory at address `0x4e59b44847b379578588920cA78FbF26c0B4956C` is pre-deployed during genesis. This enables deterministic contract address computation using `CREATE2`, which is essential for counterfactual account deployment in ERC-4337 Account Abstraction.

## Quick Start

```csharp
using Nethereum.AppChain;
using Nethereum.CoreChain.Storage.InMemory;

var config = AppChainConfig.CreateWithName("MyChain", chainId: 420420);
config.SequencerAddress = sequencerAddress;

var appChain = new AppChain(config,
    new InMemoryBlockStore(),
    new InMemoryTransactionStore(),
    new InMemoryReceiptStore(),
    new InMemoryLogStore(),
    new InMemoryStateStore(),
    new InMemoryTrieNodeStore());

await appChain.InitializeAsync();
var blockNumber = await appChain.GetBlockNumberAsync();
```

## Usage Examples

### Example 1: Create AppChain with Pre-Funded Accounts

```csharp
using Nethereum.AppChain;

var config = AppChainConfig.CreateWithName("TestChain", chainId: 31337);
var genesisOptions = new GenesisOptions
{
    PrefundedAddresses = new[] { ownerAddress, userAddress },
    PrefundBalance = Web3.Convert.ToWei(1000),
    DeployCreate2Factory = true
};

var appChain = new AppChain(config, blockStore, txStore, receiptStore,
    logStore, stateStore, trieNodeStore);
await appChain.ApplyGenesisStateAsync(genesisOptions);
```

### Example 2: Query Chain State

```csharp
// Get latest block
var block = await appChain.GetLatestBlockAsync();

// Check account balance
var balance = await appChain.GetBalanceAsync(userAddress);

// Read contract storage
var storageValue = await appChain.GetStorageAtAsync(contractAddress, slot);

// Get transaction receipt
var receipt = await appChain.GetTransactionReceiptAsync(txHash);
```

### Example 3: Configure with RocksDB Storage

```csharp
using Nethereum.CoreChain.RocksDB;

var rocksDb = new RocksDbManager(dbPath);
var appChain = new AppChain(config,
    new RocksDbBlockStore(rocksDb),
    new RocksDbTransactionStore(rocksDb),
    new RocksDbReceiptStore(rocksDb),
    new RocksDbLogStore(rocksDb),
    new RocksDbStateStore(rocksDb),
    new InMemoryTrieNodeStore());

await appChain.InitializeAsync();
```

## API Reference

### AppChain

Core chain implementation managing storage and state.

```csharp
public class AppChain : IAppChain
{
    public AppChain(AppChainConfig config, IBlockStore blocks,
        ITransactionStore transactions, IReceiptStore receipts,
        ILogStore logs, IStateStore state, ITrieNodeStore trieNodes);

    public Task InitializeAsync();
    public Task ApplyGenesisStateAsync(GenesisOptions options);
    public Task<BigInteger> GetBlockNumberAsync();
    public Task<BlockHeader?> GetBlockByNumberAsync(BigInteger number);
    public Task<BigInteger> GetBalanceAsync(string address);
    public Task<BigInteger> GetNonceAsync(string address);
    public Task<byte[]> GetCodeAsync(string address);
    public Task<byte[]> GetStorageAtAsync(string address, BigInteger slot);
}
```

### AppChainConfig

Configuration extending `ChainConfig` with AppChain-specific settings.

Key properties:
- `AppChainName` - Human-readable chain name
- `SequencerAddress` - Authorized block producer address
- `WorldAddress` - Deployed MUD World contract address
- `GenesisHash` - Expected genesis block hash for validation

### AppChainGenesisBuilder

Constructs genesis blocks with pre-funded accounts and state root computation.

- `AddPrefundedAccount(string address, BigInteger balance)` - Add pre-funded account
- `BuildGenesisBlockAsync(ChainConfig config)` - Build genesis with computed state root

### Create2FactoryGenesisBuilder

Deploys the canonical CREATE2 factory during genesis.

- `DeployCreate2FactoryAsync(IStateStore state)` - Deploy factory contract
- `CalculateCreate2Address(address, salt, initCodeHash)` - Compute deterministic address

## Related Packages

### Used By (Consumers)
- **[Nethereum.AppChain.Sequencer](../Nethereum.AppChain.Sequencer/README.md)** - Block production and transaction ordering
- **[Nethereum.AppChain.Sync](../Nethereum.AppChain.Sync/README.md)** - Follower synchronization
- **[Nethereum.AppChain.Server](../Nethereum.AppChain.Server/README.md)** - HTTP JSON-RPC server

### Dependencies
- **[Nethereum.CoreChain](../Nethereum.CoreChain/README.md)** - Storage interfaces and state root calculation
- **[Nethereum.Model](../Nethereum.Model/README.md)** - Block and transaction data structures

## Additional Resources

- [EIP-1014: CREATE2](https://eips.ethereum.org/EIPS/eip-1014)
- [MUD Framework](https://mud.dev)
- [Nethereum Documentation](https://docs.nethereum.com)
