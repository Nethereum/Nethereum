# Nethereum.CoreChain.RocksDB

High-performance persistent storage for Nethereum CoreChain using RocksDB.

## Overview

This package provides RocksDB-backed implementations of all CoreChain storage interfaces, enabling persistent blockchain data storage with excellent read/write performance.

## Features

- **Persistent Storage**: Data survives application restarts
- **High Performance**: Optimized for blockchain workloads with column families
- **Atomic Writes**: WriteBatch support for transactional consistency
- **Bloom Filters**: Fast key existence checks
- **LZ4 Compression**: Reduced disk usage
- **Snapshot Support**: Efficient state snapshots for EVM execution

## Installation

```bash
dotnet add package Nethereum.CoreChain.RocksDB
```

## Quick Start

### Using Dependency Injection

```csharp
using Nethereum.CoreChain.RocksDB;

services.AddRocksDbStorage("./chaindata");

// Or with options
services.AddRocksDbStorage(new RocksDbStorageOptions
{
    DatabasePath = "./chaindata",
    BlockCacheSize = 256 * 1024 * 1024, // 256MB
    EnableStatistics = true
});
```

### Direct Usage

```csharp
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;

var options = new RocksDbStorageOptions
{
    DatabasePath = "./chaindata"
};

using var manager = new RocksDbManager(options);

var blockStore = new RocksDbBlockStore(manager);
var stateStore = new RocksDbStateStore(manager);
var trieStore = new RocksDbTrieNodeStore(manager);
```

## Storage Interfaces Implemented

| Interface | Implementation | Description |
|-----------|---------------|-------------|
| `IBlockStore` | `RocksDbBlockStore` | Block headers by hash/number |
| `ITransactionStore` | `RocksDbTransactionStore` | Transactions with location metadata |
| `IReceiptStore` | `RocksDbReceiptStore` | Transaction receipts |
| `IStateStore` | `RocksDbStateStore` | Account state, storage, code |
| `ILogStore` | `RocksDbLogStore` | Event logs with filtering |
| `ITrieNodeStore` | `RocksDbTrieNodeStore` | Patricia Merkle Trie nodes |
| `IFilterStore` | `RocksDbFilterStore` | Active log/block filters |

## Column Families

Data is organized into column families for optimal performance:

| Column Family | Data Type |
|---------------|-----------|
| `blocks` | Block headers |
| `block_numbers` | Block number to hash index |
| `transactions` | Signed transactions |
| `tx_by_block` | Block to transaction index |
| `receipts` | Transaction receipts |
| `logs` | Event logs |
| `log_by_block` | Block to log index |
| `log_by_address` | Address to log index |
| `state_accounts` | Account data |
| `state_storage` | Contract storage |
| `state_code` | Contract bytecode |
| `trie_nodes` | Patricia trie nodes |
| `filters` | Active filters |
| `metadata` | Chain metadata |

## Configuration Options

```csharp
var options = new RocksDbStorageOptions
{
    // Database location
    DatabasePath = "./chaindata",

    // Read performance (default: 128MB)
    BlockCacheSize = 128 * 1024 * 1024,

    // Write buffer size (default: 64MB)
    WriteBufferSize = 64 * 1024 * 1024,

    // Number of write buffers (default: 3)
    MaxWriteBufferNumber = 3,

    // Background compaction threads (default: 4)
    MaxBackgroundCompactions = 4,

    // Background flush threads (default: 2)
    MaxBackgroundFlushes = 2,

    // Bloom filter bits per key (default: 10)
    BloomFilterBitsPerKey = 10,

    // Enable statistics (default: false)
    EnableStatistics = false
};
```

## State Snapshots

The state store supports snapshots for EVM execution rollback:

```csharp
var stateStore = new RocksDbStateStore(manager);

// Create snapshot before execution
var snapshot = await stateStore.CreateSnapshotAsync();

try
{
    // Modify state
    snapshot.SetAccount(address, account);
    snapshot.SetStorage(address, slot, value);

    // Commit on success
    await stateStore.CommitSnapshotAsync(snapshot);
}
catch
{
    // Revert on failure (changes are discarded)
    await stateStore.RevertSnapshotAsync(snapshot);
}
finally
{
    snapshot.Dispose();
}
```

## Performance Tips

1. **Block Cache**: Increase `BlockCacheSize` for read-heavy workloads
2. **Write Buffer**: Increase `WriteBufferSize` for write-heavy workloads
3. **Compaction**: Tune `MaxBackgroundCompactions` based on CPU cores
4. **Bloom Filters**: Enabled by default for fast key lookups

## Integration with DevChain

Use with Nethereum.DevChain.Server for persistent development chains:

```bash
nethereum-devchain --datadir ./chaindata
```

## Related Packages

- **Nethereum.CoreChain** - Core blockchain infrastructure
- **Nethereum.DevChain** - Development chain handlers
- **Nethereum.DevChain.Server** - HTTP RPC server

## Requirements

- .NET 8.0 or .NET 9.0
- RocksDB native libraries (included via NuGet)
