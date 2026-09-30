using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Snap.CatchUp;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests.CatchUp
{
    public class SnapTaskFrontierTests
    {
        private static byte[] H(byte first)
        {
            var bytes = new byte[32];
            bytes[0] = first;
            return bytes;
        }

        private static SnapSyncAccountTask Task(
            byte next, byte last, byte[][] completed = null, (byte[] account, byte subNext, byte subLast)? subtask = null)
        {
            var subTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>();
            if (subtask.HasValue)
            {
                var s = subtask.Value;
                subTasks[s.account] = new[]
                {
                    new SnapSyncStorageSubTask { AccountHash = s.account, Next = H(s.subNext), Last = H(s.subLast), StorageRoot = new byte[32] }
                };
            }
            return new SnapSyncAccountTask
            {
                Next = H(next),
                Last = H(last),
                StorageCompleted = completed ?? new byte[0][],
                SubTasks = subTasks
            };
        }

        [Fact]
        public void IsAccountFetched_BeforeTaskNext_IsTrue()
            => Assert.True(new SnapTaskFrontier(new[] { Task(0x40, 0x80) }).IsAccountFetched(H(0x20)));

        [Fact]
        public void IsAccountFetched_InRemainingRange_IsFalse()
            => Assert.False(new SnapTaskFrontier(new[] { Task(0x40, 0x80) }).IsAccountFetched(H(0x60)));

        [Fact]
        public void IsAccountFetched_PastAllTasks_IsTrue()
            => Assert.True(new SnapTaskFrontier(new[] { Task(0x40, 0x80) }).IsAccountFetched(H(0x90)));

        [Fact]
        public void IsStorageFetched_AccountRangeCompleted_IsTrue()
            => Assert.True(new SnapTaskFrontier(new[] { Task(0x40, 0x80) }).IsStorageFetched(H(0x20), H(0x01)));

        [Fact]
        public void IsStorageFetched_AccountInRangeWithNoSubtask_IsFalse()
            => Assert.False(new SnapTaskFrontier(new[] { Task(0x40, 0x80) }).IsStorageFetched(H(0x60), H(0x01)));

        [Fact]
        public void IsStorageFetched_AccountInStorageCompleted_IsTrue()
        {
            var frontier = new SnapTaskFrontier(new[] { Task(0x40, 0x80, completed: new[] { H(0x60) }) });
            Assert.True(frontier.IsStorageFetched(H(0x60), H(0x01)));
        }

        [Fact]
        public void IsStorageFetched_SlotBeforeSubtaskNext_IsTrue_AndInRemainingSubrange_IsFalse()
        {
            var frontier = new SnapTaskFrontier(new[] { Task(0x40, 0x80, subtask: (H(0x60), 0x50, 0x70)) });
            Assert.True(frontier.IsStorageFetched(H(0x60), H(0x40)));
            Assert.False(frontier.IsStorageFetched(H(0x60), H(0x60)));
        }

        [Fact]
        public void IsAccountFetched_ExactlyAtNext_IsFalse()
            => Assert.False(new SnapTaskFrontier(new[] { Task(0x40, 0x80) }).IsAccountFetched(H(0x40)));

        [Fact]
        public void IsAccountFetched_ExactlyAtLast_IsFalse()
            => Assert.False(new SnapTaskFrontier(new[] { Task(0x40, 0x80) }).IsAccountFetched(H(0x80)));

        [Fact]
        public void IsStorageFetched_SlotExactlyAtSubtaskNext_IsFalse()
            => Assert.False(new SnapTaskFrontier(new[] { Task(0x40, 0x80, subtask: (H(0x60), 0x50, 0x70)) })
                .IsStorageFetched(H(0x60), H(0x50)));

        [Fact]
        public void IsStorageFetched_SlotExactlyAtSubtaskLast_IsFalse()
            => Assert.False(new SnapTaskFrontier(new[] { Task(0x40, 0x80, subtask: (H(0x60), 0x50, 0x70)) })
                .IsStorageFetched(H(0x60), H(0x70)));

        [Fact]
        public void IsAccountFetched_IfProducerLetCursorRaceAheadOfOpenSubtask_WouldWronglyReportFetched()
        {
            var whale = H(0x50);
            var racedAheadTask = Task(next: 0x60, last: 0x80, subtask: (whale, 0x00, 0xff));

            var frontier = new SnapTaskFrontier(new[] { racedAheadTask });

            Assert.True(frontier.IsAccountFetched(whale));
        }
    }
}
