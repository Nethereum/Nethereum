using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class ByteUtilEqualityTests
    {
        [Fact]
        public void Given_NullAndEmptyArrays_When_AreEqualIsCalled_Then_NullDoesNotEqualEmptyAndNullEqualsNull()
        {
            Assert.True(ByteUtil.AreEqual(null, null));
            Assert.False(ByteUtil.AreEqual(null, new byte[0]));
            Assert.False(ByteUtil.AreEqual(new byte[0], null));
            Assert.True(ByteUtil.AreEqual(new byte[0], new byte[0]));
        }

        [Fact]
        public void Given_DataShorterThanPrefix_When_StartsWithIsCalled_Then_ItIsFalse()
        {
            Assert.False(ByteUtil.StartsWith(new byte[] { 1, 2 }, new byte[] { 1, 2, 3 }));
            Assert.True(ByteUtil.StartsWith(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }));
            Assert.True(ByteUtil.StartsWith(new byte[] { 1, 2, 3 }, new byte[0]));
        }

        [Fact]
        public void Given_ANullPrefix_When_StartsWithIsCalled_Then_ItIsFalse()
        {
            Assert.False(ByteUtil.StartsWith(new byte[] { 1, 2 }, null));
            Assert.False(ByteUtil.StartsWith(null, new byte[] { 1 }));
        }
    }
}
