# Nethereum.BlockchainStore.SqlServer

SQL Server implementation of the Nethereum blockchain storage layer using Entity Framework Core.

## Overview

Nethereum.BlockchainStore.SqlServer provides the SQL Server-specific `DbContext`, context factory, and DI registration for storing indexed Ethereum blockchain data. It inherits from `Nethereum.BlockchainStore.EFCore.BlockchainDbContextBase` and uses `Microsoft.EntityFrameworkCore.SqlServer`.

Supports optional schema isolation, allowing multiple blockchain datasets to coexist in the same database using different SQL Server schemas.

### Key Features

- `SqlServerBlockchainDbContext` configured with `UseSqlServer()` and optional schema support
- `SqlServerBlockchainDbContextFactory` implementing `IBlockchainDbContextFactory`
- `AddSqlServerBlockchainStorage()` DI extension that registers the factory and all EFCore repositories
- Design-time factory for `dotnet ef migrations` tooling
- Schema isolation for multi-chain storage in a single database

## Installation

```bash
dotnet add package Nethereum.BlockchainStore.SqlServer
```

Targets `net8.0` and `net10.0`.

### Dependencies

- **Nethereum.BlockchainStore.EFCore** - Base `BlockchainDbContextBase`, entity builders, and repository implementations
- **Nethereum.Microsoft.Configuration.Utils** - `ConfigurationUtils.Build()` for appsettings-based connection string resolution
- **Microsoft.EntityFrameworkCore.SqlServer** - SQL Server EF Core provider
- **Microsoft.EntityFrameworkCore.Design** - Design-time migration support (private asset)

## Quick Start

```csharp
using Nethereum.BlockchainStore.SqlServer;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("SqlServerConnection");

builder.Services.AddSqlServerBlockchainStorage(connectionString);
```

This registers `IBlockchainDbContextFactory` and all repository implementations via `AddBlockchainRepositories()`.

## Usage Examples

### With Schema Isolation

Store data for different chains in the same database using SQL Server schemas:

```csharp
builder.Services.AddSqlServerBlockchainStorage(connectionString, schema: "mainnet");
```

### Run Migrations

```bash
cd src/Nethereum.BlockchainStore.SqlServer

dotnet ef migrations add InitialCreate \
  --context SqlServerBlockchainDbContext

dotnet ef database update \
  --context SqlServerBlockchainDbContext
```

### Direct Context Usage

```csharp
var factory = new SqlServerBlockchainDbContextFactory(connectionString);
using var context = factory.CreateContext();

var latestBlock = await context.Blocks
    .Where(b => b.IsCanonical)
    .OrderByDescending(b => b.BlockNumber)
    .FirstOrDefaultAsync();
```

### With Block Storage Processor

```csharp
var factory = new SqlServerBlockchainDbContextFactory(connectionString);
var repoFactory = new BlockchainStoreRepositoryFactory(factory);

var steps = new BlockStorageProcessingSteps(repoFactory);
var orchestrator = new BlockCrawlOrchestrator(web3.Eth, steps);

var processor = new BlockchainProcessor(
    orchestrator,
    repoFactory.CreateBlockProgressRepository(),
    lastConfirmedBlockService);

await processor.ExecuteAsync(cancellationToken);
```

### Connection String Resolution

`SqlServerBlockchainDbContext` resolves the connection string in this order:
1. Constructor parameter (explicit string)
2. `ConnectionStrings:SqlServerConnection` from `appsettings.json`
3. `ConnectionStrings:BlockchainDbStorage` from `appsettings.json`

## Database Schema

All tables use the same schema as `Nethereum.BlockchainStore.EFCore` with `nvarchar(max)` for unlimited text fields. Numeric indexing fields (`BlockNumber`, `Timestamp`, `TransactionIndex`, `LogIndex`, `Nonce`, `TransactionType`) are stored as `bigint`.

## Related Packages

### Dependencies

- **Nethereum.BlockchainStore.EFCore** - Base DbContext, entity builders, repositories

### See Also

- [Nethereum.BlockchainStore.EFCore](../Nethereum.BlockchainStore.EFCore/README.md) - Database-agnostic EF Core base
- [Nethereum.BlockchainStore.Postgres](../Nethereum.BlockchainStore.Postgres/README.md) - PostgreSQL implementation
- [Nethereum.BlockchainStore.Sqlite](../Nethereum.BlockchainStore.Sqlite/README.md) - SQLite implementation
- [Nethereum.BlockchainProcessing](../Nethereum.BlockchainProcessing/README.md) - Processing framework and entity definitions
