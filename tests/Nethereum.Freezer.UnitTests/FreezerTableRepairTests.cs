using System;
using System.IO;
using Xunit;

namespace Nethereum.Freezer.UnitTests
{
    public class FreezerTableRepairTests
    {
        private static readonly string FixturesDirectory =
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "geth-ancient");

        private sealed class PassthroughByteCodec : IItemCodec<byte[]>
        {
            public byte[] Encode(byte[] item) => item;
            public byte[] Decode(ReadOnlySpan<byte> bytes) => bytes.ToArray();
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }

        private static void CopyHeadersIndexAndData(string destDir)
        {
            File.Copy(Path.Combine(FixturesDirectory, "headers.cidx"), Path.Combine(destDir, "headers.cidx"));
            File.Copy(Path.Combine(FixturesDirectory, "headers.0000.cdat"), Path.Combine(destDir, "headers.0000.cdat"));
        }

        private static void CopyRealMeta(string destDir)
        {
            File.Copy(Path.Combine(FixturesDirectory, "headers.meta"), Path.Combine(destDir, "headers.meta"));
        }

        private static void TruncateFile(string path, long length)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
            stream.SetLength(length);
        }

        private static uint ProbeItem1000Offset()
        {
            using var probe = FreezerIndex.OpenReadOnly(Path.Combine(FixturesDirectory, "headers.cidx"));
            return probe.EntryAt(1000).Offset;
        }

        private static FreezerTable<byte[]> OpenForAppend(string dir) =>
            FreezerTable<byte[]>.OpenForAppend(
                new FreezerTablePaths(dir, "headers", useCompression: true),
                new SnappyItemCodec<byte[]>(new PassthroughByteCodec()),
                RollingDataFiles.DefaultMaxFileSize);

        private static FreezerTable<byte[]> OpenReadOnly(string dir) =>
            FreezerTable<byte[]>.OpenReadOnly(
                new FreezerTablePaths(dir, "headers", useCompression: true),
                new SnappyItemCodec<byte[]>(new PassthroughByteCodec()));


        [Fact]
        public void Given_IndexLongerThanFlushOffset_When_Repair_Then_TruncatesIndexDown()
        {
            var dir = CreateTempDirectory();
            try
            {
                CopyHeadersIndexAndData(dir);
                new FreezerTableMeta(FreezerTableMeta.SupportedVersion, virtualTail: 0, flushOffset: 1001 * FreezerIndexEntry.Size)
                    .WriteAtomic(Path.Combine(dir, "headers.meta"));

                using (var table = OpenForAppend(dir))
                    Assert.Equal(1000L, table.Count);

                var meta = FreezerTableMeta.Decode(File.ReadAllBytes(Path.Combine(dir, "headers.meta")));
                Assert.Equal(1001L * FreezerIndexEntry.Size, meta.FlushOffset);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_IndexLongerThanFlushOffset_When_OpenReadOnly_Then_ThrowsFreezerValidationException()
        {
            var dir = CreateTempDirectory();
            try
            {
                CopyHeadersIndexAndData(dir);
                new FreezerTableMeta(FreezerTableMeta.SupportedVersion, virtualTail: 0, flushOffset: 1001 * FreezerIndexEntry.Size)
                    .WriteAtomic(Path.Combine(dir, "headers.meta"));

                Assert.Throws<FreezerValidationException>(() => OpenReadOnly(dir).Dispose());
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }


        [Fact]
        public void Given_IndexShorterThanFlushOffset_When_Repair_Then_RewindsFlushOffset()
        {
            var dir = CreateTempDirectory();
            try
            {
                CopyHeadersIndexAndData(dir);
                CopyRealMeta(dir);

                using (var table = OpenForAppend(dir))
                    Assert.Equal(2047L, table.Count);

                var meta = FreezerTableMeta.Decode(File.ReadAllBytes(Path.Combine(dir, "headers.meta")));
                Assert.Equal(2048L * FreezerIndexEntry.Size, meta.FlushOffset);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_IndexShorterThanFlushOffset_When_Validate_Then_SilentlyAccepted()
        {
            var dir = CreateTempDirectory();
            try
            {
                CopyHeadersIndexAndData(dir);
                CopyRealMeta(dir);

                var exception = Record.Exception(() => OpenReadOnly(dir).Dispose());

                Assert.Null(exception);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }


        [Fact]
        public void Given_DataFileShorterThanIndex_When_Repair_Then_WalksIndexBackAcrossFiles()
        {
            var dir = CreateTempDirectory();
            try
            {
                CopyHeadersIndexAndData(dir);
                CopyRealMeta(dir);
                TruncateFile(Path.Combine(dir, "headers.0000.cdat"), ProbeItem1000Offset());

                using (var table = OpenForAppend(dir))
                {
                    Assert.Equal(1000L, table.Count);
                    Assert.Equal(0xf9, table.Read(0)[0]);
                }

                var meta = FreezerTableMeta.Decode(File.ReadAllBytes(Path.Combine(dir, "headers.meta")));
                Assert.Equal(1001L * FreezerIndexEntry.Size, meta.FlushOffset);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_DataFileShorterThanIndex_When_OpenReadOnly_Then_ThrowsFreezerValidationException()
        {
            var dir = CreateTempDirectory();
            try
            {
                CopyHeadersIndexAndData(dir);
                CopyRealMeta(dir);
                TruncateFile(Path.Combine(dir, "headers.0000.cdat"), ProbeItem1000Offset());

                Assert.Throws<FreezerValidationException>(() => OpenReadOnly(dir).Dispose());
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }


        private static void CorruptEntryToViolateMonotonicity(string cidxPath, long itemNumber)
        {
            using var stream = new FileStream(cidxPath, FileMode.Open, FileAccess.Write, FileShare.None);
            var corrupted = new byte[FreezerIndexEntry.Size];
            new FreezerIndexEntry(0, 1).WriteTo(corrupted);
            stream.Position = itemNumber * FreezerIndexEntry.Size;
            stream.Write(corrupted, 0, corrupted.Length);
        }

        [Fact]
        public void Given_IndexDisorder_When_RepairAppendMode_Then_TruncatesSilently_NotThrows()
        {
            var dir = CreateTempDirectory();
            try
            {
                CopyHeadersIndexAndData(dir);
                CopyRealMeta(dir);
                CorruptEntryToViolateMonotonicity(Path.Combine(dir, "headers.cidx"), 1000);

                Exception exception = null;
                long count = -1;
                try
                {
                    using var table = OpenForAppend(dir);
                    count = table.Count;
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                Assert.Null(exception);
                Assert.Equal(999L, count);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_IndexDisorder_When_OpenReadOnly_Then_ThrowsFreezerValidationException()
        {
            var dir = CreateTempDirectory();
            try
            {
                CopyHeadersIndexAndData(dir);
                CopyRealMeta(dir);
                CorruptEntryToViolateMonotonicity(Path.Combine(dir, "headers.cidx"), 1000);

                Assert.Throws<FreezerValidationException>(() => OpenReadOnly(dir).Dispose());
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }
    }
}
