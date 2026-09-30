using Xunit;

namespace Nethereum.RLP.UnitTests
{
    public class CanonicalScalarEncodingTests
    {
        [Fact]
        public void Given_Zero_When_EncodedByTheLibrary_Then_ItIsTheEmptyStringNotASingleZeroByte()
        {
            Assert.Empty(0L.ToBytesForRLPEncoding());
            Assert.True(RlpScalar.IsCanonical(0L.ToBytesForRLPEncoding()));
        }

        [Fact]
        public void Given_AValueWhoseLowByteIsZero_When_EncodedByTheLibrary_Then_NoLeadingZeroSurvives()
        {
            Assert.Equal(new byte[] { 0x01, 0x00 }, 256L.ToBytesForRLPEncoding());
            Assert.True(RlpScalar.IsCanonical(256L.ToBytesForRLPEncoding()));
        }

        [Fact]
        public void Given_HandPackedBytesWithALeadingZero_When_Trimmed_Then_TheValueSurvivesAndTheScalarIsCanonical()
        {
            var handPacked = new byte[] { 0x00, 0x05 };
            var trimmed = handPacked.TrimZeroBytes();

            Assert.Equal(new byte[] { 0x05 }, trimmed);
            Assert.Equal(handPacked.ToBigIntegerFromRLPDecoded(), trimmed.ToBigIntegerFromRLPDecoded());
            Assert.True(RlpScalar.IsCanonical(trimmed));
        }

        [Fact]
        public void Given_ASingleZeroByte_When_Trimmed_Then_ItBecomesTheEmptyStringAndStillDecodesToZero()
        {
            var trimmed = new byte[] { 0x00 }.TrimZeroBytes();

            Assert.Empty(trimmed);
            Assert.Equal(0, trimmed.ToBigIntegerFromRLPDecoded());
            Assert.True(RlpScalar.IsCanonical(trimmed));
        }

        [Fact]
        public void Given_ASingleZeroByteWrittenByHand_When_CheckedAsAScalar_Then_ItIsRejected()
        {
            Assert.False(RlpScalar.IsCanonical(new byte[] { 0x00 }));
        }

        [Fact]
        public void Given_AnAbsentItem_When_CheckedAsAScalar_Then_ItIsTheCanonicalZero()
        {
            Assert.True(RlpScalar.IsCanonical(null));
        }
    }
}
