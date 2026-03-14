---
name: blockchain-indexing
description: Index blockchain data (blocks, transactions, logs, tokens) to PostgreSQL/SqlServer/SQLite with progress tracking, reorg handling, and hosted services (.NET/C#). Use this skill when the user asks about blockchain indexing, block processing, event log crawling, database storage for blockchain data, token transfer indexing, balance aggregation, or chain reorganisation handling.
user-invocable: true
---

# Blockchain Data Indexing

Nethereum provides a full block/log processing pipeline that crawls an Ethereum-compatible chain, stores blocks, transactions, logs, and token data into a relational database (Postgres, SQL Server, or SQLite via EF Core), tracks progress, handles chain reorgs, and runs as a .NET hosted service.

## Packages

| Package | Purpose |
|---------|---------|
| `Nethereum.BlockchainProcessing` | Core pipeline: crawlers, processors, progress repos, entity models |
| `Nethereum.BlockchainStore.EFCore` | EF Core base context and repository factory |
| `Nethereum.BlockchainStore.Postgres` | Postgres EF Core provider |
| `Nethereum.BlockchainStore.SqlServer` | SQL Server EF Core provider |
| `Nethereum.BlockchainStore.Sqlite` | SQLite EF Core provider |
| `Nethereum.BlockchainStorage.Processors` | Hosted service, options, DI extensions |
| `Nethereum.BlockchainStorage.Processors.Postgres` | One-call Postgres DI setup |
| `Nethereum.BlockchainStorage.Processors.SqlServer` | One-call SQL Server DI setup |
| `Nethereum.BlockchainStorage.Processors.Sqlite` | One-call SQLite DI setup |

## Quick Start: Hosted Service with Postgres

```csharp
using Nethereum.BlockchainStorage.Processors.Postgres;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPostgresBlockchainProcessor(
    builder.Configuration,
    connectionString: "Host=localhost;Database=blockchain;Username=postgres;Password=secret");

var host = builder.Build();
await host.RunAsync();
```

`appsettings.json`:

```json
{
  "BlockchainProcessing": {
    "BlockchainUrl": "https://eth.llamarpc.com",
    "FromBlock": 20000000,
    "MinimumBlockConfirmations": 12,
    "ReorgBuffer": 20,
    "NumberOfBlocksToProcessPerRequest": 1000,
    "UseBatchReceipts": true,
    "ProcessBlockTransactionsInParallel": true,
    "RetryWeight": 50
  }
}
```

This registers `BlockchainProcessingHostedService` (a `BackgroundService`) that continuously crawls blocks, persists them to Postgres, and auto-retries with exponential backoff on failure.

## Database Providers

```csharp
// Postgres
services.AddPostgresBlockchainProcessor(configuration, connectionString);

// SQL Server (optional schema)
services.AddSqlServerBlockchainProcessor(configuration, connectionString, schema: "eth");

// SQLite
services.AddSqliteBlockchainProcessor(configuration, connectionString);
```

Each extension method wires up the EF Core context, `BlockchainProcessingOptions`, and the hosted service in one call. Connection string resolution order: explicit parameter, `ConnectionStrings:PostgresConnection` (or `SqlServerConnection`/`SqliteConnection`), `ConnectionStrings:BlockchainDbStorage`.

## Configuration: BlockchainProcessingOptions

```csharp
public sealed class BlockchainProcessingOptions
{
    public string? BlockchainUrl { get; set; }
    public string? Name { get; set; }
    public BigInteger? FromBlock { get; set; }
    public BigInteger? ToBlock { get; set; }
    public uint? MinimumBlockConfirmations { get; set; }
    public int ReorgBuffer { get; set; } = 0;
    public int NumberOfBlocksToProcessPerRequest { get; set; } = 1000;
    public int RetryWeight { get; set; } = 50;
    public bool UseBatchReceipts { get; set; } = true;
    public bool ProcessBlockTransactionsInParallel { get; set; } = true;
    public bool PostVm { get; set; } = false;
}
```

Bound from `IConfiguration` section `"BlockchainProcessing"` or root keys.

## Core Pipeline Architecture

The processing pipeline has three layers:

1. **BlockchainProcessor** -- the main loop. Calls the orchestrator in a while loop, tracks progress, handles reorgs.
2. **BlockCrawlOrchestrator / LogOrchestrator** -- fetches blocks or logs from the chain in batches.
3. **BlockProcessingSteps** -- a set of typed processors that handle each entity (block, transaction, receipt, log, contract creation).

### BlockProcessingSteps

```csharp
public class BlockProcessingSteps
{
    public IProcessor<BlockWithTransactions> BlockStep;
    public IProcessor<TransactionVO> TransactionStep;
    public IProcessor<TransactionReceiptVO> TransactionReceiptStep;
    public IProcessor<FilterLogVO> FilterLogStep;
    public IProcessor<ContractCreationVO> ContractCreationStep;
}
```

Each step is a `Processor<T>` that holds a list of `ProcessorHandler<T>` instances. Add handlers to react to each entity type.

## Block Processing (Custom Handlers)

Use `web3.Processing.Blocks` to create a block processor with custom step handlers:

```csharp
using Nethereum.BlockchainProcessing.BlockProcessing;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.Web3;

var web3 = new Web3("https://eth.llamarpc.com");
var progressRepo = new JsonBlockProgressRepository(
    jsonSourceExists: () => Task.FromResult(File.Exists("progress.json")),
    jsonWriter: json => File.WriteAllTextAsync("progress.json", json),
    jsonRetriever: () => File.ReadAllTextAsync("progress.json"));

var processor = web3.Processing.Blocks.CreateBlockProcessor(
    progressRepo,
    steps =>
    {
        steps.BlockStep.AddSynchronousProcessorHandler(block =>
            Console.WriteLine($"Block {block.Number} with {block.Transactions.Length} txs"));

        steps.TransactionStep.AddSynchronousProcessorHandler(tx =>
            Console.WriteLine($"  Tx {tx.Transaction.TransactionHash}"));

        steps.TransactionReceiptStep.AddSynchronousProcessorHandler(receipt =>
            Console.WriteLine($"  Receipt status: {receipt.TransactionReceipt.Status}"));

        steps.FilterLogStep.AddSynchronousProcessorHandler(log =>
            Console.WriteLine($"  Log {log.Log.Address} topic0={log.Log.EventSignature}"));

        steps.ContractCreationStep.AddSynchronousProcessorHandler(contract =>
            Console.WriteLine($"  Contract created: {contract.ContractAddress}"));
    },
    minimumBlockConfirmations: 12);

var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
await processor.ExecuteAsync(cts.Token, startAtBlockNumberIfNotProcessed: 20000000);
```

### Block Storage Processor

To store everything in a database automatically:

```csharp
var repoFactory = new BlockchainStoreRepositoryFactory(dbContextFactory);

var processor = web3.Processing.Blocks.CreateBlockStorageProcessor(
    repoFactory,
    progressRepo,
    minimumBlockConfirmations: 12);

await processor.ExecuteAsync(cancellationToken);
```

## Log Processing (Event Crawling)

Use `web3.Processing.Logs` for targeted event log crawling without full block processing:

### Typed Event Processing

```csharp
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;

var processor = web3.Processing.Logs.CreateProcessor<TransferEventDTO>(
    action: transfer =>
        Console.WriteLine($"Transfer {transfer.Event.From} -> {transfer.Event.To}: {transfer.Event.Value}"),
    minimumBlockConfirmations: 12,
    criteria: transfer => transfer.Event.Value > 0);

await processor.ExecuteAsync(cts.Token, startAtBlockNumberIfNotProcessed: 20000000);
```

### Contract-Specific Event Processing

```csharp
var usdcAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";

var processor = web3.Processing.Logs.CreateProcessorForContract<TransferEventDTO>(
    usdcAddress,
    action: transfer =>
        Console.WriteLine($"USDC Transfer: {transfer.Event.Value}"),
    minimumBlockConfirmations: 12);

await processor.ExecuteAsync(cts.Token, startAtBlockNumberIfNotProcessed: 20000000);
```

### Multiple Contracts

```csharp
var processor = web3.Processing.Logs.CreateProcessorForContracts<TransferEventDTO>(
    new[] { "0xA0b86991...", "0xdAC17F958..." },
    action: transfer => { /* handle */ },
    minimumBlockConfirmations: 12);
```

### Bulk Event Retrieval

```csharp
var transfers = await web3.Processing.Logs.ERC20
    .GetAllTransferEventsForContract(
        usdcAddress, fromBlockNumber: 20000000, toBlockNumber: 20001000,
        cancellationToken);

var accountTransfers = await web3.Processing.Logs.ERC20
    .GetAllTransferEventsFromAndToAccount(
        usdcAddress, "0xMyAddress...",
        fromBlockNumber: 20000000, toBlockNumber: null,
        cancellationToken);
```

### Raw FilterLog Processing

```csharp
var processor = web3.Processing.Logs.CreateProcessor(
    action: (FilterLog log) =>
        Console.WriteLine($"Log from {log.Address}"),
    minimumBlockConfirmations: 12,
    filter: new NewFilterInput { Address = new[] { contractAddress } });
```

### With Reorg Buffer

```csharp
var processor = web3.Processing.Logs.CreateProcessor(
    logProcessors: handlers,
    minimumBlockConfirmations: 12,
    reorgBuffer: 20,
    filter: filterInput,
    blockProgressRepository: progressRepo);
```

## Progress Tracking

### IBlockProgressRepository

```csharp
public interface IBlockProgressRepository
{
    Task UpsertProgressAsync(BigInteger blockNumber);
    Task<BigInteger?> GetLastBlockNumberProcessedAsync();
}
```

### Built-in Implementations

**JsonBlockProgressRepository** -- persists to a JSON file:

```csharp
var progressRepo = new JsonBlockProgressRepository(
    jsonSourceExists: () => Task.FromResult(File.Exists("progress.json")),
    jsonWriter: json => File.WriteAllTextAsync("progress.json", json),
    jsonRetriever: () => File.ReadAllTextAsync("progress.json"),
    lastBlockProcessed: 19999999);
```

**InMemoryBlockchainProgressRepository** -- for testing or one-shot runs:

```csharp
var progressRepo = new InMemoryBlockchainProgressRepository(startBlock);
```

**Database-backed** -- `BlockchainStoreRepositoryFactory.CreateBlockProgressRepository()` stores progress in the same DB as block data.

### ReorgBufferedBlockProgressRepository

Wraps any progress repository to subtract a reorg buffer from the last-processed block, causing re-processing of recent blocks:

```csharp
var buffered = new ReorgBufferedBlockProgressRepository(innerRepo, reorgBuffer: 20);
```

When `GetLastBlockNumberProcessedAsync()` returns 100, the buffered repo returns 80, so the processor re-crawls blocks 81-100 each cycle.

## Reorg Handling

The pipeline supports chain reorganisation detection and recovery:

1. **ReorgBuffer** on `BlockchainProcessingOptions` -- re-processes the last N blocks each cycle to catch shallow reorgs.
2. **ChainConsistencyValidator** -- validates parent hash continuity. Throws `ReorgDetectedException` when a mismatch is found.
3. **ReorgDetectedException** -- carries `RewindToBlockNumber`, `LastCanonicalBlockNumber`, `LastCanonicalBlockHash`.
4. **BlockchainProcessor** catches the exception, rewinds progress, and continues from the rewind point.
5. **IsCanonical flags** -- `Block`, `Transaction`, and `TransactionLog` entities all have `IsCanonical` bool. Non-canonical data is marked via `INonCanonicalBlockRepository`, `INonCanonicalTransactionRepository`, `INonCanonicalTransactionLogRepository`.

```csharp
// Full reorg-aware processor with chain state validation
var processor = web3.Processing.Logs.CreateProcessor(
    logProcessors: handlers,
    minimumBlockConfirmations: 12,
    reorgBuffer: 20,
    chainStateRepository: repoFactory.CreateChainStateRepository(),
    filter: filterInput,
    blockProgressRepository: progressRepo);
```

## Entity Models

All entities inherit from `TableRow` (provides `RowIndex`, `RowCreated`, `RowUpdated`).

### Block

```csharp
public class Block : TableRow, IBlockView
{
    public long BlockNumber { get; set; }
    public string Hash { get; set; }
    public string ParentHash { get; set; }
    public string Miner { get; set; }
    public string GasLimit { get; set; }
    public string GasUsed { get; set; }
    public long Timestamp { get; set; }
    public long TransactionCount { get; set; }
    public string BaseFeePerGas { get; set; }
    public string StateRoot { get; set; }
    public bool IsCanonical { get; set; } = true;
    public bool IsFinalized { get; set; }
    public int? ChainId { get; set; }
    // + Difficulty, TotalDifficulty, Nonce, ExtraData, Size,
    //   ReceiptsRoot, LogsBloom, WithdrawalsRoot, BlobGasUsed,
    //   ExcessBlobGas, ParentBeaconBlockRoot, RequestsHash, etc.
}
```

### Transaction

```csharp
public class TransactionBase : TableRow, ITransactionView
{
    public string BlockHash { get; set; }
    public long BlockNumber { get; set; }
    public string Hash { get; set; }
    public string AddressFrom { get; set; }
    public string AddressTo { get; set; }
    public string Value { get; set; }
    public string Gas { get; set; }
    public string GasPrice { get; set; }
    public string GasUsed { get; set; }
    public string Input { get; set; }
    public long Nonce { get; set; }
    public bool Failed { get; set; }
    public string Error { get; set; }
    public string NewContractAddress { get; set; }
    public bool IsCanonical { get; set; } = true;
    public string MaxFeePerGas { get; set; }
    public string MaxPriorityFeePerGas { get; set; }
    public long TransactionType { get; set; }
    // + EffectiveGasPrice, CumulativeGasUsed, RevertReason,
    //   MaxFeePerBlobGas, BlobGasUsed, BlobGasPrice, etc.
}
```

### TransactionLog

```csharp
public class TransactionLog : TableRow, ITransactionLogView
{
    public string TransactionHash { get; set; }
    public long LogIndex { get; set; }
    public string Address { get; set; }
    public string EventHash { get; set; }
    public string IndexVal1 { get; set; }
    public string IndexVal2 { get; set; }
    public string IndexVal3 { get; set; }
    public string Data { get; set; }
    public long BlockNumber { get; set; }
    public string BlockHash { get; set; }
    public bool IsCanonical { get; set; } = true;
}
```

### Contract

```csharp
public class Contract : TableRow, IContractView
{
    public string Address { get; set; }
    public string Name { get; set; }
    public string ABI { get; set; }
    public string Code { get; set; }
    public string Creator { get; set; }
    public string TransactionHash { get; set; }
}
```

### TokenMetadata

```csharp
public class TokenMetadata : TableRow, ITokenMetadataView
{
    public string ContractAddress { get; set; }
    public string Name { get; set; }
    public string Symbol { get; set; }
    public int Decimals { get; set; }
    public string TokenType { get; set; }
}
```

### TokenBalance

```csharp
public class TokenBalance : TableRow, ITokenBalanceView
{
    public string Address { get; set; }
    public string ContractAddress { get; set; }
    public string Balance { get; set; }
    public string TokenType { get; set; }
    public long LastUpdatedBlockNumber { get; set; }
}
```

## Repository Interfaces

### Core Storage

```csharp
public interface IBlockchainStoreRepositoryFactory
{
    IBlockRepository CreateBlockRepository();
    ITransactionRepository CreateTransactionRepository();
    ITransactionLogRepository CreateTransactionLogRepository();
    IContractRepository CreateContractRepository();
    IAddressTransactionRepository CreateAddressTransactionRepository();
    ITransactionVMStackRepository CreateTransactionVmStackRepository();
}

public interface IBlockRepository
{
    Task UpsertBlockAsync(Block source);
    Task<IBlockView> FindByBlockNumberAsync(HexBigInteger blockNumber);
}

public interface ITransactionRepository
{
    Task UpsertAsync(TransactionReceiptVO transactionReceiptVO);
    Task UpsertAsync(TransactionReceiptVO transactionReceiptVO, string code, bool failedCreatingContract);
    Task<ITransactionView> FindByBlockNumberAndHashAsync(HexBigInteger blockNumber, string hash);
}

public interface ITransactionLogRepository
{
    Task UpsertAsync(FilterLogVO log);
    Task<ITransactionLogView> FindByTransactionHashAndLogIndexAsync(string hash, BigInteger logIndex);
}
```

### Token Repositories

```csharp
public interface ITokenBalanceRepository
{
    Task UpsertAsync(TokenBalance balance);
    Task UpsertBatchAsync(IEnumerable<TokenBalance> balances);
    Task<IEnumerable<ITokenBalanceView>> GetByAddressAsync(string address);
    Task<IEnumerable<ITokenBalanceView>> GetByContractAsync(string contractAddress, int page, int pageSize);
    Task DeleteByBlockNumberAsync(BigInteger blockNumber);
}

public interface INFTInventoryRepository
{
    Task UpsertAsync(NFTInventory item);
    Task UpsertBatchAsync(IEnumerable<NFTInventory> items);
    Task<IEnumerable<INFTInventoryView>> GetByAddressAsync(string address);
    Task<INFTInventoryView> GetByTokenAsync(string contractAddress, string tokenId);
}
```

### Reorg Repositories

```csharp
public interface INonCanonicalBlockRepository
{
    Task MarkNonCanonicalAsync(BigInteger fromBlockNumber);
}

public interface INonCanonicalTransactionRepository
{
    Task MarkNonCanonicalAsync(BigInteger fromBlockNumber);
}
```

## ERC-20 / ERC-721 Log Processing Services

Built-in services for common token event crawling:

```csharp
// Access via web3.Processing.Logs.ERC20 / web3.Processing.Logs.ERC721

// Get all ERC-20 Transfer events for a contract
var transfers = await web3.Processing.Logs.ERC20
    .GetAllTransferEventsForContract(contractAddress, fromBlock, toBlock, cancellationToken);

// Get all transfers to/from a specific account (any contract)
var myTransfers = await web3.Processing.Logs.ERC20
    .GetAllTransferEventsFromAndToAccount(account, fromBlock, toBlock, cancellationToken);

// Get all transfers to/from account for specific contracts
var filtered = await web3.Processing.Logs.ERC20
    .GetAllTransferEventsFromAndToAccount(
        new[] { usdcAddress, daiAddress }, account, fromBlock, toBlock, cancellationToken);
```

## ExecuteAsync Modes

The `BlockchainProcessor` supports two execution modes:

```csharp
// Continuous: runs until cancellation, following the chain head
await processor.ExecuteAsync(
    cancellationToken: cts.Token,
    startAtBlockNumberIfNotProcessed: 20000000,
    waitInterval: 1000);

// Bounded: runs from start to a specific block number, then stops
await processor.ExecuteAsync(
    toBlockNumber: 20100000,
    cancellationToken: cts.Token,
    startAtBlockNumberIfNotProcessed: 20000000);
```

## Dependency Injection (Manual Setup)

```csharp
// Register options
services.AddBlockchainProcessingOptions(configuration);

// Register EF Core storage (pick one)
services.AddPostgresBlockchainStorage(connectionString);
// or: services.AddSqlServerBlockchainStorage(connectionString, schema);
// or: services.AddSqliteBlockchainStorage(connectionString);

// Register processor + hosted service
services.AddBlockchainProcessor();

// Optional: internal transaction processor
services.AddInternalTransactionProcessor();
```

## Metrics / Observability

Implement `ILogProcessingObserver` for custom metrics:

```csharp
public interface ILogProcessingObserver
{
    void SetChainHead(BigInteger blockNumber);
    void OnBlockProgressUpdated(BigInteger blockNumber);
    void OnReorgDetected(BigInteger rewindTo, BigInteger lastCanonical);
    void OnError(string errorType);
}
```
