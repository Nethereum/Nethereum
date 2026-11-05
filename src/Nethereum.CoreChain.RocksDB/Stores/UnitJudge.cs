using System;
using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class UnitJudge
    {
        private readonly FlatReconcileEnv _env;
        private readonly TrieProof _proof;

        public UnitJudge(FlatReconcileEnv env, TrieProof proof)
        {
            _env = env;
            _proof = proof;
        }

        internal void Judge(FlatUnit u)
        {
            u.Expect();
            foreach (var large in u.LargeOwners) u.Slots += large.Slots;
            var touched = new Dictionary<byte[], AccountRoots>(ByteArrayComparer.Current);
            if (TrieProof.Proven(_proof.VerifiedReference(Array.Empty<byte>(), u.Prefix, u.Root), u.AccountRange))
                u.AdoptHoles(Array.Empty<byte>(), u.AccountRange);
            else if (!WalkUnitAccounts(u, touched))
                touched = null;
            if (touched != null) ScheduleOwnerWork(u, touched);
            if (u.Arrive()) Complete(u);
        }

        private bool WalkUnitAccounts(FlatUnit u, IDictionary<byte[], AccountRoots> touched)
        {
            var walk = new FlatLeafWalk(_env, u.Queue.Token, u.DryRun, 0);
            walk.ReconcileAccountsOnly(u.Root, u.Start, u.End, touched);
            var result = u.Record(walk, Array.Empty<byte>(), "accounts", "first_pass");
            u.MissingCode.Clear();
            u.MissingCode.UnionWith(result.MissingCode);
            return !walk.MetMissingNodes;
        }

        private void ScheduleOwnerWork(FlatUnit u, IDictionary<byte[], AccountRoots> touched)
        {
            var walked = new HashSet<byte[]>(ByteArrayComparer.Current);
            foreach (var work in OwnerWork(u, touched))
            {
                walked.Add(work.Owner);
                Schedule(u, work);
            }
            foreach (var candidate in u.OwnerHoleCandidates)
                if (!walked.Contains(candidate.Key)) u.AdoptHoles(candidate.Key, candidate.Value);
        }

        private IEnumerable<OwnerWork> OwnerWork(
            FlatUnit u, IDictionary<byte[], AccountRoots> touched)
        {
            var judged = new HashSet<byte[]>(ByteArrayComparer.Current);
            foreach (var owner in u.OwnersToWalk)
            {
                judged.Add(owner.Owner);
                var s = StorageRootOf(owner.Owner, owner.ExpectedStorageRoot, touched);
                if (IsAddedWithStorage(owner.Owner, s, touched) || owner.Malformed || !ByteUtil.AreEqual(owner.ComputedRoot, s))
                    yield return WholeOwner(owner.Owner, s, "first_pass");
            }
            foreach (var large in u.LargeOwners)
            {
                judged.Add(large.Owner);
                foreach (var work in LargeOwnerWork(u, large, StorageRootOf(large.Owner, large.ExpectedStorageRoot, touched), touched))
                    yield return work;
            }
            foreach (var account in touched)
                if (!judged.Contains(account.Key) && StorageDiffersFromFlat(account.Value))
                    yield return WholeOwner(account.Key, account.Value.NewTrieRoot, "first_pass");
        }

        private static byte[] StorageRootOf(
            byte[] owner, byte[] expectedStorageRoot, IDictionary<byte[], AccountRoots> touched)
            => touched.TryGetValue(owner, out var roots) ? roots.NewTrieRoot : expectedStorageRoot;

        private static bool IsAddedWithStorage(
            byte[] owner, byte[] storageRoot, IDictionary<byte[], AccountRoots> touched)
            => touched.TryGetValue(owner, out var roots) && roots.OldFlatRoot == null && FlatStateWalk.HasStorageRoot(storageRoot);

        private static bool StorageDiffersFromFlat(AccountRoots roots)
            => roots.OldFlatRoot == null
                ? FlatStateWalk.HasStorageRoot(roots.NewTrieRoot)
                : !ByteUtil.AreEqual(roots.OldFlatRoot, roots.NewTrieRoot);

        private IEnumerable<OwnerWork> LargeOwnerWork(
            FlatUnit u, LargeOwner large, byte[] s, IDictionary<byte[], AccountRoots> touched)
        {
            if (!FlatStateWalk.HasStorageRoot(s) || large.Malformed || IsAddedWithStorage(large.Owner, s, touched))
            {
                yield return WholeOwner(large.Owner, s, "phase2_large");
                yield break;
            }
            var verdicts = new (RangeBoundary Kind, byte[] Reference)[16];
            for (int nibble = 0; nibble < 16; nibble++)
            {
                verdicts[nibble] = _proof.VerifiedReference(large.Owner, new[] { (byte)nibble }, s);
                if (verdicts[nibble].Kind != RangeBoundary.NoBoundary) continue;
                yield return WholeOwner(large.Owner, s, "phase2_large");
                yield break;
            }
            foreach (var range in large.Ranges)
            {
                if (TrieProof.Proven(verdicts[range.Nibble], range.Hashed)) u.AdoptHoles(large.Owner, range.Hashed);
                else yield return NibbleOfOwner(large.Owner, s, range.Nibble);
            }
        }

        private OwnerWork WholeOwner(byte[] owner, byte[] storageRoot, string cause)
            => new OwnerWork(owner, "owner", cause, walk => walk.ReconcileOwnerStorage(owner, storageRoot));

        private OwnerWork NibbleOfOwner(byte[] owner, byte[] storageRoot, int nibble)
        {
            var (start, end) = FlatStateWalk.NibbleBounds(new[] { (byte)nibble });
            return new OwnerWork(owner, $"nibble={nibble:x}", "phase2_large",
                walk => walk.ReconcileStorageSlots(owner, storageRoot, start, end));
        }

        private void Schedule(FlatUnit u, OwnerWork work)
        {
            u.Expect();
            u.Queue.Enqueue(() =>
            {
                var walk = new FlatLeafWalk(_env, u.Queue.Token, u.DryRun, 0);
                work.Walk(walk);
                u.Record(walk, work.Owner, work.Range, work.Cause);
                if (u.Arrive()) Complete(u);
            });
        }

        private void Complete(FlatUnit u)
        {
            if (u.Failed || u.Holes.Count > 0) return;
            if (!u.DryRun) _env.Inventory.MergeMissingCode(u.MissingCode);
            if (u.Journal) CertifyJournal.MarkUnitDone(_env.Rocks, u.Index);
        }
    }
}
