---
name: devp2p-run-a-node
description: Help users run a full Ethereum devp2p node with Nethereum (.NET) — the SyncNode facade that ties peering, serving eth/snap to other peers, syncing, and mempool relay together. Use this skill whenever the user mentions running an Ethereum node, SyncNode, serving eth or snap to peers, PeerListener, relaying a locally-submitted transaction, RelayMempool, SubmitAsync, or wants one entry point that combines devp2p connect/discover/sync/mempool.
user-invocable: true
---

# Run a Node — SyncNode facade (Nethereum.DevP2P.Sync)

Connecting to a peer and discovering peers (see `devp2p-peer-connect` and `devp2p-peer-discovery`) cover the transport primitives one at a time — dialing, listening, discovering. A real node needs all of them working together: a peer pool, a fetch scheduler, an inbound listener serving `eth`/`snap` to other nodes, and (optionally) a mempool relay — plus the sync engine (`devp2p-full-sync`, `devp2p-snap-sync`). `SyncNode` is the facade that ties this into one object, in the same spirit as `Web3` for the JSON-RPC side of Nethereum: construct it once from your chain's storage and activation config, then reach every sync capability from it.

## Package

```bash
dotnet add package Nethereum.DevP2P.Sync
```

You'll need an `IChainStoreBundle` (block/state storage) and `IChainActivations` (fork schedule) from `Nethereum.CoreChain` — these come from whichever chain you're running (mainnet follower or an AppChain), not from this package. You'll also typically have already built the peering resources: an `IPeerPool`, an `IFetchRequestScheduler`, and optionally a `PeerListener` for serving.

## Mental model: one facade, four capabilities

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

Each of the four properties is `null` when you didn't configure that capability — a node that only backfills the archive (no inbound serving) simply won't set `serving`, and `node.Serving` stays `null`. This lets one class model "full node," "archive-only follower," and "serving-only" without three different types. The underlying drivers (`SnapSyncOrchestrator`, `SnapBootstrapper`, the phase workers) stay directly usable for advanced control — `SyncNode` is a front door, not a wall.

## Start serving eth and snap to other peers

If you built a `PeerListener` (the inbound serving composition — binds a port, drives the `eth` server handshake, and serves `eth` plus `snap/1` to peers syncing *from* you) and passed it into the constructor's `serving` parameter, `StartServingAsync` binds it:

```csharp
var node = new SyncNode(bundle, activations, logger, peers: pool, scheduler: scheduler, serving: peerListener);

await node.StartServingAsync(ct);   // no-op if you didn't pass a `serving` PeerListener
```

This is what makes your node useful to *other* nodes doing full sync or snap sync against you — without it, your node can dial out and sync but nobody can sync from it. `RlpxListener.PeerAccepted` alone (see `devp2p-peer-connect`) does not run the `eth` Status exchange; `PeerListener` fills that gap.

## Relay locally-submitted transactions

`RelayMempool` is the "good citizen" side of mempool participation: it admits transactions *your own node* (or a locally-connected wallet/RPC client) submits — after running them through `MempoolAdmissionValidator` — holds them in an `ITxPool`, and announces their hashes to your connected peers. It is deliberately **leaf-only**: it never re-gossips transactions it received from *other* peers, so a `SyncNode` participates in propagation without becoming a full relay hub.

```csharp
var node = new SyncNode(bundle, activations, logger, peers: pool, scheduler: scheduler, mempool: relayMempool);

var result = await node.Mempool.SubmitAsync(signedTx);   // MempoolSubmitResult tells you the outcome
if (!result.Accepted)
{
    // result.RejectReason (MempoolRejectReason) + result.RejectMessage explain why
}
```

`SubmitAsync(ISignedTransaction, CancellationToken) : Task<MempoolSubmitResult>` — the real signature, from `Mempool/RelayMempool.cs`. Do not use an `Admit`-named method; it does not exist. `MempoolSubmitResult` (`Mempool/MempoolSubmitResult.cs`) exposes `Accepted`, `TransactionHash`, `Sender`, `RejectReason`, `RejectMessage`. Check `result.Accepted` (and `RejectReason`/`RejectMessage` on failure) before assuming a submitted transaction actually made it into the pool — a full pool, a stale nonce, or a validator rejection all fail silently from the caller's perspective unless you inspect the result. (`AdmitFromTrustedPeerAsync` is the separate inbound path used when relaying transactions received from peers, not the path for a locally-submitted transaction.)

## Cold-start state with the same facade

`SyncNode.RunSnapBootstrapAsync` is the sync-side capability, delegating straight to `SnapSyncOrchestrator.RunAsync`:

```csharp
var result = await node.RunSnapBootstrapAsync(canonicalTip, options, ct);
// result.Ran / result.SkipReason / result.PivotBlockNumber
```

See `devp2p-snap-sync` for the full Phase 2/3 flow this delegates to — the point here is that it's reached from the same `node` object as serving and mempool relay, not a separately-constructed orchestrator.

## Common mistakes

| Symptom | Cause | Fix |
|---|---|---|
| `node.Serving` is `null` at runtime | No `serving: peerListener` argument was passed to the constructor | `StartServingAsync` is a safe no-op in this case, but check whether you *meant* to serve |
| Peers connect but the node never answers `eth`/`snap` requests | Used bare `RlpxListener` instead of `PeerListener` | `RlpxListener` only completes the transport handshake; `PeerListener` runs the actual protocol dispatch |
| Submitted transaction never reaches peers | `RelayMempool.SubmitAsync` rejected it, or `node.Mempool` is `null` because no `mempool:` argument was passed | Check the returned `MempoolSubmitResult` (`Accepted`, `RejectReason`/`RejectMessage`); confirm the constructor argument |
| A remote peer connects but is dropped right after Status | Fork-ID mismatch (EIP-2124) | Confirm both nodes share `GenesisHash`/`ForkBlockNumbers`/`ForkTimestamps` in their `DevP2PConfig` |

## Decision guidance

| I want to… | Use |
|---|---|
| The single object that gives me peering + serving + sync + mempool | `SyncNode` |
| Fine-grained control over pivot selection, retry/backoff | `SnapSyncOrchestrator` directly (advanced — see `devp2p-snap-sync`) |
| Only Phase 1 (block archive), no state sync | Drive `ParallelBlockBackfiller` directly (see `devp2p-full-sync`) |
| Only serve, never sync out | Construct `SyncNode` with `serving:` set and skip `RunSnapBootstrapAsync` |

For full documentation, see: https://docs.nethereum.com/docs/devp2p/guide-run-a-node
