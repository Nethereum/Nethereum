using System.Collections.Generic;
using System;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal static class ReconcileOutcome
    {
        private const int MaxUnresolvableCollected = 65_536;

        internal static FlatStateReconcileResult Conclude(
            IReadOnlyCollection<ShardResult> results, Action<FlatStateReconcileResult, int> onTotals)
        {
            var totals = Sum(results);
            var unresolvable = new List<(byte[] AccountHash, byte[] StorageRoot)>();
            var missingNodes = new List<(byte[] Owner, byte[] Path)>();
            var seenMissingNodes = new HashSet<(byte[] Owner, byte[] Path)>(OwnerPathComparer.Instance);
            foreach (var r in results)
            {
                unresolvable.AddRange(r.Unresolvable);
                foreach (var node in r.MissingNodes)
                    if (seenMissingNodes.Add(node)) missingNodes.Add(node);
            }
            onTotals?.Invoke(totals, unresolvable.Count + missingNodes.Count);
            if (unresolvable.Count == 0 && missingNodes.Count == 0) return totals;
            throw Unresolvable(unresolvable, missingNodes);
        }

        private static FlatStateReconcileResult Sum(IReadOnlyCollection<ShardResult> results)
        {
            long accounts = 0, slots = 0, gA = 0, gS = 0, aA = 0, aS = 0, pA = 0, pS = 0, ranges = 0, leaves = 0, missing = 0, differing = 0;
            foreach (var r in results)
            {
                accounts += r.Result.AccountsScanned; slots += r.Result.SlotsScanned;
                gA += r.Result.GhostAccountsDeleted; gS += r.Result.GhostSlotsDeleted;
                aA += r.Result.AccountsAdded; aS += r.Result.SlotsAdded;
                pA += r.Result.AccountsPatched; pS += r.Result.SlotsPatched;
                ranges += r.Result.RangesWalked; leaves += r.Result.LeavesWalked;
                missing += r.Result.TrieNodesMissing; differing += r.Result.TrieNodesDiffering;
            }
            return new FlatStateReconcileResult(accounts, slots, gA, gS, aA, aS, pA, pS)
            {
                RangesWalked = ranges,
                LeavesWalked = leaves,
                TrieNodesMissing = missing,
                TrieNodesDiffering = differing,
            };
        }

        private static FlatReconcileUnresolvableNodeException Unresolvable(
            List<(byte[] AccountHash, byte[] StorageRoot)> unresolvable, List<(byte[] Owner, byte[] Path)> missingNodes)
        {
            var subtrees = unresolvable.Count;
            if (unresolvable.Count > MaxUnresolvableCollected)
                unresolvable.RemoveRange(MaxUnresolvableCollected, unresolvable.Count - MaxUnresolvableCollected);
            var first = missingNodes.Count > 0
                ? $"first missing node owner=0x{missingNodes[0].Owner.ToHex()} path={missingNodes[0].Path.ToHex()}"
                : $"first unresolvable owner=0x{unresolvable[0].AccountHash.ToHex()} storageRoot=0x{unresolvable[0].StorageRoot.ToHex()}";
            return new FlatReconcileUnresolvableNodeException(
                $"Flat reconcile: {missingNodes.Count} missing or stale trie node(s) and {subtrees} unresolvable storage subtree(s); {first}. " +
                "Each needs its ancestor chain deleted and a heal at the current pivot.",
                unresolvable, missingNodes, null);
        }
    }
}
