using System;
using System.Collections.Generic;
using System.Threading;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Storage;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapTaskSetFlatDurabilityTests
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
        private static readonly byte[] SomeStateRoot = Filled(0xee);

        [Fact]
        public void GetCheckpointSnapshot_BeforePromotion_StillReportsThePriorDurableCursor()
        {
            var set = new SnapTaskSet(1);
            var originalNext = set.Tasks[0].Next;
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            var acct = Acc(0x10);

            set.CompleteAccountRange(frag,
                new List<AccountClassification> { new(acct, DefaultValues.EMPTY_TRIE_HASH, DefaultValues.EMPTY_DATA_HASH) },
                lastHash: acct, done: true);

            Assert.True(set.Tasks[0].RangeDone, "the live cursor DOES advance immediately -- that part must stay fast");

            var snapshot = set.GetCheckpointSnapshot();

            Assert.Equal(originalNext, snapshot[0].Next, ByteArrayComparer.Current);
        }

        [Fact]
        public void GetCheckpointSnapshot_AfterPromoteFlatDurability_ReflectsThePromotedProgress()
        {
            var set = new SnapTaskSet(1);
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            var acct = Acc(0x10);
            set.CompleteAccountRange(frag,
                new List<AccountClassification> { new(acct, DefaultValues.EMPTY_TRIE_HASH, DefaultValues.EMPTY_DATA_HASH) },
                lastHash: acct, done: true);

            set.PromoteFlatDurability();

            var snapshot = set.GetCheckpointSnapshot();

            Assert.Equal(set.Tasks[0].Last, snapshot[0].Next, ByteArrayComparer.Current);
        }

        [Fact]
        public void GetCheckpointSnapshot_BeforePromotion_StorageSubtaskProgressNotVisible()
        {
            var set = new SnapTaskSet(1) { LargeContractConcurrency = 1 };
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            var whale = Acc(0x20);
            set.CompleteAccountRange(frag,
                new List<AccountClassification> { new(whale, SomeStorageRoot, DefaultValues.EMPTY_DATA_HASH) },
                lastHash: whale, done: true);
            var batch = (SnapFragment.SmallStorageBatch)set.LeaseNext();
            var originalSubtaskNext = Acc(0x21);
            set.CompleteSmallBatch(batch,
                new List<SmallStorageOutcome> { new(whale, SmallStorageResult.Large, originalSubtaskNext) }, SomeStateRoot);

            set.PromoteFlatDurability();

            var s1 = (SnapFragment.StorageSubtask)set.LeaseNext();
            var advanced = Acc(0x77);
            set.CompleteSubtask(s1, moreRemaining: true, nextCursor: advanced);

            var snapshotBeforeSecondPromotion = set.GetCheckpointSnapshot();
            var unfinished = Assert.Single(snapshotBeforeSecondPromotion[0].SubTasks[whale]);
            Assert.Equal(originalSubtaskNext, unfinished.Next, ByteArrayComparer.Current);

            set.PromoteFlatDurability();
            var snapshotAfter = set.GetCheckpointSnapshot();
            var unfinishedAfter = Assert.Single(snapshotAfter[0].SubTasks[whale]);
            Assert.Equal(advanced, unfinishedAfter.Next, ByteArrayComparer.Current);
        }

        [Fact]
        public void PromoteFlatDurability_DoesNotChangeLiveLeaseSelection()
        {
            var set = new SnapTaskSet(1);
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            set.CompleteAccountRange(frag, new List<AccountClassification>(), lastHash: frag.Limit, done: true);

            Assert.Null(set.LeaseNext());
            set.PromoteFlatDurability();
            Assert.Null(set.LeaseNext());
        }

        [Fact]
        public void CompleteSmallBatch_Done_IsAlsoGatedByPromotion()
        {
            var set = new SnapTaskSet(1);
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            var smallAcct = Acc(0x10);
            set.CompleteAccountRange(frag,
                new List<AccountClassification> { new(smallAcct, SomeStorageRoot, DefaultValues.EMPTY_DATA_HASH) },
                lastHash: smallAcct, done: true);
            var batch = (SnapFragment.SmallStorageBatch)set.LeaseNext();

            set.CompleteSmallBatch(batch,
                new List<SmallStorageOutcome> { new(smallAcct, SmallStorageResult.Done, null) }, SomeStateRoot);

            Assert.Contains(smallAcct, set.Tasks[0].StorageCompleted, ByteArrayComparer.Current);
            Assert.DoesNotContain(smallAcct, set.GetCheckpointSnapshot()[0].StorageCompleted, ByteArrayComparer.Current);

            set.PromoteFlatDurability();

            Assert.Contains(smallAcct, set.GetCheckpointSnapshot()[0].StorageCompleted, ByteArrayComparer.Current);
        }

        [Fact]
        public void FlushBulkFlatThenPromoteDurability_BlocksAConcurrentCompletion_UntilTheFlushAndPromotionFinish()
        {
            var set = new SnapTaskSet(1);
            var frag = (SnapFragment.AccountRange)set.LeaseNext();

            var flushStarted = new ManualResetEventSlim(false);
            var releaseFlush = new ManualResetEventSlim(false);

            var checkpointThread = new Thread(() =>
                set.FlushBulkFlatThenPromoteDurability(() =>
                {
                    flushStarted.Set();
                    Assert.True(releaseFlush.Wait(TimeSpan.FromSeconds(10)));
                }));
            checkpointThread.Start();
            Assert.True(flushStarted.Wait(TimeSpan.FromSeconds(10)));

            var acct = Acc(0x10);
            var completeStarted = new ManualResetEventSlim(false);
            var completeFinished = new ManualResetEventSlim(false);
            var completerThread = new Thread(() =>
            {
                completeStarted.Set();
                set.CompleteAccountRange(frag,
                    new List<AccountClassification> { new(acct, DefaultValues.EMPTY_TRIE_HASH, DefaultValues.EMPTY_DATA_HASH) },
                    lastHash: acct, done: true);
                completeFinished.Set();
            });
            completerThread.Start();
            Assert.True(completeStarted.Wait(TimeSpan.FromSeconds(10)));

            Assert.False(completeFinished.Wait(TimeSpan.FromMilliseconds(300)),
                "CompleteAccountRange must be blocked while a flush+promote is in flight, not interleaved with it");

            releaseFlush.Set();
            Assert.True(completeFinished.Wait(TimeSpan.FromSeconds(10)));
            checkpointThread.Join(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void CompleteOwnerStorageUnderGate_BlocksAConcurrentPromotion_UntilItFinishes()
        {
            var set = new SnapTaskSet(1) { LargeContractConcurrency = 1 };
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            var whale = Acc(0x20);
            set.CompleteAccountRange(frag,
                new List<AccountClassification> { new(whale, SomeStorageRoot, DefaultValues.EMPTY_DATA_HASH) },
                lastHash: whale, done: true);
            var batch = (SnapFragment.SmallStorageBatch)set.LeaseNext();
            set.CompleteSmallBatch(batch,
                new List<SmallStorageOutcome> { new(whale, SmallStorageResult.Large, Acc(0x21)) }, SomeStateRoot);

            var flushStarted = new ManualResetEventSlim(false);
            var releaseFlush = new ManualResetEventSlim(false);
            var ownerCompleterThread = new Thread(() =>
                set.CompleteOwnerStorageUnderGate(() =>
                {
                    flushStarted.Set();
                    Assert.True(releaseFlush.Wait(TimeSpan.FromSeconds(10)));
                }));
            ownerCompleterThread.Start();
            Assert.True(flushStarted.Wait(TimeSpan.FromSeconds(10)));

            var promotionStarted = new ManualResetEventSlim(false);
            var promotionFinished = new ManualResetEventSlim(false);
            var promoterThread = new Thread(() =>
            {
                promotionStarted.Set();
                set.PromoteFlatDurability();
                promotionFinished.Set();
            });
            promoterThread.Start();
            Assert.True(promotionStarted.Wait(TimeSpan.FromSeconds(10)));

            Assert.False(promotionFinished.Wait(TimeSpan.FromMilliseconds(300)),
                "PromoteFlatDurability must be blocked while CompleteOwnerStorageUnderGate's flush-and-complete sequence is in flight");

            releaseFlush.Set();
            Assert.True(promotionFinished.Wait(TimeSpan.FromSeconds(10)));
            ownerCompleterThread.Join(TimeSpan.FromSeconds(10));
            promoterThread.Join(TimeSpan.FromSeconds(10));
        }
    }
}
