using System;
using Nethereum.Consensus.Ssz;
using Nethereum.Ssz;
using Xunit;

namespace Nethereum.Consensus.Ssz.Tests.SecurityAudit71.S14.Round2
{
    public class ConsensusSszRound2S14Tests
    {
        private const int DeclaredCount = 1 << 24;
        private const long Budget = 1L << 20;

        private static long Allocated(Action action)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            Record.Exception(action);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Fact]
        [Trait("Rule", "S14-R2-7")]
        public void Given_AZeroLengthElementVectorOfSixteenMillionOverAnEmptyBuffer_When_ReadBytesVector_Then_ItIsRejectedWithoutLooping()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();

            var allocated = Allocated(() =>
            {
                var reader = new SszReader(new byte[0]);
                reader.ReadBytesVector(DeclaredCount, 0);
            });

            Assert.True(allocated < Budget, $"allocated {allocated} bytes in {clock.ElapsedMilliseconds} ms");
        }

        [Fact]
        [Trait("Rule", "S14-R2-7")]
        public void Given_ABranchCountOfSixteenMillionOverThirtyTwoBytes_When_ReadFixedRootVector_Then_ItThrowsWithBoundedAllocation()
        {
            var thrown = default(Exception);
            var allocated = Allocated(() =>
            {
                var reader = new SszReader(new byte[32]);
                try { reader.ReadFixedRootVector(DeclaredCount); }
                catch (Exception ex) { thrown = ex; throw; }
            });

            Assert.IsType<InvalidOperationException>(thrown);
            Assert.True(allocated < Budget);
        }

        [Fact]
        [Trait("Rule", "S14-R2-7")]
        public void Given_AnElementCountOfSixteenMillionOverThirtyTwoBytes_When_ReadBytesVector_Then_ItThrowsWithBoundedAllocation()
        {
            var allocated = Allocated(() =>
            {
                var reader = new SszReader(new byte[32]);
                reader.ReadBytesVector(DeclaredCount, 32);
            });

            Assert.True(allocated < Budget);
        }
    }
}
