# Nethereum.MainnetChain.Server

> **PREVIEW** — This package is in preview. APIs may change between releases.

A read-only Ethereum mainnet follower with a JSON-RPC server, built in .NET. It syncs and validates blocks from the devp2p network, keeps a local state database, and serves that state over JSON-RPC — and, optionally, to other devp2p peers. It follows and serves the chain; it does not propose or mine.

## Install

```bash
dotnet tool install -g Nethereum.MainnetChain.Server
```

## Run it

```bash
nethereum-mainnetchain --beacon http://127.0.0.1:5052
```

This runs the follower with the default options: it snap-syncs a recent state snapshot, validates every block as it follows the head, and serves JSON-RPC on `http://127.0.0.1:8545`. State is kept in `./mainnet-data`. Run it again to resume where it left off.

The `--beacon` endpoint is a beacon-node REST API: the follower cross-checks the chain it's following against the beacon-attested chain, rejecting any block that disagrees, and `eth_getBlockByNumber` with the `finalized` / `safe` tags becomes meaningful.

A beacon endpoint is how the follower verifies consensus, so the server **refuses to start without one** — a forgotten `--beacon` fails loudly instead of silently trusting whatever peers serve. To run without a beacon anyway (not recommended for mainnet), opt out explicitly:

```bash
nethereum-mainnetchain --allow-unverified-consensus
```

## Options

```
SERVER:
  -p, --port <PORT>          JSON-RPC port (default: 8545)
      --host <HOST>          Host to bind to (default: 127.0.0.1)
  -v, --verbose              Verbose logging
      --metrics-port <PORT>  Expose Prometheus /metrics on this port (default: off)

SYNC:
  -d, --data-dir <DIR>       Chain data directory (default: ./mainnet-data)
      --no-snap              Skip snap sync (snap is the default; a full sync is impractical for mainnet)
      --trusted-peer <ENODE> Pinned enode:// peer to always dial
      --listen-port <PORT>   Also serve snap/eth to other peers (default: off)

CONSENSUS:
      --beacon <URL>         Beacon REST endpoint (enables the light-client gate)
      --allow-unverified-consensus  Start WITHOUT a beacon (no consensus verification; NOT recommended for mainnet)

MAINTENANCE:
      --wipe-state           One-shot: clear state and re-run the state sync, keeping the block archive
      --rebuild-state-from-flat  One-shot: rebuild stale storage-trie nodes from flat state, then verify
      --verify-flat          One-shot: write-free flat/trie verify at the committed head, logs the full breakdown, then stops
      --verify-flat-sample <N>  With --verify-flat: verify only the first N accounts per shard (0 = full walk, default)
      --node-key-file <PATH> Persisted node identity key (default: <data-dir>/nodekey, created on first run)

TUNING (common advanced):
      --flush-cadence <N>    Persist state every N blocks (default 1 = every block)
      --checkpoint-every <N> Checkpoint cadence in blocks (default 50000)
      --cache-size <SIZE>    RocksDB block cache size, e.g. 4G / 512M (default 1G)
```

Settings can also come from an `appsettings.json` `MainnetChain` section or `MainnetChain__`-prefixed environment variables. Run `--help` for this list, and `--help-advanced` for the full set of expert `--MainnetChain:*` knobs (peer/batch sizing, journal and trie-node history windows, freezer and split-history storage, the RPC-serving caps, and more).

## Advanced

Operational knobs you reach for occasionally:

- **Clear state, keep the block archive.** Run once with `--wipe-state` (or `MainnetChain:WipeState` in `appsettings.json` / `MainnetChain__WipeState` in the environment). This wipes the state / trie / state-history column families and the snap-sync metadata, but **keeps headers, bodies, receipts and the Phase-1 cursors** — so it re-runs the state sync without re-downloading the block archive.
- **Stable node identity.** By default the node persists its identity key at `<data-dir>/nodekey` (generated on first run) and reuses it for both the inbound listener and every outbound dial, so its enode never changes across restarts or reconnects. This is what lets a peer (e.g. geth) durably `admin_addTrustedPeer` this node — a rotating identity would make that trust evaporate on the next reconnect. The file is a raw 32-byte private key — **not** the same format as geth's own nodekey file (64 hex characters of text), so the two are not interchangeable; pointing this at an existing geth nodekey file will fail to load rather than overwrite it. Override the path with `--node-key-file <path>` (or `MainnetChain:NodeKeyFile`).
- **Prometheus metrics.** `--metrics-port 9090` exposes the node's metrics — snap-sync phase/progress, peer counts, fetch failures, durations — at `http://<host>:9090/metrics` in Prometheus text format, on a separate port from the JSON-RPC.
- **Serve state to other peers.** `--listen-port 30303` turns on the inbound RLPx listener so the node answers snap/1 + eth requests, not just JSON-RPC.
- **Pin a peer.** `--trusted-peer enode://…` always dials a known-good peer.
- **Full sync instead of snap.** `--no-snap` (rarely useful — a from-genesis mainnet replay takes days).
- **Storage mode.** State is path-keyed (bounded, prunable) by default; the legacy hash-keyed store is selectable in config but is incompatible on-disk, so switching needs a fresh sync. See the [`Nethereum.MainnetChain` reference](../Nethereum.MainnetChain/README.md) for the full configuration surface.

## Embed it

To host the follower inside your own .NET app instead of running the tool, use the library:

```csharp
var builder = WebApplication.CreateBuilder(args);

var config = new MainnetChainServerConfig { DataDir = "./mainnet-data", SnapBootstrap = true };
builder.AddMainnetChainServer(config);

using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
await builder.Services.AddMainnetNodeAsync(config, loggerFactory);

var app = builder.Build();
app.MapMainnetChainEndpoints();
app.Run("http://127.0.0.1:8545");
```

`AddMainnetChainServer` registers the follower graph (validation, the optional beacon light-client gate, and the JSON-RPC dispatcher); `AddMainnetNodeAsync` builds and registers the RocksDB and DevP2P production node; `MapMainnetChainEndpoints` maps `POST /` (JSON-RPC) and `GET /` (health). See the [`Nethereum.MainnetChain` package](../Nethereum.MainnetChain/README.md) for the hosting API.
