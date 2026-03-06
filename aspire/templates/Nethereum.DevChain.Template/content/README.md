# Nethereum DevChain Aspire Solution

A complete, self-contained Ethereum development environment orchestrated with .NET Aspire. Spin up a local Ethereum node, blockchain indexer, and explorer UI with a single `dotnet run`.

Built on [Nethereum](https://github.com/Nethereum/Nethereum) — the .NET integration library for Ethereum.

## What's Included

| Service | Description |
|---------|-------------|
| **DevChain** | In-memory Ethereum node with full JSON-RPC support (EIP-1559, debug tracing, `eth_*`/`net_*`/`web3_*` methods) |
| **Indexer** | Background worker that crawls blocks, transactions, logs, token transfers (ERC-20/721/1155), token balances, and MUD World records into PostgreSQL |
| **Explorer** | Blazor Server blockchain explorer with block/transaction/log browsing, contract interaction (read/write via EIP-6963 wallet), ABI decoding, token pages, and MUD table viewer |
| **PostgreSQL** | Managed database container for all indexed blockchain data |

All services are wired together with Aspire service discovery, OpenTelemetry distributed tracing, health checks, and resilient HTTP clients.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (required for the PostgreSQL container)
- An IDE: Visual Studio 2022 17.12+, VS Code with C# Dev Kit, or JetBrains Rider 2024.3+

## Quick Start

### 1. Create a new project

```bash
dotnet new nethereum-devchain -n MyChain
cd MyChain
```

With custom parameters:

```bash
dotnet new nethereum-devchain -n MyChain \
    --NethereumVersion 6.0.0 \
    --ChainId 42069 \
    --AspireVersion 9.2.0
```

### 2. Set the dev account private key

The Explorer uses a pre-funded dev account for contract deployment and interaction. Set it via user secrets:

```bash
cd AppHost
dotnet user-secrets set "Parameters:devAccountPrivateKey" "5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a"
```

This is the default Hardhat/Foundry account #2 — use any funded account you prefer.

### 3. Run the solution

```bash
dotnet run --project AppHost
```

The Aspire dashboard opens automatically at `https://localhost:17178`. From there you can access:

- **Aspire Dashboard** — service health, logs, traces, metrics for all services
- **DevChain** — JSON-RPC endpoint (use with MetaMask, Foundry, Hardhat, or Nethereum)
- **Explorer** — full blockchain explorer UI
- **Indexer** — background processing (visible in dashboard logs)

### 4. Interact with your chain

Connect any Ethereum tool to the DevChain RPC URL shown in the Aspire dashboard:

```csharp
// Nethereum
var web3 = new Web3("http://localhost:<port>");
var balance = await web3.Eth.GetBalance.SendRequestAsync("0x...");
```

```bash
# Foundry
cast balance 0x... --rpc-url http://localhost:<port>
```

```bash
# curl
curl -X POST http://localhost:<port> \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"eth_blockNumber","params":[],"id":1}'
```

## Template Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `--NethereumVersion` | `6.0.0` | Nethereum NuGet package version for all services |
| `--ChainId` | `31337` | Chain ID for the DevChain node |
| `--AspireVersion` | `9.2.0` | .NET Aspire SDK and hosting package version |

## Architecture

```
AppHost (Aspire Orchestrator)
│
├── PostgreSQL (Docker container, managed by Aspire)
│   └── Databases: blockchain blocks/txs/logs, token transfers/balances, MUD records
│
├── DevChain (ASP.NET Core, JSON-RPC POST endpoint)
│   ├── In-memory or SQLite persistent storage
│   ├── Pre-funded dev accounts (10,000 ETH each)
│   ├── EIP-1559 transaction support
│   ├── Full debug_traceTransaction support
│   └── Configurable chain ID and block production
│
├── Indexer (Worker Service)
│   ├── Block/Transaction/Log processor → PostgreSQL
│   ├── Token transfer log processor (ERC-20/721/1155 Transfer events)
│   ├── Token balance aggregator (computes balances from transfers)
│   ├── MUD World record indexer (auto-discovers or configured address)
│   └── All processors run concurrently as hosted services
│
└── Explorer (Blazor Server)
    ├── Block list and detail pages
    ├── Transaction list, detail, and input decoding
    ├── Log list with event decoding
    ├── Account page (balance, transactions, code)
    ├── Contract interaction (read/write functions via EIP-6963 wallet)
    ├── Token pages (ERC-20/721/1155 transfers, balances, metadata)
    ├── MUD table browser (World addresses, table IDs, records)
    └── ABI resolution (Sourcify, local storage)
```

## Configuration

### DevChain

Add an `appsettings.json` to the DevChain project:

```json
{
  "DevChain": {
    "ChainId": 31337,
    "Storage": "sqlite",
    "AutoMine": true
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `ChainId` | `31337` | Ethereum chain ID |
| `Storage` | `"sqlite"` | `"sqlite"` for persistent (default), `"memory"` for ephemeral |
| `AutoMine` | `true` | Mine a block for each transaction |

### Indexer

The Indexer automatically connects to DevChain and PostgreSQL via Aspire service discovery. Optional settings:

```json
{
  "BlockchainProcessing": {
    "MinimumBlockConfirmations": 0,
    "BatchSize": 100
  },
  "MudProcessing": {
    "Address": "0xYourMudWorldAddress"
  }
}
```

Without a MUD address, the indexer auto-discovers MUD World contracts from `StoreSetRecord` events.

### Explorer

```json
{
  "Explorer": {
    "RpcUrl": "http://localhost:8545",
    "DevAccountPrivateKey": "...",
    "EnablePendingTransactions": true,
    "AbiSources": {
      "SourcifyEnabled": true,
      "LocalStorageEnabled": false,
      "LocalStoragePath": "./contracts/out"
    }
  }
}
```

## Extending the Solution

### Add a load generator

Create a new ASP.NET Core project, add `Nethereum.Web3` and `Nethereum.Contracts` packages, and reference it from the AppHost:

```csharp
// AppHost Program.cs
var loadgen = builder.AddProject<Projects.MyChain_LoadGenerator>("loadgenerator")
    .WithReference(devchain)
    .WaitFor(devchain);
```

### Add an ERC-4337 Bundler

```csharp
// Add Nethereum.AccountAbstraction.Bundler.RpcServer package to a new project
var bundler = builder.AddProject<Projects.MyChain_Bundler>("bundler")
    .WithReference(devchain)
    .WaitFor(devchain);
```

### Use in-memory storage

By default the DevChain persists state to SQLite. For ephemeral testing where you want a clean chain on every restart:

```json
{
  "DevChain": {
    "Storage": "memory"
  }
}
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| PostgreSQL container won't start | Ensure Docker Desktop is running |
| Explorer shows no data | Wait for the Indexer to process blocks (check Aspire dashboard logs) |
| Port conflicts | Aspire assigns dynamic ports — check the dashboard for actual URLs |
| DevChain not responding | Verify the service is healthy in the Aspire dashboard |

## Learn More

- [Nethereum Documentation](https://docs.nethereum.com)
- [Nethereum GitHub](https://github.com/Nethereum/Nethereum)
- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [MUD Framework](https://mud.dev)

## License

This template uses Nethereum, which is licensed under the MIT License.
