using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using static Nethereum.CoreChain.RocksDB.UnitTests.GenerationTestState;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Trait("Slice", "Flat")]
    public class FlatStateGenerationDiffTests : GenerationTestBase
    {
        private const int UnitCount = 256;
        private static readonly byte[] Contract = HashOf("diff-contract", 0);
        private static readonly byte[] Changed = { 0xBE, 0xEF };

        private static GenerationTestState Base() => new GenerationTestState().WithEoas("diff", 400).WithContract("diff-contract", 300);

        private static byte[] Slot(int index) => HashOf("diff-contract-slot", index);

        private static byte[] Nibbles(byte[] key) => key.SelectMany(b => new[] { (byte)(b >> 4), (byte)(b & 0x0F) }).ToArray();

        private static byte[] StorageKey(byte[] owner, byte[] path) => owner.Concat(path).ToArray();

        private async Task<RocksDbChainStoreBundle> GeneratedAtAsync(GenerationTestState state)
        {
            var client = OpenPathKeyedBundle();
            state.Write(client);
            await client.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);
            return client;
        }

        private RocksDbChainStoreBundle ServerAt(GenerationTestState state)
        {
            var server = OpenPathKeyedBundle();
            state.WriteTrie(server);
            return server;
        }

        private static IHealNodeSink Sink(RocksDbChainStoreBundle client) => ((IHealNodeSinkProvider)client).CreateHealSink();

        private async Task HealAsync(RocksDbChainStoreBundle client, GenerationTestState target)
        {
            var sink = Sink(client);
            var healer = new TrieHealer(new ServingScheduler(ServerAt(target)), sink, client.StateTrieNodes, NullLogger.Instance,
                flatWriter: sink as ISnapFlatStateWriter, codeStore: client.State);
            var healed = await healer.HealAsync(target.Root, ct: CancellationToken.None);
            Assert.True(healed.Matched);
        }

        private static async Task<ConcurrentQueue<string>> ReconcileAsync(RocksDbChainStoreBundle client, byte[] root)
        {
            var lines = new ConcurrentQueue<string>();
            await client.ReconcileFlatStateAsync(root, lines.Enqueue, CancellationToken.None);
            return lines;
        }

        private static async Task AssertFlatEqualsTrieAsync(RocksDbChainStoreBundle client, GenerationTestState state)
            => Assert.Equal(0, (await OracleVerify(client, state.Root)).TotalRepairs);

        private static int DiffRegions(ConcurrentQueue<string> lines)
        {
            var diff = lines.Single(l => l.StartsWith("snap.flat.certify.diff ", StringComparison.Ordinal));
            return int.Parse(diff.Split(' ').Single(t => t.StartsWith("regions=", StringComparison.Ordinal)).Substring("regions=".Length));
        }

        private static GenerationTestState WithOneEoaChanged(GenerationTestState state, int eoa = 3)
            => state.Clone().WithAccount(HashOf("diff", eoa), NewAccount(77, 77));

        private static byte[] StoredDepthTwoPath(RocksDbChainStoreBundle bundle, byte[] owner, out int slot)
        {
            for (slot = 0; slot < 300; slot++)
            {
                var path = Nibbles(Slot(slot)).Take(2).ToArray();
                if (bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(owner, path)) != null) return path;
            }
            throw new InvalidOperationException("no stored depth-two storage node");
        }

        [Fact]
        public async Task Given_DoneUnitsAndAHealThatFetchedAChangedSlot_When_TheProcessDiesBeforeFlushAndTheRestartHealsToARootWhereItReverted_Then_TheFinalProveIsClean()
        {
            var journalState = Base();
            var client = await GeneratedAtAsync(journalState);
            var writer = Assert.IsAssignableFrom<ISnapFlatStateWriter>(Sink(client));
            await writer.SaveStorageByHashAsync(Contract, Slot(7), Changed);
            var reverted = WithOneEoaChanged(journalState);

            await HealAsync(client, reverted);
            var lines = await ReconcileAsync(client, reverted.Root);

            await AssertFlatEqualsTrieAsync(client, reverted);
            Assert.Equal(0, UnitsQueued(lines));
        }

        [Fact]
        public async Task Given_DoneUnitsAndAnAccountLeafFetchedWhoseSubtreeIsPending_When_TheHealRetargetsToARootWhereTheAccountReverted_Then_TheFinalProveIsClean()
        {
            var journalState = Base();
            var client = await GeneratedAtAsync(journalState);
            var forward = journalState.Clone().WithSlot(Contract, Slot(7), Changed);
            var writer = Assert.IsAssignableFrom<ISnapFlatStateWriter>(Sink(client));
            await writer.SaveAccountByHashAsync(Contract, forward.Accounts[Contract]);
            await writer.SaveStorageByHashAsync(Contract, Slot(7), Changed);
            var reverted = WithOneEoaChanged(journalState);

            await HealAsync(client, reverted);
            var lines = await ReconcileAsync(client, reverted.Root);

            await AssertFlatEqualsTrieAsync(client, reverted);
            Assert.Equal(0, UnitsQueued(lines));
        }

        private static (byte[] Kept, byte[] Removed) TwoSlotsUnderOneNibble(GenerationTestState state, byte[] owner)
        {
            var groups = state.Storage[owner].Keys.GroupBy(k => k[0] >> 4).Where(g => g.Count() == 2).Select(g => g.ToArray());
            foreach (var pair in groups)
                if ((pair[0][0] & 0x0F) != (pair[1][0] & 0x0F)) return (pair[0], pair[1]);
            throw new InvalidOperationException("no nibble holds exactly two slots that branch at depth one");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Given_ABranchCollapsedToALeafAtTheJournalRootWithItsChildrenLeftStored_When_AHealForwardReAddsTheDeletedKeyWithTheSameValue_Then_TheKeyIsInFlat(bool differentValue)
        {
            var full = new GenerationTestState().WithEoas("diff", 400).WithContract("diff-contract", 24);
            var (_, removed) = TwoSlotsUnderOneNibble(full, Contract);
            var collapsed = full.Clone().WithSlot(Contract, removed, null);
            var client = OpenPathKeyedBundle();
            full.WriteTrie(client);
            collapsed.Write(client);
            await client.ReconcileFlatStateAsync(collapsed.Root, null, CancellationToken.None);
            var readded = differentValue ? collapsed.Clone().WithSlot(Contract, removed, Changed) : full;

            await HealAsync(client, readded);
            var lines = await ReconcileAsync(client, readded.Root);

            Assert.Equal(readded.Storage[Contract][removed], client.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, StorageKey(Contract, removed)));
            await AssertFlatEqualsTrieAsync(client, readded);
            Assert.Equal(0, UnitsQueued(lines));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Given_AStaleNodeDeletedDuringAHealTowardOneRoot_When_TheHealRetargetsToARootWhereItsAncestorReverts_Then_TheAbsentReachablePathIsReportedAsAHole(bool deleteRewritten)
        {
            var journalState = Base();
            var client = await GeneratedAtAsync(journalState);
            var path = StoredDepthTwoPath(client, Contract, out _);
            var original = client.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(Contract, path));
            var sink = Sink(client);
            sink.DeleteNode(true, Contract, path);
            sink.Flush();
            if (deleteRewritten)
            {
                sink.PutNode(true, Contract, path, Sha3Keccack.Current.CalculateHash(original), original);
                sink.Flush();
            }
            var reverted = WithOneEoaChanged(journalState);
            await HealAsync(client, reverted);

            if (deleteRewritten)
            {
                var lines = await ReconcileAsync(client, reverted.Root);
                await AssertFlatEqualsTrieAsync(client, reverted);
                Assert.Equal(0, UnitsQueued(lines));
                return;
            }
            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => client.ReconcileFlatStateAsync(reverted.Root, null, CancellationToken.None));
            Assert.Contains(thrown.MissingNodes, m => m.Owner.SequenceEqual(Contract) && m.Path.SequenceEqual(path));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Given_ALeafRewrittenTowardOneRootWhoseParentsNeverCommit_When_TheHealRetargetsToARootWhereItReverted_Then_TheStaleNodeIsReportedAsAHole(bool rewrittenBack)
        {
            var journalState = Base();
            var client = await GeneratedAtAsync(journalState);
            var path = StoredDepthTwoPath(client, Contract, out var slot);
            var original = client.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(Contract, path));
            var forward = ServerAt(journalState.Clone().WithSlot(Contract, Slot(slot), Changed));
            var stale = forward.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(Contract, path));
            var sink = Sink(client);
            sink.PutNode(true, Contract, path, Sha3Keccack.Current.CalculateHash(stale), stale);
            sink.Flush();
            if (rewrittenBack)
            {
                sink.PutNode(true, Contract, path, Sha3Keccack.Current.CalculateHash(original), original);
                sink.Flush();
            }
            var reverted = WithOneEoaChanged(journalState);
            await HealAsync(client, reverted);

            if (rewrittenBack)
            {
                var lines = await ReconcileAsync(client, reverted.Root);
                await AssertFlatEqualsTrieAsync(client, reverted);
                Assert.Equal(0, UnitsQueued(lines));
                return;
            }
            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => client.ReconcileFlatStateAsync(reverted.Root, null, CancellationToken.None));
            Assert.Contains(thrown.MissingNodes, m => m.Owner.SequenceEqual(Contract) && m.Path.SequenceEqual(path));
        }

        private static async Task<int> InterruptAfterUnitsAsync(RocksDbChainStoreBundle client, byte[] root, int units)
        {
            using var cts = new CancellationTokenSource();
            var hashed = 0;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Reconciler(client, new SteppingClock()).ReconcileFlatStateAsync(root, line =>
            {
                if (line.StartsWith("certify.A units_done=", StringComparison.Ordinal) && Interlocked.Increment(ref hashed) == units) cts.Cancel();
            }, cts.Token));
            return Rows(client, RocksDbManager.CF_FLAT_GENERATION, new byte[] { 0x02 }).Count;
        }

        private static bool UnitDone(RocksDbChainStoreBundle client, byte[] key)
            => client.Rocks.Get(RocksDbManager.CF_FLAT_GENERATION, new byte[] { 0x02, key[0] }) != null;

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Given_GenerationInterruptedAndTheTrieHealedForwardThroughPathHealNodeSink_When_Reconciled_Then_FlatEqualsTheNewTrieAndOnlyDiffRegionsAndUnfinishedUnitsWereWalked(bool changeInDoneUnits)
        {
            var journalState = new GenerationTestState().WithEoas("diff", 2_000).WithContract("diff-contract", 300);
            var client = OpenPathKeyedBundle();
            journalState.Write(client);
            var done = await InterruptAfterUnitsAsync(client, journalState.Root, 64);
            var eoas = Enumerable.Range(0, 2_000).Where(i => UnitDone(client, HashOf("diff", i)) == changeInDoneUnits).Take(3).ToArray();
            var forward = journalState.Clone()
                .WithAccount(HashOf("diff", eoas[0]), NewAccount(91, 91))
                .WithoutAccount(HashOf("diff", eoas[1]))
                .WithAccount(HashOf("diff", eoas[2]), NewAccount(92, 92));
            if (changeInDoneUnits && UnitDone(client, Contract)) forward.WithSlot(Contract, Slot(11), Changed);

            await HealAsync(client, forward);
            var lines = await ReconcileAsync(client, forward.Root);

            await AssertFlatEqualsTrieAsync(client, forward);
            Assert.Equal(done, UnitsDoneAtStart(lines));
            Assert.Equal(UnitCount - done, UnitsQueued(lines));
            if (changeInDoneUnits) Assert.InRange(DiffRegions(lines), 1, 8);
            else Assert.Equal(0, DiffRegions(lines));
        }

        private static byte[] AccountUnderPrefixAlone(GenerationTestState state, string domain)
        {
            var single = state.Accounts.Keys.GroupBy(k => k[0]).Where(g => g.Count() == 1).Select(g => g.Key).ToHashSet();
            for (int i = 0; ; i++)
            {
                var candidate = HashOf(domain, i);
                if (single.Contains(candidate[0])) return candidate;
            }
        }

        [Fact]
        public async Task Given_ACrashAfterADiffEmitPatchedAnAccountButBeforeItsStorageWalk_When_Reconciled_Then_TheUnitRerunsAndItsStorageEqualsTheTrie()
        {
            var journalState = Base();
            var client = await GeneratedAtAsync(journalState);
            var added = AccountUnderPrefixAlone(journalState, "diff-added");
            var forward = journalState.Clone().WithContract(added, Enumerable.Range(0, 40).Select(i => (HashOf("diff-added-slot", i), SlotValue(i))));
            forward.Commit();
            await HealAsync(client, forward);

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.ReconcileFlatStateAsync(forward.Root, line =>
            {
                if (line.StartsWith("snap.flat.certify.repair ", StringComparison.Ordinal) && line.Contains(" owner=0x range=") && line.Contains(" cause=diff"))
                    throw new InvalidOperationException("crash after the account patch");
            }, CancellationToken.None));
            Assert.NotNull(await new RocksDbStateStore(client.Rocks).GetAccountByHashAsync(added));
            await ReconcileAsync(client, forward.Root);

            await AssertFlatEqualsTrieAsync(client, forward);
            Assert.Equal(40, Rows(client, RocksDbManager.CF_STATE_STORAGE, added).Count);
        }

        [Fact]
        public async Task Given_ACrashDuringTheDiff_When_ReconciledAgain_Then_FlatEqualsTheNewTrie()
        {
            var journalState = Base();
            var client = await GeneratedAtAsync(journalState);
            var forward = WithOneEoaChanged(journalState).WithSlot(Contract, Slot(5), Changed);
            await HealAsync(client, forward);

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.ReconcileFlatStateAsync(forward.Root, line =>
            {
                if (line.StartsWith("snap.flat.certify.repair ", StringComparison.Ordinal) && line.Contains(" cause=diff"))
                    throw new InvalidOperationException("crash during the diff");
            }, CancellationToken.None));
            await ReconcileAsync(client, forward.Root);

            await AssertFlatEqualsTrieAsync(client, forward);
        }

        [Fact]
        public async Task Given_AHealThatCollapsedADepthOneBranch_When_Reconciled_Then_TheCoveredRangeIsRegenerated()
        {
            var journalState = Base();
            var client = await GeneratedAtAsync(journalState);
            var nibble = journalState.Accounts.Keys.First()[0] >> 4;
            var under = journalState.Accounts.Keys.Where(k => k[0] >> 4 == nibble).ToList();
            var collapsed = journalState.Clone();
            foreach (var key in under.Skip(1)) collapsed.WithoutAccount(key);

            await HealAsync(client, collapsed);
            var lines = await ReconcileAsync(client, collapsed.Root);

            await AssertFlatEqualsTrieAsync(client, collapsed);
            Assert.All(under.Skip(1), key => Assert.Null(client.Rocks.Get(RocksDbManager.CF_STATE_ACCOUNTS, key)));
            Assert.Contains(lines, l => l.StartsWith($"snap.flat.certify.repair unit={nibble:X}", StringComparison.Ordinal) && l.Contains(" range=unit ") && l.Contains(" cause=diff"));
            Assert.Equal(0, UnitsQueued(lines));
        }

        [Fact]
        public async Task Given_AnOwnerWipedByTheHealDuringAnEpoch_When_Reconciled_Then_TheOwnerIsWalkedWhole()
        {
            var journalState = Base();
            var client = await GeneratedAtAsync(journalState);
            var sink = Sink(client);
            sink.ForceWipeStorage(Contract);
            sink.Flush();
            var forward = journalState.Clone().WithSlot(Contract, Slot(9), Changed);

            await HealAsync(client, forward);
            var lines = await ReconcileAsync(client, forward.Root);

            await AssertFlatEqualsTrieAsync(client, forward);
            Assert.Contains(lines, l => l.StartsWith("snap.flat.certify.repair ", StringComparison.Ordinal)
                                        && l.Contains($" owner=0x{Contract.ToHex()} range=owner ") && l.Contains(" cause=diff"));
        }

        private static (GenerationTestState State, byte[] Owner) LonelyOwnerState()
        {
            var state = new GenerationTestState().WithEoas("diff", 400);
            var owner = AccountUnderPrefixAlone(state, "diff-lonely");
            state.WithContract(owner, Enumerable.Range(0, 300).Select(i => (HashOf("diff-lonely-slot", i), SlotValue(i))));
            state.Commit();
            return (state, owner);
        }

        private static (byte[] Path, int Slot) LonelyStoredPath(RocksDbChainStoreBundle client, byte[] owner)
        {
            var slot = Enumerable.Range(0, 300).First(i => client.Rocks.Get(
                RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(owner, Nibbles(HashOf("diff-lonely-slot", i)).Take(2).ToArray())) != null);
            return (Nibbles(HashOf("diff-lonely-slot", slot)).Take(2).ToArray(), slot);
        }

        private static GenerationTestState WithANeighbourSharingThreeNibbles(GenerationTestState state, byte[] owner)
        {
            var neighbour = Enumerable.Range(0, int.MaxValue).Select(i => HashOf("diff-sibling", i))
                .First(k => k[0] == owner[0] && k[1] >> 4 == owner[1] >> 4);
            return state.Clone().WithAccount(neighbour, NewAccount(5, 5));
        }

        private async Task AssertHoleOrCleanAsync(RocksDbChainStoreBundle client, GenerationTestState forward, byte[] owner, byte[] path, bool expectHole)
        {
            if (!expectHole)
            {
                var lines = await ReconcileAsync(client, forward.Root);
                await AssertFlatEqualsTrieAsync(client, forward);
                Assert.Equal(0, UnitsQueued(lines));
                return;
            }
            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => client.ReconcileFlatStateAsync(forward.Root, null, CancellationToken.None));
            Assert.Contains(thrown.MissingNodes, m => m.Owner.SequenceEqual(owner) && m.Path.SequenceEqual(path));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Given_AStaleStorageNodeOfAnUntouchedAccountInsideAnEmittedAccountRange_When_Reconciled_Then_ItIsReportedAsAHole(bool rewritten)
        {
            var (journalState, owner) = LonelyOwnerState();
            var client = await GeneratedAtAsync(journalState);
            var (path, _) = LonelyStoredPath(client, owner);
            var original = client.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(owner, path));
            var sink = Sink(client);
            sink.DeleteNode(true, owner, path);
            sink.Flush();
            if (rewritten)
            {
                sink.PutNode(true, owner, path, Sha3Keccack.Current.CalculateHash(original), original);
                sink.Flush();
            }
            var forward = WithANeighbourSharingThreeNibbles(journalState, owner);
            await HealAsync(client, forward);

            await AssertHoleOrCleanAsync(client, forward, owner, path, expectHole: !rewritten);
        }

        [Fact]
        public async Task Given_AStorageLeafOverwrittenForAnUntouchedAccountInsideAnEmittedAccountRange_When_Reconciled_Then_TheStaleNodeIsReportedAsAHole()
        {
            var (journalState, owner) = LonelyOwnerState();
            var client = await GeneratedAtAsync(journalState);
            var (path, slot) = LonelyStoredPath(client, owner);
            var towardR1 = ServerAt(journalState.Clone().WithSlot(owner, HashOf("diff-lonely-slot", slot), Changed));
            var stale = towardR1.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(owner, path));
            var sink = Sink(client);
            sink.PutNode(true, owner, path, Sha3Keccack.Current.CalculateHash(stale), stale);
            sink.Flush();
            var forward = WithANeighbourSharingThreeNibbles(journalState, owner);
            await HealAsync(client, forward);

            await AssertHoleOrCleanAsync(client, forward, owner, path, expectHole: true);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Given_CapturesAndAHealThatReturnsToTheJournalRoot_When_Reconciled_Then_TheCapturesAreCheckedAndTheHoleIsReported(bool captured)
        {
            var journalState = Base();
            var client = await GeneratedAtAsync(journalState);
            var path = StoredDepthTwoPath(client, Contract, out _);
            if (captured)
            {
                var sink = Sink(client);
                sink.DeleteNode(true, Contract, path);
                sink.Flush();
            }
            await HealAsync(client, journalState);

            if (captured)
            {
                await AssertHoleOrCleanAsync(client, journalState, Contract, path, expectHole: true);
                return;
            }
            var lines = await ReconcileAsync(client, journalState.Root);
            Assert.DoesNotContain(lines, l => l.StartsWith("snap.flat.certify.diff ", StringComparison.Ordinal));
            Assert.Equal(0, UnitsQueued(lines));
        }

        private static long DiffKeysWalked(ConcurrentQueue<string> lines)
        {
            var diff = lines.Single(l => l.StartsWith("snap.flat.certify.diff ", StringComparison.Ordinal));
            return long.Parse(diff.Split(' ').Single(t => t.StartsWith("keys_walked=", StringComparison.Ordinal)).Substring("keys_walked=".Length).Replace(",", ""));
        }

        [Fact]
        public async Task Given_AWipedOwnerWhoseAccountLeafChangedButStorageRootDidNot_When_Reconciled_Then_TheOwnerIsWalkedWhole()
        {
            var journalState = Base();
            var client = await GeneratedAtAsync(journalState);
            var sink = Sink(client);
            sink.ForceWipeStorage(Contract);
            sink.Flush();
            var contract = journalState.Accounts[Contract];
            var forward = journalState.Clone().WithAccount(Contract, new Account
            {
                Nonce = contract.Nonce, Balance = contract.Balance + 1, StateRoot = contract.StateRoot, CodeHash = contract.CodeHash,
            });

            await HealAsync(client, forward);
            var lines = await ReconcileAsync(client, forward.Root);

            await AssertFlatEqualsTrieAsync(client, forward);
            Assert.True(DiffKeysWalked(lines) >= 300, "the wiped owner's 300 slots were not walked");
        }

        private static System.Collections.Generic.List<byte[]> StoredDepthTwoPathsWithDistinctFirstNibbles(RocksDbChainStoreBundle client, byte[] owner)
            => Enumerable.Range(0, 300).Select(i => Nibbles(Slot(i)).Take(2).ToArray())
                .Where(p => client.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(owner, p)) != null)
                .GroupBy(p => p[0]).Select(g => g.First()).ToList();

        [Fact]
        public async Task Given_HolesPersistAcrossFourHealForwards_When_Finalizing_Then_ItThrowsAndTheJournalKeepsDoneUnits()
        {
            var journalState = Base();
            var client = OpenPathKeyedBundle();
            journalState.Write(client);
            var holes = StoredDepthTwoPathsWithDistinctFirstNibbles(client, Contract);
            Assert.True(holes.Count >= 6, "too few distinct storage subtrees to keep a hole open across four heals");
            client.Rocks.Delete(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(Contract, holes[0]));
            var next = 1;
            var pivot = new BlockHeader { BlockNumber = 100, StateRoot = journalState.Root };
            using var bound = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(() => SnapBootstrapper.ReconcileFlatStateAsync(
                client, new ServingScheduler(ServerAt(journalState)), new SnapBootstrapper.RollingPivot(pivot, HashOf("pivot", 100)),
                (_, _) =>
                {
                    if (next < holes.Count)
                    {
                        var sink = Sink(client);
                        sink.DeleteNode(true, Contract, holes[next++]);
                        sink.Flush();
                    }
                    return Task.FromResult<(BlockHeader, byte[])?>((pivot, HashOf("pivot", 100)));
                },
                null, NullLogger.Instance, bound.Token));

            Assert.Equal(journalState.Root, client.Rocks.Get(RocksDbManager.CF_FLAT_GENERATION, new byte[] { 0x01 }));
            Assert.Equal(UnitCount - 1, Rows(client, RocksDbManager.CF_FLAT_GENERATION, new byte[] { 0x02 }).Count);
            Assert.False(UnitDone(client, Contract));
        }
    }
}
