using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class ByteArrayComparerTests
    {
        [Fact]
        public void Given_KeysWhereOneIsAPrefixOfTheOther_When_Compared_Then_TheShorterSortsFirst()
        {
            var shorter = new byte[] { 1, 2 };
            var longer = new byte[] { 1, 2, 0 };

            Assert.True(ByteArrayComparer.Current.Compare(shorter, longer) < 0);
            Assert.True(ByteArrayComparer.Current.Compare(longer, shorter) > 0);
            Assert.True(ByteArrayComparer.Current.Compare(new byte[0], new byte[] { 0 }) < 0);
        }

        [Fact]
        public void Given_BytesAbove0x7F_When_Compared_Then_OrderIsUnsignedBytewise()
        {
            var low = new byte[] { 0x7F };
            var high = new byte[] { 0x80 };
            var highest = new byte[] { 0xFF };

            Assert.True(ByteArrayComparer.Current.Compare(low, high) < 0);
            Assert.True(ByteArrayComparer.Current.Compare(high, highest) < 0);
            Assert.True(ByteArrayComparer.Current.Compare(highest, low) > 0);
            Assert.True(ByteArrayComparer.Current.Compare(new byte[] { 0x01, 0xFF }, new byte[] { 0x02, 0x00 }) < 0);
        }

        [Fact]
        public void Given_NullAndEmptyAndNull_When_Compared_Then_NullSortsBeforeEmptyAndTwoNullsAreEqual()
        {
            Assert.True(ByteArrayComparer.Current.Compare(null, new byte[0]) < 0);
            Assert.True(ByteArrayComparer.Current.Compare(new byte[0], null) > 0);
            Assert.Equal(0, ByteArrayComparer.Current.Compare(null, null));
            Assert.True(ByteArrayComparer.Current.Equals(null, null));
            Assert.False(ByteArrayComparer.Current.Equals(null, new byte[0]));
        }

        [Fact]
        public void Given_TwoEqualButDistinctArrays_When_HashedAndCompared_Then_HashesAndEqualityAgree()
        {
            var a = new byte[] { 9, 8, 7, 0, 255 };
            var b = new byte[] { 9, 8, 7, 0, 255 };

            Assert.NotSame(a, b);
            Assert.True(ByteArrayComparer.Current.Equals(a, b));
            Assert.Equal(ByteArrayComparer.Current.GetHashCode(a), ByteArrayComparer.Current.GetHashCode(b));
        }

        [Fact]
        public void Given_ArraysDifferingOnlyByTrailingZeroByte_When_Compared_Then_TheyAreNotEqual()
        {
            var a = new byte[] { 1 };
            var b = new byte[] { 1, 0 };

            Assert.False(ByteArrayComparer.Current.Equals(a, b));
            Assert.False(ByteArrayComparer.Current.Equals(b, a));
            Assert.NotEqual(0, ByteArrayComparer.Current.Compare(a, b));
        }

        [Fact]
        public void Given_ANullKey_When_GetHashCodeIsCalled_Then_ItReturnsZeroWithoutThrowing()
        {
            Assert.Equal(0, ByteArrayComparer.Current.GetHashCode(null));
        }
    }
}
