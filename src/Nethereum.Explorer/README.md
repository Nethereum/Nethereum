# Nethereum.Explorer

Blazor Server blockchain explorer Razor Class Library for Ethereum-compatible chains with indexed storage, ABI decoding, EVM debugging, and wallet interaction.

## Overview

Nethereum.Explorer is a Razor Class Library (RCL) that provides a complete blockchain explorer UI as embeddable Blazor Server components. It reads from an indexed PostgreSQL database populated by a separate blockchain processor, and optionally connects to a live RPC endpoint for balance queries and contract interaction.

The Explorer is designed for progressive integration: at minimum it requires a blockchain storage database, and optionally adds token indexing, MUD World browsing, EVM debugging, and live RPC features as additional dependencies are registered.

### Key Features

- **Indexed block, transaction, account, contract, and log browsing** with pagination and CSV export
- **ABI decoding** of function inputs, event logs, and revert errors via configurable source chain (Sourcify, Etherscan, 4Byte, local filesystem)
- **Contract interaction** via EIP-6963 browser wallet (read functions without wallet, write functions with wallet connection)
- **EVM debugger** with opcode-level step-through, Solidity source mapping, memory/stack/storage inspection
- **Internal transactions** from indexed database with decoded call data and errors
- **Token views** for ERC-20/721/1155 balances, NFT inventory, and transfer history (requires token indexing repositories)
- **MUD World browser** with raw, decoded, and normalised SQL record views (requires MUD Postgres repositories)
- **Dark/light theme** with persistent toggle (localStorage)
- **Localisation** with English and Spanish translations, switchable from the navbar
- **Search** with address, transaction hash, block number, and ENS name resolution
- **Add chain to wallet** via EIP-6963 wallet provider from the home page

## Installation

```bash
dotnet add package Nethereum.Explorer
```

Requires `net10.0`. Uses EF Core 10 and `MapStaticAssets()`.

### Dependencies

- **Nethereum.BlockchainStore.EFCore** - `IBlockchainDbContextFactory` for indexed block/transaction/log/contract queries
- **Nethereum.BlockchainProcessing** - Block storage entity models and repository interfaces
- **Nethereum.Blazor** - EIP-6963 wallet interop and `EthereumAuthenticationStateProvider`
- **Nethereum.Blazor.Solidity** - EVM debugger components, Solidity code viewer, and debug service registration
- **Nethereum.DataServices** - ABI info storage implementations (Sourcify, Etherscan, 4Byte)
- **Nethereum.Web3** - JSON-RPC client for live RPC queries
- **Nethereum.Mud.Repositories.EntityFramework** - MUD store record DB sets interface

## Integration

### Minimum Setup (Indexed Storage)

Requires `Nethereum.BlockchainStore.Postgres` to provide `IBlockchainDbContextFactory`. This enables block, transaction, account, contract, and log browsing from indexed data.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddPostgresBlockchainStorage(connectionString);
builder.Services.AddExplorerServices(builder.Configuration);

var app = builder.Build();

app.UseRateLimiter();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<Nethereum.Explorer.Components.App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(Nethereum.Blazor.EIP6963WalletInterop.EIP6963WalletBlazorInterop).Assembly);

app.MapTokenApiEndpoints();
app.MapContractApiEndpoints();

app.Run();
```

The database must be populated by a separate blockchain processor/indexer. Without indexed data, pages will be empty.

### Adding Token Indexing

Register `Nethereum.BlockchainStorage.Token.Postgres` to enable token balances, NFT inventory, and transfer history pages. The Explorer checks at runtime for `ITokenBalanceRepository`, `INFTInventoryRepository`, `ITokenTransferLogRepository`, and `ITokenMetadataRepository`. If all four are present, `TokenExplorerService` is used. If any are missing, `NullTokenExplorerService` is registered instead (returns empty results, `IsAvailable = false`).

```csharp
builder.Services.AddPostgresBlockchainStorage(connectionString);
builder.Services.AddTokenPostgresRepositories(connectionString);
builder.Services.AddExplorerServices(builder.Configuration);
```

### Adding MUD World Browser

Register `Nethereum.Mud.Repositories.Postgres` to enable MUD World record browsing. The `MudExplorerService` checks for `IMudStoreRecordsDbSets` at runtime; if null, `IsAvailable` returns false and MUD pages show empty state.

```csharp
builder.Services.AddDbContext<MudPostgresStoreRecordsDbContext>(options =>
    options.UseNpgsql(connectionString).UseLowerCaseNamingConvention());

builder.Services.AddScoped<IMudStoreRecordsDbSets>(sp =>
    sp.GetRequiredService<MudPostgresStoreRecordsDbContext>());

builder.Services.AddTransient<INormalisedTableQueryService>(sp =>
{
    var conn = new NpgsqlConnection(connectionString);
    var logger = sp.GetService<ILogger<NormalisedTableQueryService>>();
    return new NormalisedTableQueryService(conn, logger);
});
```

### Adding RPC

Configure an RPC endpoint to enable live balance/nonce queries, contract interaction, state diffs, and pending transactions.

The `ExplorerWeb3Factory` resolves the RPC URL in this order:
1. `ExplorerOptions.RpcUrl` (from `Explorer:RpcUrl` in configuration)
2. `ConnectionStrings:devchain`

```json
{
  "Explorer": {
    "RpcUrl": "http://localhost:8545"
  }
}
```

Without RPC, account pages show only database-stored state and contract write functions are unavailable.

## Pages

| Route | Page | Description |
|-------|------|-------------|
| `/` | Home | Latest blocks and transactions, chain stats (block height, chain ID), add-chain-to-wallet button, 5-second auto-refresh |
| `/blocks` | Blocks | Paginated block list with gas usage percentage, validator, age. CSV export |
| `/block/{BlockNumberOrHash}` | Block Detail | Block header fields, state/receipts roots, prev/next navigation, JSON toggle, transaction list |
| `/transactions` | Transactions | Paginated transaction list. CSV export |
| `/transaction/{TxHash}` | Transaction Detail | Status, gas metrics, decoded input, event logs, token transfers, internal transactions (from indexed DB), blob data (type 3), EIP-7702 authorizations (type 4), state diffs via PrestateTracer (if `EnableTracing` and RPC available) |
| `/transaction/{TxHash}/debug` | EVM Debugger | Opcode-level step-through execution with Solidity source mapping, memory/stack/storage inspection, call graph, and revert analysis. Requires `EnableEvmDebugger` and RPC |
| `/pending` | Pending Transactions | Pending and queued transactions from `txpool_content` (requires `EnablePendingTransactions` and RPC) |
| `/accounts` | Accounts | Paginated address list with transaction counts, EOA/contract badges |
| `/account/{Address}` | Account Detail | Balance, nonce, QR code, transaction history with direction filter (all/in/out/self), internal transactions tab, token balances (if indexed), NFT inventory |
| `/account/{Address}/tokens` | Account Tokens | Tabbed view: token balances, NFT inventory, token transfer history |
| `/contracts` | Contracts | Paginated contract list with creator address and creation transaction |
| `/contract/{Address}` | Contract Detail | ABI-decoded read/write functions, event log history, bytecode, source code (if available from ABI source) |
| `/mud` | MUD Worlds | World address cards with table and record counts |
| `/mud/tables/{Address?}` | MUD Tables | Table cards with key/value field lists, resource type badges, normalised SQL indicator |
| `/mud/records/{Address?}/{TableId?}` | MUD Records | Three view modes: raw hex, decoded (schema-aware), normalised SQL. Query builder with filters and ordering |

## UI Features

### Search

The search bar in the navbar resolves multiple input types:
- **Block number** - numeric input navigates to block detail
- **Transaction hash** - 66-character hex string navigates to transaction detail
- **Address** - 42-character hex string navigates to account detail
- **ENS name** - `.eth` domain names are resolved via ENS registry when RPC is available

Recent searches are tracked per session and shown in a dropdown for quick access.

### Theme

A dark/light theme toggle is available in the navbar. The selected theme persists to `localStorage` under the key `explorer-theme`. All components including the Monaco editor (used by the EVM debugger) adapt to the selected theme.

### Localisation

The Explorer supports English (`en`) and Spanish (`es`) via the `ExplorerLocalizer` service. A language selector dropdown in the navbar allows switching at runtime. All page titles, labels, table headers, error messages, and empty states are localised.

### EVM Debugger

The transaction detail page includes a "Debug" button (when `EnableEvmDebugger` is true and RPC is available) that navigates to a full EVM debugger:
- Replays the transaction via `debug_traceTransaction` with an opcode tracer
- Displays opcodes grouped by function with call depth indentation
- Highlights the corresponding Solidity source line when source maps are available (via Sourcify or `AbiSources.SourceBasePath`)
- Inspects memory, stack, and storage at each execution step
- Shows gas cost per instruction and call information (target contract, function selector)
- Supports filtering to show only calls, events, and storage operations

### Wallet Integration

The Explorer uses EIP-6963 for browser wallet discovery. When a wallet is connected:
- Write functions on the contract detail page send transactions through the wallet
- The home page shows an "Add to Wallet" button that registers the chain via `wallet_addEthereumChain`
- Authentication state is provided by `EthereumAuthenticationStateProvider`

## Configuration

Bound from `IConfiguration` section `"Explorer"` into `ExplorerOptions`:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RpcUrl` | `string?` | `null` | RPC endpoint. Falls back to `ConnectionStrings:devchain` |
| `ChainName` | `string` | `"DevChain"` | Chain display name |
| `ChainId` | `long` | `31337` | Network chain ID |
| `CurrencySymbol` | `string` | `"ETH"` | Native currency symbol |
| `CurrencyName` | `string` | `"Ether"` | Native currency name |
| `CurrencyDecimals` | `uint` | `18` | Native currency decimals |
| `BlockExplorerUrl` | `string?` | `null` | External block explorer link |
| `ExplorerTitle` | `string` | `"Nethereum Explorer"` | Browser page title |
| `ExplorerBrandName` | `string` | `"Nethereum"` | Navbar brand name |
| `ExplorerBrandSuffix` | `string` | `"Explorer"` | Navbar brand suffix |
| `LogoUrl` | `string?` | `null` | Logo image URL |
| `FaviconUrl` | `string?` | `null` | Favicon URL |
| `ApiKey` | `string?` | `null` | Optional dev-time guard for ABI upload POST endpoints (`X-Api-Key` header). If null, POST endpoints are open |
| `EnableMud` | `bool` | `true` | Show MUD World browser pages |
| `EnableTokens` | `bool` | `true` | Show token sections on account pages |
| `EnableTracing` | `bool` | `true` | Show state diff card on transaction detail (requires RPC with debug namespace) |
| `EnableInternalTransactions` | `bool` | `true` | Show internal transaction views |
| `EnablePendingTransactions` | `bool` | `false` | Show pending transaction page |
| `EnableEvmDebugger` | `bool` | `true` | Show EVM debugger button on transaction detail (requires RPC with debug namespace) |
| `RpcRequestTimeoutSeconds` | `int` | `30` | RPC call timeout |

Pagination uses a fixed page size of 25 items, clamped to a maximum of 100 for API requests (defined in `ExplorerConstants`).

### ABI Source Options (`AbiSources` sub-section)

ABI resolution uses a composite chain. Sources are checked in order; results are cached in a singleton in-memory cache shared across all Blazor circuits.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SourcifyEnabled` | `bool` | `true` | Query Sourcify for verified contract metadata |
| `FourByteEnabled` | `bool` | `false` | Query 4Byte Directory for function signatures |
| `EtherscanEnabled` | `bool` | `false` | Query Etherscan for contract ABI |
| `EtherscanApiKey` | `string?` | `null` | Required when `EtherscanEnabled` is true |
| `LocalStorageEnabled` | `bool` | `false` | Read/write ABIs from local filesystem |
| `LocalStoragePath` | `string?` | `null` | Directory path for local ABI storage |
| `SourceBasePath` | `string?` | `null` | Base path for Solidity source files used by the EVM debugger for source mapping |

Resolution order: in-memory cache, local filesystem (if enabled, highest priority), Sourcify, Etherscan, 4Byte. Falls back to local-only if no sources are configured.

## API Endpoints

All endpoints are rate-limited: 100 requests per minute, fixed window, 429 on rejection.

### Token API (`/api/tokens`)

| Method | Route | Parameters | Description |
|--------|-------|------------|-------------|
| GET | `/{address}/balances` | - | Token balances for address |
| GET | `/{address}/nfts` | - | NFT inventory for address |
| GET | `/{address}/transfers` | `page`, `pageSize` | Token transfer history for address |
| GET | `/contract/{contractAddress}/transfers` | `page`, `pageSize` | Transfers for a token contract |
| GET | `/contract/{contractAddress}/metadata` | - | Token name, symbol, decimals, type |

Returns `503 { error: "Token indexing not configured" }` if `ITokenExplorerService.IsAvailable` is false.

### Contract API (`/api/contracts`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/{address}/abi` | Retrieve stored ABI for a contract |
| POST | `/{address}/abi` | Upload ABI for a contract. Body: `{ "abi": "...", "name": "..." }` |
| POST | `/batch` | Batch upload (max 50). Body: `{ "contracts": [{ "address": "...", "abi": "...", "name": "..." }] }` |

POST endpoints validate ABI JSON via `ABIDeserialiserFactory.DeserialiseContractABI()`. If `ExplorerOptions.ApiKey` is set, POST requests must include a matching `X-Api-Key` header (simple ordinal string check, intended as a dev-time guard — not a production auth mechanism). If `ApiKey` is null (the default), POST endpoints are open.

## Aspire Integration

The Aspire Explorer host resolves RPC URL via service discovery (`services:devchain:http:0` or `services:devchain:https:0`) and connection strings from Aspire resources:

```csharp
// AppHost
var postgres = builder.AddPostgres("postgres").AddDatabase("nethereumdb");
var devchain = builder.AddProject<Projects.DevChainServer>("devchain");

builder.AddProject<Projects.ExplorerHost>("explorer")
    .WithReference(postgres)
    .WithReference(devchain)
    .WithExternalHttpEndpoints();
```

```csharp
// Explorer host
builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("nethereumdb");
builder.Services.AddPostgresBlockchainStorage(connectionString);
builder.Services.AddTokenPostgresRepositories(connectionString);
builder.Services.AddExplorerServices(builder.Configuration);
```

## Related Packages

### Required

- **Nethereum.BlockchainStore.Postgres** - Provides `IBlockchainDbContextFactory` via `AddPostgresBlockchainStorage()`

### Optional

- **Nethereum.BlockchainStorage.Token.Postgres** - Token balance, NFT, and transfer repositories via `AddTokenPostgresRepositories()`
- **Nethereum.Mud.Repositories.Postgres** - MUD World store records DB context and normalised table queries

### See Also

- [Nethereum.Blazor](../Nethereum.Blazor/README.md) - EIP-6963 wallet interop and Ethereum authentication provider
- [Nethereum.Blazor.Solidity](../Nethereum.Blazor.Solidity/README.md) - EVM debugger components and Solidity code viewer
- [Nethereum.DataServices](../Nethereum.DataServices/README.md) - ABI info storage implementations (Sourcify, Etherscan, 4Byte)

## Additional Resources

- [Nethereum Documentation](https://docs.nethereum.com)
- [MUD Framework](https://mud.dev)
- [EIP-6963: Multi Injected Provider Discovery](https://eips.ethereum.org/EIPS/eip-6963)
