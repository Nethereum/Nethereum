---
name: code-generation
description: Generate typed C# contract services, DTOs, Unity requests, MUD tables, and Blazor pages from Solidity ABI using Nethereum code generation. Use this skill when the user asks about generating C# from Solidity, ABI code generation, Foundry/Forge integration, nethereum-gen.multisettings, contract service generation, Unity contract requests, MUD table generation, or Blazor contract pages.
user-invocable: true
---

# Nethereum Code Generation

## Workflow: Solidity → Forge → C# Bindings

```
Solidity source → forge build → JSON artifacts (out/) → Nethereum Generator → C# services + DTOs
```

## CLI Tool

```bash
dotnet tool install -g Nethereum.Generator.Console
```

### Generate from ABI
```bash
Nethereum.Generator.Console generate from-abi \
  -abi ./MyContract.abi -bin ./MyContract.bin \
  -o ./Generated -ns MyProject.Contracts -cn MyContract
```

### Generate from Forge Output (Primary Workflow)
```bash
forge build
Nethereum.Generator.Console generate from-config \
  -cfg .nethereum-gen.multisettings -r .
```

Or use the script:
```powershell
.\scripts\generate-csharp-from-forge.ps1 -Build
```

## Configuration File (.nethereum-gen.multisettings)

Multi-contract config with shared types and struct references:

```json
[
  {
    "paths": ["out/EntryPoint.sol/EntryPoint.json"],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.AccountAbstraction",
        "basePath": "../src/MyProject.AccountAbstraction",
        "codeGenLang": 0,
        "generatorType": "ContractDefinition",
        "referencedTypesNamespaces": ["MyProject.Structs"],
        "structReferencedTypes": ["PackedUserOperation"]
      }
    ]
  },
  {
    "paths": [
      "out/NethereumAccount.sol/NethereumAccount.json",
      "out/NethereumAccountFactory.sol/NethereumAccountFactory.json"
    ],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.Contracts.Core",
        "basePath": "../src/MyProject/Contracts/Core",
        "codeGenLang": 0,
        "generatorType": "ContractDefinition",
        "referencedTypesNamespaces": ["MyProject.Structs"],
        "structReferencedTypes": ["PackedUserOperation"]
      }
    ]
  }
]
```

### Config Options
| Field | Description |
|-------|-------------|
| `paths` | Forge JSON artifact paths |
| `baseNamespace` | C# namespace |
| `basePath` | Output directory |
| `codeGenLang` | `0`=C#, `1`=VB, `3`=F# |
| `generatorType` | `ContractDefinition`, `UnityRequest`, `MudTables`, `MudExtendedService`, `BlazorPageService`, `NetStandardLibrary` |
| `sharedTypesNamespace` | Namespace for shared events/errors/structs |
| `sharedTypes` | `["events", "errors", "structs", "functions"]` |
| `referencedTypesNamespaces` | Import existing struct types instead of regenerating |
| `structReferencedTypes` | Specific struct names to import |
| `mudNamespace` | MUD world namespace |

## Generated Output

```
MyContract/
├── ContractDefinition/
│   └── MyContractDefinition.gen.cs    # Deployment + all DTOs
└── MyContractService.gen.cs           # Service with typed methods
```

### Generated Service (partial — extend without modifying .gen.cs)
```csharp
var service = await MyContractService.DeployContractAndGetServiceAsync(web3, deployment);

// Query (two overloads: raw params or typed message)
var balance = await service.BalanceOfQueryAsync(ownerAddress);
var balance = await service.BalanceOfQueryAsync(
    new BalanceOfFunction { Owner = ownerAddress }, blockParameter);

// Transact
var receipt = await service.TransferRequestAndWaitForReceiptAsync(
    new TransferFunction { To = recipient, Value = amount });

// Events
var events = await service.GetTransferEvent()
    .CreateFilterInput(fromBlock, toBlock)
    .GetAllChangesAsync();
```

## Generator Types

| Type | Output | Use Case |
|------|--------|----------|
| `ContractDefinition` | Service + all DTOs | Standard .NET |
| `UnityRequest` | Coroutine-based requests | Unity engine |
| `MudTables` | Table schemas from mud.config.ts | MUD World |
| `MudExtendedService` | Extended MUD service | MUD systems |
| `BlazorPageService` | Razor component | Blazor apps |
| `NetStandardLibrary` | Complete .csproj | Standalone packages |

Multiple types per contract:
```json
{
  "paths": ["out/ERC20.sol/Standard_Token.json"],
  "generatorConfigs": [
    { "generatorType": "ContractDefinition", ... },
    { "generatorType": "UnityRequest", ... },
    { "generatorType": "BlazorPageService", ... }
  ]
}
```

## Shared Types

For shared events/errors/structs across contracts:
```json
{
  "sharedTypesNamespace": "MyProject.SharedTypes",
  "sharedTypes": ["events", "errors", "structs"]
}
```

For referencing existing structs from another project:
```json
{
  "referencedTypesNamespaces": ["MyProject.AccountAbstraction.Structs"],
  "structReferencedTypes": ["PackedUserOperation"]
}
```

## MUD Generation

Tables from mud.config.ts:
```json
{ "generatorType": "MudTables", "mudNamespace": "MyWorld" }
```

Systems (both definition + extended service):
```json
[
  { "generatorType": "ContractDefinition", "mudNamespace": "MyWorld" },
  { "generatorType": "MudExtendedService", "mudNamespace": "MyWorld" }
]
```

## Node.js API

```bash
npm install nethereum-codegen
```

```javascript
var codegen = require('nethereum-codegen');
codegen.generateAllClasses(abi, bytecode, "ERC20", "MyProject", "SharedTypes", ["events"], "./out", 0);
codegen.generateFilesFromConfigJsonFile("./config.json", "./contracts");
codegen.generateUnityRequests(abi, bytecode, "ERC20", "MyProject", "SharedTypes", ["events"], "./out");
codegen.generateMudTables(mudJson, "MyProject.Tables", "Tables", "./out", 0, "MyWorld");
codegen.generateBlazorPageService(abi, "ERC20", "MyProject", "./out", "SharedTypes", 0);
```

## MSBuild Integration

```bash
dotnet add package Nethereum.Autogen.ContractApi
```

Place `.abi` + `.bin` in project → code generated on `dotnet build`.

## Key Rules

- **Generated files use `.gen.cs` suffix** — never edit them manually
- **All classes are `partial`** — extend in separate files
- **Use `referencedTypesNamespaces`** when structs are shared across contracts
- **Use `sharedTypes`** when events/errors repeat across contracts in the same generation run
- **The config file is the source of truth** for how Solidity maps to C# namespaces
- **VS Code Solidity extension** handles compilation + generation in one step for single contracts
