---
name: etherscan-api
description: "Query Etherscan V2 API for gas prices, account transactions, balances, token transfers, contract source code, and compilation metadata via Nethereum.DataServices. Use when the user asks about Ethereum block explorer API, wallet transactions, crypto balances, gas prices, contract verification, token transfers, NFT transfers, or Etherscan integration in .NET/C#."
user-invocable: true
---

# Etherscan API

Query the Etherscan V2 unified API across all Etherscan-supported chains using `Nethereum.DataServices`.

**NuGet Package:** `Nethereum.DataServices`

## Installation

```bash
dotnet add package Nethereum.DataServices
```

## Quick Start

```csharp
using Nethereum.DataServices.Etherscan;

var etherscan = new EtherscanApiService(chain: 1, apiKey: "YOUR_ETHERSCAN_API_KEY");

// Gas prices
var gas = await etherscan.GasTracker.GetGasOracleAsync();
Console.WriteLine($"Safe: {gas.Result.SafeGasPrice} Gwei");
Console.WriteLine($"Proposed: {gas.Result.ProposeGasPrice} Gwei");
Console.WriteLine($"Fast: {gas.Result.FastGasPrice} Gwei");

// Account balance
var balance = await etherscan.Accounts.GetBalanceAsync("0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045");
Console.WriteLine($"Balance: {balance.Result} Wei");

// Contract ABI
var abi = await etherscan.Contracts.GetAbiAsync("0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48");
```

## Sub-Services

| Property | Class | Description |
|----------|-------|-------------|
| `Contracts` | `EtherscanApiContractsService` | ABI, source code, contract creator |
| `Accounts` | `EtherscanApiAccountsService` | Balances, transactions, token transfers |
| `GasTracker` | `EtherscanApiGasTrackerService` | Gas oracle, confirmation estimates |

## Gas Tracker

```csharp
// Gas oracle with price tiers
var response = await etherscan.GasTracker.GetGasOracleAsync();
var gas = response.Result;
// gas.SafeGasPrice, gas.ProposeGasPrice, gas.FastGasPrice, gas.SuggestBaseFee, gas.GasUsedRatio

// Confirmation time estimate (gas price in Wei)
var estimate = await etherscan.GasTracker.GetEstimatedConfirmationTimeAsync(gasPriceInWei: 30_000_000_000);
Console.WriteLine($"Seconds to confirm: {estimate.Result}");
```

## Account Queries

```csharp
// Single balance
var balance = await etherscan.Accounts.GetBalanceAsync("0x...");

// Multiple balances
var balances = await etherscan.Accounts.GetBalancesAsync(new[] { "0x...", "0x..." });

// Transaction history (paginated)
var txns = await etherscan.Accounts.GetAccountTransactionsAsync("0x...", page: 1, offset: 10, sort: "desc");

// Internal transactions
var internal = await etherscan.Accounts.GetAccountInternalTransactionsAsync("0x...", page: 1, offset: 10, sort: "desc");

// ERC-20 token transfers
var transfers = await etherscan.Accounts.GetTokenTransfersAsync("0x...");

// ERC-721 (NFT) transfers
var nfts = await etherscan.Accounts.GetErc721TransfersAsync("0x...");

// ERC-1155 transfers
var multi = await etherscan.Accounts.GetErc1155TransfersAsync("0x...");

// Funded by
var funded = await etherscan.Accounts.GetFundedByAsync("0x...");
```

## Contract Data

```csharp
// ABI as JSON string
var abiResponse = await etherscan.Contracts.GetAbiAsync("0x...");
string abi = abiResponse.Result;

// Source code and compilation metadata
var source = await etherscan.Contracts.GetSourceCodeAsync("0x...");
var info = source.Result.First();
Console.WriteLine($"Name: {info.ContractName}, Compiler: {info.CompilerVersion}");

// Contract creator and deployment tx
var creator = await etherscan.Contracts.GetContractCreatorAndCreationTxHashAsync("0x...");
```

## Multi-Chain Usage

Same API key works across all Etherscan V2 chains:

```csharp
var polygonApi = new EtherscanApiService(chain: 137, apiKey: "YOUR_KEY");
var arbApi = new EtherscanApiService(chain: 42161, apiKey: "YOUR_KEY");
var baseApi = new EtherscanApiService(chain: 8453, apiKey: "YOUR_KEY");
```

## Sourcify Parquet Exports

```csharp
using Nethereum.DataServices.Sourcify;

var parquet = new SourcifyParquetExportService();
var files = await parquet.ListTableFilesAsync("verified_contracts");
using var stream = await parquet.DownloadFileAsync(files.First().Key);
```

## Key Namespaces

- `Nethereum.DataServices.Etherscan` — Etherscan API service and response models
- `Nethereum.DataServices.Sourcify` — Sourcify Parquet export service

## Documentation

- [Etherscan API Guide](https://nethereum.com/docs/data-services/guide-etherscan-api)
- [Data Services Package Reference](https://nethereum.com/docs/data-services/nethereum-dataservices)
