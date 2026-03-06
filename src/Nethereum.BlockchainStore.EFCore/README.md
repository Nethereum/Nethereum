# Nethereum.BlockchainStore.EFCore

Entity Framework Core base library for persisting Ethereum blockchain data (blocks, transactions, logs, contracts, internal transactions) to relational databases.

## Overview

Nethereum.BlockchainStore.EFCore provides the database-agnostic EF Core layer that sits between the `Nethereum.BlockchainProcessing` entity models and a specific database provider (PostgreSQL, SQL Server, SQLite, etc.). It defines the `DbContext`, entity type configurations, and repository implementations that read and write blockchain data using EF Core.

This package is not used directly by most applications. Instead, use a database-specific package such as `Nethereum.BlockchainStore.Postgres` which inherits from this base and configures the provider.

### Key Features

- `BlockchainDbContextBase` abstract DbContext with DbSets for all blockchain entities
- Entity builder configurations defining column types, lengths, indexes, and constraints
- Repository implementations for `IBlockRepository`, `ITransactionRepository`, `ITransactionLogRepository`, `IContractRepository`, `IAddressTransactionRepository`, `IInternalTransactionRepository`
- `BlockchainStoreRepositoryFactory` implementing `IBlockchainStoreRepositoryFactory` for the block storage processor pipeline
- `IBlockchainDbContextFactory` abstraction allowing short-lived context creation per operation
- Reorg handling via `EfCoreReorgHandler` (marks blocks, transactions, and logs as non-canonical)
- DI registration via `AddBlockchainRepositories()` extension method

## Installation

```bash
dotnet add package Nethereum.BlockchainStore.EFCore
```

Targets `net8.0` and `net10.0`. Uses EF Core 8.x on net8.0 and EF Core 10.x on net10.0.

### Dependencies

- **Nethereum.Web3** - RPC DTOs used in repository upsert methods (e.g. `UpsertBlockAsync(RPC.Eth.DTOs.Block)`)
- **Microsoft.EntityFrameworkCore** - ORM framework
- **Microsoft.EntityFrameworkCore.Relational** - Relational database abstractions
- **Microsoft.EntityFrameworkCore.Tools** - Design-time migration tooling (private asset)
- **Microsoft.Extensions.Configuration** - Connection string resolution

## Key Concepts

### BlockchainDbContextBase

Abstract `DbContext` subclass that defines all `DbSet` properties and applies entity configurations via `OnModelCreating`. Database-specific packages inherit from this and set the provider and `ColumnTypeForUnlimitedText` (e.g. `"text"` for Postgres, `"nvarchar(max)"` for SQL Server).

```csharp
public class PostgresBlockchainDbContext : BlockchainDbContextBase
{
    public PostgresBlockchainDbContext(string connectionString)
    {
        ColumnTypeForUnlimitedText = "text";
        _connectionString = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(_connectionString);
    }
}
```

DbSets provided: `Blocks`, `Transactions`, `TransactionLogs`, `Contracts`, `AddressTransactions`, `InternalTransactions`, `TransactionVmStacks`, `AccountStates`, `ChainStates`, `BlockProgress`, `InternalTransactionBlockProgress`.

### IBlockchainDbContextFactory

Repository implementations create a new `DbContext` per operation to avoid long-lived context issues in background processing:

```csharp
public interface IBlockchainDbContextFactory
{
    BlockchainDbContextBase CreateContext();
}
```

Each repository method calls `_contextFactory.CreateContext()` in a `using` block, performs the query or upsert, and disposes the context.

### Entity Builders

Entity type configurations define column constraints using extension methods:

- `IsHash()` - `HasMaxLength(67)` for 32-byte hex hashes (66 chars + "0x")
- `IsAddress()` - `HasMaxLength(43)` for 20-byte hex addresses (42 chars + "0x")
- `IsBigInteger()` - `HasMaxLength(100)` for string-encoded large numbers (gas values, balances)
- `IsUnlimitedText(columnType)` - `HasColumnType("text")` or `"nvarchar(max)"` for input data, error messages, logs bloom

Numeric fields that were migrated to `long` (BlockNumber, Timestamp, TransactionIndex, LogIndex, Nonce, TransactionType) no longer use `IsBigInteger()`.

## Quick Start

Most applications use this package indirectly through `Nethereum.BlockchainStore.Postgres`:

```csharp
using Nethereum.BlockchainStore.Postgres;

builder.Services.AddPostgresBlockchainStorage(connectionString);
```

This registers `IBlockchainDbContextFactory` and all repository implementations via `AddBlockchainRepositories()`.

## Usage Examples

### Register Repositories via DI

```csharp
using Nethereum.BlockchainStore.EFCore;

services.AddSingleton<IBlockchainDbContextFactory>(
    new MyDbContextFactory(connectionString));

services.AddBlockchainRepositories();
```

`AddBlockchainRepositories()` registers:
- `IBlockchainStoreRepositoryFactory` as `BlockchainStoreRepositoryFactory`
- `IBlockProgressRepositoryFactory` as `BlockchainStoreRepositoryFactory`
- `IChainStateRepositoryFactory` as `BlockchainStoreRepositoryFactory`
- Individual repositories: `IBlockRepository`, `ITransactionRepository`, `ITransactionLogRepository`, `IContractRepository`, `IAddressTransactionRepository`, `IBlockProgressRepository`, `IChainStateRepository`

### Use with Block Storage Processor

```csharp
var dbContextFactory = new PostgresBlockchainDbContextFactory(connectionString);
var repoFactory = new BlockchainStoreRepositoryFactory(dbContextFactory);

var steps = new BlockStorageProcessingSteps(repoFactory);
var orchestrator = new BlockCrawlOrchestrator(web3.Eth, steps);

var progressRepo = repoFactory.CreateBlockProgressRepository();
var processor = new BlockchainProcessor(orchestrator, progressRepo, lastConfirmedBlockService);

await processor.ExecuteAsync(cancellationToken);
```

### Custom Database Provider

To add support for a new database, inherit from `BlockchainDbContextBase`:

```csharp
public class SqliteBlockchainDbContext : BlockchainDbContextBase
{
    private readonly string _connectionString;

    public SqliteBlockchainDbContext(string connectionString)
    {
        ColumnTypeForUnlimitedText = "TEXT";
        _connectionString = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite(_connectionString);
    }
}
```

Then implement `IBlockchainDbContextFactory`:

```csharp
public class SqliteBlockchainDbContextFactory : IBlockchainDbContextFactory
{
    private readonly string _connectionString;

    public SqliteBlockchainDbContextFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public BlockchainDbContextBase CreateContext()
    {
        return new SqliteBlockchainDbContext(_connectionString);
    }
}
```

## API Reference

### BlockchainDbContextBase

Abstract `DbContext` with blockchain entity DbSets.

Key DbSets:
- `DbSet<Block> Blocks`
- `DbSet<Transaction> Transactions`
- `DbSet<TransactionLog> TransactionLogs`
- `DbSet<Contract> Contracts`
- `DbSet<InternalTransaction> InternalTransactions`
- `DbSet<BlockProgress> BlockProgress`
- `DbSet<ChainState> ChainStates`

### BlockchainStoreRepositoryFactory

Implements `IBlockchainStoreRepositoryFactory`, `IBlockProgressRepositoryFactory`, `IChainStateRepositoryFactory`.

Key methods:
- `CreateBlockRepository() : IBlockRepository`
- `CreateTransactionRepository() : ITransactionRepository`
- `CreateTransactionLogRepository() : ITransactionLogRepository`
- `CreateContractRepository() : IContractRepository`
- `CreateInternalTransactionRepository() : IInternalTransactionRepository`
- `CreateBlockProgressRepository() : IBlockProgressRepository`
- `CreateChainStateRepository() : IChainStateRepository`
- `CreateReorgHandler() : IReorgHandler`

### BlockRepository

- `FindByBlockNumberAsync(HexBigInteger blockNumber) : Task<IBlockView>`
- `GetMaxBlockNumberAsync() : Task<BigInteger?>`
- `UpsertBlockAsync(Block source) : Task`
- `MarkNonCanonicalAsync(BigInteger blockNumber) : Task`

### Entity Builders

| Builder | Table | Key Indexes |
|---------|-------|-------------|
| `BlockEntityBuilder` | Blocks | (BlockNumber, Hash) unique; BlockNumber; Hash; ParentHash; (IsCanonical, BlockNumber) |
| `TransactionEntityBuilder` | Transactions | (BlockNumber, Hash) unique; Hash; AddressFrom; AddressTo; NewContractAddress; (IsCanonical, BlockNumber) |
| `TransactionLogEntityBuilder` | TransactionLogs | (TransactionHash, LogIndex) unique; BlockNumber; Address; EventHash; (IsCanonical, BlockNumber) |
| `InternalTransactionEntityBuilder` | InternalTransactions | (TransactionHash, Index) unique; BlockNumber |
| `ContractEntityBuilder` | Contracts | Address unique |

## Related Packages

### Used By (Consumers)

- **Nethereum.BlockchainStore.Postgres** - PostgreSQL provider inheriting `BlockchainDbContextBase`
- **Nethereum.BlockchainStorage.Processors.Postgres** - Hosted services that create `BlockchainStoreRepositoryFactory` to run the processing pipeline
- **Nethereum.Explorer** - Reads from `IBlockchainDbContextFactory` to display indexed data

### Dependencies

- **Nethereum.BlockchainProcessing** - Entity models (`Block`, `Transaction`, `TransactionLog`, etc.) and repository interfaces
- **Nethereum.Web3** - RPC DTO types used in upsert method signatures

### See Also

- [Nethereum.BlockchainStore.Postgres](../Nethereum.BlockchainStore.Postgres/README.md) - PostgreSQL implementation
- [Nethereum.BlockchainStorage.Processors.Postgres](../Nethereum.BlockchainStorage.Processors.Postgres/README.md) - Hosted processing services
- [Nethereum.BlockchainProcessing](../Nethereum.BlockchainProcessing/README.md) - Processing framework and entity definitions
