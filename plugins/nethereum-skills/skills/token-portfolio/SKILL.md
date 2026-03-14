---
name: token-portfolio
description: Build wallet portfolio features with ERC-20 token discovery, multicall balances, CoinGecko pricing, and multi-account scanning (.NET/C#). Use this skill when the user asks about token balances, token portfolios, token prices, token lists, multicall token queries, multi-wallet scanning, or CoinGecko integration.
user-invocable: true
---

# Token Portfolio & Balances

Nethereum.TokenServices builds a wallet portfolio from an embedded list of thousands of known tokens per chain — checking all of them via batched multicall (`balanceOf` reads in a single `eth_call`). No chain indexing, no third-party indexer dependency. Works against any standard RPC endpoint. Optional CoinGecko pricing, multi-account scanning, and a persistent token catalog.

NuGet: `Nethereum.TokenServices`

```bash
dotnet add package Nethereum.TokenServices
```

## Quick Start

```csharp
using Nethereum.TokenServices.ERC20;
using Nethereum.TokenServices.ERC20.Balances;
using Nethereum.Web3;

var web3 = new Web3("https://eth.llamarpc.com");
var tokenService = new Erc20TokenService();

var balances = await tokenService.GetBalancesWithPricesAsync(
    web3, accountAddress, chainId: 1, vsCurrency: "usd",
    includeNative: true, NativeTokenConfig.ForChain(1, "ETH", "Ether"));

foreach (var b in balances.Where(x => x.Value > 0).OrderByDescending(x => x.Value))
    Console.WriteLine($"{b.Token?.Symbol}: {b.BalanceDecimal:N4} @ ${b.Price:N2} = ${b.Value:N2}");
```

## Key Methods

### IErc20TokenService

```csharp
// All balances with prices
var balances = await tokenService.GetBalancesWithPricesAsync(
    web3, address, chainId, "usd", includeNative: true, nativeToken);

// Balances only (no prices)
var balances = await tokenService.GetAllBalancesAsync(
    web3, address, chainId, includeNative: true, nativeToken);

// Specific tokens only
var balances = await tokenService.GetBalancesForTokensWithPricesAsync(
    web3, address, tokenList, "usd");

// Incremental refresh via Transfer events (returns List<TokenBalance>)
var updated = await tokenService.RefreshBalancesFromEventsAsync(
    web3, address, chainId, fromBlock, existingTokens, "usd");

// Token lookup
var tokens = await tokenService.GetTokenListAsync(chainId);
var token = await tokenService.GetTokenAsync(chainId, contractAddress);

// Pricing
var prices = await tokenService.GetPricesForTokensAsync(chainId, contractAddresses, "usd");
var ethPrice = await tokenService.GetNativeTokenPriceAsync(chainId, "usd");
```

## Models

```csharp
// TokenBalance — returned by all balance methods
public class TokenBalance
{
    public TokenInfo Token { get; set; }
    public BigInteger Balance { get; set; }
    public bool IsNative { get; set; }
    public decimal BalanceDecimal { get; }  // Converted from Wei
    public decimal? Price { get; set; }
    public decimal? Value { get; }          // BalanceDecimal * Price
}

// TokenInfo — token metadata
public class TokenInfo
{
    public string Address { get; set; }
    public string Symbol { get; set; }
    public string Name { get; set; }
    public int Decimals { get; set; }
    public string LogoUri { get; set; }
    public long ChainId { get; set; }
    public string CoinGeckoId { get; set; }
}
```

## Batch Price Service

```csharp
using Nethereum.TokenServices.ERC20.Pricing;

var batchPrice = new BatchPriceService(new CoinGeckoPriceProvider());

var request = new BatchPriceRequest("usd")
    .AddChain(1, new[] { "0xA0b86991...", "0xdAC17F..." }, includeNative: true)
    .AddChain(137, new[] { "0x2791Bca1..." }, includeNative: true);

var result = await batchPrice.GetPricesAsync(request);
if (result.TryGetPrice(1, "0xA0b86991...", out var price))
    Console.WriteLine($"USDC: ${price.Price}");
if (result.TryGetNativePrice(1, out var ethPrice))
    Console.WriteLine($"ETH: ${ethPrice.Price}");
```

## Multi-Account Scanning

```csharp
using Nethereum.TokenServices.MultiAccount;

var multiService = serviceProvider.GetRequiredService<IMultiAccountTokenService>();
var strategy = serviceProvider.GetRequiredService<IDiscoveryStrategy>();

var result = await multiService.ScanAsync(
    accounts: new[] { "0xWallet1...", "0xWallet2..." },
    chainIds: new[] { 1L, 137L, 42161L },
    web3Factory: chainId => new Web3(GetRpcUrl(chainId)),
    strategy: strategy,
    options: new MultiAccountScanOptions { MaxParallelChains = 3, IncludeNativeToken = true },
    progress: new Progress<MultiAccountProgress>(p =>
        Console.WriteLine($"{p.CompletedChains}/{p.TotalChains} chains, {p.TokensFound} tokens")));

// Results by chain / by account
foreach (var (chainId, chainResult) in result.ChainResults)
    Console.WriteLine($"Chain {chainId}: {chainResult.TokensFound} tokens");

// Price-only refresh
var priceResult = await multiService.RefreshPricesAsync(accountTokens, "usd");
```

## Token Catalog

Persistent, refreshable token registry:

```csharp
using Nethereum.TokenServices.Catalog;

// Setup
services.AddTokenCatalog(options =>
{
    options.CatalogDirectory = "/data/catalog";
    options.AutoSeedFromEmbedded = true;
    options.RegisterCoinGeckoSource = true;
});

// Query
var repo = serviceProvider.GetRequiredService<ITokenCatalogRepository>();
var tokens = await repo.GetAllTokensAsync(chainId: 1);
var usdc = await repo.GetTokenByAddressAsync(chainId: 1, "0xA0b86991...");

// Refresh
var refreshService = serviceProvider.GetRequiredService<ITokenCatalogRefreshService>();
var result = await refreshService.RefreshAsync(chainId: 1);

// Use catalog as token list provider
var adapter = new CatalogTokenListProviderAdapter(repo, autoSeed: true);
var tokenService = new Erc20TokenService(tokenListProvider: adapter);
```

## Discovery Strategies

| Strategy | How It Works | Speed |
|----------|-------------|-------|
| `TokenListDiscoveryStrategy` (default) | Check known tokens via multicall | Fast |
| `EventLogDiscoveryStrategy` | Scan all Transfer events | Thorough but slow |
| Custom `IDiscoveryStrategy` | Your logic | Varies |

## Dependency Injection

```csharp
// Basic
services.AddErc20TokenServices();

// With options
services.AddErc20TokenServices(options =>
{
    options.UseFileCache = true;
    options.CacheDirectory = "/data/cache";
    options.PriceCacheExpiry = TimeSpan.FromMinutes(5);
    options.MultiCallBatchSize = 100;
});

// Custom providers
services.AddErc20TokenServices()
    .UseTokenListProvider<MyProvider>()
    .UsePriceProvider<MyPriceProvider>()
    .UseDiscoveryStrategy<MyStrategy>();
```

## Supported Chains (Built-in)

Ethereum (1), Optimism (10), BNB Chain (56), Gnosis (100), Polygon (137), zkSync Era (324), Base (8453), Arbitrum (42161), Celo (42220), Avalanche (43114), Linea (59144).

## Documentation

- [Token Portfolio Guide](https://nethereum.readthedocs.io/en/latest/data-services/guide-token-portfolio/)
- [Nethereum.TokenServices Reference](https://nethereum.readthedocs.io/en/latest/data-services/nethereum-tokenservices/)
