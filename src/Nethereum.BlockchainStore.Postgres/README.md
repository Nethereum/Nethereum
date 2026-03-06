# Nethereum.BlockchainStore.Postgres

PostgreSQL implementation of the Nethereum blockchain storage layer using Entity Framework Core and Npgsql.

## Overview

Nethereum.BlockchainStore.Postgres provides the PostgreSQL-specific `DbContext`, context factory, and DI registration for storing indexed Ethereum blockchain data. It inherits from `Nethereum.BlockchainStore.EFCore.BlockchainDbContextBase` and configures Npgsql with lowercase naming conventions.

This package is used by the blockchain processing pipeline to persist blocks, transactions, logs, contracts, internal transactions, and chain state to PostgreSQL. It is also used by the Explorer to query indexed data.

### Key Features

- `PostgresBlockchainDbContext` configured with `UseNpgsql()` and `UseLowerCaseNamingConvention()`
- `PostgresBlockchainDbContextFactory` implementing `IBlockchainDbContextFactory` for short-lived context creation
- `AddPostgresBlockchainStorage()` DI extension that registers the factory and all EFCore repositories
- EF Core migrations for the full blockchain schema (blocks, transactions, logs, contracts, internal transactions, chain state, account state, address transactions)
- Design-time factory for `dotnet ef migrations` tooling

## Installation

```bash
dotnet add package Nethereum.BlockchainStore.Postgres
```

Targets `net8.0` and `net10.0`. Uses Npgsql.EntityFrameworkCore.PostgreSQL 8.x on net8.0 and 10.x on net10.0.

### Dependencies

- **Nethereum.BlockchainStore.EFCore** - Base `BlockchainDbContextBase`, entity builders, and repository implementations
- **Nethereum.Microsoft.Configuration.Utils** - `ConfigurationUtils.Build()` for appsettings-based connection string resolution
- **Npgsql.EntityFrameworkCore.PostgreSQL** - PostgreSQL EF Core provider
- **EFCore.NamingConventions** - `UseLowerCaseNamingConvention()` for snake_case table/column names
- **Microsoft.EntityFrameworkCore.Design** - Design-time migration support (private asset)

## Quick Start

```csharp
using Nethereum.BlockchainStore.Postgres;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PostgresConnection");

builder.Services.AddPostgresBlockchainStorage(connectionString);
```

This single call registers:
- `IBlockchainDbContextFactory` as `PostgresBlockchainDbContextFactory`
- All repository implementations via `AddBlockchainRepositories()` (`IBlockRepository`, `ITransactionRepository`, `ITransactionLogRepository`, `IContractRepository`, etc.)

## Usage Examples

### Run Migrations

```bash
cd src/Nethereum.BlockchainStore.Postgres

dotnet ef migrations add InitialCreate \
  --context PostgresBlockchainDbContext

dotnet ef database update \
  --context PostgresBlockchainDbContext
```

The design-time factory (`PostgresBlockchainDesignTimeDbContextFactory`) reads the connection string from `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "PostgresConnection": "Host=localhost;Database=nethereumdb;Username=postgres;Password=postgres"
  }
}
```

### Programmatic Migration

```csharp
var factory = new PostgresBlockchainDbContextFactory(connectionString);
using var context = factory.CreateContext();
await context.Database.MigrateAsync();
```

### Direct Context Usage

```csharp
var factory = new PostgresBlockchainDbContextFactory(connectionString);
using var context = factory.CreateContext();

var latestBlock = await context.Blocks
    .Where(b => b.IsCanonical)
    .OrderByDescending(b => b.BlockNumber)
    .FirstOrDefaultAsync();

var txCount = await context.Transactions
    .Where(t => t.BlockNumber == latestBlock.BlockNumber && t.IsCanonical)
    .CountAsync();
```

### With Block Storage Processor

```csharp
var dbContextFactory = new PostgresBlockchainDbContextFactory(connectionString);
var repoFactory = new BlockchainStoreRepositoryFactory(dbContextFactory);

var steps = new BlockStorageProcessingSteps(repoFactory);
var orchestrator = new BlockCrawlOrchestrator(web3.Eth, steps);
orchestrator.ContractCreatedCrawlerStep.RetrieveCode = true;

var processor = new BlockchainProcessor(
    orchestrator,
    repoFactory.CreateBlockProgressRepository(),
    lastConfirmedBlockService);

await processor.ExecuteAsync(cancellationToken);
```

### Connection String Resolution

`PostgresBlockchainDbContext` resolves the connection string in this order:
1. Constructor parameter (explicit string)
2. `ConnectionStrings:PostgresConnection` from `appsettings.json`
3. `ConnectionStrings:BlockchainDbStorage` from `appsettings.json`

## Database Schema

All table and column names use lowercase convention via `EFCore.NamingConventions`.

### Tables

| Table | Entity | Key Columns |
|-------|--------|-------------|
| `blocks` | `Block` | `blocknumber` (bigint), `hash`, `parenthash`, `miner`, `timestamp` (bigint), `gasused`, `gaslimit`, `basefeepergas`, `blobgasused`, `excessblobgas`, `parentbeaconblockroot`, `requestshash` |
| `transactions` | `Transaction` | `blocknumber` (bigint), `hash`, `addressfrom`, `addressto`, `transactionindex` (bigint), `timestamp` (bigint), `value`, `gas`, `gasprice`, `gasused`, `transactiontype` (bigint), `maxfeeperblobgas`, `blobgasused`, `blobgasprice` |
| `transactionlogs` | `TransactionLog` | `transactionhash`, `logindex` (bigint), `blocknumber` (bigint), `address`, `eventhash`, `data` |
| `contracts` | `Contract` | `address`, `name`, `abi`, `code`, `creator`, `transactionhash` |
| `internaltransactions` | `InternalTransaction` | `transactionhash`, `index`, `blocknumber` (bigint), `from`, `to`, `value`, `type` |
| `blockprogress` | `BlockProgress` | `lastblockprocessed` |
| `chainstates` | `ChainState` | `lastcanonicalblocknumber` (bigint, nullable), `finalizedblocknumber` (bigint, nullable), `chainid` |
| `accountstates` | `AccountState` | `address`, `balance`, `nonce` (bigint), `lastupdatedblock` (bigint) |

Numeric fields (`blocknumber`, `timestamp`, `transactionindex`, `logindex`, `nonce`, `transactiontype`) are stored as `bigint` (long). Gas and value fields remain as `character varying(100)` strings to accommodate full uint256 range.

## Related Packages

### Used By (Consumers)

- **Nethereum.BlockchainStorage.Processors.Postgres** - Hosted services that orchestrate the processing pipeline against this database
- **Nethereum.Explorer** - Blazor Server explorer that queries indexed data via `IBlockchainDbContextFactory`
- **Nethereum.Aspire.Indexer** - Aspire-hosted indexer worker

### Dependencies

- **Nethereum.BlockchainStore.EFCore** - Base DbContext, entity builders, repositories

### See Also

- [Nethereum.BlockchainStore.EFCore](../Nethereum.BlockchainStore.EFCore/README.md) - Database-agnostic EF Core base
- [Nethereum.BlockchainStorage.Processors.Postgres](../Nethereum.BlockchainStorage.Processors.Postgres/README.md) - Processing hosted services
- [Nethereum.BlockchainProcessing](../Nethereum.BlockchainProcessing/README.md) - Processing framework and entity definitions
