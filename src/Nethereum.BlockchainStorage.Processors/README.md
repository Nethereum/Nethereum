# Nethereum.BlockchainStorage.Processors

Database-agnostic background hosted services for indexing Ethereum blockchain data with automatic retry, reorg handling, and internal transaction tracing.

## Overview

Nethereum.BlockchainStorage.Processors provides ready-to-use `BackgroundService` implementations that wire together the `Nethereum.BlockchainProcessing` pipeline with any EF Core-based blockchain storage provider. This package contains the processing logic; pair it with a provider-specific package for DI registration.

Two processors are included:
1. **Block processor** - Crawls blocks, transactions, receipts, logs, and contract creations. Handles reorgs by marking non-canonical records and re-indexing.
2. **Internal transaction processor** - Traces transactions via `debug_traceTransaction` (call tracer) and stores internal calls. Runs behind the block processor, capped to the main processor's progress.

### Key Features

- `BlockchainProcessingHostedService` runs the block processing pipeline as a `BackgroundService`
- `InternalTransactionProcessingHostedService` traces internal transactions as a separate `BackgroundService`
- Exponential backoff retry via `RetryRunner.RunWithExponentialBackoffAsync()`
- Chain ID validation on startup via `ChainStateValidationService.EnsureChainIdMatchesAsync()`
- Reorg detection and handling: marks blocks, transactions, logs, and token transfers as non-canonical
- Batch receipt fetching (`UseBatchReceipts` option, enabled by default)
- Contract code retrieval on deployment
- Configurable via `BlockchainProcessingOptions` from `IConfiguration`

## Installation

Use the provider-specific package instead of referencing this package directly:

```bash
dotnet add package Nethereum.BlockchainStorage.Processors.Postgres
dotnet add package Nethereum.BlockchainStorage.Processors.SqlServer
dotnet add package Nethereum.BlockchainStorage.Processors.Sqlite
```

## Configuration

Options are bound from `IConfiguration`. The extension method checks for a `"BlockchainProcessing"` section first; if absent, it reads from the root configuration.

```json
{
  "BlockchainUrl": "http://localhost:8545",
  "BlockchainProcessing": {
    "BlockchainUrl": "http://localhost:8545",
    "MinimumBlockConfirmations": 12,
    "FromBlock": 0,
    "ReorgBuffer": 10,
    "UseBatchReceipts": true
  }
}
```

### BlockchainProcessingOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BlockchainUrl` | `string` | required | JSON-RPC endpoint URL |
| `Name` | `string` | `null` | Optional chain name for logging |
| `MinimumBlockConfirmations` | `uint?` | `12` | Blocks behind chain head to wait before processing |
| `FromBlock` | `BigInteger?` | `null` | Starting block if no progress exists |
| `ToBlock` | `BigInteger?` | `null` | Stop at this block (null = continuous) |
| `ReorgBuffer` | `int` | `0` | Number of blocks to re-check for reorgs |
| `UseBatchReceipts` | `bool` | `true` | Use `eth_getBlockReceipts` instead of individual receipt calls |
| `NumberOfBlocksToProcessPerRequest` | `int` | `1000` | Batch size for log retrieval |
| `RetryWeight` | `int` | `50` | Reduce batch size on retry failures |
| `ProcessBlockTransactionsInParallel` | `bool` | `true` | Parallel transaction processing within a block |
| `PostVm` | `bool` | `false` | Include VM stack traces |

## Related Packages

### Provider-Specific Packages

- [Nethereum.BlockchainStorage.Processors.Postgres](../Nethereum.BlockchainStorage.Processors.Postgres/README.md) - PostgreSQL DI registration
- [Nethereum.BlockchainStorage.Processors.SqlServer](../Nethereum.BlockchainStorage.Processors.SqlServer/README.md) - SQL Server DI registration
- [Nethereum.BlockchainStorage.Processors.Sqlite](../Nethereum.BlockchainStorage.Processors.Sqlite/README.md) - SQLite DI registration

### See Also

- [Nethereum.BlockchainProcessing](../Nethereum.BlockchainProcessing/README.md) - Core processing framework
- [Nethereum.BlockchainStore.EFCore](../Nethereum.BlockchainStore.EFCore/README.md) - Base EF Core storage layer
