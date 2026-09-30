using System;
using System.IO;
using Nethereum.RLP;
using Xunit;

namespace Nethereum.Freezer.UnitTests
{
    public class FreezerTableMetaTests
    {
        private static readonly string FixturesDirectory =
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "geth-ancient");

        private static readonly byte[] RealHeadersMetaBytes =
            { 0xC7, 0x02, 0x80, 0x84, 0x09, 0x40, 0xE3, 0x8A };

        [Fact]
        public void Given_GethMetaBytes_When_Decode_Then_Version2_Tail0_Flush155247498()
        {
            var meta = FreezerTableMeta.Decode(RealHeadersMetaBytes);

            Assert.Equal((ushort)2, meta.Version);
            Assert.Equal(0L, meta.VirtualTail);
            Assert.Equal(155247498L, meta.FlushOffset);
        }

        [Fact]
        public void Given_RealHeadersMetaFile_When_Decode_Then_MatchesFixtureBytes()
        {
            var bytes = File.ReadAllBytes(Path.Combine(FixturesDirectory, "headers.meta"));

            var meta = FreezerTableMeta.Decode(bytes);

            Assert.Equal((ushort)2, meta.Version);
            Assert.Equal(0L, meta.VirtualTail);
            Assert.True(meta.FlushOffset > 0);
            Assert.Equal(RealHeadersMetaBytes, bytes);
        }

        [Fact]
        public void Given_Meta_When_Encode_Then_ByteIdentical()
        {
            var meta = new FreezerTableMeta(2, 0, 155247498);

            var encoded = meta.Encode();

            Assert.Equal(RealHeadersMetaBytes, encoded);
        }

        [Fact]
        public void Given_V1MetaBytes_When_Decode_Then_ThrowsFreezerFormatException()
        {
            byte[] v1Bytes = { 0xC7, 0x01, 0x80, 0x84, 0x09, 0x40, 0xE3, 0x8A };

            Assert.Throws<FreezerFormatException>(() => FreezerTableMeta.Decode(v1Bytes));
        }

        [Fact]
        public void Given_OffsetAboveInt64_When_Decode_Then_ThrowsFreezerFormatException()
        {
            var fields = new byte[][]
            {
                ((int)2).ToBytesForRLPEncoding(),
                ((ulong)0).ToBytesForRLPEncoding(),
                ulong.MaxValue.ToBytesForRLPEncoding(),
            };
            var bytes = RLP.RLP.EncodeDataItemsAsElementOrListAndCombineAsList(fields);

            Assert.Throws<FreezerFormatException>(() => FreezerTableMeta.Decode(bytes));
        }

        [Fact]
        public void Given_WrongFieldCount_When_Decode_Then_ThrowsFreezerFormatException()
        {
            var fields = new byte[][]
            {
                ((int)2).ToBytesForRLPEncoding(),
                ((ulong)0).ToBytesForRLPEncoding(),
            };
            var bytes = RLP.RLP.EncodeDataItemsAsElementOrListAndCombineAsList(fields);

            Assert.Throws<FreezerFormatException>(() => FreezerTableMeta.Decode(bytes));
        }

        [Fact]
        public void Given_NewMeta_When_WriteAtomicThenDecode_Then_RoundTrips()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                var meta = new FreezerTableMeta(2, 10, 60);

                meta.WriteAtomic(path);
                var reopened = FreezerTableMeta.Decode(File.ReadAllBytes(path));

                Assert.Equal(2, reopened.Version);
                Assert.Equal(10L, reopened.VirtualTail);
                Assert.Equal(60L, reopened.FlushOffset);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private sealed class FailingMidWriteMeta : FreezerTableMeta
        {
            public FailingMidWriteMeta(ushort version, long virtualTail, long flushOffset)
                : base(version, virtualTail, flushOffset)
            {
            }

            protected override void WriteTempContent(Stream stream, byte[] bytes)
            {
                stream.Write(bytes, 0, bytes.Length / 2);
                throw new IOException("simulated crash mid-write");
            }
        }

        [Fact]
        public void Given_TornMetaWrite_When_Reopen_Then_PriorMetaIntact()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var tempPath = path + ".tmp";
            try
            {
                var original = new FreezerTableMeta(2, 0, 30);
                original.WriteAtomic(path);

                var failingWrite = new FailingMidWriteMeta(2, 0, 90);
                Assert.Throws<IOException>(() => failingWrite.WriteAtomic(path));

                var reopened = FreezerTableMeta.Decode(File.ReadAllBytes(path));
                Assert.Equal(30L, reopened.FlushOffset);
                Assert.False(File.Exists(tempPath), "a failed WriteAtomic must not leave a stale .tmp file behind");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
    }
}
