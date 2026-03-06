# Nethereum.BlockchainProcessing

**Nethereum.BlockchainProcessing** provides a comprehensive framework for crawling, processing, and storing Ethereum blockchain data including blocks, transactions, receipts, and event logs with flexible filtering, progress tracking, and storage capabilities.

## Overview

This package enables applications to:
- **Crawl blockchain data** - Process blocks, transactions, receipts, and logs sequentially
- **Track progress** - Resume processing from last processed block
- **Store data** - Persist blockchain data to custom storage backends
- **Filter events** - Process specific event types or contracts
- **Handle confirmations** - Wait for block confirmations before processing
- **Batch process logs** - Efficiently retrieve and process event logs in batches
- **Process ERC20/ERC721/ERC1155** - Built-in support for token transfer indexing and balance aggregation
- **Detect reorgs** - Chain consistency validation with automatic rewind and non-canonical marking
- **Index internal transactions** - Trace-based call tree extraction via configurable trace provider
- **Emit metrics** - OpenTelemetry-compatible instrumentation via `ILogProcessingObserver`

## Installation

```bash
dotnet add package Nethereum.BlockchainProcessing
```

## Core Architecture

The package follows a modular pipeline architecture with clear separation of concerns:

```
BlockchainProcessor (executor)
  ├── IBlockchainProcessingOrchestrator (strategy)
  │   ├── BlockCrawlOrchestrator (block-by-block crawling)
  │   ├── LogOrchestrator (batch log retrieval)
  │   └── InternalTransactionOrchestrator (trace-based indexing)
  ├── BlockProcessingSteps (pipeline stages)
  ├── IBlockProgressRepository (progress tracking)
  │   └── ReorgBufferedBlockProgressRepository (decorator)
  ├── IChainStateRepository (reorg detection state)
  ├── ILastConfirmedBlockNumberService (confirmation management)
  └── ILogProcessingObserver (metrics/telemetry)
```

## Core Components

### BlockchainProcessor

Main processor that manages continuous blockchain processing. Located in `BlockchainProcessor.cs`.

**Key Methods:**
- `ExecuteAsync(CancellationToken, BigInteger?)` - Process until cancelled
- `ExecuteAsync(BigInteger toBlockNumber, ...)` - Process to specific block

**Features:**
- Progress tracking via `IBlockProgressRepository`
- Automatic block confirmation handling
- Cancellation token support
- Resume from last processed block

### BlockCrawlOrchestrator

Orchestrates crawling of blocks, transactions, receipts, and logs. Located in `BlockProcessing/BlockCrawlOrchestrator.cs`.

**Processing Flow:**
1. **Fetch Block** → `BlockCrawlerStep`
2. **For Each Transaction:**
   - Process Transaction → `TransactionCrawlerStep`
   - Fetch Receipt → `TransactionReceiptCrawlerStep`
   - Extract Contract Creation → `ContractCreatedCrawlerStep` (if applicable)
3. **For Each Log:**
   - Process Log → `FilterLogCrawlerStep`

### BlockProcessingSteps

Defines processing pipeline stages. Located in `BlockProcessing/BlockProcessingSteps.cs`.

**Steps:**
- `BlockStep` - Processes `BlockWithTransactions`
- `TransactionStep` - Processes `TransactionVO`
- `TransactionReceiptStep` - Processes `TransactionReceiptVO`
- `FilterLogStep` - Processes `FilterLogVO`
- `ContractCreationStep` - Processes `ContractCreationVO`

Each step is a `Processor<T>` that can have multiple handlers.

### Processor<T>

Generic processor that executes multiple handlers. Located in `Processor/Processor.cs`.

**Key Features:**
- Multiple handlers per processor
- Optional match criteria for filtering
- Sequential handler execution
- Synchronous and asynchronous handler support

## Usage Examples

### Example 1: Basic Block Processing

Process all blocks, transactions, and logs:

```csharp
using Nethereum.BlockchainProcessing;
using Nethereum.Web3;
using System.Numerics;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");

var processedData = new ProcessedData();

var blockProcessor = web3.Processing.Blocks.CreateBlockProcessor(steps =>
{
    // Process each block
    steps.BlockStep.AddSynchronousProcessorHandler(block =>
    {
        Console.WriteLine($"Block: {block.Number}");
        processedData.Blocks.Add(block);
    });

    // Process each transaction
    steps.TransactionStep.AddSynchronousProcessorHandler(tx =>
    {
        Console.WriteLine($"  Transaction: {tx.Transaction.TransactionHash}");
        processedData.Transactions.Add(tx);
    });

    // Process transaction receipts
    steps.TransactionReceiptStep.AddSynchronousProcessorHandler(tx =>
    {
        Console.WriteLine($"  Receipt - Gas Used: {tx.TransactionReceipt.GasUsed}");
        processedData.TransactionsWithReceipt.Add(tx);
    });

    // Process event logs
    steps.FilterLogStep.AddSynchronousProcessorHandler(filterLog =>
    {
        Console.WriteLine($"    Log: {filterLog.Log.Address}");
        processedData.FilterLogs.Add(filterLog);
    });
});

// Process blocks 100-110
await blockProcessor.ExecuteAsync(
    toBlockNumber: new BigInteger(110),
    cancellationToken: CancellationToken.None,
    startAtBlockNumberIfNotProcessed: new BigInteger(100)
);
```

From test: `BlockProcessing/BlockProcessingTests.cs:17-44`

### Example 2: Processing with Progress Tracking

Resume processing from last processed block:

```csharp
using Nethereum.BlockchainProcessing.ProgressRepositories;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");

// Track progress (persists across runs)
var progressRepository = new InMemoryBlockchainProgressRepository(
    lastBlockProcessed: new BigInteger(1000)
);

var blockProcessor = web3.Processing.Blocks.CreateBlockProcessor(
    progressRepository,
    steps =>
    {
        steps.BlockStep.AddSynchronousProcessorHandler(block =>
        {
            Console.WriteLine($"Processing block: {block.Number}");
        });
    }
);

// Process continuously until cancelled
// Will start from block 1001 (last processed + 1)
var cancellationTokenSource = new CancellationTokenSource();

await blockProcessor.ExecuteAsync(cancellationTokenSource.Token);

// Progress is automatically saved after each block
```

From test: `BlockProcessing/BlockProcessingTests.cs:164-195`

### Example 3: Block Confirmations

Wait for block confirmations before processing:

```csharp
const uint MIN_CONFIRMATIONS = 12;

var progressRepository = new InMemoryBlockchainProgressRepository(
    lastBlockProcessed: new BigInteger(100)
);

var blockProcessor = web3.Processing.Blocks.CreateBlockProcessor(
    progressRepository,
    steps =>
    {
        steps.BlockStep.AddSynchronousProcessorHandler(block =>
        {
            Console.WriteLine($"Processing confirmed block: {block.Number}");
        });
    },
    minimumBlockConfirmations: MIN_CONFIRMATIONS  // Wait for 12 confirmations
);

await blockProcessor.ExecuteAsync(CancellationToken.None);

// Only processes blocks with at least 12 confirmations
// If latest block is 1000, will process up to block 988
```

From test: `BlockProcessing/BlockProcessingTests.cs:231-267`

### Example 4: Filtering with Criteria

Process only specific transactions:

```csharp
var blockProcessor = web3.Processing.Blocks.CreateBlockProcessor(steps =>
{
    // Only process transactions with non-zero value
    steps.TransactionStep.SetMatchCriteria(tx =>
        tx.Transaction.Value?.Value > 0);

    // Only process receipts for transaction index 0
    steps.TransactionReceiptStep.SetMatchCriteria(tx =>
        tx.Transaction.TransactionIndex.Value == 0);

    steps.TransactionReceiptStep.AddSynchronousProcessorHandler(tx =>
    {
        Console.WriteLine($"High-value transaction at index 0: {tx.TransactionHash}");
    });
});

await blockProcessor.ExecuteAsync(new BigInteger(110), CancellationToken.None, new BigInteger(100));

// Only transactions matching ALL criteria will be processed
```

From test: `BlockProcessing/BlockProcessingTests.cs:134-161`

### Example 5: Disabling Processing Steps

Optimize by skipping unnecessary steps:

```csharp
var blockProcessor = web3.Processing.Blocks.CreateBlockProcessor(steps =>
{
    // Only interested in blocks, not transactions
    steps.BlockStep.AddSynchronousProcessorHandler(block =>
    {
        Console.WriteLine($"Block {block.Number}: {block.TransactionCount} transactions");
    });
});

// Disable receipt and log processing for performance
blockProcessor.Orchestrator.TransactionWithReceiptCrawlerStep.Enabled = false;
blockProcessor.Orchestrator.FilterLogCrawlerStep.Enabled = false;

await blockProcessor.ExecuteAsync(new BigInteger(110), CancellationToken.None, new BigInteger(100));

// Receipts and logs won't be fetched or processed
```

From test: `BlockProcessing/BlockProcessingTests.cs:76-106`

### Example 6: Block Storage Processor

Automatically store blockchain data to repositories:

```csharp
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");

// In-memory storage (replace with your database implementation)
var context = new InMemoryBlockchainStorageRepositoryContext();
var repositoryFactory = new InMemoryBlockchainStorageRepositoryFactory(context);

var processor = web3.Processing.Blocks.CreateBlockStorageProcessor(
    repositoryFactory,
    minimumBlockConfirmations: 6
);

// Process and automatically store blocks, transactions, and logs
await processor.ExecuteAsync(
    toBlockNumber: new BigInteger(110),
    cancellationToken: CancellationToken.None,
    startAtBlockNumberIfNotProcessed: new BigInteger(100)
);

// Data is automatically persisted
Console.WriteLine($"Blocks stored: {context.Blocks.Count}");
Console.WriteLine($"Transactions stored: {context.Transactions.Count}");
Console.WriteLine($"Logs stored: {context.TransactionLogs.Count}");
```

From test: `BlockStorage/BlockStorageProcessorTests.cs:16-39`

### Example 7: Custom Storage Configuration

Add custom processing alongside storage:

```csharp
var repositoryFactory = new InMemoryBlockchainStorageRepositoryFactory(context);

var processor = web3.Processing.Blocks.CreateBlockStorageProcessor(
    repositoryFactory,
    minimumBlockConfirmations: 6,
    configureSteps: steps =>
    {
        // Add custom handler alongside automatic storage
        steps.BlockStep.AddSynchronousProcessorHandler(block =>
        {
            // Send notification, update cache, etc.
            Console.WriteLine($"New block stored: {block.Number}");
        });

        // Add custom filtering
        steps.TransactionStep.SetMatchCriteria(tx =>
            tx.Transaction.Value?.Value > Web3.Convert.ToWei(1));
    }
);

await processor.ExecuteAsync(new BigInteger(110), CancellationToken.None, new BigInteger(100));
```

## Log Processing

For event-focused processing, use `LogOrchestrator` for efficient batch retrieval. Located in `LogProcessing/LogOrchestrator.cs`.

### Example 8: Process All Logs

```csharp
var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");

var logsProcessed = new List<FilterLog>();

var logProcessor = web3.Processing.Logs.CreateProcessor(
    filterLog => logsProcessed.Add(filterLog)
);

// Batch retrieval of logs (more efficient than block-by-block)
await logProcessor.ExecuteAsync(
    toBlockNumber: new BigInteger(110),
    cancellationToken: CancellationToken.None,
    startAtBlockNumberIfNotProcessed: new BigInteger(100)
);

Console.WriteLine($"Processed {logsProcessed.Count} logs");
```

From test: `LogProcessing/LogProcessingTests.cs:15-60`

### Example 9: Process Specific Event Type

Process typed events with automatic decoding:

```csharp
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");

var transferEvents = new List<EventLog<TransferEventDTO>>();

var logProcessor = web3.Processing.Logs.CreateProcessor<TransferEventDTO>(
    transferEvent =>
    {
        Console.WriteLine($"Transfer: {transferEvent.Event.Value} from {transferEvent.Event.From} to {transferEvent.Event.To}");
        transferEvents.Add(transferEvent);
    }
);

await logProcessor.ExecuteAsync(
    toBlockNumber: new BigInteger(110),
    startAtBlockNumberIfNotProcessed: new BigInteger(100)
);

Console.WriteLine($"Total transfers: {transferEvents.Count}");
```

From test: `LogProcessing/LogProcessingForEventTests.cs:15-41`

### Example 10: Event Processing with Criteria

Filter events during processing:

```csharp
var largeTransfers = new List<EventLog<TransferEventDTO>>();

var logProcessor = web3.Processing.Logs.CreateProcessor<TransferEventDTO>(
    // Action
    action: transferEventLog =>
    {
        largeTransfers.Add(transferEventLog);
        return Task.CompletedTask;
    },
    // Criteria - only transfers over 1 ETH equivalent
    criteria: transferEventLog =>
    {
        var match = transferEventLog.Event.Value > Web3.Convert.ToWei(1);
        return Task.FromResult(match);
    }
);

await logProcessor.ExecuteAsync(
    toBlockNumber: new BigInteger(110),
    startAtBlockNumberIfNotProcessed: new BigInteger(100)
);

Console.WriteLine($"Large transfers: {largeTransfers.Count}");
```

From test: `LogProcessing/LogProcessingForEventTests.cs:68-99`

### Example 11: Process Contract-Specific Events

Process events from specific contract:

```csharp
var usdcAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";

var logProcessor = web3.Processing.Logs.CreateProcessorForContract<TransferEventDTO>(
    usdcAddress,
    transferEvent =>
    {
        Console.WriteLine($"USDC Transfer: {transferEvent.Event.Value}");
    }
);

await logProcessor.ExecuteAsync(new BigInteger(110), startAtBlockNumberIfNotProcessed: new BigInteger(100));
```

From: `Services/BlockchainLogProcessingService.cs:185-218`

### Example 12: Process Multiple Contracts

```csharp
var tokenAddresses = new[]
{
    "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", // USDC
    "0xdAC17F958D2ee523a2206206994597C13D831ec7"  // USDT
};

var logProcessor = web3.Processing.Logs.CreateProcessorForContracts<TransferEventDTO>(
    tokenAddresses,
    transferEvent =>
    {
        Console.WriteLine($"Stablecoin Transfer at {transferEvent.Log.Address}: {transferEvent.Event.Value}");
    }
);

await logProcessor.ExecuteAsync(new BigInteger(110), startAtBlockNumberIfNotProcessed: new BigInteger(100));
```

From: `Services/BlockchainLogProcessingService.cs:220-261`

## ERC20/ERC721 Processing

### Example 13: ERC20 Transfer Processing

Built-in support for ERC20 token tracking:

```csharp
using Nethereum.BlockchainProcessing.Services.SmartContracts;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");
var erc20Service = new ERC20LogProcessingService(web3.Eth);

var usdcAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";

// Get all USDC transfers in block range
var transfers = await erc20Service.GetAllTransferEventsForContract(
    contractAddress: usdcAddress,
    fromBlockNumber: new BigInteger(100),
    toBlockNumber: new BigInteger(110),
    cancellationToken: CancellationToken.None
);

foreach (var transfer in transfers)
{
    Console.WriteLine($"Transfer: {transfer.Event.Value} from {transfer.Event.From} to {transfer.Event.To}");
    Console.WriteLine($"  Block: {transfer.Log.BlockNumber}, Tx: {transfer.Log.TransactionHash}");
}
```

From: `Services/SmartContracts/ERC20LogProcessingService.cs:23-29`

### Example 14: Track Account Token Activity

Get all transfers involving specific account:

```csharp
var tokenAddresses = new[]
{
    "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", // USDC
    "0xdAC17F958D2ee523a2206206994597C13D831ec7"  // USDT
};

var accountAddress = "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb";

// Get transfers TO and FROM the account
var accountTransfers = await erc20Service.GetAllTransferEventsFromAndToAccount(
    contractAddresses: tokenAddresses,
    account: accountAddress,
    fromBlockNumber: new BigInteger(100),
    toBlockNumber: new BigInteger(110)
);

var received = accountTransfers.Count(t => t.Event.To.Equals(accountAddress, StringComparison.OrdinalIgnoreCase));
var sent = accountTransfers.Count(t => t.Event.From.Equals(accountAddress, StringComparison.OrdinalIgnoreCase));

Console.WriteLine($"Received: {received}, Sent: {sent}");
```

From: `Services/SmartContracts/ERC20LogProcessingService.cs:47-62`

### Example 15: ERC721 Ownership Tracking

Track NFT ownership from transfer events:

```csharp
using Nethereum.BlockchainProcessing.Services.SmartContracts;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");
var erc721Service = new ERC721LogProcessingService(web3.Eth);

var nftContract = "0xYourNFTContract";

// Get current owners by processing all transfer events
var owners = await erc721Service.GetAllCurrentOwnersProcessingAllTransferEvents(
    contractAddress: nftContract,
    fromBlockNumber: new BigInteger(0),  // Process all history
    toBlockNumber: null  // Up to latest
);

foreach (var owner in owners)
{
    Console.WriteLine($"Token #{owner.TokenId}: Owned by {owner.Owner}");
}
```

From: `Services/SmartContracts/ERC721LogProcessingService.cs:51-58`

### Example 16: Account NFT Portfolio

Get all NFTs owned by an account:

```csharp
var accountAddress = "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb";
var nftContract = "0xYourNFTContract";

var ownedNFTs = await erc721Service.GetErc721OwnedByAccountUsingAllTransfers(
    contractAddress: nftContract,
    account: accountAddress,
    fromBlockNumber: new BigInteger(0),
    toBlockNumber: null
);

Console.WriteLine($"Account owns {ownedNFTs.Count} NFTs:");
foreach (var nft in ownedNFTs)
{
    Console.WriteLine($"  Token #{nft.TokenId}");
}
```

From: `Services/SmartContracts/ERC721LogProcessingService.cs:26-36`

## Progress Tracking

### JSON File Progress Repository

Persist progress to JSON file:

```csharp
using Nethereum.BlockchainProcessing.ProgressRepositories;
using System.IO;

var progressFile = "blockchain-progress.json";

var progressRepository = new JsonBlockProgressRepository(
    jsonSourceExists: async () => File.Exists(progressFile),
    jsonWriter: async (json) => await File.WriteAllTextAsync(progressFile, json),
    jsonRetriever: async () => await File.ReadAllTextAsync(progressFile),
    lastBlockProcessed: new BigInteger(0)  // Starting block if file doesn't exist
);

var blockProcessor = web3.Processing.Blocks.CreateBlockProcessor(
    progressRepository,
    steps => { /* configure steps */ }
);

// Progress automatically persists to JSON file
await blockProcessor.ExecuteAsync(CancellationToken.None);

// Resume from same file on next run
```

From: `ProgressRepositories/JsonBlockProgressRepository.cs:13-82`

### Custom Progress Repository

Implement `IBlockProgressRepository` for custom storage:

```csharp
public class DatabaseProgressRepository : IBlockProgressRepository
{
    private readonly IDbConnection _connection;

    public DatabaseProgressRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task UpsertProgressAsync(BigInteger blockNumber)
    {
        await _connection.ExecuteAsync(
            "UPDATE BlockchainProgress SET LastBlock = @blockNumber, UpdatedAt = @now",
            new { blockNumber = (long)blockNumber, now = DateTime.UtcNow }
        );
    }

    public async Task<BigInteger?> GetLastBlockNumberProcessedAsync()
    {
        var result = await _connection.QuerySingleOrDefaultAsync<long?>(
            "SELECT LastBlock FROM BlockchainProgress"
        );
        return result.HasValue ? new BigInteger(result.Value) : null;
    }
}
```

From interface: `ProgressRepositories/IBlockProgressRepository.cs:5-10`

## Reorg Handling

The processor supports chain reorganisation detection and recovery via `ChainConsistencyValidationService` and `IChainStateRepository`.

### Chain State Tracking

`ChainState` tracks the last known canonical block number and hash. After each block is processed, the state is updated. On startup, the stored hash is compared against the RPC node — if they differ, a reorg is detected.

```csharp
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockchainProcessing.Services;

var chainStateRepo = new MyChainStateRepository(); // implements IChainStateRepository
var validator = new ChainConsistencyValidationService(web3.Eth, chainStateRepo);
validator.ReorgBuffer = 10; // rewind 10 blocks on reorg detection

try
{
    await validator.ValidateAsync(cancellationToken);
}
catch (ReorgDetectedException ex)
{
    Console.WriteLine($"Reorg detected at block {ex.LastCanonicalBlockNumber}");
    Console.WriteLine($"Rewinding to block {ex.RewindToBlockNumber}");
    // Mark non-canonical records, rewind progress, restart processing
}
```

From: `Services/ChainConsistencyValidationService.cs`

### Chain ID Validation

Prevents accidental indexing of the wrong chain by comparing the RPC chain ID against the stored value:

```csharp
await ChainStateValidationService.EnsureChainIdMatchesAsync(
    web3.Eth, chainStateRepositoryFactory);
// Throws InvalidOperationException if chain IDs don't match
```

From: `Services/ChainStateValidationService.cs`

### ReorgBufferedBlockProgressRepository

Wraps any `IBlockProgressRepository` to subtract a reorg buffer from the reported last block, ensuring blocks within the buffer are always re-processed:

```csharp
var innerProgress = new InMemoryBlockchainProgressRepository();
var bufferedProgress = new ReorgBufferedBlockProgressRepository(innerProgress, reorgBuffer: 12);

// If inner reports block 100 as last processed, buffered returns 88
var lastBlock = await bufferedProgress.GetLastBlockNumberProcessedAsync();
```

From: `ProgressRepositories/ReorgBufferedBlockProgressRepository.cs`

## Storage System

### Implementing Custom Storage

Implement `IBlockchainStoreRepositoryFactory` for your database:

```csharp
public class MyDatabaseRepositoryFactory : IBlockchainStoreRepositoryFactory
{
    private readonly IDbConnection _connection;

    public MyDatabaseRepositoryFactory(IDbConnection connection)
    {
        _connection = connection;
    }

    public IBlockRepository CreateBlockRepository()
    {
        return new MyBlockRepository(_connection);
    }

    public ITransactionRepository CreateTransactionRepository()
    {
        return new MyTransactionRepository(_connection);
    }

    public ITransactionLogRepository CreateTransactionLogRepository()
    {
        return new MyTransactionLogRepository(_connection);
    }

    public IContractRepository CreateContractRepository()
    {
        return new MyContractRepository(_connection);
    }

    public IAddressTransactionRepository CreateAddressTransactionRepository()
    {
        return new MyAddressTransactionRepository(_connection);
    }

    public ITransactionVMStackRepository CreateTransactionVMStackRepository()
    {
        return new MyTransactionVMStackRepository(_connection);
    }
}
```

From: `BlockStorage/Repositories/IBlockchainStoreRepositoryFactory.cs:5-11`

### Storage Entities

The package provides ready-to-use entity models:

**Block Entity** (`BlockStorage/Entities/Block.cs`):
- BlockNumber (long), Hash, ParentHash, Nonce, Difficulty
- Miner, GasUsed, GasLimit, Timestamp (long)
- TransactionCount (long), BaseFeePerGas
- StateRoot, ReceiptsRoot, LogsBloom, WithdrawalsRoot
- BlobGasUsed, ExcessBlobGas, ParentBeaconBlockRoot (EIP-4844/4788)
- RequestsHash (EIP-7685), TransactionsRoot, MixHash, Sha3Uncles
- IsCanonical, IsFinalized, ChainId

**Transaction Entity** (`BlockStorage/Entities/TransactionBase.cs`):
- Hash, BlockNumber (long), TransactionIndex (long)
- AddressFrom, AddressTo, Value, Gas, GasPrice, GasUsed
- Nonce (long), TransactionType (long), TimeStamp (long)
- NewContractAddress, Failed, ReceiptHash
- MaxFeePerGas, MaxPriorityFeePerGas, EffectiveGasPrice (EIP-1559)
- MaxFeePerBlobGas, BlobGasUsed, BlobGasPrice (EIP-4844)
- IsCanonical

**TransactionLog Entity** (`BlockStorage/Entities/TransactionLog.cs`):
- TransactionHash, LogIndex (long), BlockNumber (long), Address
- EventHash (Topics[0])
- IndexVal1, IndexVal2, IndexVal3 (Indexed parameters)
- Data (Non-indexed parameters)
- IsCanonical

**Contract Entity** (`BlockStorage/Entities/Contract.cs`):
- Address, Name, ABI, Code
- Creator, TransactionHash

**InternalTransaction Entity** (`BlockStorage/Entities/InternalTransaction.cs`):
- TransactionHash, BlockNumber, BlockHash
- TraceIndex, Depth, Type (CALL, DELEGATECALL, CREATE, etc.)
- AddressFrom, AddressTo, Value, Gas, GasUsed
- Input, Output, Error, RevertReason
- IsCanonical

**TokenTransferLog Entity** (`BlockStorage/Entities/TokenTransferLog.cs`):
- TransactionHash, LogIndex, BlockNumber, BlockHash
- ContractAddress, EventHash, FromAddress, ToAddress
- Amount, TokenId, OperatorAddress, TokenType
- IsCanonical

**TokenBalance Entity** (`BlockStorage/Entities/TokenBalance.cs`):
- Address, ContractAddress, Balance, TokenType, LastUpdatedBlockNumber

**TokenMetadata Entity** (`BlockStorage/Entities/TokenMetadata.cs`):
- ContractAddress, Name, Symbol, Decimals, TokenType

**NFTInventory Entity** (`BlockStorage/Entities/NFTInventory.cs`):
- Address, ContractAddress, TokenId, Amount, TokenType, LastUpdatedBlockNumber

**AccountState Entity** (`BlockStorage/Entities/AccountState.cs`):
- Address, Balance, Nonce, IsContract, LastUpdatedBlock

**ChainState Entity** (`BlockStorage/Entities/ChainState.cs`):
- LastCanonicalBlockNumber (long?), LastCanonicalBlockHash
- FinalizedBlockNumber (long?), ChainId (int?)

All numeric indexing fields (BlockNumber, LogIndex, TransactionIndex, Nonce, Timestamp, TransactionType, TransactionCount) use `long`. Gas and value fields (Value, Gas, GasPrice, GasUsed, Balance, Amount) remain `string` to preserve full uint256 precision.

## Advanced Configuration

### Log Processing Batch Size

Configure batch size for log retrieval:

```csharp
using Nethereum.BlockchainProcessing.LogProcessing;

var logProcessor = web3.Processing.Logs.CreateProcessor(filterLog => { /* ... */ });

// Customize batch size (default: 1,000,000 blocks)
logProcessor.Orchestrator.BlockRangeRequestStrategy = new BlockRangeRequestStrategy(
    defaultNumberOfBlocksPerRequest: 10000,  // 10k blocks per batch
    retryWeight: 50  // Reduce batch size on failures
);

await logProcessor.ExecuteAsync(new BigInteger(110), startAtBlockNumberIfNotProcessed: new BigInteger(100));
```

From: `Services/BlockchainLogProcessingService.cs:24-25`

### Retry Configuration

Configure retry behavior for log retrieval:

```csharp
var logProcessor = web3.Processing.Logs.CreateProcessor(filterLog => { /* ... */ });

// Configure retries (default: 10 retries)
logProcessor.Orchestrator.MaxGetLogsRetries = 5;
logProcessor.Orchestrator.MaxGetLogsNullRetries = 2;

await logProcessor.ExecuteAsync(new BigInteger(110), startAtBlockNumberIfNotProcessed: new BigInteger(100));
```

From: `LogProcessing/LogOrchestrator.cs:57-58`

### Parallel vs Sequential Log Processing

Choose processing strategy:

```csharp
using Nethereum.BlockchainProcessing.LogProcessing;

var logProcessor = web3.Processing.Logs.CreateProcessor(filterLog => { /* ... */ });

// Sequential processing (default for most use cases)
logProcessor.Orchestrator.LogProcessStrategy = new LogProcessSequentialStrategy();

// Parallel processing (faster but uses more resources)
logProcessor.Orchestrator.LogProcessStrategy = new LogProcessParallelStrategy();

await logProcessor.ExecuteAsync(new BigInteger(110), startAtBlockNumberIfNotProcessed: new BigInteger(100));
```

From: `LogProcessing/LogOrchestrator.cs:64, LogProcessing/ILogProcessStrategy.cs`

### Contract Creation Code Retrieval

Enable code retrieval for deployed contracts:

```csharp
var blockProcessor = web3.Processing.Blocks.CreateBlockProcessor(steps =>
{
    steps.ContractCreationStep.AddSynchronousProcessorHandler(contractCreation =>
    {
        Console.WriteLine($"Contract deployed at: {contractCreation.ContractAddress}");
        Console.WriteLine($"Code length: {contractCreation.Code?.Length ?? 0}");
    });
});

// Enable code retrieval (requires extra RPC call per contract)
blockProcessor.Orchestrator.ContractCreatedCrawlerStep.RetrieveCode = true;

await blockProcessor.ExecuteAsync(new BigInteger(110), CancellationToken.None, new BigInteger(100));
```

From: `BlockProcessing/CrawlerSteps/ContractCreatedCrawlerStep.cs:9, 21`

## Performance Considerations

### Block Processing Performance

**Block-by-block processing**:
- Fetches full block data with transactions
- For each transaction, fetches receipt (separate RPC call)
- RPC calls: 1 (block) + N (receipts) per block
- Best for: Complete blockchain indexing

**Optimization tips**:
1. Disable unused steps
2. Use criteria to filter early
3. Increase minimum confirmations to avoid reorgs
4. Process in batches with multiple processors

### Log Processing Performance

**Batch log retrieval**:
- Fetches all logs in block range (single RPC call)
- RPC calls: Blocks / BatchSize
- Best for: Event-focused applications

**Batch size considerations**:
- Default: 1,000,000 blocks per batch
- Reduce for nodes with rate limits
- Increase for archive nodes
- Auto-reduces on errors via `BlockRangeRequestStrategy`

From: `LogProcessing/LogOrchestrator.cs:153-194`

### RPC Call Comparison

**Process 1000 blocks with 20 transactions each:**

Block Processing:
- 1000 block fetches
- 20,000 receipt fetches
- Total: 21,000 RPC calls

Log Processing (batch):
- 1 log fetch (if 1000 blocks < batch size)
- Total: 1 RPC call

**Recommendation**: Use LogOrchestrator for event tracking, BlockCrawlOrchestrator for complete data.

## Error Handling

### Orchestrator Error Handling

Orchestrators return error status:

```csharp
var progress = await blockProcessor.Orchestrator.ProcessAsync(
    fromNumber: new BigInteger(100),
    toNumber: new BigInteger(110),
    cancellationToken: CancellationToken.None
);

if (progress.HasErrored)
{
    Console.WriteLine($"Error processing block {progress.BlockNumberProcessTo}:");
    Console.WriteLine(progress.Exception.Message);

    // Can resume from failed block
    await blockProcessor.ExecuteAsync(
        toBlockNumber: new BigInteger(110),
        cancellationToken: CancellationToken.None,
        startAtBlockNumberIfNotProcessed: progress.BlockNumberProcessTo
    );
}
```

From: `Orchestrator/OrchestrationProgress.cs:6-11`

### Handler Error Handling

Wrap handlers in try-catch for graceful error handling:

```csharp
var blockProcessor = web3.Processing.Blocks.CreateBlockProcessor(steps =>
{
    steps.TransactionStep.AddProcessorHandler(async tx =>
    {
        try
        {
            await ProcessTransactionAsync(tx);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing transaction {tx.Transaction.TransactionHash}: {ex.Message}");
            // Log error, send alert, etc.
        }
    });
});
```

### Log Processing Retry

Log processing has built-in retry with exponential backoff:

```csharp
// Automatically retries on RPC errors
// Reduces batch size on repeated failures
// Configured via MaxGetLogsRetries and BlockRangeRequestStrategy
```

From: `LogProcessing/LogOrchestrator.cs:153-194`

## Use Cases

### Blockchain Explorer

Build complete blockchain indexer:

```csharp
var repositoryFactory = CreateDatabaseRepositoryFactory();

var processor = web3.Processing.Blocks.CreateBlockStorageProcessor(
    repositoryFactory,
    minimumBlockConfirmations: 12,
    configureSteps: steps =>
    {
        // Index all data
        // Storage handlers are automatically added

        // Add custom indexing
        steps.TransactionStep.AddProcessorHandler(async tx =>
        {
            await UpdateAddressBalanceCache(tx);
        });
    }
);

// Process continuously
await processor.ExecuteAsync(cancellationToken);
```

### DEX Event Tracker

Track Uniswap swaps:

```csharp
var swapEvents = new List<EventLog<SwapEventDTO>>();

var logProcessor = web3.Processing.Logs.CreateProcessor<SwapEventDTO>(
    swapEvent =>
    {
        var pool = swapEvent.Log.Address;
        var swap = swapEvent.Event;

        Console.WriteLine($"Swap on {pool}:");
        Console.WriteLine($"  Amount0In: {swap.Amount0In}");
        Console.WriteLine($"  Amount1Out: {swap.Amount1Out}");

        swapEvents.Add(swapEvent);
    }
);

await logProcessor.ExecuteAsync(toBlockNumber, startAtBlockNumberIfNotProcessed: fromBlockNumber);
```

### Token Balance Tracker

Track token balances for addresses:

```csharp
var balances = new Dictionary<string, BigInteger>();

var erc20Service = new ERC20LogProcessingService(web3.Eth);

var transfers = await erc20Service.GetAllTransferEventsForContract(
    usdcAddress,
    fromBlockNumber,
    toBlockNumber,
    CancellationToken.None
);

foreach (var transfer in transfers)
{
    var from = transfer.Event.From;
    var to = transfer.Event.To;
    var value = transfer.Event.Value;

    balances[from] = (balances.GetValueOrDefault(from)) - value;
    balances[to] = (balances.GetValueOrDefault(to)) + value;
}

foreach (var (address, balance) in balances)
{
    Console.WriteLine($"{address}: {balance}");
}
```

### NFT Transfer Monitor

Monitor NFT transfers in real-time:

```csharp
var progressRepo = new JsonBlockProgressRepository(/* ... */);

var logProcessor = web3.Processing.Logs.CreateProcessor<TransferEventDTO>(
    progressRepo,
    transferEvent =>
    {
        Console.WriteLine($"NFT Transfer:");
        Console.WriteLine($"  Token: {transferEvent.Event.TokenId}");
        Console.WriteLine($"  From: {transferEvent.Event.From}");
        Console.WriteLine($"  To: {transferEvent.Event.To}");
        Console.WriteLine($"  Tx: {transferEvent.Log.TransactionHash}");

        // Send notification, update database, etc.
    }
);

// Run continuously
await logProcessor.ExecuteAsync(CancellationToken.None);
```

## Token Transfer Processing

The `TokenTransferLogProcessingService` indexes ERC-20, ERC-721, and ERC-1155 transfer events into `ITokenTransferLogRepository` with a unified filter that matches all three token standards in a single log query.

```csharp
using Nethereum.BlockchainProcessing.Services.SmartContracts;

var tokenService = new TokenTransferLogProcessingService(
    web3.Processing.Logs, web3.Eth);

var processor = tokenService.CreateProcessor(
    transferLogRepository, blockProgressRepository,
    numberOfBlocksPerRequest: 1000);

await processor.ExecuteAsync(cancellationToken);
```

From: `Services/SmartContracts/TokenTransferLogProcessingService.cs`

### Token Balance Aggregation

The `TokenBalanceAggregationService` reads stored `TokenTransferLog` records and maintains running `TokenBalance` and `NFTInventory` tables:

```csharp
var aggregationService = new TokenBalanceAggregationService(
    transferLogRepository, balanceRepository, nftRepository, progressRepository);

await aggregationService.AggregateAsync(fromBlock, toBlock, cancellationToken);
```

From: `Services/SmartContracts/TokenBalanceAggregationService.cs`

## Internal Transaction Processing

The `InternalTransactionPostProcessor` orchestrates trace-based internal transaction indexing. It accepts a trace provider function (e.g., `debug_traceTransaction`) and stores results via `IInternalTransactionRepository`:

```csharp
var postProcessor = new InternalTransactionPostProcessor(
    internalTransactionRepository,
    traceProvider: async txHash => await GetTracesFromRpc(txHash),
    getContractTransactionsInRange: async (from, to) => await GetContractTxs(from, to),
    progressRepository, lastConfirmedBlockService);

await postProcessor.ExecuteAsync(cancellationToken);
```

From: `Services/InternalTransactionPostProcessor.cs`

## Metrics and Observability

The `ILogProcessingObserver` interface enables telemetry integration. The built-in `LogProcessingMetrics` implementation uses `System.Diagnostics.Metrics` (net8.0+) for OpenTelemetry-compatible instrumentation:

```csharp
using Nethereum.BlockchainProcessing.Metrics;

var metrics = new LogProcessingMetrics(
    chainId: "1", processorType: "TokenTransfers", name: "MyApp");

// Pass to log processing service
var processor = logProcessingService.CreateProcessor(
    transferLogRepository, blockProgressRepository,
    observer: metrics);

// Emitted metrics:
// logprocessing.blocks.processed   - counter
// logprocessing.logs.processed     - counter
// logprocessing.errors             - counter
// logprocessing.reorgs             - counter
// logprocessing.getlogs.retries    - counter
// logprocessing.batch.duration     - histogram
// logprocessing.last_block         - gauge
// logprocessing.lag                - gauge (blocks behind chain head)
```

From: `Metrics/LogProcessingMetrics.cs`

## RetryRunner

`RetryRunner.RunWithExponentialBackoffAsync` provides resilient execution with exponential backoff for long-running processing loops:

```csharp
using Nethereum.BlockchainProcessing;

await RetryRunner.RunWithExponentialBackoffAsync(
    async ct =>
    {
        await processor.ExecuteAsync(ct);
    },
    cancellationToken,
    onRetry: (ex, attempt, delay) =>
        logger.LogError(ex, "Processing failed (attempt {Attempt}), retrying in {Delay}s", attempt, delay),
    initialDelaySeconds: 5,
    maxDelaySeconds: 300);
```

From: `RetryRunner.cs`

## Non-Canonical Record Management

Repository interfaces for marking records as non-canonical during reorg recovery:

- `INonCanonicalBlockRepository` — `MarkNonCanonicalAsync(BigInteger blockNumber)`
- `INonCanonicalTransactionRepository` — `MarkNonCanonicalAsync(BigInteger blockNumber)`
- `INonCanonicalTransactionLogRepository` — `MarkNonCanonicalAsync(BigInteger blockNumber)`
- `INonCanonicalTokenTransferLogRepository` — `MarkNonCanonicalAsync(BigInteger blockNumber)`
- `IReorgHandler` — composite interface combining all non-canonical operations with `HandleReorgAsync(BigInteger fromBlock)`

From: `BlockStorage/Repositories/INonCanonical*.cs`, `BlockStorage/Repositories/IReorgHandler.cs`

## Dependencies

Required packages:
- **Nethereum.Hex** - Hex conversions
- **Nethereum.JsonRpc.RpcClient** - RPC client
- **Nethereum.RPC** - RPC DTOs and services
- **Nethereum.Util** - Utility functions
- **Nethereum.Contracts** - Contract interaction and event decoding

## Source Files Reference

**Core Processing:**
- `BlockchainProcessor.cs` - Main processor
- `BlockchainCrawlingProcessor.cs` - Block crawling processor
- `Orchestrator/IBlockchainProcessingOrchestrator.cs` - Orchestrator interface

**Block Processing:**
- `BlockProcessing/BlockProcessingSteps.cs` - Pipeline steps
- `BlockProcessing/BlockCrawlOrchestrator.cs` - Block crawling orchestrator
- `BlockProcessing/CrawlerSteps/*.cs` - Data fetchers

**Log Processing:**
- `LogProcessing/LogOrchestrator.cs` - Log batch processor
- `LogProcessing/BlockRangeRequestStrategy.cs` - Batch sizing strategy

**Processor Framework:**
- `Processor/IProcessor.cs` - Processor interface
- `Processor/Processor.cs` - Generic processor
- `Processor/ProcessorHandler.cs` - Handler wrapper

**Storage:**
- `BlockStorage/Repositories/IBlockchainStoreRepositoryFactory.cs` - Repository factory
- `BlockStorage/Entities/*.cs` - Storage entities
- `BlockStorage/BlockStorageProcessingSteps.cs` - Storage handlers

**Progress:**
- `ProgressRepositories/IBlockProgressRepository.cs` - Progress interface
- `ProgressRepositories/JsonBlockProgressRepository.cs` - JSON persistence
- `ProgressRepositories/IChainStateRepository.cs` - Chain state tracking
- `ProgressRepositories/ReorgBufferedBlockProgressRepository.cs` - Reorg-buffered progress

**Services:**
- `Services/BlockchainProcessingService.cs` - Service entry point
- `Services/BlockchainLogProcessingService.cs` - Log processing service
- `Services/ChainConsistencyValidationService.cs` - Reorg detection
- `Services/ChainStateValidationService.cs` - Chain ID validation
- `Services/InternalTransactionPostProcessor.cs` - Trace-based internal transaction indexing
- `Services/SmartContracts/ERC20LogProcessingService.cs` - ERC20 utilities
- `Services/SmartContracts/ERC721LogProcessingService.cs` - ERC721 utilities
- `Services/SmartContracts/TokenTransferLogProcessingService.cs` - Unified token transfer indexing
- `Services/SmartContracts/TokenBalanceAggregationService.cs` - Balance aggregation from transfers

**Metrics:**
- `Metrics/ILogProcessingObserver.cs` - Observer interface
- `Metrics/LogProcessingMetrics.cs` - OpenTelemetry metrics implementation

**Infrastructure:**
- `RetryRunner.cs` - Exponential backoff retry runner

## Related Packages

- **Nethereum.Web3** - Ethereum client library
- **Nethereum.Contracts** - Smart contract interaction
- **Nethereum.RPC** - RPC infrastructure

## Support

- GitHub: https://github.com/Nethereum/Nethereum
- Documentation: https://docs.nethereum.com
- Discord: https://discord.gg/jQPrR58FxX
