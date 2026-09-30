using Nethereum.CoreChain;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class FlushCadenceTests
    {
        [Theory]
        [InlineData(1UL)]
        [InlineData(2UL)]
        [InlineData(3UL)]
        [InlineData(100UL)]
        [InlineData(0UL)]
        public void EveryBlockFlushCadence_AlwaysBoundary(ulong block)
        {
            var cadence = new EveryBlockFlushCadence();
            Assert.True(cadence.IsBoundary(block));
        }

        [Fact]
        public void FixedIntervalFlushCadence_KEqualsOne_AlwaysBoundary_MatchesEveryBlock()
        {
            var cadence = new FixedIntervalFlushCadence(1);
            for (ulong b = 0; b <= 10; b++)
                Assert.True(cadence.IsBoundary(b));
        }

        [Fact]
        public void FixedIntervalFlushCadence_KEqualsTwo_BoundaryOnlyAtEvenBlocks()
        {
            var cadence = new FixedIntervalFlushCadence(2);

            Assert.False(cadence.IsBoundary(1));
            Assert.True(cadence.IsBoundary(2));
            Assert.False(cadence.IsBoundary(3));
            Assert.True(cadence.IsBoundary(4));
            Assert.False(cadence.IsBoundary(599));
            Assert.True(cadence.IsBoundary(600));
        }

        [Fact]
        public void FixedIntervalFlushCadence_KEqualsFour_BoundaryOnlyAtMultiplesOfFour()
        {
            var cadence = new FixedIntervalFlushCadence(4);

            for (ulong b = 1; b <= 16; b++)
            {
                bool expected = b % 4 == 0;
                Assert.Equal(expected, cadence.IsBoundary(b));
            }
        }

        [Fact]
        public void FixedIntervalFlushCadence_RejectsKLessThanOne()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new FixedIntervalFlushCadence(0));
        }

        [Fact]
        public void FixedIntervalFlushCadence_BlockZero_IsBoundary()
        {
            var cadence = new FixedIntervalFlushCadence(2);
            Assert.True(cadence.IsBoundary(0));
        }
    }
}
