# Nethereum.Mud.Contracts

Nethereum.Mud.Contracts provides contract definitions and services for interacting with [MUD (Onchain Engine)](https://mud.dev/) World and Store contracts. It enables you to call MUD systems, query tables, and process Store events.

## Features

- **World Service** - Interact with MUD World contract
- **Store Services** - Query and subscribe to Store events
- **Table Services** - Type-safe table query and mutation
- **System Services** - Call MUD system functions
- **Event Processing** - Process `Store_SetRecord`, `Store_DeleteRecord`, `Store_SpliceStaticData`, `Store_SpliceDynamicData` events
- **Batch Calls** - Efficient multi-call execution
- **Access Control** - Manage World permissions

## Installation

```bash
dotnet add package Nethereum.Mud.Contracts
```

### Dependencies

- Nethereum.Mud
- Nethereum.Web3

## Key Concepts

### World Contract

The **World** contract is the central registry for all MUD resources:
- Contains all namespaces, tables, and systems
- Provides `call` and `callFrom` for system execution
- Manages access control via `grantAccess` and `revokeAccess`
- Emits Store events for all state changes

### Store Contract

The **Store** is embedded in the World and manages table data:
- Stores all table records as key-value pairs
- Emits events when data changes (`Store_SetRecord`, `Store_DeleteRecord`)
- Handles dynamic data via `Store_SpliceStaticData` and `Store_SpliceDynamicData`
- Provides schema registration via `Tables` table

### Store Events

MUD emits events for all state changes:

**Store_SetRecord**
- Emitted when a table record is created or updated
- Contains: `tableId`, `keyTuple`, `staticData`, `encodedLengths`, `dynamicData`

**Store_DeleteRecord**
- Emitted when a table record is deleted
- Contains: `tableId`, `keyTuple`

**Store_SpliceStaticData**
- Emitted when static data is partially updated
- Contains: `tableId`, `keyTuple`, `start`, `data`

**Store_SpliceDynamicData**
- Emitted when dynamic data (arrays, strings) is partially updated
- Contains: `tableId`, `keyTuple`, `dynamicFieldIndex`, `start`, `deleteCount`, `encodedLengths`, `data`

### Table Services

Generated table services provide type-safe access to MUD tables:
- Query records by key
- Query all records
- Decode table schemas
- Built on top of World contract

### System Services

Generated system services wrap MUD system functions:
- Type-safe function calls
- Automatic ABI encoding
- Integration with World's `call` or direct system calls

## World Contract Interaction

### WorldService

The `WorldService` provides methods for interacting with the World contract:

```csharp
public class WorldService
{
    // Call a system
    Task<TransactionReceipt> CallRequestAndWaitForReceiptAsync(
        byte[] systemId,
        byte[] callData
    );

    // Call a system with delegator
    Task<TransactionReceipt> CallFromRequestAndWaitForReceiptAsync(
        address delegator,
        byte[] systemId,
        byte[] callData
    );

    // Access control
    Task<TransactionReceipt> GrantAccessRequestAndWaitForReceiptAsync(
        byte[] resourceId,
        address grantee
    );

    Task<TransactionReceipt> RevokeAccessRequestAndWaitForReceiptAsync(
        byte[] resourceId,
        address grantee
    );

    // Batch calls
    Task<TransactionReceipt> BatchCallRequestAndWaitForReceiptAsync(
        List<SystemCallData> systemCalls
    );
}
```

## Usage Examples

### Example 1: Setup World Service

```csharp
using Nethereum.Web3;
using Nethereum.Mud.Contracts.World;

var web3 = new Web3("https://rpc.mud.game");
var worldAddress = "0xWorldContractAddress";

var worldService = new WorldService(web3, worldAddress);

Console.WriteLine($"Connected to World at {worldAddress}");
```

### Example 2: Call a MUD System

```csharp
using Nethereum.Mud;
using Nethereum.Mud.Contracts.World;
using Nethereum.ABI.FunctionEncoding;

// Create resource ID for the system
var moveSystemResource = new SystemResource("Game", "MoveSystem");
var systemId = moveSystemResource.ResourceIdEncoded;

// Encode function call (e.g., move(direction))
var functionEncoder = new FunctionCallEncoder();
var moveFunction = new MoveFunction { Direction = 1 }; // Assuming generated function DTO
var callData = functionEncoder.EncodeRequest(moveFunction);

// Call the system via World
var receipt = await worldService.CallRequestAndWaitForReceiptAsync(
    systemId,
    callData
);

Console.WriteLine($"System called successfully. Tx: {receipt.TransactionHash}");
```

### Example 3: Query MUD Table

```csharp
using Nethereum.Mud;
using Nethereum.Mud.Contracts.Store;

// Assuming generated PlayerTableService
var playerTableService = new PlayerTableService(web3, worldAddress);

// Query single record by key
var playerId = 42;
var playerRecord = await playerTableService.GetTableRecordAsync(playerId);

Console.WriteLine($"Player: {playerRecord.Values.Name}");
Console.WriteLine($"Level: {playerRecord.Values.Level}");
Console.WriteLine($"Health: {playerRecord.Values.Health}");
```

### Example 4: Subscribe to Store Events

```csharp
using Nethereum.Mud.Contracts.Core.StoreEvents;

var storeEventsService = new StoreEventsLogProcessingService(web3, worldAddress);

// Subscribe to SetRecord events
var setRecordFilter = storeEventsService.GetSetRecordEvent().CreateFilterInput();
var setRecordSubscription = await setRecordFilter.SubscribeAsync(async (log) =>
{
    var evt = log.Event;
    Console.WriteLine($"Record updated:");
    Console.WriteLine($"  TableId: {evt.TableId.ToHex()}");
    Console.WriteLine($"  Keys: {string.Join(", ", evt.KeyTuple.Select(k => k.ToHex()))}");
    Console.WriteLine($"  Block: {log.Log.BlockNumber.Value}");
});

// Subscribe to DeleteRecord events
var deleteRecordFilter = storeEventsService.GetDeleteRecordEvent().CreateFilterInput();
var deleteRecordSubscription = await deleteRecordFilter.SubscribeAsync(async (log) =>
{
    var evt = log.Event;
    Console.WriteLine($"Record deleted:");
    Console.WriteLine($"  TableId: {evt.TableId.ToHex()}");
    Console.WriteLine($"  Keys: {string.Join(", ", evt.KeyTuple.Select(k => k.ToHex()))}");
});

// Keep subscriptions alive
Console.WriteLine("Subscribed to Store events. Press Ctrl+C to exit.");
await Task.Delay(Timeout.Infinite);
```

### Example 5: Process Store Events to Repository

```csharp
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.TableRepository;

// Create table repository
var playerRepository = new InMemoryTableRepository<PlayerTableRecord>();

// Setup event processing
var storeEventsService = new StoreEventsLogProcessingService(web3, worldAddress);
var progressRepository = new InMemoryBlockProgressRepository();

var processor = storeEventsService.CreateProcessor(
    playerRepository,
    progressRepository,
    logger: null,
    blocksPerRequest: 1000,
    retryWeight: 50,
    minimumBlockConfirmations: 12
);

// Start processing from block 0
await processor.ExecuteAsync(
    startAtBlockNumberIfNotProcessed: 0,
    cancellationToken: CancellationToken.None
);

Console.WriteLine("Store events processed to repository");
```

### Example 6: Batch System Calls

```csharp
using Nethereum.Mud.Contracts.World;

// Prepare multiple system calls
var systemCalls = new List<SystemCallData>
{
    new SystemCallData
    {
        SystemId = moveSystemResource.ResourceIdEncoded,
        CallData = EncodeMoveCall(direction: 1)
    },
    new SystemCallData
    {
        SystemId = attackSystemResource.ResourceIdEncoded,
        CallData = EncodeAttackCall(targetId: 5)
    },
    new SystemCallData
    {
        SystemId = craftSystemResource.ResourceIdEncoded,
        CallData = EncodeCraftCall(itemId: 10, quantity: 1)
    }
};

// Execute all calls in one transaction
var receipt = await worldService.BatchCallRequestAndWaitForReceiptAsync(systemCalls);

Console.WriteLine($"Batch call successful. Gas used: {receipt.GasUsed.Value}");
```

### Example 7: Grant and Revoke Access

```csharp
using Nethereum.Mud;

// Create resource ID for a table
var playerTableResource = new Resource("Game", "Player");
var tableId = playerTableResource.ResourceIdEncoded;

// Grant access to an address
var granteeAddress = "0xGranteeAddress";
var grantReceipt = await worldService.GrantAccessRequestAndWaitForReceiptAsync(
    tableId,
    granteeAddress
);

Console.WriteLine($"Access granted to {granteeAddress}");

// Revoke access
var revokeReceipt = await worldService.RevokeAccessRequestAndWaitForReceiptAsync(
    tableId,
    granteeAddress
);

Console.WriteLine($"Access revoked from {granteeAddress}");
```

### Example 8: Deploy MUD World

```csharp
using Nethereum.Mud.Contracts.World.ContractDefinition;
using Nethereum.Web3.Accounts;

var account = new Account("0xPrivateKey", chainId: 1);
var web3 = new Web3(account, "https://rpc.mud.game");

// Deploy World contract
var deploymentMessage = new WorldDeployment();
var deploymentReceipt = await WorldService.DeployContractAndWaitForReceiptAsync(
    web3,
    deploymentMessage
);

var worldAddress = deploymentReceipt.ContractAddress;
Console.WriteLine($"World deployed at: {worldAddress}");
```

### Example 9: Register Custom Table

```csharp
using Nethereum.Mud;
using Nethereum.Mud.EncodingDecoding;

// Define table schema
var playerTableResource = new Resource("Game", "Player");
var schema = SchemaEncoder.GetSchemaEncoded<PlayerKey, PlayerValue>(
    playerTableResource.ResourceIdEncoded
);

// Register table in World
var registerTableReceipt = await worldService.RegisterTableRequestAndWaitForReceiptAsync(
    tableId: playerTableResource.ResourceIdEncoded,
    fieldLayout: schema.FieldLayout,
    keySchema: schema.KeySchema,
    valueSchema: schema.ValueSchema,
    keyNames: new[] { "playerId" },
    fieldNames: new[] { "name", "level", "health" }
);

Console.WriteLine($"Table registered: {playerTableResource.NameSpace}:{playerTableResource.Name}");
```

### Example 10: Read Table Schema from Chain

```csharp
using Nethereum.Mud.Contracts.Store.Tables;

// TablesTableService queries the on-chain Tables table
var tablesService = new TablesTableService(web3, worldAddress);

// Get schema for a specific table
var playerTableId = new Resource("Game", "Player").ResourceIdEncoded;
var tableRecord = await tablesService.GetTableRecordAsync(playerTableId);

var schema = tableRecord.GetTableSchema();
Console.WriteLine($"Table: {schema.Name}");
Console.WriteLine($"Namespace: {schema.Namespace}");
Console.WriteLine($"Keys: {string.Join(", ", schema.SchemaKeys.Select(k => k.Name))}");
Console.WriteLine($"Values: {string.Join(", ", schema.SchemaValues.Select(v => v.Name))}");
```

## Core Classes

### WorldService

```csharp
public class WorldService : ContractWeb3ServiceBase
{
    public WorldService(Web3 web3, string contractAddress);

    // System calls
    Task<TransactionReceipt> CallRequestAndWaitForReceiptAsync(
        byte[] systemId,
        byte[] callData
    );

    Task<TransactionReceipt> CallFromRequestAndWaitForReceiptAsync(
        string delegator,
        byte[] systemId,
        byte[] callData
    );

    // Batch operations
    Task<TransactionReceipt> BatchCallRequestAndWaitForReceiptAsync(
        List<SystemCallData> systemCalls
    );

    // Access control
    Task<TransactionReceipt> GrantAccessRequestAndWaitForReceiptAsync(
        byte[] resourceId,
        string grantee
    );

    Task<TransactionReceipt> RevokeAccessRequestAndWaitForReceiptAsync(
        byte[] resourceId,
        string grantee
    );

    // Table registration
    Task<TransactionReceipt> RegisterTableRequestAndWaitForReceiptAsync(
        byte[] tableId,
        byte[] fieldLayout,
        byte[] keySchema,
        byte[] valueSchema,
        string[] keyNames,
        string[] fieldNames
    );
}
```

### StoreEventsLogProcessingService

```csharp
public class StoreEventsLogProcessingService
{
    public StoreEventsLogProcessingService(Web3 web3, string contractAddress);

    // Get event definitions
    Event<StoreSetRecordEventDTO> GetSetRecordEvent();
    Event<StoreDeleteRecordEventDTO> GetDeleteRecordEvent();
    Event<StoreSpliceStaticDataEventDTO> GetSpliceStaticDataEvent();
    Event<StoreSpliceDynamicDataEventDTO> GetSpliceDynamicDataEvent();

    // Create event processor
    ILogProcessor CreateProcessor(
        ITableRepository tableRepository,
        IBlockProgressRepository progressRepository,
        ILogger logger,
        int blocksPerRequest,
        int retryWeight,
        uint minimumBlockConfirmations
    );
}
```

### Generated Table Service

Example of a generated table service:

```csharp
public class PlayerTableService
{
    public PlayerTableService(Web3 web3, string worldAddress);

    // Query single record
    Task<PlayerTableRecord> GetTableRecordAsync(BigInteger playerId);

    // Query all records (via events)
    Task<List<PlayerTableRecord>> GetTableRecordsAsync(
        BigInteger? fromBlock = null,
        BigInteger? toBlock = null
    );

    // Get table resource
    Resource GetTableResource();
}
```

### Store Event DTOs

```csharp
[Event("Store_SetRecord")]
public class StoreSetRecordEventDTO : IEventDTO
{
    [Parameter("bytes32", "tableId", 1, true)]
    public byte[] TableId { get; set; }

    [Parameter("bytes32[]", "keyTuple", 2, false)]
    public List<byte[]> KeyTuple { get; set; }

    [Parameter("bytes", "staticData", 3, false)]
    public byte[] StaticData { get; set; }

    [Parameter("bytes32", "encodedLengths", 4, false)]
    public byte[] EncodedLengths { get; set; }

    [Parameter("bytes", "dynamicData", 5, false)]
    public byte[] DynamicData { get; set; }
}

[Event("Store_DeleteRecord")]
public class StoreDeleteRecordEventDTO : IEventDTO
{
    [Parameter("bytes32", "tableId", 1, true)]
    public byte[] TableId { get; set; }

    [Parameter("bytes32[]", "keyTuple", 2, false)]
    public List<byte[]> KeyTuple { get; set; }
}
```

## Code Generation

Generate table and system services using `.nethereum-gen.multisettings`:

### Configuration

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

### Generated Output

**MudTables** generates:
- `PlayerTableRecord.gen.cs` - Table record with Keys and Values
- `PlayerTableService.gen.cs` - Service for querying the table

**MudExtendedService** generates:
- `MoveSystemService.gen.cs` - Service for calling system functions
- Function DTOs for all system methods

### Running Generation

```bash
# Install Nethereum code generator
npm install -g nethereum-codegen

# Generate C# code
nethereum-codegen generate

# Or use VS Code extension
# https://github.com/juanfranblanco/vscode-solidity
```

## Event Processing Patterns

### Pattern 1: Real-Time Event Subscription

```csharp
var storeEventsService = new StoreEventsLogProcessingService(web3, worldAddress);

var subscription = await storeEventsService
    .GetSetRecordEvent()
    .CreateFilterInput()
    .SubscribeAsync(async (eventLog) =>
    {
        var evt = eventLog.Event;

        // Update local repository
        await UpdateRepositoryFromEvent(evt);

        // Notify UI
        await NotifyUIAsync(evt);
    });
```

### Pattern 2: Historical Event Processing

```csharp
var storeEventsService = new StoreEventsLogProcessingService(web3, worldAddress);
var progressRepo = new InMemoryBlockProgressRepository();
var tableRepo = new InMemoryTableRepository<PlayerTableRecord>();

var processor = storeEventsService.CreateProcessor(
    tableRepo,
    progressRepo,
    logger: null,
    blocksPerRequest: 1000,
    retryWeight: 50,
    minimumBlockConfirmations: 12
);

// Process all historical events
await processor.ExecuteAsync(
    startAtBlockNumberIfNotProcessed: 0,
    cancellationToken: CancellationToken.None
);
```

### Pattern 3: Hybrid (Historical + Real-Time)

```csharp
// Step 1: Process historical events
await processor.ExecuteAsync(startAtBlockNumberIfNotProcessed: 0);

// Step 2: Subscribe to new events
var subscription = await storeEventsService
    .GetSetRecordEvent()
    .CreateFilterInput()
    .SubscribeAsync(async (eventLog) =>
    {
        await UpdateRepositoryFromEvent(eventLog.Event);
    });

// Now fully synced with real-time updates
```

## Advanced Topics

### Custom Event Handlers

```csharp
public class CustomEventHandler
{
    public async Task HandleSetRecord(StoreSetRecordEventDTO evt)
    {
        // Custom logic for SetRecord
        var tableId = evt.TableId.ToHex();

        if (tableId == playerTableId)
        {
            await HandlePlayerUpdate(evt);
        }
        else if (tableId == inventoryTableId)
        {
            await HandleInventoryUpdate(evt);
        }
    }
}
```

### Filtering Events by Table

```csharp
var playerTableId = new Resource("Game", "Player").ResourceIdEncoded;

var filter = storeEventsService.GetSetRecordEvent().CreateFilterInput(
    filterTopic1: new[] { playerTableId }
);

var subscription = await filter.SubscribeAsync(async (eventLog) =>
{
    // Only receives events for Player table
    var playerEvent = eventLog.Event;
    await HandlePlayerUpdate(playerEvent);
});
```

### Handling Splice Events

```csharp
// Subscribe to dynamic data updates (e.g., array push/pop)
var spliceDynamicFilter = storeEventsService
    .GetSpliceDynamicDataEvent()
    .CreateFilterInput();

var subscription = await spliceDynamicFilter.SubscribeAsync(async (eventLog) =>
{
    var evt = eventLog.Event;

    Console.WriteLine($"Dynamic data updated:");
    Console.WriteLine($"  TableId: {evt.TableId.ToHex()}");
    Console.WriteLine($"  Start: {evt.Start}");
    Console.WriteLine($"  DeleteCount: {evt.DeleteCount}");
    Console.WriteLine($"  New data length: {evt.Data.Length}");
});
```

## Production Patterns

### 1. Decentralized Game Client

```csharp
// Initialize World connection
var worldService = new WorldService(web3, worldAddress);

// Load initial state from chain
var playerTable = new PlayerTableService(web3, worldAddress);
var players = await playerTable.GetTableRecordsAsync(fromBlock: 0);

// Subscribe to updates
var subscription = await SubscribeToStoreEvents(worldAddress);

// User action: Move player
var moveSystemResource = new SystemResource("Game", "MoveSystem");
await worldService.CallRequestAndWaitForReceiptAsync(
    moveSystemResource.ResourceIdEncoded,
    EncodeMoveCall(direction: 1)
);
```

### 2. MUD Indexer

```csharp
// Continuously index all MUD events to database
var dbRepository = new PostgresTableRepository(connectionString);
var progressRepo = new PostgresBlockProgressRepository(connectionString);

var processor = storeEventsService.CreateProcessor(
    dbRepository,
    progressRepo,
    logger,
    blocksPerRequest: 1000,
    retryWeight: 50,
    minimumBlockConfirmations: 12
);

while (true)
{
    try
    {
        await processor.ExecuteAsync(0);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Indexer error, retrying...");
        await Task.Delay(30000);
    }
}
```

### 3. Multi-World Aggregator

```csharp
// Query data from multiple MUD Worlds
var world1Service = new WorldService(web3, world1Address);
var world2Service = new WorldService(web3, world2Address);

var player1Table = new PlayerTableService(web3, world1Address);
var player2Table = new PlayerTableService(web3, world2Address);

// Aggregate player data from both worlds
var players1 = await player1Table.GetTableRecordsAsync();
var players2 = await player2Table.GetTableRecordsAsync();

var allPlayers = players1.Concat(players2).ToList();
```

## Related Packages

### Dependencies
- **Nethereum.Mud** - Core MUD types and table records
- **Nethereum.Web3** - Ethereum client

### Used By
- **Nethereum.Mud.Repositories.EntityFramework** - EF Core event processing
- **Nethereum.Mud.Repositories.Postgres** - PostgreSQL event processing

### Related
- **Nethereum.Contracts** - Contract interaction base

## Additional Resources

- [MUD Documentation](https://mud.dev/)
- [MUD World Contract](https://github.com/latticexyz/mud/tree/main/packages/world)
- [MUD Store Protocol](https://github.com/latticexyz/mud/tree/main/packages/store)
- [Nethereum MUD Console Tests](https://github.com/Nethereum/Nethereum/tree/master/consoletests/NethereumMudLogProcessing)

## Support

- [Nethereum Discord](https://discord.gg/jQPrR58FxX)
- [GitHub Issues](https://github.com/Nethereum/Nethereum/issues)
