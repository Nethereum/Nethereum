# Nethereum.AppChain.Server

> **PREVIEW** — This package is in preview. APIs may change between releases.

Production-ready server for running a [Nethereum AppChain](../Nethereum.AppChain/README.md) — a lightweight, domain-specific execution layer that extends Ethereum L1/L2 with publicly readable, cryptographically verifiable state.

## Overview

Nethereum.AppChain.Server is the primary executable for running an AppChain node. It combines all subsystems into a single configurable server: block production, transaction processing, HTTP and WebSocket JSON-RPC endpoints, multi-peer synchronisation, L1 state anchoring, Prometheus metrics, batch serving, and optional MUD World deployment.

The server supports three operational modes: **sequencer** (produces blocks — your business, your rules), **follower** (syncs and verifies — anyone can run one), or **Clique validator** (multi-signer PoA consensus). Configuration is entirely via command-line arguments, making it suitable for containerised deployments.

### Key Features

- **Full JSON-RPC 2.0**: All standard `eth_*`, `web3_*`, `net_*` methods plus admin RPC
- **WebSocket Subscriptions**: `eth_subscribe`/`eth_unsubscribe` via `/ws` endpoint
- **Consensus Modes**: Single-sequencer (default) or Clique PoA with multiple validators
- **HTTP Sync**: Multi-peer sync with automatic failover and state re-execution
- **Batch & Snapshot Serving**: REST endpoints for batch download and sync status
- **MUD World Deployment**: Optional MUD framework contract deployment during genesis
- **L1 Anchoring**: Periodic state commitment to Ethereum mainnet
- **Prometheus Metrics**: Comprehensive instrumentation at `/metrics`
- **Storage Options**: In-memory or RocksDB persistent storage
- **CLI Configuration**: 30+ command-line options via System.CommandLine

## Installation

```bash
# As a .NET tool
dotnet tool install Nethereum.AppChain.Server

# Run
nethereum-appchain --help
```

Or run directly from source:

```bash
dotnet run --project src/Nethereum.AppChain.Server -- [options]
```

### Dependencies

- **Nethereum.AppChain** - Core chain abstraction and genesis
- **Nethereum.AppChain.Sequencer** - Block production and transaction ordering
- **Nethereum.AppChain.Sync** - Multi-peer synchronization and batch import
- **Nethereum.AppChain.P2P / P2P.DotNetty** - P2P networking for Clique mode
- **Nethereum.Consensus.Clique** - Clique PoA consensus engine
- **Nethereum.CoreChain** - RPC handler registry, storage interfaces
- **Nethereum.CoreChain.RocksDB** - Persistent storage backend

## Quick Start

### Sequencer Mode

```bash
nethereum-appchain \
  --port 8546 \
  --chain-id 420420 \
  --name "MyAppChain" \
  --genesis-owner-key 0xYOUR_PRIVATE_KEY \
  --sequencer-key 0xYOUR_PRIVATE_KEY \
  --block-time 1000
```

### Follower Mode

```bash
nethereum-appchain \
  --port 8547 \
  --chain-id 420420 \
  --name "MyAppChain" \
  --genesis-owner-address 0xOWNER_ADDRESS \
  --sequencer-address 0xSEQUENCER_ADDRESS \
  --sync-peers http://sequencer:8546 \
  --sync-poll-interval 100
```

## HTTP Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/` | JSON-RPC 2.0 (all `eth_*`, `web3_*`, `net_*`, `admin_*` methods) |
| WS | `/ws` | WebSocket JSON-RPC (`eth_subscribe`, `eth_unsubscribe`) |
| GET | `/health` | Health check |
| GET | `/status` | Comprehensive node status |
| GET | `/metrics` | Prometheus metrics |
| GET | `/batches` | List available batches |
| GET | `/batches/{fileName}` | Download batch file |
| GET | `/batches/latest` | Latest batch info |
| GET | `/blocks/latest` | Latest block header |
| GET | `/blocks/{number}` | Block by number |
| GET | `/blocks/range` | Block range query |
| GET | `/finality` | Finality tracker status |
| GET | `/finality/{blockNumber}` | Block finality status |
| GET | `/sync/status` | Sync status |

## CLI Options

### Core

| Option | Default | Description |
|--------|---------|-------------|
| `--host` | `0.0.0.0` | HTTP listen address |
| `--port` | `8546` | HTTP listen port |
| `--chain-id` | `420420` | Chain ID |
| `--name` | `AppChain` | Chain name |
| `--block-time` | `1000` | Block production interval (ms) |
| `--in-memory` | `false` | Use in-memory storage (no persistence) |
| `--db-path` | `./appchain-data` | RocksDB data directory |

### Genesis & Keys

| Option | Description |
|--------|-------------|
| `--genesis-owner-key` | Private key for genesis owner (sequencer mode) |
| `--genesis-owner-address` | Address of genesis owner (follower mode) |
| `--sequencer-key` | Private key for block signing |
| `--sequencer-address` | Address of sequencer (follower mode) |

### Sync

| Option | Default | Description |
|--------|---------|-------------|
| `--sync-peers` | | Comma-separated peer URLs for HTTP sync |
| `--sync-poll-interval` | `1000` | Sync poll interval (ms) |

### Clique Consensus

| Option | Default | Description |
|--------|---------|-------------|
| `--clique` | `false` | Enable Clique PoA consensus |
| `--clique-signers` | | Initial signer addresses |
| `--clique-block-period` | `1` | Block period in seconds |
| `--p2p-port` | `30303` | DotNetty P2P port |
| `--p2p-peers` | | Bootstrap peer endpoints |

### MUD & Anchoring

| Option | Default | Description |
|--------|---------|-------------|
| `--deploy-mud-world` | `true` | Deploy MUD World contracts on genesis |
| `--anchor-l1-rpc` | | L1 RPC URL for anchoring |
| `--anchor-contract` | | L1 anchor contract address |
| `--anchor-cadence` | `100` | Blocks between L1 anchors |

## Usage Examples

### Example 1: Sequencer with RocksDB

```bash
nethereum-appchain \
  --port 8546 \
  --chain-id 420420 \
  --name "ProdChain" \
  --genesis-owner-key $OWNER_KEY \
  --sequencer-key $SEQ_KEY \
  --db-path /data/appchain \
  --block-time 500 \
  --deploy-mud-world true
```

### Example 2: Clique Multi-Validator

```bash
# Validator 1
nethereum-appchain \
  --port 8546 --clique \
  --clique-signers $ADDR1,$ADDR2,$ADDR3 \
  --genesis-owner-key $KEY1 \
  --sequencer-key $KEY1 \
  --p2p-port 30303

# Validator 2
nethereum-appchain \
  --port 8547 --clique \
  --clique-signers $ADDR1,$ADDR2,$ADDR3 \
  --genesis-owner-key $KEY2 \
  --sequencer-key $KEY2 \
  --p2p-port 30304 \
  --p2p-peers 127.0.0.1:30303
```

### Example 3: Follower with L1 Anchoring Verification

```bash
nethereum-appchain \
  --port 8547 \
  --chain-id 420420 \
  --genesis-owner-address $OWNER_ADDR \
  --sequencer-address $SEQ_ADDR \
  --sync-peers http://sequencer:8546 \
  --anchor-l1-rpc https://mainnet.infura.io/v3/KEY \
  --anchor-contract $ANCHOR_ADDR
```

## Related Packages

### Dependencies
- **[Nethereum.AppChain](../Nethereum.AppChain/README.md)** - Core chain abstraction
- **[Nethereum.AppChain.Sequencer](../Nethereum.AppChain.Sequencer/README.md)** - Block production
- **[Nethereum.AppChain.Sync](../Nethereum.AppChain.Sync/README.md)** - Synchronization
- **[Nethereum.Consensus.Clique](../Nethereum.Consensus.Clique/README.md)** - PoA consensus

### See Also
- **[Nethereum.AppChain.P2P.Server](../Nethereum.AppChain.P2P.Server/README.md)** - Simpler P2P-focused server
- **[Nethereum.DevChain.Server](../Nethereum.DevChain.Server/README.md)** - Development chain server

## Additional Resources

- [MUD Framework](https://mud.dev)
- [Nethereum Documentation](https://docs.nethereum.com)
