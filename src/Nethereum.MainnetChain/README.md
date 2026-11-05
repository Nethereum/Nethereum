# Nethereum.MainnetChain

> **PREVIEW** — This package is in preview. APIs may change between releases.

A read-only Ethereum mainnet follower and serving node, hostable from a CLI, .NET host, or tests.

## Overview

Nethereum.MainnetChain composes a full mainnet-following node out of the lower-level Nethereum building blocks: it syncs and validates blocks from the devp2p network, keeps a local state database, and serves that state back out over JSON-RPC and (optionally) to other devp2p peers. It is *read-only* — it follows and serves the chain, it does not propose or mine.

It brings together:

- **Block following & validation** — executes and validates mainnet blocks through the CoreChain executor stack (`FollowerChainNode`), computing and checking state roots.
- **DevP2P sync** — cold-starts from a snapshot via snap/1 sync and backfills the block archive, then follows the head (see [Nethereum.DevP2P.Sync](../Nethereum.DevP2P.Sync/README.md)).
- **Optional beacon LightClient consensus gate** — when a beacon endpoint is configured, a light client provides both a trusted canonical tip (for snap pivot selection) and a block-admission gate that rejects any block whose hash disagrees with the beacon-attested chain.
- **Engine API mode** — with `Engine.Enabled` a consensus client drives the follower through `forkchoiceUpdated` instead of a beacon light client; with a custom genesis the node runs engine-only, with no peers (see [Engine API mode](#engine-api-mode)).
- **JSON-RPC serving** — `eth_*` / `net_*` / `web3_*` over HTTP by default (`txpool` and `debug` are opt-in through `RpcModules`; `admin` is accepted as a module name but this server registers no `admin_*` method), plus inbound snap/1 + eth serving to other peers.

Typical uses:

- **Run a self-hosted mainnet RPC endpoint** backed by your own synced state.
- **Serve snap/1 and eth** to other Nethereum nodes and standard Ethereum execution clients on the network.
- **Embed a validating follower** in a .NET application to observe and verify the chain trustlessly.
- **Bootstrap a fresh node** from snapshot state without replaying the whole chain.

## Installation

The follower is packaged both as a library and as a runnable dotnet tool.

```bash
# Library (embed in your own host)
dotnet add package Nethereum.MainnetChain

# Server as a global tool
dotnet tool install -g Nethereum.MainnetChain.Server   # command: nethereum-mainnetchain
```

### Dependencies

**Nethereum packages (direct project references):**
- **Nethereum.ChainNode.Hosting** — the shared node composition (storage, DevP2P listener, sync stack, RPC endpoint).
- **Nethereum.CoreChain** — the node/executor stack, stores, and validation.
- **Nethereum.CoreChain.RocksDB** — the persistent state + block database.
- **Nethereum.DevP2P** / **Nethereum.DevP2P.Sync** — peer networking and the sync engine.
- **Nethereum.Consensus.LightClient** — the beacon light client (optional consensus gate).
- **Nethereum.EngineApi.Hosting** — the JWT-authenticated Engine API endpoint used by Engine API mode.
- **Nethereum.EVM.Precompiles.Kzg** / **Nethereum.EVM.Precompiles.Bls** — the real KZG (EIP-4844) and BLS12-381 precompile backends the follower executes and serves `eth_call` with.
- **Nethereum.Model** — the shared block/transaction/P2P wire models.

**Package reference:** **Nethereum.Signer.Bls.Herumi** (the same release version as the other Nethereum packages) — native BLS verification for the light client's sync-committee signatures.

Targets `net8.0` and `net10.0`, and references `Microsoft.AspNetCore.App` (framework reference) for the JSON-RPC host.

## Running the server

The server binds configuration from the `MainnetChain` section of `appsettings.json`, command-line arguments, and environment variables (prefixed `MainnetChain__`). A minimal host is three calls:

```csharp
var builder = WebApplication.CreateBuilder(args);

var config = new MainnetChainServerConfig();
builder.Configuration.GetSection("MainnetChain").Bind(config);

builder.AddMainnetChainServer(config);   // registers the whole follower graph

var app = builder.Build();
app.MapMainnetChainEndpoints();          // POST / = JSON-RPC (single + batch), GET / = health
app.Run($"http://{config.Host}:{config.Port}");
```

Example `appsettings.json` for a follower that snap-bootstraps and serves RPC on 8545:

```json
{
  "MainnetChain": {
    "Host": "127.0.0.1",
    "Port": 8545,
    "DataDir": "/var/lib/nethereum-mainnet/data",
    "ListenPort": 30303,
    "SnapBootstrap": true,
    "PathKeyedState": true,
    "TrieNodeHistoryBlocks": 128,
    "TrieNodeHistoryIndex": true,
    "LightClient": {
      "BeaconEndpoint": "http://127.0.0.1:5052"
    }
  }
}
```

When `DataDir` is set, the host registers the RocksDB + DevP2P production composition by awaiting `AddMainnetNodeAsync(config, loggerFactory)` before building the container — it opens storage and loads genesis up front (awaited, not blocked), so the runtime is a ready DI singleton. Tests instead use `UseInMemoryBundleAndSource` with a scripted block source.

## Configuration

`MainnetChainServerConfig` (namespace `Nethereum.MainnetChain.Configuration`):

| Field | Type | Default | Purpose |
|---|---|---|---|
| `Host` | `string` | `"127.0.0.1"` | HTTP bind host |
| `Port` | `int` | `8545` | HTTP JSON-RPC port |
| `DataDir` | `string?` | `null` | RocksDB directory; setting it enables the production (RocksDB + DevP2P) composition |
| `TrustedPeer` | `string?` | `null` | Pinned `enode://` peer always dialed |
| `Verbose` | `bool` | `false` | Debug-level logging |
| `StartBlock` | `ulong` | `1` | Replay window start |
| `Blocks` | `ulong` | `ulong.MaxValue` | Replay window length (unbounded by default) |
| `TargetPeers` | `int` | `16` | Target peer-pool size |
| `HeadersBatch` | `int` | `192` | Header request batch size |
| `BodiesBatch` | `int` | `64` | Body request batch size |
| `CheckpointEvery` | `ulong` | `50_000` | Block interval between DB checkpoints |
| `KeepLatestCheckpoints` | `int?` | `5` | Checkpoint retention count |
| `JournalBlocks` | `int` | `128` | Value-history retention window: `N` = keep N blocks, `0` = archive (unbounded), `< 0` = disabled (historical state queries error) |
| `PathKeyedState` | `bool` | `true` | Path-keyed (PBSS-style) state store; `false` = legacy hash-keyed |
| `TrieNodeHistoryBlocks` | `int` | `128` | Trie-node history depth: `-1` off, `0` archive, `N` = N-block window |
| `TrieNodeHistoryIndex` | `bool` | `true` | Maintain the key-major index over trie-node history (as-of serving) |
| `BulkSync` | `bool` | `false` | Use the bulk-load save path for a fresh resync |
| `ListenPort` | `int` | `-1` | Inbound RLPx listener port; `< 0` disables inbound serving |
| `DisableDiscv5` | `bool` | `false` | Disable Discv5 peer discovery |
| `Discv5Port` | `int` | `0` | Discv5 UDP port (`0` = ephemeral) |
| `ContinueOnMismatch` | `bool` | `false` | Keep following past a state-root mismatch (diagnostic) |
| `SnapBootstrap` | `bool` | `false` | Opt in to snap/1 cold-start bootstrap |
| `SnapPhase1Only` | `bool` | `false` | Run only the Phase-1 block archive, then stop |
| `SnapPhase1First` | `bool` | `false` | Run Phase 1 to completion before Phase 2 (sequential, not concurrent) |
| `BackwardSkeletonPhase1` | `bool` | `true` | Lay the Phase-1 header skeleton backward from the pivot |
| `HeadersFrom` | `ulong?` | `null` | Header-sweep override start |
| `HeadersTo` | `ulong` | `0` | Header-sweep override floor |
| `ReceiptBackfill` | `bool` | `false` | Concurrently re-fetch/scrub receipts |
| `LightClient` | `LightClientConfigSection?` | `null` | Beacon light-client settings (see below) |
| `Engine` | `EngineConfigSection` | disabled | Engine API settings (see [Engine API mode](#engine-api-mode)) |
| `GenesisPath` | `string?` | `null` | standard `genesis.json` for a custom chain; requires `Engine.Enabled` |
| `EngineOnly` | `bool` (read-only) | computed | `true` when `Engine.Enabled` and `GenesisPath` are both set |
| `EnableRocksDbStatistics` | `bool` | `false` | Collect RocksDB statistics and log block-cache counters every 100 blocks |
| `PrefetchWorkers` | `int` | `0` | Dedicated threads that prefetch each block's transactions into the RocksDB caches; `0` = off |
| `StateRootWorkers` | `int` | `4` | Dedicated threads that update storage tries in parallel while a block's state root is computed; `1` = sequential, below `1` is refused |
| `RpcModules` | `string` | `"eth,net,web3"` | JSON-RPC modules served; add `txpool` or `debug` (`admin` is accepted but this server registers no `admin_*` method). `eth`, `net` and `web3` are on by default |
| `RpcAllowedHosts` | `string` | `""` | Host header names accepted besides `localhost` and the bind host, comma separated; `*` = any |
| `RpcCorsOrigins` | `string` | `""` | Browser origins sent CORS headers, comma separated; `*` = any; empty sends no CORS headers |
| `ReplayBlocks` | `ulong` | `0` | Performance-gate replay of N stored blocks (see [Replay](#replaying-stored-blocks)); `0` = off |
| `ReplayReport` | `string?` | `null` | Path of the JSON report `ReplayBlocks` writes |

`LightClientConfigSection`:

| Field | Type | Default | Purpose |
|---|---|---|---|
| `BeaconEndpoint` | `string?` | `null` | Beacon-node HTTP endpoint; presence enables the light-client gate |
| `TrustBeaconWithoutBls` | `bool` | `false` | Skip BLS verification of light-client updates (testing) |
| `WeakSubjectivityRoot` | `string?` | `null` | Trusted checkpoint block root to bootstrap from |
| `GenesisValidatorsRoot` | `string?` | `null` | Genesis validators root for signature domains |

## The LightClient consensus gate

The beacon light client is optional. When `LightClient.BeaconEndpoint` is set, `LightClientHostedService` keeps a `LightClientService` up to date (polling finality + optimistic + sync-committee updates on a ~12s cadence), and that state drives two things:

1. **Block-admission gate** — `LightClientConsensusBlockGate` implements `IConsensusBlockGate` and wraps the executor. For each block it checks the beacon-attested block hash at that height: a recorded hash that disagrees with the computed hash is a hard **reject** (chain-split signal); a block beyond the light-client cursor is **accepted** (graceful degradation). Without a beacon endpoint the gate is `AlwaysAcceptConsensusBlockGate`.

2. **Canonical tip / snap pivot source** — `TrustedHeaderCanonicalSource` implements `ICanonicalStateRootSource` over an `ITrustedHeaderProvider`. Over the light client it tracks the *optimistic* (attested, sync-committee-signed) head so the snap pivot stays inside the ~128-block window peers keep snapshots for; in Engine API mode the provider is `EngineForkchoiceHeaders`. Without a light client, the canonical source is `MainnetKnownCheckpoints` only (point lookups; no live tip).

## Engine API mode

Set `Engine.Enabled` (`--engine`, or the `MainnetChain:Engine` section) and the follower serves the Engine API on its own JWT-authenticated port, so a consensus client drives it with `newPayload` and `forkchoiceUpdated`. Nothing executes before the first `forkchoiceUpdated`.

`EngineConfigSection` (namespace `Nethereum.MainnetChain.Configuration`):

| Field | Type | Default | Purpose |
|---|---|---|---|
| `Enabled` | `bool` | `false` | Serve the Engine API (`--engine`) |
| `Port` | `int` | `8551` | Engine port (`--engine-port`) |
| `JwtSecretPath` | `string?` | `null` | Secret shared with the consensus client (`--engine-jwt-secret`); `null` = `<DataDir>/jwt.hex`, created if missing |
| `BindAddress` | `string` | `"127.0.0.1"` | Engine bind address |

In this mode the canonical tip is the `forkchoiceUpdated` head (`EngineForkchoiceHeaders`: the head is the optimistic tip, the finalized block is the finalized tip) and the beacon light client is not used: `Engine.Enabled` together with `LightClient.BeaconEndpoint` is refused at startup (`ConsensusStartupGuard.EngineWithLightClientMessage`). A `forkchoiceUpdated` head the node does not hold is found in the `newPayload` stash or fetched from peers by hash, and a peer's reply is used only when its header hashes to the requested hash. `newPayloadV3`, `V4` and `V5` compare `expectedBlobVersionedHashes` with the payload's blob transactions in order (a missing list is `-32602`, a mismatch is `INVALID`), and a payload with a transaction that does not decode is `INVALID`. The payload stash holds at most 128 blocks and an estimated 256 MiB of transaction data (`EnginePayloadStashOptions`), evicting the oldest first and always keeping the newest block.

```csharp
var services = new ServiceCollection();
services.AddLogging();
services.UseInMemoryBundleAndSource(source);
services.AddMainnetChainServer(new MainnetChainServerConfig
{
    Engine = { Enabled = true, Port = 8561, JwtSecretPath = "./jwt.hex" }
});

using var provider = services.BuildServiceProvider();
var engine    = provider.GetRequiredService<IEngineApiService>();           // FollowerEngineApiService
var canonical = provider.GetRequiredService<ICanonicalStateRootSource>();   // its Name contains "Engine"
```

`MapMainnetChainEndpoints` maps the Engine endpoint on the Engine port when the Engine API is configured.

### Custom genesis, engine-only

`--genesis <path>` (`GenesisPath`) builds the chain config from a standard genesis.json instead of the mainnet constants: chain id, fork block numbers and timestamps, blob schedule and deposit contract come from the file, and the follower resolves its fork from that schedule. It needs `--engine`; a genesis without the Engine API is refused at startup (`ConsensusStartupGuard.GenesisWithoutEngineMessage`).

With a genesis and the Engine API (`EngineOnly`) the node composes no peer pool, scheduler, header follow, snap bootstrap, history backfill, discovery, light client or progress reporter, serves no inbound peers, and takes its canonical tip from `forkchoiceUpdated` alone. On the first start the genesis allocation is written to the path-keyed RocksDB store, the genesis state root is computed and persisted, and the genesis block becomes the head; a data directory that already holds a different genesis is refused. A genesis that declares no system contracts gets the same predeploys as DevChain. `debug_setHead` is registered for an engine-only node when `RpcModules` includes `debug`: it rewinds to a block number through the reorg path the Engine API uses, refuses a block above the head, and the same payloads can be replayed to identical roots.

Flags also accept the `--name=value` form.

## Maintenance and replay

The maintenance flags (`--wipe-state`, `--compact-all`, `--rebuild-state-from-flat`, `--verify-flat`) are one-shots: the server applies a pending checkpoint restore, opens the store, runs `ChainNodeMaintenanceRunner`, and exits with its code (0 = ok) before composing peers, discovery or the hosted service. `--verify-flat` is write-free and exits `1` with a boot-recovery message when a restore is pending or the committed head's state root does not resolve. `--wipe-state` logs `state.wipe` progress. `AddMainnetNodeAsync` refuses a configuration that has a maintenance flag set; embedders call `MainnetNodeComposition.RunMaintenanceAsync` instead.

### Replaying stored blocks

`--replay-blocks <N>` (`ReplayBlocks`) re-executes the next N blocks after the executed head from the bodies already in the store, through the production composition (`MainnetChainNodeFactory`, the configured flush cadence, prefetch and state-root workers), and checks every state root against the stored header. It never fetches from the network. The first rejected block stops the run; the exit code is `0` when every root matched and `1` otherwise. `--replay-report <path>` writes a JSON report (`ReplayReport`) with the per-block wall time, allocation, GC counts, RSS, flush-window position and execution stages, the summary medians and p90, a per-window-position table, the RSS slope, and RocksDB cache deltas per flush window when `--rocksdb-statistics` is on.

## Failure and shutdown

An unhandled follower failure is logged at `Critical` and the process exits `1`. A RocksDB write failure that is not transient, reported through `IStoreFaultSource` and polled every 5 seconds, stops the node the same way. The hosted service waits for every background job it launched, on normal return, failure and cancellation, before the store is disposed, and the server's host shutdown timeout is infinite so an in-flight block finishes writing.

## Usage examples

**Compose the follower with no light client** (always-accept gate):

```csharp
var services = new ServiceCollection();
services.AddLogging();
services.AddMainnetChainServer(new MainnetChainServerConfig { DataDir = null });

using var provider = services.BuildServiceProvider();
var gate = provider.GetRequiredService<IConsensusBlockGate>(); // AlwaysAcceptConsensusBlockGate
```

**Compose with a beacon light client** (consensus gate + finality labels):

```csharp
var services = new ServiceCollection();
services.AddLogging();
services.AddSingleton<LightClientService>(/* your configured light-client service */);
services.AddMainnetChainServer(new MainnetChainServerConfig
{
    LightClient = new LightClientConfigSection { BeaconEndpoint = "http://127.0.0.1:5052" },
});

using var provider = services.BuildServiceProvider();
var gate   = provider.GetRequiredService<IConsensusBlockGate>();        // LightClientConsensusBlockGate
var cursor = provider.GetRequiredService<IFinalityCursorProvider>();    // LightClientFinalityCursorProvider
```

With a light client the canonical source becomes a `CompositeCanonicalStateRootSource` (pinned checkpoints + live light-client tip); without one it is `MainnetKnownCheckpoints` and `GetLatestAsync` returns `null`.

## Composition API

The hosting entry points (in `Nethereum.MainnetChain.Hosting`):

```csharp
// Register the follower DI graph (validation, consensus gate, node factory, RPC, hosted services).
IServiceCollection AddMainnetChainServer(this IServiceCollection services, MainnetChainServerConfig config)
WebApplicationBuilder AddMainnetChainServer(this WebApplicationBuilder builder, MainnetChainServerConfig config)

// Map POST / (JSON-RPC single + batch) and GET / (health); also the Engine endpoint when the Engine API is configured.
WebApplication MapMainnetChainEndpoints(this WebApplication app)

// Build + register the production node (opens RocksDB, loads genesis) — awaited before Build().
Task<IServiceCollection> AddMainnetNodeAsync(this IServiceCollection services, MainnetChainServerConfig config, ILoggerFactory loggerFactory)

// Register an in-memory bundle + a scripted block source (tests).
IServiceCollection UseInMemoryBundleAndSource(this IServiceCollection services, IBlockSource source)

// Register the Engine API for the follower (called by AddMainnetChainServer when config.Engine.Enabled).
static IServiceCollection AddMainnetEngine(this IServiceCollection services, MainnetChainServerConfig config)

// Run one maintenance operation (WipeState, CompactAll, RebuildStateFromFlat, VerifyFlat) and return its exit code.
static async Task<int> RunMaintenanceAsync(MainnetChainServerConfig config, ILogger logger, CancellationToken ct = default)

// Mainnet's chain config, or the one a custom genesis describes.
static ChainConfig BuildChainConfig(StandardGenesisDocument? genesis = null)
```

`AddMainnetEngine` throws `InvalidOperationException` when a beacon endpoint is also configured. `RunMaintenanceAsync` returns `1`, after logging the error, when the store cannot be opened or boot-recovered. `BuildChainConfig(null)` is chain id 1 on the mainnet fork schedule; given a genesis, the chain id, fork schedule, blob schedule, deposit contract and base fee come from it.

## Key components

| Type | Role |
|---|---|
| `AddMainnetChainServer` | Registers the full follower DI graph (validation, gate, node factory, RPC, hosted services). |
| `MapMainnetChainEndpoints` | Maps `POST /` (JSON-RPC single + batch) and `GET /` (health). |
| `MainnetNodeComposition` | Static class holding `AddMainnetNodeAsync` plus the config→storage mappers `BuildStorageOptions`, `BuildFlushCadence`, and the `--align-byhash-cursor` maintenance op `AlignByHashReindexCursorToFreezerHead`. |
| `AddMainnetNodeAsync` | Builds the runtime (opens RocksDB, loads genesis) and registers the store bundle + DevP2P peer pool, scheduler, and block source. Async so genesis load is awaited, not blocked; the host awaits it before `Build()`. |
| `UseInMemoryBundleAndSource` | Test composition: registers an in-memory `IChainStoreBundle` and a supplied `IBlockSource` in place of the RocksDB + DevP2P node. |
| `MainnetChainNodeFactory` | Builds the `FollowerChainNode`: the chain-agnostic follower executor stack wired with `MainnetChainHardforkRegistry` (real KZG/BLS backends) and the proof-of-work reward policy, wrapped in the configured `IConsensusBlockGate`. |
| `MainnetChainNodeAccessor` | Singleton holder for the running `FollowerChainNode`, so the RPC surface serves reads off the same node the follower loop drives (`Node`, `Set`, `HasValue`). |
| `MainnetChainHostedService` | Runs the optional snap-bootstrap, then the block-follower loop, with its background jobs beside it (header follow, tip-band bodies, history backfill, receipt scrub, state compaction and the store-fault watch). Its constructor that takes `IHostApplicationLifetime` is `[Obsolete]`; the host creates it through `ActivatorUtilities`. |
| `MainnetEngineComposition` | `AddMainnetEngine`: the Engine API graph (`FollowerEngineApiService`, the `newPayload` stash, `EngineForkchoiceHeaders`, `EngineHeaderResolver`, the shared write gate). |
| `EngineForkchoiceHeaders` | `ITrustedHeaderProvider` and `IEngineForkchoiceSink` fed by `forkchoiceUpdated`: `GetLatestOptimistic()` is the head, `GetLatestFinalized()` the finalized block; both throw `InvalidOperationException` before the first update. |
| `TrustedHeaderCanonicalSource` | `ICanonicalStateRootSource` over any `ITrustedHeaderProvider` (the light client or the Engine headers); its `Name` is the provider's name plus `(optimistic)` or `(finalized)`. |
| `DebugSetHeadHandler` | `debug_setHead` for an engine-only node. |
| `RocksDbCacheStatsReporter` | Registered when `EnableRocksDbStatistics` is on; logs `rocksdb.cache.stats` every 100 blocks. |
| `MainnetReplayRunner` | `--replay-blocks`: re-executes stored blocks and writes a `ReplayReport` (`ReplayRecorder` collects the per-block records, `ReplaySummaryCalculator` the medians). |
| `LightClientHostedService` | Keeps the beacon light client updated (only when a beacon endpoint is configured). |
| `SnapSyncProgressReporter` | 8-second heartbeat: Phase 1/2/3 progress, peer summary, canonical-staleness watchdogs. Not registered for an engine-only node. |
| `SnapBootstrapInvoker` | Gated on `SnapBootstrap`; drives the shared snap orchestrator with mainnet fork activations. |
| `ConsensusStartupGuard` | Startup safety check. With no beacon endpoint the consensus gate is permissive, so `Evaluate` returns `RefuseNoBeacon` unless `AllowUnverifiedConsensus` is set (the server refuses to start rather than follow unverified). With `Engine.Enabled` it returns `EngineDriven`, or `RefuseEngineWithLightClient` when a beacon endpoint is also set; a `GenesisPath` without the Engine API is `RefuseGenesisWithoutEngine`. |
| `MainnetChainServerConfig` | All configuration (see table above). |

## Notes

- Block execution follows mainnet's fork schedule (`MainnetChainActivations`) with chain id 1 and the proof-of-work reward policy, wired in `MainnetChainNodeFactory`; there is no hardfork toggle on the config. With `GenesisPath` the fork schedule and chain id come from the genesis instead.
- The inbound listener checks each peer's `Status` (network id, genesis, fork id and eth version) with `EthStatusValidator` and refuses a peer on another chain before serving it; the follower no longer mirrors a peer's `Status` back.
- The JSON-RPC port serves only the modules in `RpcModules`, answers `403` to a `Host` name that is not `localhost`, the bind host, an IP address or listed in `RpcAllowedHosts`, `415` to a content type other than JSON, and sends no CORS headers unless `RpcCorsOrigins` is set.
- The runnable server lives in **Nethereum.MainnetChain.Server** (tool command `nethereum-mainnetchain`); this package is the embeddable library behind it.
