using System;
using Xunit;

namespace Nethereum.Freezer.UnitTests
{
    public class FreezerIndexEntryTests
    {
        [Fact]
        public void Given_KnownEntryBytes_When_ReadFrom_Then_DecodesFilenumAndOffset()
        {
            byte[] bytes = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x05 };

            var entry = FreezerIndexEntry.ReadFrom(bytes);

            Assert.Equal((ushort)0, entry.FileNumber);
            Assert.Equal((uint)5, entry.Offset);
        }

        [Fact]
        public void Given_Entry_When_WriteTo_Then_ByteIdentical()
        {
            var entry = new FreezerIndexEntry(0, 5);
            var destination = new byte[FreezerIndexEntry.Size];

            entry.WriteTo(destination);

            Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x05 }, destination);
        }

        [Fact]
        public void Given_RealGethEntry_When_ReadFrom_Then_DecodesFilenum0Offset199()
        {
            byte[] bytes = { 0x00, 0x00, 0x00, 0x00, 0x00, 0xC7 };

            var entry = FreezerIndexEntry.ReadFrom(bytes);

            Assert.Equal((ushort)0, entry.FileNumber);
            Assert.Equal((uint)199, entry.Offset);
        }

        [Fact]
        public void Given_LittleEndianBytes_When_ReadFrom_Then_NotEqualBigEndianValue()
        {
            byte[] bytes = { 0x01, 0x02, 0x00, 0x00, 0x01, 0x00 };

            var entry = FreezerIndexEntry.ReadFrom(bytes);

            Assert.Equal((ushort)0x0102, entry.FileNumber);
            Assert.Equal((uint)0x00000100, entry.Offset);
            Assert.NotEqual((ushort)0x0201, entry.FileNumber);
            Assert.NotEqual((uint)0x00010000, entry.Offset);
        }

        [Fact]
        public void Given_SourceTooShort_When_ReadFrom_Then_Throws()
        {
            byte[] tooShort = { 0x00, 0x00, 0x00, 0x00, 0x00 };

            Assert.ThrowsAny<Exception>(() => FreezerIndexEntry.ReadFrom(tooShort));
        }
    }
}
