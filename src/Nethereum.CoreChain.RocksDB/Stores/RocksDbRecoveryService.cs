using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class RocksDbRecoveryService
    {
        private readonly RocksDbManager _rocks;
        private readonly RocksDbStateStore _rawFlatState;
        private readonly ITrieNodeStore StateTrieNodes;
        private readonly IBlockStore Blocks;
        private readonly IChainMetadataStore Metadata;
        private readonly IStateDiffStore Diffs;
        private readonly bool JournalEnabled;
        private readonly StorePairingGuard _pairingGuard;
        private readonly RocksDbPromotionService _promotionService;

        public RocksDbRecoveryService(
            RocksDbManager rocks, RocksDbStateStore rawFlatState, ITrieNodeStore stateTrieNodes,
            IBlockStore blocks, IChainMetadataStore metadata, IStateDiffStore diffs, bool journalEnabled,
            StorePairingGuard pairingGuard = null, RocksDbPromotionService promotionService = null)
        {
            _rocks = rocks;
            _rawFlatState = rawFlatState;
            StateTrieNodes = stateTrieNodes;
            Blocks = blocks;
            Metadata = metadata;
            Diffs = diffs;
            JournalEnabled = journalEnabled;
            _pairingGuard = pairingGuard;
            _promotionService = promotionService;
        }

        public Task<FlatStateReconcileResult> ReconcileFlatStateAsync(
            byte[] stateRoot, Action<string> progress, CancellationToken ct)
            => NewFlatStateReconciler()
                .ReconcileFlatStateAsync(stateRoot, progress, ct);

        public Task<FlatStateReconcileResult> VerifyFlatStateAsync(
            byte[] stateRoot, Action<string> progress, CancellationToken ct, long sampleAccountsPerShard = 0)
            => NewFlatStateReconciler()
                .VerifyFlatStateAsync(stateRoot, progress, ct, sampleAccountsPerShard);

        private FlatStateReconciler NewFlatStateReconciler()
            => new FlatStateReconciler(_rocks, _rawFlatState, StateTrieNodes);

        public async Task<ulong> RecoverToAsync(
            ulong targetBlock, FlatRecoverySource flatSource, Action<string> progress, CancellationToken ct,
            bool skipVerify = false)
        {
            var header = await Blocks.GetByNumberAsync(targetBlock).ConfigureAwait(false);
            if (header?.StateRoot == null || header.StateRoot.Length != 32)
                throw new InvalidOperationException($"Recover: block {targetBlock} has no header state root in the store.");
            var hash = await Blocks.GetHashByNumberAsync(targetBlock).ConfigureAwait(false);
            if (hash == null || hash.Length != 32)
                throw new InvalidOperationException($"Recover: block {targetBlock} has no block hash in the store.");
            var root = header.StateRoot;

            var stats = new RocksDbNodeReverseDiffStore(_rocks, buildKeyMajorIndex: false)
                .MaterializingRewindTo(targetBlock);
            (StateTrieNodes as ICacheInvalidatable)?.ClearCache();
            progress?.Invoke(
                $"recover.rewind applied={stats.EntriesApplied} puts={stats.Puts} deletes={stats.Deletes} " +
                $"minHistory={stats.MinHistoryBlock} maxHistory={stats.MaxHistoryBlock} target={targetBlock}");
            if (stats.EntriesApplied == 0 && !StateTrieNodes.ContainsKey(root))
                throw new InvalidOperationException(
                    $"Recover: the node history holds no reverse-diffs above block {targetBlock} and the target root does " +
                    "not resolve — cannot roll the trie back. Aborting.");

            if (flatSource == FlatRecoverySource.ReplayJournal)
                await ReplayFlatJournalToAsync(targetBlock, stats.MaxHistoryBlock, progress, ct).ConfigureAwait(false);
            else
                await ReconcileFlatFromTrieAsync(root, progress, ct).ConfigureAwait(false);

            if (flatSource == FlatRecoverySource.ReconcileFromTrie && !skipVerify)
            {
                var verify = await VerifyFlatStateAsync(root, progress, ct).ConfigureAwait(false);
                if (verify.TotalRepairs != 0)
                    throw new InvalidOperationException(
                        $"Recover: post-reconcile verify still found {verify.TotalRepairs} flat/trie diffs at block " +
                        $"{targetBlock} — the node history does not fully cover the torn head. Aborting WITHOUT moving the cursor.");
            }
            else if (!StateTrieNodes.ContainsKey(root))
            {
                throw new InvalidOperationException(
                    $"Recover: the account root for block {targetBlock} does not resolve after the flat restore — the " +
                    "node history did not fully restore the head. Aborting WITHOUT moving the cursor.");
            }

            Metadata.Commit(targetBlock, hash);
            var fh = Metadata.GetLastFetchedHeader();
            var fb = Metadata.GetLastFetchedBody();
            Metadata.SetLastFetchedHeaderAndBody(fh > targetBlock ? targetBlock : fh, fb > targetBlock ? targetBlock : fb);
            if (JournalEnabled) await Diffs.DeleteDiffsAboveBlockAsync(targetBlock).ConfigureAwait(false);
            progress?.Invoke($"recover.committed head -> {targetBlock} (flat via {flatSource}).");
            return targetBlock;
        }

        private async Task ReplayFlatJournalToAsync(
            ulong targetBlock, ulong maxHistoryBlock, Action<string> progress, CancellationToken ct)
        {
            var newestFlat = await Diffs.GetNewestDiffBlockAsync().ConfigureAwait(false);
            ulong flatTop = newestFlat.HasValue ? (ulong)newestFlat.Value : targetBlock;
            if (flatTop > maxHistoryBlock) flatTop = maxHistoryBlock;
            long accounts = 0, slots = 0;
            for (ulong b = flatTop; b > targetBlock; b--)
            {
                ct.ThrowIfCancellationRequested();
                var diff = await Diffs.GetBlockDiffAsync((System.Numerics.BigInteger)b).ConfigureAwait(false);
                if (diff == null)
                    throw new InvalidOperationException(
                        $"Flat-journal replay: no diff for block {b} (a hole in {targetBlock + 1}..{flatTop}) — " +
                        "incomplete; use ReconcileFromTrie instead.");
                foreach (var a in diff.AccountDiffs)
                {
                    if (a.PreValue == null) await _rawFlatState.DeleteAccountAsync(a.Address).ConfigureAwait(false);
                    else await _rawFlatState.SaveAccountAsync(a.Address, a.PreValue).ConfigureAwait(false);
                    accounts++;
                }
                foreach (var s in diff.StorageDiffs)
                {
                    await _rawFlatState.SaveStorageByKeccakAsync(
                        s.Address, s.SlotKey, s.PreValue ?? System.Array.Empty<byte>()).ConfigureAwait(false);
                    slots++;
                }
            }
            progress?.Invoke($"recover.flat-replay accounts={accounts} slots={slots} over ({targetBlock},{flatTop}]");
        }

        private async Task ReconcileFlatFromTrieAsync(byte[] root, Action<string> progress, CancellationToken ct)
        {
            NewFlatStateReconciler().ClearCleanShardMarkers(root);
            var reconcile = await ReconcileFlatStateAsync(root, progress, ct).ConfigureAwait(false);
            progress?.Invoke($"recover.reconcile repairs={reconcile.TotalRepairs}");
        }

        public async Task<(ulong Head, bool Recovered)> EnsureConsistentHeadAsync(
            Action<string> progress, CancellationToken ct = default)
        {
            _pairingGuard?.EnsurePaired();

            _promotionService?.ReconcilePromotionOnBoot(Metadata.GetDurableStateBlock());

            var head = Metadata.GetLastBlock();
            if (head == 0 || !_rocks.Options.PathKeyedState) return (head, false);

            var headHeader = await Blocks.GetByNumberAsync(head).ConfigureAwait(false);
            if (headHeader?.StateRoot != null && StateTrieNodes.ContainsKey(headHeader.StateRoot))
                return (head, false);

            var durableBlock = Metadata.GetDurableStateBlock();
            if (durableBlock > 0 && durableBlock < head)
            {
                var durableHeader = await Blocks.GetByNumberAsync(durableBlock).ConfigureAwait(false);
                if (durableHeader?.StateRoot != null && StateTrieNodes.ContainsKey(durableHeader.StateRoot))
                {

                    if (JournalEnabled) await Diffs.DeleteDiffsAboveBlockAsync(durableBlock).ConfigureAwait(false);

                    var durableHash = await Blocks.GetHashByNumberAsync(durableBlock).ConfigureAwait(false);
                    Metadata.Commit(durableBlock, durableHash);
                    progress?.Invoke(
                        $"integrity.durable-cursor head {head:N0} ahead of clean flush {durableBlock:N0}; " +
                        "serving from durable cursor, re-execute forward.");
                    return (durableBlock, true);
                }
            }

            var fetchBound = Metadata.GetLastFetchedHeader();
            var storedHeaderTip = await Blocks.GetHeightAsync().ConfigureAwait(false);
            if (storedHeaderTip > fetchBound) fetchBound = (ulong)storedHeaderTip;
            if (fetchBound > head)
            {
                ulong aheadResolvable = 0;
                for (ulong n = fetchBound; n > head; n--)
                {
                    var hn = await Blocks.GetByNumberAsync(n).ConfigureAwait(false);
                    if (hn?.StateRoot != null && StateTrieNodes.ContainsKey(hn.StateRoot)) { aheadResolvable = n; break; }
                }
                if (aheadResolvable > head)
                {
                    var aheadHash = await Blocks.GetHashByNumberAsync(aheadResolvable).ConfigureAwait(false);
                    if (aheadHash == null || aheadHash.Length != 32)
                        throw new InvalidOperationException(
                            $"Cursor-behind-trie: block {aheadResolvable:N0} resolves its state root but has no 32-byte " +
                            "block hash in the store — refusing to advance the cursor with a missing/stale hash.");
                    Metadata.Commit(aheadResolvable, aheadHash);
                    progress?.Invoke(
                        $"integrity.cursor-behind-trie stale cursor {head:N0} < resolvable trie head {aheadResolvable:N0}; " +
                        "advancing cursor, no rollback.");
                    return (aheadResolvable, true);
                }
            }

            var retention = _rocks.Options.TrieNodeHistoryBlocks;
            ulong floor = (retention > 0 && head > (ulong)retention) ? head - (ulong)retention : 0;
            progress?.Invoke(
                $"integrity.torn-head committed={head} root does not resolve; probing [{floor:N0}..{head:N0}] for a resolvable head.");

            ulong resolvable = 0;
            for (ulong n = head - 1; ; n--)
            {
                var hn = await Blocks.GetByNumberAsync(n).ConfigureAwait(false);
                if (hn?.StateRoot != null && StateTrieNodes.ContainsKey(hn.StateRoot)) { resolvable = n; break; }
                if (n <= floor) break;
            }
            if (resolvable == 0)
                throw new InvalidOperationException(
                    $"Integrity gate: torn head at block {head:N0}, and no resolvable trie root found in " +
                    $"[{floor:N0}..{head:N0}]. Node history cannot self-heal this — restore from a checkpoint.");

            progress?.Invoke($"integrity.recovering torn head {head:N0} -> {resolvable:N0} via node-history reconcile.");
            await RecoverToAsync(resolvable, FlatRecoverySource.ReconcileFromTrie, progress, ct).ConfigureAwait(false);
            return (resolvable, true);
        }

        public System.Collections.Generic.IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> GetPersistedDamage()
            => NewFlatStateReconciler().GetPersistedDamage();

        public void ClearPersistedDamage()
            => NewFlatStateReconciler().ClearPersistedDamage();

        public System.Collections.Generic.IReadOnlyList<byte[]> GetPersistedMissingCode()
            => NewFlatStateReconciler().GetPersistedMissingCode();

        public void ClearPersistedMissingCode()
            => NewFlatStateReconciler().ClearPersistedMissingCode();
    }
}
