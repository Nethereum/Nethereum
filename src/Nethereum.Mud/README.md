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
    public PlayerTableRecord() : base("Player") { }

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
# Using Nethereum.Generators.JavaScript
npm install -g nethereum-codegen

# Generate from mud.config.ts
nethereum-codegen generate
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

var schema = SchemaEncoder.GetSchemaEncoded<PlayerKey, PlayerValue>(resourceId);

Console.WriteLine($"Key schema: {schema.KeySchema.ToHex()}");
Console.WriteLine($"Value schema: {schema.ValueSchema.ToHex()}");
Console.WriteLine($"Static fields: {schema.NumStaticFields}");
Console.WriteLine($"Dynamic fields: {schema.NumDynamicFields}");
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

var encodedValues = playerRecord.GetEncodedValues();
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
var encodedValues = playerRecord.GetEncodedValues();

// Decode from on-chain data
playerRecord.DecodeKey(encodedKeyBytes);
playerRecord.DecodeValues(encodedValuesBytes);

Console.WriteLine($"Player {playerRecord.Keys.PlayerId}: {playerRecord.Values.Name}");
```

### Example 2: In-Memory Table Repository

```csharp
using Nethereum.Mud.TableRepository;

// Create in-memory repository
var repository = new InMemoryTableRepository<PlayerTableRecord>();

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

await repository.UpsertAsync(player1);
await repository.UpsertAsync(player2);

// Query by key
var record = await repository.GetByKeyAsync(player1.GetEncodedKey());
Console.WriteLine($"Found player: {record.Values.Name}");

// Query all records
var allPlayers = await repository.GetAsync();
Console.WriteLine($"Total players: {allPlayers.Count}");
```

### Example 3: Query with Predicates

```csharp
using Nethereum.Mud.TableRepository;

var repository = new InMemoryTableRepository<PlayerTableRecord>();

// Add multiple records
// ...

// Query with predicate builder
var predicate = new TablePredicateBuilder<PlayerTableRecord>()
    .Where(player => player.Values.Level > 5)
    .And(player => player.Values.Health >= 50)
    .Build();

var results = await repository.GetAsync(predicate);

foreach (var player in results)
{
    Console.WriteLine($"Player {player.Keys.PlayerId}: Level {player.Values.Level}, Health {player.Values.Health}");
}
```

### Example 4: Change Tracking Repository

```csharp
using Nethereum.Mud.TableRepository;

// Repository with change tracking
var repository = new InMemoryChangeTrackerTableRepository<PlayerTableRecord>();

// Modify records
var player = await repository.GetByKeyAsync(encodedKey);
player.Values.Level += 1;
player.Values.Health -= 10;
await repository.UpsertAsync(player);

// Get all changes since last checkpoint
var changeSet = repository.GetChangeSet();

Console.WriteLine($"Added: {changeSet.AddedRecords.Count}");
Console.WriteLine($"Updated: {changeSet.UpdatedRecords.Count}");
Console.WriteLine($"Deleted: {changeSet.DeletedRecords.Count}");

// Clear change tracking
repository.ClearChangeSet();
```

### Example 5: Resource Identifiers

```csharp
using Nethereum.Mud;

// Create resource for a table
var playerTableResource = new Resource("Game", "Player");

// Get resource ID (bytes32)
var resourceId = playerTableResource.ResourceIdEncoded;
Console.WriteLine($"Resource ID: {resourceId.ToHex()}");

// Create system resource
var moveSystemResource = new SystemResource("Game", "MoveSystem");

// Namespace resource
var gameNamespace = new NamespaceResource("Game");
```

### Example 6: Schema Encoding

```csharp
using Nethereum.Mud.EncodingDecoding;

// Get schema for a table record type
var schema = SchemaEncoder.GetSchemaEncoded<PlayerKey, PlayerValue>(resourceId);

Console.WriteLine($"Key schema: {schema.KeySchema.ToHex()}");
Console.WriteLine($"Value schema: {schema.ValueSchema.ToHex()}");
Console.WriteLine($"Total static fields: {schema.NumStaticFields}");
Console.WriteLine($"Total dynamic fields: {schema.NumDynamicFields}");
```

### Example 7: REST API Client

```csharp
using Nethereum.Mud.TableRepository;

// Connect to remote table repository API
var apiClient = new StoredRecordRestApiClient("https://api.example.com/mud");

// Query specific table
var records = await apiClient.GetRecordsAsync<PlayerTableRecord>(
    worldAddress: "0xWorldAddress",
    tableId: playerTableResource.ResourceIdEncoded.ToHex()
);

foreach (var record in records)
{
    Console.WriteLine($"Player {record.Keys.PlayerId}: {record.Values.Name}");
}

// Query with filters
var filteredRecords = await apiClient.GetRecordsAsync<PlayerTableRecord>(
    worldAddress: "0xWorldAddress",
    tableId: playerTableResource.ResourceIdEncoded.ToHex(),
    filter: $"Level gt 5"
);
```

### Example 8: Working with Stored Records

```csharp
using Nethereum.Mud.TableRepository;

// StoredRecord is the persisted form of a table record
var storedRecord = new StoredRecord
{
    WorldAddress = "0xWorldAddress",
    TableId = playerTableResource.ResourceIdEncoded.ToHex(),
    Key0 = encodedKey[0].ToHex(),  // First key component
    StaticData = encodedStaticData.ToHex(),
    DynamicData = encodedDynamicData.ToHex(),
    EncodedLengths = encodedLengths.ToHex(),
    IsDeleted = false
};

// Convert to table record
var mapper = new StoredRecordDTOMapper<PlayerTableRecord>();
var tableRecord = mapper.MapFromStoredRecord(storedRecord);

Console.WriteLine($"Restored player {tableRecord.Keys.PlayerId}");
```

### Example 9: Singleton Tables (No Keys)

Some MUD tables have no keys (configuration singletons):

```csharp
using Nethereum.Mud;

// Singleton table record
public class ConfigTableRecord : TableRecordSingleton<ConfigValue>
{
    public ConfigTableRecord() : base("Config") { }

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
var encodedValues = config.GetEncodedValues();
```

### Example 10: Production MUD Application Pattern

```csharp
using Nethereum.Mud;
using Nethereum.Mud.TableRepository;
using Nethereum.Web3;

// Initialize repository with change tracking
var playerRepository = new InMemoryChangeTrackerTableRepository<PlayerTableRecord>();
var inventoryRepository = new InMemoryChangeTrackerTableRepository<InventoryTableRecord>();

// Load initial state from chain or database
// ...

// Application logic modifies records
var player = await playerRepository.GetByKeyAsync(encodedPlayerId);
player.Values.Level += 1;
player.Values.Health = 100;
await playerRepository.UpsertAsync(player);

var inventory = await inventoryRepository.GetByKeyAsync(encodedInventoryKey);
inventory.Values.Quantity -= 1;
await inventoryRepository.UpsertAsync(inventory);

// Get changes to sync with chain
var playerChanges = playerRepository.GetChangeSet();
var inventoryChanges = inventoryRepository.GetChangeSet();

// Batch changes for on-chain transaction
var allChanges = new List<TableRecordChangeSet>
{
    playerChanges,
    inventoryChanges
};

// Send to chain via MUD World contract
// (See Nethereum.Mud.Contracts for World interaction)

// Clear change tracking after sync
playerRepository.ClearChangeSet();
inventoryRepository.ClearChangeSet();
```

## Core Classes

### TableRecord<TKey, TValue>

Base class for MUD table records with keys.

```csharp
public abstract class TableRecord<TKey, TValue> : ITableRecord
    where TKey : class, new()
    where TValue : class, new()
{
    public TKey Keys { get; set; }
    public TValue Values { get; set; }

    public List<byte[]> GetEncodedKey();
    public EncodedValues GetEncodedValues();
    public void DecodeKey(List<byte[]> encodedKey);
    public void DecodeValues(EncodedValues encodedValues);
}
```

### TableRecordSingleton<TValue>

Base class for tables without keys (singletons).

```csharp
public abstract class TableRecordSingleton<TValue> : ITableRecordSingleton
    where TValue : class, new()
{
    public TValue Values { get; set; }

    public EncodedValues GetEncodedValues();
    public void DecodeValues(EncodedValues encodedValues);
}
```

### ITableRepository

Interface for table record storage and querying.

```csharp
public interface ITableRepository<TTableRecord> where TTableRecord : ITableRecord, new()
{
    Task<TTableRecord> GetByKeyAsync(List<byte[]> key);
    Task<List<TTableRecord>> GetAsync();
    Task<List<TTableRecord>> GetAsync(TablePredicate<TTableRecord> predicate);
    Task UpsertAsync(TTableRecord record);
    Task DeleteAsync(List<byte[]> key);
}
```

### Resource

Represents a MUD resource identifier.

```csharp
public class Resource
{
    public Resource(string nameSpace, string name);

    public string NameSpace { get; }
    public string Name { get; }
    public byte[] ResourceIdEncoded { get; }
}
```

## Advanced Topics

### Custom Encoding

```csharp
using Nethereum.Mud.EncodingDecoding;

// Custom key encoding
var customKeys = KeyEncoderDecoder.EncodeKey(new MyKey
{
    PlayerId = 1,
    ItemId = 42
});

// Custom value encoding
var customValues = ValueEncoderDecoder.EncodeValues(new MyValue
{
    Quantity = 10,
    IsActive = true
});
```

### Field Layout

```csharp
using Nethereum.Mud.EncodingDecoding;

// Get field layout for a schema
var fieldLayout = FieldLayoutEncoder.Encode(
    staticFieldLengths: new List<byte> { 32, 32, 1 }, // uint256, uint256, bool
    numDynamicFields: 2 // Two dynamic fields (bytes or arrays)
);
```

### Resource Registry

```csharp
using Nethereum.Mud;

// Register custom resource types
ResourceTypeRegistry.Register("CustomType", 0x1234);

// Get resource type
var tableType = ResourceTypeRegistry.GetResourceTypeId("Table"); // 0x7462...
var systemType = ResourceTypeRegistry.GetResourceTypeId("System"); // 0x7379...
```

## Production Patterns

### 1. Local-First Architecture

Keep MUD table data in memory for fast reads, sync changes to chain:

```csharp
// In-memory repositories for all tables
var repositories = new Dictionary<string, object>
{
    ["Player"] = new InMemoryChangeTrackerTableRepository<PlayerTableRecord>(),
    ["Inventory"] = new InMemoryChangeTrackerTableRepository<InventoryTableRecord>(),
    // ...
};

// User interacts locally
// Changes tracked automatically

// Periodic sync to chain
await SyncAllChangesToChainAsync(repositories);
```

### 2. Offline Mode with REST API

```csharp
// Load initial state from REST API
var apiClient = new StoredRecordRestApiClient("https://api.mud.game");
var records = await apiClient.GetRecordsAsync<PlayerTableRecord>(worldAddress, tableId);

var localRepo = new InMemoryTableRepository<PlayerTableRecord>();
foreach (var record in records)
{
    await localRepo.UpsertAsync(record);
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
// (See Nethereum.Mud.Contracts for event subscriptions)

void OnStoreSetRecordEvent(StoreSetRecordEventDTO evt)
{
    var storedRecord = evt.ToStoredRecord();
    var tableRecord = mapper.MapFromStoredRecord<PlayerTableRecord>(storedRecord);

    // Update local repository
    await repository.UpsertAsync(tableRecord);

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
- [Code Generation Guide](../Nethereum.Contracts/README.md#advanced-multi-settings-configuration-preferred)

## Support

- [Nethereum Discord](https://discord.gg/jQPrR58FxX)
- [GitHub Issues](https://github.com/Nethereum/Nethereum/issues)
