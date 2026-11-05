# Nethereum.ABI.ABIInfoSqliteStorage

**SQLite-backed `IABIInfoStorage` for caching contract ABIs and looking up function, event and error signatures offline**

[![NuGet](https://img.shields.io/nuget/v/Nethereum.ABI.ABIInfoSqliteStorage.svg)](https://www.nuget.org/packages/Nethereum.ABI.ABIInfoSqliteStorage/)

## Overview

`Nethereum.ABI.ABIInfoSqliteStorage` contains one class, `SqliteABIInfoStorage`, an implementation of `Nethereum.ABI.ABIRepository.IABIInfoStorage` that persists `ABIInfo` records in a SQLite database (through `Microsoft.Data.Sqlite`). Use it when an application decodes transactions, logs or errors by contract address and wants the ABIs to survive restarts, or to work offline, instead of keeping them only in memory (`ABIInfoInMemoryStorage`) or in files (`FileSystemABIInfoStorage`), both of which live in `Nethereum.ABI`.

Every stored `ABIInfo` is also held in an in-memory `ABIInfoInMemoryStorage` that the class reads first; the database is consulted when the in-memory copy has no entry, and the record read from the database is then added to the in-memory copy.

## Installation

```bash
dotnet add package Nethereum.ABI.ABIInfoSqliteStorage
```

Targets: `netstandard2.0`, `net6.0`, `net8.0`, `net9.0`.

## Dependencies

- **Nethereum.ABI** - `IABIInfoStorage`, `ABIInfo`, `FunctionABI`, `EventABI`, `ErrorABI`
- **Microsoft.Data.Sqlite** 8.0.0

## Creating a storage

```csharp
public SqliteABIInfoStorage(string connectionString)
public SqliteABIInfoStorage(string databasePath, bool createIfNotExists = true)
public static SqliteABIInfoStorage CreateInMemory()
```

- The `connectionString` constructor opens the connection and creates the schema if it does not exist.
- The `databasePath` constructor calls the first one with `Data Source={databasePath}`. Its `createIfNotExists` parameter is accepted but ignored (the constructor forwards only `databasePath`); SQLite creates the file when the connection opens. Calling the constructor with a single string argument binds to the connection-string overload, so pass a full connection string (`Data Source=...`) for a single argument, or supply both arguments to use the path overload.
- `CreateInMemory()` uses `Data Source=:memory:`.
- The class implements `IDisposable`; `Dispose()` closes the connection.

The schema has three tables: `ABIInfo` (unique per `ChainId` and `ContractAddress`), `FunctionSignature` and `EventSignature`. Adding an `ABIInfo` upserts its row and indexes the signatures of its functions and events.

## Store and look up a contract ABI

`ABIInfo.FromABI(abi, address, contractName, contractType, chainId)` builds the record. Addresses are stored lowercase.

```csharp
using var storage = SqliteABIInfoStorage.CreateInMemory();
storage.AddABIInfo(ABIInfo.FromABI(Erc20TransferAbi, ContractAddress, "Dai", null, ChainId));

var abiInfo = await storage.GetABIInfoAsync(ChainId, ContractAddress);
var function = await storage.FindFunctionABIAsync(ChainId, ContractAddress, TransferFunctionSignature);
var functionFromInput = await storage.FindFunctionABIFromInputDataAsync(ChainId, ContractAddress,
    TransferFunctionSignature + new string('0', 128));
var evt = await storage.FindEventABIAsync(ChainId, ContractAddress, TransferEventSignature);
```

Backed by `SqliteABIInfoStorageTests.Given_AnAbiStoredForAContract_When_LookedUpByAddressAsync_Then_ItsFunctionAndEventAreFound` (`DocSection.CoreFoundation`, `abi-info-sqlite-storage`).

## Batch lookup by signature

The batch methods answer from the signatures indexed across every stored ABI, without a contract address. Unknown signatures are absent from the result dictionaries.

```csharp
using var storage = SqliteABIInfoStorage.CreateInMemory();
storage.AddABIInfo(ABIInfo.FromABI(Erc20TransferAbi, ContractAddress, "Dai", null, ChainId));

var batch = await storage.FindABIsBatchAsync(
    new[] { TransferFunctionSignature, "0x12345678" },
    new[] { TransferEventSignature });

var transfer = batch.Functions[TransferFunctionSignature];
var transferEvent = batch.Events[TransferEventSignature];
```

Backed by `Given_AnAbiStored_When_SignaturesAreLookedUpInABatch_Then_EachKnownSignatureMapsToItsAbi` (`abi-info-sqlite-storage`, Order 2).

## Persist to a database file

```csharp
using (var writer = new SqliteABIInfoStorage($"Data Source={path}"))
{
    writer.AddABIInfo(ABIInfo.FromABI(Erc20TransferAbi, ContractAddress, "Dai", null, ChainId));
}

using var reader = new SqliteABIInfoStorage($"Data Source={path}");
var function = await reader.FindFunctionABIAsync(ChainId, ContractAddress, TransferFunctionSignature);
var contractName = (await reader.GetABIInfoAsync(ChainId, ContractAddress)).ContractName;
```

Backed by `Given_AnAbiWrittenToADatabaseFile_When_AnotherStorageOpensTheFile_Then_TheAbiIsReadBackFromTheDatabase` (`abi-info-sqlite-storage`, Order 3). The tests live in `tests/Nethereum.ABI.UnitTests/ABIRepository/SqliteABIInfoStorageTests.cs`.

## Members

`SqliteABIInfoStorage` implements `IABIInfoStorage` (see `Nethereum.ABI`):

```csharp
void AddABIInfo(ABIInfo abiInfo)
ABIInfo GetABIInfo(BigInteger chainId, string contractAddress)
Task<ABIInfo> GetABIInfoAsync(long chainId, string contractAddress)

FunctionABI FindFunctionABI(BigInteger chainId, string contractAddress, string signature)
FunctionABI FindFunctionABIFromInputData(BigInteger chainId, string contractAddress, string inputData)
EventABI FindEventABI(BigInteger chainId, string contractAddress, string signature)
ErrorABI FindErrorABI(BigInteger chainId, string contractAddress, string signature)
List<FunctionABI> FindFunctionABI(string signature)
List<FunctionABI> FindFunctionABIFromInputData(string inputData)
List<EventABI> FindEventABI(string signature)
List<ErrorABI> FindErrorABI(string signature)

Task<FunctionABI> FindFunctionABIAsync(BigInteger chainId, string contractAddress, string signature)
Task<FunctionABI> FindFunctionABIFromInputDataAsync(BigInteger chainId, string contractAddress, string inputData)
Task<EventABI> FindEventABIAsync(BigInteger chainId, string contractAddress, string signature)
Task<ErrorABI> FindErrorABIAsync(BigInteger chainId, string contractAddress, string signature)

Task<IDictionary<string, FunctionABI>> FindFunctionABIsBatchAsync(IEnumerable<string> signatures)
Task<IDictionary<string, EventABI>> FindEventABIsBatchAsync(IEnumerable<string> signatures)
Task<ABIBatchResult> FindABIsBatchAsync(IEnumerable<string> functionSignatures, IEnumerable<string> eventSignatures)

void Dispose()
```

Behaviour worth knowing, taken from the source:

- The address-less overloads (`FindFunctionABI(string)`, `FindFunctionABIFromInputData(string)`, `FindEventABI(string)`, `FindErrorABI(string)` and the batch methods) answer from the in-memory copy only. The in-memory copy is filled by `AddABIInfo` in this process, and by the address-based lookups that load a record from the database. A freshly opened file is therefore not searchable by signature alone until its records have been loaded by `GetABIInfo`/`FindFunctionABI(chainId, address, ...)` or added again.
- The database stores the ABI JSON, name, runtime bytecode and runtime source map. A record read back is rebuilt with `ABIInfo.FromABI(abi, address, contractName, null, chainId)`, so its `ContractType` is `null`.
- A null `ABIInfo` passed to `AddABIInfo` is ignored. A null `ChainId` is stored as `0`.
