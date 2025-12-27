# Nethereum.TokenServices

A standalone .NET library for ERC20 token discovery, balance retrieval, pricing, and multi-account scanning. Designed for multi-chain support with pluggable providers and discovery strategies.

## Installation

```bash
dotnet add package Nethereum.TokenServices
```

## Problems This Library Solves

| Problem | Solution |
|---------|----------|
| "How do I find all tokens a wallet holds?" | Token discovery via token lists or event scanning |
| "How do I get balances for many tokens efficiently?" | Multicall batching (100+ tokens per call) |
| "How do I get USD prices for tokens?" | CoinGecko integration with caching |
| "How do I track new tokens received?" | Event-based refresh from Transfer logs |
| "How do I scan multiple wallets across chains?" | Multi-account service with parallel scanning |
| "How do I use my own token list/price source?" | Pluggable providers and strategies |

## Quick Start

```csharp
using Nethereum.TokenServices.ERC20;
using Nethereum.TokenServices.ERC20.Balances;
using Nethereum.Web3;

var web3 = new Web3("https://eth.llamarpc.com");
var tokenService = new Erc20TokenService();

// Get all token balances with prices
var balances = await tokenService.GetBalancesWithPricesAsync(
    web3,
    "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045", // Vitalik
    chainId: 1,
    vsCurrency: "usd",
    includeNative: true,
    nativeToken: NativeTokenConfig.ForChain(1, "ETH", "Ether"));

foreach (var b in balances.Where(x => x.Value > 0).OrderByDescending(x => x.Value))
{
    Console.WriteLine($"{b.Token?.Symbol ?? "ETH"}: {b.BalanceDecimal:N4} @ ${b.Price:N2} = ${b.Value:N2}");
}
```

## Use Cases & Scenarios

### Scenario 1: Wallet Portfolio Display

**Goal:** Show all tokens a user holds with current USD values.

```csharp
var tokenService = new Erc20TokenService();
var web3 = new Web3(rpcUrl);

var balances = await tokenService.GetBalancesWithPricesAsync(
    web3, userAddress, chainId, "usd", includeNative: true,
    NativeTokenConfig.ForChain(chainId, "ETH", "Ether"));

var portfolio = balances
    .Where(b => b.Balance > 0)
    .OrderByDescending(b => b.Value)
    .ToList();

decimal totalValue = portfolio.Sum(b => b.Value ?? 0);
```

### Scenario 2: Incremental Balance Updates (Polling)

**Goal:** Efficiently update balances by only checking tokens that changed.

```csharp
var tokenService = new Erc20TokenService();
ulong lastScannedBlock = GetLastScannedBlock(); // From your storage

// Scan for new Transfer events
var result = await tokenService.RefreshBalancesFromEventsAsync(
    web3, userAddress, chainId,
    fromBlock: lastScannedBlock,
    existingTokens: currentTokenList,
    vsCurrency: "usd");

if (result.Success)
{
    // Only tokens with transfers were updated
    foreach (var updated in result.UpdatedBalances)
    {
        UpdateLocalBalance(updated);
    }

    // New tokens discovered from events
    if (result.NewTokensFound > 0)
    {
        AddNewTokensToPortfolio(result.UpdatedBalances.Where(IsNew));
    }

    SaveLastScannedBlock(result.ToBlock);
}
```

### Scenario 3: Multi-Wallet, Multi-Chain Scanning

**Goal:** Scan multiple wallets across multiple chains (e.g., for a portfolio app).

```csharp
using Nethereum.TokenServices.MultiAccount;
using Nethereum.TokenServices.ERC20.Discovery;

// Get services via DI
var multiAccountService = serviceProvider.GetRequiredService<IMultiAccountTokenService>();
var discoveryStrategy = serviceProvider.GetRequiredService<IDiscoveryStrategy>();

var accounts = new[] { "0xWallet1...", "0xWallet2...", "0xWallet3..." };
var chains = new[] { 1L, 137L, 42161L, 8453L }; // Ethereum, Polygon, Arbitrum, Base

var result = await multiAccountService.ScanAsync(
    accounts,
    chains,
    web3Factory: chainId => new Web3(GetRpcUrl(chainId)),
    strategy: discoveryStrategy,
    options: new MultiAccountScanOptions
    {
        MaxParallelChains = 3,
        PageSize = 100,
        IncludeNativeToken = true
    },
    progress: new Progress<MultiAccountProgress>(p =>
    {
        Console.WriteLine($"Progress: {p.OverallPercentComplete:F1}%");
        Console.WriteLine($"Chains: {p.CompletedChains}/{p.TotalChains}");
        Console.WriteLine($"Tokens found: {p.TokensFound}");
    }));

// Access results by chain
foreach (var chainResult in result.ChainResults)
{
    Console.WriteLine($"Chain {chainResult.Key}: {chainResult.Value.TokensFound} tokens");
}

// Access results by account
foreach (var accountResult in result.AccountResults)
{
    Console.WriteLine($"Account {accountResult.Key}: {accountResult.Value.TokensFound} tokens");
}
```

### Scenario 4: Price Refresh Only

**Goal:** Update prices without re-fetching balances.

```csharp
var multiAccountService = serviceProvider.GetRequiredService<IMultiAccountTokenService>();

// Prepare token data (account, chain, existing balances)
var accountTokens = new List<(string account, long chainId, IEnumerable<TokenBalance> tokens)>
{
    ("0xWallet1...", 1, ethereumTokens),
    ("0xWallet1...", 137, polygonTokens),
    ("0xWallet2...", 1, wallet2Tokens)
};

var priceResult = await multiAccountService.RefreshPricesAsync(
    accountTokens,
    currency: "usd");

if (priceResult.Success)
{
    Console.WriteLine($"Updated prices for {priceResult.TokensUpdated} tokens");
}
```

### Scenario 5: Specific Token Monitoring

**Goal:** Track only specific tokens (e.g., USDC, WETH, DAI).

```csharp
var tokenService = new Erc20TokenService();

var watchlist = new[]
{
    new TokenInfo { Address = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", Symbol = "USDC", Decimals = 6 },
    new TokenInfo { Address = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2", Symbol = "WETH", Decimals = 18 },
    new TokenInfo { Address = "0x6B175474E89094C44Da98b954EescdeCB5BAD0", Symbol = "DAI", Decimals = 18 }
};

var balances = await tokenService.GetBalancesForTokensWithPricesAsync(
    web3, userAddress, watchlist, "usd");
```

## Discovery Strategies

Discovery strategies determine **how tokens are found** for an account. Two built-in strategies are provided:

### TokenListDiscoveryStrategy (Default, FREE)

Scans a known list of tokens (from CoinGecko/embedded) and checks balances via multicall.

- **Pros:** Fast, low RPC usage, known tokens only
- **Cons:** Won't find obscure/new tokens not in the list
- **Best for:** Standard portfolios, quick scans

```csharp
var strategy = new TokenListDiscoveryStrategy(tokenListProvider, discoveryEngine);
```

### EventLogDiscoveryStrategy (FREE, Thorough)

Scans ALL Transfer events for the account from genesis and checks current balances.

- **Pros:** Finds every token ever held
- **Cons:** Slow, high RPC usage, requires archive node for full history
- **Best for:** Complete portfolio discovery, one-time scans

```csharp
var strategy = new EventLogDiscoveryStrategy(eventScanner, balanceProvider, tokenListProvider);
```

### Custom Discovery Strategies (Pluggable)

Implement `IDiscoveryStrategy` for custom sources (e.g., Etherscan API):

```csharp
public class EtherscanDiscoveryStrategy : IDiscoveryStrategy
{
    private readonly string _apiKey;

    public string StrategyName => "Etherscan";

    public async Task<TokenDiscoveryResult> DiscoverAsync(
        IWeb3 web3, string accountAddress, long chainId,
        DiscoveryOptions options = null,
        IProgress<DiscoveryProgress> progress = null,
        CancellationToken ct = default)
    {
        // Call Etherscan API: /api?module=account&action=tokentx&address={account}
        var transfers = await _etherscanClient.GetTokenTransfersAsync(accountAddress);

        // Extract unique token addresses
        var tokenAddresses = transfers.Select(t => t.ContractAddress).Distinct();

        // Get current balances
        var tokens = tokenAddresses.Select(a => new TokenInfo { Address = a, Decimals = 18 });
        var balances = await _balanceProvider.GetBalancesAsync(web3, accountAddress, tokens);

        return TokenDiscoveryResult.Successful(
            balances.Where(b => b.Balance > 0).ToList(),
            progress, StrategyName);
    }

    public Task<bool> SupportsChainAsync(long chainId) => Task.FromResult(chainId == 1);
    public Task<int> GetExpectedTokenCountAsync(long chainId) => Task.FromResult(0);
}

// Register in DI
services.AddErc20TokenServices()
    .UseDiscoveryStrategy<EtherscanDiscoveryStrategy>();
```

## Pluggable Providers

### Token List Provider

```csharp
public class MyTokenListProvider : ITokenListProvider
{
    public async Task<List<TokenInfo>> GetTokensAsync(long chainId)
    {
        // Fetch from your database, API, or static list
        return await _myDatabase.GetTokensForChain(chainId);
    }

    public Task<TokenInfo> GetTokenAsync(long chainId, string address) { ... }
    public Task<bool> SupportsChainAsync(long chainId) { ... }
}

// Use it
var tokenService = new Erc20TokenService(tokenListProvider: new MyTokenListProvider());

// Or via DI
services.AddErc20TokenServices().UseTokenListProvider<MyTokenListProvider>();
```

### Price Provider

```csharp
public class MyPriceProvider : ITokenPriceProvider
{
    public async Task<Dictionary<string, TokenPrice>> GetPricesByContractAsync(
        long chainId, IEnumerable<string> addresses, string currency)
    {
        // Fetch from your price source
        return await _myPriceApi.GetPrices(chainId, addresses, currency);
    }

    // ... other interface methods
}

services.AddErc20TokenServices().UsePriceProvider<MyPriceProvider>();
```

### Cache Provider

```csharp
// Use file-based cache
services.AddErc20TokenServices(options =>
{
    options.UseFileCache = true;
    options.CacheDirectory = "/data/token-cache";
});

// Or provide custom cache (Redis, etc.)
public class RedisCacheProvider : ICacheProvider { ... }
services.AddErc20TokenServices().UseCacheProvider<RedisCacheProvider>();
```

### Storage Providers (Database-Ready)

The library uses pluggable storage interfaces for persisting token list diffs and coin mappings. Default implementations are file-based, but you can easily swap to PostgreSQL, SQLite, or any database:

```csharp
// Default: file-based storage
services.AddErc20TokenServices(options =>
{
    options.UseFileDiffStorage = true;
    options.TokenListDiffStorageDirectory = "/data/tokenlists";
    options.CoinMappingDiffStorageDirectory = "/data/coinmappings";
});

// PostgreSQL implementation
public class PostgresTokenListDiffStorage : ITokenListDiffStorage
{
    private readonly NpgsqlConnection _connection;

    public async Task<List<TokenInfo>> GetAdditionalTokensAsync(long chainId)
    {
        return await _connection.QueryAsync<TokenInfo>(
            "SELECT * FROM additional_tokens WHERE chain_id = @chainId",
            new { chainId });
    }

    public async Task SaveAdditionalTokensAsync(long chainId, List<TokenInfo> tokens)
    {
        await _connection.ExecuteAsync(
            "INSERT INTO additional_tokens (chain_id, address, symbol, decimals) VALUES ...",
            tokens.Select(t => new { chainId, t.Address, t.Symbol, t.Decimals }));
    }

    public async Task<DateTime?> GetLastUpdateAsync(long chainId)
    {
        return await _connection.QuerySingleOrDefaultAsync<DateTime?>(
            "SELECT last_update FROM token_list_metadata WHERE chain_id = @chainId",
            new { chainId });
    }

    public async Task SetLastUpdateAsync(long chainId, DateTime updateTime)
    {
        await _connection.ExecuteAsync(
            "INSERT INTO token_list_metadata (chain_id, last_update) VALUES (@chainId, @updateTime) " +
            "ON CONFLICT (chain_id) DO UPDATE SET last_update = @updateTime",
            new { chainId, updateTime });
    }

    public async Task ClearAsync(long chainId)
    {
        await _connection.ExecuteAsync(
            "DELETE FROM additional_tokens WHERE chain_id = @chainId;" +
            "DELETE FROM token_list_metadata WHERE chain_id = @chainId",
            new { chainId });
    }
}

// Register your custom storage
services.AddErc20TokenServices()
    .UseTokenListDiffStorage<PostgresTokenListDiffStorage>()
    .UseCoinMappingDiffStorage<PostgresCoinMappingDiffStorage>();
```

**Storage Interfaces:**

| Interface | Purpose | Default |
|-----------|---------|---------|
| `ITokenListDiffStorage` | Stores tokens discovered beyond embedded lists | `FileTokenListDiffStorage` |
| `ICoinMappingDiffStorage` | Stores contract→CoinGecko ID mappings | `FileCoinMappingDiffStorage` |
| `ICacheProvider` | General key-value cache with expiry | `MemoryCacheProvider` |

## Dependency Injection Setup

### Basic Setup

```csharp
services.AddErc20TokenServices();
```

### With Options

```csharp
services.AddErc20TokenServices(options =>
{
    options.UseFileCache = true;
    options.CacheDirectory = "/data/cache";
    options.DefaultCurrency = "usd";
    options.PriceCacheExpiry = TimeSpan.FromMinutes(5);
    options.TokenListCacheExpiry = TimeSpan.FromDays(7);
    options.MultiCallBatchSize = 100;
    options.MaxParallelChains = 3;
});
```

### With Custom Providers

```csharp
services.AddErc20TokenServices()
    .UseTokenListProvider<MyTokenListProvider>()
    .UsePriceProvider<MyPriceProvider>()
    .UseDiscoveryStrategy<MyDiscoveryStrategy>()
    .UseCacheProvider<RedisCacheProvider>();
```

## Registered Services

After calling `AddErc20TokenServices()`, these services are available:

| Interface | Default Implementation | Lifetime |
|-----------|----------------------|----------|
| `IErc20TokenService` | `Erc20TokenService` | Singleton |
| `ITokenListProvider` | `ResilientTokenListProvider` | Singleton |
| `ITokenBalanceProvider` | `MultiCallBalanceProvider` | Singleton |
| `ITokenPriceProvider` | `CoinGeckoPriceProvider` | Singleton |
| `ITokenEventScanner` | `Erc20EventScanner` | Singleton |
| `ITokenDiscoveryEngine` | `TokenDiscoveryEngine` | Singleton |
| `IDiscoveryStrategy` | `TokenListDiscoveryStrategy` | Singleton |
| `IMultiAccountTokenService` | `MultiAccountTokenService` | Singleton |
| `ICacheProvider` | `MemoryCacheProvider` | Singleton |
| `ITokenListDiffStorage` | `NullTokenListDiffStorage`* | Singleton |
| `ICoinMappingDiffStorage` | `NullCoinMappingDiffStorage`* | Singleton |

*Use `UseFileDiffStorage = true` option or `UseTokenListDiffStorage<T>()` builder method for persistence.

## Architecture

```
Nethereum.TokenServices/
├── ERC20/
│   ├── IErc20TokenService.cs           # Main facade
│   ├── Erc20TokenService.cs
│   ├── Discovery/
│   │   ├── IDiscoveryStrategy.cs       # Plugin interface
│   │   ├── TokenListDiscoveryStrategy.cs
│   │   ├── EventLogDiscoveryStrategy.cs
│   │   ├── ITokenListProvider.cs
│   │   ├── ITokenListDiffStorage.cs    # Pluggable storage
│   │   ├── ResilientTokenListProvider.cs
│   │   ├── EmbeddedTokenListProvider.cs
│   │   └── CoinGeckoTokenListProvider.cs
│   ├── Balances/
│   │   ├── ITokenBalanceProvider.cs
│   │   └── MultiCallBalanceProvider.cs
│   ├── Pricing/
│   │   ├── ITokenPriceProvider.cs
│   │   ├── ICoinMappingDiffStorage.cs  # Pluggable storage
│   │   ├── CoinGeckoPriceProvider.cs
│   │   └── BatchPriceService.cs
│   ├── Events/
│   │   ├── ITokenEventScanner.cs
│   │   └── Erc20EventScanner.cs
│   └── Models/
│       ├── TokenInfo.cs
│       ├── TokenBalance.cs
│       └── TokenPrice.cs
├── MultiAccount/
│   ├── IMultiAccountTokenService.cs    # Multi-wallet scanning
│   ├── MultiAccountTokenService.cs
│   └── Models/
├── Caching/
│   ├── ICacheProvider.cs
│   ├── MemoryCacheProvider.cs
│   ├── FileCacheProvider.cs
│   ├── FileStorageBase.cs              # Base class for file storage
│   ├── FileTokenListDiffStorage.cs     # File-based impl
│   ├── FileCoinMappingDiffStorage.cs   # File-based impl
│   ├── NullTokenListDiffStorage.cs     # No-op impl
│   └── NullCoinMappingDiffStorage.cs   # No-op impl
└── Resources/                          # Embedded token lists
```

## Supported Chains (Out of Box)

| Chain | Chain ID | Native Token |
|-------|----------|--------------|
| Ethereum | 1 | ETH |
| Optimism | 10 | ETH |
| BNB Chain | 56 | BNB |
| Gnosis | 100 | xDAI |
| Polygon | 137 | MATIC |
| zkSync Era | 324 | ETH |
| Base | 8453 | ETH |
| Arbitrum One | 42161 | ETH |
| Celo | 42220 | CELO |
| Avalanche | 43114 | AVAX |
| Linea | 59144 | ETH |

## Cache Expiry Defaults

| Data | Default Expiry |
|------|---------------|
| Token Lists | 7 days |
| Prices | 5 minutes |
| Coin Mappings | 7 days |
| Platforms | 30 days |

## Performance Tips

1. **Use `InitializeCacheAsync`** at startup to preload token lists
2. **Use event-based refresh** instead of full rescans for updates
3. **Batch price requests** using `BatchPriceService` for many tokens
4. **Use file cache** for persistence across restarts
5. **Limit parallel chains** to 3-5 to avoid RPC rate limits

## Dependencies

- `Nethereum.Web3`
- `Nethereum.DataServices` (CoinGecko API)
- `Nethereum.BlockchainProcessing` (Event scanning)
- `Microsoft.Extensions.DependencyInjection.Abstractions`

## License

MIT License - see the main Nethereum repository for details.
