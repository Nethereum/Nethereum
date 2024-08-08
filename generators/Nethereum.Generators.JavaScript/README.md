# nethereum-codegen
The Nethereum code generator provides a simple way to generate the different messages and DTOs classes to integrate with Ethereum smart contracts using Nethereum.

# Nethereum Code Generation

The nethereum code generator provides different ways to to generate code to interact with smart contracts using Nethereum, including standard class libraries, Unity requests, MUD tables, and services.

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
  - [generateFilesFromConfigJsonString](#generatefilesfromconfigjsonstring)
  - [GeneratorType Enum](#generatortype-enum)
  - [GeneratorConfig Interface](#generatorconfig-interface)
  - [GeneratorSetConfig Interface](#generatorsetconfig-interface)

## Installation

To install the module, use npm or yarn:

```bash
npm install nethereum-codegen
```

or

```bash
yarn add nethereum-codegen
```

## Usage

### Basic Example

```javascript
var codegen = require('nethereum-codegen');

var contractByteCode = "0x00";
var abierc20 = '[{"inputs":[],"stateMutability":"nonpayable","type":"constructor"},{"inputs":[],"name":"ECDSAInvalidSignature","type":"error"},...]';  // truncated for brevity
var abi = abierc20;
var contractName = "ERC20";
var baseNamespace = "Nethereum.Unity.Contracts.Standards";
var basePath = "codeGenNodeTest";

var mudTables = '{"tables":{"Counter":{"schema":{"value":"uint32"},"key":[]},"Item":{"schema":{"id":"uint32","price":"uint32","name":"string","description":"string","owner":"string"},"key":["id"]}}}';

var projectName = "MyProject";

// Generating a .NET Standard class library
codegen.generateNetStandardClassLibrary(projectName, basePath, 0);

// Generating all classes
codegen.generateAllClasses(abi, contractByteCode, contractName, baseNamespace, basePath, 0);

// Generating Unity requests
codegen.generateUnityRequests(abi, contractByteCode, contractName, baseNamespace, basePath);

// Generating MUD tables
codegen.generateMudTables(mudTables, baseNamespace, "Tables", basePath, 0, "");

// Generating MUD service
codegen.generateMudService(abi, contractByteCode, contractName, baseNamespace, basePath, 0, "");
```


### Generating Files from Configuration File

You can generate files using a JSON configuration file or string:

#### Using a JSON Configuration String

```javascript
var jsonGeneratorSetsExample1 = 
`[
    {
        "paths": ["out/ERC20.sol/Standard_Token.json"],
        "generatorConfigs": [
            {
                "baseNamespace": "MyProject.Contracts",
                "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts",
                "codeGenLang": 0,
                "generatorType": "ContractDefinition"
            },
            {
                "baseNamespace": "MyProject.Contracts",
                "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts",
                "codeGenLang": 0,
                "generatorType": "UnityRequest"
            }
        ]
    },
    {
        "paths": ["out/IncrementSystem.sol/IncrementSystem.json"],
        "generatorConfigs": [
            {
                "baseNamespace": "MyProject.Contracts.MyWorld.Systems",
                "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts.MyWorld.Systems",
                "codeGenLang": 0,
                "generatorType": "ContractDefinition"
            },
            {
                "baseNamespace": "MyProject.Contracts.MyWorld.Systems",
                "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts.MyWorld.Systems",
                "codeGenLang": 0,
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
                "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts.MyWorld.Tables",
                "generatorType": "MudTables",
                "mudNamespace": "MyWorld"
            }
        ]
    }
]`;

codegen.generateFilesFromConfigJsonString(jsonGeneratorSetsExample1, "examples/testAbi");
```

#### Using a JSON Configuration File

```javascript
codegen.generateFilesFromConfigJsonFile("path/to/config.json", "examples/testAbi");
```

## Configuration Examples

### JSON Configuration Example 1

```json
[
    {
        "paths": ["out/ERC20.sol/Standard_Token.json"],
        "generatorConfigs": [
            {
                "baseNamespace": "MyProject.Contracts",
                "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts",
                "codeGenLang": 0,
                "generatorType": "ContractDefinition"
            },
            {
                "baseNamespace": "MyProject.Contracts",
                "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts",
                "codeGenLang": 0,
                "generatorType": "UnityRequest"
            }
        ]
    },
    {
        "paths": ["out/IncrementSystem.sol/IncrementSystem.json"],
        "generatorConfigs": [
            {
                "baseNamespace": "MyProject.Contracts.MyWorld.Systems",
                "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts.MyWorld.Systems",
                "codeGenLang": 0,
                "generatorType": "ContractDefinition"
            },
            {
                "baseNamespace": "MyProject.Contracts.MyWorld.Systems",
                "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts.MyWorld.Systems",
                "codeGenLang": 0,
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
                "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts.MyWorld.Tables",
                "generatorType": "MudTables",
                "mudNamespace": "MyWorld"
            }
        ]
    }
]
```

### JSON Configuration Example 2

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
                "basePath": "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts.MyWorld1.Systems",
                "codeGenLang": 0,
                "generatorType": "ContractDefinition"
            },
            {
                "baseNamespace": "MyProject.Contracts.MyWorld1.Systems",
                "basePath": "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts.MyWorld1.Systems",
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
                "basePath": "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts.MyWorld1.Tables",
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
                "basePath": "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts.MyWorld2.Tables",
                "generatorType": "MudTables",
                "mudNamespace": "myworld2"
            }
        ]
    }
]
```

## API

### generateNetStandardClassLibrary

Generates a .NET Standard class library.

#### Parameters
- `projectName` (string): The name of the project.
- `basePath` (string): The base path where the project will be generated.
- `codeLang` (number): The programming language (0 for C#, 1 for VB, 3 for F#).

#### Example

```javascript
codegen.generateNetStandardClassLibrary("MyProject", "codeGenNodeTest", 0);
```

### generateAllClasses

Generates all classes for a smart contract.

#### Parameters
- `abi` (string): The ABI of the contract.
- `byteCode` (string): The bytecode of the contract.
- `contractName` (string): The name of the contract.
- `baseNamespace` (string): The base namespace for the generated code.
- `basePath` (string): The base path where the code will be generated.
- `codeGenLang` (number): The programming language (0 for C#, 1 for VB, 3 for F#).

#### Example

```javascript
codegen.generateAllClasses(abi, contractByteCode, "ERC20", "Nethereum.Unity.Contracts.Standards", "codeGenNodeTest", 0);
```

### generateMudService

Generates a MUD service for a smart contract.

#### Parameters
- `abi` (string): The ABI of the contract.
- `byteCode` (string): The bytecode of the contract.
- `contractName` (string): The name of the contract.
- `baseNamespace` (string): The base namespace for the generated code.
- `basePath` (string): The base path where the code will be generated.
- `codeGenLang` (number): The programming language (0 for C#, 1 for VB, 3 for F#).
- `mudNamespace` (string): The MUD namespace.

#### Example

```javascript
codegen.generateMudService(abi, contractByteCode, "ERC20", "Nethereum.Unity.Contracts.Standards", "codeGenNodeTest", 0, "");
```

### generateMudTables

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

### generateUnityRequests

Generates Unity requests for a smart contract.

#### Parameters
- `abi` (string): The ABI of the contract.
- `byteCode` (string): The bytecode of the contract.
- `contractName` (string): The name of the contract.
- `baseNamespace` (string): The base namespace for the generated code.
- `basePath` (string): The base path where the code will be generated.

#### Example

```javascript
codegen.generateUnityRequests(abi, contractByteCode, "ERC20", "Nethereum.Unity.Contracts.Standards", "codeGenNodeTest");
```

### generateFilesFromConfigJsonString

Generates files from a JSON configuration string.

#### Parameters
- `configJson` (string): The JSON configuration string.
- `rootPath` (string): The root path for the configurations.

#### Example

```javascript
var jsonConfig = '[...]';  // your JSON configuration
codegen.generateFilesFromConfigJsonString(jsonConfig, "examples/testAbi");
```

### GeneratorType Enum

Enum for specifying the generator type.

```typescript
export enum GeneratorType {
    ContractDefinition = "ContractDefinition",
    UnityRequest = "UnityRequest",
    MudExtendedService = "MudExtendedService",
    MudTables = "MudTables",
    NetStandardLibrary = "NetStandardLibrary"
}
```

### GeneratorConfig Interface

Interface for a generator configuration.

```typescript
export interface GeneratorConfig {
    baseNamespace: string;
    codeGenLang: number;
    basePath: string;
    generatorType: GeneratorType;
    mudNamespace: string;
}
```

### GeneratorSetConfig Interface

Interface for a generator set configuration.

```typescript
export interface GeneratorSetConfig {
    paths: string[];
    default: boolean;
    generatorConfigs: GeneratorConfig[];
}
```


