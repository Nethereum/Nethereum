using System;
using System.Linq;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapHashRangesTests
    {
        [Fact]
        public void Given_AllFfHash_When_IncrementHashIsCalled_Then_ItWrapsToZeros()
        {
            var allFf = Enumerable.Repeat((byte)0xFF, 32).ToArray();

            var next = SnapHashRanges.IncrementHash(allFf);

            Assert.Equal(new byte[32], next);
            Assert.All(allFf, b => Assert.Equal(0xFF, b));
        }

        [Fact]
        public void Given_AHashEndingInFf_When_IncrementHashIsCalled_Then_TheCarryPropagatesAndTheInputIsNotMutated()
        {
            var hash = new byte[32];
            hash[30] = 0x01;
            hash[31] = 0xFF;

            var next = SnapHashRanges.IncrementHash(hash);

            var expected = new byte[32];
            expected[30] = 0x02;
            Assert.Equal(expected, next);
            Assert.Equal((byte)0xFF, hash[31]);
        }

        [Fact]
        public void Given_NoInput_When_MaxHashIsCalled_Then_ItIsThirtyTwoFfBytesAndEachCallIsAFreshArray()
        {
            var first = SnapHashRanges.MaxHash();
            first[0] = 0x00;
            var second = SnapHashRanges.MaxHash();

            Assert.Equal(Enumerable.Repeat((byte)0xFF, 32).ToArray(), second);
            Assert.Equal(SnapHashRanges.FilledHash(0xff), second);
        }

        [Fact]
        public void Given_NoInput_When_MinHashIsCalled_Then_ItIsThirtyTwoZeroBytesAndEachCallIsAFreshArray()
        {
            var first = SnapHashRanges.MinHash();
            first[31] = 0x01;
            var second = SnapHashRanges.MinHash();

            Assert.Equal(new byte[32], second);
        }

        [Fact]
        public void Given_MinAndMaxHash_When_TheFullRangeIsSplit_Then_TheChunksCoverItEndToEnd()
        {
            var ranges = SnapHashRanges.SplitHashRange(SnapHashRanges.MinHash(), SnapHashRanges.MaxHash(), 4);

            Assert.Equal(4, ranges.Count);
            Assert.Equal(SnapHashRanges.MinHash(), ranges[0].Start);
            Assert.Equal(SnapHashRanges.MaxHash(), ranges[3].End);
        }
    }
}
