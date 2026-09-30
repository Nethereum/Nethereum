# Nethereum.MainnetChain

> **PREVIEW** — This package is in preview. APIs may change between releases.

A read-only Ethereum mainnet follower and serving node, hostable from a CLI, .NET host, or tests.

## Overview

Nethereum.MainnetChain composes a full mainnet-following node out of the lower-level Nethereum building blocks: it syncs and validates blocks from the devp2p network, keeps a local state database, and serves that state back out over JSON-RPC and (optionally) to other devp2p peers. It is *read-only* — it follows and serves the chain, it does not propose or mine.

It brings together:

- **Block following & validation** — executes and validates mainnet blocks through the CoreChain executor stack (`FollowerChainNode`), computing and checking state roots.
- **DevP2P sync** — cold-starts from a snapshot via snap/1 sync and backfills the block archive, then follows the head (see [Nethereum.DevP2P.Sync](../Nethereum.DevP2P.Sync/README.md)).
- **Optional beacon LightClient consensus gate** — when a beacon endpoint is configured, a light client provides both a trusted canonical tip (for snap pivot selection) and a block-admission gate that rejects any block whose hash disagrees with the beacon-attested chain.
- **JSON-RPC serving** — `eth_*` / `net_*` / `web3_*` over HTTP, plus inbound snap/1 + eth serving to other peers.

Typical uses:

- **Run a self-hosted mainnet RPC endpoint** backed by your own synced state.
- **Serve snap/1 and eth** to other Nethereum or geth nodes on the network.
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
- **Nethereum.CoreChain** — the node/executor stack, stores, and validation.
- **Nethereum.CoreChain.RocksDB** — the persistent state + block database.
- **Nethereum.DevP2P** / **Nethereum.DevP2P.Sync** — peer networking and the sync engine.
- **Nethereum.Consensus.LightClient** — the beacon light client (optional consensus gate).
- **Nethereum.EVM.Precompiles.Kzg** / **Nethereum.EVM.Precompiles.Bls** — the real KZG (EIP-4844) and BLS12-381 precompile backends the follower executes and serves `eth_call` with.
- **Nethereum.Model** — the shared block/transaction/P2P wire models.
- **Nethereum.Documentation** — the `[NethereumDocExample]` traceability attribute.

**Package reference:** **Nethereum.Signer.Bls.Herumi** (`6.5.0`) — native BLS verification for the light client's sync-committee signatures.

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

2. **Canonical tip / snap pivot source** — `LightClientCanonicalSource` implements `ICanonicalStateRootSource`. It tracks the *optimistic* (attested, sync-committee-signed) head so the snap pivot stays inside the ~128-block window peers keep snapshots for. Without a light client, the canonical source is `MainnetKnownCheckpoints` only (point lookups; no live tip).

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

// Map POST / (JSON-RPC single + batch) and GET / (health).
WebApplication MapMainnetChainEndpoints(this WebApplication app)

// Build + register the production node (opens RocksDB, loads genesis) — awaited before Build().
Task<IServiceCollection> AddMainnetNodeAsync(this IServiceCollection services, MainnetChainServerConfig config, ILoggerFactory loggerFactory)

// Register an in-memory bundle + a scripted block source (tests).
IServiceCollection UseInMemoryBundleAndSource(this IServiceCollection services, IBlockSource source)
```

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
| `MainnetChainHostedService` | Runs the optional snap-bootstrap, then the block-follower loop. |
| `LightClientHostedService` | Keeps the beacon light client updated (only when a beacon endpoint is configured). |
| `SnapSyncProgressReporter` | 8-second heartbeat: Phase 1/2/3 progress, peer summary, canonical-staleness watchdogs. |
| `SnapBootstrapInvoker` | Gated on `SnapBootstrap`; drives the shared snap orchestrator with mainnet fork activations. |
| `ConsensusStartupGuard` | Startup safety check: with no beacon endpoint the consensus gate is permissive, so `Evaluate` returns `RefuseNoBeacon` unless `AllowUnverifiedConsensus` is set (the server refuses to start rather than follow unverified). |
| `MainnetChainServerConfig` | All configuration (see table above). |

## Notes

- Block execution follows mainnet's fork schedule (`MainnetChainActivations`) with chain id 1 and the proof-of-work reward policy, wired in `MainnetChainNodeFactory`; there is no hardfork toggle on the config.
- Setting `NETHEREUM_WIPE_SNAP_STATE=1` performs a one-shot wipe of snap state/trie CFs on startup (keeps headers/bodies/receipts and their cursors) — used to restart Phase 2 cleanly on an existing archive.
- The runnable server lives in **Nethereum.MainnetChain.Server** (tool command `nethereum-mainnetchain`); this package is the embeddable library behind it.
