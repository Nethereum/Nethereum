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
    public partial class FlatStateCertifyRegenerationTests : IDisposable
    {
        private readonly List<string> _dirs = new();
        private readonly List<IDisposable> _owned = new();

        public void Dispose()
        {
            foreach (var owned in _owned) owned.Dispose();
            foreach (var dir in _dirs)
                try { Directory.Delete(dir, recursive: true); } catch { }
        }

        private RocksDbChainStoreBundle OpenBundle(bool journaled = false)
        {
            var dir = Path.Combine(Path.GetTempPath(), $"certifyregen_{Guid.NewGuid():N}");
            _dirs.Add(dir);
            var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: new RocksDbStorageOptions
            {
                PathKeyedState = true,
                TrieNodeHistoryBlocks = journaled ? 128 : -1,
            });
            _owned.Add(bundle);
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

        private static long RowCount(RocksDbManager rocks, string column)
        {
            long rows = 0;
            using var it = rocks.CreateIterator(column);
            for (it.SeekToFirst(); it.Valid(); it.Next()) rows++;
            return rows;
        }

        private static List<(byte[] Key, byte[] Value)> OwnerTrieRows(RocksDbManager rocks, byte[] owner)
        {
            var rows = new List<(byte[], byte[])>();
            using var it = rocks.CreateIterator(RocksDbManager.CF_STATE_TRIE_STORAGE);
            for (it.Seek(owner); it.Valid() && it.Key().Take(32).SequenceEqual(owner); it.Next()) rows.Add((it.Key(), it.Value()));
            return rows;
        }

        private static bool SameRows(List<(byte[] Key, byte[] Value)> a, List<(byte[] Key, byte[] Value)> b)
            => a.Count == b.Count && a.Zip(b).All(p => p.First.Key.SequenceEqual(p.Second.Key) && p.First.Value.SequenceEqual(p.Second.Value));

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

        private static Task<FlatStateReconcileResult> OracleVerify(RocksDbChainStoreBundle bundle, byte[] root)
            => new FlatStateReconciler(bundle.Rocks, new RocksDbStateStore(bundle.Rocks), new HiddenPathStore(bundle.StateTrieNodes))
                .VerifyFlatStateAsync(root, null, CancellationToken.None);

        private sealed class OwnerState
        {
            public byte[] Owner { get; }
            public SortedDictionary<byte[], byte[]> Slots { get; } = new(ByteArrayComparer.Current);
            public byte[] Root { get; private set; }

            public OwnerState(string domain, int slotCount)
            {
                Owner = HashOf(domain, 0);
                for (int i = 0; i < slotCount; i++) Slots[HashOf(domain + "-slot", i)] = SlotValue(i);
            }

            public void Write(RocksDbChainStoreBundle bundle, int eoas = 40)
            {
                var rocks = bundle.Rocks;
                var flatStorage = rocks.GetColumnFamily(RocksDbManager.CF_STATE_STORAGE);
                var trieStorage = rocks.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_STORAGE);
                var trieAccounts = rocks.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_ACCOUNT);
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
                var storage = new StackTrie(null, (path, blob) => Put(Owner.Concat(path).ToArray(), blob, trieStorage));
                foreach (var slot in Slots)
                {
                    Put(Owner.Concat(slot.Key).ToArray(), slot.Value, flatStorage);
                    storage.Update(slot.Key, RLP.RLP.EncodeElement(slot.Value));
                }
                var accounts = new SortedDictionary<byte[], Account>(ByteArrayComparer.Current) { [Owner] = NewAccount(1, 1, storage.Hash()) };
                for (int i = 0; i < eoas; i++) accounts[HashOf("regen-eoa", i)] = NewAccount((ulong)i, 1, DefaultValues.EMPTY_TRIE_HASH);
                var accountTrie = new StackTrie(null, (path, blob) => Put(path, blob, trieAccounts));
                foreach (var account in accounts) accountTrie.Update(account.Key, new AccountEncoder().Encode(account.Value));
                Root = accountTrie.Hash();
                rocks.Write(batch);
                batch.Dispose();
                var flat = new RocksDbStateStore(rocks);
                foreach (var account in accounts) flat.SaveAccountByHashAsync(account.Key, account.Value).GetAwaiter().GetResult();
            }

            public byte[] StoredPathOfLength(RocksDbChainStoreBundle bundle, int length, int skip = 0)
                => OwnerTrieRows(bundle.Rocks, Owner).Where(r => r.Key.Length == 32 + length).Skip(skip).First().Key.Skip(32).ToArray();
        }

        private static void DeleteRaw(RocksDbChainStoreBundle bundle, byte[] owner, byte[] path)
            => bundle.Rocks.Delete(RocksDbManager.CF_STATE_TRIE_STORAGE, owner.Concat(path).ToArray());

        [Fact]
        public async Task Given_TheSameHoledOwner_When_Verified_Then_ItReportsTheHoleAndWritesNothing()
        {
            var bundle = OpenBundle();
            var state = new OwnerState("r6-dry", 300);
            state.Write(bundle);
            var hole = state.StoredPathOfLength(bundle, 1);
            DeleteRaw(bundle, state.Owner, hole);
            var holed = OwnerTrieRows(bundle.Rocks, state.Owner);

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.VerifyFlatStateAsync(state.Root, null, CancellationToken.None));

            Assert.Contains(thrown.MissingNodes, m => m.Owner.SequenceEqual(state.Owner) && m.Path.SequenceEqual(hole));
            Assert.True(SameRows(holed, OwnerTrieRows(bundle.Rocks, state.Owner)));
            Assert.Empty(GenerationJournalRows(bundle));
        }

        private static List<(byte[] Key, byte[] Value)> GenerationJournalRows(RocksDbChainStoreBundle bundle)
        {
            var rows = new List<(byte[], byte[])>();
            using var it = bundle.Rocks.CreateIterator(RocksDbManager.CF_FLAT_GENERATION);
            for (it.SeekToFirst(); it.Valid(); it.Next()) rows.Add((it.Key(), it.Value()));
            return rows;
        }

        [Fact]
        public async Task Given_AStoredStorageNodeWhoseBytesDifferFromTheCanonicalNodeAtItsPath_When_Reconciled_Then_TheDifferenceIsReportedAndTheTrieIsNotRewritten()
        {
            var bundle = OpenBundle();
            var state = new OwnerState("r8", 300);
            state.Write(bundle);
            var path = state.StoredPathOfLength(bundle, 2);
            var key = state.Owner.Concat(path).ToArray();
            var stored = bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, key);
            stored[stored.Length - 1] ^= 0x01;
            bundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_STORAGE, key, stored);
            var before = OwnerTrieRows(bundle.Rocks, state.Owner);
            var lines = new ConcurrentQueue<string>();

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None));

            Assert.Contains(thrown.MissingNodes, m => m.Owner.SequenceEqual(state.Owner) && m.Path.SequenceEqual(path));
            Assert.Contains(lines, line => line.StartsWith("snap.flat.certify done ", StringComparison.Ordinal) && line.Contains(" trie_differ=1 "));
            Assert.True(SameRows(before, OwnerTrieRows(bundle.Rocks, state.Owner)));
        }

        [Fact]
        public async Task Given_AHoledWhaleOwnerThroughTheProductionStoreChain_When_Reconciled_Then_NoTrieNodeAndNoReverseDiffEntryIsWritten()
        {
            var bundle = OpenBundle(journaled: true);
            var state = new OwnerState("r16-whale", 70_000);
            state.Write(bundle);
            var hole = state.StoredPathOfLength(bundle, 2, skip: 7);
            DeleteRaw(bundle, state.Owner, hole);
            var holed = OwnerTrieRows(bundle.Rocks, state.Owner);
            var journalBefore = RowCount(bundle.Rocks, RocksDbManager.CF_NODE_HISTORY) + RowCount(bundle.Rocks, RocksDbManager.CF_NODE_HISTORY_INDEX);

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None));

            Assert.Contains(thrown.MissingNodes, m => m.Owner.SequenceEqual(state.Owner) && m.Path.SequenceEqual(hole));
            Assert.True(SameRows(holed, OwnerTrieRows(bundle.Rocks, state.Owner)));
            Assert.Equal(journalBefore, RowCount(bundle.Rocks, RocksDbManager.CF_NODE_HISTORY) + RowCount(bundle.Rocks, RocksDbManager.CF_NODE_HISTORY_INDEX));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Given_AStaleNodeAtAPathTheCanonicalTrieDoesNotReference_When_Certified_Then_ItIsToleratedAndOnlyARealHoleIsReported(bool ownerAlsoHoled)
        {
            var bundle = OpenBundle();
            var state = new OwnerState("r17", 300);
            state.Write(bundle);
            var stalePath = new byte[] { 0xF, 0xF, 0xF, 0xF, 0xF, 0xF, 0xF };
            var staleKey = state.Owner.Concat(stalePath).ToArray();
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, staleKey));
            bundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_STORAGE, staleKey, HashOf("stale", 0).Concat(HashOf("stale", 1)).ToArray());
            var hole = state.StoredPathOfLength(bundle, 1);
            if (ownerAlsoHoled) DeleteRaw(bundle, state.Owner, hole);

            if (ownerAlsoHoled)
            {
                var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                    () => bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None));
                var missing = Assert.Single(thrown.MissingNodes);
                Assert.Equal(hole, missing.Path);
            }
            else
            {
                Assert.Equal(0, (await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None)).TotalRepairs);
            }

            Assert.NotNull(bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, staleKey));
        }

        private static (byte[] ProvenHole, byte[] DivergedHole, byte[] SlotUnderDiverged) MixedHoles(OwnerState state, RocksDbChainStoreBundle bundle)
        {
            var depthTwo = OwnerTrieRows(bundle.Rocks, state.Owner).Where(r => r.Key.Length == 34).Select(r => r.Key.Skip(32).ToArray()).ToList();
            var groups = depthTwo.GroupBy(p => p[0]).Where(g => bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, state.Owner.Concat(new[] { g.Key }).ToArray()) != null).ToList();
            var proven = groups[0].First();
            var diverged = groups[1].First();
            var slot = state.Slots.Keys.First(k => k[0] >> 4 == diverged[0] && (k[0] & 0x0F) == diverged[1]);
            return (proven, diverged, slot);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Given_AnOwnerWithAHoleInAProvenChildAndAnotherUnderADivergedChild_When_Reconciled_Then_ItIsUnresolvableAndItsTrieIsNeverRewritten(bool divergedChildAlsoHoled)
        {
            var bundle = OpenBundle();
            var state = new OwnerState("mixed", 3_000);
            state.Write(bundle);
            var (provenHole, divergedHole, slotUnder) = MixedHoles(state, bundle);
            DeleteRaw(bundle, state.Owner, provenHole);
            if (divergedChildAlsoHoled) DeleteRaw(bundle, state.Owner, divergedHole);
            var divergedRow = state.Owner.Concat(slotUnder).ToArray();
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, divergedRow, new byte[] { 0xEE, 0x01 });
            var before = OwnerTrieRows(bundle.Rocks, state.Owner);

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None));

            var holes = new[] { provenHole, divergedHole }.Take(divergedChildAlsoHoled ? 2 : 1).ToList();
            Assert.Contains(thrown.MissingNodes, m => m.Owner.SequenceEqual(state.Owner) && holes.Any(h => m.Path.SequenceEqual(h)));
            Assert.All(thrown.MissingNodes, m => Assert.Contains(holes, h => m.Path.SequenceEqual(h)));
            Assert.True(SameRows(before, OwnerTrieRows(bundle.Rocks, state.Owner)));
        }

        private static readonly TimeSpan ReportBound = TimeSpan.FromSeconds(120);

        [Fact]
        public async Task Given_An80kSlotOwnerWhoseStorageTrieWasWipedEntirely_When_Reconciled_Then_ItsMissingRootIsReportedWithinTheBound()
        {
            var bundle = OpenBundle();
            var state = new OwnerState("wiped-owner", 80_000);
            state.Write(bundle);
            ((IContractStorageWipeable)bundle.StateTrieNodes).DeleteRange(state.Owner);
            Assert.Empty(OwnerTrieRows(bundle.Rocks, state.Owner));
            using var bound = new CancellationTokenSource(ReportBound);

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(state.Root, null, bound.Token));

            Assert.Contains(thrown.MissingNodes, m => m.Owner.SequenceEqual(state.Owner) && m.Path.Length == 0);
            Assert.Empty(OwnerTrieRows(bundle.Rocks, state.Owner));
        }

        [Fact]
        public void Given_TwoOwnersWhereOneIsTheOthersSuccessor_When_TheFirstIsWiped_Then_OnlyItsNodesAreRemoved()
        {
            var bundle = OpenBundle();
            var owner = HashOf("wipe-neighbour", 0);
            owner[31] = 0xFE;
            var successor = (byte[])owner.Clone();
            successor[31] = 0xFF;
            var predecessor = (byte[])owner.Clone();
            predecessor[31] = 0xFD;
            foreach (var key in new[] { owner, owner.Concat(new byte[] { 0x0F, 0x0F }).ToArray(), owner.Concat(new byte[] { 0x00 }).ToArray(),
                                        successor, successor.Concat(new byte[] { 0x00 }).ToArray(), predecessor.Concat(new byte[] { 0x0F }).ToArray() })
                bundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_STORAGE, key, HashOf("blob", key.Length));

            ((IContractStorageWipeable)bundle.StateTrieNodes).DeleteRange(owner);

            Assert.Empty(OwnerTrieRows(bundle.Rocks, owner));
            Assert.Equal(2, OwnerTrieRows(bundle.Rocks, successor).Count);
            Assert.Single(OwnerTrieRows(bundle.Rocks, predecessor));
        }

        public static IEnumerable<object[]> GuardedOperations()
        {
            yield return new object[] { "generate" };
            yield return new object[] { "recover" };
            yield return new object[] { "ensure-head" };
        }

        [Theory]
        [MemberData(nameof(GuardedOperations))]
        public async Task Given_ANodeCommitBlockContextWithACurrentBlock_When_ATrieRebuildingOperationRuns_Then_ItRefusesBeforeAnyWork(string operation)
        {
            var bundle = OpenBundle(journaled: true);
            var state = new OwnerState("p1", 20);
            state.Write(bundle);
            var before = OwnerTrieRows(bundle.Rocks, state.Owner);
            bundle.NodeCommitBlockSource.Arm(5);

            var refused = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => operation switch
            {
                "generate" => bundle.GenerateTrieFromFlatAsync(state.Root, null, CancellationToken.None),
                "recover" => bundle.RecoverToAsync(0, FlatRecoverySource.ReconcileFromTrie, null, CancellationToken.None),
                _ => bundle.EnsureConsistentHeadAsync(null, CancellationToken.None),
            });

            Assert.Contains("CurrentBlock", refused.Message);
            Assert.True(SameRows(before, OwnerTrieRows(bundle.Rocks, state.Owner)));
        }

        [Fact]
        public async Task Given_ANodeCommitBlockContextThatWasCleared_When_TheTrieIsGeneratedFromFlat_Then_ItRuns()
        {
            var bundle = OpenBundle(journaled: true);
            var state = new OwnerState("p1-twin", 20);
            state.Write(bundle);
            bundle.NodeCommitBlockSource.Arm(5);
            bundle.NodeCommitBlockSource.Clear();

            var generated = await bundle.GenerateTrieFromFlatAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(state.Root, generated.Root);
        }

        [Fact]
        public async Task Given_AnAccountHoleMetByAWholeShardWalkInAnUnprovenPartition_When_Certified_Then_TheFinalLineCountsIt()
        {
            var bundle = OpenBundle();
            var state = new OwnerState("whole-hole", 10);
            state.Write(bundle, eoas: 200);
            byte[] holePath = null;
            using (var it = bundle.Rocks.CreateIterator(RocksDbManager.CF_STATE_TRIE_ACCOUNT))
                for (it.Seek(new byte[] { 0x6 }); it.Valid() && holePath == null; it.Next())
                    if (it.Key().Length >= 2 && it.Key()[0] == 0x6) holePath = it.Key();
            var undecodable = Enumerable.Range(0, 200).Select(i => HashOf("regen-eoa", i)).First(k => k[0] >> 4 == 0x6);
            bundle.Rocks.Put(RocksDbManager.CF_STATE_ACCOUNTS, undecodable, new byte[] { 0x01, 0x02, 0x03 });
            bundle.Rocks.Delete(RocksDbManager.CF_STATE_TRIE_ACCOUNT, holePath);
            var lines = new ConcurrentQueue<string>();

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None));

            Assert.Contains(thrown.MissingNodes, m => m.Owner.Length == 0 && m.Path.SequenceEqual(holePath));
            var done = Assert.Single(lines, line => line.StartsWith("snap.flat.certify done ", StringComparison.Ordinal));
            Assert.DoesNotContain(" trie_missing=0 ", done);
        }

        [Fact]
        public async Task Given_AnUndecodableAccountRow_When_Certified_Then_ItsUnitIsWalkedAndTheFlatEqualsTheTrie()
        {
            var bundle = OpenBundle();
            var state = new OwnerState("whole-reproof", 10);
            state.Write(bundle, eoas: 200);
            var undecodable = HashOf("regen-eoa", 17);
            bundle.Rocks.Put(RocksDbManager.CF_STATE_ACCOUNTS, undecodable, new byte[] { 0x01, 0x02, 0x03 });

            var result = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(1, result.AccountsPatched);
            Assert.Equal(0, (await OracleVerify(bundle, state.Root)).TotalRepairs);
        }

        public static IEnumerable<object[]> WholeShardRepairShapes()
        {
            yield return new object[] { "single-partition" };
            yield return new object[] { "undecodable-owner-row" };
            yield return new object[] { "leafwalk-fallback" };
        }

        [Theory]
        [MemberData(nameof(WholeShardRepairShapes))]
        public async Task Given_AWrongSlotUnderAnUnprovenOrBypassedUnit_When_Reconciled_Then_TheRepairedFlatEqualsTheTrie(string shape)
        {
            var bundle = OpenBundle();
            var state = new OwnerState("whole-slot-" + shape, 50);
            state.Write(bundle, eoas: shape == "single-partition" ? 0 : 40);
            var wrongSlot = state.Slots.Keys.ElementAt(23);
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, state.Owner.Concat(wrongSlot).ToArray(), new byte[] { 0x5A });
            if (shape == "single-partition")
            {
                var canonical = new AccountEncoder().Decode(PatriciaTrie.LoadFromStorage(state.Root, bundle.StateTrieNodes).Get(state.Owner));
                await new RocksDbStateStore(bundle.Rocks).SaveAccountByHashAsync(state.Owner, NewAccount(1, 2, canonical.StateRoot));
            }
            if (shape == "undecodable-owner-row")
                bundle.Rocks.Put(RocksDbManager.CF_STATE_ACCOUNTS, state.Owner, new byte[] { 0x01, 0x02, 0x03 });
            var reconciler = shape == "leafwalk-fallback"
                ? new FlatStateReconciler(bundle.Rocks, new RocksDbStateStore(bundle.Rocks), new HiddenPathStore(bundle.StateTrieNodes))
                : null;
            var lines = new ConcurrentQueue<string>();

            var result = reconciler != null
                ? await reconciler.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None)
                : await bundle.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None);

            Assert.Equal(1, result.SlotsPatched);
            Assert.Equal(state.Slots[wrongSlot], bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, state.Owner.Concat(wrongSlot).ToArray()));
            Assert.Equal(0, (await OracleVerify(bundle, state.Root)).TotalRepairs);
            Assert.Equal(shape == "leafwalk-fallback", lines.Contains("certify.mode=leafwalk reason=no_path_store"));
        }

        [Theory]
        [InlineData("orphan-slots")]
        [InlineData("ghost-account-storage")]
        public async Task Given_StorageRowsUnderAnOwnerWhoseProvenStorageRootIsEmpty_When_Reconciled_Then_TheyAreDrainedAndTheFlatEqualsTheTrie(string shape)
        {
            var bundle = OpenBundle();
            var state = new OwnerState("empty-root-" + shape, 30);
            state.Write(bundle, eoas: 40);
            var stray = HashOf("stray-owner-" + shape, 0);
            if (shape == "ghost-account-storage")
                await new RocksDbStateStore(bundle.Rocks).SaveAccountByHashAsync(stray, NewAccount(5, 5, state.Root));
            for (int i = 0; i < 12; i++)
                bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, stray.Concat(HashOf("stray-slot", i)).ToArray(), SlotValue(i));
            var lines = new ConcurrentQueue<string>();

            var result = await bundle.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None);

            Assert.Equal(12, result.GhostSlotsDeleted);
            Assert.Equal(shape == "ghost-account-storage" ? 1 : 0, result.GhostAccountsDeleted);
            using (var it = bundle.Rocks.CreateIterator(RocksDbManager.CF_STATE_STORAGE))
            {
                it.Seek(stray);
                Assert.False(it.Valid() && it.Key().Take(32).SequenceEqual(stray), "storage rows survived under the stray owner");
            }
            Assert.Equal(0, (await OracleVerify(bundle, state.Root)).TotalRepairs);
            Assert.Equal(shape == "ghost-account-storage",
                lines.Any(line => line.StartsWith("snap.flat.certify.repair ", StringComparison.Ordinal) && line.Contains(" range=accounts ")));
        }

        private sealed class SwappedBlobPathStore : ITrieNodeStore, IRawNodeReader, IContractStorageWipeable
        {
            private readonly ITrieNodeStore _inner;
            private readonly byte[] _owner;
            private readonly byte[] _path;
            private readonly byte[] _swappedBlob;

            public SwappedBlobPathStore(ITrieNodeStore inner, byte[] owner, byte[] path, byte[] swappedBlob)
            {
                _inner = inner;
                _owner = owner;
                _path = path;
                _swappedBlob = swappedBlob;
            }

            public byte[] Get(Node reference)
                => (reference.Owner ?? Array.Empty<byte>()).SequenceEqual(_owner) && (reference.Path ?? Array.Empty<byte>()).SequenceEqual(_path)
                    ? _swappedBlob
                    : _inner.Get(reference);

            public bool Contains(Node reference) => _inner.Contains(reference);
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
            public void Commit(TrieNodeSet nodes) => _inner.Commit(nodes);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
            public byte[] TryGetRawNode(byte[] owner, byte[] path) => ((IRawNodeReader)_inner).TryGetRawNode(owner, path);
            public void DeleteRange(byte[] owner) => ((IContractStorageWipeable)_inner).DeleteRange(owner);
        }

        [Fact]
        public async Task Given_AStoreThatReturnsAnotherNodesBlobWithoutCheckingItsHash_When_TheLeafWalkReadsIt_Then_ItIsRecordedAsAHoleAndTheRangeWritesNothing()
        {
            var bundle = OpenBundle();
            var state = new OwnerState("swapped", 300);
            state.Write(bundle);
            var path = state.StoredPathOfLength(bundle, 1, skip: 0);
            var sibling = state.StoredPathOfLength(bundle, 1, skip: 1);
            var siblingBlob = bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, state.Owner.Concat(sibling).ToArray());
            var divergedSlot = state.Slots.Keys.First(s => s[0] >> 4 == path[0]);
            var divergedRow = state.Owner.Concat(divergedSlot).ToArray();
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, divergedRow, new byte[] { 0xEE });
            var reconciler = new FlatStateReconciler(bundle.Rocks, new RocksDbStateStore(bundle.Rocks),
                new SwappedBlobPathStore(bundle.StateTrieNodes, state.Owner, path, siblingBlob));

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => reconciler.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None));

            Assert.Contains(thrown.MissingNodes, m => m.Owner.SequenceEqual(state.Owner) && m.Path.SequenceEqual(path));
            Assert.Equal(new byte[] { 0xEE }, bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, divergedRow));
        }
    }
}
