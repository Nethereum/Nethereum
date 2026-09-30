# Nethereum.AppChain.Server

> **PREVIEW** — This package is in preview. APIs may change between releases.

The executable for running a [Nethereum AppChain](../Nethereum.AppChain/README.md) — a lightweight, domain-specific execution layer that extends Ethereum L1/L2 with publicly readable, cryptographically verifiable state. One process gives you block production, JSON-RPC (HTTP + WebSocket), DevP2P peering and sync, Clique PoA consensus, cross-chain messaging, L1 anchoring, and optional MUD World deployment.

A node runs in one of three roles, chosen by which keys/addresses you hand it: **sequencer** (holds a private key, produces blocks), **follower** (holds only addresses, imports and verifies blocks from a peer), or **Clique validator** (one of several signers in a multi-node PoA set).

## Install

```bash
dotnet tool install -g Nethereum.AppChain.Server
```

## Getting started

Run it with no arguments:

```bash
nethereum-appchain
```

With no genesis owner, sequencer, or Clique signer identity supplied, the server generates ephemeral keys in memory, logs `AppChain dev mode: generated ephemeral keys (NOT for production)`, and starts producing blocks straight away — single-sequencer consensus, RocksDB storage under `./appchain-data`, JSON-RPC on `http://127.0.0.1:8546`. This is the fastest way to get an AppChain running locally; the generated keys are lost on restart, so it is not for anything you need to come back to (`AppChainComposition.ApplyEphemeralDevKeys`, `src/Nethereum.AppChain.Server.Core/AppChainComposition.cs:136-142`, gated by `AppChainServerConfig.IsFullyUnconfigured`, `src/Nethereum.AppChain.Server.Core/Configuration/AppChainServerConfig.cs:42-45`).

For anything persistent, supply your own keys:

```bash
nethereum-appchain \
  --port 8546 \
  --chain-id 420420 \
  --name "MyAppChain" \
  --genesis-owner-key 0xYOUR_PRIVATE_KEY \
  --sequencer-key 0xYOUR_PRIVATE_KEY \
  --block-time 1000
```

### Follower mode

A follower supplies addresses instead of private keys, and points `--follow-peer` at a sequencer's enode. It imports and verifies blocks over DevP2P instead of producing any:

```bash
nethereum-appchain \
  --port 8547 \
  --chain-id 420420 \
  --name "MyAppChain" \
  --genesis-owner-address 0xOWNER_ADDRESS \
  --sequencer-address 0xSEQUENCER_ADDRESS \
  --follow-peer enode://PRODUCER_PUBKEY@sequencer:30403
```

Follower mode requires **both** `--genesis-owner-address` and `--sequencer-address` — the server needs to know who it's trusting before it will import a single block (`AppChainServerConfigValidator.ValidateOperatorKeys`, `src/Nethereum.AppChain.Server.Core/Configuration/AppChainServerConfigValidator.cs:46-61`).

### Clique multi-validator

```bash
# Validator 1
nethereum-appchain \
  --port 8546 --consensus clique \
  --initial-signers $ADDR1,$ADDR2,$ADDR3 \
  --signer-key $KEY1 \
  --genesis-owner-key $KEY1 \
  --sequencer-key $KEY1 \
  --enable-devp2p-serve --devp2p-serve-port 30403

# Validator 2
nethereum-appchain \
  --port 8547 --consensus clique \
  --initial-signers $ADDR1,$ADDR2,$ADDR3 \
  --signer-key $KEY2 \
  --genesis-owner-key $KEY2 \
  --sequencer-key $KEY2 \
  --enable-devp2p-serve --devp2p-serve-port 30404 \
  --devp2p-peers enode://VALIDATOR1_PUBKEY@127.0.0.1:30403
```

Clique consensus additionally requires `--signer-key` (unless following), at least one `--initial-signers` entry, and at least one DevP2P peer to reach — each is enforced at startup with a specific error, not a silent no-op (`AppChainServerConfigValidator.ValidateClique`, `src/Nethereum.AppChain.Server.Core/Configuration/AppChainServerConfigValidator.cs:67-81`).

## HTTP endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/` | JSON-RPC 2.0 (all `eth_*`, `web3_*`, `net_*`, plus `admin_*`) |
| WS | `/ws` | WebSocket JSON-RPC (`eth_subscribe`, `eth_unsubscribe`) |
| GET | `/health` | Health check |
| GET | `/status` | Chain ID, block number, RPC URL, consensus mode, sequencer status |
| GET | `/blocks/latest` | Latest block header |
| GET | `/blocks/{number}` | Block header by number |
| GET | `/blocks/{number}/full` | Block with transactions by number |
| GET | `/blocks/range?from=&to=` | Block range query |
| GET | `/finality` | Finality tracker status |
| GET | `/finality/{blockNumber}` | Block finality status |

(`AppChainServerRunner.MapEndpoints`, `src/Nethereum.AppChain.Server.Core/AppChainServerRunner.cs:365-457`; `LiveBlockEndpoints.MapLiveBlockEndpoints`, `src/Nethereum.AppChain.Server.Core/Endpoints/LiveBlockEndpoints.cs:19`.)

## CLI options

Every option below is defined in `AppChainCliOptions` (`src/Nethereum.AppChain.Server/AppChainCli.cs:12-86`) — `Program.cs` just wires it up. This is the full set the tool understands today.

### Server / RPC

| Option | Default | Description |
|--------|---------|-------------|
| `--host` | `127.0.0.1` | Host to bind to |
| `--port` | `8546` | Port to listen on |
| `--chain-id` | `420420` | Chain ID |
| `--name` | `AppChain` | Chain name |
| `--otlp-endpoint` | | OpenTelemetry OTLP endpoint URL (falls back to `OTEL_EXPORTER_OTLP_ENDPOINT` env var) |

### Genesis & keys

| Option | Description |
|--------|-------------|
| `--genesis-owner-key` | Genesis owner private key (deploys MUD, owns root namespace) |
| `--genesis-owner-address` | Genesis owner address (for follower mode, no private key needed) |
| `--sequencer-key` | Sequencer private key (produces blocks) |
| `--sequencer-address` | Sequencer address (for follower mode, no private key needed) |

Leave all four unset and the server generates ephemeral keys itself — see [Getting started](#getting-started).

### Block production

| Option | Default | Description |
|--------|---------|-------------|
| `--block-time` | `1000` | Block time in milliseconds |
| `--allow-empty-blocks` | `false` | Produce blocks even when no pending transactions |

### Storage

| Option | Default | Description |
|--------|---------|-------------|
| `--db-path` (alias `--data-dir`) | `./appchain-data` | Database path (RocksDB) |
| `--in-memory` | `false` | Use in-memory storage |

### Consensus & DevP2P

| Option | Default | Description |
|--------|---------|-------------|
| `--consensus` | `single-sequencer` | Consensus mode: `single-sequencer` or `clique` |
| `--enable-devp2p-serve` | `false` | Serve eth/snap over RLPx DevP2P (lets snap-sync followers cold-sync from this node) |
| `--devp2p-serve-port` (alias `--listen-port`) | `30403` | DevP2P eth/snap serve listen port |
| `--devp2p-peers` (alias `--trusted-peer`) | (empty) | Peer enodes to dial and trust (replaces `--sync-peers` and `--bootstrap-nodes`) |
| `--node-key-hex` | | Hex-encoded DevP2P node identity private key (pins a stable enode across restarts) |
| `--node-key-file` | | Persisted DevP2P node identity key file (default: `<db-path>/nodekey`, created on first run) |
| `--follow-peer` | | Enode of the node to follow; syncs from it over DevP2P instead of producing blocks |
| `--signer-key` | | Clique signer private key |
| `--initial-signers` | (empty) | Initial Clique signers (addresses) |
| `--clique-period` | `15` | Clique block period in seconds |
| `--clique-epoch` | `30000` | Clique epoch length |

### MUD

| Option | Default | Description |
|--------|---------|-------------|
| `--deploy-mud-world` | `true` | Deploy MUD World contracts at genesis (`--deploy-mud-world false` to skip) |
| `--world-salt` | | MUD World salt (32 bytes hex) |

### Anchoring

| Option | Default | Description |
|--------|---------|-------------|
| `--l1-rpc` | | L1 RPC URL for anchoring |
| `--anchor-contract` | | Anchor contract address on L1 |

### Cross-chain messaging

| Option | Default | Description |
|--------|---------|-------------|
| `--enable-messaging` | `false` | Enable cross-chain message processing |
| `--hub-source-chains` | (empty) | Hub source chains, format: `chainId:rpcUrl:hubAddress` |
| `--message-poll-interval` | `5000` | Message poll interval in milliseconds |
| `--max-messages-per-poll` | `100` | Max messages per poll cycle |
| `--enable-acknowledgment` | `false` | Enable message acknowledgment back to source Hubs |
| `--acknowledgment-interval` | `30000` | Message acknowledgment interval in milliseconds |

## Advanced configuration

The everyday flags above are layered on top of a full configuration system, so you have three ways to set any value and they compose in a fixed order (later wins): `appsettings.json` → environment variables → command-line flags. `BuildLayeredConfiguration` wires all three (`src/Nethereum.AppChain.Server/AppChainCli.cs:102-107`); the `AppChain` section of `appsettings.json` and `AppChain__`-prefixed env vars bind straight onto `AppChainServerConfig`.

Beyond the everyday flags, every setting on `AppChainServerConfig` can be set in its raw `--AppChain:<Path> <value>` form. Run `nethereum-appchain --help-advanced` to list the full expert surface (`TryHandleAdvancedHelp`/`PrintAdvancedHelp`, `src/Nethereum.AppChain.Server/AppChainCli.cs:178-199`, invoked from `Program.cs`).

Node identity is a first-class concern: `--node-key-hex` pins the DevP2P identity from a hex key, and `--node-key-file` persists it to a file (defaulting to `<db-path>/nodekey`), so a node keeps a stable enode across restarts.

## Embed it

`AppChainServerRunner.RunAsync` is the whole server — composition, HTTP host, and endpoint mapping — packaged as a single call, so embedding it in your own process is one line:

```csharp
using Nethereum.AppChain.Server;
using Nethereum.AppChain.Server.Configuration;

var config = new AppChainServerConfig
{
    ChainId = 420420,
    ChainName = "MyAppChain",
};
config.Consensus.Sequencer.PrivateKey = sequencerKey;
config.Genesis.Owner.PrivateKey = genesisOwnerKey;

await AppChainServerRunner.RunAsync(config);
```

If you're composing your own ASP.NET Core host instead and only want the AppChain building blocks wired into your DI container, `AddAppChainServer` registers the config, `MudWorldDeployer`, and the in-memory finality tracker as singletons; pair it with `AddAppChainMetrics`, `AddAppChainOpenTelemetry`, and `AddAppChainHealthChecks` for the same instrumentation `AppChainServerRunner` sets up internally (`src/Nethereum.AppChain.Server.Core/Hosting/ServiceCollectionExtensions.cs`). Endpoint mapping (`/`, `/ws`, `/status`, `/blocks/*`, `/finality/*`) is not yet exposed as a reusable `Map*Endpoints` extension the way `Nethereum.DevChain.Server` and `Nethereum.MainnetChain.Server` offer it — `AppChainServerRunner.RunAsync` is the supported way to get the full HTTP surface today.

## Related packages

### Dependencies
- **[Nethereum.AppChain](../Nethereum.AppChain/README.md)** - Core chain abstraction
- **[Nethereum.AppChain.Sequencer](../Nethereum.AppChain.Sequencer/README.md)** - Block production
- **[Nethereum.DevP2P.Sync](../Nethereum.DevP2P.Sync/README.md)** - DevP2P peering and sync
- **[Nethereum.Consensus.Clique](../Nethereum.Consensus.Clique/README.md)** - PoA consensus

### See also
- **[Nethereum.DevChain.Server](../Nethereum.DevChain.Server/README.md)** - Development chain server
- **[Nethereum.MainnetChain.Server](../Nethereum.MainnetChain.Server/README.md)** - Mainnet follower server

## Additional resources

- [MUD Framework](https://mud.dev)
- [Nethereum Documentation](https://docs.nethereum.com)
