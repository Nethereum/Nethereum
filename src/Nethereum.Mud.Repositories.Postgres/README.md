# Nethereum.Mud.Repositories.Postgres

Nethereum.Mud.Repositories.Postgres provides a production-ready PostgreSQL implementation for persisting and querying [MUD (Onchain Engine)](https://mud.dev/) table data. It includes database schema normalization, allowing you to automatically create typed database tables from MUD schemas.

## Features

- **PostgreSQL Optimized DbContext** - Uses `bytea` for binary data instead of hex strings
- **MUD Schema Normalizer** - Automatically creates PostgreSQL tables from MUD schemas
- **Binary Key Storage** - Stores keys as `bytea` for efficient indexing and queries
- **Background Sync Service** - Process Store events and sync to PostgreSQL
- **Composite Indexes** - Optimized indexes for Address + TableId + Key queries
- **Typed Table Creation** - Generate normalized tables with proper column types
- **Snake Case Naming** - Lowercase naming convention for PostgreSQL best practices
- **Singleton Table Support** - Handles tables without keys (configuration singletons)

## Installation

```bash
dotnet add package Nethereum.Mud.Repositories.Postgres
```

### Dependencies

- **Npgsql.EntityFrameworkCore.PostgreSQL** 8.0+
- **EFCore.NamingConventions** 8.0+
- Nethereum.Mud.Repositories.EntityFramework
- Nethereum.Mud.Contracts
- Nethereum.Mud

## Key Concepts

### MudPostgresStoreRecordsDbContext

PostgreSQL-optimized DbContext with:
- `bytea` columns for binary data (TableId, Address, Key, StaticData, DynamicData, EncodedLengths)
- Composite primary key: `(AddressBytes, TableIdBytes, KeyBytes)`
- Indexes on individual keys (Key0Bytes, Key1Bytes, Key2Bytes, Key3Bytes)
- Lower case naming convention via `UseLowerCaseNamingConvention()`

### MudPostgresStoreRecordsTableRepository

Extends `MudEFTableRepository` with PostgreSQL-specific binary operations:
- Queries using `bytea` comparisons (faster than hex string comparisons)
- Binary predicate builder for complex queries
- Batch processing with `AsNoTracking` optimization

### MudPostgresStoreRecordsProcessingService

Background service for syncing MUD Store events to PostgreSQL:
- Processes `Store_SetRecord`, `Store_DeleteRecord`, `Store_SpliceStaticData`, `Store_SpliceDynamicData` events
- Tracks block progress to resume from last synced block
- Configurable batch size and retry logic
- Minimum block confirmations support

### MudPostgresStoreRecordsNormaliser

Converts raw `StoredRecord` entries into typed PostgreSQL tables:
- Reads MUD schema from on-chain `Tables` table
- Creates PostgreSQL tables dynamically with proper types
- Handles singleton tables (no keys) with auto-incrementing `id`
- Converts ABI types to PostgreSQL types (uint256 → NUMERIC, address → TEXT, etc.)
- Upserts records with conflict resolution

## Usage Examples

### Example 1: Setup PostgreSQL DbContext

Configure connection string and create DbContext:

```csharp
using Microsoft.EntityFrameworkCore;
using Nethereum.Mud.Repositories.Postgres;

var connectionString = "Host=localhost;Database=muddb;Username=postgres;Password=password";

var optionsBuilder = new DbContextOptionsBuilder<MudPostgresStoreRecordsDbContext>();
optionsBuilder.UseNpgsql(connectionString);

using var context = new MudPostgresStoreRecordsDbContext(optionsBuilder.Options);

// Create database and tables
await context.Database.EnsureCreatedAsync();

Console.WriteLine("PostgreSQL database initialized");
```

### Example 2: Apply Migrations

Generate and apply Entity Framework migrations:

```bash
# Add initial migration
dotnet ef migrations add InitialMudPostgres --context MudPostgresStoreRecordsDbContext

# Update database
dotnet ef database update --context MudPostgresStoreRecordsDbContext
```

Or run migrations in code:

```csharp
using (var context = new MudPostgresStoreRecordsDbContext(optionsBuilder.Options))
{
    await context.Database.MigrateAsync();
    Console.WriteLine("Migrations applied successfully");
}
```

### Example 3: Background Sync Service

Use the processing service to sync MUD events:

```csharp
using Microsoft.Extensions.Logging;
using Nethereum.Mud.Repositories.Postgres;

var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("MudSync");

using var context = new MudPostgresStoreRecordsDbContext(optionsBuilder.Options);

var processingService = new MudPostgresStoreRecordsProcessingService(context, logger)
{
    Address = "0xWorldContractAddress",
    RpcUrl = "https://rpc.mud.game",
    StartAtBlockNumberIfNotProcessed = 0,
    NumberOfBlocksToProcessPerRequest = 1000,
    RetryWeight = 50,
    MinimumNumberOfConfirmations = 12
};

// Start syncing (blocks until cancelled)
await processingService.ExecuteAsync(CancellationToken.None);
```

### Example 4: Query StoredRecords by Binary Keys

Query using bytea for performance:

```csharp
using Nethereum.Hex.HexConvertors.Extensions;
using Microsoft.EntityFrameworkCore;

var tableIdBytes = "0x7462000000000000000000000000000000000000000000000000000000000000".HexToByteArray();
var keyBytes = "0x000000000000000000000000000000000000000000000000000000000000002a".HexToByteArray();

using var context = new MudPostgresStoreRecordsDbContext(optionsBuilder.Options);

var record = await context.StoredRecords
    .AsNoTracking()
    .FirstOrDefaultAsync(r =>
        r.TableIdBytes == tableIdBytes &&
        r.KeyBytes == keyBytes &&
        !r.IsDeleted
    );

if (record != null)
{
    Console.WriteLine($"Found record at block {record.BlockNumber}");
    Console.WriteLine($"Static data length: {record.StaticData?.Length ?? 0}");
    Console.WriteLine($"Dynamic data length: {record.DynamicData?.Length ?? 0}");
}
```

### Example 5: Query with TablePredicate

Use predicates for complex queries:

```csharp
using Nethereum.Mud.TableRepository;
using Nethereum.Mud.Repositories.Postgres;

var repository = new MudPostgresStoreRecordsTableRepository(context);

// Build predicate
var predicate = new TablePredicate
{
    Conditions = new List<TableCondition>
    {
        new TableCondition
        {
            TableId = "0x7462...",
            Address = "0xWorldAddress",
            Key = "key0",
            ComparisonOperator = ">",
            HexValue = "0x0000000000000000000000000000000000000000000000000000000000000000",
            UnionOperator = "AND"
        }
    }
};

var records = await repository.GetRecordsAsync(predicate);
Console.WriteLine($"Found {records.Count} records matching predicate");
```

### Example 6: Convert to Typed TableRecords

Retrieve strongly-typed MUD table records:

```csharp
using Nethereum.Mud;
using Nethereum.Mud.TableRepository;

// Assume PlayerTableRecord is a generated MUD table record
var playerTableResource = new Resource("Game", "Player");
var tableIdHex = playerTableResource.ResourceIdEncoded.ToHex(true);

var repository = new MudPostgresStoreRecordsTableRepository(context);
var playerRecords = await repository.GetTableRecordsAsync<PlayerTableRecord>(tableIdHex);

foreach (var player in playerRecords)
{
    Console.WriteLine($"Player ID: {player.Keys.PlayerId}");
    Console.WriteLine($"Name: {player.Values.Name}");
    Console.WriteLine($"Level: {player.Values.Level}");
}
```

### Example 7: Schema Normalization - Create Typed Tables

Use the normalizer to create typed PostgreSQL tables:

```csharp
using Npgsql;
using Nethereum.Mud.Repositories.Postgres.StoreRecordsNormaliser;
using Nethereum.Mud.Contracts.Store;
using Nethereum.Web3;
using Microsoft.Extensions.Logging;

var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Normalizer");

// Connect to PostgreSQL
var connectionString = "Host=localhost;Database=muddb;Username=postgres;Password=password";
using var connection = new NpgsqlConnection(connectionString);

// Connect to MUD World for schema retrieval
var web3 = new Web3("https://rpc.mud.game");
var worldAddress = "0xWorldAddress";
var storeNamespace = new StoreNamespace(web3, worldAddress);

// Create normalizer
var normalizer = new MudPostgresStoreRecordsNormaliser(connection, storeNamespace, logger);

// Get table schema from on-chain
var playerTableId = new Resource("Game", "Player").ResourceIdEncoded;
var schema = await normalizer.GetTableSchemaAsync(playerTableId);

Console.WriteLine($"Table: {schema.Namespace}:{schema.Name}");
Console.WriteLine($"Keys: {string.Join(", ", schema.SchemaKeys.Select(k => k.Name))}");
Console.WriteLine($"Values: {string.Join(", ", schema.SchemaValues.Select(v => v.Name))}");

// Table is automatically created in PostgreSQL
// Example: "game_player" table with columns (playerid NUMERIC, name TEXT, level SMALLINT, health INTEGER, ...)
```

### Example 8: Normalize Records to Typed Tables

Convert StoredRecords to typed table rows:

```csharp
using Nethereum.Mud.EncodingDecoding;

// Get StoredRecord from database
var storedRecord = await context.StoredRecords
    .FirstOrDefaultAsync(r => r.TableIdBytes == playerTableId && !r.IsDeleted);

if (storedRecord != null)
{
    // Convert to EncodedTableRecord
    var encodedTableRecord = new EncodedTableRecord
    {
        TableId = storedRecord.TableIdBytes,
        Key = storedRecord.KeyBytes.SplitBytes(),
        EncodedValues = storedRecord
    };

    // Upsert to normalized table (e.g., "game_player")
    await normalizer.UpsertRecordAsync(encodedTableRecord);

    Console.WriteLine("Record normalized to typed table");
}
```

### Example 9: Continuous Normalization Service

Background service to continuously normalize records:

```csharp
using Nethereum.Mud.Repositories.Postgres.StoreRecordsNormaliser;

var progressService = new MudPostgresNormaliserProgressService(connection, logger);

var normaliserProcessingService = new MudPostgresNormaliserProcessingService(
    connection,
    context,
    storeNamespace,
    progressService,
    logger
)
{
    PageSize = 100
};

// Process all stored records in batches
await normaliserProcessingService.ExecuteAsync(CancellationToken.None);

Console.WriteLine("All records normalized to typed tables");
```

### Example 10: Production Sync + Normalize Pipeline

Complete pipeline: sync events → store raw records → normalize to typed tables:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public class MudPipelineBackgroundService : BackgroundService
{
    private readonly ILogger<MudPipelineBackgroundService> _logger;
    private readonly MudPostgresStoreRecordsDbContext _context;
    private readonly string _rpcUrl;
    private readonly string _worldAddress;
    private readonly string _postgresConnectionString;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting MUD Pipeline: Sync + Normalize");

        // Task 1: Sync Store events to StoredRecords table
        var syncTask = Task.Run(async () =>
        {
            var syncService = new MudPostgresStoreRecordsProcessingService(_context, _logger)
            {
                Address = _worldAddress,
                RpcUrl = _rpcUrl,
                StartAtBlockNumberIfNotProcessed = 0,
                NumberOfBlocksToProcessPerRequest = 1000
            };

            await syncService.ExecuteAsync(stoppingToken);
        }, stoppingToken);

        // Task 2: Normalize StoredRecords to typed tables
        var normalizeTask = Task.Run(async () =>
        {
            await Task.Delay(10000, stoppingToken); // Wait for initial sync

            var web3 = new Web3(_rpcUrl);
            var storeNamespace = new StoreNamespace(web3, _worldAddress);
            using var connection = new NpgsqlConnection(_postgresConnectionString);
            var progressService = new MudPostgresNormaliserProgressService(connection, _logger);

            var normaliserService = new MudPostgresNormaliserProcessingService(
                connection,
                _context,
                storeNamespace,
                progressService,
                _logger
            );

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await normaliserService.ExecuteAsync(stoppingToken);
                    await Task.Delay(5000, stoppingToken); // Check for new records every 5s
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Normalization error");
                    await Task.Delay(30000, stoppingToken);
                }
            }
        }, stoppingToken);

        await Task.WhenAll(syncTask, normalizeTask);
    }
}

// Register in Program.cs
services.AddDbContext<MudPostgresStoreRecordsDbContext>(options =>
    options.UseNpgsql(postgresConnectionString));

services.AddHostedService<MudPipelineBackgroundService>();
```

## Core Classes

### MudPostgresStoreRecordsDbContext

```csharp
public class MudPostgresStoreRecordsDbContext : DbContext, IMudStoreRecordsDbSets
{
    public DbSet<StoredRecord> StoredRecords { get; set; }
    public DbSet<BlockProgress> BlockProgress { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Composite primary key using bytea
        modelBuilder.Entity<StoredRecord>()
            .HasKey(r => new { r.AddressBytes, r.TableIdBytes, r.KeyBytes });

        // Indexes for queries
        modelBuilder.Entity<StoredRecord>()
            .HasIndex(r => new { r.AddressBytes, r.TableIdBytes, r.Key0Bytes });

        // Map to bytea
        modelBuilder.Entity<StoredRecord>()
            .Property(e => e.TableIdBytes)
            .HasColumnName("tableid")
            .HasColumnType("bytea");

        // BigInteger to numeric conversion
        modelBuilder.Entity<StoredRecord>()
            .Property(r => r.BlockNumber)
            .HasConversion(
                v => (decimal)v,
                v => (BigInteger)v
            )
            .HasColumnType("numeric(1000, 0)");
    }
}
```

### MudPostgresStoreRecordsTableRepository

```csharp
public class MudPostgresStoreRecordsTableRepository : MudEFTableRepository<MudPostgresStoreRecordsDbContext>
{
    // Query using bytea
    public override async Task<StoredRecord> GetRecordAsync(string tableIdHex, string keyHex)
    {
        var tableIdBytes = tableIdHex.HexToByteArray();
        var keyBytes = keyHex.HexToByteArray();

        return await Context.StoredRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TableIdBytes == tableIdBytes && r.KeyBytes == keyBytes);
    }

    // SQL predicate using bytea
    public override Task<List<StoredRecord>> GetRecordsAsync(TablePredicate predicate)
    {
        var builder = new MudPostgresStoreRecordsSqlByteaPredicateBuilder();
        var sqlPredicate = builder.BuildSql(predicate);
        string sqlQuery = $"SELECT * FROM storedrecords WHERE {sqlPredicate.Sql}";
        return Context.StoredRecords.FromSqlRaw(sqlQuery, sqlPredicate.GetParameterValues()).ToListAsync();
    }
}
```

### MudPostgresStoreRecordsProcessingService

```csharp
public class MudPostgresStoreRecordsProcessingService
{
    public string Address { get; set; }                     // World contract address
    public string RpcUrl { get; set; }                      // Ethereum RPC URL
    public BigInteger StartAtBlockNumberIfNotProcessed { get; set; } = 0;
    public int NumberOfBlocksToProcessPerRequest { get; set; } = 1000;
    public int RetryWeight { get; set; } = 50;
    public uint MinimumNumberOfConfirmations { get; set; } = 0;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default);
}
```

### MudPostgresStoreRecordsNormaliser

```csharp
public class MudPostgresStoreRecordsNormaliser
{
    // Retrieve and cache MUD schema
    public async Task<TableSchema> GetTableSchemaAsync(byte[] tableId);

    // Create PostgreSQL table from MUD schema
    public async Task CreateTableIfNotExistsAsync(TableSchema schema);

    // Upsert record to normalized table
    public async Task UpsertRecordAsync(EncodedTableRecord encodedTableRecord);
    public async Task UpsertRecordAsync(TableSchema schema, List<FieldValue> fieldValues);

    // Delete record from normalized table
    public async Task DeleteRecordAsync(EncodedTableRecord encodedTableRecord);
    public async Task DeleteRecordAsync(TableSchema schema, List<FieldValue> fieldValues);
}
```

### Type Conversion Mapping

MUD ABI types are mapped to PostgreSQL types:

| ABI Type   | PostgreSQL Type |
|------------|-----------------|
| `address`  | `TEXT`          |
| `bool`     | `BOOLEAN`       |
| `string`   | `TEXT`          |
| `uint8`    | `SMALLINT`      |
| `uint16`   | `INTEGER`       |
| `uint32`   | `BIGINT`        |
| `uint64`   | `NUMERIC(20,0)` |
| `uint128`  | `NUMERIC(38,0)` |
| `uint256`  | `NUMERIC(78,0)` |
| `int8`     | `SMALLINT`      |
| `int16`    | `SMALLINT`      |
| `int32`    | `INTEGER`       |
| `int64`    | `BIGINT`        |
| `int128`   | `NUMERIC(38,0)` |
| `int256`   | `NUMERIC(78,0)` |
| `bytes`    | `BYTEA`         |
| `bytes32`  | `BYTEA`         |
| Arrays     | `BYTEA` (encoded) |

## Advanced Topics

### PostgreSQL Performance Optimization

**1. Use bytea for keys and binary data:**
```csharp
// ✅ Fast: Binary comparison
r.TableIdBytes == tableIdBytes

// ❌ Slow: String comparison
r.TableId == tableIdHex
```

**2. Leverage composite indexes:**
```sql
CREATE INDEX ix_address_tableid_key0 ON storedrecords (addressbytes, tableidbytes, key0bytes);
```

**3. Use NUMERIC for BigInteger:**
```csharp
// Handles uint256 values without overflow
modelBuilder.Entity<StoredRecord>()
    .Property(r => r.BlockNumber)
    .HasColumnType("numeric(1000, 0)");
```

### Handling Singleton Tables

Singleton tables (no keys) are handled with auto-incrementing `id`:

```sql
-- Generated for singleton table
CREATE TABLE config (
    id SERIAL PRIMARY KEY,
    maxplayers NUMERIC(78, 0),
    ispaused BOOLEAN
);

-- Upsert always updates id=1
INSERT INTO config (id, maxplayers, ispaused)
VALUES (1, 100, false)
ON CONFLICT (id)
DO UPDATE SET maxplayers = 100, ispaused = false;
```

### Custom Table Schemas

Override `GetTableName` to customize table naming:

```csharp
public class CustomNormaliser : MudPostgresStoreRecordsNormaliser
{
    protected override string GetTableName(TableSchema schema)
    {
        // Custom naming: prefix all tables with "mud_"
        return $"mud_{base.GetTableName(schema)}";
    }
}
```

### Query Normalized Tables Directly

Once normalized, query typed tables directly with SQL:

```sql
-- Query game_player table
SELECT playerid, name, level, health, experience
FROM game_player
WHERE level > 10
ORDER BY experience DESC;

-- Join multiple MUD tables
SELECT p.playerid, p.name, i.itemid, i.quantity
FROM game_player p
JOIN game_inventory i ON p.playerid = i.playerid
WHERE p.level > 5;
```

## Production Patterns

### 1. Two-Phase Sync: Raw + Normalized

Store raw records first, then normalize asynchronously:

```csharp
// Phase 1: Fast sync to StoredRecords (critical path)
var syncService = new MudPostgresStoreRecordsProcessingService(context, logger);
await syncService.ExecuteAsync(cancellationToken);

// Phase 2: Normalize to typed tables (background)
var normalizeService = new MudPostgresNormaliserProcessingService(...);
await normalizeService.ExecuteAsync(cancellationToken);
```

### 2. Read from Typed Tables, Write to StoredRecords

- **Writes:** Always go through MUD contracts → Store events → StoredRecords
- **Reads:** Query normalized typed tables for fast SQL queries

```csharp
// Write: Via MUD contracts
await worldService.CallFromRequestAndWaitForReceiptAsync(systemId, callData);

// Read: From normalized table
var players = await connection.QueryAsync<PlayerInfo>(
    "SELECT * FROM game_player WHERE level > @minLevel",
    new { minLevel = 10 }
);
```

### 3. Horizontal Scaling with Read Replicas

```csharp
// Primary: Writes to StoredRecords
var primaryConnectionString = "Host=primary.db;Database=mud;...";

// Replica: Read-only queries on typed tables
var replicaConnectionString = "Host=replica.db;Database=mud;...";

// Use replica for queries
using var replicaConnection = new NpgsqlConnection(replicaConnectionString);
var players = await replicaConnection.QueryAsync<PlayerInfo>(...);
```

### 4. Connection Pooling Configuration

```csharp
var connectionString = "Host=localhost;Database=muddb;Username=postgres;Password=password;Pooling=true;MinPoolSize=5;MaxPoolSize=100;ConnectionLifetime=300";

services.AddDbContext<MudPostgresStoreRecordsDbContext>(options =>
    options.UseNpgsql(connectionString));
```

## Database Schema

### StoredRecords Table (Raw Storage)

```sql
CREATE TABLE storedrecords (
    rowid BIGSERIAL,
    addressbytes BYTEA NOT NULL,
    tableidbytes BYTEA NOT NULL,
    keybytes BYTEA NOT NULL,
    key0bytes BYTEA,
    key1bytes BYTEA,
    key2bytes BYTEA,
    key3bytes BYTEA,
    static_data BYTEA,
    encoded_lengths BYTEA,
    dynamic_data BYTEA,
    isdeleted BOOLEAN NOT NULL DEFAULT false,
    blocknumber NUMERIC(1000, 0),
    logindex INTEGER,
    PRIMARY KEY (addressbytes, tableidbytes, keybytes)
);

CREATE INDEX ix_rowid ON storedrecords (rowid);
CREATE INDEX ix_address_tableid_key0 ON storedrecords (addressbytes, tableidbytes, key0bytes);
```

### Example Normalized Table

```sql
-- Generated from MUD Player table schema
CREATE TABLE game_player (
    playerid NUMERIC(78, 0),         -- uint256
    name TEXT,                       -- string
    level SMALLINT,                  -- uint8
    health INTEGER,                  -- uint16
    experience NUMERIC(78, 0),       -- uint256
    isactive BOOLEAN,                -- bool
    PRIMARY KEY (playerid)
);
```

## Related Packages

### Dependencies
- **Nethereum.Mud.Repositories.EntityFramework** - Base EF Core abstractions
- **Nethereum.Mud.Contracts** - Store contract interactions
- **Nethereum.Mud** - Core MUD types
- **Npgsql.EntityFrameworkCore.PostgreSQL** - PostgreSQL EF provider

### Complementary
- **Dapper** - Can be used to query normalized tables directly
- **Npgsql** - Direct PostgreSQL access for advanced queries

## Additional Resources

- [MUD Documentation](https://mud.dev/)
- [Npgsql Documentation](https://www.npgsql.org/efcore/)
- [PostgreSQL NUMERIC Type](https://www.postgresql.org/docs/current/datatype-numeric.html)
- [Nethereum MUD Console Tests](https://github.com/Nethereum/Nethereum/tree/master/consoletests/NethereumMudLogProcessing)

## Support

- [Nethereum Discord](https://discord.gg/jQPrR58FxX)
- [GitHub Issues](https://github.com/Nethereum/Nethereum/issues)
