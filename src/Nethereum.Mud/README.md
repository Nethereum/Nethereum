# Nethereum.Mud

Nethereum.Mud provides core infrastructure for interacting with [MUD (Onchain Engine)](https://mud.dev/) applications. MUD is a framework for building ambitious Ethereum applications with on-chain state management using an Entity-Component-System (ECS) architecture.

## What is MUD?

MUD (Onchain Engine) is a framework for building complex, composable applications on Ethereum. It provides:

- **Standardized On-Chain Data Storage** - All application state lives on-chain in structured tables
- **Entity-Component-System Architecture** - Organize game/app logic using ECS patterns
- **Composability** - Applications can read and extend each other's data permissionlessly
- **Automatic Synchronization** - Client-side state stays in sync with blockchain state
- **Developer Experience** - TypeScript + Solidity tooling for rapid development

Nethereum.Mud brings this power to .NET, enabling you to build MUD clients, indexers, and tools in C#.

## Features

- **Table Records** - Strongly-typed representations of MUD tables (keys + values)
- **Schema Encoding/Decoding** - Automatic encoding/decoding of MUD schemas
- **Resource Management** - Resource identifiers for namespaces, tables, and systems
- **TableRepository** - Query interface with LINQ-like predicates
- **In-Memory Storage** - Local table record caching and change tracking
- **REST API Client** - Query remote table repositories via HTTP

## Installation

```bash
dotnet add package Nethereum.Mud
```

### Dependencies

- Nethereum.Web3
- Nethereum.Util.Rest

## MUD Architecture

MUD organizes on-chain applications using several core concepts:

### World Contract

The **World** is the central registry contract that contains:
- All namespaces, tables, and systems
- Access control and permissions
- Store logic for reading/writing data

Every MUD application has one World contract address.

### Namespaces

**Namespaces** organize related tables and systems:
- Provide access control boundaries
- Group related functionality
- Example: `"Land"`, `"Inventory"`, `"Combat"`

### Tables

**Tables** store on-chain data in structured key-value format:
- **Schema** defines field types (keys + values)
- **Keys** identify records (e.g., `playerId`, `itemId`)
- **Values** contain the actual data
- Stored in the World's Store contract

Example table structure:
```
Table: "Player"
Keys: [playerId: uint256]
Values: [name: string, level: uint8, health: uint16]
```

### Systems

**Systems** are smart contracts containing application logic:
- Functions that read/write table data
- Registered in the World
- Can be called via World contract's delegation
- Example: `"MoveSystem"`, `"CraftingSystem"`, `"TradeSystem"`

### Resources

**Resources** are identified by `namespace:name`:
- Tables: `tb` type (e.g., `"Game:Player"` → `0x7462...`)
- Systems: `sy` type (e.g., `"Game:MoveSystem"` → `0x7379...`)
- Encoded as `bytes32` resource IDs

### Composability

MUD's composability model allows:
1. **Reading other apps' data** - Any application can query any MUD World's tables
2. **Extending applications** - Deploy new systems that interact with existing tables
3. **Building on top** - Create meta-applications using multiple Worlds as data sources

## Code Generation

MUD tables are typically code-generated from `mud.config.ts`. Use `.nethereum-gen.multisettings` for C# generation:

### Configuration File

Create `.nethereum-gen.multisettings` in your contracts directory:

```json
{
  "paths": ["mud.config.ts"],
  "generatorConfigs": [
    {
      "baseNamespace": "MyGame.Tables",
      "basePath": "../MyGame/Tables",
      "generatorType": "MudTables"
    },
    {
      "baseNamespace": "MyGame.Systems",
      "basePath": "../MyGame/Systems",
      "generatorType": "MudExtendedService"
    }
  ]
}
```

### Generator Types

**1. MudTables** - Generates table record classes:
- TableRecord classes with typed Keys and Values
- Automatic encoding/decoding methods
- Schema information

**2. MudExtendedService** - Generates system service classes:
- Service classes for calling system functions
- Typed parameters and return values
- Integration with World contract

### Generated Table Record Example

From a MUD config defining a Player table:

```csharp
// Auto-generated from mud.config.ts
public partial class PlayerTableRecord : TableRecord<PlayerKey, PlayerValue>
{
    public PlayerTableRecord() : base("MyWorld", "Player") { }

    public class PlayerKey
    {
        [Parameter("uint256", "playerId", 1)]
        public BigInteger PlayerId { get; set; }
    }

    public class PlayerValue
    {
        [Parameter("string", "name", 1)]
        public string Name { get; set; }

        [Parameter("uint8", "level", 2)]
        public byte Level { get; set; }

        [Parameter("uint16", "health", 3)]
        public ushort Health { get; set; }
    }
}
```

### Running Code Generation

```bash
# Install the Nethereum code generator
dotnet tool install -g Nethereum.Generator.Console

# Generate from the multisettings file
Nethereum.Generator.Console generate from-config -cfg .nethereum-gen.multisettings
```

Or use the VS Code Solidity extension with multisettings support: https://github.com/juanfranblanco/vscode-solidity

## Encoding & Decoding

MUD uses custom encoding for efficient on-chain storage:

### Schema Encoding

Table schemas are encoded into `bytes32`:
- First 2 bytes: Static field types
- Next 2 bytes: Dynamic field types
- Remaining bytes: Field count metadata

```csharp
using Nethereum.Mud.EncodingDecoding;

var resourceId = ResourceEncoder.EncodeTable("Game", "Player");
var schema = SchemaEncoder.GetSchemaEncoded<PlayerTableRecord.PlayerKey, PlayerTableRecord.PlayerValue>(resourceId);

Console.WriteLine($"Key schema: {schema.KeySchema.ToHex()}");
Console.WriteLine($"Value schema: {schema.ValueSchema.ToHex()}");
Console.WriteLine($"Field layout: {schema.FieldLayout.ToHex()}");
Console.WriteLine($"Key names: {string.Join(", ", schema.KeyNames)}");
Console.WriteLine($"Field names: {string.Join(", ", schema.FieldNames)}");
```

### Key Encoding

Keys are encoded as fixed 32-byte chunks:
- Each key component padded to 32 bytes
- Concatenated together
- Used as the record identifier

```csharp
var playerRecord = new PlayerTableRecord
{
    Keys = new PlayerTableRecord.PlayerKey { PlayerId = 42 }
};

// Encoded as: 0x000000000000000000000000000000000000000000000000000000000000002a
var encodedKey = playerRecord.GetEncodedKey();
```

### Value Encoding

Values are encoded in two parts:

**1. Static Data** - Fixed-size fields (uint256, address, bool, etc.):
```csharp
// Static fields packed tightly
// e.g., uint8 + uint16 = 3 bytes total
```

**2. Dynamic Data** - Variable-size fields (string, bytes, arrays):
```csharp
// Prefixed with EncodedLengths (packed field lengths)
// Then concatenated dynamic data
```

Example:
```csharp
var playerRecord = new PlayerTableRecord
{
    Keys = new PlayerTableRecord.PlayerKey { PlayerId = 1 },
    Values = new PlayerTableRecord.PlayerValue
    {
        Name = "Alice",
        Level = 5,
        Health = 100
    }
};

var encodedValues = playerRecord.GetEncodeValues();
Console.WriteLine($"Static data: {encodedValues.StaticData.ToHex()}");
Console.WriteLine($"Dynamic data: {encodedValues.DynamicData.ToHex()}");
Console.WriteLine($"Encoded lengths: {encodedValues.EncodedLengths.ToHex()}");
```

### Decoding

Decode on-chain data back to typed records:

```csharp
// Decode from on-chain bytes
playerRecord.DecodeKey(encodedKeyBytes);
playerRecord.DecodeValues(encodedValueBytes);

Console.WriteLine($"Player {playerRecord.Keys.PlayerId}: {playerRecord.Values.Name}");
Console.WriteLine($"Level {playerRecord.Values.Level}, Health {playerRecord.Values.Health}");
```

## Usage Examples

### Example 1: Working with Table Records

```csharp
using Nethereum.Mud;
using Nethereum.Web3;

// Table record with key and values
var playerRecord = new PlayerTableRecord();

// Set key
playerRecord.Keys = new PlayerTableRecord.PlayerKey
{
    PlayerId = 42
};

// Set values
playerRecord.Values = new PlayerTableRecord.PlayerValue
{
    Name = "Alice",
    Level = 10,
    Health = 100
};

// Encode for on-chain storage
var encodedKey = playerRecord.GetEncodedKey();
var encodedValues = playerRecord.GetEncodeValues();

// Decode from on-chain data
playerRecord.DecodeKey(encodedKeyBytes);
playerRecord.DecodeValues(encodedValuesBytes);

Console.WriteLine($"Player {playerRecord.Keys.PlayerId}: {playerRecord.Values.Name}");
```

### Example 2: In-Memory Table Repository

```csharp
using Nethereum.Mud.TableRepository;

// The repository is not generic; a single instance stores records for any table
var repository = new InMemoryTableRepository();

// Add records
var player1 = new PlayerTableRecord
{
    Keys = new() { PlayerId = 1 },
    Values = new() { Name = "Alice", Level = 10, Health = 100 }
};

var player2 = new PlayerTableRecord
{
    Keys = new() { PlayerId = 2 },
    Values = new() { Name = "Bob", Level = 5, Health = 50 }
};

// SetRecordAsync<T> resolves the table id and key from the record itself
await repository.SetRecordAsync(player1);
await repository.SetRecordAsync(player2);

// Read all records for a table, decoded to the strongly-typed record
var allPlayers = (await repository.GetTableRecordsAsync<PlayerTableRecord>()).ToList();
Console.WriteLine($"Total players: {allPlayers.Count}");

// Read a single raw stored record by encoded table id + combined key bytes
var keyBytes = TableRepositoryBase.ConvertKeyToCombinedHex(player1.GetEncodedKey()).HexToByteArray();
var stored = await repository.GetRecordAsync(player1.ResourceIdEncoded, keyBytes);
```

### Example 3: Query with Predicates

```csharp
using Nethereum.Mud.TableRepository;

// Predicates filter on KEY fields. The builder takes three type parameters
// (record, key, value) and the World address, and is finalized with Expand().
var predicate = new TablePredicateBuilder<PlayerTableRecord, PlayerTableRecord.PlayerKey, PlayerTableRecord.PlayerValue>("0xWorldAddress")
    .AndEqual(key => key.PlayerId, 1)     // AND key0 = 0x01
    .OrEqual(key => key.PlayerId, 2)      // OR  key0 = 0x02
    .AndNotEqual(key => key.PlayerId, 3)  // AND key0 != 0x03
    .Expand();                            // Finalize the predicate

// Run the predicate against a repository that supports predicate queries.
// InMemoryTableRepository does not (query AllRecords with LINQ instead);
// the REST API client and the EF/Postgres repositories do.
var results = await apiClient.GetTableRecordsAsync<PlayerTableRecord>(predicate);

foreach (var player in results)
{
    Console.WriteLine($"Player {player.Keys.PlayerId}: Level {player.Values.Level}, Health {player.Values.Health}");
}
```

### Example 4: Change Tracking Repository

```csharp
using Nethereum.Mud.TableRepository;

// Repository with change tracking (non-generic)
var repository = new InMemoryChangeTrackerTableRepository();
repository.StartTracking();

// Modify and upsert a record (tracked once tracking is enabled)
player.Values.Level += 1;
player.Values.Health -= 10;
await repository.SetRecordAsync(player);

// The accumulated changes: Upserted and Deleted are keyed by table id then key
var changeSet = repository.ChangeSet;
Console.WriteLine($"Tables with upserts: {changeSet.Upserted.Count}");
Console.WriteLine($"Tables with deletes: {changeSet.Deleted.Count}");

// Project the changes for a specific table to strongly-typed records
var playerChanges = changeSet.GetTableRecordChanges<PlayerTableRecord>();
Console.WriteLine($"Upserted: {playerChanges.Upserted.Count}, Deleted: {playerChanges.Deleted.Count}");

// Take a snapshot and clear the tracker in one step (or ClearChangeSet() to just clear)
var snapshot = repository.GetAndClearChangeSet();
```

### Example 5: Resource Identifiers

```csharp
using Nethereum.Mud.EncodingDecoding;

// Create resource ID for a table (returns bytes32)
var playerTableResourceId = ResourceEncoder.EncodeTable("Game", "Player");
Console.WriteLine($"Table Resource ID: {playerTableResourceId.ToHex()}");

// Create resource ID for a system
var moveSystemResourceId = ResourceEncoder.EncodeSystem("Game", "MoveSystem");
Console.WriteLine($"System Resource ID: {moveSystemResourceId.ToHex()}");

// Create resource ID for a namespace
var gameNamespaceId = ResourceEncoder.EncodeNamespace("Game");
Console.WriteLine($"Namespace Resource ID: {gameNamespaceId.ToHex()}");

// Decode a resource ID back to its components
var decoded = ResourceEncoder.Decode(playerTableResourceId);
Console.WriteLine($"Namespace: {decoded.Namespace}, Name: {decoded.Name}");
```

### Example 6: Schema Encoding

```csharp
using Nethereum.Mud.EncodingDecoding;

// Get schema for a table record type (pass the encoded table resource id)
var resourceId = ResourceEncoder.EncodeTable("Game", "Player");
var schema = SchemaEncoder.GetSchemaEncoded<PlayerTableRecord.PlayerKey, PlayerTableRecord.PlayerValue>(resourceId);

Console.WriteLine($"Key schema: {schema.KeySchema.ToHex()}");
Console.WriteLine($"Value schema: {schema.ValueSchema.ToHex()}");
Console.WriteLine($"Field layout: {schema.FieldLayout.ToHex()}");
Console.WriteLine($"Key names: {string.Join(", ", schema.KeyNames)}");
Console.WriteLine($"Field names: {string.Join(", ", schema.FieldNames)}");
```

### Example 7: REST API Client

```csharp
using Nethereum.Mud.TableRepository;

// Connect to a remote table repository API
// ctor: (IRestHttpHelper httpHelper, string baseUrl, string postPath = "storedrecords")
var httpHelper = new RestHttpHelper(new HttpClient());
var apiClient = new StoredRecordRestApiClient(httpHelper, "https://api.example.com/mud");

// Build a predicate (filters on key fields) and query typed records
var predicate = new TablePredicateBuilder<PlayerTableRecord, PlayerTableRecord.PlayerKey, PlayerTableRecord.PlayerValue>("0xWorldAddress")
    .AndEqual(key => key.PlayerId, 1)
    .Expand();

IEnumerable<PlayerTableRecord> records = await apiClient.GetTableRecordsAsync<PlayerTableRecord>(predicate);
foreach (var record in records)
{
    Console.WriteLine($"Player {record.Keys.PlayerId}: {record.Values.Name}");
}

// Or fetch the raw stored records matching the predicate
List<StoredRecord> stored = await apiClient.GetRecordsAsync(predicate);
```

### Example 8: Working with Stored Records

```csharp
using Nethereum.Mud;
using Nethereum.Mud.TableRepository;

// StoredRecord is the persisted form of a table record.
// It derives from EncodedValues, so StaticData/DynamicData/EncodedLengths are byte[];
// the *Hex string setters (and TableId/Key0/Address) accept hex strings.
var playerTableResourceId = ResourceEncoder.EncodeTable("Game", "Player");
var encodedKey = playerRecord.GetEncodedKey();
var encodedValues = playerRecord.GetEncodeValues();

var storedRecord = new StoredRecord
{
    Address = "0xWorldAddress",                              // the World address (property is Address)
    TableId = playerTableResourceId.ToHex(true),
    Key = TableRepositoryBase.ConvertKeyToCombinedHex(encodedKey), // combined key (hex string setter)
    Key0 = encodedKey[0].ToHex(true),                        // first key component (hex string setter)
    StaticDataHex = encodedValues.StaticData.ToHex(true),
    DynamicDataHex = encodedValues.DynamicData.ToHex(true),
    EncodedLengthsHex = encodedValues.EncodedLengths.ToHex(true),
    IsDeleted = false
};

// Map to/from the transport DTO with the static extension methods
StoredRecordDTO dto = storedRecord.MapToStoredRecordDTO();
StoredRecord roundTripped = dto.MapToStoredRecord();

// Decode a stored record into a strongly-typed table record
var tableRecord = new PlayerTableRecord();
tableRecord.DecodeValues(roundTripped);                      // StoredRecord : EncodedValues
tableRecord.DecodeKey(KeyUtils.ConvertKeyFromCombinedHex(roundTripped.Key));

Console.WriteLine($"Restored player {tableRecord.Keys.PlayerId}");
```

### Example 9: Singleton Tables (No Keys)

Some MUD tables have no keys (configuration singletons):

```csharp
using Nethereum.Mud;

// Singleton table record
public class ConfigTableRecord : TableRecordSingleton<ConfigValue>
{
    public ConfigTableRecord() : base("MyWorld", "Config") { }

    public class ConfigValue
    {
        [Parameter("uint256", "maxPlayers", 1)]
        public BigInteger MaxPlayers { get; set; }

        [Parameter("bool", "isPaused", 2)]
        public bool IsPaused { get; set; }
    }
}

// Usage
var config = new ConfigTableRecord();
config.Values = new ConfigTableRecord.ConfigValue
{
    MaxPlayers = 100,
    IsPaused = false
};

// Only has values, no keys
var encodedValues = config.GetEncodeValues();
```

### Example 10: Production MUD Application Pattern

```csharp
using Nethereum.Mud;
using Nethereum.Mud.TableRepository;
using Nethereum.Web3;

// Initialize repositories with change tracking (non-generic; one stores many tables)
var playerRepository = new InMemoryChangeTrackerTableRepository();
var inventoryRepository = new InMemoryChangeTrackerTableRepository();
playerRepository.StartTracking();
inventoryRepository.StartTracking();

// Load initial state from chain or database
// ...

// Application logic modifies records, then upserts them
player.Values.Level += 1;
player.Values.Health = 100;
await playerRepository.SetRecordAsync(player);

inventory.Values.Quantity -= 1;
await inventoryRepository.SetRecordAsync(inventory);

// Snapshot the changes to sync with chain (GetAndClearChangeSet also clears the tracker)
InMemoryChangeSet playerChanges = playerRepository.GetAndClearChangeSet();
InMemoryChangeSet inventoryChanges = inventoryRepository.GetAndClearChangeSet();

// Project to strongly-typed changes per table when needed
TableRecordChangeSet<PlayerTableRecord> typedPlayerChanges =
    playerChanges.GetTableRecordChanges<PlayerTableRecord>();

// Send to chain via MUD World contract
// (See Nethereum.Mud.Contracts for World interaction)
```

## Core Classes

### TableRecord<TKey, TValue>

Base class for MUD table records with keys.

```csharp
public abstract class TableRecord<TKey, TValue> : TableRecordSingleton<TValue>, ITableRecord
    where TKey : class, new()
    where TValue : class, new()
{
    public TKey Keys { get; set; }
    // Values is inherited from TableRecordSingleton<TValue>

    public virtual List<byte[]> GetEncodedKey();
    public virtual void DecodeKey(List<byte[]> encodedKey);
    public override SchemaEncoded GetSchemaEncoded();

    // Inherited from TableRecordSingleton<TValue>:
    // public virtual EncodedValues GetEncodeValues();
    // public void DecodeValues(EncodedValues encodedValues);
}
```

### TableRecordSingleton<TValue>

Base class for tables without keys (singletons).

```csharp
public abstract class TableRecordSingleton<TValue> : ITableRecordSingleton
    where TValue : class, new()
{
    public TValue Values { get; set; }

    public EncodedValues GetEncodeValues();
    public void DecodeValues(EncodedValues encodedValues);
}
```

### ITableRepository

Interface for table record storage and querying. It is **not** generic: a single
repository stores records for any table, and the record type is supplied per call.

```csharp
public interface ITableRepository : ITablePredicateQueryRepository
{
    Task SetRecordAsync<TTableRecord>(TTableRecord record, string address = null,
        BigInteger? blockNumber = null, int? logIndex = null) where TTableRecord : ITableRecord;
    Task SetRecordsAsync<TTableRecord>(IEnumerable<TTableRecord> records, string address = null,
        BigInteger? blockNumber = null, int? logIndex = null) where TTableRecord : ITableRecord;
    Task SetRecordAsync(byte[] tableId, List<byte[]> key, EncodedValues encodedValues,
        string address = null, BigInteger? blockNumber = null, int? logIndex = null);

    Task<StoredRecord> GetRecordAsync(byte[] tableId, byte[] key);
    Task<IEnumerable<EncodedTableRecord>> GetRecordsAsync(byte[] tableId);
    Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>()
        where TTableRecord : ITableRecordSingleton, new();

    Task DeleteRecordAsync(byte[] tableId, List<byte[]> key, string address = null,
        BigInteger? blockNumber = null, int? logIndex = null);

    Task SetSpliceStaticDataAsync(byte[] tableId, List<byte[]> key, ulong start, byte[] newData,
        string address = null, BigInteger? blockNumber = null, int? logIndex = null);
    Task SetSpliceDynamicDataAsync(byte[] tableId, List<byte[]> key, ulong start, byte[] newData,
        ulong deleteCount, byte[] encodedLengths, string address = null,
        BigInteger? blockNumber = null, int? logIndex = null);
}

// Predicate queries are declared on the base interface:
public interface ITablePredicateQueryRepository
{
    Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>(TablePredicate predicate)
        where TTableRecord : ITableRecord, new();
    Task<List<StoredRecord>> GetRecordsAsync(TablePredicate predicate);
}
```

### ResourceEncoder

Static utility for creating and decoding MUD resource identifiers (bytes32).

```csharp
public static class ResourceEncoder
{
    public static byte[] EncodeTable(string @namespace, string name);
    public static byte[] EncodeSystem(string @namespace, string name);
    public static byte[] EncodeNamespace(string @namespace);
    public static byte[] EncodeOffchainTable(string @namespace, string name);
    public static byte[] EncodeRootTable(string name);
    public static byte[] EncodeRootSystem(string name);
    public static Resource Decode(byte[] resourceBytes);
}
```

## Advanced Topics

### Custom Encoding

```csharp
using Nethereum.Mud.EncodingDecoding;

// Custom key encoding (EncodeKey<T> returns the key components as List<byte[]>)
var customKeys = KeyEncoderDecoder.EncodeKey(new MyKey
{
    PlayerId = 1,
    ItemId = 42
});

// Custom value encoding.
// EncodeValues takes a List<FieldValue>; to encode an object, use EncodedValues<T>
// (returns EncodedValues) or EncodeValuesAsyByteArray<T> (returns a single packed byte[]).
EncodedValues customValues = ValueEncoderDecoder.EncodedValues(new MyValue
{
    Quantity = 10,
    IsActive = true
});

byte[] packedValues = ValueEncoderDecoder.EncodeValuesAsyByteArray(new MyValue
{
    Quantity = 10,
    IsActive = true
});
```

### Field Layout

```csharp
using Nethereum.Mud.EncodingDecoding;

// Get field layout for a schema.
// EncodeFieldLayout takes the value fields (List<FieldInfo>) and derives the static
// field lengths and dynamic field count from them.
List<FieldInfo> valueFields = SchemaEncoder.GetFieldsFromType<PlayerTableRecord.PlayerValue>();
byte[] fieldLayout = FieldLayoutEncoder.EncodeFieldLayout(valueFields);
```

### Resource Registry

```csharp
using Nethereum.Mud;

// Map an encoded resource id (hex) to a .NET record type
var playerTableResourceId = ResourceEncoder.EncodeTable("Game", "Player");
ResourceTypeRegistry.RegisterType(playerTableResourceId.ToHex(true), typeof(PlayerTableRecord));

// Look the type back up by its encoded resource id
Type recordType = ResourceTypeRegistry.GetResourceType(playerTableResourceId.ToHex(true));
```

## Production Patterns

### 1. Local-First Architecture

Keep MUD table data in memory for fast reads, sync changes to chain:

```csharp
// A change-tracking repository is not generic and stores records for every table.
// Use one instance, or key several by concern if you prefer isolated change sets.
var repositories = new Dictionary<string, InMemoryChangeTrackerTableRepository>
{
    ["Player"] = new InMemoryChangeTrackerTableRepository(),
    ["Inventory"] = new InMemoryChangeTrackerTableRepository(),
    // ...
};

foreach (var repo in repositories.Values)
    repo.StartTracking();

// User interacts locally; upserts are tracked automatically

// Periodic sync to chain
await SyncAllChangesToChainAsync(repositories);
```

### 2. Offline Mode with REST API

```csharp
// Load initial state from REST API
var httpHelper = new RestHttpHelper(new HttpClient());
var apiClient = new StoredRecordRestApiClient(httpHelper, "https://api.mud.game");

var predicate = new TablePredicateBuilder<PlayerTableRecord, PlayerTableRecord.PlayerKey, PlayerTableRecord.PlayerValue>(worldAddress)
    .Expand();
var records = await apiClient.GetTableRecordsAsync<PlayerTableRecord>(predicate);

var localRepo = new InMemoryTableRepository();
foreach (var record in records)
{
    await localRepo.SetRecordAsync(record);
}

// Work offline
// ...

// Sync back when online
var changes = GetLocalChanges();
await SyncToChainAsync(changes);
```

### 3. Real-Time Updates

```csharp
// Subscribe to on-chain table updates
// (See Nethereum.Mud.Contracts for event subscriptions that yield StoredRecord instances)

async Task OnStoreSetRecord(StoredRecord storedRecord)
{
    // Decode the stored record into a strongly-typed record
    var tableRecord = new PlayerTableRecord();
    tableRecord.DecodeValues(storedRecord);                  // StoredRecord : EncodedValues
    tableRecord.DecodeKey(KeyUtils.ConvertKeyFromCombinedHex(storedRecord.Key));

    // Update local repository
    await repository.SetRecordAsync(tableRecord);

    // Notify UI
    NotifyUIOfUpdate(tableRecord);
}
```

## Why Use MUD?

### Composability

MUD applications are inherently composable:
- **Read any World's data** - Query tables from other applications
- **Extend existing apps** - Deploy new systems that interact with existing tables
- **Build meta-applications** - Aggregate data from multiple Worlds

### On-Chain Data Indexing

All state lives on-chain in structured tables:
- **Queryable** - Use Store events to index data
- **Verifiable** - All data is on-chain and cryptographically secure
- **Persistent** - Data survives as long as Ethereum does

### Client Synchronization

MUD provides automatic state sync:
- **Store Events** - `Store_SetRecord`, `Store_DeleteRecord`
- **Real-time updates** - Clients stay in sync via event subscriptions
- **Optimistic updates** - Apply changes locally, sync to chain asynchronously

### Complex Application Building

MUD enables complex on-chain applications:
- **Games** - Fully on-chain games with rich state
- **Autonomous Worlds** - Persistent, extensible virtual worlds
- **DeFi Protocols** - Complex multi-table financial logic
- **Social Networks** - On-chain social graphs and interactions

## Related Packages

### Dependencies
- **Nethereum.Web3** - Ethereum interaction
- **Nethereum.Util.Rest** - REST API utilities

### Used By
- **Nethereum.Mud.Contracts** - On-chain MUD World and Store contracts
- **Nethereum.Mud.Repositories.EntityFramework** - EF Core persistence
- **Nethereum.Mud.Repositories.Postgres** - PostgreSQL persistence

## Additional Resources

- [MUD Documentation](https://mud.dev/)
- [MUD GitHub](https://github.com/latticexyz/mud)
- [Nethereum MUD Console Tests](https://github.com/Nethereum/Nethereum/tree/master/consoletests/NethereumMudLogProcessing)
- [Code Generation Guide](../Nethereum.Contracts/README.md#pattern-3-code-generation-production-recommended)

## Support

- [Nethereum Discord](https://discord.gg/jQPrR58FxX)
- [GitHub Issues](https://github.com/Nethereum/Nethereum/issues)
