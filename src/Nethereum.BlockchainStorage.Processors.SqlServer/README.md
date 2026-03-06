# Nethereum.BlockchainStorage.Processors.SqlServer

SQL Server-specific DI registration for the Nethereum blockchain indexer hosted services.

## Overview

Provides `AddSqlServerBlockchainProcessor()` and `AddSqlServerInternalTransactionProcessor()` extension methods that wire together the database-agnostic processing pipeline from `Nethereum.BlockchainStorage.Processors` with SQL Server storage from `Nethereum.BlockchainStore.SqlServer`.

Supports optional schema isolation for multi-chain storage in a single database.

## Installation

```bash
dotnet add package Nethereum.BlockchainStorage.Processors.SqlServer
```

Targets `net10.0`.

### Dependencies

- **Nethereum.BlockchainStorage.Processors** - Database-agnostic processing services and hosted services
- **Nethereum.BlockchainStore.SqlServer** - SQL Server DbContext and context factory

## Quick Start

```csharp
var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("SqlServerConnection");

builder.Services.AddSqlServerBlockchainProcessor(
    builder.Configuration,
    connectionString);

builder.Services.AddSqlServerInternalTransactionProcessor();

var host = builder.Build();
await host.RunAsync();
```

### With Schema Isolation

```csharp
builder.Services.AddSqlServerBlockchainProcessor(
    builder.Configuration,
    connectionString,
    schema: "mainnet");
```

### Connection String Resolution

The extension resolves the connection string in order:
1. Explicit `connectionString` parameter
2. `ConnectionStrings:SqlServerConnection`
3. `ConnectionStrings:BlockchainDbStorage`

## Configuration

See [Nethereum.BlockchainStorage.Processors](../Nethereum.BlockchainStorage.Processors/README.md) for `BlockchainProcessingOptions` configuration.

## Related Packages

- [Nethereum.BlockchainStorage.Processors](../Nethereum.BlockchainStorage.Processors/README.md) - Base processing services
- [Nethereum.BlockchainStore.SqlServer](../Nethereum.BlockchainStore.SqlServer/README.md) - SQL Server storage layer
