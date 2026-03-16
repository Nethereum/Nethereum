# Nethereum Aspire Template Pack

A NuGet template pack providing ready-to-use .NET Aspire solutions for Ethereum development with [Nethereum](https://github.com/Nethereum/Nethereum).

## Installation

```bash
dotnet new install Nethereum.Aspire.TemplatePack
```

To install a specific version:

```bash
dotnet new install Nethereum.Aspire.TemplatePack::1.0.0
```

## Templates

### `nethereum-devchain` — Nethereum DevChain Aspire Solution

A complete, self-contained Ethereum development environment orchestrated with .NET Aspire. Spin up a local Ethereum node, blockchain indexer, and explorer UI with a single `dotnet run`.

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
| `--NethereumVersion` | `6.0.0` | Nethereum NuGet package version |
| `--ChainId` | `31337` | Chain ID for the DevChain node |
| `--AspireVersion` | `9.2.0` | .NET Aspire SDK and hosting package version |

**Prerequisites:**

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for the PostgreSQL container)

See the full [DevChain template README](templates/Nethereum.DevChain.Template/README.md) for architecture details, configuration, and extension guides.

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
