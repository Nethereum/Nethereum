using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Documentation;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.Nodes.Rlp;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.ProofVerification;

namespace Nethereum.DevP2P.Sync.Snap.Healing
{
    public sealed partial class TrieHealer
    {
        private async Task<HealResult?> TryRotateUnseededPivotAsync(bool stallTrigger, CancellationToken ct)
        {
            try
            {
                (byte[] Root, ulong Block)? refreshed;
                var refreshSw = System.Diagnostics.Stopwatch.StartNew();
                using (var refreshCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    refreshCts.CancelAfter(TimeSpan.FromSeconds(30));
                    try { refreshed = await PivotRefresher(stallTrigger, refreshCts.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        _logger.LogWarning("heal.pivot.refresh_timeout after {Ms}ms — keeping current root", refreshSw.ElapsedMilliseconds);
                        refreshed = null;
                    }
                }
                if (refreshSw.ElapsedMilliseconds > 5000)
                    _logger.LogWarning("heal.pivot.refresh slow: {Ms}ms", refreshSw.ElapsedMilliseconds);
                var newRoot = refreshed?.Root;
                bool rootChanged = newRoot != null && newRoot.Length == 32
                    && !ByteUtil.AreEqual(newRoot, _liveTargetRoot);
                if (rootChanged
                    && ShouldRotateHealPivot(stallTrigger, rootChanged, _currentPivotBlock, refreshed.Value.Block, StalePivotDistanceBlocks))
                {
                    _logger.LogWarning(
                        "Heal {Trigger} at round {Round} (stall={Stall}) — root 0x{Old} (blk {OldBlk}) stale, signalling retarget to 0x{New} (blk {NewBlk})",
                        stallTrigger ? "stalled" : "pivot-stale",
                        _round, _stallRounds, _liveTargetRoot.ToHex(), _currentPivotBlock, newRoot.ToHex(), refreshed.Value.Block);
                    _metrics?.RecordPhase3PivotRotation();
                    return new HealResult(false, _totalNodesFetched, _liveTargetRoot, Array.Empty<byte>(),
                        _prunedChildren, _requiredAbsent, _requiredStale,
                        NeedsRetarget: true, RetargetRoot: newRoot, RetargetBlock: refreshed.Value.Block);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Heal pivot refresh failed; continuing against current root");
            }
            return null;
        }

        private void SeedQueue(
            IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> seedStorageHeal,
            IReadOnlyList<byte[]> seedCodeHeal)
        {
            if (_seeded)
            {
                foreach (var s in seedStorageHeal)
                {
                    var t = new HealTask(IsStorage: true, AccountHash: s.AccountHash, NibblePath: Array.Empty<byte>(), ExpectedHash: s.StorageRoot);
                    if (_inQueue.Add(LocKey(t))) _queue.Push(t);
                }
            }
            else if (!_codeOnly)
            {
                var t = new HealTask(IsStorage: false, AccountHash: null, NibblePath: Array.Empty<byte>(), ExpectedHash: _liveTargetRoot);
                if (_inQueue.Add(LocKey(t))) _queue.Push(t);
            }
            if (_hasCodeSeeds)
                foreach (var h in seedCodeHeal) _neededCode.Add(h);
        }

        private long SatisfyCode(byte[] codeHash)
        {
            _neededCode.Remove(codeHash);
            if (!_codeWaiters.TryGetValue(codeHash, out var leaves)) return 0;
            _codeWaiters.Remove(codeHash);
            long committed = 0;
            foreach (var leafKey in leaves)
            {
                if (!_pending.TryGetValue(leafKey, out var leaf)) continue;
                leaf.MissingChildren--;
                if (leaf.MissingChildren > 0) continue;
                _pending.Remove(leafKey);
                committed += PersistAndPropagate(leafKey, leaf.Task, leaf.Hash, leaf.Blob, _pending, _parentOf);
            }
            return committed;
        }

        private async Task<long> ResolveCodeDependenciesAsync(CancellationToken ct)
        {
            if (_codeStore == null || _neededCode.Count == 0) return 0;
            long committed = 0;
            var toFetch = new List<byte[]>();
            foreach (var h in new List<byte[]>(_neededCode))
            {
                var existing = await _codeStore.GetCodeAsync(h).ConfigureAwait(false);
                if (existing != null && existing.Length > 0) committed += SatisfyCode(h);
                else toFetch.Add(h);
            }
            const int maxCodeRequestCount = 84;
            for (int off = 0; off < toFetch.Count; off += maxCodeRequestCount)
            {
                ct.ThrowIfCancellationRequested();
                int take = Math.Min(maxCodeRequestCount, toFetch.Count - off);
                var chunk = toFetch.GetRange(off, take);
                ByteCodesMessage resp;
                try { resp = await _scheduler.FetchByteCodesAsync(chunk, ResponseBytesBudget, ct).ConfigureAwait(false); }
                catch (FetchRequestFailedException) { continue; }
                foreach (var code in resp.Codes)
                {
                    if (code == null || code.Length == 0) continue;
                    var h = Sha3Keccack.Current.CalculateHash(code);
                    if (!_neededCode.Contains(h)) continue;
                    await _codeStore.SaveCodeAsync(h, code).ConfigureAwait(false);
                    _metrics?.RecordPhase3BytecodesHealed(1);
                    committed += SatisfyCode(h);
                }
            }
            return committed;
        }

        private async Task<(bool Resolved, byte[] NewRoot, ulong NewBlock, IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> UpdatedSeeds)>
            TryResolveSeededRotationAsync(
                bool stalled,
                CancellationToken ct,
                IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> seedStorageHeal)
        {
            if (PivotRefresher == null || _scheduler == null)
            {
                LogSeededRotateUnavailable();
                return (false, null, 0, null);
            }

            var target = await TryFetchRotationTargetAsync(stalled, ct).ConfigureAwait(false);
            if (target == null) return (false, null, 0, null);
            var newRoot = target.Value.Root;

            var sorted = new List<(byte[] AccountHash, byte[] StorageRoot)>(seedStorageHeal);
            sorted.Sort((a, b) => ByteArrayComparer.Current.Compare(a.AccountHash, b.AccountHash));

            var resolvedStorageRoot = await ResolveSeedStorageRootsAsync(sorted, newRoot, ct).ConfigureAwait(false);

            return FinalizeSeededRotation(sorted, resolvedStorageRoot, newRoot, target.Value.Block, seedStorageHeal);
        }

        private void LogSeededRotateUnavailable()
        {
            var now = DateTimeOffset.UtcNow;
            if (SnapBootstrapper.ShouldLogStalledRecycle(_lastRefresherUnavailableLogAt, now, RefresherUnavailableLogInterval))
            {
                _lastRefresherUnavailableLogAt = now;
                _logger.LogError(
                    "heal.seeded.rotate unavailable: PivotRefresher={HasRefresher} scheduler={HasScheduler} — " +
                    "this seeded heal can never roll off its pinned root and will stall once it ages " +
                    "out of peers' serving window",
                    PivotRefresher != null, _scheduler != null);
            }
        }

        private async Task<(byte[] Root, ulong Block)?> TryFetchRotationTargetAsync(bool stalled, CancellationToken ct)
        {
            (byte[] Root, ulong Block)? refreshed;
            using (var refreshCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                refreshCts.CancelAfter(TimeSpan.FromSeconds(30));
                try { refreshed = await PivotRefresher(stalled, refreshCts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "heal.seeded.rotate refresher threw — keeping current root, will retry");
                    return null;
                }
            }
            var newRoot = refreshed?.Root;
            if (newRoot == null || newRoot.Length != 32 || ByteUtil.AreEqual(newRoot, _liveTargetRoot)) return null;
            if (!ShouldRotateHealPivot(stalled, true, _currentPivotBlock, refreshed.Value.Block, StalePivotDistanceBlocks)) return null;
            return refreshed;
        }

        private async Task<byte[][]> ResolveSeedStorageRootsAsync(
            List<(byte[] AccountHash, byte[] StorageRoot)> sorted, byte[] newRoot, CancellationToken ct)
        {
            var resolvedStorageRoot = new byte[sorted.Count][];
            int claimFrom = 0;
            bool resolveFailed = false;
            var applyLock = new object();
            var decoder = new AccountEncoder();
            var lastSeedHash = sorted[sorted.Count - 1].AccountHash;

            async Task ResolveWorkerAsync()
            {
                while (true)
                {
                    int mine = -1;
                    lock (applyLock)
                    {
                        if (resolveFailed) return;
                        while (claimFrom < sorted.Count && resolvedStorageRoot[claimFrom] != null) claimFrom++;
                        if (claimFrom >= sorted.Count) return;
                        mine = claimFrom++;
                    }
                    try
                    {
                        var anchor = sorted[mine].AccountHash;
                        var resp = await _scheduler.FetchAccountRangeAsync(newRoot, anchor, lastSeedHash, 16_384, ct).ConfigureAwait(false);
                        var keys = new List<byte[]>(resp.Accounts.Count);
                        var values = new List<byte[]>(resp.Accounts.Count);
                        foreach (var e in resp.Accounts)
                        {
                            if (e.Hash is not { Length: 32 }) throw new InvalidOperationException("peer returned malformed account hash");
                            keys.Add(e.Hash);
                            values.Add(SlimAccountEncoder.FromSlim(e.Body));
                        }
                        var proof = (IList<byte[]>)(resp.Proof ?? new List<byte[]>());
                        var pr = ProofVerification.Current.Range.Verify(newRoot, anchor, keys, values, proof);
                        if (!pr.Valid) throw new InvalidOperationException("account-range proof invalid");

                        var pageEnd = keys.Count > 0 ? keys[keys.Count - 1] : null;
                        lock (applyLock)
                        {
                            int k = 0;
                            for (int j = mine; j < sorted.Count; j++)
                            {
                                if (resolvedStorageRoot[j] != null) continue;
                                var seed = sorted[j];
                                bool covered = pageEnd == null
                                    ? !pr.HasMore
                                    : ByteArrayComparer.Current.Compare(seed.AccountHash, pageEnd) <= 0 || !pr.HasMore;
                                if (!covered) break;
                                while (k < keys.Count && ByteArrayComparer.Current.Compare(keys[k], seed.AccountHash) < 0) k++;
                                if (k < keys.Count && ByteUtil.AreEqual(keys[k], seed.AccountHash))
                                {
                                    var newStorageRoot = decoder.Decode(values[k]).StateRoot;
                                    resolvedStorageRoot[j] =
                                        newStorageRoot is { Length: 32 } && !ByteUtil.AreEqual(newStorageRoot, DefaultValues.EMPTY_TRIE_HASH)
                                            ? newStorageRoot
                                            : DefaultValues.EMPTY_TRIE_HASH;
                                }
                                else
                                {
                                    resolvedStorageRoot[j] = DefaultValues.EMPTY_TRIE_HASH;
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        lock (applyLock) { resolveFailed = true; }
                        _logger.LogWarning(ex, "heal.seeded.rotate seed re-resolve failed (peer data or fetch) — keeping current root");
                        return;
                    }
                }
            }

            var resolveWorkers = new Task[HealFetchConcurrency];
            for (int w = 0; w < resolveWorkers.Length; w++) resolveWorkers[w] = Task.Run(ResolveWorkerAsync, ct);
            await Task.WhenAll(resolveWorkers).ConfigureAwait(false);
            return resolvedStorageRoot;
        }

        private (bool Resolved, byte[] NewRoot, ulong NewBlock, IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> UpdatedSeeds)
            FinalizeSeededRotation(
                List<(byte[] AccountHash, byte[] StorageRoot)> sorted,
                byte[][] resolvedStorageRoot,
                byte[] newRoot,
                ulong newBlock,
                IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> seedStorageHeal)
        {
            var updated = new List<(byte[] AccountHash, byte[] StorageRoot)>(sorted.Count);
            var dropWipes = new List<byte[]>();
            var unresolved = new List<(byte[] AccountHash, byte[] StorageRoot)>();
            for (int j = 0; j < sorted.Count; j++)
            {
                var r = resolvedStorageRoot[j];
                if (r == null) { unresolved.Add(sorted[j]); continue; }
                if (ByteUtil.AreEqual(r, DefaultValues.EMPTY_TRIE_HASH)) dropWipes.Add(sorted[j].AccountHash);
                else updated.Add((sorted[j].AccountHash, r));
            }
            if (updated.Count == 0 && dropWipes.Count == 0)
            {
                return (false, null, 0, null);
            }
            foreach (var w in dropWipes) _sink.WipeStorage(w);
            _sink.Flush();
            int reResolvedCount = updated.Count;
            foreach (var u in unresolved) updated.Add(u);

            _logger.LogWarning(
                "heal.seeded.rotate root 0x{Old} (blk {OldBlk}) -> 0x{New} (blk {NewBlk}); {Kept}/{Total} seeds re-resolved — signalling retarget",
                _liveTargetRoot.ToHex(), _currentPivotBlock, newRoot.ToHex(), newBlock,
                reResolvedCount, seedStorageHeal.Count);
            return (true, newRoot, newBlock, updated);
        }

        private async Task<HealResult> BuildFinalResult(
            IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> seedStorageHeal,
            IReadOnlyList<byte[]> seedCodeHeal)
        {
            if (_queue.Count > 0)
            {
                _logger.LogWarning("Heal exhausted {MaxRounds} rounds with {Remaining} unresolved tasks", MaxRounds, _queue.Count);
                return new HealResult(false, _totalNodesFetched, _liveTargetRoot, Array.Empty<byte>(),
                    _prunedChildren, _requiredAbsent, _requiredStale);
            }

            if (_pending.Count > 0)
            {
                _logger.LogError(
                    "Heal queue drained with {Count} uncommitted parents — reporting non-convergence", _pending.Count);
                return new HealResult(false, _totalNodesFetched, _liveTargetRoot, Array.Empty<byte>(),
                    _prunedChildren, _requiredAbsent, _requiredStale);
            }

            _logger.LogInformation(
                "heal.transition loop exited at round {Round} (queue={Queue}, codes={Codes}) — verifying convergence",
                _round, _queue.Count, _neededCode.Count);
            bool matched;
            if (_seeded)
            {
                matched = true;
                foreach (var s in seedStorageHeal)
                {
                    if (!_sink.HasNode(true, s.AccountHash, Array.Empty<byte>(), s.StorageRoot))
                    {
                        matched = false;
                        break;
                    }
                }
            }
            else
            {
                matched = _sink.HasRoot(_liveTargetRoot);
            }

            if (matched && _hasCodeSeeds)
            {
                foreach (var h in seedCodeHeal)
                {
                    var code = await _codeStore.GetCodeAsync(h).ConfigureAwait(false);
                    if (code == null || code.Length == 0) { matched = false; break; }
                }
            }

            _logger.LogInformation(
                "Heal complete after {Rounds} rounds ({Fetched} nodes fetched): target=0x{Target} matched={Matched}",
                _round, _totalNodesFetched, _liveTargetRoot.ToHex(), matched);
            return new HealResult(matched, _totalNodesFetched, _liveTargetRoot,
                matched ? _liveTargetRoot : Array.Empty<byte>(),
                _prunedChildren, _requiredAbsent, _requiredStale);
        }

        private static IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> SanitizeSeeds(
            IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> seedStorageHeal)
        {
            if (seedStorageHeal != null)
            {
                var valid = new List<(byte[] AccountHash, byte[] StorageRoot)>(seedStorageHeal.Count);
                foreach (var s in seedStorageHeal)
                    if (s.AccountHash is { Length: 32 } && s.StorageRoot is { Length: 32 })
                        valid.Add(s);
                seedStorageHeal = valid;
            }
            return seedStorageHeal;
        }

        private static IReadOnlyList<byte[]> SanitizeCodeSeeds(IReadOnlyList<byte[]> seedCodeHeal)
        {
            if (seedCodeHeal == null || seedCodeHeal.Count == 0) return seedCodeHeal;
            var seen = new HashSet<byte[]>(ByteArrayComparer.Current);
            var valid = new List<byte[]>(seedCodeHeal.Count);
            foreach (var h in seedCodeHeal)
            {
                if (h is not { Length: 32 }) continue;
                if (ByteUtil.AreEqual(h, DefaultValues.EMPTY_DATA_HASH)) continue;
                if (seen.Add(h)) valid.Add(h);
            }
            return valid;
        }
    }
}
