---
name: chainlist-rpc
description: "Discover EVM chains, RPC endpoints, native currencies, block explorers, and faucets using the Chainlist registry via Nethereum.DataServices. Use when the user asks about blockchain networks, network configuration, chain IDs, RPC URLs, finding an endpoint, available EVM chains, testnet faucets, or chain metadata lookup in .NET/C#."
user-invocable: true
---

# Chainlist RPC Discovery

Discover EVM chain metadata from the Chainlist public registry using `Nethereum.DataServices`.

**NuGet Package:** `Nethereum.DataServices`

## Installation

```bash
dotnet add package Nethereum.DataServices
```

## Quick Start

```csharp
using Nethereum.DataServices.Chainlist;

var chainlist = new ChainlistRpcApiService();

// Get all EVM chains
var allChains = await chainlist.GetAllChainsAsync();
Console.WriteLine($"Total EVM chains known: {allChains.Count}");

// Get a specific chain by ID
var ethereum = await chainlist.GetChainByIdAsync(1);
Console.WriteLine($"{ethereum.Name} (Chain ID: {ethereum.ChainId})");
Console.WriteLine($"Currency: {ethereum.NativeCurrency.Symbol}");
```

## Available Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetAllChainsAsync()` | `List<ChainlistChainInfo>` | Full registry of all EVM chains |
| `GetChainByIdAsync(long chainId)` | `ChainlistChainInfo` | Single chain by numeric chain ID |

## ChainlistChainInfo Model

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Chain name (e.g., "Ethereum Mainnet") |
| `Chain` | `string` | Short identifier (e.g., "ETH") |
| `ChainId` | `long` | Numeric chain ID |
| `NetworkId` | `long` | Network ID |
| `Rpc` | `List<ChainlistRpc>` | RPC endpoints with URL, Tracking, IsOpenSource |
| `NativeCurrency` | `ChainlistNativeCurrency` | Name, Symbol, Decimals |
| `Explorers` | `List<ChainlistExplorer>` | Block explorers with Name, Url, Standard |
| `Faucets` | `List<string>` | Testnet faucet URLs |
| `Features` | `List<ChainlistFeature>` | Supported features (e.g., EIP-1559) |
| `InfoURL` | `string` | Chain information page |
| `ShortName` | `string` | Short name for display |

## Chain Lookup

```csharp
var polygon = await chainlist.GetChainByIdAsync(137);
Console.WriteLine($"Name:     {polygon.Name}");
Console.WriteLine($"Currency: {polygon.NativeCurrency.Symbol} ({polygon.NativeCurrency.Name})");
Console.WriteLine($"Decimals: {polygon.NativeCurrency.Decimals}");
```

## RPC Endpoints

```csharp
var ethereum = await chainlist.GetChainByIdAsync(1);
foreach (var rpc in ethereum.Rpc)
{
    Console.WriteLine($"URL: {rpc.Url}, Tracking: {rpc.Tracking}, OpenSource: {rpc.IsOpenSource}");
}
```

## Block Explorers

```csharp
foreach (var explorer in ethereum.Explorers)
    Console.WriteLine($"{explorer.Name}: {explorer.Url} (Standard: {explorer.Standard})");
```

## Filtering Chains

```csharp
var chains = await chainlist.GetAllChainsAsync();

// Testnets
var testnets = chains.Where(c => c.Name.Contains("Sepolia", StringComparison.OrdinalIgnoreCase)).ToList();

// Chains with EIP-1559
var eip1559 = chains.Where(c => c.Features?.Any(f => f.Name == "EIP1559") == true).ToList();

// By native currency
var ethChains = chains.Where(c => c.NativeCurrency?.Symbol == "ETH").ToList();
```

## Key Namespaces

- `Nethereum.DataServices.Chainlist` — Service and response models

## Documentation

- [Chainlist RPC Guide](https://nethereum.com/docs/data-services/guide-chainlist-rpc)
- [Data Services Package Reference](https://nethereum.com/docs/data-services/nethereum-dataservices)
