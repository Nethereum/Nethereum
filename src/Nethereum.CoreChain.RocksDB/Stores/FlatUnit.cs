using System;
using System.Collections.Generic;
using System.Threading;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class FlatUnit
    {
        private readonly UnitContext _ctx;
        private readonly FlatReconcileEnv _env;
        private readonly Action<FlatUnit, NibbleRange> _hashLargeOwnerRange;
        private readonly CancellationToken _ct;
        private readonly byte[] _slot = new byte[32];
        private readonly HashSet<byte[]> _codeSeen = new(ByteArrayComparer.Current);
        private RocksDbSharp.Iterator _trieStorage;
        private int _outstanding = 1;

        public FlatUnit(UnitContext ctx, int index, Action<FlatUnit, NibbleRange> hashLargeOwnerRange)
        {
            _ctx = ctx;
            _env = ctx.Env;
            _hashLargeOwnerRange = hashLargeOwnerRange;
            _ct = ctx.Queue.Token;
            Index = index;
            Shard = index / FlatUnitPass.UnitsPerShard;
            Prefix = new[] { (byte)Shard, (byte)(index % FlatUnitPass.UnitsPerShard) };
            (Start, End) = FlatStateWalk.NibbleBounds(Prefix);
        }

        public int Index { get; }
        public int Shard { get; }
        public byte[] Root => _ctx.Root;
        public byte[] Prefix { get; }
        public byte[] Start { get; }
        public byte[] End { get; }
        public WorkQueue Queue => _ctx.Queue;
        public HashingTally Tally => _ctx.Tally;
        public Action<string> Progress => _ctx.Progress;
        public bool DryRun => _ctx.DryRun;
        public bool Journal => _ctx.Journal;
        public object Gate { get; } = new object();
        public long Accounts { get; private set; }
        public long Slots { get; set; }
        public RangeHash AccountRange { get; private set; }
        public List<OwnerToWalk> OwnersToWalk { get; } = new();
        public List<LargeOwner> LargeOwners { get; } = new();
        public Dictionary<byte[], RangeHash> OwnerHoleCandidates { get; } = new(ByteArrayComparer.Current);
        public HashSet<byte[]> MissingCode { get; } = new(ByteArrayComparer.Current);
        public bool Failed { get; set; }
        public bool Emitted { get; set; }
        public List<(byte[] Owner, byte[] Path)> Holes { get; } = new();
        public long TrieNodesMissing { get; set; }
        public long TrieNodesDiffering { get; set; }
        public List<(byte[] AccountHash, byte[] StorageRoot)> Unresolvable { get; } = new();
        public List<ShardResult> Walks { get; } = new();

        public void Expect() => Interlocked.Increment(ref _outstanding);

        public bool Arrive() => Interlocked.Decrement(ref _outstanding) == 0;

        public long Run()
        {
            using var accounts = _env.Rocks.CreateIterator(RocksDbManager.CF_STATE_ACCOUNTS);
            using var storage = _env.Rocks.CreateIterator(RocksDbManager.CF_STATE_STORAGE);
            using var trieAccounts = _env.Rocks.CreateIterator(RocksDbManager.CF_STATE_TRIE_ACCOUNT);
            using var trieStorage = _env.Rocks.CreateIterator(RocksDbManager.CF_STATE_TRIE_STORAGE);
            _trieStorage = trieStorage;
            AccountRange = new RangeHash(Prefix, trieAccounts, Array.Empty<byte>(), _ct);
            accounts.Seek(Start);
            storage.Seek(FlatStateWalk.OwnerSlotKey(Start, FlatStateWalk.ZeroSlot));
            byte[] accountKey;
            while ((accountKey = FlatStateWalk.NextAccountKey(accounts, End)) != null)
            {
                HashOrphanOwnersBelow(storage, accountKey);
                HashAccount(storage, accountKey, accounts.Value());
                accounts.Next();
            }
            HashOrphanOwnersBelow(storage, End);
            AccountRange.Seal();
            _trieStorage = null;
            return Accounts + Slots;
        }

        private void HashAccount(RocksDbSharp.Iterator storage, byte[] accountKey, byte[] flatValue)
        {
            if ((Accounts & 0xFFF) == 0) _ct.ThrowIfCancellationRequested();
            var account = FlatStateWalk.DecodeFlatAccount(_env.Flat, accountKey, flatValue);
            _env.CollectMissingCode(account, _codeSeen, MissingCode);
            HashOwner(storage, accountKey, account?.StateRoot ?? DefaultValues.EMPTY_TRIE_HASH);
            AccountRange.Add(accountKey, account == null ? null : AccountEncoder.Current.Encode(account));
            Accounts++;
        }

        private void HashOrphanOwnersBelow(RocksDbSharp.Iterator storage, byte[] bound32)
        {
            while (storage.Valid())
            {
                var key = storage.GetKeySpan();
                if (key.Length < 32)
                {
                    storage.Next();
                    continue;
                }
                var owner = key.Slice(0, 32);
                if (bound32 != null && owner.SequenceCompareTo(bound32) >= 0) return;
                HashOwner(storage, owner.ToArray(), DefaultValues.EMPTY_TRIE_HASH);
            }
        }

        private void HashOwner(RocksDbSharp.Iterator storage, byte[] owner, byte[] expectedStorageRoot)
        {
            var hashed = FlatStateWalk.IsStorageOf(storage, owner) ? new RangeHash(Array.Empty<byte>(), _trieStorage, owner, _ct) : null;
            long ownerSlots = 0;
            while (hashed != null && storage.Valid())
            {
                var key = storage.GetKeySpan();
                if (key.Length < 32 || !key.Slice(0, 32).SequenceEqual(owner)) break;
                hashed.AddFlatSlot(key, storage.Value(), _slot);
                ownerSlots++;
                Slots++;
                storage.Next();
                if (ownerSlots < FlatUnitPass.LargeOwnerSlotThreshold) continue;
                BecomeLargeOwner(storage, owner, expectedStorageRoot, hashed.Malformed, ownerSlots);
                return;
            }
            hashed?.Seal();
            RecordHashedOwner(owner, expectedStorageRoot, hashed);
        }

        private void RecordHashedOwner(byte[] owner, byte[] expectedStorageRoot, RangeHash hashed)
        {
            var computed = hashed?.RootHash ?? DefaultValues.EMPTY_TRIE_HASH;
            var malformed = hashed?.Malformed == true;
            if (hashed != null && hashed.HoleCount > 0) OwnerHoleCandidates[owner] = hashed;
            if (!malformed && ByteUtil.AreEqual(computed, expectedStorageRoot)) return;
            OwnersToWalk.Add(new OwnerToWalk
            {
                Owner = owner, ExpectedStorageRoot = expectedStorageRoot, ComputedRoot = computed, Malformed = malformed,
            });
        }

        private void BecomeLargeOwner(
            RocksDbSharp.Iterator storage, byte[] owner, byte[] expectedStorageRoot, bool malformed, long ownerSlots)
        {
            Slots -= ownerSlots;
            var largeOwner = new LargeOwner(owner, expectedStorageRoot, malformed);
            LargeOwners.Add(largeOwner);
            foreach (var range in largeOwner.Ranges)
            {
                Expect();
                Queue.Enqueue(() => _hashLargeOwnerRange(this, range));
            }
            SkipStorageOf(storage, owner);
        }

        private static void SkipStorageOf(RocksDbSharp.Iterator storage, byte[] owner)
        {
            var successor = ByteUtil.PrefixSuccessor(owner);
            if (successor != null)
            {
                storage.Seek(successor);
                return;
            }
            while (FlatStateWalk.IsStorageOf(storage, owner)) storage.Next();
        }

        public ShardResult ToShardResult()
        {
            long gA = 0, gS = 0, aA = 0, aS = 0, pA = 0, pS = 0, ranges = 0, leaves = 0;
            foreach (var walk in Walks)
            {
                gA += walk.Result.GhostAccountsDeleted; gS += walk.Result.GhostSlotsDeleted;
                aA += walk.Result.AccountsAdded; aS += walk.Result.SlotsAdded;
                pA += walk.Result.AccountsPatched; pS += walk.Result.SlotsPatched;
                ranges += walk.Result.RangesWalked; leaves += walk.Result.LeavesWalked;
            }
            return new ShardResult
            {
                Result = new FlatStateReconcileResult(Accounts + aA - gA, Slots + aS - gS, gA, gS, aA, aS, pA, pS)
                {
                    RangesWalked = ranges,
                    LeavesWalked = leaves,
                    TrieNodesMissing = TrieNodesMissing,
                    TrieNodesDiffering = TrieNodesDiffering,
                },
                Unresolvable = Unresolvable,
                MissingNodes = Holes,
                MissingCode = MissingCode,
            };
        }

        public ShardResult Record(FlatLeafWalk walk, byte[] owner, string range, string cause)
        {
            var result = walk.ToShardResult();
            lock (Gate)
            {
                Walks.Add(result);
                Unresolvable.AddRange(result.Unresolvable);
                if (walk.MetMissingNodes) Failed = true;
                foreach (var (missingOwner, path) in result.MissingNodes) ReportHole(missingOwner, path, StoredNode.Missing, null);
            }
            Tally.Walked(result.Result.LeavesWalked);
            if (result.Result.TotalRepairs > 0) Progress?.Invoke(RepairLine(owner, range, cause, result.Result));
            return result;
        }

        private string RepairLine(byte[] owner, string range, string cause, FlatStateReconcileResult r)
            => $"snap.flat.certify.repair unit={Index:X2} owner=0x{owner.ToHex()} range={range} " +
               $"added={r.AccountsAdded}/{r.SlotsAdded} ghost={r.GhostAccountsDeleted}/{r.GhostSlotsDeleted} " +
               $"patched={r.AccountsPatched}/{r.SlotsPatched} cause={cause}";

        public void AdoptHoles(byte[] owner, RangeHash hashed)
        {
            if (hashed.HoleCount == 0) return;
            lock (Gate)
            {
                foreach (var (path, stored, expected) in hashed.Holes) ReportHole(owner, path, stored, expected);
                if (hashed.HoleCount > hashed.Holes.Count) Failed = true;
            }
        }

        private void ReportHole(byte[] owner, byte[] path, StoredNode stored, byte[] expected)
        {
            if (Holes.Count >= FlatStateWalk.MaxMissingPathsPerStore)
            {
                Failed = true;
                return;
            }
            Holes.Add((owner, path));
            if (stored == StoredNode.Differs) TrieNodesDiffering++;
            else TrieNodesMissing++;
            if (Progress != null) Progress(HoleLine(owner, path, expected));
        }

        private string HoleLine(byte[] owner, byte[] path, byte[] expected)
        {
            var raw = (IRawNodeReader)_env.TrieNodes;
            var present = 0;
            for (int depth = 0; depth < path.Length; depth++)
                if (raw.TryGetRawNode(owner, path.AsSpan(0, depth).ToArray()) != null) present++;
            var stored = raw.TryGetRawNode(owner, path);
            var (captured, _) = CertifyJournal.PreImage(_env.Rocks, owner, path);
            var capture = !captured ? "none" : stored == null ? "delete" : "put";
            return $"snap.flat.certify.hole owner=0x{owner.ToHex()} path={path.ToHex()} ancestors_present={present}/{path.Length} " +
                   $"stored_keccak={(stored == null ? "absent" : "0x" + Sha3Keccack.Current.CalculateHash(stored).ToHex())} " +
                   $"expected={(expected == null ? "?" : "0x" + expected.ToHex())} captured={capture}";
        }

        public void Hole(byte[] owner, byte[] path, byte[] expected)
        {
            lock (Gate)
            {
                Failed = true;
                ReportHole(owner, path, StoredNode.Missing, expected);
            }
        }
    }
}
