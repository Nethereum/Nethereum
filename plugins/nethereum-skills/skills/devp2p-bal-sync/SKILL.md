---
name: devp2p-bal-sync
description: Help users close the state gap when a snap sync pivot moves mid-flight, using snap/2 Block Access Lists (BAL) with Nethereum (.NET) — instead of re-streaming already-fetched ranges. Use this skill whenever the user mentions block access list, BAL heal, snap/2, snap-v2, heal while the pivot moves, BalHealEnabled, BlockAccessListFetcher/Verifier/Applier, or patching state deltas during snap sync on Amsterdam or later.
user-invocable: true
---

# Block Access Lists (Snap-v2 BAL Heal) — Nethereum.DevP2P.Sync

Snap sync (`devp2p-snap-sync`) follows a rolling pivot as the chain advances, and Phase 3 heal re-fetches missing trie nodes when the root doesn't match. But there's a gap those mechanisms don't close by themselves: while Phase 2/3 is still streaming or healing, the *live chain keeps producing blocks* — and the account/storage ranges you already fetched at the old pivot are now stale relative to the new one. Re-streaming those ranges from scratch would waste everything already downloaded. Snap-v2's **Block Access List (BAL)** — a per-block account/storage/code/nonce/balance change set — lets you patch just the deltas instead. This is an advanced, opt-in path: everything here sits behind `SnapSyncOrchestratorOptions.BalHealEnabled`, which defaults to `false`.

## Package

```bash
dotnet add package Nethereum.DevP2P.Sync
```

This skill assumes you've already read `devp2p-snap-sync` — BAL heal is a supplement to Phase 2/3, not a replacement. You also need peers advertising `snap/2` — `devp2p-peer-connect` covers `DevP2PConfig.AdvertiseSnap2`, the flag that puts `snap/2` in your own `Hello`.

## Mental model: patch the frontier, don't re-stream it

Everything Phase 2 has already fetched has a **frontier** — the boundary of account/storage ranges already covered. When the pivot moves mid-flight, `Snap/CatchUp`'s job is narrow and specific:

1. **Fetch** each intervening block's BAL from a peer.
2. **Verify** it against the block header's BAL-hash commitment (never trust an unverified change set).
3. **Apply** only the changes that land inside the already-fetched frontier — anything outside it will be picked up naturally when Phase 2/3 reaches that range anyway, so applying it early would be wasted (or worse, inconsistent) work.

This is the same trustless-fetch discipline as the rest of the sync engine (`devp2p-full-sync`, `devp2p-snap-sync`): nothing gets applied to state without being checked against a cryptographic commitment first.

## Fetch: pulling raw BAL entries

`IBlockAccessListFetcher` (concrete: `BlockAccessListFetcher`) fetches raw BAL entries for a set of block references from serviceable peers, retrying and redistributing across peers on refusal, hash mismatch, or a stateless response:

```csharp
// IBlockAccessListFetcher.FetchAsync — Snap/CatchUp/IBlockAccessListFetcher.cs:10
Task<IReadOnlyList<IReadOnlyList<AccountChanges>>> FetchAsync(
    IReadOnlyList<BalBlockRef> blocks, CancellationToken ct);
```

Each `BalBlockRef` is `(byte[] BlockHash, byte[] BlockAccessListHash)` — the block's identity plus the commitment the fetched entry must hash-match. The fetcher batches at most `MaxHashesPerRequest` (28) blocks per request within a `ResponseByteBudget` of 2 MiB, and throws `InvalidOperationException` if a block becomes unobtainable from *every* serviceable peer — there's no silent partial result to accidentally apply.

## Verify: checking the commitment before trusting anything

`BlockAccessListVerifier.Verify(expectedBalHashes, rawEntries)` Keccak-hashes each raw entry against its expected commitment and decodes it via `BlockAccessListRLPEncoder`, returning one `VerifiedBlockAccessList` per entry:

- `VerifiedBlockAccessList.Status` (enum `BlockAccessListStatus`) is one of `Verified` / `Unavailable` / `HashMismatch`.
- Only on `Verified` does the result carry a decoded `IReadOnlyList<AccountChanges>` — an `Unavailable` or `HashMismatch` result carries nothing to apply.

This is the checkpoint that keeps a malicious or buggy peer from injecting fabricated balance/storage changes: a mismatched hash is discarded, not applied.

## Apply: patching flat state and the trie

`IBlockAccessListApplier` (concrete: `BlockAccessListApplier`) is where verified changes actually land in storage:

```csharp
// IBlockAccessListApplier.ApplyAsync — Snap/CatchUp/IBlockAccessListApplier.cs:10
Task ApplyAsync(IReadOnlyList<AccountChanges> blockAccessList, ISnapTaskFrontier frontier, CancellationToken ct = default);
```

`ApplyAsync` applies a verified block's `AccountChanges` — balance, nonce, code, and storage — into flat state (`ISnapFlatStateWriter`, plus `IStateStore` for code), and, when supplied, into the in-progress trie via `IHealedStateWriter`. Empty accounts are deleted, matching Ethereum's account-clearing rule. Within one account's changes, conflicting writes at different points in the block resolve **last-writer-wins by `BlockAccessIndex`** — not by list order:

> Given balance changes at indices `3` (→ 500) and `0` (→ 100) submitted in that order, the applied balance is 500 — the change with the *higher* `BlockAccessIndex` wins, regardless of which one appears first in the list.

Confirmed in `tests/Nethereum.DevP2P.Sync.UnitTests/CatchUp/BlockAccessListApplierTests.cs:24` (`Given_BalanceAndNonceChanges_When_Applied_Then_PostValuesAreLastWriteByIndexNotByListOrder`).

## The frontier: only patch what you already fetched

`ISnapTaskFrontier` (concrete: `SnapTaskFrontier`) is what makes "only patch state already fetched" possible — it answers two questions the applier asks before writing anything:

- `IsAccountFetched(accountHash)`
- `IsStorageFetched(accountHash, slotHash)`

If an account or slot falls *outside* the Phase-2 range-task frontier, the applier skips it — that range hasn't been streamed yet, so applying a delta to it now would create state that's inconsistent with everything around it. It'll be picked up correctly once Phase 2 actually reaches that range.

## Where BAL peers come from

`IBlockAccessListPeerSource` (concrete: `PeerPoolBlockAccessListPeerSource`) filters your existing peer pool down to sessions where `SyncPeerSession.SupportsSnap2` is `true` — you don't maintain a separate BAL-capable peer list; it's derived from the same pool `devp2p-peer-connect` and `devp2p-peer-discovery` built up.

## Turning it on

BAL heal is opt-in for a reason — it's additional machinery most syncs don't need. Enable it via the orchestrator option (`devp2p-snap-sync`):

```csharp
var options = new SnapSyncOrchestratorOptions { BalHealEnabled = true };
```

**Gotcha — the flag alone isn't enough.** Setting `BalHealEnabled = true` has no effect unless the block's active hardfork is Amsterdam or later — `SnapBootstrapper.ShouldBalHeal` gates on *both* the option and the fork schedule. Enabling the flag against a pre-Amsterdam chain silently does nothing; there's no error, because BAL simply isn't part of the wire protocol yet at that fork.

Verified in `tests/Nethereum.DevP2P.Sync.UnitTests/SnapBalHealGapCloserTests.cs:36-53` (`ShouldBalHeal_DisabledByConfig_ReturnsFalse`, `ShouldBalHeal_EnabledButPreAmsterdam_ReturnsFalse`, `ShouldBalHeal_EnabledAndAmsterdamActive_ReturnsTrue`); default `false` is set in `SnapSyncOrchestratorOptions.cs:39`.

## Common mistakes

| Symptom | Cause | Fix |
|---|---|---|
| `BalHealEnabled = true` but nothing happens | Pre-Amsterdam fork active at the pivot | Confirm `IChainActivations` resolves Amsterdam or later at the relevant block |
| `BlockAccessListFetcher.FetchAsync` throws `InvalidOperationException` | Every serviceable (`snap/2`-supporting) peer refused, mismatched, or went stateless for at least one requested block | Widen the peer pool, or check whether any peers actually advertise `snap/2` (`DevP2PConfig.AdvertiseSnap2`) |
| A verified BAL entry doesn't change state | The affected account/slot fell outside `ISnapTaskFrontier`'s already-fetched range | Expected — that range will be covered correctly once Phase 2 reaches it |
| Conflicting values for the same account after apply | Assumed list order determines the winner | It doesn't — highest `BlockAccessIndex` wins, regardless of list position |

## Decision guidance

| Situation | Action |
|---|---|
| Pivot is stable, Phase 2/3 keeps up on its own | Leave `BalHealEnabled` at its default (`false`) — no need for this machinery |
| Pivot moves faster than heal converges, chain is on/after Amsterdam | Enable `BalHealEnabled`; peers must advertise `snap/2` |
| Pivot moves but chain is pre-Amsterdam | BAL heal is unavailable at this fork — rely on `TrieHealer`'s own pivot-follow instead (see `devp2p-snap-sync`) |

For full documentation, see: https://docs.nethereum.com/docs/devp2p/guide-block-access-lists
