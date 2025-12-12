# Nethereum.Mud.Repositories.EntityFramework

Nethereum.Mud.Repositories.EntityFramework provides Entity Framework Core abstractions for persisting [MUD (Onchain Engine)](https://mud.dev/) table data. It enables you to sync on-chain MUD state to a relational database for querying, caching, and offline access.

## Features

- **Entity Framework Core Integration** - Abstract base repository for any EF Core provider
- **StoredRecord Persistence** - Store raw MUD table records in relational databases
- **Block Progress Tracking** - Resume synchronization from last processed block
- **Batch Processing** - Efficient paging for large datasets
- **SQL Predicate Builder** - Convert TablePredicates to SQL queries
- **Change Tracker Optimization** - AsNoTracking for memory-efficient reads
- **Table Record Mapping** - Convert StoredRecords to strongly-typed TableRecords

## Installation

```bash
dotnet add package Nethereum.Mud.Repositories.EntityFramework
```

### Dependencies

- **Microsoft.EntityFrameworkCore** 8.0+
- **Microsoft.EntityFrameworkCore.Relational** 8.0+
- Nethereum.Mud
- Nethereum.Mud.Contracts
- Nethereum.BlockchainProcessing

## Key Concepts

### StoredRecord Entity

The `StoredRecord` entity represents a persisted MUD table record:

```csharp
public class StoredRecord : EncodedValues
{
    public long RowId { get; set; }              // Auto-incrementing primary key
    public string Address { get; set; }          // World contract address
    public string TableId { get; set; }          // MUD table resource ID
    public string Key { get; set; }              // Combined key (key0 + key1 + ...)
    public string Key0 { get; set; }             // Individual key components
    public string Key1 { get; set; }
    public string Key2 { get; set; }
    public string Key3 { get; set; }
    public byte[] StaticData { get; set; }       // Static field data
    public byte[] EncodedLengths { get; set; }   // Dynamic field lengths
    public byte[] DynamicData { get; set; }      // Dynamic field data
    public bool IsDeleted { get; set; }          // Soft delete flag
    public BigInteger? BlockNumber { get; set; } // Block where change occurred
    public int? LogIndex { get; set; }           // Log index within block
}
```

### IMudStoreRecordsDbSets Interface

Your DbContext must implement this interface:

```csharp
public interface IMudStoreRecordsDbSets
{
    public DbSet<StoredRecord> StoredRecords { get; set; }
    public DbSet<BlockProgress> BlockProgress { get; set; }
}
```

### MudEFTableRepository<TDbContext>

Abstract base class providing:
- CRUD operations for StoredRecords
- Batch processing with paging
- AsNoTracking optimization for reads
- Conversion to strongly-typed TableRecords
- Block progress tracking

### BlockProgressRepository

Tracks synchronization progress:

```csharp
public interface IBlockProgressRepository
{
    Task<BigInteger?> GetLastBlockNumberProcessedAsync();
    Task UpsertProgressAsync(BigInteger blockNumber);
}
```

## Usage Examples

### Example 1: Create Custom DbContext

Extend your EF Core DbContext to implement `IMudStoreRecordsDbSets`:

```csharp
using Microsoft.EntityFrameworkCore;
using Nethereum.Mud.Repositories.EntityFramework;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

public class MyMudDbContext : DbContext, IMudStoreRecordsDbSets
{
    public DbSet<StoredRecord> StoredRecords { get; set; }
    public DbSet<BlockProgress> BlockProgress { get; set; }

    public MyMudDbContext(DbContextOptions<MyMudDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure StoredRecord primary key
        modelBuilder.Entity<StoredRecord>()
            .HasKey(r => new { r.Address, r.TableId, r.Key });

        // Add indexes for common queries
        modelBuilder.Entity<StoredRecord>()
            .HasIndex(r => new { r.Address, r.TableId })
            .HasDatabaseName("IX_Address_TableId");

        modelBuilder.Entity<StoredRecord>()
            .HasIndex(r => r.RowId)
            .HasDatabaseName("IX_RowId");

        // Configure BlockProgress primary key
        modelBuilder.Entity<BlockProgress>()
            .HasKey(b => b.RowIndex);
    }
}
```

### Example 2: Create Custom Repository

Extend `MudEFTableRepository` with your DbContext:

```csharp
using Nethereum.Mud.Repositories.EntityFramework;
using Nethereum.Mud.TableRepository;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

public class MyMudTableRepository : MudEFTableRepository<MyMudDbContext>
{
    public MyMudTableRepository(MyMudDbContext context) : base(context)
    {
    }

    // Implement abstract methods for SQL predicate handling
    public override Task<List<StoredRecord>> GetRecordsAsync(TablePredicate predicate)
    {
        var builder = new EFSqlHexPredicateBuilder();
        var sqlPredicate = builder.BuildSql(predicate);

        string sqlQuery = $"SELECT * FROM StoredRecords WHERE {sqlPredicate.Sql}";

        return Context.StoredRecords
            .FromSqlRaw(sqlQuery, sqlPredicate.GetParameterValues())
            .ToListAsync();
    }

    public override async Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>(
        TablePredicate predicate)
    {
        var storedRecords = await GetRecordsAsync(predicate);
        var result = new List<TTableRecord>();

        foreach (var storedRecord in storedRecords)
        {
            var tableRecord = new TTableRecord();
            tableRecord.DecodeValues(storedRecord);

            if (tableRecord is ITableRecord tableRecordKey)
            {
                tableRecordKey.DecodeKey(ConvertKeyFromCombinedHex(storedRecord.Key));
            }

            result.Add(tableRecord);
        }

        return result;
    }
}
```

### Example 3: Database Migrations

Create and apply migrations:

```bash
# Add initial migration
dotnet ef migrations add InitialMudSchema --context MyMudDbContext

# Update database
dotnet ef database update --context MyMudDbContext
```

Or in code:

```csharp
using Microsoft.EntityFrameworkCore;

// Apply migrations at startup
using (var context = new MyMudDbContext(options))
{
    await context.Database.MigrateAsync();
    Console.WriteLine("Database migrated successfully");
}
```

### Example 4: Sync MUD Events to Database

Process Store events and save to database:

```csharp
using Nethereum.Web3;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Repositories.EntityFramework;
using Microsoft.Extensions.Logging;

var web3 = new Web3("https://rpc.mud.game");
var worldAddress = "0xWorldAddress";

using (var context = new MyMudDbContext(options))
{
    var repository = new MyMudTableRepository(context);
    var progressRepository = new BlockProgressRepository<MyMudDbContext>(context);
    var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("MudSync");

    var storeEventsService = new StoreEventsLogProcessingService(web3, worldAddress);
    var processor = storeEventsService.CreateProcessor(
        repository,
        progressRepository,
        logger,
        blocksPerRequest: 1000,
        retryWeight: 50,
        minimumBlockConfirmations: 0
    );

    // Start syncing from block 0 (or resume from last processed block)
    await processor.ExecuteAsync(
        startAtBlockNumberIfNotProcessed: 0,
        cancellationToken: CancellationToken.None
    );
}
```

### Example 5: Query StoredRecords with Paging

Efficiently process large datasets using paging:

```csharp
using (var context = new MyMudDbContext(options))
{
    var repository = new MyMudTableRepository(context);
    long? lastRowId = null;
    int pageSize = 100;

    while (true)
    {
        var page = await repository.GetStoredRecordsAsync(pageSize, lastRowId);

        Console.WriteLine($"Processing page: {page.Records.Count} records");
        Console.WriteLine($"Total records in database: {page.TotalRecords}");

        foreach (var record in page.Records)
        {
            Console.WriteLine($"RowId: {record.RowId}, TableId: {record.TableId}, Key: {record.Key}");
        }

        // Break if no more records
        if (!page.LastRowId.HasValue || page.Records.Count == 0)
            break;

        lastRowId = page.LastRowId;
    }
}
```

### Example 6: Query by Block Number Range

Process records incrementally by block number:

```csharp
using (var context = new MyMudDbContext(options))
{
    var repository = new MyMudTableRepository(context);
    BigInteger? lastBlockNumber = null;
    long? lastRowId = null;
    int pageSize = 100;

    while (true)
    {
        var page = await repository.GetStoredRecordsGreaterThanBlockNumberAsync(
            pageSize,
            lastBlockNumber,
            lastRowId
        );

        Console.WriteLine($"Processing {page.Records.Count} records from block {page.LastBlockNumber}");

        foreach (var record in page.Records)
        {
            Console.WriteLine($"Block: {record.BlockNumber}, TableId: {record.TableId}");
            // Process record...
        }

        if (!page.LastBlockNumber.HasValue || page.Records.Count == 0)
            break;

        lastBlockNumber = page.LastBlockNumber;
        lastRowId = page.LastRowId;
    }
}
```

### Example 7: Convert StoredRecords to TableRecords

Retrieve strongly-typed MUD table records:

```csharp
using Nethereum.Mud.TableRepository;

// Assume PlayerTableRecord is a generated MUD table record
using (var context = new MyMudDbContext(options))
{
    var repository = new MyMudTableRepository(context);

    // Get all records for a specific table
    var playerResource = new Resource("Game", "Player");
    var tableIdHex = playerResource.ResourceIdEncoded.ToHex(true);

    var playerRecords = await repository.GetTableRecordsAsync<PlayerTableRecord>(tableIdHex);

    foreach (var player in playerRecords)
    {
        Console.WriteLine($"Player ID: {player.Keys.PlayerId}");
        Console.WriteLine($"Name: {player.Values.Name}");
        Console.WriteLine($"Level: {player.Values.Level}");
    }
}
```

### Example 8: Query with TablePredicate

Use predicates for complex queries:

```csharp
using Nethereum.Mud.TableRepository;

using (var context = new MyMudDbContext(options))
{
    var repository = new MyMudTableRepository(context);

    // Build a predicate
    var predicate = new TablePredicate
    {
        Conditions = new List<TableCondition>
        {
            new TableCondition
            {
                TableId = "0x..." + playerResource.ResourceIdEncoded.ToHex(),
                Address = worldAddress.ToLowerInvariant(),
                Key = "key0",
                ComparisonOperator = "=",
                HexValue = "0x000000000000000000000000000000000000000000000000000000000000002a",
                UnionOperator = "AND"
            }
        }
    };

    var records = await repository.GetRecordsAsync(predicate);
    Console.WriteLine($"Found {records.Count} records matching predicate");
}
```

### Example 9: Track Block Progress

Resume processing from last synced block:

```csharp
using Nethereum.Mud.Repositories.EntityFramework;

using (var context = new MyMudDbContext(options))
{
    var progressRepository = new BlockProgressRepository<MyMudDbContext>(context);

    // Check last processed block
    var lastBlock = await progressRepository.GetLastBlockNumberProcessedAsync();

    if (lastBlock.HasValue)
    {
        Console.WriteLine($"Last processed block: {lastBlock.Value}");
    }
    else
    {
        Console.WriteLine("No blocks processed yet");
    }

    // Update progress after processing
    BigInteger newBlock = lastBlock.GetValueOrDefault() + 1000;
    await progressRepository.UpsertProgressAsync(newBlock);

    Console.WriteLine($"Updated progress to block {newBlock}");
}
```

### Example 10: Production Sync Service

Complete background service for MUD synchronization:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.Web3;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Repositories.EntityFramework;
using System.Threading;
using System.Threading.Tasks;

public class MudSyncBackgroundService : BackgroundService
{
    private readonly ILogger<MudSyncBackgroundService> _logger;
    private readonly MyMudDbContext _context;
    private readonly string _rpcUrl;
    private readonly string _worldAddress;

    public MudSyncBackgroundService(
        ILogger<MudSyncBackgroundService> logger,
        MyMudDbContext context,
        string rpcUrl,
        string worldAddress)
    {
        _logger = logger;
        _context = context;
        _rpcUrl = rpcUrl;
        _worldAddress = worldAddress;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MUD Sync Service starting...");

        try
        {
            var web3 = new Web3(_rpcUrl);
            var repository = new MyMudTableRepository(_context);
            var progressRepository = new BlockProgressRepository<MyMudDbContext>(_context);

            var storeEventsService = new StoreEventsLogProcessingService(web3, _worldAddress);
            var processor = storeEventsService.CreateProcessor(
                repository,
                progressRepository,
                _logger,
                blocksPerRequest: 1000,
                retryWeight: 50,
                minimumBlockConfirmations: 12 // Wait for 12 confirmations
            );

            await processor.ExecuteAsync(
                startAtBlockNumberIfNotProcessed: 0,
                cancellationToken: stoppingToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MUD Sync Service failed");
            throw;
        }
    }
}

// Register in Startup.cs or Program.cs
services.AddDbContext<MyMudDbContext>(options =>
    options.UseSqlServer(connectionString));

services.AddHostedService<MudSyncBackgroundService>(provider =>
    new MudSyncBackgroundService(
        provider.GetRequiredService<ILogger<MudSyncBackgroundService>>(),
        provider.GetRequiredService<MyMudDbContext>(),
        rpcUrl: "https://rpc.mud.game",
        worldAddress: "0xWorldAddress"
    ));
```

## Core Classes

### MudEFTableRepository<TDbContext>

Base repository with optimized database operations:

```csharp
public abstract class MudEFTableRepository<TDbContext> : ITableRepository
    where TDbContext : DbContext, IMudStoreRecordsDbSets
{
    // Paging and batch operations
    Task<PagedResult<StoredRecord>> GetStoredRecordsAsync(int pageSize = 100, long? startingRowId = null);
    Task<PagedBlockNumberResult<StoredRecord>> GetStoredRecordsGreaterThanBlockNumberAsync(int pageSize = 100, BigInteger? startingBlockNumber = null, long? lastProcessedRowId = null);

    // CRUD operations (AsNoTracking optimized)
    Task<StoredRecord> GetRecordAsync(string tableIdHex, string keyHex);
    Task<IEnumerable<EncodedTableRecord>> GetRecordsAsync(string tableIdHex);
    Task SetRecordAsync(byte[] tableId, List<byte[]> key, EncodedValues encodedValues, string address = null, BigInteger? blockNumber = null, int? logIndex = null);
    Task DeleteRecordAsync(byte[] tableId, List<byte[]> key, string address = null, BigInteger? blockNumber = null, int? logIndex = null);

    // Splice operations for partial updates
    Task SetSpliceStaticDataAsync(byte[] tableId, List<byte[]> key, ulong start, byte[] newData, string address = null, BigInteger? blockNumber = null, int? logIndex = null);
    Task SetSpliceDynamicDataAsync(byte[] tableId, List<byte[]> key, ulong start, byte[] newData, ulong deleteCount, byte[] encodedLengths, string address = null, BigInteger? blockNumber = null, int? logIndex = null);

    // Strongly-typed table record operations
    Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>(string tableIdHex) where TTableRecord : ITableRecord, new();

    // Abstract methods for SQL predicate support
    abstract Task<List<StoredRecord>> GetRecordsAsync(TablePredicate predicate);
    abstract Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>(TablePredicate predicate) where TTableRecord : ITableRecord, new();
}
```

### BlockProgressRepository<TDbContext>

```csharp
public class BlockProgressRepository<TDbContext> : IBlockProgressRepository
    where TDbContext : DbContext, IMudStoreRecordsDbSets
{
    Task<BigInteger?> GetLastBlockNumberProcessedAsync();
    Task UpsertProgressAsync(BigInteger blockNumber);
}
```

### EFSqlHexPredicateBuilder

Converts `TablePredicate` to SQL queries:

```csharp
public class EFSqlHexPredicateBuilder : IEFSqlPredicateBuilder
{
    SqlPredicateResult BuildSql(TablePredicate predicate);
}
```

## Advanced Topics

### Performance Optimization

The repository uses several EF Core optimization techniques:

1. **AsNoTracking** - Disables change tracking for read-only queries
2. **Batch Processing** - Processes large datasets in chunks (default 1000 records)
3. **Manual Change Tracking** - Disables auto-detect changes during bulk updates
4. **Change Tracker Clearing** - Prevents memory bloat during long-running operations

```csharp
// Example from SetRecordAsync
Context.ChangeTracker.AutoDetectChangesEnabled = false;
// ... perform operations ...
await Context.SaveChangesAsync();
Context.ChangeTracker.Clear();  // Clear tracking to avoid memory bloat
Context.ChangeTracker.AutoDetectChangesEnabled = true;
```

### Handling Large Datasets

For production systems with millions of records:

```csharp
// Use paging to avoid loading entire table into memory
const int batchSize = 1000;
long? lastRowId = null;

while (true)
{
    var page = await repository.GetStoredRecordsAsync(batchSize, lastRowId);

    if (page.Records.Count == 0)
        break;

    // Process batch
    await ProcessBatchAsync(page.Records);

    lastRowId = page.LastRowId;
}
```

### Custom Database Providers

This package works with any EF Core provider (SQL Server, PostgreSQL, SQLite, etc.):

```csharp
// SQL Server
services.AddDbContext<MyMudDbContext>(options =>
    options.UseSqlServer(connectionString));

// SQLite
services.AddDbContext<MyMudDbContext>(options =>
    options.UseSqlite(connectionString));

// In-Memory (for testing)
services.AddDbContext<MyMudDbContext>(options =>
    options.UseInMemoryDatabase("MudTestDb"));
```

## Production Patterns

### 1. Continuous Sync with Retry Logic

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    try
    {
        await processor.ExecuteAsync(
            startAtBlockNumberIfNotProcessed: 0,
            cancellationToken: stoppingToken
        );
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Sync failed, retrying in 30 seconds...");
        await Task.Delay(30000, stoppingToken);
    }
}
```

### 2. Multiple World Synchronization

```csharp
var worlds = new[]
{
    ("0xWorld1", "Database1"),
    ("0xWorld2", "Database2")
};

var tasks = worlds.Select(async world =>
{
    var context = CreateDbContext(world.Item2);
    var repository = new MyMudTableRepository(context);
    var progressRepo = new BlockProgressRepository<MyMudDbContext>(context);
    var processor = CreateProcessor(world.Item1, repository, progressRepo);
    await processor.ExecuteAsync(0);
});

await Task.WhenAll(tasks);
```

### 3. Read-Heavy Workloads

```csharp
// Use read-only replicas for queries
services.AddDbContext<MyMudDbContext>(options =>
{
    options.UseSqlServer(readReplicaConnectionString);
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking); // Default no tracking
});
```

## Related Packages

### Dependencies
- **Nethereum.Mud** - Core MUD abstractions
- **Nethereum.Mud.Contracts** - Store contract event processing
- **Microsoft.EntityFrameworkCore** - EF Core framework

### Implementations
- **Nethereum.Mud.Repositories.Postgres** - PostgreSQL-specific implementation with bytea optimization and normalizer

### Related
- **Nethereum.BlockchainProcessing** - Block and log processing infrastructure

## Additional Resources

- [MUD Documentation](https://mud.dev/)
- [Entity Framework Core Docs](https://docs.microsoft.com/en-us/ef/core/)
- [Nethereum MUD Console Tests](https://github.com/Nethereum/Nethereum/tree/master/consoletests/NethereumMudLogProcessing)

## Support

- [Nethereum Discord](https://discord.gg/jQPrR58FxX)
- [GitHub Issues](https://github.com/Nethereum/Nethereum/issues)
