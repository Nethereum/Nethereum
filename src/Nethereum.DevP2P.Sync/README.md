# Nethereum.DevP2P.Sync

> **PREVIEW** — This package is in preview. APIs may change between releases.

The block and state sync engine for a Nethereum node syncing Ethereum over `eth/68`–`eth/71` and `snap/1`.

## Overview

Nethereum.DevP2P.Sync cold-starts and keeps a node in sync with the Ethereum devp2p network. It implements a **snap-first** sync in three phases, every one of which verifies what it fetches against cryptographic commitments so a peer cannot poison the local database:

- **Phase 1 — block archive backfill.** Download headers, bodies (transactions + uncles), and receipts for `[genesis..pivot]`. Bodies and receipts are content-addressed against the header's `TransactionsHash` / `UnclesHash` / `ReceiptHash`, and the parent-hash chain is validated within and across batches.
- **Phase 2 — snap state stream.** Stream the full account, storage, and bytecode state at a pivot block via snap range requests, verifying every range against a Merkle-Patricia proof and comparing the assembled trie root to the pivot's state root.
- **Phase 3 — heal.** Walk the reconstructed trie and re-fetch any missing nodes (and bytecode) until the computed root matches the target, following the pivot as the head advances.

The engine is chain-agnostic: it drives Ethereum mainnet (via [Nethereum.MainnetChain](../Nethereum.MainnetChain/README.md)) and Nethereum AppChains through the same orchestrator, differing only in the injected fork activations and canonical-tip source.

Typical uses:

- **Cold-start a node from snapshot state** instead of replaying the entire chain.
- **Backfill a full block + receipt archive** behind a snap-synced head.
- **Follow a rolling pivot** so state stays fresh while syncing a large chain.

## Installation

```bash
dotnet add package Nethereum.DevP2P.Sync
```

### Dependencies

**Nethereum packages (direct):** `Nethereum.DevP2P` (peer networking), `Nethereum.Model`, `Nethereum.CoreChain` (stores, state, snap-sync state model), `Nethereum.Util`, `Nethereum.Hex`, `Nethereum.RLP`. The `[NethereumDocExample]` / `DocSection` doc-tagging types used to trace README examples to source are supplied by `Nethereum.Util` (`Nethereum.Documentation.NethereumDocExampleAttribute`), not by a separate `Nethereum.Documentation` project reference. Proof verification (`Nethereum.Merkle.Patricia`) and `Microsoft.Extensions.Logging.Abstractions` arrive transitively, not as direct references. Targets `net8.0`, `net9.0`, `net10.0`.

## Architecture

```
SyncNode                             the "start here" facade — one object: serve state + snap-bootstrap
   └─ SnapSyncOrchestrator           anchor pivot, retry/backoff, follow rolling pivot
        └─ SnapBootstrapper          sequence one pivot: skip-if-synced -> Phase 2 -> heal-on-mismatch -> commit
             ├─ ParallelBlockBackfiller   Phase 1: header + body + receipt + persist stages over a BlockTaskQueue
             │     └─ BackwardBlockWalker  (skeleton mode) lay headers backward from a trusted tip
             ├─ SnapSyncClient            Phase 2: parallel account/storage/bytecode leaf stream -> ISnapSyncSink
             └─ TrieHealer                Phase 3: BFS re-fetch of missing trie nodes until root matches
```

`SyncNode` is the discoverable top-level entry point — construct it once and reach every sync capability from it. Under it, `SnapSyncOrchestrator` and `SnapBootstrapper` remain usable directly for advanced control, and the phase workers can be driven on their own (e.g. Phase 1 only). `SnapBootstrapper` and its partials live under `Snap/Bootstrap/`.

## Public entry points

**`SyncNode`** — the single discoverable entry point, in the spirit of `Nethereum.Web3.Web3`. Construct it once from the chain's storage bundle, activations and peering resources, then reach every sync capability from this one object. The underlying drivers remain available for advanced use; a consumer normally only needs this facade:

```csharp
public sealed class SyncNode
{
    public IPeerPool Peers { get; }                    // dial-out / inbound pool (null if not configured)
    public IFetchRequestScheduler Scheduler { get; }   // fans fetch requests across peers (null if not configured)
    public PeerListener Serving { get; }               // inbound listener; serves eth + snap (null if not serving)
    public RelayMempool Mempool { get; }               // good-citizen relay tx pool (null if not relaying)

    public SyncNode(
        IChainStoreBundle bundle, IChainActivations activations, ILogger logger,
        IPeerPool peers = null, IFetchRequestScheduler scheduler = null, PeerListener serving = null,
        RelayMempool mempool = null);

    public Task StartServingAsync(CancellationToken ct = default);   // bind the listener; no-op if not serving
    public Task<SnapBootstrapper.Result> RunSnapBootstrapAsync(      // cold-start state; delegates to the orchestrator
        ICanonicalStateRootSource canonicalTip, SnapSyncOrchestratorOptions options = null,
        CancellationToken ct = default);
}
```

**`SnapSyncOrchestrator`** (static) — top-level driver, and what `SyncNode.RunSnapBootstrapAsync` delegates to. Anchors the pivot from the injected canonical tip, runs the bootstrapper, and follows a rolling pivot with backoff:

```csharp
Task<SnapBootstrapper.Result> RunAsync(
    IChainStoreBundle bundle, IPeerPool? pool, IFetchRequestScheduler? scheduler,
    ICanonicalStateRootSource canonicalTip, IChainActivations activations, ILogger logger,
    SnapSyncOrchestratorOptions options = null, CancellationToken ct = default);
```

Optional mode/tuning lives on `SnapSyncOrchestratorOptions`:

| Property | Type | Default |
|---|---|---|
| `UseBackwardSkeleton` | `bool` | `true` |
| `Metrics` | `SnapSyncMetrics?` | `null` |
| `RootRefreshIntervalMs` | `int` | `12_000` |
| `PivotStaleDistanceBlocks` | `ulong` | `SnapSyncOrchestrator.PivotStaleDistanceBlocks` |
| `HeaderSweepOverride` | `(ulong From, ulong To)?` | `null` |
| `BackfillOnly` | `bool` | `false` |
| `RunHistoryBackfill` | `bool` | `true` |
| `Phase1First` | `bool` | `false` |
| `AccountConcurrency` | `int?` | `null` |
| `LargeContractConcurrency` | `int?` | `null` |
| `EnableFlatReconcile` | `bool` | `false` |
| `FinalizeVerify` | `bool` | `true` |
| `HeaderFollow` | `HeaderFollowService?` | `null` |
| `BalHealEnabled` | `bool` | `false` |

**`SnapBootstrapper`** (static) — sequences the cold-start for a single pivot: skip if state is already committed, run the Phase-2 stream, heal on root mismatch, persist the pivot and completion sentinel. It also exposes pure, unit-tested decision helpers:

- `SnapResumeMode DecideResumeMode(SnapPhase savedPhase, bool healTargetValid, bool pivotMoved)`
- `bool ShouldMarkPhase2Entry(bool skipPhase2, bool backfillOnly)`

Cold-starts a single pivot: the required inputs are direct parameters; all optional collaborators, mode selection and tuning live on `SnapRunOptions`:

```csharp
Task<SnapBootstrapper.Result> RunAsync(
    IChainStoreBundle bundle, ISnapPeer peer, BlockHeader pivot, byte[] pivotHash, ILogger logger,
    SnapRunOptions options = null, CancellationToken ct = default);
```

**`SnapSyncClient`** — the Phase-2 leaf-stream fetcher. Pulls account ranges with `AccountConcurrency` (16) partitioned workers, per-account storage, and bytecodes from an `ISnapPeer` into an `ISnapSyncSink`, verifying proofs and comparing the assembled root to target:

```csharp
Task<SyncResult> SyncStateAsync(byte[] targetRoot, CancellationToken ct = default);
Task<SyncResult> SyncStateAsync(byte[] targetRoot, SnapSyncState resumeFrom,
                                Action<SnapSyncState> checkpointSink, CancellationToken ct = default);
// The checkpointing entry the resumable overload delegates to (checkpoints a SnapSyncCheckpoint):
Task<SyncResult> SyncStateWithCheckpointAsync(byte[] targetRoot, SnapSyncState resumeFrom,
                                Action<SnapSyncCheckpoint> checkpointSink, CancellationToken ct = default);
```

A `SnapRootMismatchException` signals that heal is required.

**`TrieHealer`** — Phase 3. BFS-walks the in-storage trie under a target root, re-fetching missing hash-referenced nodes (and missing bytecode), and re-roots on staleness as the pivot advances:

```csharp
Task<HealResult> HealAsync(byte[] targetRoot,
    IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> seedStorageHeal = null,
    IReadOnlyList<byte[]> seedCodeHeal = null,
    ulong pivotBlock = 0, bool wipeSeedsFirst = false, CancellationToken ct = default);
```

**`ParallelBlockBackfiller`** — Phase 1. Runs a concurrent pipeline (header producer/loader, body fetcher, receipt fetcher, persistence drain) over a shared `BlockTaskQueue`:

```csharp
Task<BackfillResult> BackfillAsync(ulong startBlock, ulong endBlock, CancellationToken ct);
Task<BackfillResult> BackfillAsync(ulong startBlock, ulong endBlock, IBodyFillCursor cursor, CancellationToken ct);
Task<BackfillResult> BackfillAsync(ulong startBlock, ulong endBlock, bool headersFromStore, CancellationToken ct);
```

**`BlockTaskQueue`** — the producer/consumer work queue behind Phase 1: content-addressed body/receipt matching, a per-peer in-flight reservation gate, and a strict cursor-ordered persist drain. Constructed with `maxInFlightPerPeer` (default 1; the backfiller uses 4).

**`BackwardBlockWalker`** — walks the chain backward from a trusted-tip hash anchor, validating parent-hash chains and persisting a header skeleton (optionally bodies).

Concrete infrastructure impls are also provided: `FetchRequestScheduler` (`IFetchRequestScheduler`), `PeerRequestWorker` (`IPeerRequestWorker`), `PeerPoolManager` (`IPeerPool`), `SchedulerSnapPeer` / `Eth68SnapPeer` (`ISnapPeer`), and `TrieSnapSyncSink` / `InMemorySnapSyncSink` (`ISnapSyncSink`).

## Key types

### Core abstractions & resume model

| Type | Role |
|---|---|
| `SnapPhase` (`NotStarted`/`Phase2Running`/`Phase3Running`/`Complete`) | Persisted phase in `SnapSyncState`; drives resume. *Defined in Nethereum.CoreChain.* |
| `SnapResumeMode` (`Fresh`/`Phase2`/`Phase3Heal`/`ClearOrphan`) | Resume routing from a saved state. |
| `SnapSyncState` | Resumable checkpoint blob (phase, pivot, heal target, task cursors, counters). *Defined in Nethereum.CoreChain.* |
| `IFetchRequestScheduler` | Peer-agnostic request scheduler (headers/bodies/receipts/account-range/storage/bytecode/trie-nodes). |
| `IPeerPool` | Active peer set with add/remove events and a target size. |
| `ISnapSyncSink` | Streaming sink the Phase-2 client writes accounts/storage/bytecode into. |
| `ICanonicalStateRootSource` | Trusted-tip source that anchors the pivot. *Defined in Nethereum.CoreChain.* |

### Peering & serving

| Type | Role |
|---|---|
| `PeerPoolManager` (`IPeerPool`) | The concrete peer pool: score-ordered candidate prioritisation, ban tracking, dial-out and the useless-peer floor. |
| `PeerPoolOptions` | Peer-pool tuning record (`TargetPeerCount` 16, dial concurrency/cooldown, subnet-diversity caps, trusted-redial interval). |
| `SyncPeerSession` (`IEthPeer`) | Live per-peer session for any chain: dial → RLPx + Hello → eth handshake (Status with EIP-2124 fork-ID) → header/body/receipt requests over the negotiated `eth/68`–`eth/71`. |
| `PeerListener` / `PeerListenerOptions` | Inbound RLPx serving composition: binds a port, drives the eth server handshake and serves `eth` + `snap/1` to peers syncing *from* this node. |
| `RelayMempool` | Good-citizen relay tx pool: admits locally-submitted signed txs (via `MempoolAdmissionValidator`), holds them in the `ITxPool`, and announces hashes to peers. Leaf-only by default (`MempoolRelay.Ours`) — it does not re-gossip remote txs unless `MempoolRelay.All` opts in. |
| `MempoolAdmission` / `MempoolRejectReason` | Admission decision and its reject reasons for a submitted transaction. |
| `Eth68PeerPool` / `Eth68PeerSession` | Broadcast peer registry for block/tx publishing (distinct from `IPeerPool`; used by `DevP2PBlockPublisher`). |
| `Eip2124ForkIdCalculator` / `Eip2124ValidationResult` | EIP-2124 fork-ID compute (CRC32 over the fork schedule) and peer-compatibility validation (mirrors geth's stale/incompatible outcomes). *Defined in Nethereum.EVM.Core.* |

### Fetch pipeline (Phase 1)

| Type | Role |
|---|---|
| `FetchRequestScheduler` (`IFetchRequestScheduler`) | Concrete scheduler that fans and retries fetch requests across the peer pool, with per-peer in-flight caps and body/receipt fan-out chunking. |
| `PeerRequestWorker` (`IPeerRequestWorker`) | Executes a single fetch against a single peer; the scheduler's per-attempt unit. |
| `BackwardBlockWalker` (`IBackwardBlockWalker`) | Lays a header skeleton backward from a trusted-tip hash, validating parent-hash chains (optionally bodies). |
| `TipBandBodyFollowService` | Standing catch-up that keeps `[execution-head+1 .. trusted-tip]` supplied with bodies + receipts so the executor never starves. |
| `ReceiptBackfillService` | Background scrub that fills missing receipts behind the executor head as blocks become eligible. |
| `DevP2PBlockSource` (`IBlockSource`) | Multi-peer block source (pool + scheduler) a follower pulls from, reporting chain breaks for auto-rewind. |

### Snap state (Phase 2/3)

| Type | Role |
|---|---|
| `SnapRunOptions` / `SnapSyncOrchestratorOptions` | Optional collaborators, mode selection and tuning for `SnapBootstrapper.RunAsync` / `SnapSyncOrchestrator.RunAsync`. |
| `SchedulerSnapPeer` / `Eth68SnapPeer` (`ISnapPeer`) | Snap request transports — scheduler-backed and direct-eth/68-session-backed. |
| `TrieSnapSyncSink` / `InMemorySnapSyncSink` (`ISnapSyncSink`) | Phase-2 sinks: durable Patricia-trie + bytecode writer, and an in-memory sink for tests. |
| `SnapSyncMetrics` | Phase 2/3 progress counters (accounts/slots/bytecodes, rates). |

## Snap/2 bootstrap (`SnapBootstrapper.Snap2.cs`)

`SnapBootstrapper.RunAsync` picks the mode once per run: when `BalHealEnabled` is set, the run is not `BackfillOnly`, and the boot pivot resolves to Amsterdam or later (`ShouldBalHeal`), it runs `RunSnap2Async`; otherwise it runs the snap/1 path (trie + flat download, heal, reconcile). snap/2 follows go-ethereum's `syncv2`:

- **Preconditions.** A scheduler, a peer pool, a bundle implementing `IFlatStateTrieGenerator`, and `ExternalHeaderFollow`; any missing one throws `InvalidOperationException` before any state is touched.
- **Resume or reset.** `RouteSnap2ResumeAsync` resumes a saved `Generating` state whose pivot is still canonical, or a saved `Phase2Running` state whose pivot is canonical, Amsterdam, and has tasks. Every other saved state (another phase, a non-canonical or pre-Amsterdam pivot, no tasks, or none) is wiped with `ResetSnapBootstrapStateAsync` and the run starts fresh; the log line is `snap.bootstrap.snap2_reset reason=<phase|not_canonical|pre_amsterdam|no_tasks>`.
- **Flat-only download.** Phase 2 writes through `FlatSnapSyncSink`: account and storage pages go to flat state only (each page is still range-proof verified), and large storage accounts are fetched as cursored subtasks. No trie node is written before generation.
- **BAL catch-up.** `BalCatchUp` owns the pivot the flat state is at (`Applied`), and checkpoints are stamped with it. At start, and on every mid-flight pivot move (after draining the attempt, discarding parked range advances and checkpointing), it reads headers from the local canonical chain in windows of 512, fetches and verifies each block's BAL, applies it inside the downloaded frontier, and persists the pivot after every block. A target not ahead of `Applied`, an `Applied` no longer canonical, or a gap above 90,000 blocks throws `SnapSyncResetRequiredException`; the Phase-2 handler then abandons the attempt, cancels the backfill and wipes the snap state.
- **Generation.** After Phase 2 the storage completeness gate runs, `SnapPhase.Generating` is persisted at `Applied`, and `IFlatStateTrieGenerator.GenerateTrieFromFlatAsync` builds the state trie from flat state and checks it against the pivot's state root. A mismatch throws and leaves the phase at `Generating`, so the next attempt regenerates against the same pivot. On success the bytecode backstop runs and the pivot is committed as for snap/1.
- **Not enabled on mainnet.** `BalHealEnabled` defaults to `false`, and no MainnetChain configuration sets it.

## Snap-v2 BAL heal (`Snap/CatchUp`)

When the pivot moves while Phase 2 is still in flight, the account/storage ranges already streamed can fall behind the blocks produced since the old pivot. The `Snap/CatchUp` types close that gap by fetching each intervening block's **Block Access List** (`snap/2`'s per-block account/storage/code/nonce/balance change set) from a peer, verifying it against the block header's BAL-hash commitment, and applying only the changes that land inside the frontier of state already fetched. This path is gated by `SnapSyncOrchestratorOptions.BalHealEnabled` (`SnapSyncOrchestratorOptions.cs:39`, default `false`).

| Type | Role |
|---|---|
| `IBlockAccessListFetcher` / `BlockAccessListFetcher` | Fetches raw BAL entries for a set of `BalBlockRef`s from serviceable peers, retrying/redistributing across peers on refusal, hash mismatch, or a stateless response; throws `InvalidOperationException` when a block becomes unobtainable from every serviceable peer. `MaxHashesPerRequest` = 28, `ResponseByteBudget` = 2 MiB. |
| `BlockAccessListVerifier` | `Verify(expectedBalHashes, rawEntries)` — Keccak-hashes each raw entry against its expected commitment and decodes it via `BlockAccessListRLPEncoder`, returning one `VerifiedBlockAccessList` per entry. |
| `IBlockAccessListApplier` / `BlockAccessListApplier` | `ApplyAsync(blockAccessList, frontier, ct)` — applies a verified block's `AccountChanges` (balance/nonce/code/storage, last-writer-wins by `BlockAccessIndex`) into flat state (`ISnapFlatStateWriter`, `IStateStore` for code); deletes empty accounts. |
| `ISnapTaskFrontier` / `SnapTaskFrontier` | `IsAccountFetched(accountHash)` / `IsStorageFetched(accountHash, slotHash)` — tells the applier whether a given account/slot already fell inside the Phase-2 range-task frontier, so BAL heal only patches state that was actually fetched. |
| `VerifiedBlockAccessList` / `BlockAccessListStatus` | Verification outcome (`Verified` / `Unavailable` / `HashMismatch`) plus the decoded `IReadOnlyList<AccountChanges>` when `Verified`. |
| `IBlockAccessListPeer` / `IBlockAccessListPeerSource` / `PeerPoolBlockAccessListPeerSource` | Peer abstraction (`Id`, `RequestBlockAccessListsAsync(blockHashes, responseBytes, ct)`) and its live source, which filters the peer pool to sessions where `SyncPeerSession.SupportsSnap2` is true. |
| `BalBlockRef` | `(byte[] BlockHash, byte[] BlockAccessListHash)` — one block's identity for BAL fetch/verify. |

```csharp
// IBlockAccessListFetcher.FetchAsync — Snap/CatchUp/IBlockAccessListFetcher.cs:10
Task<IReadOnlyList<IReadOnlyList<AccountChanges>>> FetchAsync(
    IReadOnlyList<BalBlockRef> blocks, CancellationToken ct);

// IBlockAccessListApplier.ApplyAsync — Snap/CatchUp/IBlockAccessListApplier.cs:10
Task ApplyAsync(IReadOnlyList<AccountChanges> blockAccessList, ISnapTaskFrontier frontier, CancellationToken ct = default);
```

## Usage examples

The first two examples drive a live peer pool (a connected devp2p network); the Phase-1 backfill example runs fully in-memory and is exercised by a unit test.

**Full orchestrated snap sync** (the production path):

```csharp
var activations = new FixedChainActivations(HardforkNames.Parse("prague"));
var result = await SnapSyncOrchestrator.RunAsync(
    bundle, pool, scheduler, canonical, activations, logger,
    new SnapSyncOrchestratorOptions { UseBackwardSkeleton = true }, ct);
// result.Ran / result.SkipReason / result.PivotBlockNumber
```

**Drive `SnapBootstrapper` directly against a scheduler-backed snap peer:**

```csharp
var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());
var snapPeer  = new SchedulerSnapPeer(scheduler);

await bundle.Blocks.SaveAsync(pivotHeader, pivotHash); // trusted pivot header
var result = await SnapBootstrapper.RunAsync(
    bundle, snapPeer, pivotHeader, pivotHash, logger,
    new SnapRunOptions
    {
        Scheduler = scheduler,
        PivotRefresher = null,
        RunBackfill = false,
        Activations = new FixedChainActivations(HardforkNames.Parse("prague")),
        Pool = pool,
    },
    ct);
```

**Phase-1 backfill over a pre-laid header skeleton:**

```csharp
var backfiller = new ParallelBlockBackfiller(scheduler, pool, worker, bundle);
var result = await backfiller.BackfillAsync(startBlock, endBlock, headersFromStore: true, ct);
// result.BlocksWritten; bundle.Metadata.GetLastFetchedBody() advances to endBlock
```

## Design notes

- **Trustless fetch (verify-on-read).** Snap account and storage ranges are proof-verified against their roots before persistence, and the final assembled root is compared to target (mismatch throws `SnapRootMismatchException`). Phase-1 bodies/receipts are content-addressed against the header commitments and parent-hash-validated. A peer cannot corrupt the archive or state.
- **Resumability.** Phase 2 checkpoints a `SnapSyncState` blob every ~8 MB and on graceful shutdown; resume routing (`DecideResumeMode`) honours the saved phase — a kill during heal resumes heal directly even if the pivot moved. Phase 1 resumes from the `LastFetchedHeader` / `LastFetchedBody` metadata cursors.
- **Per-peer in-flight depth.** `BlockTaskQueue` caps concurrent reservations per peer per stage (`maxInFlightPerPeer`). Depth 1 is the historical one-in-flight behaviour; the backfiller uses 4 so a fast peer can pipeline across the round-trip instead of idling. Reservations are release-XOR-deliver so a failed request never double-frees a peer's slot.
- **Rolling pivot.** Phase 2 (`PivotRefresher` + `RootRefreshIntervalMs`) and Phase 3 (`TrieHealer.PivotRefresher` + a staleness gate) follow the advancing head so the target root doesn't age out of peers' snapshot windows.
- **Shared-socket awareness.** Heal deliberately caps its snap responses below the server frame limit because eth and snap multiplex over one RLPx socket; oversized snap frames were starving concurrent Phase-1 backfill.
- **Bytecode is fetched outside the trie root check** (contract code isn't in the state trie), with explicit completeness passes in Phase 2, heal, and on resume.
- **Atomic cursor ordering.** Phase 1 writes block data durably first, then advances the metadata cursor over only the contiguous persisted prefix, so a crash leaves the cursor consistent with on-disk content. The final pivot commit and the `Phase=Complete` sentinel land in one batch.
- **Bogus-converge guard.** The bootstrapper refuses to declare success if the leaf stream wrote zero accounts and heal matched after fetching implausibly few nodes (a stale-subtree false positive).
