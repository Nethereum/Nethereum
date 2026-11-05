using System;
using Nethereum.Model.SSZ;
using Xunit;

namespace Nethereum.Model.SSZ.Tests.SecurityAudit71.S14.Round3
{
    public class ModelSszRound3S14Tests
    {
        private static byte[] Record(byte selector, uint payloadLength, uint signatureLength, int actualPayloadBytes, int actualSignatureBytes)
        {
            var bytes = new byte[1 + 4 + actualPayloadBytes + 4 + actualSignatureBytes];
            bytes[0] = selector;
            BitConverter.GetBytes(payloadLength).CopyTo(bytes, 1);
            BitConverter.GetBytes(signatureLength).CopyTo(bytes, 5 + actualPayloadBytes);
            return bytes;
        }

        [Theory]
        [InlineData(0xFFFFFFFFu, 0u, 0, 0)]
        [InlineData(0x80000000u, 0u, 0, 0)]
        [InlineData(0x7FFFFFFFu, 0u, 8, 0)]
        [InlineData(4u, 0xFFFFFFFFu, 4, 0)]
        [InlineData(4u, 0x80000000u, 4, 16)]
        [InlineData(0u, 0xFFFFFFFCu, 0, 0)]
        [Trait("Rule", "S14-R3-6")]
        public void Given_DeclaredPayloadOrSignatureLengthsBeyondTheRecord_When_ParseTransaction_Then_ItThrowsBeforeAllocating(
            uint payloadLength, uint signatureLength, int payloadBytes, int signatureBytes)
        {
            var record = Record(1, payloadLength, signatureLength, payloadBytes, signatureBytes);
            var before = GC.GetAllocatedBytesForCurrentThread();

            var thrown = Xunit.Record.Exception(() => SszTransactionEncoder.Current.ParseTransaction(record, out _, out _, out _));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsType<ArgumentException>(thrown);
            Assert.True(allocated < 64 * 1024, $"allocated {allocated}");
        }

        [Fact]
        [Trait("Rule", "S14-R3-6")]
        public void Given_ARecordWithExactLengths_When_ParseTransaction_Then_PayloadAndSignatureAreReturned()
        {
            var record = Record(2, 3, 2, 3, 2);
            record[5] = 9;
            record[5 + 3 + 4] = 7;

            SszTransactionEncoder.Current.ParseTransaction(record, out var selector, out var payload, out var signature);

            Assert.Equal(2, selector);
            Assert.Equal(3, payload.Length);
            Assert.Equal(9, payload[0]);
            Assert.Equal(2, signature.Length);
            Assert.Equal(7, signature[0]);
        }
    }
}
