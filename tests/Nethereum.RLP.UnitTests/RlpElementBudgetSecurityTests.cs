using System;
using Xunit;

namespace Nethereum.RLP.UnitTests
{
    public class RlpElementBudgetSecurityTests
    {
        private static byte[] ListFilledWith(byte element, int payloadLength)
        {
            var encoded = new byte[payloadLength + 4];
            encoded[0] = 0xfa;
            encoded[1] = (byte)(payloadLength >> 16);
            encoded[2] = (byte)(payloadLength >> 8);
            encoded[3] = (byte)payloadLength;
            for (var i = 4; i < encoded.Length; i++) encoded[i] = element;
            return encoded;
        }

        [Theory]
        [InlineData(0xc0)]
        [InlineData(0x01)]
        [InlineData(0x80)]
        [Trait("Category", "SecurityFinding")]
        public void Given_AFrameSizedFloodOfTinyElements_When_DecodedWithTheUntrustedBudget_Then_ItIsRejectedAndAllocationStaysNearTheInputCopy(int element)
        {
            var encoded = ListFilledWith((byte)element, 15 * 1024 * 1024);
            var before = GC.GetAllocatedBytesForCurrentThread();

            var ex = Record.Exception(() => RLP.Decode(encoded, RLP.UntrustedElementBudget(encoded.Length)));

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.NotNull(ex);
            Assert.True(allocated <= 3L * encoded.Length, $"allocated {allocated} for {encoded.Length} input bytes");
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_ACollectionOverTheBudget_When_DecodedWithThatBudget_Then_ItIsRejected()
        {
            var encoded = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            Assert.ThrowsAny<Exception>(() => RLP.DecodeCollection(encoded, 3));
            Assert.Equal(4, ((RLPCollection)RLP.DecodeCollection(encoded, 4)).Count);
        }

        [Fact]
        public void Given_ADenseButValidPayload_When_DecodedWithoutABudget_Then_ItDecodes()
        {
            var encoded = ListFilledWith(0x01, 100000);
            var decoded = (RLPCollection)RLP.Decode(encoded);
            Assert.Equal(100000, decoded.Count);
        }
    }
}
