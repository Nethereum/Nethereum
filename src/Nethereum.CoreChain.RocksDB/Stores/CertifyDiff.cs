using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class CertifyDiff
    {
        private readonly FlatReconcileEnv _env;
        private readonly TrieProof _proof;

        public CertifyDiff(FlatReconcileEnv env, TrieProof proof)
        {
            _env = env;
            _proof = proof;
        }

        internal async Task<List<ShardResult>> DiffEpochAsync(byte[] root, Action<string> progress, CancellationToken ct)
        {
            var oldRoot = CertifyJournal.JournalRoot(_env.Rocks);
            using var queue = new WorkQueue(ct);
            var tally = new HashingTally(_env, progress, Stopwatch.StartNew());
            var ctx = new UnitContext(_env, root, queue, tally, progress, DryRun: false, Journal: true);
            var units = new FlatUnit[FlatUnitPass.UnitCount];
            for (int i = 0; i < FlatUnitPass.UnitCount; i++)
                if (CertifyJournal.IsUnitDone(_env.Rocks, i)) units[i] = new FlatUnit(ctx, i, null);
            DiffAbove(units, Sides(oldRoot), Array.Empty<byte>(), RootReference(oldRoot), RootReference(root));
            foreach (var unit in units)
                if (unit != null && unit.Arrive()) FinishDiff(unit);
            await queue.DrainAsync(Math.Min(Environment.ProcessorCount, FlatUnitPass.UnitCount)).ConfigureAwait(false);
            CertifyJournal.CloseEpoch(_env.Rocks, root);
            var diffed = units.Where(u => u != null).ToList();
            progress?.Invoke(DiffLine(diffed));
            return diffed.SelectMany(DiffResults).ToList();
        }

        private DiffSides Sides(byte[] oldRoot)
            => new DiffSides(new MissingNodeRecordingStore(_env.TrieNodes, PreImageOf), new MissingNodeRecordingStore(_env.TrieNodes), oldRoot);

        private (bool Captured, byte[] Blob) PreImageOf(byte[] owner, byte[] path) => CertifyJournal.PreImage(_env.Rocks, owner, path);

        private static byte[] RootReference(byte[] root)
            => root == null || ByteUtil.AreEqual(root, DefaultValues.EMPTY_TRIE_HASH) ? null : root;

        private void DiffAbove(
            FlatUnit[] units, DiffSides s,
            byte[] p, byte[] oldRef, byte[] newRef)
        {
            if (ByteUtil.AreEqual(oldRef, newRef))
            {
                CheckShallowCaptures(units, s, p, newRef);
                ScheduleUnder(units, s, p, (u, us) => CheckCapturesUnder(u, us, Array.Empty<byte>(), p, newRef, u.Prefix));
                return;
            }
            var oldNode = _proof.VerifiedNode(s.Old, Array.Empty<byte>(), p, oldRef);
            var newNode = _proof.VerifiedNode(s.New, Array.Empty<byte>(), p, newRef);
            if (newRef != null && newNode == null) FailUnder(units, p, newRef);
            else if (ShapeDiffers(oldNode, newNode))
                ScheduleUnder(units, s, p, (u, us) => EmitAccounts(u, us, u.Start, u.End, "unit"));
            else if (newNode.Count == 17)
                for (int i = 0; i < 16; i++)
                    DescendAbove(units, s, ChildPath(p, i), TrieProof.ChildReference(oldNode[i]), TrieProof.ChildReference(newNode[i]));
            else if (TrieProof.IsLeafNode(newNode))
                ScheduleUnit(units, s, UnitIndex(ExtensionPath(p, newNode)),
                    (u, us) => DiffNode(u, us, Array.Empty<byte>(), u.Root, p, oldRef, newRef));
            else
                DescendAbove(units, s, ExtensionPath(p, newNode),
                    TrieProof.ChildReference(oldNode[1]), TrieProof.ChildReference(newNode[1]));
        }

        private void DescendAbove(
            FlatUnit[] units, DiffSides s,
            byte[] child, byte[] oldRef, byte[] newRef)
        {
            if (child.Length < 2)
                DiffAbove(units, s, child, oldRef, newRef);
            else
                ScheduleUnit(units, s, UnitIndex(child), (u, us) => DiffNode(u, us, Array.Empty<byte>(), u.Root, child, oldRef, newRef));
        }

        private static byte[] ChildPath(byte[] p, int nibble) => ByteUtil.Merge(p, new[] { (byte)nibble });

        private static byte[] ExtensionPath(byte[] p, Nethereum.RLP.RLPCollection node)
            => ByteUtil.Merge(p, PatriciaPathWalker.CompactToNibbles(node[0].RLPData));

        private static int UnitIndex(byte[] path) => path[0] << 4 | path[1];

        private void ScheduleUnder(
            FlatUnit[] units, DiffSides s, byte[] p,
            Action<FlatUnit, DiffSides> work)
        {
            for (int unit = 0; unit < FlatUnitPass.UnitCount; unit++)
                if (UnderPath(unit, p)) ScheduleUnit(units, s, unit, work);
        }

        private static bool UnderPath(int unit, byte[] p)
            => p.Length == 0 || (unit >> 4 == p[0] && (p.Length == 1 || (unit & 0x0F) == p[1]));

        private void ScheduleUnit(
            FlatUnit[] units, DiffSides s, int unit,
            Action<FlatUnit, DiffSides> work)
        {
            var u = units[unit];
            if (u == null) return;
            u.Expect();
            u.Queue.Enqueue(() =>
            {
                work(u, Sides(s.OldRoot));
                if (u.Arrive()) FinishDiff(u);
            });
        }

        private void FailUnder(FlatUnit[] units, byte[] p, byte[] reference)
        {
            foreach (var u in units)
                if (u != null && UnderPath(u.Index, p)) u.Hole(Array.Empty<byte>(), p, reference);
        }

        private void CheckShallowCaptures(
            FlatUnit[] units, DiffSides s, byte[] p, byte[] reference)
        {
            foreach (var q in CapturedPaths(Array.Empty<byte>(), p).Where(q => q.Length < 2))
            {
                var hole = UnreachableOrHole(s, Array.Empty<byte>(), p, reference, q);
                if (hole != null) FailUnder(units, hole, null);
            }
        }

        private void DiffNode(
            FlatUnit u, DiffSides s,
            byte[] owner, byte[] root, byte[] p, byte[] oldRef, byte[] newRef)
        {
            if (ByteUtil.AreEqual(oldRef, newRef))
            {
                CheckCapturesUnder(u, s, owner, p, newRef, p);
                return;
            }
            var oldNode = _proof.VerifiedNode(s.Old, owner, p, oldRef);
            var newNode = _proof.VerifiedNode(s.New, owner, p, newRef);
            if (newRef != null && newNode == null) u.Hole(owner, p, newRef);
            else if (ShapeDiffers(oldNode, newNode)) Emit(u, s, owner, root, p);
            else if (newNode.Count == 17)
                for (int i = 0; i < 16; i++)
                    DiffNode(u, s, owner, root, ChildPath(p, i), TrieProof.ChildReference(oldNode[i]), TrieProof.ChildReference(newNode[i]));
            else if (TrieProof.IsLeafNode(newNode))
                EmitLeaf(u, s, owner, root, ExtensionPath(p, newNode), oldNode, newNode);
            else
                DiffNode(u, s, owner, root, ExtensionPath(p, newNode),
                    TrieProof.ChildReference(oldNode[1]), TrieProof.ChildReference(newNode[1]));
        }

        private static bool ShapeDiffers(Nethereum.RLP.RLPCollection oldNode, Nethereum.RLP.RLPCollection newNode)
        {
            if (oldNode == null || newNode == null) return true;
            if (oldNode.Count != newNode.Count) return true;
            if (oldNode.Count == 17) return false;
            return TrieProof.IsLeafNode(oldNode) != TrieProof.IsLeafNode(newNode)
                   || !ByteUtil.AreEqual(PatriciaPathWalker.CompactToNibbles(oldNode[0].RLPData), PatriciaPathWalker.CompactToNibbles(newNode[0].RLPData));
        }

        private void EmitLeaf(
            FlatUnit u, DiffSides s,
            byte[] owner, byte[] root, byte[] keyPath, Nethereum.RLP.RLPCollection oldLeaf, Nethereum.RLP.RLPCollection newLeaf)
        {
            if (owner.Length > 0)
            {
                Emit(u, s, owner, root, keyPath);
                return;
            }
            var (start, end) = FlatStateWalk.NibbleBounds(keyPath);
            if (WalkAccounts(u, start, end, keyPath.ToHex()) == null) return;
            StorageDiff(u, s, start, new OldStorage(true, StorageRootOfLeaf(oldLeaf)), StorageRootOfLeaf(newLeaf));
        }

        private static byte[] StorageRootOfLeaf(Nethereum.RLP.RLPCollection leaf)
            => AccountEncoder.Current.Decode(leaf[1].RLPData)?.StateRoot ?? DefaultValues.EMPTY_TRIE_HASH;

        private void Emit(
            FlatUnit u, DiffSides s,
            byte[] owner, byte[] root, byte[] p)
        {
            if (owner.Length == 0)
            {
                var (start, end) = FlatStateWalk.NibbleBounds(p);
                EmitAccounts(u, s, start, end, p.ToHex());
            }
            else if (p.Length == 0)
                EmitWholeOwner(u, owner, root);
            else
                EmitSlots(u, owner, root, p);
        }

        private void EmitAccounts(
            FlatUnit u, DiffSides s,
            byte[] start, byte[] end, string range)
        {
            var touched = WalkAccounts(u, start, end, range);
            if (touched == null) return;
            foreach (var account in touched)
                StorageDiff(u, s, account.Key, OldRoot(s.OldRoot, account.Key), account.Value.NewTrieRoot);
            foreach (var owner in StorageOwnersUnder(start, end).Where(o => !touched.ContainsKey(o)))
                CheckOwnerStorage(u, s, owner, Array.Empty<byte>(), RootReference(u.Root));
        }

        private IDictionary<byte[], AccountRoots> WalkAccounts(FlatUnit u, byte[] start, byte[] end, string range)
        {
            EnterEmit(u);
            var walk = new FlatLeafWalk(_env, u.Queue.Token, dryRun: false, 0);
            var touched = new Dictionary<byte[], AccountRoots>(ByteArrayComparer.Current);
            walk.ReconcileAccountsOnly(u.Root, start, end, touched);
            var result = u.Record(walk, Array.Empty<byte>(), range, "diff");
            lock (u.Gate) u.MissingCode.UnionWith(result.MissingCode);
            return walk.MetMissingNodes ? null : touched;
        }

        private OldStorage OldRoot(byte[] oldRoot, byte[] account)
        {
            if (RootReference(oldRoot) == null) return new OldStorage(true, DefaultValues.EMPTY_TRIE_HASH);
            var oldSide = new MissingNodeRecordingStore(_env.TrieNodes, PreImageOf);
            byte[] leaf;
            try
            {
                leaf = PatriciaTrie.LoadFromStorage(oldRoot, oldSide).Get(account);
            }
            catch (MissingTrieNodeException)
            {
                return new OldStorage(false, null);
            }
            if (oldSide.Missing.Count > 0) return new OldStorage(false, null);
            return new OldStorage(true, leaf == null ? DefaultValues.EMPTY_TRIE_HASH : AccountEncoder.Current.Decode(leaf)?.StateRoot ?? DefaultValues.EMPTY_TRIE_HASH);
        }

        private void StorageDiff(
            FlatUnit u, DiffSides s,
            byte[] owner, OldStorage oldStorage, byte[] newStorageRoot)
        {
            if (!oldStorage.Known)
                EmitWholeOwner(u, owner, newStorageRoot);
            else if (!ByteUtil.AreEqual(oldStorage.StorageRoot, newStorageRoot))
                DiffNode(u, s, owner, newStorageRoot, Array.Empty<byte>(), RootReference(oldStorage.StorageRoot), RootReference(newStorageRoot));
            else
                CheckOwnerStorage(u, s, owner, Array.Empty<byte>(), RootReference(u.Root));
        }

        private bool IsWiped(byte[] owner) => _env.Rocks.Get(CertifyJournal.GenerationCf, CertifyJournal.WipeKey(owner)) != null;

        private void EmitWholeOwner(FlatUnit u, byte[] owner, byte[] storageRoot)
        {
            EnterEmit(u);
            var walk = new FlatLeafWalk(_env, u.Queue.Token, dryRun: false, 0);
            walk.ReconcileOwnerStorage(owner, storageRoot);
            u.Record(walk, owner, "owner", "diff");
        }

        private void EmitSlots(FlatUnit u, byte[] owner, byte[] storageRoot, byte[] p)
        {
            EnterEmit(u);
            var (start, end) = FlatStateWalk.NibbleBounds(p);
            var walk = new FlatLeafWalk(_env, u.Queue.Token, dryRun: false, 0);
            walk.ReconcileStorageSlots(owner, storageRoot, start, end);
            u.Record(walk, owner, p.ToHex(), "diff");
        }

        private void EnterEmit(FlatUnit u)
        {
            if (u.Emitted) return;
            u.Emitted = true;
            CertifyJournal.ClearUnit(_env.Rocks, u.Index);
        }

        private void CheckCapturesUnder(
            FlatUnit u, DiffSides s,
            byte[] owner, byte[] p, byte[] reference, byte[] scope)
        {
            foreach (var q in CapturedPaths(owner, scope))
            {
                var hole = UnreachableOrHole(s, owner, p, reference, q);
                if (hole != null) u.Hole(owner, hole, null);
            }
            if (owner.Length > 0) return;
            var (start, end) = FlatStateWalk.NibbleBounds(scope);
            foreach (var storageOwner in StorageOwnersUnder(start, end))
                CheckOwnerStorage(u, s, storageOwner, p, reference);
        }

        private void CheckOwnerStorage(
            FlatUnit u, DiffSides s,
            byte[] owner, byte[] p, byte[] reference)
        {
            var (_, leaf, hole) = FollowNew(s, Array.Empty<byte>(), p, reference, owner.ConvertToNibbles());
            if (hole != null)
            {
                u.Hole(Array.Empty<byte>(), hole, null);
                return;
            }
            if (leaf == null) return;
            var storageRoot = StorageRootOfLeaf(leaf);
            if (IsWiped(owner))
                EmitWholeOwner(u, owner, storageRoot);
            else
                CheckCapturesUnder(u, s, owner, Array.Empty<byte>(), RootReference(storageRoot), Array.Empty<byte>());
        }

        private byte[] UnreachableOrHole(
            DiffSides s,
            byte[] owner, byte[] p, byte[] reference, byte[] q)
        {
            var (target, _, hole) = FollowNew(s, owner, p, reference, q);
            if (hole != null) return hole;
            if (target is not { Length: 32 }) return null;
            return _proof.VerifiedNode(s.New, owner, q, target) == null ? q : null;
        }

        private (byte[] Reference, Nethereum.RLP.RLPCollection Leaf, byte[] HolePath) FollowNew(
            DiffSides s,
            byte[] owner, byte[] path, byte[] reference, byte[] target)
        {
            while (path.Length < target.Length)
            {
                if (reference is not { Length: 32 }) return (null, null, null);
                var node = _proof.VerifiedNode(s.New, owner, path, reference);
                if (node == null) return (null, null, path);
                var (child, consumed, boundary) = TrieProof.Descend(node, target, path.Length);
                if (boundary != RangeBoundary.Ref)
                {
                    var endsAtTarget = boundary == RangeBoundary.NoBoundary && TrieProof.IsLeafNode(node) && consumed == target.Length - path.Length;
                    return (null, endsAtTarget ? node : null, null);
                }
                path = ByteUtil.Merge(path, target.AsSpan(path.Length, consumed).ToArray());
                reference = child;
            }
            return (reference, null, null);
        }

        private List<byte[]> CapturedPaths(byte[] owner, byte[] scope)
        {
            var prefix = CertifyJournal.CaptureKey(owner.Length > 0, owner, scope);
            var header = owner.Length > 0 ? 2 + owner.Length : 2;
            var paths = new List<byte[]>();
            using var it = _env.Rocks.CreateIterator(CertifyJournal.GenerationCf);
            for (it.Seek(prefix); it.Valid() && it.GetKeySpan().StartsWith(prefix); it.Next())
                paths.Add(it.GetKeySpan().Slice(header).ToArray());
            return paths;
        }

        private List<byte[]> StorageOwnersUnder(byte[] start, byte[] end)
        {
            var owners = new SortedSet<byte[]>(ByteArrayComparer.Current);
            CollectOwners(owners, new[] { CertifyJournal.CaptureTag, CertifyJournal.StorageTrieTag }, start, end);
            CollectOwners(owners, new[] { CertifyJournal.WipeTag }, start, end);
            return owners.ToList();
        }

        private void CollectOwners(SortedSet<byte[]> owners, byte[] header, byte[] start, byte[] end)
        {
            using var it = _env.Rocks.CreateIterator(CertifyJournal.GenerationCf);
            it.Seek(ByteUtil.Merge(header, start));
            while (it.Valid() && it.GetKeySpan().StartsWith(header) && it.GetKeySpan().Length >= header.Length + 32)
            {
                var owner = it.GetKeySpan().Slice(header.Length, 32).ToArray();
                if (end != null && ByteArrayComparer.Current.Compare(owner, end) >= 0) return;
                owners.Add(owner);
                var successor = ByteUtil.PrefixSuccessor(ByteUtil.Merge(header, owner));
                if (successor == null) return;
                it.Seek(successor);
            }
        }


        private void FinishDiff(FlatUnit u)
        {
            if (u.Failed || u.Holes.Count > 0)
            {
                CertifyJournal.ClearUnit(_env.Rocks, u.Index);
                return;
            }
            if (!u.Emitted) return;
            _env.Inventory.MergeMissingCode(u.MissingCode);
            CertifyJournal.MarkUnitDone(_env.Rocks, u.Index);
        }

        private static IEnumerable<ShardResult> DiffResults(FlatUnit u)
            => u.Walks.Append(new ShardResult
            {
                Result = new FlatStateReconcileResult(0, 0, 0, 0, 0, 0, 0, 0)
                {
                    TrieNodesMissing = u.TrieNodesMissing - u.Walks.Sum(w => w.Result.TrieNodesMissing),
                    TrieNodesDiffering = u.TrieNodesDiffering,
                },
                Unresolvable = u.Unresolvable,
                MissingNodes = u.Holes,
                MissingCode = new HashSet<byte[]>(ByteArrayComparer.Current),
            });

        private static string DiffLine(List<FlatUnit> units)
            => $"snap.flat.certify.diff regions={units.Sum(u => u.Walks.Count):N0} " +
               $"keys_walked={units.Sum(u => u.Walks.Sum(w => w.Result.LeavesWalked)):N0} holes={units.Sum(u => u.Holes.Count):N0}";
    }
}
