using System;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapTaskSetParkedAdvanceDiscardTests
    {
        private static readonly byte[] Whale = Hash(0x40);
        private static readonly byte[] PageEnd = Hash(0x60);
        private static readonly byte[] StorageRoot = Hash(0xab);
        private static readonly byte[] OldStateRoot = Hash(0xe1);

        private static byte[] Hash(byte first)
        {
            var h = new byte[32];
            h[0] = first;
            h[31] = 0x11;
            return h;
        }

        [Fact]
        public void Given_ARangeParkedBehindAPendingWhale_When_ParkedAdvancesAreDiscardedAndTheWhaleCompletesFirst_Then_NextStaysAtThePageStart()
        {
            var set = ParkPageBehindWhale(out var pageStart);

            set.RevertAllInFlightLeases();
            set.DiscardParkedRangeAdvances();
            var refetch = LeaseRangeAgain(set);
            CompleteWhale(set);

            Assert.Equal(pageStart.ToHex(), refetch.Origin.ToHex());
            Assert.Equal(pageStart.ToHex(), set.Tasks[0].Next.ToHex());
            Assert.True(set.Tasks[0].RangeInFlight);
            Assert.False(set.Tasks[0].RangeDone);
        }

        [Fact]
        public void Given_ARangeParkedBehindAPendingWhale_When_TheWhaleCompletesFirstWithoutADiscard_Then_NextJumpsToTheOldPageEnd()
        {
            var set = ParkPageBehindWhale(out var pageStart);

            set.RevertAllInFlightLeases();
            var refetch = LeaseRangeAgain(set);
            CompleteWhale(set);

            Assert.Equal(pageStart.ToHex(), refetch.Origin.ToHex());
            Assert.Equal(SnapHashRanges.IncrementHash(PageEnd).ToHex(), set.Tasks[0].Next.ToHex());
            Assert.False(set.Tasks[0].RangeInFlight);
        }

        private static SnapTaskSet ParkPageBehindWhale(out byte[] pageStart)
        {
            var set = new SnapTaskSet(1) { LargeContractConcurrency = 1 };
            pageStart = (byte[])set.Tasks[0].Next.Clone();

            var page = Assert.IsType<SnapFragment.AccountRange>(set.LeaseNext());
            set.CreateLargeContract(page.TaskIndex, Whale, StorageRoot, OldStateRoot);
            set.CompleteAccountRange(page, Array.Empty<AccountClassification>(), PageEnd, done: false);

            Assert.Equal(pageStart.ToHex(), set.Tasks[0].Next.ToHex());
            return set;
        }

        private static SnapFragment.AccountRange LeaseRangeAgain(SnapTaskSet set)
        {
            for (int i = 0; i < 4; i++)
            {
                var lease = set.LeaseNext();
                if (lease is SnapFragment.AccountRange range) return range;
                set.Revert(lease);
            }
            throw new InvalidOperationException("the parked range was never re-leased");
        }

        private static void CompleteWhale(SnapTaskSet set)
        {
            SnapFragment.StorageSubtask subtask = null;
            for (int i = 0; i < 4 && subtask == null; i++)
            {
                var lease = set.LeaseNext();
                if (lease is SnapFragment.StorageSubtask s) subtask = s;
                else if (lease != null) set.Revert(lease);
            }
            Assert.NotNull(subtask);
            Assert.True(set.CompleteSubtask(subtask, moreRemaining: false, nextCursor: null));
            set.MarkStorageCompleted(Whale);
        }
    }
}
