using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using static Nethereum.CoreChain.RocksDB.UnitTests.GenerationTestState;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Collection(DiskHeavyCertifyCollection.Name)]
    [Trait("Slice", "Flat")]
    public class FlatStateGenerationTests : GenerationTestBase
    {
        private const int LargeOwnerSlots = 70_000;

        private static byte[] InUnit(byte[] key, int unit)
        {
            var copy = (byte[])key.Clone();
            copy[0] = (byte)unit;
            return copy;
        }

        private static byte[] StorageRow(byte[] owner, byte[] slot) => owner.Concat(slot).ToArray();

        [Fact]
        public async Task Given_AnAccountMissingFromFlat_When_Generated_Then_ItsRowAndStorageAreWrittenAndAFinalProveIsClean()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new GenerationTestState().WithEoas("c1-eoa", 300).WithContract("c1-contract", 40);
            state.Write(bundle);
            var contract = HashOf("c1-contract", 0);
            bundle.Rocks.Delete(RocksDbManager.CF_STATE_ACCOUNTS, contract);
            foreach (var slot in state.Storage[contract].Keys) bundle.Rocks.Delete(RocksDbManager.CF_STATE_STORAGE, StorageRow(contract, slot));

            var result = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(1, result.AccountsAdded);
            Assert.Equal(40, result.SlotsAdded);
            Assert.Equal(0, (await OracleVerify(bundle, state.Root)).TotalRepairs);
        }

        [Fact]
        public async Task Given_AFlatAccountWithStorageAbsentFromTheTrie_When_Generated_Then_TheAccountAndEverySlotAreDeleted()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new GenerationTestState().WithEoas("c2-eoa", 300).WithContract("c2-kept", 25);
            state.Write(bundle);
            var ghost = InUnit(HashOf("c2-ghost", 0), HashOf("c2-kept", 0)[0]);
            await new RocksDbStateStore(bundle.Rocks).SaveAccountByHashAsync(ghost, NewAccount(4, 4, HashOf("c2-ghost-root", 0)));
            for (int i = 0; i < 12; i++) bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, StorageRow(ghost, HashOf("c2-ghost-slot", i)), SlotValue(i));
            var neighbour = Rows(bundle, RocksDbManager.CF_STATE_STORAGE, HashOf("c2-kept", 0));

            var result = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(1, result.GhostAccountsDeleted);
            Assert.Equal(12, result.GhostSlotsDeleted);
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_ACCOUNTS, ghost));
            Assert.Empty(Rows(bundle, RocksDbManager.CF_STATE_STORAGE, ghost));
            Assert.True(SameRows(neighbour, Rows(bundle, RocksDbManager.CF_STATE_STORAGE, HashOf("c2-kept", 0))));
            Assert.Equal(0, (await OracleVerify(bundle, state.Root)).TotalRepairs);
        }

        [Theory]
        [InlineData(300)]
        [InlineData(LargeOwnerSlots)]
        public async Task Given_AWhaleWhoseBalanceChanged_When_Generated_Then_OnlyTheAccountRowIsRewrittenAndNoSlotIsWalked(int slots)
        {
            var bundle = OpenPathKeyedBundle();
            var state = new GenerationTestState().WithEoas("c3-eoa", 400).WithContract("c3-whale", slots);
            state.Write(bundle);
            var whale = HashOf("c3-whale", 0);
            await new RocksDbStateStore(bundle.Rocks).SaveAccountByHashAsync(whale, NewAccount(1, 999, state.Accounts[whale].StateRoot));
            var accountsInUnit = state.Accounts.Keys.Count(k => k[0] == whale[0]);

            var result = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(1, result.AccountsPatched);
            Assert.Equal(1, result.TotalRepairs);
            Assert.Equal(1, result.RangesWalked);
            Assert.Equal(accountsInUnit, result.LeavesWalked);
            Assert.Equal(slots, result.SlotsScanned);
            Assert.Equal(0, (await OracleVerify(bundle, state.Root)).TotalRepairs);
        }

        public static IEnumerable<object[]> DriftedNibbles()
        {
            yield return new object[] { new[] { 5 } };
            yield return new object[] { Enumerable.Range(0, 16).ToArray() };
        }

        [Theory]
        [MemberData(nameof(DriftedNibbles))]
        public async Task Given_ALargeOwnerWithDriftInOneNibble_When_Generated_Then_OnlyThatNibbleRangeIsWalked(int[] nibbles)
        {
            var bundle = OpenPathKeyedBundle();
            var state = new GenerationTestState().WithEoas("c4-eoa", 400).WithContract("c4-whale", LargeOwnerSlots);
            state.Write(bundle);
            var whale = HashOf("c4-whale", 0);
            foreach (var nibble in nibbles)
            {
                var slot = state.Storage[whale].Keys.First(k => k[0] >> 4 == nibble);
                bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, StorageRow(whale, slot), new byte[] { 0xEE, 0x01 });
            }
            var lines = new ConcurrentQueue<string>();

            var result = await bundle.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None);

            Assert.Equal(nibbles.Length, result.SlotsPatched);
            Assert.Equal(nibbles.Length, result.RangesWalked);
            foreach (var nibble in nibbles)
                Assert.Contains(lines, l => l.StartsWith("snap.flat.certify.repair ", StringComparison.Ordinal) && l.Contains($" range=nibble={nibble:x} "));
            Assert.Equal(0, (await OracleVerify(bundle, state.Root)).TotalRepairs);
        }

        [Fact]
        public async Task Given_AnAccountWhoseStorageRootBecameEmpty_When_Generated_Then_NoFlatSlotRemains()
        {
            var bundle = OpenPathKeyedBundle();
            var trie = new GenerationTestState().WithEoas("c5-eoa", 100).WithContract("c5-emptied", 0);
            var flat = new GenerationTestState().WithEoas("c5-eoa", 100).WithContract("c5-emptied", 30);
            trie.WriteTrie(bundle);
            flat.WriteFlat(bundle);
            var owner = HashOf("c5-emptied", 0);

            var result = await bundle.ReconcileFlatStateAsync(trie.Root, null, CancellationToken.None);

            Assert.Equal(30, result.GhostSlotsDeleted);
            Assert.Equal(1, result.AccountsPatched);
            Assert.Empty(Rows(bundle, RocksDbManager.CF_STATE_STORAGE, owner));
            Assert.Equal(0, (await OracleVerify(bundle, trie.Root)).TotalRepairs);
        }

        [Fact]
        public async Task Given_StorageRowsWithNoAccount_When_Generated_Then_TheyAreDeleted()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new GenerationTestState().WithEoas("c7-eoa", 300).WithContract("c7-contract", 20);
            state.Write(bundle);
            var orphan = HashOf("c7-orphan", 0);
            for (int i = 0; i < 9; i++) bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, StorageRow(orphan, HashOf("c7-orphan-slot", i)), SlotValue(i));

            var result = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(9, result.GhostSlotsDeleted);
            Assert.Equal(9, result.TotalRepairs);
            Assert.Empty(Rows(bundle, RocksDbManager.CF_STATE_STORAGE, orphan));
        }

        private static (byte[] Owner, GenerationTestState State, List<byte[]> EmbeddedSlots) EmbeddedLeafState()
        {
            var owner = HashOf("embedded-owner", 0);
            var shared = HashOf("embedded-prefix", 0);
            var slots = Enumerable.Range(0, 8).Select(nibble =>
            {
                var slot = (byte[])shared.Clone();
                slot[30] = (byte)((shared[30] & 0xF0) | nibble);
                slot[31] = 0x11;
                return slot;
            }).ToList();
            var state = new GenerationTestState().WithEoas("embedded-eoa", 60)
                .WithContract(owner, slots.Select((s, i) => (s, SlotValue(i))));
            return (owner, state, slots);
        }

        [Fact]
        public async Task Given_AStorageBranchWithAnEmbeddedLeafChild_When_Generated_Then_TheEmbeddedSlotIsInFlat()
        {
            var bundle = OpenPathKeyedBundle();
            var (owner, state, slots) = EmbeddedLeafState();
            state.Write(bundle);
            Assert.Single(Rows(bundle, RocksDbManager.CF_STATE_TRIE_STORAGE, owner).Where(r => r.Key.Length == 32 + 61));
            bundle.Rocks.Delete(RocksDbManager.CF_STATE_STORAGE, StorageRow(owner, slots[3]));

            var result = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(1, result.SlotsAdded);
            Assert.Equal(SlotValue(3), bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, StorageRow(owner, slots[3])));
            Assert.Equal(0, (await OracleVerify(bundle, state.Root)).TotalRepairs);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Given_AStaleNodeStoredAtAnEmbeddedChildsPath_When_Generated_Then_TheFlatIsExactAndNoHoleIsReported(bool ownerAlsoDrifted)
        {
            var bundle = OpenPathKeyedBundle();
            var (owner, state, slots) = EmbeddedLeafState();
            state.Write(bundle);
            var branchPath = Rows(bundle, RocksDbManager.CF_STATE_TRIE_STORAGE, owner).Single(r => r.Key.Length == 32 + 61).Key.Skip(32).ToArray();
            var embeddedChildPath = branchPath.Append((byte)2).ToArray();
            bundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_STORAGE, owner.Concat(embeddedChildPath).ToArray(), HashOf("garbage", 0).Prepend((byte)0xE0).ToArray());
            if (ownerAlsoDrifted) bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, StorageRow(owner, slots[5]), new byte[] { 0x7F });

            var result = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(ownerAlsoDrifted ? 1 : 0, result.SlotsPatched);
            Assert.Equal(0, result.TrieNodesMissing + result.TrieNodesDiffering);
            Assert.Equal(0, (await OracleVerify(bundle, state.Root)).TotalRepairs);
        }

        [Fact]
        public async Task Given_ALargeOwnerWhoseSlotsShareAFirstNibble_When_Generated_Then_TheWholeOwnerIsWalkedAndTheProveIsClean()
        {
            var bundle = OpenPathKeyedBundle();
            var whale = HashOf("c12-whale", 0);
            var slots = Enumerable.Range(0, LargeOwnerSlots).Select(i =>
            {
                var slot = HashOf("c12-slot", i);
                slot[0] = 0x5A;
                return (slot, SlotValue(i));
            });
            var state = new GenerationTestState().WithEoas("c12-eoa", 40).WithContract(whale, slots);
            state.Write(bundle);

            var result = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(1, result.RangesWalked);
            Assert.Equal(0, result.TotalRepairs);
            Assert.Equal(LargeOwnerSlots, result.SlotsScanned);
            Assert.Equal(0, (await OracleVerify(bundle, state.Root)).TotalRepairs);
        }

        [Fact]
        public async Task Given_AnUndecodableAccountRow_When_Generated_Then_ItIsRewrittenFromTheTrie()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new GenerationTestState().WithEoas("c13-eoa", 300);
            state.Write(bundle);
            var eoa = HashOf("c13-eoa", 17);
            bundle.Rocks.Put(RocksDbManager.CF_STATE_ACCOUNTS, eoa, new byte[] { 0x01, 0x02, 0x03 });

            var result = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(1, result.AccountsPatched);
            Assert.Equal((EvmUInt256)1, (await new RocksDbStateStore(bundle.Rocks).GetAccountByHashAsync(eoa)).Balance);
            Assert.Equal(0, (await OracleVerify(bundle, state.Root)).TotalRepairs);
        }

        [Fact]
        public async Task Given_ANon64ByteStorageKeyInsideAnOwner_When_Generated_Then_ItIsDeleted()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new GenerationTestState().WithEoas("c13b-eoa", 100).WithContract("c13b-contract", 30);
            state.Write(bundle);
            var malformed = HashOf("c13b-contract", 0).Concat(new byte[] { 0x00 }).ToArray();
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, malformed, new byte[] { 0x01 });

            var result = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(1, result.GhostSlotsDeleted);
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, malformed));
        }

        private static byte[] StoredPathOfLength(RocksDbChainStoreBundle bundle, byte[] owner, int length, int skip = 0)
            => Rows(bundle, RocksDbManager.CF_STATE_TRIE_STORAGE, owner).Where(r => r.Key.Length == 32 + length).Skip(skip).First().Key.Skip(32).ToArray();

        [Fact]
        public async Task Given_AMissingNodeUnderAnUnprovenOwner_When_Generated_Then_TheUnitIsNotDoneAndTheNodeIsReported()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new GenerationTestState().WithEoas("c15-eoa", 200).WithContract("c15-contract", 3_000);
            state.Write(bundle);
            var owner = HashOf("c15-contract", 0);
            var hole = StoredPathOfLength(bundle, owner, 2);
            bundle.Rocks.Delete(RocksDbManager.CF_STATE_TRIE_STORAGE, owner.Concat(hole).ToArray());
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, StorageRow(owner, state.Storage[owner].Keys.First()), new byte[] { 0x7F });
            var lines = new ConcurrentQueue<string>();

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None));
            await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None));

            Assert.Contains(thrown.MissingNodes, m => m.Owner.SequenceEqual(owner) && m.Path.SequenceEqual(hole));
            Assert.Equal(UnitCount - 1, UnitsDoneAtStart(lines, run: 1));
            Assert.Equal(1, UnitsQueued(lines, run: 1));
        }

        private const int UnitCount = 256;

        [Fact]
        public async Task Given_MoreHolesThanTheCapInOneUnit_When_Generated_Then_TheUnitIsNotDoneAndARerunFindsTheRest()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new GenerationTestState().WithEoas("c15b-eoa", 20).WithContract("c15b-contract", 20_000);
            state.Write(bundle);
            var owner = HashOf("c15b-contract", 0);
            var canonical = Rows(bundle, RocksDbManager.CF_STATE_TRIE_STORAGE, owner);
            var deleted = canonical.Where(r => r.Key.Length == 32 + 4).Take(1_100).ToList();
            foreach (var row in deleted) bundle.Rocks.Delete(RocksDbManager.CF_STATE_TRIE_STORAGE, row.Key);

            var first = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None));
            Assert.Equal(1_024, first.MissingNodes.Count);
            foreach (var row in deleted.Where(r => first.MissingNodes.Any(m => owner.Concat(m.Path).SequenceEqual(r.Key))))
                bundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_STORAGE, row.Key, row.Value);
            var second = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None));
            foreach (var row in deleted) bundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_STORAGE, row.Key, row.Value);

            var clean = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(1_100 - 1_024, second.MissingNodes.Count);
            Assert.Empty(first.MissingNodes.Intersect(second.MissingNodes, new PathComparer()));
            Assert.Equal(0, clean.TotalRepairs);
        }

        private sealed class PathComparer : IEqualityComparer<(byte[] Owner, byte[] Path)>
        {
            public bool Equals((byte[] Owner, byte[] Path) x, (byte[] Owner, byte[] Path) y)
                => x.Owner.SequenceEqual(y.Owner) && x.Path.SequenceEqual(y.Path);
            public int GetHashCode((byte[] Owner, byte[] Path) obj) => obj.Path.Length;
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public async Task Given_AMissingUnitNodeAtTheRoot_When_Generated_Then_TheUnitIsWalkedAndTheNodeReported(int depth)
        {
            var bundle = OpenPathKeyedBundle();
            var state = new GenerationTestState().WithEoas("c15c-eoa", 3_000);
            state.Write(bundle);
            var path = Rows(bundle, RocksDbManager.CF_STATE_TRIE_ACCOUNT).First(r => r.Key.Length == depth).Key;
            var canonical = bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path);
            bundle.Rocks.Delete(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path);
            var lines = new ConcurrentQueue<string>();

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None));
            bundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path, canonical);
            var healed = await bundle.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None);

            Assert.Contains(thrown.MissingNodes, m => m.Owner.Length == 0 && m.Path.SequenceEqual(path));
            Assert.Equal(depth == 0 ? UnitCount : 1, UnitsQueued(lines, run: 1));
            Assert.Equal(0, healed.TotalRepairs);
        }

        [Fact]
        public async Task Given_GenerationCancelledAfterSomeUnitsCompleted_When_ReconcileRunsAgainAtTheSameRoot_Then_OnlyUnfinishedUnitsAreHashed()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new GenerationTestState().WithEoas("r3-eoa", 2_000).WithContract("r3-contract", 300);
            state.Write(bundle);
            using var cts = new CancellationTokenSource();
            var unitsHashed = 0;
            void CancelAfterSixtyFourUnits(string line)
            {
                if (line.StartsWith("certify.A units_done=", StringComparison.Ordinal) && Interlocked.Increment(ref unitsHashed) == 64) cts.Cancel();
            }

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => Reconciler(bundle, new SteppingClock()).ReconcileFlatStateAsync(state.Root, CancelAfterSixtyFourUnits, cts.Token));
            var lines = new ConcurrentQueue<string>();
            var resumed = await bundle.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None);

            var done = UnitsDoneAtStart(lines);
            Assert.InRange(done, 1, UnitCount - 1);
            Assert.Equal(UnitCount - done, UnitsQueued(lines));
            Assert.True(resumed.AccountsScanned < state.Accounts.Count, "the resumed run re-hashed every unit");
            Assert.Equal(0, (await OracleVerify(bundle, state.Root)).TotalRepairs);
        }

        [Fact]
        public async Task Given_ACancellationAfterAUnitsRepairWritesButBeforeItsDoneKey_When_Reconciled_Then_TheUnitRerunsAndTheProveIsClean()
        {
            var bundle = OpenPathKeyedBundle();
            var eoa = InUnit(HashOf("c20-eoa-diverged", 0), 0x42);
            var contract = InUnit(HashOf("c20-contract", 0), 0x42);
            var state = new GenerationTestState().WithEoas("c20-eoa", 300)
                .WithContract(contract, Enumerable.Range(0, 40).Select(i => (HashOf("c20-contract-slot", i), SlotValue(i))));
            state.Accounts[eoa] = NewAccount(3, 3);
            state.Write(bundle);
            await new RocksDbStateStore(bundle.Rocks).SaveAccountByHashAsync(eoa, NewAccount(4, 4));
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, StorageRow(contract, state.Storage[contract].Keys.First()), new byte[] { 0x7F });
            var crashed = 0;
            void CrashAfterTheAccountRepair(string line)
            {
                if (line.StartsWith("snap.flat.certify.repair unit=42 ", StringComparison.Ordinal) && line.Contains(" range=accounts ")
                    && Interlocked.Exchange(ref crashed, 1) == 0)
                    throw new IOException("simulated crash after a repair write");
            }

            await Assert.ThrowsAsync<IOException>(() => bundle.ReconcileFlatStateAsync(state.Root, CrashAfterTheAccountRepair, CancellationToken.None));
            var lines = new ConcurrentQueue<string>();
            var rerun = await bundle.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None);

            Assert.Equal(1, crashed);
            Assert.Equal(0, rerun.AccountsPatched);
            Assert.Equal(1, rerun.SlotsPatched);
            Assert.Contains(lines, l => l.StartsWith("snap.flat.certify.repair unit=42 ", StringComparison.Ordinal) && l.Contains(" range=owner "));
            Assert.Equal(0, (await OracleVerify(bundle, state.Root)).TotalRepairs);
        }

        [Fact]
        public async Task Given_ASamplingVerify_When_Run_Then_OnlyTheSampledAccountsPerShardAreWalkedAndNothingIsWritten()
        {
            const int sample = 3;
            var bundle = OpenPathKeyedBundle();
            var state = new GenerationTestState().WithEoas("c29b-eoa", 600);
            state.Write(bundle);
            var diverged = HashOf("c29b-eoa", 9);
            await new RocksDbStateStore(bundle.Rocks).SaveAccountByHashAsync(diverged, NewAccount(77, 77));
            var flatBefore = Rows(bundle, RocksDbManager.CF_STATE_ACCOUNTS);
            var lines = new ConcurrentQueue<string>();

            var result = await bundle.VerifyFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None, sample);

            Assert.Equal(16 * (sample + 1), result.AccountsScanned);
            Assert.Equal(16, result.RangesWalked);
            Assert.True(SameRows(flatBefore, Rows(bundle, RocksDbManager.CF_STATE_ACCOUNTS)));
            Assert.Empty(Rows(bundle, RocksDbManager.CF_FLAT_GENERATION));
            Assert.DoesNotContain(lines, l => l.StartsWith("snap.flat.certify start ", StringComparison.Ordinal));
        }

        [Fact]
        public async Task Given_AnEmptyStateRootWithStrayFlatRows_When_Generated_Then_FlatIsEmpty()
        {
            var bundle = OpenPathKeyedBundle();
            new GenerationTestState().WithEoas("c30-eoa", 120).WithContract("c30-contract", 25).WriteFlat(bundle);
            for (int i = 0; i < 4; i++) bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, StorageRow(HashOf("c30-orphan", 0), HashOf("c30-orphan-slot", i)), SlotValue(i));

            var result = await bundle.ReconcileFlatStateAsync(DefaultValues.EMPTY_TRIE_HASH, null, CancellationToken.None);

            Assert.Equal(121, result.GhostAccountsDeleted);
            Assert.Equal(29, result.GhostSlotsDeleted);
            Assert.Empty(Rows(bundle, RocksDbManager.CF_STATE_ACCOUNTS));
            Assert.Empty(Rows(bundle, RocksDbManager.CF_STATE_STORAGE));
        }

        [Fact]
        public async Task Given_AGenerationResumedAcrossARestart_When_Complete_Then_MissingCodeFromBothRunsIsPersisted()
        {
            var bundle = OpenPathKeyedBundle();
            var early = InUnit(HashOf("c32-early", 0), 0xFA);
            var late = InUnit(HashOf("c32-late", 0), 0x05);
            var earlyCode = Sha3Keccack.Current.CalculateHash(new byte[] { 0x60, 0x01 });
            var lateCode = Sha3Keccack.Current.CalculateHash(new byte[] { 0x60, 0x02 });
            var state = new GenerationTestState().WithEoas("c32-eoa", 300)
                .WithContract(early, Enumerable.Range(0, 5).Select(i => (HashOf("c32-early-slot", i), SlotValue(i))), earlyCode)
                .WithContract(late, Enumerable.Range(0, 5).Select(i => (HashOf("c32-late-slot", i), SlotValue(i))), lateCode);
            state.Write(bundle);
            bundle.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, StorageRow(late, state.Storage[late].Keys.First()), new byte[] { 0x7F });
            var crashed = 0;

            await Assert.ThrowsAsync<IOException>(() => bundle.ReconcileFlatStateAsync(state.Root, line =>
            {
                if (line.StartsWith("snap.flat.certify.repair ", StringComparison.Ordinal) && Interlocked.Exchange(ref crashed, 1) == 0)
                    throw new IOException("simulated crash");
            }, CancellationToken.None));
            var persistedAfterTheCrash = ((IFlatStateReconciler)bundle).GetPersistedMissingCode();
            await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Contains(persistedAfterTheCrash, h => h.SequenceEqual(earlyCode));
            Assert.DoesNotContain(persistedAfterTheCrash, h => h.SequenceEqual(lateCode));
            var persisted = ((IFlatStateReconciler)bundle).GetPersistedMissingCode();
            Assert.Contains(persisted, h => h.SequenceEqual(earlyCode));
            Assert.Contains(persisted, h => h.SequenceEqual(lateCode));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Given_AStorageTrieMissingANodeUnderAFlatThatProvesTheOwner_When_Reconciled_Then_TheMissingNodeIsReportedAndNoTrieNodeIsWritten(bool holed)
        {
            var bundle = OpenPathKeyedBundle();
            var state = new GenerationTestState().WithEoas("r6-eoa", 100).WithContract("r6-contract", 300);
            state.Write(bundle);
            var owner = HashOf("r6-contract", 0);
            var hole = StoredPathOfLength(bundle, owner, 1);
            if (holed) bundle.Rocks.Delete(RocksDbManager.CF_STATE_TRIE_STORAGE, owner.Concat(hole).ToArray());
            var trieBefore = Rows(bundle, RocksDbManager.CF_STATE_TRIE_STORAGE);
            var flatBefore = Rows(bundle, RocksDbManager.CF_STATE_STORAGE);

            if (holed)
            {
                var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                    () => bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None));
                Assert.Contains(thrown.MissingNodes, m => m.Owner.SequenceEqual(owner) && m.Path.SequenceEqual(hole));
            }
            else
            {
                Assert.Equal(0, (await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None)).TotalRepairs);
            }

            Assert.True(SameRows(trieBefore, Rows(bundle, RocksDbManager.CF_STATE_TRIE_STORAGE)));
            Assert.True(SameRows(flatBefore, Rows(bundle, RocksDbManager.CF_STATE_STORAGE)));
        }
    }
}
