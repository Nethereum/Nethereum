# Nethereum.AccountAbstraction.Bundler.RocksDB

RocksDB-backed mempool and reputation storage for the ERC-4337 bundler, so pending UserOperations and entity reputation survive a bundler process restart instead of living only in memory.

## Overview

Nethereum.AccountAbstraction.Bundler.RocksDB provides drop-in `IUserOpMempool` and reputation-service implementations for `Nethereum.AccountAbstraction.Bundler`, backed by a RocksDB instance instead of the package's default in-memory collections. Both stores share one `BundlerRocksDbManager`, which opens a single RocksDB database with eight column families - one each for pending, submitted, included and failed UserOperations, a (sender, EntryPoint, nonce) index, a transaction-hash mapping used to revert dropped bundles, reputation entries, and internal metadata (including a schema-version marker used to migrate the sender index in place when its on-disk layout changes).

Entries are RLP-encoded on write and decoded on read, so nothing survives only as an in-memory object: killing the process and reopening the same `DatabasePath` reconstructs the mempool and reputation state exactly as it was.

### Key Features

- **Persistent mempool**: `RocksDbUserOpMempool` implements the same `IUserOpMempool` mempool-admission rules (one pending op per (sender, EntryPoint, nonce), 10%+ fee-bump replacement, contiguous-nonce-chain bundle selection) as the in-memory backend, against RocksDB storage
- **Persistent reputation**: `RocksDbReputationStore` implements `IReputationStore` (an `IReputationService`) using the same throttle/ban thresholds and hourly 23/24 decay formula as the in-memory backend, with an in-process read cache in front of RocksDB
- **One-line DI registration**: `AddBundlerRocksDbStorage`, or `AddBundlerRocksDbMempoolOnly` / `AddBundlerRocksDbReputationOnly` to register just one of the two stores
- **Configurable RocksDB tuning**: `BundlerRocksDbOptions` exposes block cache size, background compaction/flush thread counts, bloom filter bits, write buffer sizing, mempool capacity, and entry TTL/retention windows

## Installation

```bash
dotnet add package Nethereum.AccountAbstraction.Bundler.RocksDB
```

### Dependencies

- **Nethereum.AccountAbstraction.Bundler** - `IUserOpMempool`, `IReputationService`, `MempoolEntry`, `ReputationEntry`, `ReputationConfig`
- **Nethereum.RLP** - entry serialization

## When to Use

Use this package instead of the core package's in-memory mempool/reputation collections when the bundler process needs its pending UserOperations and entity reputation to survive a restart - for example a long-running bundler service where a redeploy or crash should not silently drop pending operations or reset throttle/ban state.

## Usage Example

Extracted from `RocksDbMempoolTests.DataPersistsAcrossManagerRecreation` (`tests/Nethereum.AccountAbstraction.Bundler.RocksDB.UnitTests/RocksDbMempoolTests.cs`):

```csharp
var options = new BundlerRocksDbOptions { DatabasePath = testDbPath };
var manager = new BundlerRocksDbManager(options);
var mempool = new RocksDbUserOpMempool(manager, options);

await mempool.AddAsync(entry);

// Process restart: dispose the manager and reopen the same DatabasePath.
manager.Dispose();

var newManager = new BundlerRocksDbManager(options);
var newMempool = new RocksDbUserOpMempool(newManager, options);

var retrieved = await newMempool.GetAsync(entry.UserOpHash);
// retrieved is not null and retrieved.UserOpHash == entry.UserOpHash
```

## DI Registration

```csharp
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.Bundler.Reputation; // ReputationConfig
using Nethereum.AccountAbstraction.Bundler.RocksDB;

// Mempool + reputation, both backed by RocksDB
services.AddBundlerRocksDbStorage("./bundlerdata");

// Or with explicit tuning
services.AddBundlerRocksDbStorage(
    new BundlerRocksDbOptions
    {
        DatabasePath = "./bundlerdata",
        MaxMempoolSize = 20000,
        EntryTtl = TimeSpan.FromMinutes(30)
    },
    new ReputationConfig());

// Only one store persisted, the other left to whatever IUserOpMempool /
// IReputationService registration the host already has
services.AddBundlerRocksDbMempoolOnly(options);
services.AddBundlerRocksDbReputationOnly(options);
```

`AddBundlerRocksDbStorage` registers a shared `BundlerRocksDbManager` singleton plus `IUserOpMempool` (`RocksDbUserOpMempool`) and `IReputationStore` (`RocksDbReputationStore`). `AddBundlerRocksDbReputationOnly` reuses an already-registered `BundlerRocksDbManager` instead of adding a second one, so it composes with a prior `AddBundlerRocksDbMempoolOnly` call against the same database.

## API Reference

### BundlerRocksDbOptions

| Property | Default | Description |
|----------|---------|--------------|
| `DatabasePath` | `./bundlerdata` | RocksDB data directory |
| `BlockCacheSize` | 64 MB | LRU block cache size |
| `MaxBackgroundCompactions` | 2 | Background compaction threads |
| `MaxBackgroundFlushes` | 1 | Background flush threads |
| `BloomFilterBitsPerKey` | 10 | Bloom filter bits per key |
| `EnableStatistics` | `false` | Enables RocksDB statistics collection |
| `WriteBufferSize` | 32 MB | Memtable size before flush |
| `MaxWriteBufferNumber` | 2 | Number of memtables kept in memory |
| `MaxMempoolSize` | 10000 | Maximum pending UserOperations before `AddAsync` returns `MempoolAddOutcome.RejectedFull` |
| `EntryTtl` | 30 minutes | Retention window for pending entries swept by `PruneAsync` |
| `IncludedEntryRetention` | 5 minutes | Retention window for included/failed entries swept by `PruneAsync` |

### BundlerRocksDbManager

Owns the RocksDB database handle and its eight column families (`CF_USEROP_PENDING`, `CF_USEROP_SUBMITTED`, `CF_USEROP_INCLUDED`, `CF_USEROP_FAILED`, `CF_SENDER_INDEX`, `CF_TX_MAPPING`, `CF_REPUTATION`, `CF_METADATA`). Exposes `Get`/`Put`/`Delete`/`KeyExists`/`CreateIterator` per column family, plus `CreateWriteBatch`, `Write`, `CreateSnapshot`, `Flush` and `Compact`. Implements `IDisposable`.

### RocksDbUserOpMempool

`IUserOpMempool` implementation. Constructor: `RocksDbUserOpMempool(BundlerRocksDbManager manager, BundlerRocksDbOptions options)`. On construction it checks the sender-index schema version stored in `CF_METADATA` and rebuilds the (sender, EntryPoint, nonce) index from the stored UserOperations if it is missing or stale, so an older on-disk layout self-heals on open.

### RocksDbReputationStore

`IReputationStore` (an `IReputationService`) implementation. Constructor: `RocksDbReputationStore(BundlerRocksDbManager manager, ReputationConfig? config = null)`. Loads all reputation entries into an in-process cache on construction and keeps the cache and RocksDB in sync on every write.

## Related Packages

### Dependencies
- **[Nethereum.AccountAbstraction.Bundler](../Nethereum.AccountAbstraction.Bundler/README.md)** - `IUserOpMempool`, `IReputationService` and the in-memory default implementations this package replaces

### See Also
- **[Nethereum.AccountAbstraction.Bundler.RpcServer](../Nethereum.AccountAbstraction.Bundler.RpcServer/README.md)** - JSON-RPC surface for the bundler this package provides storage for
- **[Nethereum.AccountAbstraction](../Nethereum.AccountAbstraction/README.md)** - Core ERC-4337 types and client-side usage

## Additional Resources

- [ERC-4337: Account Abstraction](https://eips.ethereum.org/EIPS/eip-4337)
- [Nethereum Documentation](https://docs.nethereum.com)
