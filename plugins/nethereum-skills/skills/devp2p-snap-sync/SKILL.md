---
name: devp2p-snap-sync
description: Help users snap sync an Ethereum node — cold start state without replaying every block from genesis, using Nethereum (.NET)'s SnapSyncOrchestrator/SnapBootstrapper, SnapSyncClient state streaming, TrieHealer trie healing, and checkpoint/resume. Use this skill whenever the user mentions snap sync, sync state without an archive, cold start a node, state download, SnapSyncOrchestrator, SnapBootstrapper, TrieHealer, trie healing, SnapRootMismatchException, resuming an interrupted sync, or a moving/rolling sync pivot.
user-invocable: true
---

# Snap Sync (State Download) — Nethereum.DevP2P.Sync

Full sync (`devp2p-full-sync`) covers downloading the block *archive* — headers, bodies, receipts. That tells you what happened, but not the current *state* (every account's balance, nonce, code, and storage). Replaying every block from genesis to compute that state is exactly what snap sync avoids: it streams the state at a recent pivot block directly, verified against Merkle-Patricia proofs, then heals any gaps. This skill covers cold-starting that state stream (Phase 2), healing the reconstructed trie (Phase 3), and resuming a sync that was interrupted partway through.

## Package

```bash
dotnet add package Nethereum.DevP2P.Sync
```

You need a peer pool with peers advertising `snap/1` (see `devp2p-peer-connect` for capability negotiation), an `IChainStoreBundle`, `IChainActivations` for the target chain, and an `ICanonicalStateRootSource` — a trusted-tip source that anchors the pivot block.

## Mental model: three phases, one orchestrator

```
SyncNode                             the "start here" facade — one object: serve state + snap-bootstrap
   └─ SnapSyncOrchestrator           anchor pivot, retry/backoff, follow rolling pivot
        └─ SnapBootstrapper          sequence one pivot: skip-if-synced -> Phase 2 -> heal-on-mismatch -> commit
             ├─ ParallelBlockBackfiller   Phase 1: header + body + receipt + persist stages over a BlockTaskQueue
             │     └─ BackwardBlockWalker  (skeleton mode) lay headers backward from a trusted tip
             ├─ SnapSyncClient            Phase 2: parallel account/storage/bytecode leaf stream -> ISnapSyncSink
             └─ TrieHealer                Phase 3: BFS re-fetch of missing trie nodes until root matches
```

Phase 1 (`devp2p-full-sync`) can run inside this same orchestration or standalone. Phase 2 streams the full account/storage/bytecode state at the pivot, verifying every range against a proof and comparing the assembled root to the pivot's `StateRoot`. Phase 3 walks the resulting trie and re-fetches anything still missing, following the pivot forward if the head advances while healing runs — which is exactly the scenario `devp2p-bal-sync` covers in more depth for the snap-v2 case.

## Run the full orchestrated sync (the production path)

`SnapSyncOrchestrator.RunAsync` is the top-level driver — anchors the pivot from your canonical tip source, runs the bootstrapper, and follows a rolling pivot with backoff if the chain keeps advancing while you sync:

```csharp
var activations = new FixedChainActivations(HardforkNames.Parse("prague"));
var result = await SnapSyncOrchestrator.RunAsync(
    bundle, pool, scheduler, canonical, activations, logger,
    new SnapSyncOrchestratorOptions { UseBackwardSkeleton = true }, ct);
// result.Ran / result.SkipReason / result.PivotBlockNumber
```

This is what `SyncNode.RunSnapBootstrapAsync` (`devp2p-run-a-node`) delegates to — reach for the orchestrator directly only when you need tuning it doesn't expose through the facade, or you're not using `SyncNode` at all.

## Tuning the orchestration

`SnapSyncOrchestratorOptions` is where mode and tuning knobs live. The defaults are chosen for a fresh cold-start against a live network; most callers only override one or two:

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

`RootRefreshIntervalMs` is how often the orchestrator re-checks whether the pivot has moved; `BackfillOnly` lets you run just Phase 1 through this same entry point when you don't want state sync at all. `BalHealEnabled` gates the snap-v2 BAL heal path — see `devp2p-bal-sync` for what it does and why it defaults to `false`.

## Drive `SnapBootstrapper` directly

For advanced control over a single pivot — skipping the orchestrator's rolling-pivot/backoff loop — `SnapBootstrapper.RunAsync` sequences one cold-start: skip if state is already committed, run Phase 2, heal on root mismatch, then persist the pivot and completion sentinel:

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

The pivot header is saved to the store *before* calling `RunAsync` — the bootstrapper trusts the header you hand it as the sync target; it doesn't independently fetch and validate the pivot itself (that trust decision belongs to whatever supplies your `ICanonicalStateRootSource` in the orchestrated path above). `SnapBootstrapper` also exposes pure, unit-tested decision helpers if you need to reason about resume routing yourself: `DecideResumeMode(SnapPhase savedPhase, bool healTargetValid, bool pivotMoved)` and `ShouldMarkPhase2Entry(bool skipPhase2, bool backfillOnly)`.

## Phase 2: the leaf-stream fetcher

`SnapSyncClient` is what actually pulls state during Phase 2 — account ranges via 16 (`AccountConcurrency`) partitioned parallel workers, per-account storage, and bytecodes, verifying proofs and comparing the assembled root against target as it goes:

```csharp
Task<SyncResult> SyncStateAsync(byte[] targetRoot, CancellationToken ct = default);
```

If the assembled root doesn't match `targetRoot` when the stream completes, `SyncStateAsync` doesn't fail silently — it signals via a `SnapRootMismatchException`, which is exactly the trigger `SnapBootstrapper` uses to hand off to Phase 3 healing.

## Resume from a checkpoint

A cold-start over a large state trie can run for hours — you need to survive a restart partway through without re-streaming everything:

```csharp
Task<SyncResult> SyncStateAsync(byte[] targetRoot, SnapSyncState resumeFrom,
                                Action<SnapSyncState> checkpointSink, CancellationToken ct = default);
// The checkpointing entry the resumable overload delegates to (checkpoints a SnapSyncCheckpoint):
Task<SyncResult> SyncStateWithCheckpointAsync(byte[] targetRoot, SnapSyncState resumeFrom,
                                Action<SnapSyncCheckpoint> checkpointSink, CancellationToken ct = default);
```

Phase 2 checkpoints a `SnapSyncState` blob roughly every 8 MB and on graceful shutdown. On the next run, `SnapBootstrapper.DecideResumeMode` reads the saved `SnapPhase` (`NotStarted`/`Phase2Running`/`Phase3Running`/`Complete`) and routes accordingly — critically, **a kill during heal resumes heal directly even if the pivot moved in the meantime**, rather than restarting Phase 2 from scratch. Phase 1's resume is simpler: it just picks up from the `LastFetchedHeader`/`LastFetchedBody` metadata cursors (`devp2p-full-sync`).

> **Note:** the checkpoint/resume signatures above are verified against the README's accepted signature block; no tagged doc-example test exercises a full checkpoint-then-resume cycle end to end.

## Phase 3: healing

`TrieHealer` BFS-walks the in-storage trie under a target root, re-fetching any hash-referenced node (or bytecode) still missing, and re-roots as the pivot advances:

```csharp
Task<HealResult> HealAsync(byte[] targetRoot,
    IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> seedStorageHeal = null,
    IReadOnlyList<byte[]> seedCodeHeal = null,
    ulong pivotBlock = 0, bool wipeSeedsFirst = false, CancellationToken ct = default);
```

Heal deliberately caps its snap response sizes below the server's frame limit, because `eth` and `snap` share one RLPx socket per peer (`devp2p-peer-connect`) — an oversized snap response would starve a concurrent Phase-1 backfill sharing the same connection. The bootstrapper also refuses to declare heal successful if it converged after fetching implausibly few nodes from an all-zero leaf stream — a guard against a stale-subtree false positive rather than a real completion.

## Common mistakes

| Symptom | Cause | Fix |
|---|---|---|
| `SyncStateAsync` throws `SnapRootMismatchException` | Expected — this is Phase 2 signaling that heal is required, not necessarily a bug | `SnapBootstrapper` catches this and hands off to `TrieHealer` automatically in the orchestrated path |
| Sync appears to restart from zero after a restart | Not using `SyncStateWithCheckpointAsync`/`resumeFrom`, or resume routing decided `Fresh` because the saved phase was invalid | Confirm you're persisting `SnapSyncState` via the checkpoint sink and passing it back in on restart |
| Heal never converges while the chain keeps producing blocks | Pivot moving faster than heal can catch up | Widen `PivotStaleDistanceBlocks`, or see `devp2p-bal-sync` for the dedicated gap-closing mechanism |
| Snap responses seem throttled compared to eth traffic | Intentional — heal caps snap frame sizes to protect concurrent Phase-1 traffic on the same socket | Not a bug; tune peer count/concurrency instead of frame size |

## Decision guidance

| I want to… | Use |
|---|---|
| The production cold-start path with rolling-pivot follow | `SnapSyncOrchestrator.RunAsync` (or `SyncNode.RunSnapBootstrapAsync`) |
| One pivot, advanced control, no rolling-pivot loop | `SnapBootstrapper.RunAsync` directly |
| Just the leaf-stream fetch, custom sink | `SnapSyncClient.SyncStateAsync` |
| Resume across restarts | `SnapSyncClient.SyncStateWithCheckpointAsync` + persisted `SnapSyncState` |
| Re-fetch missing trie nodes after a root mismatch | `TrieHealer.HealAsync` |

For full documentation, see: https://docs.nethereum.com/docs/devp2p/guide-snap-sync
