# Nethereum.BlockchainStorage.Processors.Postgres

PostgreSQL-specific DI registration for the Nethereum blockchain indexer hosted services.

## Overview

Provides `AddPostgresBlockchainProcessor()` and `AddPostgresInternalTransactionProcessor()` extension methods that wire together the database-agnostic processing pipeline from `Nethereum.BlockchainStorage.Processors` with PostgreSQL storage from `Nethereum.BlockchainStore.Postgres`.

## Installation

```bash
dotnet add package Nethereum.BlockchainStorage.Processors.Postgres
```

Targets `net10.0`.

### Dependencies

- **Nethereum.BlockchainStorage.Processors** - Database-agnostic processing services and hosted services
- **Nethereum.BlockchainStore.Postgres** - PostgreSQL DbContext and context factory

## Quick Start

```csharp
var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PostgresConnection");

builder.Services.AddPostgresBlockchainProcessor(
    builder.Configuration,
    connectionString);

builder.Services.AddPostgresInternalTransactionProcessor();

var host = builder.Build();

using var context = new PostgresBlockchainDbContext(connectionString);
await context.Database.MigrateAsync();

await host.RunAsync();
```

### Connection String Resolution

The extension resolves the connection string in order:
1. Explicit `connectionString` parameter
2. `ConnectionStrings:PostgresConnection`
3. `ConnectionStrings:BlockchainDbStorage`

### Aspire Integration

```csharp
var connectionString = builder.Configuration.GetConnectionString("nethereumdb");
builder.Services.AddPostgresBlockchainProcessor(builder.Configuration, connectionString);
builder.Services.AddPostgresInternalTransactionProcessor();
```

## Configuration

See [Nethereum.BlockchainStorage.Processors](../Nethereum.BlockchainStorage.Processors/README.md) for `BlockchainProcessingOptions` configuration.

## Related Packages

- [Nethereum.BlockchainStorage.Processors](../Nethereum.BlockchainStorage.Processors/README.md) - Base processing services
- [Nethereum.BlockchainStore.Postgres](../Nethereum.BlockchainStore.Postgres/README.md) - PostgreSQL storage layer
- [Nethereum.BlockchainStorage.Token.Postgres](../Nethereum.BlockchainStorage.Token.Postgres/README.md) - Token transfer log processing
