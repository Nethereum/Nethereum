---
name: mud-indexing
description: Help users index MUD Store events, process on-chain table mutations, store in PostgreSQL or EF Core, normalise schemas, run continuous sync. Use when users mention MUD indexing, Store events, MUD PostgreSQL, MUD EF Core, StoreEventsLogProcessingService, or building an indexer for MUD World data.
user-invocable: true
---

# MUD Store Event Indexing with Nethereum

Every MUD table mutation emits a Store event. By processing these events, you can rebuild the complete state of any MUD World and store it in PostgreSQL with typed relational tables.

## When to Use This Skill

- User wants to index MUD World state off-chain
- User needs to process Store events into a database
- User wants PostgreSQL tables mirroring MUD schemas
- User mentions StoreEventsLogProcessingService, MUD normalisation, or MUD repositories

## Required Packages

```bash
dotnet add package Nethereum.Mud.Contracts

# For PostgreSQL normalisation:
dotnet add package Nethereum.Mud.Repositories.Postgres

# For EF Core (any database):
dotnet add package Nethereum.Mud.Repositories.EntityFramework
```

## Quick Start: In-Memory

```csharp
var storeEventsService = new StoreEventsLogProcessingService(web3, worldAddress);
var repository = new InMemoryTableRepository();
await storeEventsService.ProcessAllStoreChangesAsync(repository);
// repository now contains full current state
```

## PostgreSQL Normalisation

```csharp
var normaliser = new MudPostgresStoreRecordsNormaliser(connectionString, worldAddress);
await normaliser.UpsertAsync(repository);
```

Creates typed PostgreSQL tables: `{namespace}__{tableName}` with proper column types.

## Query Normalised Tables

```csharp
var queryService = new NormalisedTableQueryService(connectionString);
var tables = await queryService.GetAvailableTablesAsync();
var results = await queryService.QueryAsync("app__Player", limit: 100);
var count = await queryService.CountAsync("app__Player");
```

## Continuous Sync

```csharp
var processor = storeEventsService.CreateProcessor(
    repository,
    blockProgressRepository: new InMemoryBlockchainProgressRepository(),
    numberOfBlocksPerRequest: 1000);

await processor.ExecuteAsync(cancellationToken);
```

## Processing from Transaction Receipt

```csharp
var receipt = await playerService.SetRecordRequestAndWaitForReceiptAsync(key, value);
await StoreEventsLogProcessingService.ProcessAllStoreChangesFromLogs(repository, receipt);
```

## Storage Decision Table

| Strategy | Best For |
|---|---|
| `InMemoryTableRepository` | Development, testing |
| `MudEFCoreTableRepository` | Any EF Core database, simple persistence |
| PostgreSQL normalisation | Production, analytics, explorer UIs |

For full documentation, see: https://docs.nethereum.com/docs/mud-framework/guide-mud-indexing
