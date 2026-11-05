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

        private static FreezerTablePaths WriteSequentialIndexWithGarbageEntry(string dir, int items, int garbageItem)
        {
            var paths = new FreezerTablePaths(dir, "synthetic", useCompression: false);
            var index = new byte[(items + 1) * FreezerIndexEntry.Size];
            for (var entry = 0; entry <= items; entry++)
                new FreezerIndexEntry(0, (uint)entry).WriteTo(index.AsSpan(entry * FreezerIndexEntry.Size));
            if (garbageItem > 0)
                new FreezerIndexEntry(7, 0).WriteTo(index.AsSpan(garbageItem * FreezerIndexEntry.Size));
            File.WriteAllBytes(paths.IndexPath, index);
            File.WriteAllBytes(Path.Combine(dir, "synthetic.0000.rdat"), new byte[items]);
            return paths;
        }

        [Fact]
        public void Given_AnIndexWithAGarbageEntryMidway_When_TheTableOpensForAppend_Then_ItTruncatesAtTheSameItemAsBefore()
        {
            var dir = CreateTempDirectory();
            try
            {
                var paths = WriteSequentialIndexWithGarbageEntry(dir, items: 5000, garbageItem: 2500);

                using var table = FreezerTable<byte[]>.OpenForAppend(
                    paths, new PassthroughByteCodec(), RollingDataFiles.DefaultMaxFileSize);

                Assert.Equal(2499L, table.Count);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_AnIndexWithAGarbageEntryMidway_When_TheTableOpensReadOnly_Then_ItThrowsFreezerValidationException()
        {
            var dir = CreateTempDirectory();
            try
            {
                var paths = WriteSequentialIndexWithGarbageEntry(dir, items: 5000, garbageItem: 2500);

                Assert.Throws<FreezerValidationException>(() =>
                    FreezerTable<byte[]>.OpenReadOnly(paths, new PassthroughByteCodec()).Dispose());
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_AnIndexOfAMillionItems_When_TheTableOpens_Then_IndexReadOperationsAreProportionalToBytesNotItems()
        {
            var dir = CreateTempDirectory();
            try
            {
                const int items = 1_000_000;
                var paths = WriteSequentialIndexWithGarbageEntry(dir, items, garbageItem: 0);
                var indexBytes = (items + 1L) * FreezerIndexEntry.Size;
                const long oneMebibyte = 1024 * 1024;
                const long smallConstant = 8;

                using var table = FreezerTable<byte[]>.OpenForAppend(
                    paths, new PassthroughByteCodec(), RollingDataFiles.DefaultMaxFileSize);

                Assert.Equal((long)items, table.Count);
                Assert.True(table.IndexReadOperationCount <= indexBytes / oneMebibyte + smallConstant,
                    $"index read operations {table.IndexReadOperationCount} exceed {indexBytes / oneMebibyte + smallConstant}");
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        private sealed class AbstractMembersOnlyIndex : IFreezerIndex
        {
            private readonly FreezerIndex _inner;
            public AbstractMembersOnlyIndex(FreezerIndex inner) { _inner = inner; }
            public long Count => _inner.Count;
            public long ByteSize => _inner.ByteSize;
            public FreezerIndexEntry EntryAt(long itemNumber) => _inner.EntryAt(itemNumber);
            public System.Collections.Generic.IReadOnlyList<FreezerIndexEntry> ReadEntries(long firstEntry, int maxEntries) => _inner.ReadEntries(firstEntry, maxEntries);
            public ItemRange RangeOf(long itemNumber) => _inner.RangeOf(itemNumber);
            public void Append(FreezerIndexEntry entry) => _inner.Append(entry);
            public void TruncateToItems(long items) => _inner.TruncateToItems(items);
            public long DurableItemCount(long flushOffset) => _inner.DurableItemCount(flushOffset);
            public void Sync() => _inner.Sync();
            public void Dispose() => _inner.Dispose();
        }

        [Fact]
        public void Given_AnIndexImplementationRelyingOnTheDefaultMember_When_ScanningAGarbageEntry_Then_ItFindsTheSameItemAsFreezerIndex()
        {
            var dir = CreateTempDirectory();
            try
            {
                var paths = WriteSequentialIndexWithGarbageEntry(dir, items: 5000, garbageItem: 2500);
                using var real = FreezerIndex.OpenReadOnly(paths.IndexPath);
                IFreezerIndex defaulted = new AbstractMembersOnlyIndex(real);

                Assert.Equal(2500L, real.FirstItemBreakingSequence());
                Assert.Equal(real.FirstItemBreakingSequence(), defaulted.FirstItemBreakingSequence());
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }
    }
}
