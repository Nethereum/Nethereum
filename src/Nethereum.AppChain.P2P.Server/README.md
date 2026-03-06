# Nethereum.AppChain.P2P.Server

> **PREVIEW** — This package is in preview. APIs may change between releases.

[Nethereum AppChain](../Nethereum.AppChain/README.md) node with integrated DotNetty P2P networking and Clique PoA consensus.

## Overview

A complete AppChain node combining Clique Proof-of-Authority consensus, DotNetty P2P networking, and a full Ethereum JSON-RPC endpoint. It produces blocks, propagates them to peers, and serves RPC queries — all in one process.

The server supports multi-validator networks where authorised signers take turns producing blocks according to the Clique protocol, with blocks broadcast to all connected peers via the DotNetty transport layer.

### Key Features

- **Complete Node**: Combines consensus, block production, P2P, and RPC in one process
- **Clique PoA Consensus**: Turn-based block production with dynamic validator voting
- **DotNetty P2P**: High-performance peer-to-peer block propagation
- **JSON-RPC 2.0**: Standard Ethereum RPC endpoint compatible with ethers.js/web3.js
- **Health & Status**: Built-in `/health` and `/status` endpoints for monitoring
- **CLI Configuration**: Command-line argument parsing for all node parameters

## Installation

```bash
dotnet add package Nethereum.AppChain.P2P.Server
```

### Dependencies

- **Nethereum.AppChain** - Core chain abstraction and genesis
- **Nethereum.AppChain.Sequencer** - Block production and transaction ordering
- **Nethereum.AppChain.P2P.DotNetty** - DotNetty P2P transport
- **Nethereum.AppChain.Metrics** - Prometheus metrics instrumentation
- **Nethereum.Consensus.Clique** - Clique PoA consensus engine
- **Nethereum.CoreChain** - RPC handler registry and storage
- **System.CommandLine** - CLI argument parsing

## Quick Start

```bash
dotnet run --project src/Nethereum.AppChain.P2P.Server -- \
  --port 8546 \
  --p2p-port 30303 \
  --chain-id 420420 \
  --name "MyChain" \
  --signer-key 0xYOUR_PRIVATE_KEY \
  --signers 0xSIGNER1,0xSIGNER2 \
  --block-time 1000
```

## Usage Examples

### Example 1: Single Validator

```bash
dotnet run --project src/Nethereum.AppChain.P2P.Server -- \
  --port 8546 \
  --p2p-port 30303 \
  --chain-id 420420 \
  --name "TestChain" \
  --signer-key 0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80 \
  --block-time 1000
```

### Example 2: Multi-Validator with Peer Connection

```bash
# Node 1 (Validator)
dotnet run -- --port 8546 --p2p-port 30303 \
  --signer-key $KEY1 --signers $ADDR1,$ADDR2 \
  --node-id "validator-1"

# Node 2 (Validator, connects to Node 1)
dotnet run -- --port 8547 --p2p-port 30304 \
  --signer-key $KEY2 --signers $ADDR1,$ADDR2 \
  --peers 127.0.0.1:30303 \
  --node-id "validator-2"
```

## HTTP Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/` | JSON-RPC 2.0 endpoint (all `eth_*`, `web3_*`, `net_*` methods) |
| GET | `/health` | Health check (`{"status": "healthy"}`) |
| GET | `/status` | Node status (chain info, peers, block number) |

## CLI Options

| Option | Default | Description |
|--------|---------|-------------|
| `--host` | `127.0.0.1` | HTTP listen address |
| `--port` | `8546` | HTTP listen port |
| `--p2p-port` | `30303` | P2P TCP listen port |
| `--chain-id` | `420420` | Chain ID |
| `--name` | `AppChain` | Chain name |
| `--signer-key` | (required) | Signer private key |
| `--signers` | | Comma-separated initial signer addresses |
| `--peers` | | Comma-separated bootstrap peer endpoints |
| `--block-time` | `1000` | Block production interval (ms) |
| `--node-id` | | Human-readable node identifier |

## Related Packages

### Dependencies
- **[Nethereum.Consensus.Clique](../Nethereum.Consensus.Clique/README.md)** - Clique PoA consensus
- **[Nethereum.AppChain.P2P.DotNetty](../Nethereum.AppChain.P2P.DotNetty/README.md)** - P2P transport
- **[Nethereum.AppChain](../Nethereum.AppChain/README.md)** - Core chain abstraction

### See Also
- **[Nethereum.AppChain.Server](../Nethereum.AppChain.Server/README.md)** - Full-featured AppChain server with HTTP sync, anchoring, MUD deployment, and batch serving

## Additional Resources

- [EIP-225: Clique PoA](https://eips.ethereum.org/EIPS/eip-225)
- [Nethereum Documentation](https://docs.nethereum.com)
