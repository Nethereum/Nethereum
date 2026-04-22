---
name: blockchain-explorer
description: "Embed a full Blazor blockchain explorer with block/transaction browsing, ABI-decoded contract interaction, token pages, EVM debugger, and MUD World browser (.NET/C#). Use when the user asks about blockchain explorer, block explorer, embedding explorer UI, contract interaction UI, EVM debugger, or MUD browser."
user-invocable: true
---

# Blockchain Explorer

Nethereum.Explorer is a Blazor Server Razor Class Library (RCL) that provides a complete blockchain explorer for Ethereum-compatible chains. It renders blocks, transactions, accounts, contracts, tokens, NFTs, internal transactions, event logs, an EVM debugger, and a MUD World browser — all with ABI decoding and wallet integration via EIP-6963.

NuGet: `Nethereum.Explorer`

```bash
dotnet add package Nethereum.Explorer
```

## DI Setup

Register explorer services in `Program.cs`:

```csharp
using Nethereum.Explorer.Services;
using Nethereum.BlockchainStore.Postgres;
using Nethereum.BlockchainStorage.Token.Postgres;

var builder = WebApplication.CreateBuilder(args);

// Core explorer services (required)
builder.Services.AddExplorerServices(builder.Configuration);

// Blockchain storage — provides block/transaction/contract repositories
var connString = builder.Configuration.GetConnectionString("PostgresConnection")!;
builder.Services.AddPostgresBlockchainStorage(connString);

// Token storage — provides token balances, NFT inventory, transfer logs, metadata
builder.Services.AddTokenPostgresRepositories(connString);

var app = builder.Build();

// Map API endpoints
app.MapTokenApiEndpoints();
app.MapContractApiEndpoints();
app.UseRateLimiter();

app.Run();
```

Without `AddPostgresBlockchainStorage`, the explorer falls back to direct RPC queries. Without `AddTokenPostgresRepositories`, token pages show a "Token indexing not configured" message via `NullTokenExplorerService`.

## Configuration

All options are bound from `appsettings.json` section `Explorer`:

```json
{
  "Explorer": {
    "RpcUrl": "http://localhost:8545",
    "ChainName": "DevChain",
    "ChainId": 31337,
    "CurrencySymbol": "ETH",
    "CurrencyName": "Ether",
    "CurrencyDecimals": 18,
    "BlockExplorerUrl": null,

    "ExplorerTitle": "Nethereum Explorer",
    "ExplorerBrandName": "Nethereum",
    "ExplorerBrandSuffix": "Explorer",
    "LogoUrl": null,
    "FaviconUrl": null,

    "ApiKey": null,

    "EnableMud": true,
    "EnableTokens": true,
    "EnableTracing": true,
    "EnableInternalTransactions": true,
    "EnablePendingTransactions": false,
    "EnableEvmDebugger": true,

    "RpcRequestTimeoutSeconds": 30,

    "AbiSources": {
      "SourcifyEnabled": true,
      "FourByteEnabled": false,
      "EtherscanEnabled": false,
      "EtherscanApiKey": null,
      "LocalStorageEnabled": false,
      "LocalStoragePath": null,
      "SourceBasePath": null
    }
  }
}
```

### Key ExplorerOptions Properties

| Property | Default | Purpose |
|----------|---------|---------|
| `RpcUrl` | `null` | JSON-RPC endpoint for the target chain |
| `ChainName` / `ChainId` | `"DevChain"` / `31337` | Chain display name and EIP-155 ID |
| `CurrencySymbol` / `CurrencyName` | `"ETH"` / `"Ether"` | Native currency ticker and name |
| `ApiKey` | `null` | Protects write API endpoints (X-Api-Key header) |
| `EnableMud` | `true` | Show MUD World browser pages |
| `EnableTokens` | `true` | Show token balance and transfer pages |
| `EnableTracing` | `true` | Enable transaction tracing (requires `debug_traceTransaction` RPC support) |
| `EnableEvmDebugger` | `true` | Enable EVM step debugger |
| `RpcRequestTimeoutSeconds` | `30` | RPC call timeout |

Branding properties (`ExplorerTitle`, `ExplorerBrandName`, `ExplorerBrandSuffix`, `LogoUrl`, `FaviconUrl`) and remaining feature toggles (`EnableInternalTransactions`, `EnablePendingTransactions`) are also available -- see the JSON example above for all options.

### AbiSourceOptions

ABI sources are chained as a composite: in-memory cache is checked first, then each enabled source in order (local -> Sourcify -> Etherscan -> 4Byte). Key options: `SourcifyEnabled` (default `true`), `EtherscanEnabled` (default `false`, requires `EtherscanApiKey`), `FourByteEnabled` (default `false`), `LocalStorageEnabled` (default `false`, requires `LocalStoragePath`).

## Features

- **Block browsing** — paginated block list, block detail with transactions
- **Transaction browsing** — paginated transaction list, full transaction detail with decoded input data, event logs, gas breakdown, state diffs, blob data, authorization lists
- **Pending transactions** — live pending transaction feed (when enabled)
- **Account pages** — balance, transaction history with direction filtering, internal transactions, token holdings, NFT inventory
- **Contract interaction** — ABI-decoded read/write functions, event log decoding, contract detail with source metadata
- **Token pages** — ERC-20 balances, ERC-721/ERC-1155 NFT inventory, transfer history per address or contract
- **Internal transactions** — decoded internal calls with method signatures
- **EVM step debugger** — opcode-level transaction trace with stack, memory, and storage inspection
- **MUD World browser** — list MUD worlds, browse tables, view decoded records, query normalised tables with filters
- **Global search** — resolves addresses, transaction hashes, block numbers, ENS names
- **Recent searches** — client-side search history
- **CSV export** — export transaction and token data
- **QR codes** — address QR code display
- **Wallet integration** — EIP-6963 wallet discovery, authentication state, send transactions from contract interaction UI
- **Localization** — English and Spanish translations
- **Rate limiting** — built-in fixed-window rate limiter on API endpoints (100 req/min)
- **Toast notifications** — success/error feedback

## Pages Reference

| Route | Page | Features |
|-------|------|----------|
| `/` | Home | Latest blocks table, latest transactions table, chain stats |
| `/blocks` | Blocks | Paginated block list |
| `/block/{BlockNumberOrHash}` | BlockDetail | Block header, transactions in block |
| `/transactions` | Transactions | Paginated transaction list |
| `/transaction/{TxHash}` | TransactionDetail | Overview, gas, input data, event logs, token transfers, state diff, blob data, authorization list |
| `/transaction/{TxHash}/debug` | TransactionDebug | EVM step debugger with opcode trace |
| `/pending` | PendingTransactions | Live pending transaction feed |
| `/accounts` | Accounts | Account listing |
| `/account/{Address}` | Account | Balance, transactions, internal txs, contract info |
| `/account/{Address}/tokens` | AccountTokens | Token balances, NFT inventory |
| `/contracts` | Contracts | Contract listing |
| `/contract/{Address}` | ContractDetail | ABI-decoded read/write, events, source metadata |
| `/mud` | MudWorlds | List of MUD World addresses |
| `/mud/tables/{Address?}` | MudTables | Tables for a MUD World with schema info |
| `/mud/records/{Address}/{TableId}` | MudRecords | Decoded records, normalised table queries |

## API Endpoints

### Token API (`/api/tokens`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/{address}/balances` | Token balances for an address |
| GET | `/{address}/nfts` | NFT inventory for an address |
| GET | `/{address}/transfers?page=1&pageSize=25` | Token transfers for an address |
| GET | `/contract/{contractAddress}/transfers?page=1&pageSize=25` | Transfers for a token contract |
| GET | `/contract/{contractAddress}/metadata` | Token metadata (name, symbol, decimals, type) |

### Contract API (`/api/contracts`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/{address}/abi` | Retrieve cached ABI for a contract |
| POST | `/{address}/abi` | Upload ABI for a contract (requires `X-Api-Key` if configured) |
| POST | `/batch` | Batch upload ABIs (max 50, requires `X-Api-Key` if configured) |

Upload ABI example:

```bash
curl -X POST http://localhost:5000/api/contracts/0x1234.../abi \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-key" \
  -d '{"abi": "[{\"type\":\"function\",...}]", "name": "MyContract"}'
```

Batch upload:

```bash
curl -X POST http://localhost:5000/api/contracts/batch \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-key" \
  -d '{"contracts": [{"address": "0x...", "abi": "[...]", "name": "Token"}, ...]}'
```

## Branding Customization

Set branding via `ExplorerOptions`:

```json
{
  "Explorer": {
    "ExplorerTitle": "My Chain Explorer",
    "ExplorerBrandName": "MyChain",
    "ExplorerBrandSuffix": "Explorer",
    "LogoUrl": "/images/logo.svg",
    "FaviconUrl": "/images/favicon.ico",
    "CurrencySymbol": "MYC",
    "CurrencyName": "MyCoin",
    "ChainName": "MyChain Mainnet"
  }
}
```

## MUD Browser Setup

The MUD browser requires MUD Entity Framework repositories. Register them alongside the explorer:

```csharp
using Nethereum.Mud.Repositories.EntityFramework;

builder.Services.AddMudPostgresRepositories(connString);
```

The explorer's `IMudExplorerService` automatically detects whether MUD repositories are available. When `EnableMud` is `true` and repositories are registered, the MUD nav items and pages appear. The browser supports:

- Listing all MUD World contract addresses
- Browsing tables per World with key/value field schemas
- Viewing raw and decoded records
- Querying normalised tables with field-level filter conditions (equals, greater than, less than, contains)
- Schema introspection via on-chain `getSchema` calls

## Core Services

| Service | Interface | Purpose |
|---------|-----------|---------|
| `BlockQueryService` | `IBlockQueryService` | Fetch blocks and block transactions |
| `TransactionQueryService` | `ITransactionQueryService` | Fetch transactions and receipts |
| `AccountQueryService` | `IAccountQueryService` | Fetch account balance and code |
| `ContractQueryService` | `IContractQueryService` | Contract metadata and interaction |
| `LogQueryService` | `ILogQueryService` | Event log queries |
| `InternalTransactionQueryService` | `IInternalTransactionQueryService` | Internal (trace) transactions |
| `TransactionTraceService` | `ITransactionTraceService` | EVM opcode-level tracing |
| `RpcQueryService` | `IRpcQueryService` | Direct RPC calls |
| `AbiStorageService` | `IAbiStorageService` | ABI retrieval and caching |
| `AbiDecodingService` | `IAbiDecodingService` | Decode input data and logs using ABI |
| `TokenExplorerService` | `ITokenExplorerService` | Token balances, NFTs, transfers |
| `MudExplorerService` | `IMudExplorerService` | MUD World browsing and record queries |
| `SearchResolverService` | `ISearchResolverService` | Resolve search input to entity type |
| `ExplorerChainService` | — | Chain metadata and connection state |
| `ExplorerWeb3Factory` | — | Create Web3 instances from configured RPC |

All query services are registered as scoped. ABI storage uses a singleton composite chain with in-memory cache.

## Minimal Standalone Example

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddExplorerServices(builder.Configuration);

var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseRateLimiter();
app.MapTokenApiEndpoints();
app.MapContractApiEndpoints();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
```

With `appsettings.json`:

```json
{
  "Explorer": {
    "RpcUrl": "http://localhost:8545",
    "ChainName": "Local DevChain",
    "ChainId": 31337
  }
}
```

This gives a fully functional explorer against a local dev chain with Sourcify ABI resolution, no database required. Add `AddPostgresBlockchainStorage` and `AddTokenPostgresRepositories` for indexed data.

### Validate Setup

After starting the application, verify the home page loads and displays chain stats:

```
GET http://localhost:5000/
```

The home page should show the latest blocks table, latest transactions table, and chain stats (chain name, chain ID, currency symbol). If the page loads but shows no data, confirm the RPC endpoint is reachable and returning blocks.

## Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| Home page shows no blocks | RPC endpoint unreachable or wrong URL | Verify `Explorer:RpcUrl` in `appsettings.json`; test with `curl -X POST -H "Content-Type: application/json" -d '{"jsonrpc":"2.0","method":"eth_blockNumber","params":[],"id":1}' <RPC_URL>` |
| "Token indexing not configured" message | `AddTokenPostgresRepositories` not registered | Add `builder.Services.AddTokenPostgresRepositories(connString)` in `Program.cs` |
| ABI not decoded for contract | No ABI source found the contract | Enable additional ABI sources (Etherscan, local storage) in `AbiSources` config; upload ABI via POST `/api/contracts/{address}/abi` |
| MUD pages not visible | MUD repositories not registered or `EnableMud` is false | Register `AddMudPostgresRepositories(connString)` and set `EnableMud: true` |
| 500 errors on transaction detail | Tracing not supported by RPC node | Set `EnableTracing: false` and `EnableInternalTransactions: false` if the RPC node does not support `debug_traceTransaction` |
