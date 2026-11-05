using System;
using Nethereum.Consensus.Ssz;
using Nethereum.Ssz;
using Xunit;

namespace Nethereum.Consensus.Ssz.Tests.SecurityAudit71.S14.Round3
{
    public class ConsensusSszRound3S14Tests
    {
        private const int DeclaredCount = 1 << 24;
        private const long Budget = 1L << 20;

        private static (long allocated, Exception thrown) Measure(Action action)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var thrown = Record.Exception(action);
            return (GC.GetAllocatedBytesForCurrentThread() - before, thrown);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(32)]
        [InlineData(48)]
        [Trait("Rule", "S14-R3-5")]
        public void Given_ACountOfSixteenMillionOverAnEmptyOrShortBuffer_When_ReadBytesVector_Then_ItThrowsInvalidOperationWithBoundedAllocation(int elementLength)
        {
            var (allocated, thrown) = Measure(() =>
            {
                var reader = new SszReader(new byte[32]);
                reader.ReadBytesVector(DeclaredCount, elementLength);
            });

            Assert.IsType<InvalidOperationException>(thrown);
            Assert.True(allocated < Budget, $"allocated {allocated}");
        }

        [Fact]
        [Trait("Rule", "S14-R3-5")]
        public void Given_ACountAtTheExactRemainingBytes_When_ReadBytesVectorAndReadFixedRootVector_Then_TheyReadEverythingAndNothingMore()
        {
            var reader = new SszReader(new byte[64]);
            var roots = reader.ReadFixedRootVector(2);
            Assert.Equal(2, roots.Count);
            Assert.Equal(0, reader.Remaining);

            var second = new SszReader(new byte[96]);
            Assert.Equal(2, second.ReadBytesVector(2, 48).Count);
        }

        [Fact]
        [Trait("Rule", "S14-R3-5")]
        public void Given_ANegativeOrOverflowingCount_When_ReadBytesVector_Then_ItThrowsWithoutAllocating()
        {
            var (allocated, thrown) = Measure(() =>
            {
                var reader = new SszReader(new byte[32]);
                reader.ReadBytesVector(int.MaxValue, int.MaxValue);
            });

            Assert.NotNull(thrown);
            Assert.True(allocated < Budget);

            var (allocatedNegative, _) = Measure(() =>
            {
                var reader = new SszReader(new byte[32]);
                reader.ReadBytesVector(-1, 32);
            });

            Assert.True(allocatedNegative < Budget);
        }
    }
}
