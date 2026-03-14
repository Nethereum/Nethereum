# Nethereum.DataServices

Client library for accessing external blockchain data APIs — Etherscan, Sourcify V2, CoinGecko, 4Byte Directory, and Chainlist. Includes a composite ABI retrieval system with automatic fallback across multiple sources.

## Installation

```bash
dotnet add package Nethereum.DataServices
```

## Problems This Library Solves

| Problem | Solution |
|---------|----------|
| "How do I get a contract's ABI without the source?" | Composite ABI lookup: Sourcify → Etherscan → 4Byte |
| "How do I verify a contract on Sourcify?" | `SourcifyApiServiceV2` with full V2 API support |
| "How do I decode unknown calldata?" | `FourByteDirectoryService` + `Sourcify4ByteSignatureService` |
| "How do I get token prices and metadata?" | `CoinGeckoApiService` with built-in caching |
| "How do I discover RPC endpoints for a chain?" | `ChainlistRpcApiService` |
| "How do I query Etherscan for gas prices?" | `EtherscanApiGasTrackerService` |

## Quick Start

### ABI Retrieval (Composite Pattern)

```csharp
using Nethereum.DataServices.ABIInfoStorage;

// Composite: cache → Sourcify → Etherscan → Sourcify4Byte → FourByteDirectory
var abiStorage = ABIInfoStorageFactory.CreateDefault(etherscanApiKey: "YOUR_KEY");

// Get full contract ABI
var abiInfo = await abiStorage.GetABIInfoAsync(chainId: 1, "0xdAC17F958D2ee523a2206206994597C13D831ec7");
Console.WriteLine($"Contract: {abiInfo.ContractName}, Functions: {abiInfo.FunctionABIs.Count}");

// Find a specific function by selector
var transfer = await abiStorage.FindFunctionABIFromInputDataAsync(
    chainId: 1,
    "0xdAC17F958D2ee523a2206206994597C13D831ec7",
    "0xa9059cbb0000000000000000000000001234...");
Console.WriteLine($"Function: {transfer.Name}");
```

### Factory Methods

| Method | Sources | Use Case |
|--------|---------|----------|
| `CreateDefault(etherscanApiKey, cache)` | Cache → Sourcify → Etherscan → Sourcify4Byte → FourByte | Production: full coverage |
| `CreateWithSourcifyOnly(cache)` | Cache → Sourcify → Sourcify4Byte → FourByte | No Etherscan key needed |
| `CreateWithEtherscanOnly(apiKey, cache)` | Cache → Etherscan → Sourcify4Byte → FourByte | Etherscan-preferred |
| `CreateLocalOnly(cache)` | Cache only | Offline / air-gapped |
| `CreateCustom(cache, storages...)` | Your choice | Custom fallback chain |

### Custom Fallback Chain

```csharp
var myStorage = ABIInfoStorageFactory.CreateCustom(
    cache: new ABIInfoInMemoryStorage(),
    new SourcifyABIInfoStorage(),
    new EtherscanABIInfoStorage("YOUR_KEY"),
    new FourByteDirectoryABIInfoStorage());
```

## Etherscan API

Full client for the Etherscan V2 unified API (supports all Etherscan-compatible chains via chain ID).

```csharp
using Nethereum.DataServices.Etherscan;

var etherscan = new EtherscanApiService(chain: 1, apiKey: "YOUR_KEY");
```

### Contracts

```csharp
// Get verified contract ABI
var abiResponse = await etherscan.Contracts.GetAbiAsync("0xdAC17F958D2ee523a2206206994597C13D831ec7");
string abi = abiResponse.Result;

// Get verified source code
var sourceResponse = await etherscan.Contracts.GetSourceCodeAsync("0xdAC17F958D2ee523a2206206994597C13D831ec7");
var source = sourceResponse.Result.First();
Console.WriteLine($"Name: {source.ContractName}, Compiler: {source.CompilerVersion}");

// Get contract creator and deployment tx
var creatorResponse = await etherscan.Contracts.GetContractCreatorAndCreationTxHashAsync("0xdAC17F958D2ee523a2206206994597C13D831ec7");
```

### Accounts

```csharp
// Get ETH balance
var balance = await etherscan.Accounts.GetBalanceAsync("0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045");

// Get multiple balances
var balances = await etherscan.Accounts.GetBalancesAsync(new[] { "0xAddr1...", "0xAddr2..." });

// Get transaction history (paginated)
var txns = await etherscan.Accounts.GetAccountTransactionsAsync("0xAddr...", page: 1, offset: 50);

// Get ERC-20 token transfers
var transfers = await etherscan.Accounts.GetTokenTransfersAsync("0xAddr...",
    contractAddress: "0xdAC17F958D2ee523a2206206994597C13D831ec7",
    startBlock: 18000000, endBlock: 19000000);

// Get ERC-721 and ERC-1155 transfers
var nfts = await etherscan.Accounts.GetErc721TransfersAsync("0xAddr...");
var erc1155 = await etherscan.Accounts.GetErc1155TransfersAsync("0xAddr...");

// Get historical balance at a specific block
var historicalBalance = await etherscan.Accounts.GetHistoricalBalanceAsync("0xAddr...", blockNumber: 18000000);

// Discover who funded an account
var fundedBy = await etherscan.Accounts.GetFundedByAsync("0xAddr...");
```

### Gas Tracker

```csharp
var gasOracle = await etherscan.GasTracker.GetGasOracleAsync();
Console.WriteLine($"Safe: {gasOracle.Result.SafeGasPrice} Gwei");
Console.WriteLine($"Propose: {gasOracle.Result.ProposeGasPrice} Gwei");
Console.WriteLine($"Fast: {gasOracle.Result.FastGasPrice} Gwei");

// Estimate confirmation time for a gas price
var estimate = await etherscan.GasTracker.GetEstimatedConfirmationTimeAsync(gasPriceInWei: 30000000000);
```

## Sourcify V2 API

Contract verification and ABI retrieval from the decentralised Sourcify repository.

```csharp
using Nethereum.DataServices.Sourcify;

var sourcify = new SourcifyApiServiceV2();
```

### Contract Lookup

```csharp
// Get full contract info (ABI, sources, compilation, proxy resolution)
var contract = await sourcify.GetContractAsync(chainId: 1, "0xdAC17F958D2ee523a2206206994597C13D831ec7");

// Get just the ABI
string abi = await sourcify.GetContractAbiAsync(chainId: 1, "0xdAC17F958D2ee523a2206206994597C13D831ec7");

// Check if contract is verified on a specific chain
var check = await sourcify.GetCheckByAddressAsync(chainId: 1, "0xdAC17F958D2ee523a2206206994597C13D831ec7");

// Check all chains for a contract
var allChains = await sourcify.GetContractAllChainsAsync("0xdAC17F958D2ee523a2206206994597C13D831ec7");

// List verified contracts on a chain
var contracts = await sourcify.GetContractsAsync(chainId: 1, limit: 50, sort: "desc");

// Get supported chains
var chains = await sourcify.GetChainsAsync();
```

### Contract Verification

```csharp
// Verify from Etherscan (easiest)
var result = await sourcify.VerifyFromEtherscanAsync(chainId: 1, "0xAddr...", apiKey: "ETHERSCAN_KEY");

// Verify with source files
var sourceFiles = new Dictionary<string, string>
{
    ["contracts/Token.sol"] = solSourceCode,
    ["metadata.json"] = metadataJson
};
var verifyResult = await sourcify.PostVerifyAsync(chainId: 1, "0xAddr...", sourceFiles);

// Check verification status
var status = await sourcify.GetVerificationStatusAsync(verifyResult.VerificationId);
```

### Proxy Resolution

The `SourcifyABIInfoStorage` automatically resolves proxy contracts and fetches the implementation ABI:

```csharp
var sourcifyStorage = new SourcifyABIInfoStorage(); // resolveProxies: true by default

// Automatically detects proxy and returns implementation ABI
var abiInfo = await sourcifyStorage.GetABIInfoAsync(chainId: 1, "0xProxyAddress...");
```

## 4Byte Directory Services

Two services for function/event signature lookups:

### FourByteDirectoryService (4byte.directory)

```csharp
using Nethereum.DataServices.FourByteDirectory;

var fourByte = new FourByteDirectoryService();

// Look up function by 4-byte selector
var result = await fourByte.GetFunctionSignatureByHexSignatureAsync("0xa9059cbb");
foreach (var sig in result.Results)
    Console.WriteLine(sig.TextSignature); // "transfer(address,uint256)"

// Look up event by topic hash
var events = await fourByte.GetEventSignatureByHexSignatureAsync("0xddf252ad...");

// Search by text
var search = await fourByte.GetFunctionSignatureByTextSignatureAsync("transfer");
```

### Sourcify4ByteSignatureService (Sourcify's 4byte API)

```csharp
using Nethereum.DataServices.Sourcify;

var sourcify4Byte = new Sourcify4ByteSignatureService();

// Look up function signature
var result = await sourcify4Byte.LookupFunctionAsync("0xa9059cbb");

// Look up event signature
var eventResult = await sourcify4Byte.LookupEventAsync("0xddf252ad...");

// Batch lookup (multiple signatures at once)
var batch = await sourcify4Byte.LookupAsync(
    functionSignatures: new[] { "0xa9059cbb", "0x095ea7b3" },
    eventSignatures: new[] { "0xddf252ad..." });

// Full-text search
var search = await sourcify4Byte.SearchAsync("transfer");
```

## CoinGecko API

Token metadata, pricing, and chain discovery with built-in configurable caching.

```csharp
using Nethereum.DataServices.CoinGecko;

var coinGecko = new CoinGeckoApiService(); // Default caching enabled
```

### Pricing

```csharp
// Get prices by CoinGecko IDs
var prices = await coinGecko.GetPricesAsync(new[] { "ethereum", "usd-coin" }, vsCurrency: "usd");
decimal ethPrice = prices["ethereum"]["usd"];

// Get token price by contract address
decimal? usdcPrice = await coinGecko.GetTokenPriceByContractAsync(
    platformId: "ethereum", "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48");
```

### Token Discovery

```csharp
// Get token list for a chain
var tokenList = await coinGecko.GetTokenListForChainAsync(chainId: 1);
foreach (var token in tokenList.Tokens.Take(5))
    Console.WriteLine($"{token.Symbol}: {token.Name} ({token.Address})");

// Find CoinGecko ID for a contract
string geckoId = await coinGecko.FindCoinGeckoIdAsync("0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", chainId: 1);

// Batch find IDs
var ids = await coinGecko.FindCoinGeckoIdsAsync(
    new[] { "0xA0b869...", "0xdAC17F..." }, chainId: 1);
```

### Chain/Platform Mapping

```csharp
// Get all supported platforms
var platforms = await coinGecko.GetAssetPlatformsAsync();

// Get platform ID for a chain
string platformId = await coinGecko.GetPlatformIdForChainAsync(chainId: 137); // "polygon-pos"
```

### Cache Configuration

```csharp
// Custom cache durations
var config = new CoinGeckoCacheConfiguration
{
    Enabled = true,
    PlatformsCacheDuration = TimeSpan.FromHours(24),
    CoinsListCacheDuration = TimeSpan.FromHours(1),
    TokenListCacheDuration = TimeSpan.FromHours(1),
    RateLimitDelay = TimeSpan.FromMilliseconds(1500)
};
var coinGecko = new CoinGeckoApiService(config);

// Disable caching entirely
var noCacheCoinGecko = new CoinGeckoApiService(CoinGeckoCacheConfiguration.Disabled);

// Clear cache manually
coinGecko.ClearCache();
```

## Chainlist RPC Discovery

Discover RPC endpoints for any EVM chain.

```csharp
using Nethereum.DataServices.Chainlist;

var chainlist = new ChainlistRpcApiService();

// Get all chains with their RPC endpoints
var allChains = await chainlist.GetAllChainsAsync();

// Get a specific chain
var polygon = await chainlist.GetChainByIdAsync(137);
Console.WriteLine($"Name: {polygon.Name}");
foreach (var rpc in polygon.Rpc)
    Console.WriteLine($"  RPC: {rpc}");
```

## Sourcify Parquet Exports

Download Sourcify's full verified contract dataset as Parquet files for local analysis.

```csharp
using Nethereum.DataServices.Sourcify;

var parquet = new SourcifyParquetExportService();

// List available tables
string[] tables = SourcifyParquetExportService.AvailableTables;
// "sourcify_matches", "verified_contracts", "sources", "compiled_contracts_sources",
// "compiled_contracts", "contract_deployments", "contracts", "code",
// "compiled_contracts_signatures", "signatures"

// List files for a table
var files = await parquet.ListTableFilesAsync("verified_contracts");

// Download a single file
await parquet.DownloadFileToPathAsync(files[0].Key, "/data/verified_contracts_0.parquet");

// Sync entire dataset to local directory (incremental via ETags)
var syncResult = await parquet.SyncToDirectoryAsync(
    "/data/sourcify-export",
    progress: new Progress<SourcifyParquetSyncProgress>(p =>
        Console.WriteLine($"Downloaded {p.FilesDownloaded}/{p.TotalFiles}")));

Console.WriteLine($"New: {syncResult.FilesDownloaded}, Skipped: {syncResult.FilesSkipped}");
```

## EVM Trace Decoding

Decode EVM execution traces using ABI information from any storage source:

```csharp
using Nethereum.DataServices.ABIInfoStorage;

var storage = ABIInfoStorageFactory.CreateDefault(etherscanApiKey: "YOUR_KEY");

// Decode a Program result with trace
var decoded = programResult.DecodeWithStorage(trace, callInput, chainId: 1, storage);

// Or use convenience methods
var decoded2 = program.DecodeWithSourcify(callInput, chainId: 1);
var decoded3 = program.Decode(callInput, chainId: 1, etherscanApiKey: "YOUR_KEY");
```

## Batch Signature Lookups

Look up multiple function/event signatures in a single call:

```csharp
var storage = ABIInfoStorageFactory.CreateWithSourcifyOnly();

// Batch function signature lookup
var functions = await storage.FindFunctionABIsBatchAsync(
    new[] { "0xa9059cbb", "0x095ea7b3", "0x23b872dd" });

// Batch event signature lookup
var events = await storage.FindEventABIsBatchAsync(
    new[] { "0xddf252ad...", "0x8c5be1e5..." });

// Combined batch lookup
var batch = await storage.FindABIsBatchAsync(
    functionSignatures: new[] { "0xa9059cbb" },
    eventSignatures: new[] { "0xddf252ad..." });
```

## Sourcify Repository Interface

`ISourcifyRepository` provides a database abstraction for storing Sourcify contract data locally. See [Nethereum.Sourcify.Database](../Nethereum.Sourcify.Database/README.md) for the PostgreSQL EF Core implementation.

```csharp
using Nethereum.DataServices.Sourcify.Database;

// Key operations:
await repository.AddCodeAsync(code);
await repository.AddContractAsync(contract);
await repository.AddDeploymentAsync(deployment);
await repository.GetVerifiedContractAsync(chainId: 1, addressBytes);

// Signature lookups
var sig = await repository.GetSignatureByHash4Async(selectorBytes);
var results = await repository.SearchSignaturesAsync("transfer");

// Bulk import
await repository.BulkInsertAsync(verifiedContracts);
```

## Architecture

```
Nethereum.DataServices/
├── ABIInfoStorage/
│   ├── ABIInfoStorageFactory.cs        # Factory for composite ABI lookup
│   ├── CompositeABIInfoStorage.cs      # Chains multiple sources with cache
│   ├── SourcifyABIInfoStorage.cs       # Sourcify V2 + proxy resolution
│   ├── EtherscanABIInfoStorage.cs      # Etherscan ABI retrieval
│   ├── FourByteDirectoryABIInfoStorage.cs  # 4byte.directory lookups
│   ├── Sourcify4ByteABIInfoStorage.cs  # Sourcify 4byte lookups (batch)
│   └── ProgramResultDecoderExtensions.cs   # EVM trace decoding helpers
├── Etherscan/
│   ├── EtherscanApiService.cs          # Main entry point
│   ├── EtherscanApiAccountsService.cs  # Account queries
│   ├── EtherscanApiContractsService.cs # ABI, source, verification
│   └── EtherscanApiGasTrackerService.cs# Gas oracle
├── Sourcify/
│   ├── SourcifyApiService.cs           # V1 API (legacy)
│   ├── SourcifyApiServiceV2.cs         # V2 API (recommended)
│   ├── Sourcify4ByteSignatureService.cs# Sourcify 4byte API
│   ├── SourcifyParquetExportService.cs # Parquet dataset downloads
│   └── Database/
│       ├── ISourcifyRepository.cs      # Repository interface
│       └── Models/                     # Code, Contract, Signature, etc.
├── CoinGecko/
│   └── CoinGeckoApiService.cs          # Prices, tokens, platforms
├── FourByteDirectory/
│   └── FourByteDirectoryService.cs     # 4byte.directory API
└── Chainlist/
    └── ChainlistRpcApiService.cs       # RPC endpoint discovery
```

## Dependencies

- `Nethereum.ABI`
- `Nethereum.EVM`
- `Nethereum.Util.Rest`

## License

MIT License — see the main Nethereum repository for details.
