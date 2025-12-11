using System;
using Xunit;

namespace Nethereum.Ssz.Tests
{
    public class SszWriterReaderTests
    {
        [Fact]
        public void RoundTrip_Primitives()
        {
            var fixedBytes = new byte[32];
            fixedBytes[0] = 0xAA;
            fixedBytes[^1] = 0xBB;

            byte[] buffer;
            using (var writer = new SszWriter())
            {
                writer.WriteBoolean(true);
                writer.WriteUInt64(42);
                writer.WriteFixedBytes(fixedBytes, fixedBytes.Length);
                buffer = writer.ToArray();
            }

            var reader = new SszReader(buffer);
            Assert.True(reader.ReadBoolean());
            Assert.Equal((ulong)42, reader.ReadUInt64());
            Assert.Equal(fixedBytes, reader.ReadFixedBytes(32));
        }

        [Fact]
        public void WriteFixedBytes_Throws_When_LengthMismatch()
        {
            using var writer = new SszWriter();
            var data = new byte[4];

            Assert.Throws<ArgumentException>(() => writer.WriteFixedBytes(data, 32));
        }

        [Fact]
        public void WriteVariableBytes_Throws_When_LengthExceedsLimit()
        {
            using var writer = new SszWriter();
            var payload = new byte[8];

            Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteVariableBytes(payload, maxLength: 4));
        }

        [Fact]
        public void ReadVariableBytes_Throws_When_LengthExceedsLimit()
        {
            byte[] buffer;
            using (var writer = new SszWriter())
            {
                writer.WriteVariableBytes(new byte[] { 0x01, 0x02, 0x03 });
                buffer = writer.ToArray();
            }

            var reader = new SszReader(buffer);
            var threw = false;
            try
            {
                var localReader = reader;
                localReader.ReadVariableBytes(maxLength: 2);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.True(threw);
        }

        [Fact]
        public void List_RoundTrip_Works()
        {
            byte[] buffer;
            using (var writer = new SszWriter())
            {
                writer.WriteList(new[] { 10UL, 12UL }, (w, value) => w.WriteUInt64(value), maxLength: 4);
                buffer = writer.ToArray();
            }

            var reader = new SszReader(buffer);
            var values = SszReader.ReadList<ulong>(ref reader, 2);
            Assert.Equal(new[] { 10UL, 12UL }, values);
        }

        [Fact]
        public void Vector_Validates_ElementCount()
        {
            using var writer = new SszWriter();
            var vector = new[] { new byte[32], new byte[32] };
            Assert.Throws<ArgumentException>(() => writer.WriteVector(vector, 32, expectedElementCount: 1));
        }

    }
}
