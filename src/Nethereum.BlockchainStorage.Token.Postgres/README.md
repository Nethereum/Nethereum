# Nethereum.BlockchainStorage.Token.Postgres

PostgreSQL repositories and processing services for indexing ERC-20, ERC-721, and ERC-1155 token transfer logs with on-chain balance aggregation.

## Overview

Nethereum.BlockchainStorage.Token.Postgres provides a complete token indexing pipeline: it captures token transfer events from the blockchain, denormalizes raw transaction logs into typed `TokenTransferLog` records, and aggregates current token balances by querying the chain via batched RPC calls. All data is stored in a dedicated `TokenPostgresDbContext` with its own schema and migration history.

The package provides three independent hosted services that run as background processors:
1. **Token log processor** - Indexes raw transaction logs containing ERC-20/721/1155 Transfer events
2. **Token denormalizer** - Reads indexed raw logs and creates typed `TokenTransferLog` records with from/to/amount/tokenId fields
3. **Balance aggregation** - Reads denormalized transfer logs and queries on-chain balances via batched `eth_call` (using `MultiQueryBatchRpcHandler`), storing results in `TokenBalance` and `NFTInventory` tables

### Key Features

- `TokenPostgresDbContext` with tables for transfer logs, token balances, NFT inventory, token metadata, and processing progress
- `TokenLogPostgresProcessingService` indexes raw Transfer event logs from the blockchain
- `TokenDenormalizerService` converts raw logs into typed `TokenTransferLog` records with checkpoint-based progress
- `TokenBalanceRpcAggregationService` queries on-chain ERC-20 `balanceOf`, ERC-721 `ownerOf`, and ERC-1155 `balanceOf` via batched RPC
- Reorg recovery: detects non-canonical transfer logs and re-queries affected account balances at the latest block
- Repository implementations for `ITokenTransferLogRepository`, `ITokenBalanceRepository`, `INFTInventoryRepository`, `ITokenMetadataRepository`
- DI extensions for each processor: `AddTokenLogPostgresProcessing()`, `AddTokenDenormalizerProcessing()`, `AddTokenBalanceAggregationProcessing()`

## Installation

```bash
dotnet add package Nethereum.BlockchainStorage.Token.Postgres
```

Targets `net8.0` and `net10.0`.

### Dependencies

- **Nethereum.BlockchainProcessing** - Entity models (`TokenTransferLog`, `TokenBalance`, `NFTInventory`, `BlockProgress`), repository interfaces, and `TokenTransferLogProcessingService`
- **Nethereum.Web3** - RPC client for balance aggregation queries
- **Npgsql.EntityFrameworkCore.PostgreSQL** - PostgreSQL EF Core provider
- **EFCore.NamingConventions** - Lowercase table/column naming
- **Microsoft.Extensions.Hosting.Abstractions** - `BackgroundService` base class

## Quick Start

```csharp
var builder = Host.CreateApplicationBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("PostgresConnection");

builder.Services.AddTokenLogPostgresProcessing(builder.Configuration, connectionString);
builder.Services.AddTokenDenormalizerProcessing(builder.Configuration, connectionString);
builder.Services.AddTokenBalanceAggregationProcessing(builder.Configuration, connectionString);

var host = builder.Build();

using var context = new TokenPostgresDbContext(
    new DbContextOptionsBuilder<TokenPostgresDbContext>()
        .UseNpgsql(connectionString)
        .UseLowerCaseNamingConvention()
        .Options);
await context.Database.MigrateAsync();

await host.RunAsync();
```

## Configuration

### Token Log Processing

Bound from `"TokenLogProcessing"` configuration section:

```json
{
  "TokenLogProcessing": {
    "BlockchainUrl": "http://localhost:8545",
    "NumberOfBlocksToProcessPerRequest": 1000,
    "RetryWeight": 50,
    "MinimumNumberOfConfirmations": 0,
    "ReorgBuffer": 10,
    "ContractAddresses": null
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BlockchainUrl` | `string` | required | JSON-RPC endpoint |
| `NumberOfBlocksToProcessPerRequest` | `int` | `1000` | Log retrieval batch size |
| `MinimumNumberOfConfirmations` | `uint` | `0` | Wait for block confirmations |
| `ReorgBuffer` | `int` | `0` | Re-check recent blocks for reorgs |
| `ContractAddresses` | `string[]` | `null` | Filter to specific contracts (null = all) |

### Token Denormalizer

Bound from `"TokenDenormalizer"` section:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BatchSize` | `int` | `1000` | Rows per processing batch |

### Balance Aggregation

Bound from `"TokenBalanceAggregation"` section:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RpcUrl` | `string` | required | JSON-RPC endpoint for balance queries |
| `BatchSize` | `int` | `1000` | Transfer logs per processing batch |

## Database Schema

All tables use lowercase naming via `EFCore.NamingConventions`.

| Table | Entity | Key Columns |
|-------|--------|-------------|
| `tokentransferlogs` | `TokenTransferLog` | `transactionhash`, `logindex` (bigint), `blocknumber` (bigint), `contractaddress`, `fromaddress`, `toaddress`, `amount`, `tokenid`, `tokentype`, `iscanonical` |
| `tokenbalances` | `TokenBalance` | `address`, `contractaddress` (unique pair), `balance`, `tokentype`, `lastupdatedblocknumber` (bigint) |
| `nftinventory` | `NFTInventory` | `address`, `contractaddress`, `tokenid` (unique triple), `amount`, `tokentype`, `lastupdatedblocknumber` |
| `tokenmetadata` | `TokenMetadata` | `contractaddress` (unique), `name`, `symbol`, `decimals`, `tokentype` |
| `tokenblockprogress` | `BlockProgress` | `lastblockprocessed` |
| `balanceaggregationprogress` | `BalanceAggregationProgress` | `lastprocessedrowindex` |
| `denormalizerprogress` | `DenormalizerProgress` | `lastprocessedrowindex` |

The `transactionlogs` table is referenced read-only (`ExcludeFromMigrations()`) — it is owned by the main `PostgresBlockchainDbContext` and shared via the same database.

## Processing Pipeline

### Token Log Processor

1. Reads `TokenBlockProgress` for last processed block
2. Calls `eth_getLogs` with Transfer event topic filters in batch ranges
3. Stores raw logs in the shared `TransactionLogs` table (via `TokenPostgresTransactionLogRepository`)
4. Updates progress

### Token Denormalizer

1. Reads `DenormalizerProgress` for last processed `RowIndex`
2. Queries `TransactionLogs` where `EventHash` matches ERC-20/721/1155 Transfer signatures and `IsCanonical = true`
3. Decodes log topics and data into typed `TokenTransferLog` records (from, to, amount, tokenId, tokenType)
4. Upserts to `TokenTransferLogs` table
5. Updates progress checkpoint

### Balance Aggregation

1. Reads `BalanceAggregationProgress` for last processed `RowIndex`
2. Queries new `TokenTransferLogs` since checkpoint
3. Groups by block number and builds batched RPC calls:
   - ERC-20: `balanceOf(address)` for each affected account
   - ERC-721: `balanceOf(address)` + `ownerOf(tokenId)` for ownership tracking
   - ERC-1155: `balanceOf(address, tokenId)` for each affected token
4. Executes batch via `MultiQueryBatchRpcHandler` with `BlockParameter` at the transfer's block
5. Upserts results to `TokenBalances` and `NFTInventory` tables
6. **Reorg recovery**: on startup, finds non-canonical transfer logs, re-queries affected accounts at latest block, removes non-canonical records

## Usage Examples

### Register All Token Services

```csharp
var connectionString = configuration.GetConnectionString("PostgresConnection");

services.AddTokenPostgresRepositories(connectionString);
services.AddTokenLogPostgresProcessing(configuration, connectionString);
services.AddTokenDenormalizerProcessing(configuration, connectionString);
services.AddTokenBalanceAggregationProcessing(configuration, connectionString);
```

### Repository-Only Registration

For the Explorer or other read-only consumers that don't need processing:

```csharp
services.AddTokenPostgresRepositories(connectionString);
```

This registers `ITokenTransferLogRepository`, `ITokenBalanceRepository`, `INFTInventoryRepository`, and `ITokenMetadataRepository` without starting any hosted services.

### Query Token Balances

```csharp
var balanceRepo = serviceProvider.GetRequiredService<ITokenBalanceRepository>();

var balances = await balanceRepo.GetBalancesForAddressAsync(
    "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb");

foreach (var balance in balances)
{
    Console.WriteLine($"{balance.ContractAddress}: {balance.Balance} ({balance.TokenType})");
}
```

## API Reference

### TokenPostgresServiceCollectionExtensions

- `AddTokenPostgresRepositories(IServiceCollection, string) : IServiceCollection` - Registers DbContext and read repositories only
- `AddTokenLogPostgresProcessing(IServiceCollection, IConfiguration, string?) : IServiceCollection` - Registers token log processor hosted service
- `AddTokenDenormalizerProcessing(IServiceCollection, IConfiguration, string?) : IServiceCollection` - Registers denormalizer hosted service
- `AddTokenBalanceAggregationProcessing(IServiceCollection, IConfiguration, string?) : IServiceCollection` - Registers balance aggregation hosted service

### TokenLogPostgresProcessingService

- `ExecuteAsync(CancellationToken) : Task` - Continuously indexes Transfer event logs from the blockchain

### TokenDenormalizerService

- `ProcessFromCheckpointAsync(CancellationToken) : Task` - Processes raw logs into typed transfer records from last checkpoint

### TokenBalanceRpcAggregationService

- `ProcessFromCheckpointAsync(CancellationToken) : Task` - Queries on-chain balances for accounts with new transfer activity

## Related Packages

### Dependencies

- **Nethereum.BlockchainProcessing** - Entity models and repository interfaces
- **Nethereum.Web3** - RPC client for balance queries

### See Also

- [Nethereum.BlockchainStore.Postgres](../Nethereum.BlockchainStore.Postgres/README.md) - Main blockchain storage (blocks, transactions, logs)
- [Nethereum.BlockchainStorage.Processors.Postgres](../Nethereum.BlockchainStorage.Processors.Postgres/README.md) - Block processing hosted services
- [Nethereum.Explorer](../Nethereum.Explorer/README.md) - Explorer UI that displays token data
