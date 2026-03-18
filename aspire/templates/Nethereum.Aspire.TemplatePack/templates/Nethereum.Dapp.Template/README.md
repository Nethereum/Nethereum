# NethereumDapp — Complete dApp Development Environment

A full-stack Ethereum dApp template orchestrated with .NET Aspire. Write Solidity, generate C# typed contract services, test with embedded DevChain, explore with a Blazor explorer, and interact with a web UI — all out of the box.

Built on [Nethereum](https://github.com/Nethereum/Nethereum) — the .NET integration library for Ethereum.

## What's Included

| Service | Description |
|---------|-------------|
| **DevChain** | In-memory Ethereum node with JSON-RPC, EIP-1559, debug tracing |
| **Indexer** | Background worker indexing blocks, transactions, token transfers (ERC-20/721/1155) into PostgreSQL |
| **Explorer** | Blazor blockchain explorer with contract interaction, ABI decoding, Solidity source mapping |
| **WebApp** | Blazor Server dApp with EIP-6963 wallet integration, token deploy/mint/transfer UI |
| **LoadGenerator** | Worker service generating test transactions (mint, transfer, ETH send) |
| **ContractServices** | Pre-generated C# typed contract access from Solidity ABI |
| **Tests** | Fast TDD tests with embedded DevChain (no Docker needed) |
| **IntegrationTests** | E2E tests against running AppHost |
| **contracts/** | Foundry/Forge project with starter ERC20 contract, tests, and deployment script |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for PostgreSQL container)
- An IDE: Visual Studio 2022 17.12+, VS Code with C# Dev Kit, or JetBrains Rider 2024.3+
- [Foundry](https://getfoundry.sh/) (optional — for Solidity compilation and Forge tests)

## Quick Start

### 1. Create a new project

```bash
dotnet new nethereum-dapp -n MyDapp
cd MyDapp
```

### 2. Set the dev account private key

```bash
cd AppHost
dotnet user-secrets set "Parameters:devAccountPrivateKey" "5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a"
cd ..
```

### 3. Run TDD tests (no Docker needed)

```bash
dotnet test Tests/
```

These tests use an embedded DevChain — no Docker, no Aspire, pure in-process.

### 4. Run the full stack

```bash
dotnet run --project AppHost
```

The Aspire dashboard opens at `https://localhost:17178`. From there access:

- **DevChain** — JSON-RPC endpoint
- **Explorer** — blockchain explorer with decoded contract calls and Solidity source
- **WebApp** — dApp UI to deploy tokens, mint, transfer, check balances
- **LoadGenerator** — generating test transactions (visible in dashboard logs)

### 5. Open the WebApp

Navigate to the WebApp URL in the Aspire dashboard. Deploy a token, mint, transfer, and check balances.

### 6. Run E2E tests (with AppHost running)

```bash
dotnet test IntegrationTests/
```

## Template Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `--NethereumVersion` | `6.0.4` | Nethereum NuGet package version |
| `--ChainId` | `31337` | Chain ID for the DevChain |
| `--AspireVersion` | `13.1.1` | .NET Aspire SDK version |

## Architecture

```
AppHost (Aspire Orchestrator)
│
├── PostgreSQL (Docker container)
│
├── DevChain (JSON-RPC Ethereum node)
│
├── Indexer (Worker → PostgreSQL)
│
├── Explorer (Blazor Server + ABI decoding + Solidity source)
│
├── WebApp (Blazor Server + EIP-6963 wallet)
│
└── LoadGenerator (Background transaction generator)

contracts/           Foundry project (Solidity source, tests, deploy scripts)
ContractServices/    Generated C# typed contract services
Tests/               Fast TDD with embedded DevChain
IntegrationTests/    E2E against running AppHost
```

## Solidity → C# Workflow

1. Edit contracts in `contracts/src/`
2. Build with Forge: `cd contracts && forge build`
3. Regenerate C# services: `./scripts/generate-csharp.sh -b` (or `.ps1`)
4. The generated code lands in `ContractServices/`

The template ships with pre-generated C# so you can build without Forge installed.

### Install Forge Dependencies

```bash
cd contracts
forge install openzeppelin/openzeppelin-contracts
```

### Run Forge Tests

```bash
cd contracts
forge test
```

## Two-Tier Testing Strategy

### Tier 1: `Tests/` — Fast TDD

- In-process DevChain via `DevChainNode.CreateAndStartAsync()`
- No Docker, no Aspire, no HTTP
- Deploys contracts, tests logic, runs in seconds
- Use for contract development TDD loop

### Tier 2: `IntegrationTests/` — E2E

- Connects to running AppHost (start with `dotnet run --project AppHost`)
- Tests full pipeline: deploy → indexer → query
- Set `DEVCHAIN_URL` env var if not using default `http://localhost:8545`

## Configuration

### LoadGenerator

```json
{
  "LoadGenerator": {
    "PrivateKey": "0xac0974...",
    "ChainId": 31337,
    "DelayMs": 2000
  }
}
```

### DevChain

```json
{
  "DevChain": {
    "ChainId": 31337,
    "Storage": "sqlite",
    "AutoMine": true
  }
}
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| PostgreSQL container won't start | Ensure Docker Desktop is running |
| Explorer shows no data | Wait for Indexer to process blocks (check Aspire dashboard logs) |
| Tests fail with DevChain errors | Ensure no port conflicts; TDD tests use in-process node |
| Forge commands not found | Install Foundry: `curl -L https://foundry.paradigm.xyz \| bash` |

## Learn More

- [Nethereum Documentation](https://docs.nethereum.com)
- [Nethereum GitHub](https://github.com/Nethereum/Nethereum)
- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [Foundry Book](https://book.getfoundry.sh/)

## License

This template uses Nethereum, which is licensed under the MIT License.
