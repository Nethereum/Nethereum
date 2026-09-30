---
name: devp2p-full-sync
description: Help users backfill the Ethereum block archive (headers, bodies, receipts) over devp2p with Nethereum (.NET) — ParallelBlockBackfiller, BackwardBlockWalker header skeletons, IBlockSource streaming, and reorg handling. Use this skill whenever the user mentions full sync, block backfill, downloading blocks/headers/bodies/receipts over devp2p, Phase 1 sync, ParallelBlockBackfiller, TipBandBodyFollowService, or handling a chain reorg while syncing.
user-invocable: true
---

# Full Sync (Block Archive Backfill) — Nethereum.DevP2P.Sync

Once your node can connect to peers (`devp2p-peer-connect`) and is wired up as a `SyncNode` (`devp2p-run-a-node`), it needs the actual chain data: headers, bodies, and receipts for every block. This is **Phase 1** of Nethereum's snap-first sync — downloading and verifying the block archive from `[genesis..pivot]` — and it's useful on its own even outside a full snap-sync run, for backfilling history behind an already snap-synced head.

## Package

```bash
dotnet add package Nethereum.DevP2P.Sync
```

You need an `IChainStoreBundle` to persist into, an `IPeerPool` with at least one connected peer, and an `IFetchRequestScheduler` to fan requests across that pool.

## Mental model: content-addressed, pipelined, resumable

Phase 1 downloads three things per block — headers, bodies (transactions + uncles), and receipts — and verifies every one of them cryptographically before it's trusted:

- Bodies are checked against the header's `TransactionsHash` and `UnclesHash`.
- Receipts are checked against the header's `ReceiptHash`.
- The parent-hash chain is validated within and across fetch batches.

A peer that returns bad data doesn't corrupt your archive — a mismatch is detected and the request is retried against another peer. This is the same trustless-fetch principle behind Phase 2 in snap sync (`devp2p-snap-sync`): never persist what you can't verify.

## Run a bounded backfill

`ParallelBlockBackfiller` runs a concurrent pipeline — header producer/loader, body fetcher, receipt fetcher, and a persistence drain — over a shared `BlockTaskQueue`:

```csharp
Task<BackfillResult> BackfillAsync(ulong startBlock, ulong endBlock, CancellationToken ct);
Task<BackfillResult> BackfillAsync(ulong startBlock, ulong endBlock, IBodyFillCursor cursor, CancellationToken ct);
Task<BackfillResult> BackfillAsync(ulong startBlock, ulong endBlock, bool headersFromStore, CancellationToken ct);
```

The `headersFromStore: true` overload is the one you reach for when headers are already persisted (laid down by a header skeleton, or by a previous partial run) and only bodies/receipts are missing:

```csharp
var pool = new OnePeerPool();                 // your IPeerPool with a connected peer
var worker = new ServingWorker(chain);        // your IPeerRequestWorker implementation
var backfiller = new ParallelBlockBackfiller(scheduler, pool, worker, bundle);

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var result = await backfiller.BackfillAsync(3, 7, headersFromStore: true, cts.Token);

// result.Ran == true
// result.BlocksWritten == 5, result.TransactionsWritten == 5, result.ReceiptsWritten == 5
// bundle.Metadata.GetLastFetchedBody() advances to 7
```

`BlocksWritten`/`TransactionsWritten`/`ReceiptsWritten` tell you exactly what landed; `bundle.Metadata.GetLastFetchedBody()` is the durable cursor Phase 1 advances only over the *contiguous* persisted prefix — so a crash mid-fill leaves the cursor pointing at real, complete data, never past a gap.

## Where the header skeleton comes from

The `headersFromStore: true` path assumes headers are already in the store. `BackwardBlockWalker` is what lays that skeleton down: it walks the chain backward from a trusted-tip hash anchor, validating the parent-hash chain as it goes, and optionally persisting bodies too. `BlockTaskQueue` — the producer/consumer queue underneath the backfiller — caps concurrent in-flight requests per peer per stage via `maxInFlightPerPeer` (default 1; the backfiller itself uses 4, so a fast peer can pipeline across the round trip instead of idling waiting for one response before sending the next request).

## Keeping the tip band fed independently

Phase 1's archive backfill and the "keep the live tip fresh" job are deliberately separate concerns: `TipBandBodyFollowService` fills `[execution-head+1 .. trusted-tip]` with bodies and receipts so the block executor never starves waiting on the *history* drain to catch up. It's driven the same way as a bounded backfill, but against an `IBodyFillCursor` anchored at the execution head rather than a fixed `[start, end]` range:

```csharp
var service = new TipBandBodyFollowService(scheduler, pool, bundle, activations);
var cursor = new ExecutionHeadBodyFillCursor(bundle);

await service.FillOnceAsync(backfiller, cursor, ct);
// cursor advances to the trusted tip independently of bundle.Metadata's archive body cursor
```

The archive cursor (`bundle.Metadata.GetLastFetchedBody()`) does **not** move when you drive the tip band this way — the two cursors track different things on purpose, so a slow archive backfill can never stall block execution at the tip.

## Consuming blocks as a stream: `IBlockSource`

A follower doesn't call the backfiller directly in most cases — it consumes an `IBlockSource`, a chain-agnostic abstraction (defined in `Nethereum.CoreChain`) that streams blocks and reports when the source diverges from what was previously seen (a reorg or a bad peer). Two DevP2P-backed implementations exist:

- **`DevP2PBlockSource`** — a multi-peer source (built on the pool + scheduler) that a follower pulls from, reporting chain breaks so the caller can auto-rewind.
- **`PushedBlockSource`** — accepts blocks pushed to it (e.g. from a broadcast peer bridge) rather than pulling.

> **Note:** these two types are named in the README's type table (role description, no method signature block); no tagged doc-example test covers constructing or streaming from either. Check `src/Nethereum.CoreChain/Sync/IBlockSource.cs` directly for the current interface shape before relying on it as stable.

## What happens on a reorg

Phase 1 validates the parent-hash chain as it fetches, so a peer serving a block whose parent doesn't match what was already fetched is detected immediately rather than silently corrupting the archive. At the streaming layer, `DevP2PBlockSource` reports this as a chain break so the follower can auto-rewind to the last common ancestor and re-fetch forward — the same "verify before persist, never trust a single peer" discipline Phase 1 applies to bodies and receipts applies here to chain continuity itself.

## Common mistakes

| Symptom | Cause | Fix |
|---|---|---|
| `BackfillAsync(headersFromStore: true)` waits forever without progress | The header skeleton hasn't reached the requested `startBlock` yet — the filler correctly waits rather than falsely declaring failure | Confirm `BackwardBlockWalker`'s descent has reached `startBlock`, or increase how long you wait |
| `bundle.Metadata.GetLastFetchedBody()` doesn't move even though blocks are being written | You're driving `TipBandBodyFollowService`, whose cursor is intentionally separate from the archive cursor | This is expected — check the tip-band cursor (`ExecutionHeadBodyFillCursor`), not the archive one |
| Backfill throws on a mismatched body/receipt | A peer returned data that doesn't match the header's commitment — this is the trustless-fetch check working as intended | The scheduler retries against another peer; if it persists, suspect a malicious or badly-synced peer |
| Archive cursor jumps unexpectedly on restart after a crash | It shouldn't — Phase 1 only advances the cursor over the *contiguous* persisted prefix | If you see this, it's worth filing — this is a documented invariant, not expected behavior |

## Decision guidance

| I want to… | Use |
|---|---|
| Fill a bounded, known range of blocks | `ParallelBlockBackfiller.BackfillAsync(start, end, ct)` |
| Fill bodies/receipts over headers already in the store | `BackfillAsync(start, end, headersFromStore: true, ct)` |
| Keep the live tip fed without waiting on history | `TipBandBodyFollowService.FillOnceAsync` |
| Consume blocks as a stream with reorg handling | `IBlockSource` (`DevP2PBlockSource` pulling, or `PushedBlockSource` for a push model) |
| Lay the header skeleton itself, backward from a trusted tip | `BackwardBlockWalker` |

For full documentation, see: https://docs.nethereum.com/docs/devp2p/guide-full-sync
