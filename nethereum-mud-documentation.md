# Nethereum.MUD Integration Documentation

## Overview

Nethereum.MUD provides seamless integration between the Nethereum .NET Ethereum library and MUD (Multi-User Dungeon), enabling .NET developers to build complex applications and games on Ethereum using Unity, Blazor, and other .NET platforms.

**Author:** Juan Blanco (Creator of Nethereum and VSCode Solidity extension)

---

## Quick Reference: Code Generation

| Generator Type | Input | Output | Purpose |
|----------------|-------|--------|---------|
| `ContractDefinition` | ABI JSON | Standard Service | Deployment, functions, events |
| `MudExtendedService` | ABI JSON | Extended Service | + Resource ID, Create2, registration |
| `MudTables` | mud.config.ts | TableRecord + TableService | Table schema and CRUD operations |
| `UnityRequest` | ABI JSON | Unity Requests | Unity-specific coroutine handlers |

---

## What is MUD?

MUD is a framework for building complex Ethereum applications and games. It provides:

### Smart Contract Features

- **Data Model**: Structured table-based storage supporting key-value and relational database formats
- **Dynamic Schemas**: Define schemas at runtime with flexibility to evolve post-deployment
- **Typed Data Access**: Auto-generated libraries for type-safe getters and setters with Solidity-compatible encoding
- **Data Indexing**: Events on all data changes, enabling real-time off-chain indexing of all on-chain data

### Why MUD for Complex Applications and Games

- **Scalability**: Supports advanced data structures, ideal for complex logic in applications
- **Flexibility**: Adapts as needs evolve, allowing new data schemas without redeployment
- **Upgradability**: Upgrade or add new business logic without migrating data
- **Interoperability**: Data structures can be directly mapped to relational databases, easing off-chain integration
- **Delegated Authority**: Device wallets / Burner / Session wallets - users should not worry about their "main" account when using an application or a game
- **Ever Evolving Worlds**: New namespaces with new functionality and authority allows you or anyone to expand the "world"

---

## Nethereum Vision

> To provide the tools, frameworks and guidance so any .NET developer and application can be created or integrated with Ethereum - bringing the love of Ethereum to .NET (and vice versa).

### .NET Ecosystem Support

Nethereum supports integration across the entire .NET ecosystem:

- **Mobile**: iOS, Android, Tizen
- **Desktop**: Windows, macOS, Linux
- **Gaming**: Unity (AR/VR, Wearables, TV)
- **Web**: Blazor, ASP.NET, WebAssembly (WASM)
- **Cloud**: Azure, AWS
- **IoT**: Raspberry Pi, Windows IoT
- **Enterprise**: LOB applications, Windows Server

---

## Nethereum Features

### Core Integration
- Ethereum node (JSON-RPC)
- Smart contract integration
- RLP encoding
- EVM simulator
- Signing
- ABI Encoding
- Chain/log indexing
- L2 support

### External Wallets and Signers
- MetaMask
- WalletConnect
- Reown AppKit (Blazor support)
- Azure Key Vault
- AWS Key Management
- Hardware wallets (Ledger, Trezor)

### Tooling
- Nethereum Playground
- Code generators
- Testing frameworks

### Standards and Common Protocols
- SIWE (Sign-In with Ethereum)
- Gnosis Safe
- ENS
- ERC20, ERC721, ERC1155
- EIP-712
- ERC6492

### Templates and Examples
- Getting started templates (smart contracts)
- Blazor template
- SIWE template
- Web, Desktop, Mobile, Unity examples

---

## Nethereum.MUD Architecture

### Core Components

```
Nethereum + MUD
     │
     ├── Tables (Data Layer)
     │   ├── TableRecord
     │   ├── TableService
     │   └── TableRepository (InMemory, EF, Postgres)
     │
     ├── Systems (Business Logic)
     │   ├── SystemService
     │   └── SystemServiceResource
     │
     └── Namespaces (Organization)
         ├── NamespaceResource
         └── TablesServices / SystemsServices
```

---

## Code Generation

Nethereum's code generation extends the existing standard contract service generation to support MUD-specific components. The code generator produces:

1. **Standard Contract Services** - Same as regular Nethereum generation
2. **MUD Extended Services** - Systems with MUD-specific functionality (Resource IDs, Create2 deployment, function registration)
3. **MUD Tables** - Table records and table services from `mud.config.ts`

### MUD Code Generation Workflow

```
┌─────────────────────────────────────────────────────────────────────┐
│                        MUD Code Generation                          │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  mud.config.ts ──────► MUD tablegen ──────► Solidity Tables         │
│       │                                           │                 │
│       │                                           ▼                 │
│       │                                    Compile (forge/hardhat)  │
│       │                                           │                 │
│       ▼                                           ▼                 │
│  .nethereum-gen.multisettings              ABI JSON files           │
│       │                                           │                 │
│       └───────────────┬───────────────────────────┘                 │
│                       │                                             │
│                       ▼                                             │
│              Nethereum Code Generator                               │
│              (VSCode Right-Click or CLI)                            │
│                       │                                             │
│       ┌───────────────┼───────────────┐                             │
│       ▼               ▼               ▼                             │
│  TableRecords   TableServices   SystemServices                      │
│  (from config)  (from config)   (from ABI + MudExtended)            │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### Step 1: MUD Config and Table Generation

First, define your tables in `mud.config.ts`:

```typescript
// mud.config.ts
import { defineWorld } from "@latticexyz/world";

export default defineWorld({
  namespace: "myworld",
  tables: {
    CatalogueItem: {
      schema: {
        id: "uint32",
        price: "uint32",
        name: "string",
        description: "string",
        owner: "string",
      },
      key: ["id"],
    },
    Counter: {
      schema: {
        value: "uint32",
      },
      key: [],
    },
  },
});
```

Generate Solidity tables using MUD tablegen:

```bash
# Note: On Windows, easier to use Git Bash or WSL2
pnpm mud tablegen
```

### Step 2: Configure Nethereum Generation

Create `.nethereum-gen.multisettings` file:

```json
[
  {
    "paths": ["out/IncrementSystem.sol/IncrementSystem.json"],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.Contracts.MyWorld.Systems",
        "basePath": "MyProject.Contracts/MyWorld/Systems",
        "codeGenLang": 0,
        "generatorType": "ContractDefinition"
      },
      {
        "baseNamespace": "MyProject.Contracts.MyWorld.Systems",
        "basePath": "MyProject.Contracts/MyWorld/Systems",
        "codeGenLang": 0,
        "generatorType": "MudExtendedService",
        "mudNamespace": "myworld"
      }
    ]
  },
  {
    "paths": ["mud.config.ts"],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.Contracts.MyWorld.Tables",
        "basePath": "MyProject.Contracts/MyWorld/Tables",
        "generatorType": "MudTables",
        "mudNamespace": "myworld"
      }
    ]
  }
]
```

### Step 3: Generate Code

**Using VSCode Solidity Extension:**
- Right-click on the project
- Select "Nethereum: Generate Code from Settings"

**Using CLI:**
```bash
Nethereum.Generator.Console generate from-config
# Defaults to ".nethereum-gen.multisettings" in current folder
```

### Configuration File: `.nethereum-gen.multisettings`

The code generator supports multiple configurations for generating client-side code for various platforms:

```json
[
  {
    "paths": ["out/ERC20.sol/Standard_Token.json"],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.Contracts",
        "basePath": "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts",
        "codeGenLang": 0,
        "generatorType": "ContractDefinition"
      },
      {
        "baseNamespace": "MyProject.Contracts",
        "basePath": "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts",
        "codeGenLang": 0,
        "generatorType": "UnityRequest"
      }
    ]
  },
  {
    "paths": ["out/IncrementSystem.sol/IncrementSystem.json"],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.Contracts.MyWorld1.Systems",
        "basePath": "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts/MyWorld1/Systems",
        "codeGenLang": 0,
        "generatorType": "ContractDefinition",
        "mudNamespace": "myworld1"
      },
      {
        "baseNamespace": "MyProject.Contracts.MyWorld1.Systems",
        "basePath": "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts/MyWorld1/Systems",
        "codeGenLang": 0,
        "generatorType": "MudExtendedService",
        "mudNamespace": "myworld1"
      }
    ]
  },
  {
    "paths": ["mudMultipleNamespace/mud.config.ts"],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.Contracts.MyWorld1.Tables",
        "basePath": "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts/MyWorld1/Tables",
        "generatorType": "MudTables",
        "mudNamespace": "myworld1"
      }
    ]
  }
]
```

### Generator Types

| Type | Description |
|------|-------------|
| `ContractDefinition` | Standard Nethereum service, deployment, etc. |
| `MudExtendedService` | Extended functionality for MUD systems |
| `MudTables` | Table definitions from mud.config.ts |
| `UnityRequest` | Unity-specific request handlers |

---

## Generated Code: Systems (MudExtendedService)

When you use `MudExtendedService`, the generator creates a **partial class that extends** the standard `ContractDefinition` service with MUD-specific functionality.

### What Gets Generated

**1. Standard Contract Service (ContractDefinition):**

```csharp
// IncrementSystemService.cs - Standard Nethereum service
public partial class IncrementSystemService
{
    public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(...)
    public static Task<IncrementSystemService> DeployContractAndGetServiceAsync(...)
    
    // All function handlers from ABI
    public Task<string> IncrementRequestAsync(IncrementFunction incrementFunction) { ... }
    public Task<TransactionReceipt> IncrementRequestAndWaitForReceiptAsync(...) { ... }
    
    // Event handlers, error handlers, etc.
}
```

**2. MUD Extended Service (MudExtendedService) - Extends the above:**

```csharp
// IncrementSystemServiceMudExtended.cs - MUD-specific extensions
public class IncrementSystemServiceResource : SystemResource
{
    public IncrementSystemServiceResource() : base("IncrementSystem") { }
}

public partial class IncrementSystemService : ISystemService<IncrementSystemServiceResource>
{
    // System Resource identification
    public IResource Resource => this.GetResource();

    // System registration helper
    public ISystemServiceResourceRegistration SystemServiceResourceRegistrator
    {
        get
        {
            return this.GetSystemServiceResourceRegistration<IncrementSystemServiceResource, IncrementSystemService>();
        }
    }

    // Get all function ABIs for registration
    public List<FunctionABI> GetSystemFunctionABIs()
    {
        return GetAllFunctionABIs();
    }

    // Create2 deployment support
    public string CalculateCreate2Address(string deployerAddress, string salt, params ByteCodeLibrary[] byteCodeLibraries)
    {
        return new IncrementSystemDeployment().CalculateCreate2Address(deployerAddress, salt, byteCodeLibraries);
    }

    public Task<Create2ContractDeploymentTransactionResult> DeployCreate2ContractAsync(
        string deployerAddress, string salt, params ByteCodeLibrary[] byteCodeLibraries)
    {
        var create2ProxyDeployerService = Web3.Eth.Create2DeterministicDeploymentProxyService;
        var deployment = new IncrementSystemDeployment();
        return create2ProxyDeployerService.DeployContractRequestAsync(deployment, deployerAddress, salt, byteCodeLibraries);
    }

    public Task<Create2ContractDeploymentTransactionReceiptResult> DeployCreate2ContractAndWaitForReceiptAsync(
        string deployerAddress, string salt, ByteCodeLibrary[] byteCodeLibraries, CancellationToken cancellationToken = default)
    {
        var create2ProxyDeployerService = Web3.Eth.Create2DeterministicDeploymentProxyService;
        var deployment = new IncrementSystemDeployment();
        return create2ProxyDeployerService.DeployContractRequestAndWaitForReceiptAsync(
            deployment, deployerAddress, salt, byteCodeLibraries, cancellationToken);
    }
}
```

### Key MUD Extensions

| Feature | Description |
|---------|-------------|
| `ISystemService<T>` | Interface marking this as a MUD system |
| `SystemResource` | Resource identifier for the system |
| `GetSystemFunctionABIs()` | Returns all functions to register with World |
| `SystemServiceResourceRegistrator` | Helper for registering the system |
| `CalculateCreate2Address()` | Deterministic address calculation |
| `DeployCreate2ContractAsync()` | Create2 deployment support |

---

## Generated Code: Tables (MudTables)

When you use `MudTables`, the generator reads `mud.config.ts` and creates:

1. **TableRecord** - Data structure matching the MUD schema
2. **TableService** - Service for interacting with the table

### What Gets Generated

**1. TableRecord - Maps directly to mud.config.ts schema:**

```csharp
// CatalogueItemTableRecord.cs
public partial class CatalogueItemTableRecord : TableRecord<CatalogueItemTableRecord.CatalogueItemKey, CatalogueItemTableRecord.CatalogueItemValue> 
{
    // Constructor with table name and namespace
    public CatalogueItemTableRecord() : base("myworld", "CatalogueItem")
    {
    }
    
    // ========== Direct Property Accessors ==========
    // Convenient access to key properties
    public virtual uint Id => Keys.Id;
    
    // Convenient access to value properties
    public virtual uint Price => Values.Price;
    public virtual string Name => Values.Name;
    public virtual string Description => Values.Description;
    public virtual string Owner => Values.Owner;

    // ========== Key Class ==========
    public partial class CatalogueItemKey
    {
        [Parameter("uint32", "id", 1)]
        public virtual uint Id { get; set; }
    }

    // ========== Value Class ==========
    public partial class CatalogueItemValue
    {
        [Parameter("uint32", "price", 1)]
        public virtual uint Price { get; set; }
        
        [Parameter("string", "name", 2)]
        public virtual string Name { get; set; }
        
        [Parameter("string", "description", 3)]
        public virtual string Description { get; set; }
        
        [Parameter("string", "owner", 4)]
        public virtual string Owner { get; set; }          
    }
}
```

**2. TableService - CRUD operations for the table:**

```csharp
// CatalogueItemTableService.cs
public partial class CatalogueItemTableService : TableService<CatalogueItemTableRecord, CatalogueItemTableRecord.CatalogueItemKey, CatalogueItemTableRecord.CatalogueItemValue>
{ 
    public CatalogueItemTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress) {}
    
    // ========== GET by parameters ==========
    public virtual Task<CatalogueItemTableRecord> GetTableRecordAsync(uint id, BlockParameter blockParameter = null)
    {
        var key = new CatalogueItemTableRecord.CatalogueItemKey();
        key.Id = id;
        return GetTableRecordAsync(key, blockParameter);
    }
    
    // ========== SET by parameters ==========
    public virtual Task<string> SetRecordRequestAsync(uint id, uint price, string name, string description, string owner)
    {
        var key = new CatalogueItemTableRecord.CatalogueItemKey();
        key.Id = id;

        var values = new CatalogueItemTableRecord.CatalogueItemValue();
        values.Price = price;
        values.Name = name;
        values.Description = description;
        values.Owner = owner;
        
        return SetRecordRequestAsync(key, values);
    }
    
    // ========== SET with receipt ==========
    public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceiptAsync(
        uint id, uint price, string name, string description, string owner)
    {
        var key = new CatalogueItemTableRecord.CatalogueItemKey();
        key.Id = id;

        var values = new CatalogueItemTableRecord.CatalogueItemValue();
        values.Price = price;
        values.Name = name;
        values.Description = description;
        values.Owner = owner;
        
        return SetRecordRequestAndWaitForReceiptAsync(key, values);
    }
}
```

### Base TableService Methods (Inherited)

The generated TableService inherits these methods from the base class:

```csharp
public class TableService<TTableRecord, TKey, TValue> 
{
    // Register table schema in MUD
    public Task<string> RegisterTableRequestAsync();
    public Task<TransactionReceipt> RegisterTableRequestAndWaitForReceiptAsync();
    
    // Set record using key/value objects
    public Task<string> SetRecordRequestAsync(TKey key, TValue values);
    public Task<TransactionReceipt> SetRecordRequestAndWaitForReceiptAsync(TKey key, TValue values);
    
    // Get record from chain
    public Task<TTableRecord> GetTableRecordAsync(TKey key, BlockParameter blockParameter = null);
    
    // Get all records from logs
    public Task<List<TTableRecord>> GetAllRecordsFromLogsAsync(BlockParameter fromBlock = null, BlockParameter toBlock = null);
    
    // Query from repository
    public Task<IEnumerable<TTableRecord>> GetRecordsFromRepositoryAsync(ITableRepository repository);
}
```

### Partial Classes - Extend with Custom Logic

Both TableRecord and TableService are generated as **partial classes**, allowing you to extend them:

```csharp
// CatalogueItemTableService.Custom.cs - Your custom extensions
public partial class CatalogueItemTableService
{
    // Add custom validation
    public async Task<string> SetRecordWithValidationAsync(uint id, uint price, string name, string description, string owner)
    {
        if (price <= 0)
            throw new ArgumentException("Price must be positive");
            
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Name is required");
            
        return await SetRecordRequestAsync(id, price, name, description, owner);
    }
    
    // Add custom queries
    public async Task<List<CatalogueItemTableRecord>> GetItemsByOwnerAsync(string owner, ITableRepository repository)
    {
        var allRecords = await GetRecordsFromRepositoryAsync(repository);
        return allRecords.Where(r => r.Owner == owner).ToList();
    }
}
```

---

## Generated Project Structure

After code generation, your project structure will look like:

```
MyProject.Contracts/
├── MyWorld/
│   ├── Tables/
│   │   ├── CatalogueItemTableRecord.cs      # Generated from mud.config.ts
│   │   ├── CatalogueItemTableService.cs     # Generated from mud.config.ts
│   │   ├── CounterTableRecord.cs            # Generated from mud.config.ts
│   │   ├── CounterTableService.cs           # Generated from mud.config.ts
│   │   └── MyWorldTableServices.cs          # Container for all tables (manual)
│   │
│   ├── Systems/
│   │   ├── IncrementSystem/
│   │   │   ├── IncrementSystemService.cs           # Standard ContractDefinition
│   │   │   ├── IncrementSystemServiceMudExtended.cs # MUD extensions
│   │   │   ├── ContractDefinition/
│   │   │   │   ├── IncrementFunction.cs
│   │   │   │   ├── IncrementSystemDeployment.cs
│   │   │   │   └── ...
│   │   │   └── ...
│   │   └── MyWorldSystemServices.cs         # Container for all systems (manual)
│   │
│   ├── MyWorldNamespace.cs                  # Namespace container (manual)
│   └── MyWorldNamespaceResource.cs          # Namespace resource (manual)
│
└── .nethereum-gen.multisettings             # Generation configuration
```

---

## Multiple Namespaces

MUD supports multiple namespaces, and so does Nethereum code generation:

```json
[
  {
    "paths": ["mud.config.ts"],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.Contracts.World1.Tables",
        "basePath": "MyProject.Contracts/World1/Tables",
        "generatorType": "MudTables",
        "mudNamespace": "world1"
      }
    ]
  },
  {
    "paths": ["mud.config.ts"],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.Contracts.World2.Tables",
        "basePath": "MyProject.Contracts/World2/Tables",
        "generatorType": "MudTables",
        "mudNamespace": "world2"
      }
    ]
  }
]
```

### Using VSCode Solidity Extension

1. Create a `.nethereum-gen.multisettings` file with your configuration
2. Right-click in VSCode to generate code
3. Different settings for different ABIs or mud.config.ts files

**Note:** When working on Windows, it is easier to install pnpm in Git Bash or WSL2

---

## TableRecord

A TableRecord is mapped directly to the MUD config schema:

```csharp
public partial class ItemTableRecord : TableRecord<ItemTableRecord.ItemKey, ItemTableRecord.ItemValue> 
{
    public ItemTableRecord() : base("MyWorld", "Item")
    {
    }
    
    // Direct access to key properties
    public virtual uint Id => Keys.Id;
    
    // Direct access to value properties
    public virtual uint Price => Values.Price;
    public virtual string Name => Values.Name;
    public virtual string Description => Values.Description;
    public virtual string Owner => Values.Owner;

    public partial class ItemKey
    {
        [Parameter("uint32", "id", 1)]
        public virtual uint Id { get; set; }
    }

    public partial class ItemValue
    {
        [Parameter("uint32", "price", 1)]
        public virtual uint Price { get; set; }
        [Parameter("string", "name", 2)]
        public virtual string Name { get; set; }
        [Parameter("string", "description", 3)]
        public virtual string Description { get; set; }
        [Parameter("string", "owner", 4)]
        public virtual string Owner { get; set; }          
    }
}
```

### Key Features
- Initializes with both table name and namespace
- Separates Keys and Values
- Each property decorated with Parameter attribute (same as Nethereum Function Messages, Events)
- Uses MUD's **packed encoder/decoder** (different from standard ABI encoding)

### MUD Packed Encoding

MUD uses a packed encoding scheme that differs from standard Ethereum ABI encoding:

```csharp
// Standard ABI encoding pads each value to 32 bytes
// MUD packed encoding is more efficient - values are tightly packed

// The Parameter attribute works the same way as standard Nethereum:
[Parameter("uint32", "id", 1)]
public virtual uint Id { get; set; }

// But internally, TableRecord uses PackedEncoder/PackedDecoder
// This is handled automatically by the generated code
```

---

## TableService

The TableService maps the table record, key, and value types to a service:

```csharp
public partial class ItemTableService : TableService<ItemTableRecord, ItemTableRecord.ItemKey, ItemTableRecord.ItemValue>
{ 
    public ItemTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress) {}
    
    public virtual Task<ItemTableRecord> GetTableRecordAsync(uint id, BlockParameter blockParameter = null)
    {
        var _key = new ItemTableRecord.ItemKey();
        _key.Id = id;
        return GetTableRecordAsync(_key, blockParameter);
    }
    
    public virtual Task<string> SetRecordRequestAsync(uint id, uint price, string name, string description, string owner)
    {
        var _key = new ItemTableRecord.ItemKey();
        _key.Id = id;

        var _values = new ItemTableRecord.ItemValue();
        _values.Price = price;
        _values.Name = name;
        _values.Description = description;
        _values.Owner = owner;
        return SetRecordRequestAsync(_key, _values);
    }
    
    public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceiptAsync(uint id, uint price, string name, string description, string owner)
    {
        var _key = new ItemTableRecord.ItemKey();
        _key.Id = id;

        var _values = new ItemTableRecord.ItemValue();
        _values.Price = price;
        _values.Name = name;
        _values.Description = description;
        _values.Owner = owner;
        return SetRecordRequestAndWaitForReceiptAsync(_key, _values);
    }
}
```

### TableService Capabilities

- **Set records** directly onto the table
- **Get records** from the chain by key
- **Get all data** related to a table using logs
- **Register tables** in MUD (so schema is accessible)
- **Query table repositories**, logs, or other providers using the table resource ID
- **Partial class** - can be extended with custom logic

---

## Table Repository and Log Processing

### InMemoryTableRepository

A common abstraction to store all records from the MUD store:

```csharp
var inMemoryStore = new InMemoryTableRepository();
var storeLogProcessingService = new StoreEventsLogProcessingService(web3, WorldAddress);

// Process all store changes
await storeLogProcessingService.ProcessAllStoreChangesAsync(inMemoryStore, null, null, CancellationToken.None);

// Query records
var results = await inMemoryStore.GetTableRecordsAsync<CounterTableRecord>(tableId);
```

### Database Repository (EF/Postgres)

**NuGet Packages:**
- `Nethereum.Mud.Repositories.EF`
- `Nethereum.Mud.Repositories.Postgres`

Features:
- Log indexer using Nethereum Log Processor
- MUD-specific EF context (generic EF)
- Specialized Postgres implementation
- All data stored in `storedrecords` table (similar to MUD out-of-the-box indexer)

```csharp
// Example: Postgres Log Processing
var connection = new NpgsqlConnection("Host=myserver;Username=mylogin;Password=mypass;Database=mydatabase");
var storeRecordsTableRepository = new MudPostgresStoreRecordsTableRepository(connection, logger);

var processingService = new MudPostgresNormaliserProcessingService(
    storeRecordsTableRepository,
    connection,
    logger
)
{
    RpcUrl = "https://localhost:8545",
    Address = "0xYourContractAddress",
    PageSize = 1000
};

await processingService.ExecuteAsync(cancellationToken);
```

### Data Normalization

The Normalizer process creates tables based on the schema and decodes record data:

- Creates table structures based on MUD schemas
- Decodes encoded data into proper columns
- Once running, custom indexes, relationships, and optimizations can be added
- Enables creating reports, custom APIs, and complex queries

---

## TablePredicateBuilder

A fluent API to build predicates for querying MUD storage tables:

```csharp
var predicateBuilder = new TablePredicateBuilder<ItemTableRecord, ItemTableRecord.ItemKey, ItemTableRecord.ItemValue>("0xABC123");

var predicate = predicateBuilder
    .AndEqual(x => x.Id, 1)         // AND key0 = '0x1'
    .AndEqual(x => x.Id, 2)         // AND key0 = '0x2'
    .OrEqual(x => x.Id, 3)          // OR key0 = '0x3'
    .AndNotEqual(x => x.Id, 4)      // AND key0 != '0x4'
    .Expand();                       // Finalize the predicate
```

### Features
- Fluent API with chainable `.AndEqual()`, `.OrEqual()`, `.AndNotEqual()` methods
- Reusable across storage types (PostgreSQL, SQLite, etc.)
- REST API integration - predicates can be serialized to JSON

---

## SystemService

> **Note:** SystemService classes are code-generated. See [Generated Code: Systems](#generated-code-systems-mudextendedservice) for full details on what gets generated.

### Standard Contract Service (Code Generated)

The same contract service created for standard Nethereum integration, extended with MUD functionality:

```csharp
public partial class IncrementSystemService : ISystemService<IncrementSystemServiceResource>
{
    public IResource Resource => this.GetResource();

    public ISystemServiceResourceRegistration SystemServiceResourceRegistrator
    {
        get
        {
            return this.GetSystemServiceResourceRegistration<IncrementSystemServiceResource, IncrementSystemService>();
        }
    }

    public List<FunctionABI> GetSystemFunctionABIs()
    {
        return GetAllFunctionABIs();
    }

    public string CalculateCreate2Address(string deployerAddress, string salt, params ByteCodeLibrary[] byteCodeLibraries)
    {
        return new IncrementSystemDeployment().CalculateCreate2Address(deployerAddress, salt, byteCodeLibraries);
    }

    public Task<Create2ContractDeploymentTransactionResult> DeployCreate2ContractAsync(string deployerAddress, string salt, params ByteCodeLibrary[] byteCodeLibraries)
    {
        var create2ProxyDeployerService = Web3.Eth.Create2DeterministicDeploymentProxyService;
        var accessManagementSystemDeployment = new IncrementSystemDeployment();
        return create2ProxyDeployerService.DeployContractRequestAsync(accessManagementSystemDeployment, deployerAddress, salt, byteCodeLibraries);
    }
}
```

### MUD Extended Features
- System Resource ID identification
- Registered function signatures
- Deployment helpers for Create2
- Helper functions for required registrations

---

## Namespaces

### Namespace Resource

```csharp
public class MudTestNamespaceResource : NamespaceResource
{
    public MudTestNamespaceResource() : base(String.Empty) { }
}

public class MudTestNamespace : NamespaceBase<MudTestNamespaceResource, MudTestSystemServices, MudTestTableServices>
{
    public MudTestNamespace(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
    {
        Tables = new MudTestTableServices(web3, contractAddress);
        Systems = new MudTestSystemServices(web3, contractAddress);
    }
}
```

### Tables Services Container

```csharp
public class MudTestTableServices : TablesServices
{
    public CounterTableService CounterTableService { get; protected set; }
    public ItemTableService ItemTableService { get; protected set; }
    public ConfigTableService ConfigTableService { get; protected set; }

    public MudTestTableServices(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
    {
        CounterTableService = new CounterTableService(web3, contractAddress);
        ItemTableService = new ItemTableService(web3, contractAddress);
        ConfigTableService = new ConfigTableService(web3, contractAddress);

        TableServices = new List<ITableServiceBase> { CounterTableService, ItemTableService, ConfigTableService };
    }
}
```

### Systems Services Container

```csharp
public class MudTestSystemServices : SystemsServices
{
    public IncrementSystemService IncrementSystemService { get; protected set; }
    
    public MudTestSystemServices(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
    {
        IncrementSystemService = new IncrementSystemService(web3, contractAddress);
        SystemServices = new List<ISystemService> { IncrementSystemService };
    }
}
```

### Namespace Functionality

All tables and services belong to a namespace class that matches each namespace in MUD. The namespace provides:

- Deployment of all systems
- Registration of all tables
- Finding typed errors across all systems
- Registration of delegate authority

---

## Local State Management

### Architecture

```csharp
public class LocalState
{
    public InMemoryTableRepository TableRepository { get; }
    public List<SystemCallMulticallInput> UpdateLandOperations { get; }
    // Model objects for changes before saving
}
```

### How It Works

1. **Initial State**: InMemoryTableRepository contains original game state (Land, Inventory, etc.)
2. **Play Phase**: Changes stored in model objects, enabling continuation without automatic saving
3. **Action Tracking**: Each action added to `UpdateLandOperations` for batch submission
4. **Save Phase**: All collected actions executed and re-executed in order

### Game Logic Pattern

```csharp
// When "craft" action is performed:
// 1. Action performed against local state
// 2. Validated using similar logic to smart contracts
// 3. SystemCallMulticallInput created per action
//    - Contains System Resource ID
//    - Contains Nethereum Function Message to execute
```

### Saving/Executing Actions

```csharp
// Periodic or on-demand save:
// 1. Execute all collected actions on chain
// 2. Ensure transactions don't fail (logic must match)
// 3. Sync logs from transaction with InMemoryTableRepository
// 4. Recreate object model with chain-specific state changes
```

### Chain-Specific Changes

Changes that can come from the chain include:
- Logic that prevents cheating/bots
- Placement time
- XP points changes
- Level progress
- Quests completed, rewards

---

## Deployment

### Standard MUD Deployment Flow

1. **Deploy World Factory** and then the World
2. **Register Namespace and Tables** once World address obtained
3. **Register Systems** and function selectors automatically in batch call

```csharp
// As your world evolves:
// - Register or update systems individually
// - Register new tables
// - Add new namespaces with new functionality
```

---

## Testing

### Test Chain Support

Nethereum includes a testing library in all templates and examples:

```csharp
// Launch test chains automatically
// - Geth
// - Hardhat
// - Anvil

// Extension methods supported via Nethereum.RPC.Extensions
```

### Integration Testing Example

```csharp
[Fact]
public async Task ShouldGetAllChanges()
{
    var web3 = GetWeb3();
    var storeLogProcessingService = new StoreEventsLogProcessingService(web3, WorldAddress);
    var inMemoryStore = new InMemoryTableRepository();
    
    await storeLogProcessingService.ProcessAllStoreChangesAsync(inMemoryStore, null, null, CancellationToken.None);
    
    var results = await inMemoryStore.GetTableRecordsAsync<CounterTableRecord>(tableId);
    Assert.True(results.ToList()[0].Values.Value > 0);
}
```

---

## Delegated Authority

For constant saving, temporary accounts are needed (device accounts, session accounts, or burner accounts).

### Why Delegated Authority?

In games and applications, users shouldn't need to sign every transaction with their main wallet. MUD's delegated authority allows:
- **Session keys**: Temporary keys that can act on behalf of the user
- **Burner wallets**: Disposable wallets for gameplay
- **Device wallets**: Wallet per device without exposing main account

### Registration Flow

1. **Register Delegatee Account**: Use namespace world systems
2. **Set Delegator in Namespace**: Configure every system/service to use `MudCallFromContractHandler`
3. **Transaction Wrapping**: Every transaction wrapped with `CallFromFunction`

### MudCallFromContractHandler

When delegated authority is configured, the `MudCallFromContractHandler` automatically wraps all contract calls:

```csharp
// Without delegation - direct call
await incrementSystemService.IncrementRequestAsync();

// With delegation - automatically wrapped in CallFrom
// The handler intercepts the call and wraps it:
// World.callFrom(delegatorAddress, systemResourceId, encodedFunctionData)
```

### Setting Up Delegation

```csharp
// 1. Register the delegatee (burner wallet) with the World
await worldService.RegisterDelegationRequestAsync(burnerAddress, delegationType, initCallData);

// 2. Configure the namespace to use CallFrom
namespace.SetDelegator(delegatorAddress, burnerAccount);

// 3. Now all system calls go through the delegator
// The MudCallFromContractHandler wraps every transaction
await namespace.Systems.IncrementSystemService.IncrementRequestAsync();
// ^ This is now executed as: CallFrom(delegatorAddress, systemId, data)
```

---

## Contract Handler Override

The Contract Handler can be overridden and changed on ContractServices, allowing for specialization:

```csharp
var privateKeySigner = "";
var privatekeySender = "";
var web3 = new Web3(new Nethereum.Web3.Accounts.Account(privateKey), "https://rpc.aboutcircles.com/");
var hubService = new HubService(web3, v2HubAddress);

// Change to Safe execution handler
hubService.ChangeContractHandlerToSafeExecTransaction(humanAddress1, privateKeySigner);
```

### Interception Levels

1. **RPC Level**: For MetaMask, WalletConnect, etc. - redirects messages to another provider
2. **ContractHandler Level**: Sign as usual but embed contract call into another function message

---

## NuGet Packages

| Package | Description |
|---------|-------------|
| `Nethereum.Mud` | Core MUD integration |
| `Nethereum.Mud.Contracts` | MUD contract services |
| `Nethereum.Mud.Repositories.EF` | Entity Framework repository |
| `Nethereum.Mud.Repositories.Postgres` | PostgreSQL repository |
| `Nethereum.RPC.Extensions` | Hardhat/Anvil extensions |

---

## Release History

### Version 5.0.0 (May 2025)
- EIP-7702 support
- Multicall RPC batch support improvements
- .NET AOT Native support
- EVM Simulator updates (BASEFEE, BLOBHASH, BLOBBASEFEE, TLOAD, TSTORE)

### Version 4.28.0 (January 2025)
- Code generator improvements for MUD tables
- Services now inherit from base class with virtual methods
- MUD Table Service extended with Set/Get methods using parameters
- Support for `.nethereum-gen.multisettings` in console

### Version 4.27.0 (December 2024)
- Reown AppKit Blazor support
- .NET 9 target
- MUD Normaliser Postgres dbnull fix

### Version 4.26.0 (October 2024)
- MUD Postgres Normaliser
- EF Stored records retrieval by BlockNumber and RowId
- TableSchema generic object from chain retrieval

### Version 4.25.0 (September 2024)
- TablePredicateBuilder fluent API
- Mud.Repositories.EntityFramework project
- MUD Repository Postgres with EF integration
- REST API Integration examples

### Version 4.21.4 (September 2024)
- Contract Handler override support
- Multiple configuration support in code generator

### Version 4.21.3 (July 2024)
- Multiple Table registration support
- Contract Standards interface support

### Version 4.21.2 (June 2024)
- ERC6492 support
- MUD multiquery RPC support
- Additional table service functionality

### Version 4.21.0 (June 2024)
- **Initial MUD Support**
- Nethereum.Mud and Nethereum.Mud.Contracts packages
- Initial code generation support
- StoreEventsLogProcessingService
- InMemoryTableRepository

---

## Links and Resources

| Resource | URL |
|----------|-----|
| MUD Framework | https://mud.dev/ |
| Nethereum GitHub | https://github.com/Nethereum |
| Nethereum NuGet | https://www.nuget.org/profiles/nethereum |
| Nethereum Discord | https://discord.gg/u3Ej2BReNn |
| Nethereum.Unity | https://github.com/Nethereum/Nethereum.Unity |
| Unity Starter Template | https://github.com/Nethereum/Unity3dSampleTemplate |
| Nethereum Playground | http://playground.nethereum.com |
| VSCode Solidity Extension | https://marketplace.visualstudio.com/items?itemName=JuanBlanco.solidity |
| Cafe Cosmos (Example Game) | https://www.cafecosmos.io/ |

---

## Example Projects

- **Full Integration Tests**: https://github.com/Nethereum/Nethereum/tree/master/tests/Nethereum.Mud.IntegrationTests
- **Project Structure**: https://github.com/Nethereum/Nethereum/tree/master/tests/Nethereum.Mud.IntegrationTests/MudTest
- **Log Processing Example**: https://github.com/Nethereum/Nethereum/tree/master/consoletests/NethereumMudLogProcessing
- **REST API Example**: https://github.com/Nethereum/Nethereum/tree/master/consoletests/NethereumMudStoredRecordsRestApi
- **REST Client Example**: https://github.com/Nethereum/Nethereum/tree/master/consoletests/NethereumMudStoreRestApiClient

---

## Dogfooding

The integration of Nethereum.MUD with **Cafe Cosmos** and early community feedback has allowed dogfooding the implementation, testing and refining it in a real-world scenario.
