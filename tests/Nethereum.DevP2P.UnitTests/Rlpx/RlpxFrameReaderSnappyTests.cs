using Nethereum.DevP2P.Rlpx;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Rlpx
{
    public class RlpxFrameReaderSnappyTests
    {
        [Fact]
        public void Given_SnappyVarint_When_TryReadDecompressedLength_Then_DecodesCorrectly()
        {
            Assert.True(RlpxFrameReader.TryReadSnappyDecompressedLength(new byte[] { 0x05 }, out var v));
            Assert.Equal(5, v);

            Assert.True(RlpxFrameReader.TryReadSnappyDecompressedLength(new byte[] { 0x80, 0x01 }, out v));
            Assert.Equal(128, v);

            Assert.True(RlpxFrameReader.TryReadSnappyDecompressedLength(new byte[] { 0xAC, 0x02 }, out v));
            Assert.Equal(300, v);
        }

        [Fact]
        public void Given_SnappyHeaderClaimsAboveMaxDecompressedFrameSize_When_Parsed_Then_DetectedBeforeDecode()
        {
            int oneByteOverCap = RlpxFrameReader.MaxDecompressedFrameSize + 1;
            var varint = EncodeVarint(oneByteOverCap);

            Assert.True(RlpxFrameReader.TryReadSnappyDecompressedLength(varint, out var v));
            Assert.True(v > RlpxFrameReader.MaxDecompressedFrameSize,
                $"varint should decode > MaxDecompressedFrameSize; got {v}");
        }

        [Fact]
        public void Given_SnappyBombHeaderClaimsGigabytes_When_Parsed_Then_RejectedByCap()
        {
            const long oneGiB = 1L * 1024 * 1024 * 1024;
            var varint = EncodeVarint(oneGiB);

            Assert.True(RlpxFrameReader.TryReadSnappyDecompressedLength(varint, out var v));
            Assert.Equal(oneGiB, v);
            Assert.True(v > RlpxFrameReader.MaxDecompressedFrameSize);
        }

        [Fact]
        public void Given_EmptyBody_When_Parsed_Then_ReturnsFalse()
        {
            Assert.False(RlpxFrameReader.TryReadSnappyDecompressedLength(System.Array.Empty<byte>(), out _));
            Assert.False(RlpxFrameReader.TryReadSnappyDecompressedLength(null, out _));
        }

        [Fact]
        public void Given_TruncatedVarintWithHighBitSetAllBytes_When_Parsed_Then_ReturnsFalse()
        {
            var truncated = new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80 };
            Assert.False(RlpxFrameReader.TryReadSnappyDecompressedLength(truncated, out _));
        }

        private static byte[] EncodeVarint(long v)
        {
            var bytes = new System.Collections.Generic.List<byte>();
            while (v >= 0x80)
            {
                bytes.Add((byte)((v & 0x7F) | 0x80));
                v >>= 7;
            }
            bytes.Add((byte)v);
            return bytes.ToArray();
        }
    }
}
