---
name: aspire-devchain
description: Help users create and run a local Ethereum development environment with .NET Aspire — DevChain node, blockchain indexer, Blazor explorer, and PostgreSQL. Use this skill when the user mentions Aspire DevChain template, nethereum-devchain, creating a local blockchain environment with Aspire, setting up DevChain with an explorer and indexer, or wants a single-command Ethereum dev environment with .NET. Also trigger when users mention dotnet new nethereum-devchain or Nethereum.Aspire.TemplatePack.
user-invocable: true
---

# Nethereum DevChain Aspire Template

The `nethereum-devchain` template creates a complete local Ethereum development environment orchestrated by .NET Aspire — a DevChain node, blockchain indexer, Blazor explorer, and PostgreSQL database, all wired together with service discovery, telemetry, and health checks.

## When to Use This

- Setting up a local Ethereum development environment quickly
- Need a blockchain explorer for local development
- Want an indexed local chain (blocks, transactions, token transfers in PostgreSQL)
- Replacing Docker Compose setups for Ethereum dev infrastructure
- Setting up infrastructure before building a dApp (see `aspire-dapp` for full-stack)

## Install & Create

```bash
# Install the template pack (once)
dotnet new install Nethereum.Aspire.TemplatePack

# Create a project
dotnet new nethereum-devchain -n MyChain
cd MyChain

# Set the dev account private key
cd AppHost
dotnet user-secrets set "Parameters:devAccountPrivateKey" "5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a"
cd ..

# Run
dotnet run --project AppHost
```

The Aspire dashboard opens at `https://localhost:17178` with all services visible.

## Template Parameters

```bash
dotnet new nethereum-devchain -n MyChain \
    --NethereumVersion 6.1.0 \
    --ChainId 42069 \
    --AspireVersion 13.1.1
```

| Parameter | Default | Description |
|-----------|---------|-------------|
| `--NethereumVersion` | `6.1.0` | Nethereum NuGet package version |
| `--ChainId` | `31337` | Chain ID for the DevChain |
| `--AspireVersion` | `13.1.1` | .NET Aspire SDK version |

## What's Included

| Service | Description |
|---------|-------------|
| **DevChain** | In-memory Ethereum node (EIP-1559, debug tracing, pre-funded accounts) |
| **Indexer** | Indexes blocks, transactions, token transfers (ERC-20/721/1155), MUD records into PostgreSQL |
| **Explorer** | Blazor blockchain explorer with contract interaction, ABI decoding, wallet connection |
| **PostgreSQL** | Managed Docker container for all indexed data |

## Project Structure

```
MyChain/
├── AppHost/          Aspire orchestrator
├── DevChain/         Ethereum node
├── Indexer/          Block indexer → PostgreSQL
├── Explorer/         Blazor blockchain explorer
└── ServiceDefaults/  Shared Aspire config
```

## Prerequisites

- .NET 10 SDK or later
- Docker Desktop (for PostgreSQL)

## Configuration

### DevChain Storage

```json
{
  "DevChain": {
    "ChainId": 31337,
    "Storage": "sqlite",
    "AutoMine": true
  }
}
```

Set `"Storage": "memory"` for ephemeral chains that reset on restart.

## Connect Tools

```csharp
// Nethereum
var web3 = new Web3("http://localhost:<port>");
var balance = await web3.Eth.GetBalance.SendRequestAsync("0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266");
```

```bash
# Foundry
cast balance 0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266 --rpc-url http://localhost:<port>
```

## Troubleshooting

- **PostgreSQL unhealthy**: `docker volume rm nethereum-pgdata` then restart
- **Port conflicts**: Aspire assigns dynamic ports — check the dashboard

## Related Skills

- `devchain` — DevChain library details (configuration, forking, RPC methods, testing patterns)
- `aspire-dapp` — Full-stack dApp template (adds WebApp, Solidity contracts, wallet integration)
- `blockchain-explorer` — Explorer features in depth
- `blockchain-indexing` — Indexer configuration and customisation

For full documentation, see: https://docs.nethereum.com/docs/aspire-templates/guide-devchain-template
