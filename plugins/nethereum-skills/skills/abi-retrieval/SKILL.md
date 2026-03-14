---
name: abi-retrieval
description: Fetch contract ABIs from Sourcify, Etherscan, and 4Byte Directory using the composite ABIInfoStorage pattern (.NET/C#). Use this skill when the user asks about ABI retrieval, contract verification, function signature lookup, calldata decoding, or Sourcify/Etherscan API usage.
user-invocable: true
---

# ABI Retrieval & Contract Verification

Nethereum.DataServices provides a composite ABI lookup system that chains multiple sources with automatic fallback: Sourcify → Etherscan → 4Byte Directory.

NuGet: `Nethereum.DataServices`

```bash
dotnet add package Nethereum.DataServices
```

## Composite ABI Lookup (Recommended)

Use `ABIInfoStorageFactory` to create a fallback chain of ABI sources:

```csharp
using Nethereum.DataServices.ABIInfoStorage;

// Full chain: cache → Sourcify → Etherscan → Sourcify4Byte → FourByte
var storage = ABIInfoStorageFactory.CreateDefault(etherscanApiKey: "YOUR_KEY");

// Get complete ABI
var abiInfo = await storage.GetABIInfoAsync(chainId: 1, contractAddress);
Console.WriteLine($"Contract: {abiInfo.ContractName}, Functions: {abiInfo.FunctionABIs.Count}");
```

### Factory Presets

| Method | Sources |
|--------|---------|
| `CreateDefault(etherscanApiKey, cache)` | Cache → Sourcify → Etherscan → Sourcify4Byte → FourByte |
| `CreateWithSourcifyOnly(cache)` | Cache → Sourcify → Sourcify4Byte → FourByte |
| `CreateWithEtherscanOnly(apiKey, cache)` | Cache → Etherscan → Sourcify4Byte → FourByte |
| `CreateLocalOnly(cache)` | Cache only |
| `CreateCustom(cache, storages...)` | Custom fallback chain |

### Custom Chain

```csharp
var storage = ABIInfoStorageFactory.CreateCustom(
    cache: new ABIInfoInMemoryStorage(),
    new SourcifyABIInfoStorage(),
    new EtherscanABIInfoStorage("YOUR_KEY"),
    new FourByteDirectoryABIInfoStorage());
```

## Find Functions and Events

```csharp
// By 4-byte selector from input data
var func = await storage.FindFunctionABIFromInputDataAsync(chainId, contractAddress, inputData);

// By topic hash
var evt = await storage.FindEventABIAsync(chainId, contractAddress, topicHash);

// By error selector
var err = await storage.FindErrorABIAsync(chainId, contractAddress, errorSelector);
```

### Batch Lookups

```csharp
var functions = await storage.FindFunctionABIsBatchAsync(
    new[] { "0xa9059cbb", "0x095ea7b3", "0x23b872dd" });

var events = await storage.FindEventABIsBatchAsync(
    new[] { "0xddf252ad...", "0x8c5be1e5..." });

var combined = await storage.FindABIsBatchAsync(functionSignatures, eventSignatures);
```

## Sourcify V2 Direct

```csharp
using Nethereum.DataServices.Sourcify;

var sourcify = new SourcifyApiServiceV2();

// Get contract (ABI, sources, compilation, proxy resolution)
var contract = await sourcify.GetContractAsync(chainId: 1, contractAddress);

// Just ABI
string abi = await sourcify.GetContractAbiAsync(chainId: 1, contractAddress);

// Verify from Etherscan
var result = await sourcify.VerifyFromEtherscanAsync(chainId, address, etherscanApiKey);

// Verify with source files
var verifyResult = await sourcify.PostVerifyAsync(chainId, address, sourceFiles);
```

### Proxy Resolution

`SourcifyABIInfoStorage` automatically resolves proxy contracts:

```csharp
var sourcifyStorage = new SourcifyABIInfoStorage(); // resolveProxies: true
var abiInfo = await sourcifyStorage.GetABIInfoAsync(chainId, proxyAddress);
// Returns implementation ABI
```

## Etherscan API

```csharp
using Nethereum.DataServices.Etherscan;

var etherscan = new EtherscanApiService(chain: 1, apiKey: "YOUR_KEY");

var abiResponse = await etherscan.Contracts.GetAbiAsync(contractAddress);
var sourceResponse = await etherscan.Contracts.GetSourceCodeAsync(contractAddress);
```

## 4Byte Signature Lookup

### FourByteDirectoryService

```csharp
using Nethereum.DataServices.FourByteDirectory;

var fourByte = new FourByteDirectoryService();
var result = await fourByte.GetFunctionSignatureByHexSignatureAsync("0xa9059cbb");
// result.Results[0].TextSignature → "transfer(address,uint256)"
```

### Sourcify4ByteSignatureService

```csharp
using Nethereum.DataServices.Sourcify;

var sig4byte = new Sourcify4ByteSignatureService();

// Batch lookup
var result = await sig4byte.LookupAsync(
    functionSignatures: new[] { "0xa9059cbb", "0x095ea7b3" },
    eventSignatures: new[] { "0xddf252ad..." });
```

## EVM Trace Decoding

```csharp
using Nethereum.DataServices.ABIInfoStorage;

var decoded = program.DecodeWithSourcify(callInput, chainId: 1);
var decoded2 = program.Decode(callInput, chainId, etherscanApiKey: "YOUR_KEY");
var decoded3 = programResult.DecodeWithStorage(trace, callInput, chainId, storage);
```

## Local Sourcify Database

For local storage of Sourcify data, use `Nethereum.Sourcify.Database` (EF Core + PostgreSQL):

```csharp
using Nethereum.Sourcify.Database;

services.AddDbContext<SourcifyDbContext>(options => options.UseNpgsql(connectionString));
services.AddScoped<ISourcifyRepository, EFCoreSourcifyRepository>();

// Query
var sig = await repo.GetSignatureByHash4Async(selectorBytes);
var verified = await repo.GetVerifiedContractAsync(chainId, addressBytes);
```

## Key Namespaces

| Namespace | Purpose |
|-----------|---------|
| `Nethereum.DataServices.ABIInfoStorage` | Composite ABI lookup factory and implementations |
| `Nethereum.DataServices.Sourcify` | Sourcify V1/V2 API, Parquet exports, 4byte signatures |
| `Nethereum.DataServices.Etherscan` | Etherscan V2 unified API |
| `Nethereum.DataServices.FourByteDirectory` | 4byte.directory API |
| `Nethereum.DataServices.Chainlist` | Chainlist RPC discovery |

## Documentation

- [ABI Retrieval Guide](https://nethereum.readthedocs.io/en/latest/data-services/guide-abi-retrieval/)
- [Nethereum.DataServices Reference](https://nethereum.readthedocs.io/en/latest/data-services/nethereum-dataservices/)
- [Nethereum.Sourcify.Database Reference](https://nethereum.readthedocs.io/en/latest/data-services/nethereum-sourcify-database/)
