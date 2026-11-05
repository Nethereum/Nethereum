using System;
using Nethereum.Model.SSZ;
using Xunit;

namespace Nethereum.Model.SSZ.Tests.SecurityAudit71.S14
{
    public class ModelSszDeclaredLengthS14Tests
    {
        private const int DeclaredCount = 1 << 24;
        private const long Budget = 1L << 20;

        private static byte[] LogWithDeclaredTopicCount(uint topicCount)
        {
            var bytes = new byte[20 + 4 + 4 + 4];
            BitConverter.GetBytes(topicCount).CopyTo(bytes, 28);
            return bytes;
        }

        private static long Allocated(Action action)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            Record.Exception(action);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Fact]
        [Trait("Rule", "S14-4")]
        public void Given_ALogDeclaringSixteenMillionTopicsInTwelveTrailingBytes_When_Decoded_Then_AllocationIsBoundedByTheInput()
        {
            var payload = LogWithDeclaredTopicCount(DeclaredCount);

            var allocated = Allocated(() => SszLogEncoder.Current.Decode(payload));

            Assert.True(allocated < Budget, $"{payload.Length} byte log allocated {allocated} bytes");
        }

        [Fact]
        [Trait("Rule", "S14-4")]
        public void Given_AnAccessListWhoseFirstOffsetDeclaresSixteenMillionItems_When_Decoded_Then_AllocationIsBoundedByTheInput()
        {
            var payload = new byte[8];
            BitConverter.GetBytes((uint)DeclaredCount * 4).CopyTo(payload, 0);

            var allocated = Allocated(() => SszAccessListEncoder.Current.DecodeAccessList(payload));

            Assert.True(allocated < Budget, $"{payload.Length} byte access list allocated {allocated} bytes");
        }

        [Fact]
        [Trait("Rule", "S14-4")]
        public void Given_AReceiptLogListDeclaringSixteenMillionLogs_When_Decoded_Then_AllocationIsBoundedByTheInput()
        {
            var payload = new byte[20 + 8 + 4 + 1 + 4];
            BitConverter.GetBytes((uint)DeclaredCount).CopyTo(payload, 33);

            var allocated = Allocated(() => SszReceiptEncoder.Current.DecodeBasicReceipt(
                payload, out _, out _, out _, out _));

            Assert.True(allocated < Budget, $"{payload.Length} byte receipt allocated {allocated} bytes");
        }
    }
}
