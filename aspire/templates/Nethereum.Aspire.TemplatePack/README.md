# Nethereum Aspire Template Pack

A NuGet template pack providing ready-to-use .NET Aspire solutions for Ethereum development with [Nethereum](https://github.com/Nethereum/Nethereum).

## Installation

```bash
dotnet new install Nethereum.Aspire.TemplatePack
```

To install a specific version:

```bash
dotnet new install Nethereum.Aspire.TemplatePack::1.0.3
```

## Templates

### `nethereum-dapp` — Nethereum dApp Aspire Solution

A complete dApp development environment with Solidity contracts, C# code generation, Blazor web UI with EIP-6963 wallet integration, and Aspire-orchestrated DevChain + Indexer + Explorer.

**What's included:**

| Service | Description |
|---------|-------------|
| **DevChain** | In-memory Ethereum node with full JSON-RPC support (EIP-1559, debug tracing) |
| **Indexer** | Background worker that indexes blocks, transactions, logs, token transfers, balances into PostgreSQL |
| **Explorer** | Blazor Server blockchain explorer with contract interaction, ABI decoding, wallet connection, and auto-discovery of deployed contract ABIs from Foundry artifacts |
| **WebApp** | Blazor Server dApp UI with EIP-6963 wallet connection, chain validation/switching, and ERC-20 token deploy/mint/transfer |
| **LoadGenerator** | Configurable transaction load generator for stress testing |
| **ContractServices** | Generated C# typed contract access from Solidity ABI |
| **Contracts** | Foundry project with MyToken (ERC-20 + mint), deploy script, and Forge tests |
| **PostgreSQL** | Managed database container for all indexed blockchain data |

**Quick start:**

```bash
dotnet new nethereum-dapp -n MyDapp
cd MyDapp/AppHost
dotnet user-secrets set "Parameters:devAccountPrivateKey" "5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a"
dotnet run
```

Open the Aspire dashboard, then:
1. **WebApp** — Connect MetaMask, switch to DevChain (chain 31337), deploy tokens, mint, transfer
2. **Explorer** — Browse blocks/transactions, view contract ABIs auto-discovered from Foundry output

**Parameters:**

| Parameter | Default | Description |
|-----------|---------|-------------|
| `--NethereumVersion` | `6.1.0` | Nethereum NuGet package version |
| `--ChainId` | `31337` | Chain ID for the DevChain node |
| `--AspireVersion` | `13.1.1` | .NET Aspire SDK and hosting package version |

**WebApp wallet flow:**

The WebApp's Token Interaction page has three states:
- **Not connected** — prompts user to connect wallet via the header button
- **Connected, wrong chain** — shows "Switch to DevChain" button (uses `wallet_addEthereumChain`)
- **Connected, correct chain** — shows token deploy/mint/transfer/balance UI

The DevChain RPC URL is resolved automatically via Aspire service discovery and passed to MetaMask when switching chains.

**Explorer ABI auto-discovery:**

The Explorer automatically matches deployed contracts to their Foundry build artifacts by comparing on-chain runtime bytecode. When you deploy a contract from the WebApp and navigate to its address in the Explorer, the ABI, function signatures, and Solidity source are available immediately — no manual upload needed.

The AppHost configures this via:
- `Explorer__AbiSources__LocalStorageEnabled=true`
- `Explorer__AbiSources__LocalStoragePath` → `contracts/out/`
- `Explorer__AbiSources__SourceBasePath` → `contracts/`

**Regenerating C# contract services after Solidity changes:**

```bash
cd contracts
forge build
cd ..
pwsh scripts/generate-csharp.ps1   # Windows
# or
bash scripts/generate-csharp.sh    # Linux/macOS
```

---

### `nethereum-devchain` — Nethereum DevChain Aspire Solution

A self-contained Ethereum development environment orchestrated with .NET Aspire. Spin up a local Ethereum node, blockchain indexer, and explorer UI with a single `dotnet run`.

**What's included:**

| Service | Description |
|---------|-------------|
| **DevChain** | In-memory Ethereum node with full JSON-RPC support (EIP-1559, debug tracing) |
| **Indexer** | Background worker that indexes blocks, transactions, logs, token transfers (ERC-20/721/1155), balances, and MUD World records into PostgreSQL |
| **Explorer** | Blazor Server blockchain explorer with contract interaction, ABI decoding, token pages, and MUD table viewer |
| **PostgreSQL** | Managed database container for all indexed blockchain data |

All services include Aspire service discovery, OpenTelemetry distributed tracing, health checks, and resilient HTTP clients.

**Quick start:**

```bash
dotnet new nethereum-devchain -n MyChain
cd MyChain/AppHost
dotnet user-secrets set "Parameters:devAccountPrivateKey" "5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a"
dotnet run
```

**Parameters:**

| Parameter | Default | Description |
|-----------|---------|-------------|
| `--NethereumVersion` | `6.1.0` | Nethereum NuGet package version |
| `--ChainId` | `31337` | Chain ID for the DevChain node |
| `--AspireVersion` | `13.1.1` | .NET Aspire SDK and hosting package version |

See the full [DevChain template README](templates/Nethereum.DevChain.Template/README.md) for architecture details, configuration, and extension guides.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for the PostgreSQL container)
- A browser wallet supporting [EIP-6963](https://eips.ethereum.org/EIPS/eip-6963) (MetaMask, Rabby, etc.) for dApp wallet features

## Troubleshooting

**PostgreSQL unhealthy on restart:** Aspire generates a random password each run but the Docker volume persists the old one. Fix: `docker volume rm nethereum-pgdata` then restart from the Aspire dashboard.

**Explorer wallet shows "No Wallet Available":** Ensure your browser wallet extension is enabled and refresh the page. EIP-6963 wallet detection requires the Blazor SignalR circuit to be active.

## Uninstall

```bash
dotnet new uninstall Nethereum.Aspire.TemplatePack
```

## Links

- [Nethereum Documentation](https://docs.nethereum.com)
- [Nethereum GitHub](https://github.com/Nethereum/Nethereum)
- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)

## License

MIT — see [LICENSE.md](LICENSE.md)
