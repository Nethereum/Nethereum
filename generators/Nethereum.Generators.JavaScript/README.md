
# nethereum-codegen
The Nethereum code generator provides a simple way to generate messages, DTOs, and services to integrate with Ethereum smart contracts using Nethereum.

# Nethereum Code Generation
The `nethereum-codegen` module generates code for interacting with smart contracts, including .NET Standard class libraries, Unity requests, MUD tables and services, and Blazor page services. It supports shared types for reusable events and errors across contracts.

## Table of Contents
- [Installation](#installation)
- [Usage](#usage)
  - [Basic Example](#basic-example)
  - [Generating Files from Configuration](#generating-files-from-configuration)
- [Configuration Examples](#configuration-examples)
- [API](#api)
  - [generateNetStandardClassLibrary](#generatenetstandardclasslibrary)
  - [generateAllClasses](#generateallclasses)
  - [generateMudService](#generatemudservice)
  - [generateMudTables](#generatemudtables)
  - [generateUnityRequests](#generateunityrequests)
  - [generateBlazorPageService](#generateblazorpageservice)
  - [generateFilesFromConfigJsonString](#generatefilesfromconfigjsonstring)
  - [GeneratorType Enum](#generatortype-enum)
  - [GeneratorConfig Interface](#generatorconfig-interface)
  - [GeneratorSetConfig Interface](#generatorsetconfig-interface)

## Installation <a id="installation"></a>
Install the module using npm or yarn:

```bash
npm install nethereum-codegen
```

or
```bash
yarn add nethereum-codegen
```

## Usage <a id="usage"></a>
### Basic Example <a id="basic-example"></a>
```javascript
var codegen = require('nethereum-codegen');

var contractByteCode = "0x00";
var abierc20 = '[{"inputs":[],"stateMutability":"nonpayable","type":"constructor"},{"inputs":[],"name":"ECDSAInvalidSignature","type":"error"},...]'; // truncated for brevity
var abi = abierc20;
var contractName = "ERC20";
var baseNamespace = "Nethereum.Unity.Contracts.Standards";
var sharedTypesNamespace = "SharedTypes";
var sharedTypes = ["events", "errors"];
var basePath = "codeGenNodeTest";
var mudTables = '{"tables":{"Counter":{"schema":{"value":"uint32"},"key":[]},"Item":{"schema":{"id":"uint32","price":"uint32","name":"string","description":"string","owner":"string"},"key":["id"]}}}';
var projectName = "MyProject";

// Generating a .NET Standard class library
codegen.generateNetStandardClassLibrary(projectName, basePath, 0);

// Generating all classes with shared types
codegen.generateAllClasses(abi, contractByteCode, contractName, baseNamespace, sharedTypesNamespace, sharedTypes, basePath, 0);

// Generating Unity requests
codegen.generateUnityRequests(abi, contractByteCode, contractName, baseNamespace, sharedTypesNamespace, sharedTypes, basePath);

// Generating MUD tables
codegen.generateMudTables(mudTables, baseNamespace, "Tables", basePath, 0, "");

// Generating MUD service
codegen.generateMudService(abi, contractByteCode, contractName, baseNamespace, basePath, sharedTypesNamespace, sharedTypes, 0, "");

// Generating Blazor page service
codegen.generateBlazorPageService(abi, contractName, baseNamespace, basePath, sharedTypesNamespace, 0);
```

### Generating Files from Configuration <a id="generating-files-from-configuration"></a>
Generate files using a JSON configuration file or string:

#### Using a JSON Configuration String <a id="using-a-json-configuration-string"></a>
```javascript
var jsonGeneratorSetsExample1 = `[
  {
    "paths": ["out/ERC20.sol/Standard_Token.json"],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.Contracts",
        "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts",
        "codeGenLang": 0,
        "sharedTypesNamespace": "SharedTypes",
        "sharedTypes": ["events", "errors"],
        "generatorType": "ContractDefinition"
      },
      {
        "baseNamespace": "MyProject.Contracts",
        "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Blazor",
        "codeGenLang": 0,
        "sharedTypesNamespace": "SharedTypes",
        "generatorType": "BlazorPageService"
      },
      {
        "baseNamespace": "MyProject.Contracts",
        "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts",
        "codeGenLang": 0,
        "sharedTypesNamespace": "SharedTypes",
        "sharedTypes": ["events", "errors"],
        "generatorType": "UnityRequest"
      }
    ]
  },
  {
    "paths": ["out/IncrementSystem.sol/IncrementSystem.json"],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.Contracts.MyWorld.Systems",
        "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts/MyWorld/Systems",
        "codeGenLang": 0,
        "sharedTypesNamespace": "SharedTypes",
        "sharedTypes": ["events", "errors"],
        "generatorType": "ContractDefinition",
        "mudNamespace": "MyWorld"
      },
      {
        "baseNamespace": "MyProject.Contracts.MyWorld.Systems",
        "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts/MyWorld/Systems",
        "codeGenLang": 0,
        "sharedTypesNamespace": "SharedTypes",
        "sharedTypes": ["events", "errors"],
        "generatorType": "MudExtendedService",
        "mudNamespace": "MyWorld"
      }
    ]
  },
  {
    "paths": ["mudSingleNamespace/mud.config.ts"],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.Contracts.MyWorld.Tables",
        "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts/MyWorld/Tables",
        "generatorType": "MudTables",
        "mudNamespace": "MyWorld"
      }
    ]
  }
]`;

codegen.generateFilesFromConfigJsonString(jsonGeneratorSetsExample1, "examples/testAbi");
```

#### Using a JSON Configuration File <a id="using-a-json-configuration-file"></a>
```javascript
codegen.generateFilesFromConfigJsonFile("path/to/config.json", "examples/testAbi");
```

## Configuration Examples <a id="configuration-examples"></a>
### JSON Configuration Example 1 <a id="json-configuration-example-1"></a>
```json
[
  {
    "paths": ["out/ERC20.sol/Standard_Token.json"],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.Contracts",
        "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts",
        "codeGenLang": 0,
        "sharedTypesNamespace": "SharedTypes",
        "sharedTypes": ["events", "errors"],
        "generatorType": "ContractDefinition"
      },
      {
        "baseNamespace": "MyProject.Contracts",
        "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Blazor",
        "codeGenLang": 0,
        "sharedTypesNamespace": "SharedTypes",
        "sharedTypes": ["events", "errors"],
        "generatorType": "BlazorPageService"
      },
      {
        "baseNamespace": "MyProject.Contracts",
        "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts",
        "codeGenLang": 0,
        "sharedTypesNamespace": "SharedTypes",
        "sharedTypes": ["events", "errors"],
        "generatorType": "UnityRequest"
      }
    ]
  },
  {
    "paths": ["out/IncrementSystem.sol/IncrementSystem.json"],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.Contracts.MyWorld.Systems",
        "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts/MyWorld/Systems",
        "codeGenLang": 0,
        "sharedTypesNamespace": "SharedTypes",
        "sharedTypes": ["events", "errors"],
        "generatorType": "ContractDefinition",
        "mudNamespace": "MyWorld"
      },
      {
        "baseNamespace": "MyProject.Contracts.MyWorld.Systems",
        "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts/MyWorld/Systems",
        "codeGenLang": 0,
        "sharedTypesNamespace": "SharedTypes",
        "sharedTypes": ["events", "errors"],
        "generatorType": "MudExtendedService",
        "mudNamespace": "MyWorld"
      }
    ]
  },
  {
    "paths": ["mudSingleNamespace/mud.config.ts"],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.Contracts.MyWorld.Tables",
        "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts/MyWorld/Tables",
        "generatorType": "MudTables",
        "mudNamespace": "MyWorld"
      }
    ]
  }
]
```

### JSON Configuration Example 2 <a id="json-configuration-example-2"></a>
```json
[
  {
    "paths": ["out/ERC20.sol/Standard_Token.json"],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.Contracts",
        "basePath": "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts",
        "codeGenLang": 0,
        "sharedTypesNamespace": "SharedTypes",
        "sharedTypes": ["events", "errors"],
        "generatorType": "ContractDefinition"
      },
      {
        "baseNamespace": "MyProject.Contracts",
        "basePath": "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts",
        "codeGenLang": 0,
        "sharedTypesNamespace": "SharedTypes",
        "sharedTypes": ["events", "errors"],
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
        "sharedTypesNamespace": "SharedTypes",
        "sharedTypes": ["events", "errors"],
        "generatorType": "ContractDefinition",
        "mudNamespace": "myworld1"
      },
      {
        "baseNamespace": "MyProject.Contracts.MyWorld1.Systems",
        "basePath": "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts/MyWorld1/Systems",
        "codeGenLang": 0,
        "sharedTypesNamespace": "SharedTypes",
        "sharedTypes": ["events", "errors"],
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
  },
  {
    "paths": ["mudMultipleNamespace/mud.config.ts"],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.Contracts.MyWorld2.Tables",
        "basePath": "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts/MyWorld2/Tables",
        "generatorType": "MudTables",
        "mudNamespace": "myworld2"
      }
    ]
  }
]
```

## API <a id="api"></a>
### generateNetStandardClassLibrary <a id="generatenetstandardclasslibrary"></a>
Generates a .NET Standard class library.

#### Parameters
- `projectName` (string): The name of the project.
- `basePath` (string): The base path where the project will be generated.
- `codeLang` (number): The programming language (0 for C#, 1 for VB, 3 for F#).

#### Example
```javascript
codegen.generateNetStandardClassLibrary("MyProject", "codeGenNodeTest", 0);
```

### generateAllClasses <a id="generateallclasses"></a>
Generates all classes for a smart contract, supporting shared types.

#### Parameters
- `abi` (string): The ABI of the contract.
- `byteCode` (string): The bytecode of the contract.
- `contractName` (string): The name of the contract.
- `baseNamespace` (string): The base namespace for the generated code.
- `sharedTypesNamespace` (string): The namespace for shared types (e.g., events, errors).
- `sharedTypes` (string[]): List of shared types to include (e.g., `["events", "errors"]`).
- `basePath` (string): The base path where the code will be generated.
- `codeGenLang` (number): The programming language (0 for C#, 1 for VB, 3 for F#).
- `mudNamespace` (string, optional): The MUD namespace.

#### Example
```javascript
codegen.generateAllClasses(abi, contractByteCode, "ERC20", "Nethereum.Unity.Contracts.Standards", "SharedTypes", ["events", "errors"], "codeGenNodeTest", 0);
```

### generateMudService <a id="generatemudservice"></a>
Generates a MUD service for a smart contract, supporting shared types.

#### Parameters
- `abi` (string): The ABI of the contract.
- `byteCode` (string): The bytecode of the contract.
- `contractName` (string): The name of the contract.
- `baseNamespace` (string): The base namespace for the generated code.
- `basePath` (string): The base path where the code will be generated.
- `sharedTypesNamespace` (string): The namespace for shared types.
- `sharedTypes` (string[]): List of shared types to include.
- `codeGenLang` (number): The programming language (0 for C#, 1 for VB, 3 for F#).
- `mudNamespace` (string): The MUD namespace.

#### Example
```javascript
codegen.generateMudService(abi, contractByteCode, "ERC20", "Nethereum.Unity.Contracts.Standards", "codeGenNodeTest", "SharedTypes", ["events", "errors"], 0, "");
```

### generateMudTables <a id="generatemudtables"></a>
Generates MUD tables from a JSON configuration.

#### Parameters
- `json` (string): The JSON configuration for the tables.
- `baseNamespace` (string): The base namespace for the generated code.
- `namespace` (string): The namespace for the tables.
- `basePath` (string): The base path where the code will be generated.
- `codeGenLang` (number): The programming language (0 for C#, 1 for VB, 3 for F#).
- `mudNamespace` (string): The MUD namespace.

#### Example
```javascript
var mudTables = '{"tables":{"Counter":{"schema":{"value":"uint32"},"key":[]},"Item":{"schema":{"id":"uint32","price":"uint32","name":"string","description":"string","owner":"string"},"key":["id"]}}}';
codegen.generateMudTables(mudTables, "Nethereum.Unity.Contracts.Standards", "Tables", "codeGenNodeTest", 0, "");
```

### generateUnityRequests <a id="generateunityrequests"></a>
Generates Unity requests for a smart contract, supporting shared types.

#### Parameters
- `abi` (string): The ABI of the contract.
- `byteCode` (string): The bytecode of the contract.
- `contractName` (string): The name of the contract.
- `baseNamespace` (string): The base namespace for the generated code.
- `sharedTypesNamespace` (string): The namespace for shared types.
- `sharedTypes` (string[]): List of shared types to include.
- `basePath` (string): The base path where the code will be generated.

#### Example
```javascript
codegen.generateUnityRequests(abi, contractByteCode, "ERC20", "Nethereum.Unity.Contracts.Standards", "SharedTypes", ["events", "errors"], "codeGenNodeTest");
```

### generateBlazorPageService <a id="generateblazorpageservice"></a>
Generates a Blazor page service for a smart contract, supporting shared types.

#### Parameters
- `abi` (string): The ABI of the contract.
- `contractName` (string): The name of the contract.
- `baseNamespace` (string): The base namespace for the generated code.
- `basePath` (string): The base path where the code will be generated.
- `sharedTypesNamespace` (string): The namespace for shared types.
- `codeGenLang` (number): The programming language (0 for C#) It will default / to c# razor.

#### Example
```javascript
codegen.generateBlazorPageService(abi, "ERC20", "Nethereum.Unity.Contracts.Standards", "codeGenNodeTest", "SharedTypes", 0);
```

### generateFilesFromConfigJsonString <a id="generatefilesfromconfigjsonstring"></a>
Generates files from a JSON configuration string.

#### Parameters
- `configJson` (string): The JSON configuration string.
- `rootPath` (string): The root path for the configurations.

#### Example
```javascript
var jsonConfig = '[...]'; // your JSON configuration
codegen.generateFilesFromConfigJsonString(jsonConfig, "examples/testAbi");
```

### GeneratorType Enum <a id="generatortype-enum"></a>
Enum for specifying the generator type.
```typescript
export enum GeneratorType {
  ContractDefinition = "ContractDefinition",
  UnityRequest = "UnityRequest",
  MudExtendedService = "MudExtendedService",
  MudTables = "MudTables",
  NetStandardLibrary = "NetStandardLibrary",
  BlazorPageService = "BlazorPageService"
}
```

### GeneratorConfig Interface <a id="generatorconfig-interface"></a>
Interface for a generator configuration, including shared types support.
```typescript
export interface GeneratorConfig {
  baseNamespace: string;
  codeGenLang: number;
  basePath: string;
  sharedTypesNamespace: string;
  sharedTypes: string[];
  generatorType: GeneratorType;
  mudNamespace: string;
}
```

### GeneratorSetConfig Interface <a id="generatorsetconfig-interface"></a>
Interface for a generator set configuration.
```typescript
export interface GeneratorSetConfig {
  paths: string[];
  default: boolean;
  generatorConfigs: GeneratorConfig[];
}
```
