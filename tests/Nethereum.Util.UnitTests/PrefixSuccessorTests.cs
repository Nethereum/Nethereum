using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class PrefixSuccessorTests
    {
        [Fact]
        public void Given_AllFfPrefix_When_PrefixSuccessorIsComputed_Then_ItIsNull()
        {
            Assert.Null(ByteUtil.PrefixSuccessor(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }));
        }

        [Fact]
        public void Given_ATrailingFfPrefix_When_PrefixSuccessorIsComputed_Then_TheCarryPropagatesAndTheTailIsZero()
        {
            Assert.Equal(new byte[] { 0x01, 0x03, 0x00, 0x00 }, ByteUtil.PrefixSuccessor(new byte[] { 0x01, 0x02, 0xFF, 0xFF }));
            Assert.Equal(new byte[] { 0x00, 0x01 }, ByteUtil.PrefixSuccessor(new byte[] { 0x00, 0x00 }));
            Assert.Equal(new byte[] { 0xFF, 0x00 }, ByteUtil.PrefixSuccessor(new byte[] { 0xFE, 0xFF }));
        }

        [Fact]
        public void Given_AnEmptyPrefix_When_PrefixSuccessorIsComputed_Then_ItIsNull()
        {
            Assert.Null(ByteUtil.PrefixSuccessor(new byte[0]));
        }

        [Fact]
        public void Given_APrefix_When_PrefixSuccessorIsComputed_Then_TheInputIsNotMutated()
        {
            var prefix = new byte[] { 0x01, 0xFF };
            var successor = ByteUtil.PrefixSuccessor(prefix);
            Assert.Equal(new byte[] { 0x01, 0xFF }, prefix);
            Assert.NotSame(prefix, successor);
        }
    }
}
