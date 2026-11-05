# Nethereum.AppChain.Anchoring.Postgres

> **PREVIEW** - This package is in preview. APIs may change between releases.

**EF Core / PostgreSQL persistence and background indexers for AppChain anchoring: the anchor-contract event index, per-chain summaries, and the cross-chain message index**

[![NuGet](https://img.shields.io/nuget/v/Nethereum.AppChain.Anchoring.Postgres.svg)](https://www.nuget.org/packages/Nethereum.AppChain.Anchoring.Postgres/)

## Overview

`Nethereum.AppChain.Anchoring` anchors AppChain state on a main chain and defines the message hub. This package stores what happens on the main chain in PostgreSQL (Npgsql with `UseLowerCaseNamingConvention()`), and registers hosted services that keep the database in sync. It contains three independent pieces, each registered by its own `IServiceCollection` extension:

| Piece | Registered by | What it does |
|---|---|---|
| Anchor index storage | `AddAnchorIndexPostgresStorage(connectionString)` | `AnchorIndexDbContext` plus the four anchor repositories |
| Anchor indexer and summary denormalizer | `AddAnchorIndexProcessing(...)`, `AddAnchorSummaryDenormalizerProcessing(...)` | Hosted services that read `AnchorSubmitted` and `AppChainRegistered` events from the anchor contract into the tables, and roll them up into one summary row per chain |
| Message index | `AddMessageIndexPostgresProcessing(...)` | `MessageIndexDbContext`, `PostgresMessageIndexStore` (an `IMessageIndexStore`) and a hosted service that runs one `HubMessageLogProcessor` per configured source chain |

It targets `net8.0` and `net10.0`.

## Installation

```bash
dotnet add package Nethereum.AppChain.Anchoring.Postgres
```

## Dependencies

- **Nethereum.AppChain.Anchoring** - the anchor contract DTOs (`AnchorSubmittedEventDTO`, `AppChainRegisteredEventDTO`), `IMessageIndexStore`, `MessageInfo`, `HubMessageLogProcessor`
- **Nethereum.BlockchainProcessing** - `IBlockProgressRepository`
- **Npgsql.EntityFrameworkCore.PostgreSQL**, **EFCore.NamingConventions**, `Microsoft.Extensions.*` (configuration, DI, hosting abstractions, logging, options)

## Test coverage

This package has no test project and the repository has no PostgreSQL test fixture, so the code blocks below are declaration-only (signatures copied from source); none of them is a runnable example. The package's consumers in the repository are `Nethereum.Explorer.Anchoring`, which calls `AddAnchorIndexPostgresStorage`, and the Aspire service `aspire/appchain/Nethereum.AppChain.AnchoringIndexer`, whose `Program.cs` calls `AddAnchorIndexProcessing` and `AddAnchorSummaryDenormalizerProcessing`.

## Anchor index

### Registration

```csharp
public static class AnchorIndexServiceCollectionExtensions
{
    public static IServiceCollection AddAnchorIndexPostgresStorage(
        this IServiceCollection services, string connectionString)

    public static IServiceCollection AddAnchorIndexProcessing(
        this IServiceCollection services,
        IConfiguration configuration,
        string? connectionString = null)

    public static IServiceCollection AddAnchorSummaryDenormalizerProcessing(
        this IServiceCollection services,
        IConfiguration configuration,
        string? connectionString = null)
}
```

- `AddAnchorIndexPostgresStorage` throws `InvalidOperationException` for an empty connection string, registers a scoped `AnchorIndexDbContext` (`UseNpgsql(connectionString).UseLowerCaseNamingConvention()`) and the scoped `PostgresAnchorRecordRepository`, `PostgresChainRegistrationRepository`, `PostgresBlockProofRecordRepository` and `PostgresChainAnchoringSummaryRepository` as `IAnchorRecordRepository`, `IChainRegistrationRepository`, `IBlockProofRecordRepository` and `IChainAnchoringSummaryRepository`.
- `AddAnchorIndexProcessing` resolves the connection string from the argument, then `ConnectionStrings:PostgresConnection`, then `ConnectionStrings:AnchorIndexConnection` (otherwise it throws), calls `AddAnchorIndexPostgresStorage`, binds `AnchorIndexProcessingOptions` from the `AnchorIndexing` section (or from the root configuration if the section does not exist), registers `AnchorIndexingMetrics` as a singleton and adds `AnchorIndexProcessingHostedService`.
- `AddAnchorSummaryDenormalizerProcessing` resolves the connection string the same way, binds `AnchorSummaryDenormalizerOptions` from the `AnchorDenormalizer` section if it exists, and adds `AnchorSummaryDenormalizerHostedService`.

### Options

```csharp
public sealed class AnchorIndexProcessingOptions
{
    public string RpcUrl { get; set; } = "http://localhost:8545";
    public string AnchorContractAddress { get; set; } = "";
    public long StartAtBlockNumberIfNotProcessed { get; set; }
    public int NumberOfBlocksPerRequest { get; set; } = 1000;
    public int PollIntervalMs { get; set; } = 5000;
    public uint MinimumBlockConfirmations { get; set; }
    public int ReorgBuffer { get; set; }
    public List<long> ChainIdFilter { get; set; } = new();
}

public sealed class AnchorSummaryDenormalizerOptions
{
    public int ProcessingIntervalSeconds { get; set; } = 10;
    public int BatchSize { get; set; } = 500;
}
```

`AnchorIndexProcessingHostedService` throws `InvalidOperationException` ("Missing AnchorIndexing RpcUrl configuration value.") if `RpcUrl` is blank. A non-empty `ChainIdFilter` restricts indexing to those chain ids.

### Indexer and denormalizer

```csharp
public class AnchorIndexProcessingService
{
    public AnchorIndexProcessingService(
        AnchorIndexDbContext context,
        IAnchorRecordRepository anchorRepository,
        IChainRegistrationRepository chainRepository,
        AnchorIndexProgressRepository progressRepository,
        ILogger<AnchorIndexProcessingService> logger)

    public AnchorIndexingMetrics Metrics { get; set; }
    public string RpcUrl { get; set; } = "";
    public string AnchorContractAddress { get; set; } = "";
    public long StartAtBlockNumberIfNotProcessed { get; set; }
    public int NumberOfBlocksPerRequest { get; set; } = 1000;
    public int PollIntervalMs { get; set; } = 5000;
    public uint MinimumBlockConfirmations { get; set; }
    public int ReorgBuffer { get; set; }
    public List<long> ChainIdFilter { get; set; } = new();

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
}
```

`ExecuteAsync` loops until cancelled. Each pass reads the last processed block from `AnchorIndexProgressRepository`, takes `headBlock - MinimumBlockConfirmations - ReorgBuffer` as the safe head, reads up to `NumberOfBlocksPerRequest` blocks of `AnchorSubmittedEventDTO` and `AppChainRegisteredEventDTO` logs from `AnchorContractAddress`, upserts `AnchorRecord` and `ChainRegistration` rows (with the main-chain block timestamp), then records the progress. When the stored progress is ahead of the chain head it logs a restart, calls `ResetAllAsync()` and re-indexes from block 1. Errors are retried with a delay that starts at 5 seconds and doubles up to 5 minutes.

```csharp
public class AnchorSummaryDenormalizerService
{
    public AnchorSummaryDenormalizerService(
        AnchorIndexDbContext context,
        IChainAnchoringSummaryRepository summaryRepo,
        AnchorDenormalizerProgressRepository progressRepo,
        IOptions<AnchorSummaryDenormalizerOptions> options,
        AnchorIndexingMetrics metrics = null,
        ILogger<AnchorSummaryDenormalizerService> logger = null)

    public async Task ProcessFromCheckpointAsync(CancellationToken ct = default)
}
```

`ProcessFromCheckpointAsync` reads `BatchSize` new `Anchors` rows after the last processed id and, for each affected chain, upserts a `ChainAnchoringSummary`: latest anchored block, anchor count, count of `BlockProofs` rows, proof system of the latest anchor, last anchor timestamp, and the average interval between the timestamps of the latest ten anchors. The hosted service repeats this every `ProcessingIntervalSeconds`.

### Repositories

```csharp
public interface IAnchorRecordRepository
{
    Task UpsertAsync(AnchorRecord record);
    Task<AnchorRecord> GetLatestAsync(long chainId);
    Task<AnchorRecord> GetByEndBlockAsync(long chainId, long endBlock);
    Task<IReadOnlyList<AnchorRecord>> GetByChainAsync(long chainId, int skip = 0, int take = 50);
    Task<int> GetCountByChainAsync(long chainId);
}

public interface IChainRegistrationRepository
{
    Task UpsertAsync(ChainRegistration registration);
    Task<ChainRegistration?> GetByChainIdAsync(long chainId);
    Task<IReadOnlyList<ChainRegistration>> GetAllAsync();
}

public interface IBlockProofRecordRepository
{
    Task UpsertAsync(BlockProofRecord record);
    Task<IReadOnlyList<BlockProofRecord>> GetByChainAsync(long chainId, int skip = 0, int take = 50);
    Task<bool> IsBlockProvenAsync(long chainId, long blockNumber);
}

public interface IChainAnchoringSummaryRepository
{
    Task UpsertAsync(ChainAnchoringSummary summary);
    Task<ChainAnchoringSummary?> GetAsync(long chainId);
    Task<IReadOnlyList<ChainAnchoringSummary>> GetAllAsync();
}
```

Progress repositories, registered by the processing extensions: `AnchorIndexProgressRepository` (`GetLastProcessedBlockAsync(long defaultValue = 0)`, `UpsertAsync(long blockNumber)`, `ResetAllAsync()`) and `AnchorDenormalizerProgressRepository` (`GetLastProcessedRowIndexAsync()`, `UpsertProgressAsync(long lastAnchorId)`).

The indexer in this package writes anchors and chain registrations. `IBlockProofRecordRepository` and the `anchor_block_proofs` table exist, and the denormalizer counts their rows, but nothing in this package writes block-proof rows.

### Entities and tables

`AnchorIndexDbContext` (constructors `AnchorIndexDbContext()` and `AnchorIndexDbContext(DbContextOptions<AnchorIndexDbContext> options)`) exposes:

| DbSet | Entity | Table | Unique index |
|---|---|---|---|
| `Anchors` | `AnchorRecord` | `anchor_records` | `(ChainId, EndBlock)` |
| `ChainRegistrations` | `ChainRegistration` | `anchor_chain_registrations` | `ChainId` |
| `BlockProofs` | `BlockProofRecord` | `anchor_block_proofs` | `(ChainId, BlockNumber)` |
| `ChainSummaries` | `ChainAnchoringSummary` | `anchor_chain_summaries` | key `ChainId` |
| `IndexProgress` | `AnchorIndexProgress` | `anchor_index_progress` | key `Id` |
| `DenormalizerProgress` | `AnchorDenormalizerProgress` | `anchor_denormalizer_progress` | key `Id` |

```csharp
public class AnchorRecord
{
    public long Id { get; set; }
    public long ChainId { get; set; }
    public long StartBlock { get; set; }
    public long EndBlock { get; set; }
    public byte ProofSystem { get; set; }
    public byte AnchorVersion { get; set; }
    public byte[] EndBlockHash { get; set; }
    public byte[] PostStateRoot { get; set; }
    public byte[] BlockHashesRoot { get; set; }
    public byte[] ManifestHash { get; set; }
    public byte[] PreviousAnchorHash { get; set; }
    public int ProofBytesLength { get; set; }
    public string TransactionHash { get; set; }
    public long MainchainBlockNumber { get; set; }
    public long Timestamp { get; set; }
    public string OperatorAddress { get; set; }
}

public class ChainRegistration
{
    public long Id { get; set; }
    public long ChainId { get; set; }
    public byte[] GenesisHash { get; set; }
    public byte MinimumProofSystem { get; set; }
    public byte MinimumAnchorVersion { get; set; }
    public string AuthorityAddress { get; set; }
    public string TransactionHash { get; set; }
    public long BlockNumber { get; set; }
    public long Timestamp { get; set; }
}

public class BlockProofRecord
{
    public long Id { get; set; }
    public long ChainId { get; set; }
    public long BlockNumber { get; set; }
    public byte ProofSystem { get; set; }
    public string ProverAddress { get; set; }
    public string TransactionHash { get; set; }
    public long MainchainBlockNumber { get; set; }
    public long Timestamp { get; set; }
}

public class ChainAnchoringSummary
{
    public long ChainId { get; set; }
    public long LatestAnchoredBlock { get; set; }
    public long TotalAnchors { get; set; }
    public long TotalProvenBlocks { get; set; }
    public byte CurrentProofSystem { get; set; }
    public long LastAnchorTimestamp { get; set; }
    public double AverageAnchorIntervalSeconds { get; set; }
    public int ConsecutiveFailures { get; set; }
    public long LastUpdated { get; set; }
}
```

The package ships no migrations; create the schema from the model (for example `Database.EnsureCreatedAsync()` or your own EF migrations).

### Metrics

```csharp
public class AnchorIndexingMetrics : IDisposable
{
    public AnchorIndexingMetrics(string name = "Nethereum", IMeterFactory meterFactory = null)
    public void RecordBatch(long fromBlock, long toBlock, int anchorCount, int chainCount, double durationSeconds)
    public void SetChainHead(long blockNumber)
    public void RecordError(string reason)
    public void RecordReset()
    public void RecordDenormalization(int count)
    public void Dispose()
}
```

The meter is named `{name}.AnchorIndexing` and publishes `anchoring.index.blocks_processed`, `anchoring.index.events_indexed`, `anchoring.index.errors`, `anchoring.index.resets`, `anchoring.index.batch.duration`, and the gauges `anchoring.index.lag`, `anchoring.index.last_block` and `anchoring.denormalizer.processed`.

## Message index

```csharp
public static class MessageIndexServiceCollectionExtensions
{
    public static IServiceCollection AddMessageIndexPostgresProcessing(
        this IServiceCollection services,
        IConfiguration configuration,
        string? connectionString = null)
}
```

It resolves the connection string from the argument, then `ConnectionStrings:PostgresConnection`, then `ConnectionStrings:MessageIndexPostgresConnection` (otherwise it throws `InvalidOperationException`), registers `MessageIndexDbContext`, binds `MessageIndexProcessingOptions` from the `MessageIndexProcessing` section (or from the root configuration), registers `PostgresMessageIndexStore` as itself and as `IMessageIndexStore`, and adds `MessageIndexProcessingHostedService`. The hosted service throws if `SourceChains` is empty; otherwise it starts one `HubMessageLogProcessor` per `SourceChainOption`, restarting a failed one after a delay that starts at 5 seconds and doubles up to 5 minutes.

```csharp
public sealed class MessageIndexProcessingOptions
{
    public string RpcUrl { get; set; } = "";
    public string HubContractAddress { get; set; } = "";
    public ulong TargetChainId { get; set; }
    public List<SourceChainOption> SourceChains { get; set; } = new();
    public BigInteger StartAtBlockNumber { get; set; } = 0;
    public uint MinimumBlockConfirmations { get; set; } = 12;
    public int ReorgBuffer { get; set; } = 0;
    public int BlocksPerRequest { get; set; } = 1000;
    public int RetryWeight { get; set; } = 50;

    public static MessageIndexProcessingOptions Load(IConfiguration configuration)
}

public sealed class SourceChainOption
{
    public ulong ChainId { get; set; }
    public string RpcUrl { get; set; } = "";
    public string HubContractAddress { get; set; } = "";
}
```

`Load` reads `SourceChains` as child sections with `ChainId`, `RpcUrl` and `HubContractAddress`.

```csharp
public class PostgresMessageIndexStore : IMessageIndexStore
{
    public PostgresMessageIndexStore(MessageIndexDbContext context)

    public async Task StoreAsync(MessageInfo message)
    public async Task StoreBatchAsync(IEnumerable<MessageInfo> messages)
    public async Task<MessageInfo?> GetAsync(ulong sourceChainId, ulong messageId)
    public async Task<List<MessageInfo>> GetPendingAsync(ulong sourceChainId, ulong afterMessageId, int maxCount)
    public async Task<ulong> GetLastIndexedMessageIdAsync(ulong sourceChainId)
    public async Task RemoveFromAsync(ulong sourceChainId, ulong messageId)
    public IBlockProgressRepository GetBlockProgressRepository(ulong sourceChainId)
}

public class MessageBlockProgressRepository : IBlockProgressRepository
{
    public MessageBlockProgressRepository(MessageIndexDbContext context, long sourceChainId)
    public async Task<BigInteger?> GetLastBlockNumberProcessedAsync()
    public async Task UpsertProgressAsync(BigInteger blockNumber)
}
```

`MessageIndexDbContext` exposes `IndexedMessages` (`IndexedMessage`, unique on `(SourceChainId, MessageId)`, indexed on `(SourceChainId, BlockNumber)`, `Data` stored as `bytea`) and `MessageBlockProgress` (`MessageBlockProgress`, unique on `SourceChainId`).

```csharp
public class IndexedMessage
{
    public long Id { get; set; }
    public long SourceChainId { get; set; }
    public long MessageId { get; set; }
    public long TargetChainId { get; set; }
    public string Sender { get; set; } = "";
    public string Target { get; set; } = "";
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public long BlockNumber { get; set; }
    public long Timestamp { get; set; }
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
}

public class MessageBlockProgress
{
    public int Id { get; set; }
    public long SourceChainId { get; set; }
    public string LastBlockProcessed { get; set; } = "";
    public DateTime? UpdatedAt { get; set; }
}
```
