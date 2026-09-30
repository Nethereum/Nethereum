using System;
using System.IO;
using Xunit;

namespace Nethereum.Freezer.UnitTests
{
    public class SnappyItemCodecTests
    {
        private static readonly string FixturesDirectory =
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "geth-ancient");

        private static string HeadersCdatPath => Path.Combine(FixturesDirectory, "headers.0000.cdat");

        private sealed class PassthroughByteCodec : IItemCodec<byte[]>
        {
            public byte[] Encode(byte[] item) => item;
            public byte[] Decode(ReadOnlySpan<byte> bytes) => bytes.ToArray();
        }

        [Fact]
        public void Given_RealGethCdatFirstItem_When_CodecDecode_Then_ValidRlpHeader()
        {
            var compressed = new byte[199];
            using (var stream = new FileStream(HeadersCdatPath, FileMode.Open, FileAccess.Read))
            {
                var read = stream.Read(compressed, 0, compressed.Length);
                Assert.Equal(199, read);
            }

            var codec = new SnappyItemCodec<byte[]>(new PassthroughByteCodec());
            var decoded = codec.Decode(compressed);

            Assert.Equal(0xf9, decoded[0]);
            var declaredPayloadLength = (decoded[1] << 8) | decoded[2];
            Assert.Equal(decoded.Length, 3 + declaredPayloadLength);

            Assert.InRange(decoded.Length, 200, 560);
        }

        [Fact]
        public void Given_EmptyPayload_When_SnappyRoundTrip_Then_Identity()
        {
            var codec = new SnappyItemCodec<byte[]>(new PassthroughByteCodec());

            var encoded = codec.Encode(Array.Empty<byte>());
            var decoded = codec.Decode(encoded);

            Assert.Equal(Array.Empty<byte>(), decoded);
        }

        [Theory]
        [MemberData(nameof(RoundTripPayloads))]
        public void Given_Payload_When_SnappyRoundTrip_Then_Identity(byte[] payload)
        {
            var codec = new SnappyItemCodec<byte[]>(new PassthroughByteCodec());

            var encoded = codec.Encode(payload);
            var decoded = codec.Decode(encoded);

            Assert.Equal(payload, decoded);
        }

        public static TheoryData<byte[]> RoundTripPayloads()
        {
            return new TheoryData<byte[]>
            {
                new byte[] { 0x00 },
                new byte[] { 1, 2, 3, 4, 5 },
                new byte[1000],
                CreateRandomBytes(2048),
            };
        }

        private static byte[] CreateRandomBytes(int length)
        {
            var bytes = new byte[length];
            new Random(42).NextBytes(bytes);
            return bytes;
        }
    }
}
