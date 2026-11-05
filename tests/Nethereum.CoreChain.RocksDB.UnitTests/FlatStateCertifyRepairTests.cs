using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Collection(DiskHeavyCertifyCollection.Name)]
    [Trait("Slice", "Flat")]
    public class FlatStateCertifyRepairTests : IDisposable
    {
        private const int LeafWalkBatchSize = 4_096;

        private readonly List<string> _dirs = new();
        private readonly List<IDisposable> _owned = new();

        public void Dispose()
        {
            foreach (var owned in _owned) owned.Dispose();
            foreach (var dir in _dirs)
                try { Directory.Delete(dir, recursive: true); } catch { }
        }

        private readonly Dictionary<RocksDbChainStoreBundle, string> _bundleDirs = new();

        private RocksDbChainStoreBundle OpenPathKeyedBundle()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"certifyrepair_{Guid.NewGuid():N}");
            _dirs.Add(dir);
            var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: new RocksDbStorageOptions { PathKeyedState = true });
            _owned.Add(bundle);
            _bundleDirs[bundle] = dir;
            return bundle;
        }

        private static byte[] HashOf(string domain, int index)
            => Sha3Keccack.Current.CalculateHash(System.Text.Encoding.ASCII.GetBytes($"{domain}:{index}"));

        private static byte[] SlotValue(int index) => ((BigInteger)(index + 1)).ToByteArray(isUnsigned: true, isBigEndian: true);

        private static Account NewAccount(ulong nonce, ulong balance, byte[] storageRoot) => new Account
        {
            Nonce = nonce,
            Balance = balance,
            StateRoot = storageRoot,
            CodeHash = DefaultValues.EMPTY_DATA_HASH,
        };

        private static int Nibble(byte[] key, int index) => index % 2 == 0 ? key[index / 2] >> 4 : key[index / 2] & 0x0F;

        private static string ColumnChecksum(RocksDbManager rocks, string column)
        {
            var bytes = new List<byte>();
            using var it = rocks.CreateIterator(column);
            for (it.SeekToFirst(); it.Valid(); it.Next())
            {
                bytes.AddRange(it.Key());
                bytes.AddRange(it.Value());
            }
            return Convert.ToHexString(Sha3Keccack.Current.CalculateHash(bytes.ToArray()));
        }

        private static string FlatChecksum(RocksDbManager rocks)
            => ColumnChecksum(rocks, RocksDbManager.CF_STATE_ACCOUNTS) + ColumnChecksum(rocks, RocksDbManager.CF_STATE_STORAGE);

        private static void DeleteStoredNode(RocksDbChainStoreBundle bundle, byte[] owner, byte[] path)
        {
            var raw = (IRawNodeReader)bundle.StateTrieNodes;
            var stored = raw.TryGetRawNode(owner, path);
            Assert.NotNull(stored);
            var set = new TrieNodeSet();
            set.AddDelete(owner, path, stored);
            bundle.StateTrieNodes.Commit(set);
            Assert.Null(raw.TryGetRawNode(owner, path));
        }

        private sealed class HiddenPathStore : ITrieNodeStore
        {
            private readonly ITrieNodeStore _inner;
            public HiddenPathStore(ITrieNodeStore inner) { _inner = inner; }
            public byte[] Get(Node reference) => _inner.Get(reference);
            public bool Contains(Node reference) => _inner.Contains(reference);
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
            public void Commit(TrieNodeSet nodes) => _inner.Commit(nodes);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
        }

        private sealed class CountingPathNodeStore : ITrieNodeStore, IRawNodeReader, IContractStorageWipeable
        {
            private readonly ITrieNodeStore _inner;
            private long _gets;
            public CountingPathNodeStore(ITrieNodeStore inner) { _inner = inner; }
            public long Gets => Interlocked.Read(ref _gets);
            public byte[] Get(Node reference) { Interlocked.Increment(ref _gets); return _inner.Get(reference); }
            public bool Contains(Node reference) => _inner.Contains(reference);
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
            public void Commit(TrieNodeSet nodes) => _inner.Commit(nodes);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
            public byte[] TryGetRawNode(byte[] owner, byte[] path) => ((IRawNodeReader)_inner).TryGetRawNode(owner, path);
            public void DeleteRange(byte[] owner) => ((IContractStorageWipeable)_inner).DeleteRange(owner);
        }

        private static FlatStateReconciler Oracle(RocksDbChainStoreBundle bundle)
            => new FlatStateReconciler(bundle.Rocks, new RocksDbStateStore(bundle.Rocks), new HiddenPathStore(bundle.StateTrieNodes));

        private sealed class Model
        {
            public SortedDictionary<byte[], Account> Accounts { get; } = new(ByteArrayComparer.Current);
            public Dictionary<byte[], SortedDictionary<byte[], byte[]>> Storage { get; } = new(ByteArrayComparer.Current);
            public byte[] Root { get; private set; }

            public static Model Random(int seed, int eoas, int contracts, int maxSlots)
            {
                var random = new Random(seed);
                var model = new Model();
                for (int i = 0; i < eoas; i++)
                    model.Accounts[HashOf($"eoa-{seed}", i)] = NewAccount((ulong)i, (ulong)random.Next(1, 1_000_000), DefaultValues.EMPTY_TRIE_HASH);
                for (int c = 0; c < contracts; c++)
                {
                    var owner = HashOf($"contract-{seed}", c);
                    var slots = new SortedDictionary<byte[], byte[]>(ByteArrayComparer.Current);
                    var count = 1 + random.Next(maxSlots);
                    for (int s = 0; s < count; s++) slots[HashOf($"slot-{seed}-{c}", s)] = SlotValue(random.Next(1_000_000));
                    model.Storage[owner] = slots;
                    model.Accounts[owner] = NewAccount(1, 1, StorageRootOf(slots));
                }
                model.Seal();
                return model;
            }

            public static byte[] StorageRootOf(SortedDictionary<byte[], byte[]> slots)
            {
                if (slots.Count == 0) return DefaultValues.EMPTY_TRIE_HASH;
                var trie = new StackTrie();
                foreach (var slot in slots) trie.Update(slot.Key, RLP.RLP.EncodeElement(slot.Value));
                return trie.Hash();
            }

            public void Seal()
            {
                var trie = new StackTrie();
                foreach (var account in Accounts) trie.Update(account.Key, new AccountEncoder().Encode(account.Value));
                Root = trie.Hash();
            }

            public async Task SeedAsync(RocksDbChainStoreBundle bundle)
            {
                var flat = new RocksDbStateStore(bundle.Rocks);
                foreach (var account in Accounts) await flat.SaveAccountByHashAsync(account.Key, account.Value);
                foreach (var owner in Storage)
                    foreach (var slot in owner.Value)
                        await flat.SaveStorageByHashAsync(owner.Key, slot.Key, slot.Value);
                await bundle.GenerateTrieFromFlatAsync(Root, null, CancellationToken.None);
            }
        }

        private static async Task<(RocksDbChainStoreBundle Bundle, Model Model)> SeededAsync(Func<RocksDbChainStoreBundle> open, int seed, int eoas, int contracts, int maxSlots)
        {
            var bundle = open();
            var model = Model.Random(seed, eoas, contracts, maxSlots);
            await model.SeedAsync(bundle);
            return (bundle, model);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Given_OneGhostSlotInsideA50kSlotOwner_When_Reconciled_Then_OnlyThatOwnerIsWalkedAndTheGhostIsDeleted(bool withGhost)
        {
            const int slotCount = 50_000;
            var bundle = OpenPathKeyedBundle();
            var model = new Model();
            var owner = HashOf("r2-owner", 0);
            var slots = new SortedDictionary<byte[], byte[]>(ByteArrayComparer.Current);
            for (int i = 0; i < slotCount; i++) slots[HashOf("r2-slot", i)] = SlotValue(i);
            model.Storage[owner] = slots;
            model.Accounts[owner] = NewAccount(1, 1, Model.StorageRootOf(slots));
            for (int i = 0; i < 400; i++) model.Accounts[HashOf("r2-eoa", i)] = NewAccount((ulong)i, 1, DefaultValues.EMPTY_TRIE_HASH);
            model.Seal();
            await model.SeedAsync(bundle);
            var ghostRow = owner.Concat(HashOf("r2-ghost", 0)).ToArray();
            if (withGhost) bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, ghostRow, new byte[] { 0x07 });

            var result = await bundle.ReconcileFlatStateAsync(model.Root, null, CancellationToken.None);

            Assert.Equal(withGhost ? 1 : 0, result.GhostSlotsDeleted);
            Assert.Equal(withGhost ? 1 : 0, result.TotalRepairs);
            Assert.Equal(slotCount, result.SlotsScanned);
            if (withGhost)
            {
                Assert.Equal(slotCount, result.LeavesWalked);
                Assert.Equal(1, result.RangesWalked);
                Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, ghostRow));
            }
            else
            {
                Assert.Equal(0, result.LeavesWalked);
                Assert.Equal(0, result.RangesWalked);
            }
        }

        private static void ApplyMixedDivergence(RocksDbChainStoreBundle bundle, Model model)
        {
            var flat = new RocksDbStateStore(bundle.Rocks);
            var eoas = model.Accounts.Where(a => !model.Storage.ContainsKey(a.Key)).Select(a => a.Key).ToList();
            var contracts = model.Storage.Keys.OrderBy(k => k, ByteArrayComparer.Current).ToList();
            foreach (var shard in Enumerable.Range(0, 16))
            {
                var inShard = eoas.Where(k => Nibble(k, 0) == shard).ToList();
                if (inShard.Count < 3) continue;
                flat.SaveAccountByHashAsync(inShard[0], NewAccount(77, 77, DefaultValues.EMPTY_TRIE_HASH)).GetAwaiter().GetResult();
                bundle.Rocks.Delete(RocksDbManager.CF_STATE_ACCOUNTS, inShard[1]);
                var ghost = (byte[])inShard[2].Clone();
                ghost[31] ^= 0xFF;
                flat.SaveAccountByHashAsync(ghost, NewAccount(5, 5, DefaultValues.EMPTY_TRIE_HASH)).GetAwaiter().GetResult();
                if (shard % 4 == 0) flat.SaveStorageByHashAsync(ghost, HashOf("ghost-account-slot", shard), new byte[] { 0x09 }).GetAwaiter().GetResult();
            }
            for (int c = 0; c < contracts.Count; c++)
            {
                var owner = contracts[c];
                var slots = model.Storage[owner].Keys.ToList();
                switch (c % 4)
                {
                    case 0:
                        bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, owner.Concat(slots[0]).ToArray(), new byte[] { 0xEE });
                        break;
                    case 1:
                        bundle.Rocks.Delete(RocksDbManager.CF_STATE_STORAGE, owner.Concat(slots[slots.Count - 1]).ToArray());
                        break;
                    case 2:
                        bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, owner.Concat(HashOf("ghost-slot", c)).ToArray(), new byte[] { 0x01 });
                        break;
                }
            }
        }

        [Fact]
        public async Task Given_GhostAddedAndPatchedAccountsAndSlotsAcrossAllShards_When_Reconciled_Then_TheSplitEqualsTheFullWalkOracleOnACopy()
        {
            var (certified, model) = await SeededAsync(OpenPathKeyedBundle, 31, 600, 24, 40);
            var (oracleCopy, _) = await SeededAsync(OpenPathKeyedBundle, 31, 600, 24, 40);
            ApplyMixedDivergence(certified, model);
            ApplyMixedDivergence(oracleCopy, model);
            Assert.Equal(FlatChecksum(certified.Rocks), FlatChecksum(oracleCopy.Rocks));

            var byHash = await certified.ReconcileFlatStateAsync(model.Root, null, CancellationToken.None);
            var byWalk = await Oracle(oracleCopy).ReconcileFlatStateAsync(model.Root, null, CancellationToken.None);

            Assert.True(byHash.TotalRepairs > 40);
            Assert.True(byHash.GhostAccountsDeleted > 0 && byHash.AccountsAdded > 0 && byHash.AccountsPatched > 0);
            Assert.True(byHash.GhostSlotsDeleted > 0 && byHash.SlotsAdded > 0 && byHash.SlotsPatched > 0);
            Assert.Equal(byWalk.GhostAccountsDeleted, byHash.GhostAccountsDeleted);
            Assert.Equal(byWalk.GhostSlotsDeleted, byHash.GhostSlotsDeleted);
            Assert.Equal(byWalk.AccountsAdded, byHash.AccountsAdded);
            Assert.Equal(byWalk.SlotsAdded, byHash.SlotsAdded);
            Assert.Equal(byWalk.AccountsPatched, byHash.AccountsPatched);
            Assert.Equal(byWalk.SlotsPatched, byHash.SlotsPatched);
            Assert.Equal(byWalk.AccountsScanned, byHash.AccountsScanned);
            Assert.Equal(byWalk.SlotsScanned, byHash.SlotsScanned);
            Assert.Equal(FlatChecksum(oracleCopy.Rocks), FlatChecksum(certified.Rocks));
            Assert.True(byHash.LeavesWalked < byWalk.AccountsScanned + byWalk.SlotsScanned);
        }

        [Fact]
        public async Task Given_AFlatAccountWithAStaleStorageRootButCurrentSlots_When_Reconciled_Then_TheAccountIsPatchedAndItsOwnerIsSettledAgainstTheTrieRoot()
        {
            var (bundle, model) = await SeededAsync(OpenPathKeyedBundle, 41, 300, 6, 60);
            var owner = model.Storage.Keys.First();
            var stale = NewAccount(1, 1, HashOf("stale-root", 0));
            await new RocksDbStateStore(bundle.Rocks).SaveAccountByHashAsync(owner, stale);
            var lines = new ConcurrentQueue<string>();

            var result = await bundle.ReconcileFlatStateAsync(model.Root, lines.Enqueue, CancellationToken.None);

            Assert.Equal(1, result.AccountsPatched);
            Assert.Equal(1, result.TotalRepairs);
            Assert.Equal(model.Accounts[owner].StateRoot, (await new RocksDbStateStore(bundle.Rocks).GetAccountByHashAsync(owner)).StateRoot);
            var again = await bundle.VerifyFlatStateAsync(model.Root, null, CancellationToken.None);
            Assert.Equal(0, again.TotalRepairs);
            Assert.Equal(0, again.RangesWalked);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Given_StorageRowsWhoseAccountRowIsMissingFromFlatButPresentInTheTrie_When_Reconciled_Then_TheAccountIsAddedAndTheRowsAreKept(bool accountInTrie)
        {
            var model = Model.Random(51, 300, 6, 60);
            var owner = model.Storage.Keys.First();
            var rows = model.Storage[owner].Keys.Select(slot => owner.Concat(slot).ToArray()).ToList();
            var bundle = OpenPathKeyedBundle();
            if (accountInTrie)
            {
                await model.SeedAsync(bundle);
                bundle.Rocks.Delete(RocksDbManager.CF_STATE_ACCOUNTS, owner);
            }
            else
            {
                var ownerSlots = model.Storage[owner];
                model.Accounts.Remove(owner);
                model.Storage.Remove(owner);
                model.Seal();
                await model.SeedAsync(bundle);
                foreach (var slot in ownerSlots)
                    bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, owner.Concat(slot.Key).ToArray(), slot.Value);
            }
            var root = model.Root;

            var result = await bundle.ReconcileFlatStateAsync(root, null, CancellationToken.None);

            if (accountInTrie)
            {
                Assert.Equal(1, result.AccountsAdded);
                Assert.Equal(1, result.TotalRepairs);
                Assert.All(rows, row => Assert.NotNull(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, row)));
            }
            else
            {
                Assert.Equal(rows.Count, result.GhostSlotsDeleted);
                Assert.Equal(rows.Count, result.TotalRepairs);
                Assert.All(rows, row => Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, row)));
            }
        }

        [Fact]
        public async Task Given_AMissingAccountTrieInteriorNodeUnderAMatchingFlat_When_Reconciled_Then_ItThrowsUnresolvableForOwnerEmptyWithTheExactPath()
        {
            var (bundle, model) = await SeededAsync(OpenPathKeyedBundle, 61, 2_000, 0, 0);
            byte[] holePath = null;
            using (var it = bundle.Rocks.CreateIterator(RocksDbManager.CF_STATE_TRIE_ACCOUNT))
                for (it.Seek(new byte[] { 0x7 }); it.Valid() && holePath == null; it.Next())
                    if (it.Key().Length == 3) holePath = it.Key();
            Assert.NotNull(holePath);
            DeleteStoredNode(bundle, Array.Empty<byte>(), holePath);
            var before = FlatChecksum(bundle.Rocks);
            var counting = new CountingPathNodeStore(bundle.StateTrieNodes);
            var reconciler = new FlatStateReconciler(bundle.Rocks, new RocksDbStateStore(bundle.Rocks), counting);

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => reconciler.ReconcileFlatStateAsync(model.Root, null, CancellationToken.None));

            var missing = Assert.Single(thrown.MissingNodes);
            Assert.Empty(missing.Owner);
            Assert.Equal(holePath, missing.Path);
            Assert.Empty(thrown.Subtrees);
            Assert.InRange(counting.Gets, 1, 2 * 256);
            Assert.Equal(before, FlatChecksum(bundle.Rocks));
        }

        [Fact]
        public async Task Given_RepairsWereApplied_When_Certified_Then_AFreshVerifyFindsNothing()
        {
            var (bundle, model) = await SeededAsync(OpenPathKeyedBundle, 71, 600, 24, 40);
            ApplyMixedDivergence(bundle, model);

            var result = await bundle.ReconcileFlatStateAsync(model.Root, null, CancellationToken.None);

            Assert.True(result.TotalRepairs > 0);
            var again = await bundle.VerifyFlatStateAsync(model.Root, null, CancellationToken.None);
            Assert.Equal(0, again.TotalRepairs);
            Assert.Equal(0, again.RangesWalked);
            var oracle = await Oracle(bundle).VerifyFlatStateAsync(model.Root, null, CancellationToken.None);
            Assert.Equal(0, oracle.TotalRepairs);
        }

        [Fact]
        public async Task Given_AOneSlotOwnerWithADivergedSlot_When_Reconciled_Then_ItsSingleLeafIsWalked()
        {
            var bundle = OpenPathKeyedBundle();
            var model = new Model();
            var owner = HashOf("r20-owner", 0);
            var slot = HashOf("r20-slot", 0);
            model.Storage[owner] = new SortedDictionary<byte[], byte[]>(ByteArrayComparer.Current) { [slot] = SlotValue(41) };
            model.Accounts[owner] = NewAccount(1, 1, Model.StorageRootOf(model.Storage[owner]));
            for (int i = 0; i < 400; i++) model.Accounts[HashOf("r20-eoa", i)] = NewAccount((ulong)i, 1, DefaultValues.EMPTY_TRIE_HASH);
            model.Seal();
            await model.SeedAsync(bundle);
            var row = owner.Concat(slot).ToArray();
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, row, new byte[] { 0xEE });

            var result = await bundle.ReconcileFlatStateAsync(model.Root, null, CancellationToken.None);

            Assert.Equal(1, result.SlotsPatched);
            Assert.Equal(1, result.TotalRepairs);
            Assert.Equal(1, result.RangesWalked);
            Assert.Equal(1, result.LeavesWalked);
            Assert.Equal(SlotValue(41), bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, row));
        }

        [Fact]
        public async Task Given_AStoredStorageNodeWithDifferentBytesInsideADivergedRange_When_Reconciled_Then_ItsPathIsRecordedAsUnresolvableAndTheRangeWritesNothing()
        {
            var bundle = OpenPathKeyedBundle();
            var model = new Model();
            var owner = HashOf("r8-owner", 0);
            var slots = new SortedDictionary<byte[], byte[]>(ByteArrayComparer.Current);
            for (int i = 0; i < 300; i++) slots[HashOf("r8-slot", i)] = SlotValue(i);
            model.Storage[owner] = slots;
            model.Accounts[owner] = NewAccount(1, 1, Model.StorageRootOf(slots));
            model.Accounts[HashOf("r8-eoa", 0)] = NewAccount(1, 1, DefaultValues.EMPTY_TRIE_HASH);
            model.Seal();
            await model.SeedAsync(bundle);
            var nibble = Nibble(slots.Keys.First(), 0);
            var flippedPath = new[] { (byte)nibble };
            var stored = bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, owner.Concat(flippedPath).ToArray());
            stored[stored.Length - 1] ^= 0x01;
            bundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_STORAGE, owner.Concat(flippedPath).ToArray(), stored);
            var divergedRow = owner.Concat(slots.Keys.First()).ToArray();
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, divergedRow, new byte[] { 0xEE });

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(model.Root, null, CancellationToken.None));

            Assert.Contains(thrown.MissingNodes, m => m.Owner.SequenceEqual(owner) && m.Path.SequenceEqual(flippedPath));
            Assert.Contains(thrown.Subtrees, s => s.AccountHash.SequenceEqual(owner));
            Assert.Equal(new byte[] { 0xEE }, bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, divergedRow));
        }

        private static byte[] UnderShardZero(byte[] key)
        {
            var copy = (byte[])key.Clone();
            copy[0] &= 0x0F;
            return copy;
        }

        [Fact]
        public async Task Given_MoreThanAMillionOrphanOwnersInOneShard_When_Reconciled_Then_EveryOrphanOwnerIsDrained()
        {
            const int orphans = 1_000_001;
            var bundle = OpenPathKeyedBundle();
            var rocks = bundle.Rocks;
            var flatStorage = rocks.GetColumnFamily(RocksDbManager.CF_STATE_STORAGE);
            var batch = rocks.CreateWriteBatch();
            for (int i = 0; i < orphans; i++)
            {
                batch.Put(UnderShardZero(HashOf("orphan-owner", i)).Concat(HashOf("orphan-slot", 0)).ToArray(), new byte[] { 0x01 }, flatStorage);
                if (i % 20_000 != 19_999) continue;
                rocks.Write(batch);
                batch.Dispose();
                batch = rocks.CreateWriteBatch();
            }
            rocks.Write(batch);
            batch.Dispose();
            var lines = new ConcurrentQueue<string>();

            var result = await bundle.ReconcileFlatStateAsync(DefaultValues.EMPTY_TRIE_HASH, lines.Enqueue, CancellationToken.None);

            Assert.Equal(orphans, result.GhostSlotsDeleted);
            Assert.Equal(orphans, result.RangesWalked);
            using var it = rocks.CreateIterator(RocksDbManager.CF_STATE_STORAGE);
            it.SeekToFirst();
            Assert.False(it.Valid());
        }

        public static IEnumerable<object[]> MutationSeeds() => Enumerable.Range(1, 20).Select(seed => new object[] { seed });

        [Theory]
        [MemberData(nameof(MutationSeeds))]
        public async Task Given_FiveHundredSeededMutationsOfFlatAndTrieHolesPerSeed_When_ReconciledThenCheckedByTheOracle_Then_ZeroDiffsOrFailedClosed(int seed)
        {
            const int rounds = 5;
            const int mutationsPerRound = 100;
            var random = new Random(seed);
            var model = Model.Random(seed, 300, 20, 30);
            var holeAndDiffRounds = 0;
            var failedClosed = 0;
            for (int round = 0; round < rounds; round++)
            {
                var bundle = OpenPathKeyedBundle();
                await model.SeedAsync(bundle);
                var flat = new RocksDbStateStore(bundle.Rocks);
                var accountKeys = model.Accounts.Keys.ToList();
                var owners = model.Storage.Keys.ToList();
                var withHole = round % 2 == 0;
                for (int m = 0; m < mutationsPerRound; m++)
                {
                    var account = accountKeys[random.Next(accountKeys.Count)];
                    var owner = owners[random.Next(owners.Count)];
                    var slots = model.Storage[owner].Keys.ToList();
                    switch (random.Next(8))
                    {
                        case 0: await flat.SaveAccountByHashAsync(account, NewAccount((ulong)random.Next(), 3, DefaultValues.EMPTY_TRIE_HASH)); break;
                        case 1: bundle.Rocks.Delete(RocksDbManager.CF_STATE_ACCOUNTS, account); break;
                        case 2: await flat.SaveAccountByHashAsync(HashOf($"ghost-{seed}-{round}", m), NewAccount(1, 1, DefaultValues.EMPTY_TRIE_HASH)); break;
                        case 3: bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, owner.Concat(slots[random.Next(slots.Count)]).ToArray(), new byte[] { (byte)(1 + random.Next(250)) }); break;
                        case 4: bundle.Rocks.Delete(RocksDbManager.CF_STATE_STORAGE, owner.Concat(slots[random.Next(slots.Count)]).ToArray()); break;
                        case 5: bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, owner.Concat(HashOf($"ghost-slot-{seed}-{round}", m)).ToArray(), new byte[] { 0x02 }); break;
                        case 6: bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, HashOf($"orphan-{seed}-{round}", m).Concat(HashOf("orphan-slot", m)).ToArray(), new byte[] { 0x03 }); break;
                        default: await flat.SaveAccountByHashAsync(owner, NewAccount(1, 1, HashOf($"stale-{seed}-{round}", m))); break;
                    }
                }
                if (withHole)
                {
                    var column = random.Next(2) == 0 ? RocksDbManager.CF_STATE_TRIE_ACCOUNT : RocksDbManager.CF_STATE_TRIE_STORAGE;
                    var nodes = new List<byte[]>();
                    using (var it = bundle.Rocks.CreateIterator(column))
                        for (it.SeekToFirst(); it.Valid(); it.Next()) nodes.Add(it.Key());
                    bundle.Rocks.Delete(column, nodes[random.Next(nodes.Count)]);
                    holeAndDiffRounds++;
                }

                FlatStateReconcileResult reconciled = null;
                try
                {
                    reconciled = await bundle.ReconcileFlatStateAsync(model.Root, null, CancellationToken.None);
                }
                catch (FlatReconcileUnresolvableNodeException)
                {
                    failedClosed++;
                }

                if (reconciled == null)
                {
                    Assert.True(withHole, $"seed {seed} round {round}: reconcile failed closed without a trie hole");
                    continue;
                }
                Assert.True(reconciled.TotalRepairs > 0);
                var oracle = await Oracle(bundle).VerifyFlatStateAsync(model.Root, null, CancellationToken.None);
                Assert.Equal(0, oracle.TotalRepairs);
            }
            Assert.True(holeAndDiffRounds > 0);
            Assert.Equal(holeAndDiffRounds, failedClosed);
        }
            private sealed class DisposedOnReadPathStore : ITrieNodeStore, IRawNodeReader, IContractStorageWipeable
        {
            private readonly ITrieNodeStore _inner;
            public DisposedOnReadPathStore(ITrieNodeStore inner) { _inner = inner; }
            public byte[] Get(Node reference) => throw new ObjectDisposedException("trie node store");
            public bool Contains(Node reference) => _inner.Contains(reference);
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
            public void Commit(TrieNodeSet nodes) => _inner.Commit(nodes);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
            public byte[] TryGetRawNode(byte[] owner, byte[] path) => ((IRawNodeReader)_inner).TryGetRawNode(owner, path);
            public void DeleteRange(byte[] owner) => ((IContractStorageWipeable)_inner).DeleteRange(owner);
        }

        [Fact]
        public async Task Given_ATrieStoreDisposedWhileCertifyReadsANode_When_Reconciled_Then_TheDisposalSurfacesAndNothingIsReportedAsAHole()
        {
            var (bundle, model) = await SeededAsync(OpenPathKeyedBundle, 1301, 300, 6, 60);
            var owner = model.Storage.Keys.First();
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, owner.Concat(model.Storage[owner].Keys.First()).ToArray(), new byte[] { 0xEE });
            var reconciler = new FlatStateReconciler(bundle.Rocks, new RocksDbStateStore(bundle.Rocks), new DisposedOnReadPathStore(bundle.StateTrieNodes));

            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => reconciler.ReconcileFlatStateAsync(model.Root, null, CancellationToken.None));
        }

        [Fact]
        public async Task Given_AHoleMetOnlyByAnOwnerWalk_When_Certified_Then_TheFinalLineCountsItAsMissing()
        {
            var bundle = OpenPathKeyedBundle();
            var model = new Model();
            var owner = HashOf("f4-owner", 0);
            var slots = new SortedDictionary<byte[], byte[]>(ByteArrayComparer.Current);
            for (int i = 0; i < 300; i++) slots[HashOf("f4-slot", i)] = SlotValue(i);
            model.Storage[owner] = slots;
            model.Accounts[owner] = NewAccount(1, 1, Model.StorageRootOf(slots));
            model.Accounts[HashOf("f4-eoa", 0)] = NewAccount(1, 1, DefaultValues.EMPTY_TRIE_HASH);
            model.Seal();
            await model.SeedAsync(bundle);
            var first = slots.Keys.First();
            var holePath = new[] { (byte)Nibble(first, 0) };
            DeleteStoredNode(bundle, owner, holePath);
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, owner.Concat(first).ToArray(), new byte[] { 0xEE });
            var lines = new ConcurrentQueue<string>();

            await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(model.Root, lines.Enqueue, CancellationToken.None));

            Assert.Contains(lines, line => line.StartsWith("certify.A shard=", StringComparison.Ordinal) && line.Contains(" trie_missing=0 "));
            var done = Assert.Single(lines, line => line.StartsWith("snap.flat.certify done ", StringComparison.Ordinal));
            Assert.Contains(" trie_missing=1 ", done);
        }

        public static IEnumerable<object[]> FiveSeeds() => Enumerable.Range(1, 5).Select(seed => new object[] { seed });

        [Theory]
        [MemberData(nameof(FiveSeeds))]
        public async Task Given_AnUndecodableAccountRowAndAMissingAccountTrieNodeInTheSameShard_When_Reconciled_Then_ItThrowsUnresolvableWithThatPath(int seed)
        {
            var (bundle, model) = await SeededAsync(OpenPathKeyedBundle, 900 + seed, 80 + seed * 7, 0, 0);
            var random = new Random(seed);
            var stored = new List<byte[]>();
            using (var it = bundle.Rocks.CreateIterator(RocksDbManager.CF_STATE_TRIE_ACCOUNT))
                for (it.SeekToFirst(); it.Valid(); it.Next())
                    if (it.Key().Length >= 1) stored.Add(it.Key());
            var holePath = stored[random.Next(stored.Count)];
            var inShard = model.Accounts.Keys.Where(k => k[0] >> 4 == holePath[0]).ToList();
            var undecodable = inShard[random.Next(inShard.Count)];
            bundle.Rocks.Put(RocksDbManager.CF_STATE_ACCOUNTS, undecodable, new byte[] { 0x01, 0x02, 0x03 });
            DeleteStoredNode(bundle, Array.Empty<byte>(), holePath);
            var elsewhere = model.Accounts.Keys.First(k => k[0] >> 4 != holePath[0]);
            await new RocksDbStateStore(bundle.Rocks).SaveAccountByHashAsync(elsewhere, NewAccount(4242, 1, DefaultValues.EMPTY_TRIE_HASH));

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(model.Root, null, CancellationToken.None));

            Assert.Contains(thrown.MissingNodes, m => m.Owner.Length == 0 && m.Path.SequenceEqual(holePath));
        }

        private static Model SinglePartitionModel(int seed)
        {
            var model = new Model();
            for (int i = 0; i < 40; i++)
            {
                var key = HashOf($"single-{seed}", i);
                key[0] = (byte)(0x50 | (key[0] & 0x0F));
                model.Accounts[key] = NewAccount((ulong)i, 1, DefaultValues.EMPTY_TRIE_HASH);
            }
            model.Seal();
            return model;
        }

        private static void DivergeSinglePartition(RocksDbChainStoreBundle bundle, Model model)
        {
            var keys = model.Accounts.Keys.ToList();
            bundle.Rocks.Put(RocksDbManager.CF_STATE_ACCOUNTS, keys[3], new byte[] { 0x01, 0x02, 0x03 });
            var flat = new RocksDbStateStore(bundle.Rocks);
            flat.SaveAccountByHashAsync(keys[7], NewAccount(99, 99, DefaultValues.EMPTY_TRIE_HASH)).GetAwaiter().GetResult();
            var ghostInPartition = (byte[])keys[11].Clone();
            ghostInPartition[31] ^= 0xFF;
            flat.SaveAccountByHashAsync(ghostInPartition, NewAccount(1, 1, DefaultValues.EMPTY_TRIE_HASH)).GetAwaiter().GetResult();
            var ghostElsewhere = HashOf("single-ghost", 0);
            ghostElsewhere[0] = 0x9C;
            flat.SaveAccountByHashAsync(ghostElsewhere, NewAccount(1, 1, DefaultValues.EMPTY_TRIE_HASH)).GetAwaiter().GetResult();
            flat.SaveStorageByHashAsync(ghostElsewhere, HashOf("single-ghost-slot", 0), new byte[] { 0x05 }).GetAwaiter().GetResult();
            bundle.Rocks.Delete(RocksDbManager.CF_STATE_ACCOUNTS, keys[20]);
        }

        [Fact]
        public async Task Given_ASinglePartitionStoreWithAnUndecodableRowAndGhosts_When_ReconciledRepeatedly_Then_EveryRunsSplitEqualsTheLeafWalkOracle()
        {
            for (int run = 0; run < 12; run++)
            {
                var model = SinglePartitionModel(run % 3);
                var certified = OpenPathKeyedBundle();
                var oracleCopy = OpenPathKeyedBundle();
                await model.SeedAsync(certified);
                await model.SeedAsync(oracleCopy);
                DivergeSinglePartition(certified, model);
                DivergeSinglePartition(oracleCopy, model);

                var byHash = await certified.ReconcileFlatStateAsync(model.Root, null, CancellationToken.None);
                var byWalk = await Oracle(oracleCopy).ReconcileFlatStateAsync(model.Root, null, CancellationToken.None);

                Assert.True(byWalk.TotalRepairs >= 5);
                Assert.Equal(byWalk.GhostAccountsDeleted, byHash.GhostAccountsDeleted);
                Assert.Equal(byWalk.GhostSlotsDeleted, byHash.GhostSlotsDeleted);
                Assert.Equal(byWalk.AccountsAdded, byHash.AccountsAdded);
                Assert.Equal(byWalk.AccountsPatched, byHash.AccountsPatched);
                Assert.Equal(byWalk.AccountsScanned, byHash.AccountsScanned);
                Assert.Equal(byWalk.SlotsScanned, byHash.SlotsScanned);
                Assert.Equal(FlatChecksum(oracleCopy.Rocks), FlatChecksum(certified.Rocks));
                ReleaseBundles(certified, oracleCopy);
            }
        }

        private void ReleaseBundles(params RocksDbChainStoreBundle[] bundles)
        {
            foreach (var bundle in bundles)
            {
                bundle.Dispose();
                _owned.Remove(bundle);
                try { Directory.Delete(_bundleDirs[bundle], recursive: true); } catch { }
            }
        }

        private sealed class DescentScaleState
        {
            public SortedDictionary<byte[], Account> Accounts { get; } = new(ByteArrayComparer.Current);
            public Dictionary<byte[], SortedDictionary<byte[], byte[]>> Storage { get; } = new(ByteArrayComparer.Current);
            public byte[] Root { get; private set; }
            public byte[] WideOwner { get; private set; }
            public byte[] ExtensionOwner { get; private set; }
            public byte[] LargeOwner { get; private set; }

            public static DescentScaleState Create(int seed)
            {
                var state = new DescentScaleState();
                for (int i = 0; i < 6_000; i++)
                {
                    var key = HashOf($"wide-partition-{seed}", i);
                    key[0] = 0x3A;
                    state.Accounts[key] = NewAccount((ulong)i, 1, DefaultValues.EMPTY_TRIE_HASH);
                }
                for (int i = 0; i < 400; i++)
                    state.Accounts[HashOf($"spread-{seed}", i)] = NewAccount((ulong)i, 2, DefaultValues.EMPTY_TRIE_HASH);
                state.WideOwner = state.AddOwner($"wide-owner-{seed}", 5_000, slot => slot);
                state.ExtensionOwner = state.AddOwner($"extension-owner-{seed}", 12_000, slot => { slot[0] = 0x5C; return slot; });
                state.LargeOwner = state.AddOwner($"large-owner-{seed}", 70_000, slot => slot);
                return state;
            }

            private byte[] AddOwner(string domain, int slotCount, Func<byte[], byte[]> place)
            {
                var owner = HashOf(domain, 0);
                var slots = new SortedDictionary<byte[], byte[]>(ByteArrayComparer.Current);
                for (int i = 0; i < slotCount; i++) slots[place(HashOf(domain + "-slot", i))] = SlotValue(i);
                Storage[owner] = slots;
                Accounts[owner] = NewAccount(1, 1, Model.StorageRootOf(slots));
                return owner;
            }

            public void Write(RocksDbChainStoreBundle bundle)
            {
                var rocks = bundle.Rocks;
                var flatAccounts = rocks.GetColumnFamily(RocksDbManager.CF_STATE_ACCOUNTS);
                var flatStorage = rocks.GetColumnFamily(RocksDbManager.CF_STATE_STORAGE);
                var trieAccounts = rocks.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_ACCOUNT);
                var trieStorage = rocks.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_STORAGE);
                var batch = rocks.CreateWriteBatch();
                var pending = 0;
                void Put(byte[] key, byte[] value, RocksDbSharp.ColumnFamilyHandle cf)
                {
                    batch.Put(key, value, cf);
                    if (++pending < 20_000) return;
                    rocks.Write(batch);
                    batch.Dispose();
                    batch = rocks.CreateWriteBatch();
                    pending = 0;
                }
                foreach (var owner in Storage)
                {
                    var trie = new StackTrie(null, (path, blob) => Put(owner.Key.Concat(path).ToArray(), blob, trieStorage));
                    foreach (var slot in owner.Value)
                    {
                        Put(owner.Key.Concat(slot.Key).ToArray(), slot.Value, flatStorage);
                        trie.Update(slot.Key, RLP.RLP.EncodeElement(slot.Value));
                    }
                    trie.Hash();
                }
                var accountTrie = new StackTrie(null, (path, blob) => Put(path, blob, trieAccounts));
                foreach (var account in Accounts)
                {
                    var encoded = new AccountEncoder().Encode(account.Value);
                    Put(account.Key, new byte[20].Concat(encoded).ToArray(), flatAccounts);
                    accountTrie.Update(account.Key, encoded);
                }
                Root = accountTrie.Hash();
                rocks.Write(batch);
                batch.Dispose();
            }
        }

        private static void InjectDescentScaleDefects(RocksDbChainStoreBundle bundle, DescentScaleState state, int seed, int round)
        {
            var random = new Random(seed * 101 + round);
            var flat = new RocksDbStateStore(bundle.Rocks);
            var wide = state.Accounts.Keys.Where(k => k[0] == 0x3A).ToList();
            var start = random.Next(wide.Count - 600);
            for (int i = start; i < start + 500; i++) bundle.Rocks.Delete(RocksDbManager.CF_STATE_ACCOUNTS, wide[i]);
            flat.SaveAccountByHashAsync(wide[random.Next(wide.Count)], NewAccount(7, 7, DefaultValues.EMPTY_TRIE_HASH)).GetAwaiter().GetResult();
            var ghost = (byte[])wide[random.Next(wide.Count)].Clone();
            ghost[31] ^= 0xA5;
            flat.SaveAccountByHashAsync(ghost, NewAccount(3, 3, DefaultValues.EMPTY_TRIE_HASH)).GetAwaiter().GetResult();
            foreach (var owner in new[] { state.WideOwner, state.ExtensionOwner, state.LargeOwner })
            {
                var slots = state.Storage[owner].Keys.ToList();
                bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, owner.Concat(slots[random.Next(slots.Count)]).ToArray(), new byte[] { 0xEE });
                bundle.Rocks.Delete(RocksDbManager.CF_STATE_STORAGE, owner.Concat(slots[random.Next(slots.Count)]).ToArray());
                var ghostSlot = (byte[])slots[random.Next(slots.Count)].Clone();
                ghostSlot[31] ^= 0x5A;
                bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, owner.Concat(ghostSlot).ToArray(), new byte[] { 0x01 });
            }
            var bulk = state.Storage[state.ExtensionOwner].Keys.Skip(random.Next(5_000)).Take(4_500).ToList();
            foreach (var slot in bulk) bundle.Rocks.Delete(RocksDbManager.CF_STATE_STORAGE, state.ExtensionOwner.Concat(slot).ToArray());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task Given_WideAndLargeOwnerStatesWithSeededDefects_When_Reconciled_Then_TheRepairsAndFlatEqualTheLeafWalkOracleOrAHoleFailsClosed(int seed)
        {
            var state = DescentScaleState.Create(seed);
            for (int round = 0; round < 3; round++)
            {
                var certified = OpenPathKeyedBundle();
                var oracleCopy = OpenPathKeyedBundle();
                state.Write(certified);
                state.Write(oracleCopy);
                InjectDescentScaleDefects(certified, state, seed, round);
                InjectDescentScaleDefects(oracleCopy, state, seed, round);
                byte[] holePath = null;
                if (round == 2)
                {
                    using (var it = certified.Rocks.CreateIterator(RocksDbManager.CF_STATE_TRIE_ACCOUNT))
                        for (it.Seek(new byte[] { 0x3, 0xA, 0x7 }); it.Valid() && holePath == null; it.Next())
                            if (it.Key().Length == 4) holePath = it.Key();
                    DeleteStoredNode(certified, Array.Empty<byte>(), holePath);
                }

                if (holePath != null)
                {
                    var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                        () => certified.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None));
                    Assert.Contains(thrown.MissingNodes, m => m.Owner.Length == 0 && m.Path.SequenceEqual(holePath));
                }
                else
                {
                    var byHash = await certified.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);
                    var byWalk = await Oracle(oracleCopy).ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);
                    Assert.True(byWalk.TotalRepairs > 5_000);
                    Assert.Equal(byWalk.GhostAccountsDeleted, byHash.GhostAccountsDeleted);
                    Assert.Equal(byWalk.GhostSlotsDeleted, byHash.GhostSlotsDeleted);
                    Assert.Equal(byWalk.AccountsAdded, byHash.AccountsAdded);
                    Assert.Equal(byWalk.SlotsAdded, byHash.SlotsAdded);
                    Assert.Equal(byWalk.AccountsPatched, byHash.AccountsPatched);
                    Assert.Equal(byWalk.SlotsPatched, byHash.SlotsPatched);
                    Assert.Equal(byWalk.AccountsScanned, byHash.AccountsScanned);
                    Assert.Equal(byWalk.SlotsScanned, byHash.SlotsScanned);
                    Assert.Equal(FlatChecksum(oracleCopy.Rocks), FlatChecksum(certified.Rocks));
                    Assert.True(byHash.LeavesWalked < byWalk.AccountsScanned + byWalk.SlotsScanned);
                    var again = await certified.VerifyFlatStateAsync(state.Root, null, CancellationToken.None);
                    Assert.Equal(0, again.TotalRepairs);
                }
                ReleaseBundles(certified, oracleCopy);
            }
        }

        [Fact]
        public async Task Given_TheSameCertifyRun_When_RunByUnitHashAndByLeafWalk_Then_BothRepairTheSameRows()
        {
            var (byUnitHashBundle, model) = await SeededAsync(OpenPathKeyedBundle, 61, 400, 12, 30);
            var (byLeafWalkBundle, _) = await SeededAsync(OpenPathKeyedBundle, 61, 400, 12, 30);
            ApplyMixedDivergence(byUnitHashBundle, model);
            ApplyMixedDivergence(byLeafWalkBundle, model);
            Assert.Equal(FlatChecksum(byUnitHashBundle.Rocks), FlatChecksum(byLeafWalkBundle.Rocks));

            var byUnitHash = await byUnitHashBundle.ReconcileFlatStateAsync(model.Root, null, CancellationToken.None);
            var byLeafWalk = await Oracle(byLeafWalkBundle).ReconcileFlatStateAsync(model.Root, null, CancellationToken.None);

            Assert.True(byUnitHash.TotalRepairs > 0);
            Assert.Equal(byLeafWalk.TotalRepairs, byUnitHash.TotalRepairs);
            Assert.Equal(byLeafWalk.GhostAccountsDeleted, byUnitHash.GhostAccountsDeleted);
            Assert.Equal(byLeafWalk.GhostSlotsDeleted, byUnitHash.GhostSlotsDeleted);
            Assert.Equal(byLeafWalk.AccountsAdded, byUnitHash.AccountsAdded);
            Assert.Equal(byLeafWalk.SlotsAdded, byUnitHash.SlotsAdded);
            Assert.Equal(byLeafWalk.AccountsPatched, byUnitHash.AccountsPatched);
            Assert.Equal(byLeafWalk.SlotsPatched, byUnitHash.SlotsPatched);
            Assert.Equal(FlatChecksum(byLeafWalkBundle.Rocks), FlatChecksum(byUnitHashBundle.Rocks));
        }
    }
}
