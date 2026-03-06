# Nethereum.BlockchainStorage.Processors.Sqlite

SQLite-specific DI registration for the Nethereum blockchain indexer hosted services.

## Overview

Provides `AddSqliteBlockchainProcessor()` and `AddSqliteInternalTransactionProcessor()` extension methods that wire together the database-agnostic processing pipeline from `Nethereum.BlockchainStorage.Processors` with SQLite storage from `Nethereum.BlockchainStore.Sqlite`.

## Installation

```bash
dotnet add package Nethereum.BlockchainStorage.Processors.Sqlite
```

Targets `net10.0`.

### Dependencies

- **Nethereum.BlockchainStorage.Processors** - Database-agnostic processing services and hosted services
- **Nethereum.BlockchainStore.Sqlite** - SQLite DbContext and context factory

## Quick Start

```csharp
var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("SqliteConnection")
    ?? "Data Source=blockchain.db";

builder.Services.AddSqliteBlockchainProcessor(
    builder.Configuration,
    connectionString);

builder.Services.AddSqliteInternalTransactionProcessor();

var host = builder.Build();
await host.RunAsync();
```

### Connection String Resolution

The extension resolves the connection string in order:
1. Explicit `connectionString` parameter
2. `ConnectionStrings:SqliteConnection`
3. `ConnectionStrings:BlockchainDbStorage`

## Configuration

See [Nethereum.BlockchainStorage.Processors](../Nethereum.BlockchainStorage.Processors/README.md) for `BlockchainProcessingOptions` configuration.

## Related Packages

- [Nethereum.BlockchainStorage.Processors](../Nethereum.BlockchainStorage.Processors/README.md) - Base processing services
- [Nethereum.BlockchainStore.Sqlite](../Nethereum.BlockchainStore.Sqlite/README.md) - SQLite storage layer
