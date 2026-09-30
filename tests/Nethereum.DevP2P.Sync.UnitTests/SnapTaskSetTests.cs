using System.Collections.Generic;
using System.Linq;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.CatchUp;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapTaskSetTests
    {
        private static byte[] Filled(byte b)
        {
            var a = new byte[32];
            for (int i = 0; i < 32; i++) a[i] = b;
            return a;
        }

        private static byte[] Acc(byte first)
        {
            var a = new byte[32];
            a[0] = first;
            a[31] = 0x11;
            return a;
        }

        private static readonly byte[] SomeStorageRoot = Filled(0xab);
        private static readonly byte[] SomeCodeHash = Filled(0xcd);
        private static readonly byte[] SomeStateRoot = Filled(0xee);

        [Fact]
        public void Seed_PartitionsCoverFullKeyspace_Contiguous()
        {
            var set = new SnapTaskSet(4);

            Assert.Equal(4, set.Tasks.Count);
            Assert.Equal(new byte[32], set.Tasks[0].Next);
            Assert.Equal(Filled(0xff), set.Tasks[^1].Last);
            for (int i = 0; i < set.Tasks.Count - 1; i++)
                Assert.Equal(SnapHashRanges.IncrementHash(set.Tasks[i].Last), set.Tasks[i + 1].Next);
        }

        [Fact]
        public void Seed_FromExplicitPartitions_UsesGivenCursors()
        {
            var parts = new List<(byte[] Next, byte[] Last)>
            {
                (Acc(0x05), Filled(0x7f)),
                (Acc(0x90), Filled(0xff)),
            };

            var set = new SnapTaskSet(parts);

            Assert.Equal(2, set.Tasks.Count);
            Assert.Equal(Acc(0x05), set.Tasks[0].Next, ByteArrayComparer.Current);
            Assert.Equal(Filled(0x7f), set.Tasks[0].Last, ByteArrayComparer.Current);
            Assert.Equal(Acc(0x90), set.Tasks[1].Next, ByteArrayComparer.Current);
            var f = (SnapFragment.AccountRange)set.LeaseNext();
            Assert.Equal(set.Tasks[f.TaskIndex].Next, f.Origin, ByteArrayComparer.Current);
        }

        [Fact]
        public void LeaseNext_FreshSet_ReturnsOneAccountRangePerPartition_ThenNull()
        {
            var set = new SnapTaskSet(2);

            var a = Assert.IsType<SnapFragment.AccountRange>(set.LeaseNext());
            var b = Assert.IsType<SnapFragment.AccountRange>(set.LeaseNext());
            Assert.Null(set.LeaseNext());

            Assert.Equal(new[] { 0, 1 }, new[] { a.TaskIndex, b.TaskIndex }.OrderBy(x => x).ToArray());
            Assert.Equal(set.Tasks[a.TaskIndex].Next, a.Origin);
            Assert.Equal(set.Tasks[a.TaskIndex].Last, a.Limit);
        }

        [Fact]
        public void CompleteAccountRange_NotDone_WithPendingStorageInPage_ClassifiesButHoldsCursor()
        {
            var set = new SnapTaskSet(1);
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            var originalNext = set.Tasks[0].Next;

            var withStorage = Acc(0x10);
            var withCode = Acc(0x20);
            var plain = Acc(0x30);
            var accounts = new List<AccountClassification>
            {
                new(withStorage, SomeStorageRoot, DefaultValues.EMPTY_DATA_HASH),
                new(withCode, DefaultValues.EMPTY_TRIE_HASH, SomeCodeHash),
                new(plain, DefaultValues.EMPTY_TRIE_HASH, DefaultValues.EMPTY_DATA_HASH),
            };

            set.CompleteAccountRange(frag, accounts, lastHash: plain, done: false);

            var t = set.Tasks[0];
            Assert.True(t.RangeInFlight);
            Assert.False(t.RangeDone);
            Assert.Equal(originalNext, t.Next, ByteArrayComparer.Current);
            Assert.Contains(withStorage, t.StateTasks.Keys, ByteArrayComparer.Current);
            Assert.DoesNotContain(withCode, t.StateTasks.Keys, ByteArrayComparer.Current);
            Assert.DoesNotContain(plain, t.StateTasks.Keys, ByteArrayComparer.Current);
            Assert.True(set.HasPendingCode);
        }

        [Fact]
        public void CompleteAccountRange_NotDone_NoPendingStorageInPage_AdvancesCursorImmediately()
        {
            var set = new SnapTaskSet(1);
            var frag = (SnapFragment.AccountRange)set.LeaseNext();

            var withCode = Acc(0x20);
            var plain = Acc(0x30);
            var accounts = new List<AccountClassification>
            {
                new(withCode, DefaultValues.EMPTY_TRIE_HASH, SomeCodeHash),
                new(plain, DefaultValues.EMPTY_TRIE_HASH, DefaultValues.EMPTY_DATA_HASH),
            };

            set.CompleteAccountRange(frag, accounts, lastHash: plain, done: false);

            var t = set.Tasks[0];
            Assert.False(t.RangeInFlight);
            Assert.False(t.RangeDone);
            Assert.Equal(SnapHashRanges.IncrementHash(plain), t.Next, ByteArrayComparer.Current);
            Assert.True(set.HasPendingCode);
        }

        [Fact]
        public void LeaseNext_PrefersSmallStorageBatch_OverOpeningNewAccountPage()
        {
            var set = new SnapTaskSet(1);
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            var acct = Acc(0x10);
            set.CompleteAccountRange(frag,
                new List<AccountClassification> { new(acct, SomeStorageRoot, DefaultValues.EMPTY_DATA_HASH) },
                lastHash: acct, done: false);

            var next = set.LeaseNext();

            var batch = Assert.IsType<SnapFragment.SmallStorageBatch>(next);
            Assert.Single(batch.Items);
            Assert.Equal(acct, batch.Items[0].AccountHash, ByteArrayComparer.Current);
        }

        [Fact]
        public void CompleteSmallBatch_Done_Large_Heal_AreRoutedCorrectly()
        {
            var set = new SnapTaskSet(1) { LargeContractConcurrency = 4 };
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            var doneAcc = Acc(0x10);
            var largeAcc = Acc(0x20);
            var healAcc = Acc(0x30);
            set.CompleteAccountRange(frag, new List<AccountClassification>
            {
                new(doneAcc, SomeStorageRoot, DefaultValues.EMPTY_DATA_HASH),
                new(largeAcc, SomeStorageRoot, DefaultValues.EMPTY_DATA_HASH),
                new(healAcc, SomeStorageRoot, DefaultValues.EMPTY_DATA_HASH),
            }, lastHash: healAcc, done: true);

            var batch = (SnapFragment.SmallStorageBatch)set.LeaseNext();
            var resumeFrom = Acc(0x21);
            set.CompleteSmallBatch(batch, new List<SmallStorageOutcome>
            {
                new(doneAcc, SmallStorageResult.Done, null),
                new(largeAcc, SmallStorageResult.Large, resumeFrom),
                new(healAcc, SmallStorageResult.Heal, null),
            }, SomeStateRoot);

            var t = set.Tasks[0];
            Assert.Contains(doneAcc, t.StorageCompleted, ByteArrayComparer.Current);
            Assert.Contains(healAcc, set.AccountsNeedingHeal, ByteArrayComparer.Current);
            Assert.True(t.LargeContracts.ContainsKey(largeAcc));
            Assert.Equal(4, t.LargeContracts[largeAcc].Subtasks.Count);
            Assert.Equal(4, t.LargeContracts[largeAcc].Pending);
            Assert.Equal(resumeFrom, t.LargeContracts[largeAcc].Subtasks[0].Next, ByteArrayComparer.Current);
            Assert.Equal(Filled(0xff), t.LargeContracts[largeAcc].Subtasks[^1].Last, ByteArrayComparer.Current);
        }

        [Fact]
        public void CreateLargeContract_SplitsWholeKeyspaceFromOrigin()
        {
            var set = new SnapTaskSet(1) { LargeContractConcurrency = 4 };
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            var whale = Acc(0x20);
            set.CompleteAccountRange(frag, new List<AccountClassification>(), lastHash: whale, done: true);

            set.CreateLargeContract(0, whale, SomeStorageRoot, SomeStateRoot);

            var contract = set.Tasks[0].LargeContracts[whale];
            Assert.Equal(SomeStorageRoot, contract.StorageRoot, ByteArrayComparer.Current);
            Assert.Equal(4, contract.Subtasks.Count);
            Assert.Equal(4, contract.Pending);
            Assert.Equal(new byte[32], contract.Subtasks[0].Next, ByteArrayComparer.Current);
            Assert.Equal(Filled(0xff), contract.Subtasks[^1].Last, ByteArrayComparer.Current);
        }

        [Fact]
        public void LeaseNext_PrefersLargeContractSubtask_First()
        {
            var set = new SnapTaskSet(1) { LargeContractConcurrency = 2 };
            var largeAcc = PromoteLargeContract(set, out _);

            var next = set.LeaseNext();

            var sub = Assert.IsType<SnapFragment.StorageSubtask>(next);
            Assert.Equal(largeAcc, sub.AccountHash, ByteArrayComparer.Current);
        }

        [Fact]
        public void CompleteSubtask_LastPage_DrainsAccount_ThenMarkedCompleted()
        {
            var set = new SnapTaskSet(1) { LargeContractConcurrency = 2 };
            var largeAcc = PromoteLargeContract(set, out _);

            var s1 = (SnapFragment.StorageSubtask)set.LeaseNext();
            var s2 = (SnapFragment.StorageSubtask)set.LeaseNext();
            Assert.Null(set.LeaseNext());

            Assert.False(set.CompleteSubtask(s1, moreRemaining: false, nextCursor: null));
            var drained = set.CompleteSubtask(s2, moreRemaining: false, nextCursor: null);

            Assert.True(drained);
            set.MarkStorageCompleted(largeAcc);
            Assert.Contains(largeAcc, set.Tasks[0].StorageCompleted, ByteArrayComparer.Current);
            Assert.True(set.AllDone);
        }

        [Fact]
        public void Storage_WhaleSubtaskStillOpen_CursorHeldBack_IsAccountFetchedFalseUntilDrained()
        {
            var set = new SnapTaskSet(1) { LargeContractConcurrency = 2 };
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            var originalNext = set.Tasks[0].Next;
            var earlierAccountInSamePage = Acc(0x10);
            var whale = Acc(0x20);

            set.CompleteAccountRange(frag, new List<AccountClassification>
            {
                new(earlierAccountInSamePage, DefaultValues.EMPTY_TRIE_HASH, DefaultValues.EMPTY_DATA_HASH),
                new(whale, SomeStorageRoot, DefaultValues.EMPTY_DATA_HASH),
            }, lastHash: whale, done: true);

            var batch = (SnapFragment.SmallStorageBatch)set.LeaseNext();
            set.CompleteSmallBatch(batch,
                new List<SmallStorageOutcome> { new(whale, SmallStorageResult.Large, Acc(0x21)) }, SomeStateRoot);

            var t = set.Tasks[0];
            Assert.True(t.RangeInFlight);
            Assert.False(t.RangeDone);
            Assert.Equal(originalNext, t.Next, ByteArrayComparer.Current);

            var frontierWhileOpen = new SnapTaskFrontier(set.GetCheckpointSnapshot());
            Assert.False(frontierWhileOpen.IsAccountFetched(earlierAccountInSamePage));
            Assert.False(frontierWhileOpen.IsAccountFetched(whale));

            var s1 = (SnapFragment.StorageSubtask)set.LeaseNext();
            var s2 = (SnapFragment.StorageSubtask)set.LeaseNext();
            Assert.False(set.CompleteSubtask(s1, moreRemaining: false, nextCursor: null));

            var frontierOneSubtaskLeft = new SnapTaskFrontier(set.GetCheckpointSnapshot());
            Assert.False(frontierOneSubtaskLeft.IsAccountFetched(whale));

            var drained = set.CompleteSubtask(s2, moreRemaining: false, nextCursor: null);
            Assert.True(drained);
            set.MarkStorageCompleted(whale);

            Assert.True(t.RangeDone);
            Assert.False(t.RangeInFlight);
            Assert.Equal(t.Last, t.Next, ByteArrayComparer.Current);
            Assert.True(set.AllDone);

            set.PromoteFlatDurability();
            var frontierAfterDrain = new SnapTaskFrontier(set.GetCheckpointSnapshot());
            Assert.True(frontierAfterDrain.IsAccountFetched(earlierAccountInSamePage));
            Assert.True(frontierAfterDrain.IsAccountFetched(whale));
        }

        [Fact]
        public void Revert_AccountRange_ReturnsFragmentToPending()
        {
            var set = new SnapTaskSet(1);
            var frag = set.LeaseNext();
            Assert.Null(set.LeaseNext());

            set.Revert(frag);

            var again = Assert.IsType<SnapFragment.AccountRange>(set.LeaseNext());
            Assert.Equal(0, again.TaskIndex);
        }

        [Fact]
        public void GetDiagnostics_OrphanedInflightRange_ReportsNoLeasableWorkButNotDone()
        {
            var set = new SnapTaskSet(1);
            Assert.IsType<SnapFragment.AccountRange>(set.LeaseNext());

            var diagnostics = set.GetDiagnostics();

            Assert.False(diagnostics.AllDone);
            Assert.False(diagnostics.HasLeasableWork);
            Assert.Equal(1, diagnostics.RangeInFlightCount);
            Assert.Equal(0, diagnostics.RangePendingCount);
            Assert.Equal(0, diagnostics.RangeDoneCount);
            Assert.Null(set.LeaseNext());
        }

        [Fact]
        public void Storage_SubTasks_ResumeFromPersistedCursor_NotFromScratch()
        {
            var completedAccount = Acc(0x10);
            var whaleAccount = Acc(0x20);
            var persistedSubtaskNext = Acc(0x50);
            var persisted = new SnapSyncAccountTask
            {
                Next = Acc(0x05),
                Last = Filled(0xff),
                StorageCompleted = new List<byte[]> { completedAccount },
                SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(ByteArrayComparer.Current)
                {
                    [whaleAccount] = new List<SnapSyncStorageSubTask>
                    {
                        new()
                        {
                            AccountHash = whaleAccount,
                            Next = persistedSubtaskNext,
                            Last = Filled(0xff),
                            StorageRoot = SomeStorageRoot,
                        },
                    },
                },
            };

            var set = new SnapTaskSet(new List<SnapSyncAccountTask> { persisted });

            Assert.Equal(persisted.Next, set.Tasks[0].Next, ByteArrayComparer.Current);
            Assert.Contains(completedAccount, set.Tasks[0].StorageCompleted, ByteArrayComparer.Current);

            var leases = new List<SnapFragment>();
            SnapFragment lease;
            while ((lease = set.LeaseNext()) != null) leases.Add(lease);

            var accountFrag = Assert.Single(leases.OfType<SnapFragment.AccountRange>());
            Assert.Equal(persisted.Next, accountFrag.Origin, ByteArrayComparer.Current);

            var subtaskFrag = Assert.Single(leases.OfType<SnapFragment.StorageSubtask>());
            Assert.Equal(whaleAccount, subtaskFrag.AccountHash, ByteArrayComparer.Current);
            Assert.Equal(persistedSubtaskNext, subtaskFrag.Next, ByteArrayComparer.Current);

            Assert.DoesNotContain(leases, f => f is SnapFragment.SmallStorageBatch batch
                && batch.Items.Any(i => ByteArrayComparer.Current.Equals(i.AccountHash, completedAccount)));
        }

        [Fact]
        public void Storage_WhaleSubtasks_DoNotStarveAccountRanges()
        {
            const int concurrency = 16;
            var set = new SnapTaskSet(concurrency) { LargeContractConcurrency = concurrency };

            var whaleFrag = (SnapFragment.AccountRange)set.LeaseNext();
            var whaleAcc = Acc(0x20);
            set.CompleteAccountRange(whaleFrag,
                new List<AccountClassification> { new(whaleAcc, SomeStorageRoot, DefaultValues.EMPTY_DATA_HASH) },
                lastHash: whaleAcc, done: true);
            var batch = (SnapFragment.SmallStorageBatch)set.LeaseNext();
            set.CompleteSmallBatch(batch,
                new List<SmallStorageOutcome> { new(whaleAcc, SmallStorageResult.Large, Acc(0x21)) }, SomeStateRoot);

            var leases = new List<SnapFragment>();
            for (int i = 0; i < concurrency; i++)
                leases.Add(set.LeaseNext());

            var accountLeases = leases.OfType<SnapFragment.AccountRange>().Count();
            var subtaskLeases = leases.OfType<SnapFragment.StorageSubtask>().Count();

            Assert.Equal(concurrency, accountLeases + subtaskLeases);
            Assert.True(accountLeases > 0,
                "account-range leasing must receive a guaranteed per-round share even with a full whale pending");
            Assert.True(subtaskLeases < concurrency,
                "the whale must not monopolize every consumer");
        }

        [Fact]
        public void Storage_SubTaskSnapshot_AccountCursorHeldBackWhileSubtaskStillOpen_CheckpointedAtomicallyWithDebt()
        {
            var set = new SnapTaskSet(1) { LargeContractConcurrency = 2 };
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            var originalNext = set.Tasks[0].Next;
            var completedAcc = Acc(0x10);
            var whaleAcc = Acc(0x20);
            set.CompleteAccountRange(frag, new List<AccountClassification>
            {
                new(completedAcc, SomeStorageRoot, DefaultValues.EMPTY_DATA_HASH),
                new(whaleAcc, SomeStorageRoot, DefaultValues.EMPTY_DATA_HASH),
            }, lastHash: whaleAcc, done: true);

            var batch = (SnapFragment.SmallStorageBatch)set.LeaseNext();
            set.CompleteSmallBatch(batch, new List<SmallStorageOutcome>
            {
                new(completedAcc, SmallStorageResult.Done, null),
                new(whaleAcc, SmallStorageResult.Large, Acc(0x21)),
            }, SomeStateRoot);

            var s1 = (SnapFragment.StorageSubtask)set.LeaseNext();
            set.CompleteSubtask(s1, moreRemaining: false, nextCursor: null);

            var s2 = (SnapFragment.StorageSubtask)set.LeaseNext();
            var advancedCursor = Acc(0x77);
            set.CompleteSubtask(s2, moreRemaining: true, nextCursor: advancedCursor);

            set.PromoteFlatDurability();
            var snapshot = set.GetCheckpointSnapshot();

            var task = Assert.Single(snapshot);
            Assert.Equal(originalNext, task.Next, ByteArrayComparer.Current);
            Assert.Contains(completedAcc, task.StorageCompleted, ByteArrayComparer.Current);

            var unfinished = Assert.Single(task.SubTasks[whaleAcc]);
            Assert.Equal(advancedCursor, unfinished.Next, ByteArrayComparer.Current);
            Assert.Equal(SomeStorageRoot, unfinished.StorageRoot, ByteArrayComparer.Current);
        }

        [Fact]
        public void Hydrate_StorageCompletedOwnerWithPendingSubtasks_DropsPendingFanout()
        {
            var completedOwner = Acc(0x30);
            var persisted = new SnapSyncAccountTask
            {
                Next = Filled(0xff),
                Last = Filled(0xff),
                StorageCompleted = new[] { completedOwner },
                SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(ByteArrayComparer.Current)
                {
                    [completedOwner] = new[]
                    {
                        new SnapSyncStorageSubTask
                        {
                            AccountHash = completedOwner,
                            Next = new byte[32],
                            Last = Filled(0xff),
                            StorageRoot = SomeStorageRoot,
                        },
                    },
                },
            };

            var set = new SnapTaskSet(new[] { persisted });

            Assert.False(set.Tasks[0].HasPendingStorage);
            Assert.Equal(0, set.GetDiagnostics().LargeSubtaskPendingCount);
            Assert.False(set.Tasks[0].LargeContracts.ContainsKey(completedOwner));
        }

        private static byte[] PromoteLargeContract(SnapTaskSet set, out byte[] resumeFrom)
        {
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            var largeAcc = Acc(0x20);
            set.CompleteAccountRange(frag,
                new List<AccountClassification> { new(largeAcc, SomeStorageRoot, DefaultValues.EMPTY_DATA_HASH) },
                lastHash: largeAcc, done: true);
            var batch = (SnapFragment.SmallStorageBatch)set.LeaseNext();
            resumeFrom = Acc(0x21);
            set.CompleteSmallBatch(batch,
                new List<SmallStorageOutcome> { new(largeAcc, SmallStorageResult.Large, resumeFrom) }, SomeStateRoot);
            return largeAcc;
        }
    }
}
