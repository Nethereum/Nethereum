---
name: mud-quickstart
description: Help users build MUD applications with Nethereum — define tables, generate C# code, interact with World contracts, use the namespace pattern. Use when users mention MUD, autonomous worlds, on-chain structured data, MUD tables, MUD systems, defineWorld, World contract, or any MUD-related development with C# or .NET.
user-invocable: true
---

# MUD Quickstart with Nethereum

MUD is an enhanced EIP-2535 Diamond pattern for building complex smart contract applications. A World contract acts as a diamond proxy — routing calls to system contracts via delegatecall, with all state stored in typed tables. Namespaces group systems and tables into a unified context (like `web3.Eth`). It's not just for games — any application with complex structured on-chain state benefits.

## When to Use This Skill

- User wants to build a MUD application with .NET/C#
- User needs to generate C# code from MUD table definitions
- User is working with MUD World contracts, tables, or systems
- User mentions `defineWorld`, `mud.config.ts`, or MUD namespaces

## Required Packages

```bash
dotnet add package Nethereum.Mud
dotnet add package Nethereum.Mud.Contracts
```

For code generation CLI:
```bash
dotnet tool install -g Nethereum.Generator.Console
```

## Code Generation Workflow

1. Define tables in `mud.config.ts` using `defineWorld()` at [mud.dev](https://mud.dev)
2. Compile contracts with Forge
3. Create `.nethereum-gen.multisettings`:

```json
[
  {
    "paths": ["path/to/mud.config.ts"],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.Contracts",
        "basePath": "Generated/Tables",
        "codeGenLang": 0,
        "generatorType": "MudTables"
      }
    ]
  }
]
```

4. Run: `Nethereum.Generator.Console generate from-config`

Generator types:
- `MudTables` — generates TableRecord and TableService from mud.config.ts
- `MudExtendedService` — generates system service wrappers from compiled JSON
- `ContractDefinition` — standard ABI-to-C# (non-MUD contracts)

## Using Generated Services

```csharp
var playerService = new PlayerTableService(web3, worldAddress);

// Read
var player = await playerService.GetTableRecordAsync(
    new PlayerTableRecord.PlayerKey { Address = addr });

// Write
await playerService.SetRecordRequestAndWaitForReceiptAsync(
    new PlayerTableRecord.PlayerKey { Address = addr },
    new PlayerTableRecord.PlayerValue { Score = 100, Name = "Alice" });

// Delete
await playerService.DeleteRecordRequestAndWaitForReceiptAsync(
    new PlayerTableRecord.PlayerKey { Address = addr });
```

## Namespace Pattern (Production)

Aggregate generated services into namespace classes:

```csharp
public class AppNamespace : NamespaceBase<AppResource, AppSystems, AppTables>
{
    public AppNamespace(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
    {
        Systems = new AppSystems(web3, contractAddress);
        Tables = new AppTables(web3, contractAddress);
    }
}

// Usage
var app = new AppNamespace(web3, worldAddress);
var player = await app.Tables.Player.GetTableRecordAsync(key);
```

See [CafeCosmos](https://github.com/CafeCosmosHQ/CafeCosmosDotNet) for a production example with 35+ tables.

## Key Classes

| Class | Purpose |
|---|---|
| `TableRecord<TKey, TValue>` | Base for keyed table records |
| `TableRecordSingleton<TValue>` | Base for singleton table records (key: []) |
| `TableService<TRecord, TKey, TValue>` | Typed CRUD for keyed tables |
| `TableSingletonService<TRecord, TValue>` | Typed CRUD for singleton tables |
| `ResourceEncoder` | Encode table/system/namespace resource IDs |
| `NamespaceBase<TResource, TSystems, TTables>` | Base for namespace aggregation |

For full documentation, see: https://docs.nethereum.com/docs/mud-framework/guide-mud-quickstart
