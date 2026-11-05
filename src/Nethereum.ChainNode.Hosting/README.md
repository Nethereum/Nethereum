# Nethereum.ChainNode.Hosting

`Nethereum.ChainNode.Hosting` is the shared node-composition layer used by every Nethereum-hosted
chain — the mainnet follower (`Nethereum.MainnetChain`), the AppChain server
(`Nethereum.AppChain.Server.Core`), and the single-process DevChain (`Nethereum.DevChain`). It wires
storage (RocksDB or in-memory), the DevP2P peer pool and listener, the mempool, and the sync stack
into one `ChainNode` object from a single `ChainNodeConfig`. A host supplies an `IChainDefinition`
(genesis + chain profile + tip source) and gets back a running node; it does not have to hand-assemble
`PeerPoolManager`, `PeerListener`, `RelayMempool` and friends itself. Starting an RPC server and peer
discovery (discv4/discv5/DNS) are **not** part of this library — those stay the host's job. What it does
carry for the hosts is the one JSON-RPC HTTP endpoint they all map (`RpcHttpEndpoint`) and the host,
origin and module settings that endpoint reads.

## Compose a node

```csharp
using Nethereum.ChainNode.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;

// A preset gives you a coherent starting point; presets set Storage + Sync.Mode
// (and, for SnapSyncV2, Sync.Snap.AdvertiseSnap2).
var config = ChainNodeConfigFactory.Create(ChainNodePreset.Pruned);
config.Storage.DataDirectory = "./my-chain-data";
config.Network.ListenPort = 30303;

// definition: your IChainDefinition — supplies genesis, IChainProfile, and the tip source.
var node = await ChainNode.StartAsync(definition, config, loggerFactory);

// node.Bundle     -> IChainStoreBundle (blocks/state/receipts/...)
// node.Mempool    -> ChainNodeMempool (TxPool, Relay, BroadcastPool)
// node.Listener   -> PeerListener (inbound eth/snap serving), null if Network.Serve == false
// node.Sync       -> ChainNodeSyncStack (peer pool, scheduler, block source), null if Sync.Mode == None

await node.DisposeAsync();
```

`ChainNode.StartAsync` is `ComposeAsync` + `StartServingAsync` + `StartSyncAsync` in one call. Compose
and start the stages separately when you need to do something in between (wire tx-submission off the
mempool before peers connect, pass serve callbacks, or swap the storage backend):

```csharp
var node = await ChainNode.ComposeAsync(definition, config, loggerFactory,
    storageFactory: (cfg, logger) => ChainNodeStorage.Open(cfg, logger, mySigner));

await node.StartServingAsync(new ChainNodeServeCallbacks
{
    TrustedTransactionsReceived = msg => { /* ... */ },
});

await node.StartSyncAsync(minPeerLatestBlockFactory: b => Task.FromResult(b.Metadata.GetLastBlock() + 1));
```

`StartServingAsync` no-ops (returns the existing/null `Listener`) when `Network.Serve` is `false`. When it
does start a listener it also starts `node.BlockRangeAnnouncer`, which tells every eth/69+ peer the head
(`BlockRangeUpdate`) as it moves; peers pull the new blocks, and a block-range update received from a peer
reaches `ChainNodeServeCallbacks.BlockRangeUpdateReceived`.
`StartSyncAsync` no-ops when `Sync.Mode` is `SyncMode.None`. Both are idempotent — calling them again
returns the already-started instance.

`ChainNodePreset` (`InMemory`, `Pruned`, `Archive`, `SnapSync`, `SnapSyncV2`) sets `Storage` and
`Sync.Mode` (and, for `SnapSyncV2`, `Sync.Snap.AdvertiseSnap2`); use
`AsRole(ChainNodeRole.Signer|Follower|GossipNode)` afterwards to set the mempool's
retention/relay policy (`ChainNodeConfigFactory.AsRole`).

## Config reference

Every field below is on `ChainNodeConfig` (`Configuration/ChainNodeConfig.cs`), one sub-config per
section. "Level" is beginner-safe (defaults work, safe to leave alone) or expert (changes storage
format, protocol behaviour, or is a named opt-in seam).

### `Storage` — `ChainNodeStorageConfig` (`Configuration/ChainNodeStorageConfig.cs`)

| Field | Meaning | Default | Level |
|---|---|---|---|
| `DataDirectory` | RocksDB path; ignored when `InMemory` is true. | `"./chain-data"` | beginner |
| `InMemory` | Use the in-memory store bundle instead of RocksDB (tests, ephemeral chains). | `false` | beginner |
| `PathKeyedState` | Path-keyed vs. hash-keyed state trie on disk. Set via preset, not by hand. | `false` | expert |
| `JournalBlocks` | Historical-state journal window: `-1` disables the journal, `0` = full archive (`HistoricalStateOptions.FullArchive`), `>0` = pruning window of that many blocks. | `128` | expert |
| `TrieNodeHistoryBlocks` | Trie-node history retention window; only meaningful when `PathKeyedState` is true. | `-1` | expert |
| `TrieNodeHistoryIndex` | Build the trie-node history index. | `false` | expert |
| `SplitHistoryStore` | Split hot/cold history storage. | `false` | expert |
| `HotWindowBlocks` | Size of the hot window when `SplitHistoryStore` is on. | `128` | expert |
| `PromotionEnabled` | Enable cold→hot promotion in the split store. | `false` | expert |
| `UseFreezerHistory` | Use the append-only freezer store for history. | `false` | expert |
| `FreezerHistoryDirectory` | Freezer directory; `null` uses the storage layer's own default. | `null` | expert |
| `BackgroundFreezeIndexing` | Build freezer indexes on a background sweep. | `true` | expert |
| `FreezerBackgroundDegreeOfParallelism` | Parallelism for the background freeze-index sweep; `0` = default. | `0` | expert |
| `BlockCacheSize` | RocksDB block cache, bytes. | `1 GiB` | expert |
| `EnableRocksDbStatistics` | Collect RocksDB statistics and log block-cache counters. | `false` | expert |
| `FlushCadenceBlocks` | Flush every N blocks; `1` = flush every block. Live on every node kind. | `1` | expert |
| `PrefetchWorkers` | Dedicated threads that prefetch each block's transactions into the state caches; `0` = off. Only the mainnet follower composes the prefetcher. | `0` | expert |
| `StateRootWorkers` | Dedicated threads that update storage tries in parallel while a block's state root is computed; `1` = sequential. Only the mainnet follower passes it; AppChain and DevChain run with the default. | `4` | expert |
| `EnableLogIndex` | Build the log/filter index. | `false` | expert |

`ChainNodeStorage.BuildStorageOptions` maps this 1:1 onto
`RocksDbStorageOptions`, except `JournalBlocks` and `FlushCadenceBlocks`; `BuildJournalOptions` implements the
`JournalBlocks` tri-state above.

### `Network` — `ChainNodeNetworkConfig` (`Configuration/ChainNodeNetworkConfig.cs`)

| Field | Meaning | Default | Level |
|---|---|---|---|
| `Serve` | Accept inbound eth/snap connections. `StartServingAsync` no-ops when false. | `true` | beginner |
| `ListenPort` | Inbound RLPx port. | `30303` | beginner |
| `BindAddress` | Inbound bind address. | `IPAddress.Any` | beginner |
| `DialBudgetPerSecond` | Outbound dial-rate limit. | `5` | expert |
| `MaxPeersPerIPv4Subnet` / `MaxPeersPerIPv6Subnet` | Anti-eclipse peer caps per subnet. | `10` / `10` | expert |
| `NodeKeyFile` | Path to the persisted node identity key; defaults to `<DataDirectory>/nodekey` when unset. An in-memory node with neither `NodeKeyFile` nor `NodeKeyHex` generates its own ephemeral key, kept on the config so later resolutions return the same identity. | `null` | beginner |
| `NodeKeyHex` | Node identity key as hex; wins over `NodeKeyFile` when set. | `null` | expert |
| `TrustedPeers` | Enode URLs **dialed** (via `ResolveDialEnodes`) and marked trusted. | `[]` | expert |
| `TrustedBootnodes` | Enode URLs marked as trusted **dial** targets only — see "Trust and dialing" below. | `[]` | expert / seam |
| `TrustedNodeIds` | Additional node-ids trusted for **inbound** connections (not full enode URLs). | `[]` | expert / seam |
| `TargetPeerCount` | Steady-state peer pool size. | `16` | beginner |
| `MaxConcurrentDials` | Outbound dial concurrency. | `10` | expert |
| `MaxInboundPeers` | Inbound peer cap. | `25` | expert |
| `MaxInboundPerIP` | Inbound peer cap per IP. | `9` | expert |
| `HandshakeTimeoutMs` | RLPx/eth handshake timeout. | `5000` | expert |
| `IdleTimeout` | Peer idle disconnect timeout. | `30 s` | expert |
| `MirrorRemoteStatus` | Serve a status reply that mirrors what the connecting peer sent instead of this node's real status. | `false` | expert / seam — see note below |
| `ClientId` | Client id string advertised in the eth handshake. | `"Nethereum"` | beginner |
| `Discovery` | `ChainNodeDiscoveryConfig` (Discv4/Discv5 toggles+ports). Live on mainnet (`MainnetNodeComposition.cs`). On AppChain/DevChain it is explicitly disabled by default and a validator (`ChainNodeDiscoveryValidator.RefuseIfRequested`) throws `InvalidOperationException` at startup if you try to turn it on. | new instance | n/a |

**`MirrorRemoteStatus` note**: the underlying `PeerListenerOptions.MirrorRemoteStatus`
(`Nethereum.DevP2P.Sync/Serving/PeerListenerOptions.cs`) defaults to **`true`**. Because
`ChainNodeServeListener.BuildListenerOptions` always sets it from `config.Network.MirrorRemoteStatus`
(`ChainNodeServeListener.BuildListenerOptions`), which defaults to **`false`**, a `ChainNode`-composed listener
defaults to serving its own real status (built from genesis/fork thresholds/head — see
`BuildStatusTemplateAsync`), not the mirrored behaviour you'd get
from `PeerListenerOptions` directly. `BuildStatusTemplateAsync` runs unconditionally on every
`StartServingAsync` call, even though its result is only used when `MirrorRemoteStatus` is false.

### `Sync` — `ChainNodeSyncConfig` (`Configuration/ChainNodeSyncConfig.cs`)

| Field | Meaning | Default | Level |
|---|---|---|---|
| `Mode` | `SyncMode.None` \| `ForwardExecute` \| `Snap`. `StartSyncAsync` no-ops on `None`. | `ForwardExecute` | beginner |
| `FollowPeerEnode` | Single enode to prepend to the dial list; also gates `ChainNodeConfig.FollowsAPeer`. | `null` | beginner |
| `TrustedPeersOnlyTip` | Only compute the canonical tip from trusted peers (`PeerHeadCanonicalSource`). | `false` | expert |
| `FloorTargetPeerCountByDialPool` | Opt-in seam: raise the pool's effective `TargetPeerCount` to `max(TargetPeerCount, dialTargets.Count)` so every configured dial target gets a slot. AppChain turns this on. | `false` | expert / seam |
| `HeaderBatchSize` / `BodyBatchSize` | Batch sizes for `DevP2PBlockSource`. | `192` / `64` | expert |
| `MaxInFlightPerPeer` | Scheduler's max in-flight requests per peer. | `1` | expert |
| `MinPeerLatestBlock` | Don't connect to peers whose advertised head is below this block. Overridable per-call via `StartSyncAsync(minPeerLatestBlockFactory:)`. | `0` | expert / seam |
| `BulkSync` | Passed straight to `RocksDbChainStoreBundle.Open(bulkSync:)` — the only field here consumed by `ChainNodeStorage`, not by the sync stack. | `false` | expert |
| `StartBlock`, `Blocks`, `HeadersFrom`, `HeadersTo`, `CheckpointEvery`, `KeepLatestCheckpoints`, `ReceiptBackfill`, `ContinueOnMismatch` | Declared; the shared config-to-`FollowerOptions` translation that wires these into `ChainNode.Hosting` is not wired yet (tracked as NotYetWired in `ChainNodeConfigSurface`). AppChain's follower hardcodes its own values today instead of reading them (`AppChainDevP2PFollower.cs`). | see source | n/a |
| `Snap` | `ChainNodeSnapConfig` — see below. | new instance | — |
| `SnapBootstrap` | Convenience get/set that aliases `Mode == SyncMode.Snap`. Setting it `true` selects `Snap`; setting it `false` moves `Mode` from `Snap` to `ForwardExecute` and leaves any other mode as it was. | computed | beginner |

`ChainNodeSnapConfig` (`Configuration/ChainNodeSnapConfig.cs`):

| Field | Meaning | Default | Level |
|---|---|---|---|
| `AdvertiseSnap2` | Advertise snap/2 in the handshake worker and to inbound peers. Set by the `SnapSyncV2` preset. | `false` | expert / seam |
| `SoftResponseLimit` | Cap on snap response size served by `PatriciaSnapRequestHandler`; `null` uses that handler's own default. | `null` | expert |
| `BackwardSkeletonPhase1`, `Phase1Only`, `Phase1First` | Declared; same NotYetWired status as the `Sync` catch-up fields above — AppChain hardcodes `UseBackwardSkeleton=true` and never reads `Phase1Only`/`Phase1First` today. | `true` / `false` / `false` | n/a |

### `Mempool` — `ChainNodeMempoolConfig` (`Configuration/ChainNodeMempoolConfig.cs`)

| Field | Meaning | Default | Level |
|---|---|---|---|
| `MaxPoolSize` | Max pooled transactions (default `TxPool`). | `5000` | beginner |
| `MaxTxsPerSender` | Max pooled transactions per sender (default `TxPool`). | `64` | beginner |
| `Retention` | `MempoolRetention.Full` \| `RelayOnly`. Set per-role by `AsRole`. | `Full` | beginner |
| `Relay` | `MempoolRelay.Ours` \| `All`. Set per-role by `AsRole`. | `Ours` | beginner |
| `EnableTrustedPeerAdmission` | Opt-in seam: wires `PeerListenerOptions.OnTrustedTransactionsReceived[From]` to `ChainNodeMempool.TrustedAdmission[From]`, so transactions pushed by a **trusted** peer are admitted to the local pool without going through normal gossip admission. The field defaults `false`, but **AppChain turns it on** via `AppChainServerConfig.DefaultNode()`; mainnet and DevChain leave it off. | `false` (field); AppChain sets `true` | expert / seam |
| `PoolFactory` | Override how the `ITxPool` is built; bypasses `MaxPoolSize`/`MaxTxsPerSender` if set. | `null` | expert |

`ChainNodeConfigFactory.AsRole`: `Signer` → `Full`/`Ours`,
`Follower` → `RelayOnly`/`Ours`, `GossipNode` → `Full`/`All`.

### `Rpc` — `ChainNodeRpcConfig` (`Configuration/ChainNodeRpcConfig.cs`)

`ChainNode.Hosting` does not start an RPC server and `ChainNode` never reads this section itself — it
exists so a host can carry its RPC settings on the same `ChainNodeConfig` object it passes to `ChainNode`.
The hosts turn the section into endpoint options with `ChainNodeRpcConfigExtensions.ToHttpOptions` and
`ToModuleSet`, described under "RPC HTTP endpoint" below.

| Field | Meaning | Default | Level |
|---|---|---|---|
| `Host` | RPC bind host. Read by AppChain (`AppChainServerRunner.cs`); DevChain reads it through `DevChainServerConfig.Host`, which forwards onto this section. | `"127.0.0.1"` | beginner |
| `Port` | RPC bind port. Read by AppChain; DevChain reads it through `DevChainServerConfig.Port`, which forwards onto this section. Validated by `AppChainServerConfigValidator`. | `8545` | beginner |
| `MetricsPort` | Live on AppChain and DevChain: gates `Nethereum.Aspire.ServiceDefaults`' `AddPrometheusMetrics`/`MapPrometheusMetrics`. | `0` | beginner |
| `MaxLogBlockRange` | Live on AppChain and DevChain via `ChainNodeRpcConfigExtensions.ApplyTo` → `CoreChain.ChainConfig.RpcMaxLogBlockRange`, enforced by `LogQueryGuards.EnforceBlockRangeCap`. | `10000` | expert |
| `MaxLogResults` | Live the same way, enforced by `LogQueryGuards.EnforceResultCap`. | `10000` | expert |
| `GasCap` | Live the same way, read by `ChainNodeBase.cs` for `eth_call`/`estimateGas`/`traceCall`. | `50000000` | expert |
| `Modules` | Comma-separated JSON-RPC modules served: `eth`, `net` and `web3` are the default; add `txpool` or `debug` (`admin` is also accepted on AppChain; the mainnet follower registers no `admin_*` method). An unknown name throws `ArgumentException`. Live on AppChain and the mainnet follower (`MainnetChain:RpcModules`); DevChain always serves its full development method set. | `"eth,net,web3"` | expert |
| `AllowedHosts` | Host header names accepted besides `localhost` and the bind host; `*` = any. | `""` | expert |
| `AllowedOrigins` | Browser origins allowed to open the WebSocket; `*` = any, none by default. Live on AppChain; DevChain serves no WebSocket endpoint. | `""` | expert |
| `Url` | Computed `http://{Host}:{Port}`. | computed | beginner |

### RPC HTTP endpoint — `RpcHttpEndpoint` (`Rpc/RpcHttpEndpoint.cs`)

AppChain, DevChain and the mainnet follower map the same `POST /` endpoint:

```csharp
public static void MapRpcHttpEndpoint(
    this WebApplication app,
    IRpcDispatcher dispatcher,
    JsonSerializerOptions serializerOptions,
    RpcHttpOptions options,
    ILogger? logger)
```

It answers `403 invalid host specified` when the `Host` header fails `RpcHostPolicy`, `413` when the body is
larger than `RpcHttpOptions.MaxRequestBodySize` (10 MiB), and `415` unless the content type is
`application/json`, `application/json-rpc` or `application/jsonrequest`. The body is handled by the CoreChain
`JsonRpcEnvelope`, which caps a batch response at `RpcHttpOptions.MaxResponseSize` (25,000,000 bytes).

`RpcHostPolicy` accepts a missing `Host`, any IP literal, `localhost`, `RpcHttpOptions.BindHost`, the names in
`AllowedHosts`, and every name when the list holds `*`. `RpcOriginPolicy` accepts a request with no `Origin`
header, and otherwise only an origin that matches an `AllowedOrigins` entry by scheme, host and port (`*` = any).
AppChain and the mainnet follower use the default host policy; DevChain adds `*` when `AllowedHosts` is empty.

`ChainNodeConfigSurface` (`Configuration/ChainNodeConfigSurface.cs`) is now the single source of truth for
whether any `ChainNodeConfig` field is Live/Disabled/NotYetWired on AppChain and DevChain, driving both
hosts' `--help-advanced` output; a reflection-based coverage test
(`ChainNodeConfigSurfaceCoverageTests`) fails the build if a field is added to `ChainNodeConfig` without
a matching entry there. The per-field notes below predate that descriptor and are being superseded by it
field by field — treat `ChainNodeConfigSurface.All` as authoritative over this table
where the two disagree.

### `Producer` — `ChainNodeProducerConfig` (`Configuration/ChainNodeProducerConfig.cs`)

Block-producing nodes (AppChain and DevChain) share one section for the block gas limit and base fee. A mainnet
follower takes its gas limit from the headers it imports and has no `Producer` section of its own.

| Field | Meaning | Default | Level |
|---|---|---|---|
| `BlockGasLimit` | Gas limit of produced blocks; must be positive when set (`ChainNodeProducerValidator.Validate` throws `InvalidOperationException` naming the section). | `null` (the chain's own default) | expert |
| `BaseFee` | Base fee of produced blocks, in wei (`ulong`). | `null` (the chain's own default, 1 gwei) | expert |

```csharp
public static void ApplyTo(this ChainNodeProducerConfig producer, ChainConfig target)
```

`ApplyTo` copies the values that are set onto the chain config; AppChain and DevChain call it when they build
their live chain config. `DevChainServerConfig.BlockGasLimit` keeps its public setter and forwards onto this section; AppChain reads the section from `AppChainServerConfig.Node.Producer`.

### `Maintenance` — `ChainNodeMaintenanceConfig` (`Configuration/ChainNodeMaintenanceConfig.cs`)

Live on all three nodes via the shared `ChainNodeMaintenanceRunner` (`ChainNodeMaintenanceRunner.cs`):
every host calls `ChainNodeMaintenanceRunner.AnyRequested`/`RunAndReportExitCodeAsync` against
this exact `ChainNodeConfig.Maintenance` object before composing/serving, and the process exits with the
run's result (0=ok, non-zero=failure) rather than falling through to normal startup.

| Field | Meaning | Default |
|---|---|---|
| `Verbose` | Verbose maintenance logging. | `false` |
| `WipeState` | Wipe state before boot. | `false` |
| `CompactAll` | Force a full RocksDB compaction. | `false` |
| `RebuildStateFromFlat` | Rebuild the trie from the flat state store. | `false` |
| `VerifyFlat` | Verify flat-state consistency. | `false` |
| `VerifyFlatSampleAccountsPerShard` | Sample size for `VerifyFlat`. | `0` |

## Trust and dialing model

Three lists interact, and they are **not** symmetric — this is the subtlest part of the config surface.

- **`Network.TrustedPeers`** (enode URLs): fed into `ChainNodeConfig.ResolveDialEnodes()`
  (`ChainNodeConfig.ResolveDialEnodes`, prepended by `Sync.FollowPeerEnode` when set). These enodes are
  **actively dialed** — `ChainNodeSyncStack.DialTargets` includes them, and `StartPoolAsync` calls
  `pool.EnqueueCandidate(enode)` for each one. Their node-ids also
  flow into `ResolveTrustedNodeIds()` (below), so they're trusted **inbound** too.
- **`Network.TrustedBootnodes`** (enode URLs): only ever combined with `ResolveDialEnodes()` to build
  `trustedDialKeys` for the `PeerPoolManager` constructor (`TrustedDialTargets`). They mark a peer as trusted **if/when it connects**, but they
  are **not** enqueued as dial candidates — `TrustedBootnodes` alone will not cause an outbound
  connection. Also note: `TrustedBootnodes` is **not** included in `ResolveTrustedNodeIds()`, so it has
  no effect on inbound trust either. Its only effect today is outbound trust-marking in the dial pool.
- **`Network.TrustedNodeIds`** (node-ids, not enode URLs): combined with the node-ids parsed out of
  `ResolveDialEnodes()` (i.e. `TrustedPeers` + `FollowPeerEnode`, not `TrustedBootnodes`) to build
  `ResolveTrustedNodeIds()`, which becomes
  `PeerListenerOptions.TrustedNodeIds` — governing which **inbound** connections are treated as trusted
  (e.g. eligible for `EnableTrustedPeerAdmission`'s bypass).
- Independently, `profile.Bootnodes` (from `IChainProfile`, chain-specific — mainnet bootnodes, AppChain
  seed nodes, etc.) are appended to the dial list in `DialTargets` but
  carry no trust.

A malformed enode in `Sync.FollowPeerEnode`, `Network.TrustedPeers` or `Network.TrustedBootnodes` refuses the
start instead of being logged and dialled. The hosts call the validator at their configuration boundary,
before storage, the node key or genesis is touched:

```csharp
public static void RefuseMalformed(ChainNodeConfig node, string configPath)
```

It throws `InvalidOperationException` naming every offending key, for example `DevChain:Node:Network:TrustedPeers:1`.
The chain profile's bootnodes are checked the same way when the sync stack starts.
`ChainNodeNetworkConfig.SplitEnodeList` turns a comma-separated list into trimmed entries, which is how the
mainnet `--trusted-peer` value and the AppChain peer flags are read.

In short: `TrustedPeers` = dial + trust (both directions); `TrustedBootnodes` = trust-if-dialed-elsewhere,
outbound only; `TrustedNodeIds` = inbound trust only, independent of dialing.

## How the three hosts use it

- **AppChain** (`src/Nethereum.AppChain.Server.Core/AppChainComposition.cs`) calls
  `Nethereum.ChainNode.Hosting.ChainNode.StartAsync` directly (compose+serve+sync in one call), then
  layers consensus (Clique or producer-authority), messaging, and MUD deployment on top using
  `chainNode.Bundle`, `chainNode.Mempool`, `chainNode.Listener`. It's the only host that turns on
  `Sync.FloorTargetPeerCountByDialPool` — set in `DefaultNode()`'s
  `Sync` block (`AppChainServerConfig.DefaultNode`; it also turns on
  `Mempool.EnableTrustedPeerAdmission`).
- **Mainnet** (`src/Nethereum.MainnetChain/Hosting/MainnetNodeComposition.cs`) wraps `ChainNode` in an
  `IHostedService` (`MainnetNodeRuntime`). It calls `ChainNode.ComposeAsync` with a custom
  `storageFactory` (to inject a signer), runs its own boot-recovery/integrity gate
  (`_recoveryGate.EnsureConsistentOrEscalate`), then `StartSyncAsync` with a
  `minPeerLatestBlockFactory`, and only afterwards — in its own `StartAsync` — starts DNS seeding,
  inbound serving (`StartServingAsync`), and discv4/discv5 discovery. **Starting discv4/discv5
  discovery is mainnet's own orchestration responsibility** (it runs in `MainnetNodeRuntime`, not in
  `ChainNode.Hosting`), but it reads its discovery settings off the shared
  `ChainNodeConfig.Network.Discovery` sub-config: `MainnetNodeRuntime` consults
  `Network.Discovery.DisableDiscv4`/`Discv4Port`/`DisableDiscv5`/`Discv5Port`
  (`MainnetNodeComposition.cs`), reading only `ListenPort` off the flat
  `MainnetChainServerConfig` (`MainnetNodeComposition.cs`). That sub-config is itself seeded from
  the flat CLI fields by `MainnetChainNodeConfigFactory.MapNetwork`
  (`Configuration/MainnetChainNodeConfigFactory.cs`).
- **DevChain** (`src/Nethereum.DevChain/Hosting/DevChainComposition.cs`) does **not** call
  `ChainNode.ComposeAsync`/`StartAsync` at all — it already owns its storage bundle (`DevChainNode`) and
  instead calls the same lower-level building blocks `ChainNode` itself calls internally:
  `ChainNodeMempool.Create`, `ChainNodeServeListener.StartAsync` (gated on `Network.Serve`), and
  `ChainNodeSyncStack.StartAsync` (gated on `Sync.Mode != None`), wiring them together by hand
  (`DevChainComposition.cs`).

## Confusing/inert surface (read the code, not the field name)

**Superseded by `ChainNodeConfigSurface`** (`Configuration/ChainNodeConfigSurface.cs`) for AppChain and
DevChain: every field below now has one authoritative entry there, marked `Live`/`Disabled`/
`NotYetWired`, with a reflection-checked known consumer for every `Live` entry
(`ChainNodeConfigSurfaceCoverageTests`) — read that file, not this list, for the current AppChain/
DevChain truth. This list is kept only as history and for mainnet, which the descriptor does not cover
yet (its CLI keys are still flat, tracked separately):

1. **`Network.Discovery` (`ChainNodeDiscoveryConfig`)** — live on mainnet; on AppChain/DevChain now
   `Disabled` (a validator throws if you try to enable it), not silently inert.
2. **`Maintenance` (`ChainNodeMaintenanceConfig`)** — `Live` on all three nodes via the shared
   `ChainNodeMaintenanceRunner`.
3. **`Sync.{StartBlock,Blocks,HeadersFrom,HeadersTo,CheckpointEvery,KeepLatestCheckpoints,ReceiptBackfill,ContinueOnMismatch}`**
   — still `NotYetWired` on AppChain/DevChain (the config-to-`FollowerOptions` translation is not wired yet); mainnet's own backward-walk/checkpoint/receipt-backfill code reads its own flat fields, not
   this mirror.
4. **`Sync.Snap.{BackwardSkeletonPhase1,Phase1Only,Phase1First}`** — same `NotYetWired` status as above.
   Only `AdvertiseSnap2` and `SoftResponseLimit` are `Live`.
5. **`Rpc.{MetricsPort,MaxLogBlockRange,MaxLogResults,GasCap}`** — now `Live` on AppChain/DevChain.
6. **`Storage.FlushCadenceBlocks`** — `Live` on every node kind through `FlushCadences.ForBlocks`;
   `ChainNodeStorage.BuildStorageOptions` does not read it. `Storage.PrefetchWorkers` and
   `Storage.StateRootWorkers` are `NotYetWired` on AppChain and DevChain: only the mainnet follower composes
   them.
7. **`Network.MirrorRemoteStatus`** defaults to `false` through this library, the opposite of the
   underlying `PeerListenerOptions.MirrorRemoteStatus` default of `true` — see the note under `Network`
   above.

If you're extending one of these fields, add it to `ChainNodeConfigSurface.All` — the coverage test
fails the build otherwise — rather than assuming setting it changes behaviour.
