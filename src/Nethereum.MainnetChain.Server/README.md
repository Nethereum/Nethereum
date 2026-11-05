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

A beacon endpoint is how the follower verifies consensus, so the server **refuses to start without one** — a forgotten `--beacon` fails loudly instead of silently trusting whatever peers serve. The alternative is `--engine`, which lets a consensus client drive the follower through the Engine API (see below); `--engine` and `--beacon` together are refused. To run without a beacon anyway (not recommended for mainnet), opt out explicitly:

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
      --engine               Serve the Engine API so a consensus client (Lighthouse) drives the follower; excludes --beacon
      --genesis <PATH>       Custom chain: a standard genesis.json (chain id, fork schedule, alloc). Needs --engine; runs with no peers, no snap and no light client, and serves debug_setHead when --MainnetChain:RpcModules includes debug
      --engine-port <PORT>   Engine API port (default: 8551)
      --engine-jwt-secret <PATH>  JWT secret file shared with the consensus client (default: <data-dir>/jwt.hex, created if missing)

MAINTENANCE:
      --wipe-state           One-shot: clear state, keeping the block archive, then exit; the next start re-runs the state sync
      --rebuild-state-from-flat  One-shot: rebuild stale storage-trie nodes from flat state, then verify
      --verify-flat          One-shot: write-free flat/trie verify at the committed head, logs the full breakdown, then stops
      --verify-flat-sample <N>  With --verify-flat: verify only the first N accounts per shard (0 = full walk, default)
      --node-key-file <PATH> Persisted node identity key (default: <data-dir>/nodekey, created on first run)

TUNING (common advanced):
      --flush-cadence <N>    Persist state every N blocks (default 1 = every block)
      --prefetch-workers <N> Dedicated threads that prefetch each block's transactions into the RocksDB caches (default 0 = off)
      --state-root-workers <N>  Dedicated threads that update storage tries in parallel while computing each block's state root (default 4; 1 = sequential)
      --checkpoint-every <N> Checkpoint cadence in blocks (default 50000)
      --cache-size <SIZE>    RocksDB block cache size, e.g. 4G / 512M (default 1G)
      --rocksdb-statistics   Collect RocksDB statistics and log block-cache counters every 100 blocks
      --replay-blocks <N>    One-shot performance gate: re-execute the N blocks after the executed head from the stored bodies, verify every root, exit 0 (all roots matched) or 1
      --replay-report <PATH> JSON report written by --replay-blocks
```

Flags also accept the `--name=value` form.

Settings can also come from an `appsettings.json` `MainnetChain` section or `MainnetChain__`-prefixed environment variables. Run `--help` for the everyday flags, and `--help-advanced` for the full set of expert `--MainnetChain:*` knobs, `--state-root-workers` and the replay flags (peer/batch sizing, journal and trie-node history windows, freezer and split-history storage, the RPC-serving caps, and more).

## Advanced

Operational knobs you reach for occasionally:

- **Engine API.** `--engine` serves the Engine API on `--engine-port` (default 8551, bound to 127.0.0.1), authenticated with the JWT secret at `--engine-jwt-secret` (default `<data-dir>/jwt.hex`, created on first run as a 32-byte hex file readable by its owner only on Unix). The follower executes nothing until the consensus client's first `forkchoiceUpdated`. With `--genesis <path>` as well it runs a custom chain engine-only: no peers, no snap sync, no light client, and `debug_setHead` (when `--MainnetChain:RpcModules` includes `debug`) rewinds the chain to an earlier block.
- **What the JSON-RPC port serves.** Only the `eth`, `net` and `web3` modules by default; add `txpool` or `debug` (`admin` is accepted but this server registers no `admin_*` method) with `--MainnetChain:RpcModules eth,net,web3,debug`. A request whose `Host` is not `localhost`, the bind host, an IP address or listed in `--MainnetChain:RpcAllowedHosts` gets `403`, a content type other than JSON gets `415`, and no CORS header is sent unless `--MainnetChain:RpcCorsOrigins` lists the origin (or `*`).
- **Logging.** The server logs `Nethereum` at Information (Debug with `-v`) and everything else at Warning; the standard `--Logging:LogLevel:<Category>=<Level>` overrides win over those defaults. It does not watch its configuration files for changes.
- **Failure and shutdown.** An unhandled follower failure, or a RocksDB write failure that is not transient, is logged at Critical and the process exits `1`. A stop waits for the block being written to finish.
- **Clear state, keep the block archive.** Run once with `--wipe-state` (or `MainnetChain:WipeState` in `appsettings.json` / `MainnetChain__WipeState` in the environment). This wipes the state / trie / state-history column families and the snap-sync metadata and exits, but **keeps headers, bodies, receipts and the Phase-1 cursors** — so the next start re-runs the state sync without re-downloading the block archive.
- **Stable node identity.** By default the node persists its identity key at `<data-dir>/nodekey` (generated on first run) and reuses it for both the inbound listener and every outbound dial, so its enode never changes across restarts or reconnects. This is what lets a peer durably trust this node by enode — a rotating identity would make that trust evaporate on the next reconnect. The file is a raw 32-byte private key, not a hex text file, so a node key file written by another client is not interchangeable; pointing this at one fails to load rather than overwriting it. Override the path with `--node-key-file <path>` (or `MainnetChain:NodeKeyFile`).
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
