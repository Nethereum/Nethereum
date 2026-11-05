using System;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.RLP.UnitTests
{
    public class RlpNonCanonicalEncodingGethDivergenceSecurityTests
    {
        private static string Repeat(string hexByte, int count) =>
            string.Concat(Enumerable.Repeat(hexByte, count));

        private static void AssertRejected(string hex) =>
            Assert.ThrowsAny<Exception>(() => RLP.Decode(hex.HexToByteArray()));

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_ASingleByteBelow0x80WrappedInAOneByteStringPrefix_When_Decoded_Then_ItIsRejectedAsNonCanonicalSize()
        {
            AssertRejected("8105");
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_AStringUnder56BytesUsingTheLongForm_When_Decoded_Then_ItIsRejectedAsNonCanonicalSize()
        {
            AssertRejected("b803aabbcc");
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_AListUnder56BytesUsingTheLongForm_When_Decoded_Then_ItIsRejectedAsNonCanonicalSize()
        {
            AssertRejected("f803010203");
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_AStringWhoseLengthOfLengthCarriesALeadingZero_When_Decoded_Then_ItIsRejectedAsNonCanonicalSize()
        {
            AssertRejected("b90038" + Repeat("11", 56));
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_AListWhoseLengthOfLengthCarriesALeadingZero_When_Decoded_Then_ItIsRejectedAsNonCanonicalSize()
        {
            AssertRejected("f90038" + Repeat("01", 56));
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_AFiveByteLengthOfLengthClaimingMoreThan4GiB_When_Decoded_Then_ItIsRejectedAsLargerThanTheInput()
        {
            AssertRejected("bc0100000003aabbccdd");
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_AnEightByteLengthOfLengthClaimingMoreThan4GiB_When_Decoded_Then_ItIsRejectedAsLargerThanTheInput()
        {
            AssertRejected("ff000000010000000301020304");
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_BytesAfterTheTopLevelItem_When_DecodedAsASingleValue_Then_ItIsRejected()
        {
            AssertRejected("c20102" + "03");
        }
    }
}
