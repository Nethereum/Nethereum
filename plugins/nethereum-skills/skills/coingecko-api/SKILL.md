---
name: coingecko-api
description: Fetch token metadata, prices by contract address or CoinGecko ID, asset platform mappings, and token lists via the CoinGecko API in Nethereum.DataServices
user-invocable: true
---

# CoinGecko API

Access CoinGecko token metadata, pricing, and asset platform mappings using `Nethereum.DataServices`.

**NuGet Package:** `Nethereum.DataServices`

## Installation

```bash
dotnet add package Nethereum.DataServices
```

## Quick Start

```csharp
using Nethereum.DataServices.CoinGecko;

var coingecko = new CoinGeckoApiService();

// Map chain ID to CoinGecko platform
string platformId = await coingecko.GetPlatformIdForChainAsync(chainId: 1);
// "ethereum"

// Get token list for a chain
var tokenList = await coingecko.GetTokenListForChainAsync(chainId: 1);
Console.WriteLine($"Ethereum tokens: {tokenList.Tokens.Count}");

// Get prices by CoinGecko ID
var prices = await coingecko.GetPricesAsync(new[] { "ethereum", "usd-coin" }, "usd");
Console.WriteLine($"ETH: ${prices["ethereum"]["usd"]}");

// Get price by contract address
decimal? usdcPrice = await coingecko.GetTokenPriceByContractAsync(
    "ethereum", "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", "usd");
```

## Available Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetAssetPlatformsAsync(forceRefresh?)` | `List<CoinGeckoAssetPlatform>` | All blockchain platforms with chain identifiers |
| `GetPlatformIdForChainAsync(chainId)` | `string` | Map numeric chain ID to CoinGecko platform ID |
| `GetCoinsListAsync(forceRefresh?)` | `List<CoinGeckoCoin>` | Every coin with platform/address mappings |
| `GetTokenListForChainAsync(chainId, forceRefresh?)` | `CoinGeckoTokenList` | Token list for a chain (address, symbol, name, decimals, logo) |
| `GetTokenListForPlatformAsync(platformId, forceRefresh?)` | `CoinGeckoTokenList` | Token list by platform ID |
| `GetTokensForChainAsync(chainId, forceRefresh?)` | `List<CoinGeckoToken>` | Flat token collection for a chain |
| `GetPricesAsync(geckoIds, vsCurrency)` | `Dictionary<string, Dictionary<string, decimal>>` | Batch prices by CoinGecko ID |
| `GetTokenPriceByContractAsync(platformId, address, vsCurrency)` | `decimal?` | Price by contract address (null if not found) |
| `FindCoinGeckoIdAsync(contractAddress, chainId)` | `string` | Map contract address to CoinGecko ID |
| `FindCoinGeckoIdsAsync(addresses, chainId)` | `Dictionary<string, string>` | Batch address-to-ID mapping |
| `ClearCache()` | `void` | Clear all cached data |

## Asset Platforms

```csharp
var platforms = await coingecko.GetAssetPlatformsAsync();
foreach (var p in platforms.Where(x => x.ChainIdentifier.HasValue).Take(10))
    Console.WriteLine($"Chain {p.ChainIdentifier}: {p.Id} ({p.Name})");
```

## Token Lists

```csharp
var tokenList = await coingecko.GetTokenListForChainAsync(chainId: 1);
foreach (var token in tokenList.Tokens.Take(5))
    Console.WriteLine($"{token.Symbol}: {token.Name} ({token.Address})");
```

## Pricing

```csharp
// By CoinGecko ID (batches at 250 per request)
var prices = await coingecko.GetPricesAsync(
    new[] { "ethereum", "usd-coin", "bitcoin" }, "usd");

// By contract address
decimal? price = await coingecko.GetTokenPriceByContractAsync(
    "ethereum", "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", "usd");
```

## Address-to-ID Mapping

```csharp
string geckoId = await coingecko.FindCoinGeckoIdAsync(
    "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", chainId: 1);
// "usd-coin"

var ids = await coingecko.FindCoinGeckoIdsAsync(
    new[] { "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", "0xdAC17F958D2ee523a2206206994597C13D831ec7" },
    chainId: 1);
```

## Cache Configuration

```csharp
var config = new CoinGeckoCacheConfiguration
{
    Enabled = true,
    PlatformsCacheDuration = TimeSpan.FromHours(24),
    CoinsListCacheDuration = TimeSpan.FromHours(1),
    TokenListCacheDuration = TimeSpan.FromHours(1),
    RateLimitDelay = TimeSpan.FromMilliseconds(1500)
};

var coingecko = new CoinGeckoApiService(config);

// Disable caching (for tests)
var testService = new CoinGeckoApiService(CoinGeckoCacheConfiguration.Disabled);

// Force refresh specific cache
var fresh = await coingecko.GetAssetPlatformsAsync(forceRefresh: true);

// Clear all caches
coingecko.ClearCache();
```

## Key Namespaces

- `Nethereum.DataServices.CoinGecko` — Service, cache config, and response models

## Documentation

- [CoinGecko API Guide](https://nethereum.com/docs/data-services/guide-coingecko-api)
- [Token Portfolio Guide](https://nethereum.com/docs/data-services/guide-token-portfolio)
- [Data Services Package Reference](https://nethereum.com/docs/data-services/nethereum-dataservices)
