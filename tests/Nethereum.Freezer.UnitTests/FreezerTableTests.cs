using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Nethereum.Freezer.UnitTests
{
    public class FreezerTableTests
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

        [Fact]
        public void Given_RealGethHeadersTable_When_OpenReadOnly_Then_CountAndReadItem0DecodesHeaderRlp()
        {
            var paths = new FreezerTablePaths(FixturesDirectory, "headers", useCompression: true);
            var codec = new SnappyItemCodec<byte[]>(new PassthroughByteCodec());

            using var table = FreezerTable<byte[]>.OpenReadOnly(paths, codec);

            Assert.Equal(2047L, table.Count);
            var item0 = table.Read(0);
            Assert.Equal(0xf9, item0[0]);
        }

        [Fact]
        public void Given_AllSealedFiles_When_ReadAsSegments_Then_CoverEveryItemContiguouslyAndMatchPerItemReadByteForByte()
        {
            var dir = CreateTempDirectory();
            try
            {
                var paths = new FreezerTablePaths(dir, "seg", useCompression: false);
                using var table = FreezerTable<byte[]>.OpenForAppend(paths, new PassthroughByteCodec(), maxFileSize: 32);

                for (var i = 0; i < 40; i++)
                {
                    var item = new byte[5 + (i % 4)];
                    for (var b = 0; b < item.Length; b++) item[b] = (byte)(i * 7 + b);
                    table.Append(i, item);
                }

                Assert.True(table.SealedHead > 0, "expected sealed files after rolling");

                var all = new List<(long ItemNumber, byte[] Raw)>();
                var filesRead = 0;
                for (ushort f = 0; ; f++)
                {
                    IReadOnlyList<(long, byte[])> seg;
                    try { seg = table.ReadSealedSegment(f); }
                    catch (FreezerConsistencyException) { break; }
                    all.AddRange(seg);
                    filesRead++;
                }

                Assert.True(filesRead >= 2, "expected multiple sealed files so the file-cross boundary is exercised");
                Assert.Equal(table.SealedHead, all.Count);
                for (var k = 0; k < all.Count; k++)
                {
                    Assert.Equal((long)k, all[k].ItemNumber);
                    Assert.Equal(table.Read(all[k].ItemNumber), all[k].Raw);
                }
            }
            finally { DeleteDirectory(dir); }
        }

        [Fact]
        public void Given_ASealedSegment_When_DecodedInParallel_Then_MatchesSerialDecodeInOrder()
        {
            var dir = CreateTempDirectory();
            try
            {
                var paths = new FreezerTablePaths(dir, "pseg", useCompression: false);
                using var table = FreezerTable<byte[]>.OpenForAppend(paths, new PassthroughByteCodec(), maxFileSize: 32);
                for (var i = 0; i < 20; i++)
                {
                    var item = new byte[4 + (i % 5)];
                    for (var b = 0; b < item.Length; b++) item[b] = (byte)(i * 11 + b);
                    table.Append(i, item);
                }

                var parallel = FreezerParallelSegment.DecodeSealed(table, 0, maxDegreeOfParallelism: 4);

                Assert.NotEmpty(parallel);
                Assert.Equal(0L, parallel[0].ItemNumber);
                for (var k = 0; k < parallel.Count; k++)
                {
                    if (k > 0) Assert.Equal(parallel[k - 1].ItemNumber + 1, parallel[k].ItemNumber);
                    Assert.Equal(table.Read(parallel[k].ItemNumber), parallel[k].Decoded);
                }
            }
            finally { DeleteDirectory(dir); }
        }

        [Fact]
        public void Given_TwoTablesWithDifferentDop_When_DecodeSealedChunkEach_Then_EachUsesItsOwnConfiguredDopAndPreservesOrder()
        {
            const int dopA = 3;
            const int dopB = 7;
            Assert.NotEqual(dopA, dopB);
            Assert.Equal(dopA, FrozenParallelism.Resolve(dopA));
            Assert.Equal(dopB, FrozenParallelism.Resolve(dopB));

            var dirA = CreateTempDirectory();
            var dirB = CreateTempDirectory();
            try
            {
                var pathsA = new FreezerTablePaths(dirA, "byhash", useCompression: false);
                using var tableA = FreezerTable<byte[]>.OpenForAppend(pathsA, new PassthroughByteCodec(), maxFileSize: 32);
                for (var i = 0; i < 20; i++)
                {
                    var item = new byte[4 + (i % 5)];
                    for (var b = 0; b < item.Length; b++) item[b] = (byte)(i * 11 + b);
                    tableA.Append(i, item);
                }

                var pathsB = new FreezerTablePaths(dirB, "log", useCompression: false);
                using var tableB = FreezerTable<byte[]>.OpenForAppend(pathsB, new PassthroughByteCodec(), maxFileSize: 40);
                for (var i = 0; i < 25; i++)
                {
                    var item = new byte[3 + (i % 6)];
                    for (var b = 0; b < item.Length; b++) item[b] = (byte)(i * 5 + b);
                    tableB.Append(i, item);
                }

                string capturedA = null;
                string capturedB = null;
                IReadOnlyList<(long ItemNumber, byte[] Decoded)> decodedA;
                IReadOnlyList<(long ItemNumber, byte[] Decoded)> decodedB;
                try
                {
                    FreezerParallelSegment.StepLog = m => capturedA = m;
                    decodedA = FreezerParallelSegment.DecodeSealedChunk(
                        tableA, startItem: 0, targetBytes: 4096, endExclusive: tableA.SealedHead, maxDegreeOfParallelism: dopA);

                    FreezerParallelSegment.StepLog = m => capturedB = m;
                    decodedB = FreezerParallelSegment.DecodeSealedChunk(
                        tableB, startItem: 0, targetBytes: 4096, endExclusive: tableB.SealedHead, maxDegreeOfParallelism: dopB);
                }
                finally
                {
                    FreezerParallelSegment.StepLog = null;
                }

                Assert.NotNull(capturedA);
                Assert.Contains("dop=3", capturedA);
                Assert.NotNull(capturedB);
                Assert.Contains("dop=7", capturedB);

                Assert.NotEmpty(decodedA);
                Assert.NotEmpty(decodedB);

                for (var k = 0; k < decodedA.Count; k++)
                {
                    if (k > 0) Assert.Equal(decodedA[k - 1].ItemNumber + 1, decodedA[k].ItemNumber);
                    Assert.Equal(tableA.Read(decodedA[k].ItemNumber), decodedA[k].Decoded);
                }
                for (var k = 0; k < decodedB.Count; k++)
                {
                    if (k > 0) Assert.Equal(decodedB[k - 1].ItemNumber + 1, decodedB[k].ItemNumber);
                    Assert.Equal(tableB.Read(decodedB[k].ItemNumber), decodedB[k].Decoded);
                }
            }
            finally
            {
                DeleteDirectory(dirA);
                DeleteDirectory(dirB);
            }
        }

        [Fact]
        public void Given_ASealedRangeSpanningFiles_When_Read_Then_MatchesPerItemReadAndClampsAtSealedHead()
        {
            var dir = CreateTempDirectory();
            try
            {
                var paths = new FreezerTablePaths(dir, "rng", useCompression: false);
                using var table = FreezerTable<byte[]>.OpenForAppend(paths, new PassthroughByteCodec(), maxFileSize: 32);
                for (var i = 0; i < 40; i++)
                {
                    var item = new byte[5 + (i % 4)];
                    for (var b = 0; b < item.Length; b++) item[b] = (byte)(i * 7 + b);
                    table.Append(i, item);
                }

                var sealedHead = table.SealedHead;
                Assert.True(sealedHead >= 4, "need enough sealed items to span a file boundary");

                const long start = 3L;
                var window = table.ReadSealedRange(start, maxItems: 1000);

                Assert.Equal((int)(sealedHead - start), window.Count);
                Assert.Equal(start, window[0].ItemNumber);
                var crossedFile = false;
                var prevFile = table.FileNumberOf(window[0].ItemNumber);
                for (var k = 0; k < window.Count; k++)
                {
                    Assert.Equal(start + k, window[k].ItemNumber);
                    Assert.Equal(table.Read(window[k].ItemNumber), window[k].Raw);
                    var f = table.FileNumberOf(window[k].ItemNumber);
                    if (f != prevFile) crossedFile = true;
                    prevFile = f;
                }
                Assert.True(crossedFile, "window must span a file boundary");
                Assert.True(window[window.Count - 1].ItemNumber < sealedHead, "window must not reach into the open file");
            }
            finally { DeleteDirectory(dir); }
        }

        [Fact]
        public void Given_MultiFileSealedTable_When_ReadInSmallChunks_Then_SplitsByBytesAndFilesAndMatchesPerItemReadByteForByte()
        {
            var dir = CreateTempDirectory();
            try
            {
                var paths = new FreezerTablePaths(dir, "chunk", useCompression: false);
                using var table = FreezerTable<byte[]>.OpenForAppend(paths, new PassthroughByteCodec(), maxFileSize: 32);
                for (var i = 0; i < 40; i++)
                {
                    var item = new byte[5 + (i % 4)];
                    for (var b = 0; b < item.Length; b++) item[b] = (byte)(i * 7 + b);
                    table.Append(i, item);
                }

                var sealedHead = table.SealedHead;
                Assert.True(sealedHead >= 4, "need enough sealed items across files");

                var all = new List<(long ItemNumber, byte[] Raw)>();
                var chunks = 0;
                var cursor = 0L;
                while (cursor < sealedHead)
                {
                    var (items, next) = table.ReadSealedChunk(cursor, targetBytes: 10, endExclusive: long.MaxValue);
                    if (items.Count == 0) break;
                    Assert.True(next > cursor, "chunk must advance the cursor");
                    var chunkFile = table.FileNumberOf(items[0].ItemNumber);
                    foreach (var it in items)
                        Assert.Equal(chunkFile, table.FileNumberOf(it.ItemNumber));
                    all.AddRange(items);
                    chunks++;
                    cursor = next;
                }

                Assert.True(chunks >= 4, "small targetBytes over multiple files must produce several chunks");
                Assert.Equal((int)sealedHead, all.Count);
                for (var k = 0; k < all.Count; k++)
                {
                    Assert.Equal((long)k, all[k].ItemNumber);
                    Assert.Equal(table.Read(all[k].ItemNumber), all[k].Raw);
                }
            }
            finally { DeleteDirectory(dir); }
        }

        [Fact]
        public void Given_ASealedChunk_When_ReadSealedChunk_Then_ResolvesRangesInSlabsNotPerRecord()
        {
            var dir = CreateTempDirectory();
            try
            {
                const int recordCount = 600;
                const uint itemSize = 3;
                const uint maxFileSizeThatSealsExactlyRecordCountItemsInFileZero = recordCount * itemSize;

                var paths = new FreezerTablePaths(dir, "slab", useCompression: false);
                using var table = FreezerTable<byte[]>.OpenForAppend(
                    paths, new PassthroughByteCodec(), maxFileSizeThatSealsExactlyRecordCountItemsInFileZero);

                for (var i = 0; i < recordCount + 1; i++)
                    table.Append(i, new byte[] { (byte)i, 1, 2 });

                Assert.Equal((long)recordCount, table.SealedHead);

                var indexReadsBefore = table.IndexReadOperationCount;
                var (items, next) = table.ReadSealedChunk(0, targetBytes: (int)(recordCount * itemSize * 2), endExclusive: recordCount);
                var indexReadsForWholeChunk = table.IndexReadOperationCount - indexReadsBefore;

                Assert.Equal(recordCount, items.Count);
                Assert.Equal((long)recordCount, next);

                const long maxSlabsForThisChunkPlusSealedHeadBinarySearchOverhead = 20;
                Assert.True(indexReadsForWholeChunk <= maxSlabsForThisChunkPlusSealedHeadBinarySearchOverhead,
                    $"expected a small bounded number of index reads, got {indexReadsForWholeChunk}");
                Assert.True(indexReadsForWholeChunk < recordCount,
                    $"delta {indexReadsForWholeChunk} must be far smaller than recordCount {recordCount}");
            }
            finally { DeleteDirectory(dir); }
        }

        [Fact]
        public void Given_AFixedSizeHashTable_When_DecodeSealedChunk_Then_OneChunkCoversManyRecordsWithoutPerRecordIndexReads()
        {
            var dir = CreateTempDirectory();
            try
            {
                const int recordCount = 4096;
                const uint itemSize = 32;
                const uint maxFileSizeThatSealsExactlyRecordCountItemsInFileZero = recordCount * itemSize;

                var paths = new FreezerTablePaths(dir, "hashes", useCompression: false);
                using var table = FreezerTable<byte[]>.OpenForAppend(
                    paths, new PassthroughByteCodec(), maxFileSizeThatSealsExactlyRecordCountItemsInFileZero);

                for (var i = 0; i < recordCount + 1; i++)
                {
                    var hash = new byte[itemSize];
                    for (var b = 0; b < hash.Length; b++) hash[b] = (byte)(i + b);
                    table.Append(i, hash);
                }

                Assert.Equal((long)recordCount, table.SealedHead);

                var indexReadsBefore = table.IndexReadOperationCount;
                var decoded = FreezerParallelSegment.DecodeSealedChunk(
                    table, startItem: 0, targetBytes: (int)(recordCount * itemSize * 2), endExclusive: recordCount);
                var indexReadsForWholeChunk = table.IndexReadOperationCount - indexReadsBefore;

                Assert.Equal(recordCount, decoded.Count);
                for (var i = 0; i < decoded.Count; i++)
                {
                    Assert.Equal((long)i, decoded[i].ItemNumber);
                    Assert.Equal(table.Read(i), decoded[i].Decoded);
                }

                const long maxSlabsForThisChunkPlusSealedHeadBinarySearchOverhead = 20;
                Assert.True(indexReadsForWholeChunk <= maxSlabsForThisChunkPlusSealedHeadBinarySearchOverhead,
                    $"expected a small bounded number of index reads, got {indexReadsForWholeChunk}");
                Assert.True(indexReadsForWholeChunk < recordCount,
                    $"delta {indexReadsForWholeChunk} must be far smaller than recordCount {recordCount}");
            }
            finally { DeleteDirectory(dir); }
        }

        [Fact]
        public void Given_TheOpenCurrentFile_When_ReadAsSegment_Then_Throws_BecauseItIsNotSealed()
        {
            var dir = CreateTempDirectory();
            try
            {
                var paths = new FreezerTablePaths(dir, "seg", useCompression: false);
                using var table = FreezerTable<byte[]>.OpenForAppend(
                    paths, new PassthroughByteCodec(), RollingDataFiles.DefaultMaxFileSize);
                for (var i = 0; i < 3; i++) table.Append(i, new byte[] { (byte)i, 1, 2, 3, 4 });

                Assert.Equal(0L, table.SealedHead);
                Assert.Throws<FreezerConsistencyException>(() => table.ReadSealedSegment(0));
            }
            finally { DeleteDirectory(dir); }
        }

        [Fact]
        public void Given_IndexShorterThanFlushOffset_When_OpenReadOnly_Then_DoesNotThrow()
        {
            var paths = new FreezerTablePaths(FixturesDirectory, "headers", useCompression: true);
            var codec = new SnappyItemCodec<byte[]>(new PassthroughByteCodec());

            var exception = Record.Exception(() =>
            {
                using var table = FreezerTable<byte[]>.OpenReadOnly(paths, codec);
            });

            Assert.Null(exception);
        }

        [Fact]
        public void Given_NewTable_When_AppendThenRead_Then_RoundTripsBytes()
        {
            var dir = CreateTempDirectory();
            try
            {
                var paths = new FreezerTablePaths(dir, "custom", useCompression: false);
                using var table = FreezerTable<byte[]>.OpenForAppend(
                    paths, new PassthroughByteCodec(), RollingDataFiles.DefaultMaxFileSize);

                table.Append(0, new byte[] { 1, 2, 3 });
                table.Append(1, new byte[] { 4, 5 });
                table.Append(2, Array.Empty<byte>());

                Assert.Equal(3L, table.Count);
                Assert.Equal(new byte[] { 1, 2, 3 }, table.Read(0));
                Assert.Equal(new byte[] { 4, 5 }, table.Read(1));
                Assert.Empty(table.Read(2));
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_UncommittedAppend_When_Reopened_Then_RepairRollsItBack()
        {
            var dir = CreateTempDirectory();
            try
            {
                var paths = new FreezerTablePaths(dir, "custom", useCompression: false);
                using (var table = FreezerTable<byte[]>.OpenForAppend(
                    paths, new PassthroughByteCodec(), RollingDataFiles.DefaultMaxFileSize))
                {
                    table.Append(0, new byte[] { 9, 8, 7 });
                }

                using var reopened = FreezerTable<byte[]>.OpenForAppend(
                    paths, new PassthroughByteCodec(), RollingDataFiles.DefaultMaxFileSize);

                Assert.Equal(0L, reopened.Count);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_ReadOnlyTable_WithMissingDataFile_When_Open_Then_Throws()
        {
            var dir = CreateTempDirectory();
            try
            {
                var paths = new FreezerTablePaths(dir, "custom", useCompression: false);
                using (var table = FreezerTable<byte[]>.OpenForAppend(
                    paths, new PassthroughByteCodec(), RollingDataFiles.DefaultMaxFileSize))
                {
                    table.Append(0, new byte[] { 1, 2, 3 });
                }

                File.Delete(Path.Combine(dir, "custom.0000.rdat"));

                Assert.Throws<FileNotFoundException>(() =>
                    FreezerTable<byte[]>.OpenReadOnly(paths, new PassthroughByteCodec()));

                Assert.False(File.Exists(Path.Combine(dir, "custom.0000.rdat")));
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_WrongExpectedItemNumber_When_Append_Then_ThrowsFreezerConsistencyException()
        {
            var dir = CreateTempDirectory();
            try
            {
                var paths = new FreezerTablePaths(dir, "custom", useCompression: false);
                using var table = FreezerTable<byte[]>.OpenForAppend(
                    paths, new PassthroughByteCodec(), RollingDataFiles.DefaultMaxFileSize);

                Assert.Throws<FreezerConsistencyException>(() => table.Append(5, new byte[] { 1 }));
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_TableAtCount5_When_TruncateHeadTo2_Then_CountDropsAndDataGone()
        {
            var dir = CreateTempDirectory();
            try
            {
                var paths = new FreezerTablePaths(dir, "custom", useCompression: false);
                using var table = FreezerTable<byte[]>.OpenForAppend(
                    paths, new PassthroughByteCodec(), RollingDataFiles.DefaultMaxFileSize);

                for (var i = 0; i < 5; i++)
                    table.Append(i, new byte[] { (byte)i });

                var result = table.TruncateHead(2);

                Assert.Equal(2L, result);
                Assert.Equal(2L, table.Count);
                Assert.Equal(new byte[] { 0 }, table.Read(0));
                Assert.Equal(new byte[] { 1 }, table.Read(1));
                Assert.Throws<ArgumentOutOfRangeException>(() => table.Read(2));
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_NegativeNewItemCount_When_TruncateHead_Then_ThrowsFreezerImmutableException()
        {
            var dir = CreateTempDirectory();
            try
            {
                var paths = new FreezerTablePaths(dir, "custom", useCompression: false);
                using var table = FreezerTable<byte[]>.OpenForAppend(
                    paths, new PassthroughByteCodec(), RollingDataFiles.DefaultMaxFileSize);
                for (var i = 0; i < 5; i++)
                    table.Append(i, new byte[] { (byte)i });

                Assert.Throws<FreezerImmutableException>(() => table.TruncateHead(-1));
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_ValidPhysicalTargetOnTableWithVirtualTailAboveZero_When_TruncateHead_Then_Succeeds()
        {
            var dir = CreateTempDirectory();
            try
            {
                var paths = new FreezerTablePaths(dir, "custom", useCompression: false);

                new FreezerTableMeta(FreezerTableMeta.SupportedVersion, virtualTail: 3, flushOffset: FreezerIndexEntry.Size)
                    .WriteAtomic(paths.MetaPath);

                using var table = FreezerTable<byte[]>.OpenForAppend(
                    paths, new PassthroughByteCodec(), RollingDataFiles.DefaultMaxFileSize);
                Assert.Equal(3L, table.VirtualTail);

                for (var i = 0; i < 5; i++)
                    table.Append(i, new byte[] { (byte)i });

                var result = table.TruncateHead(2);

                Assert.Equal(2L, result);
                Assert.Equal(2L, table.Count);
                Assert.Equal(new byte[] { 0 }, table.Read(0));
                Assert.Equal(new byte[] { 1 }, table.Read(1));
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_NoItemsYetInTheActiveFile_When_SealedHead_Then_EqualsAllPriorSealedFiles()
        {
            const long tinyMaxFileSize = 10;
            var dir = CreateTempDirectory();
            try
            {
                var paths = new FreezerTablePaths(dir, "custom", useCompression: false);
                using var table = FreezerTable<byte[]>.OpenForAppend(paths, new PassthroughByteCodec(), tinyMaxFileSize);

                for (var i = 0; i < 3; i++)
                    table.Append(i, new byte[] { 1, 2, 3 });
                Assert.Equal(0L, table.SealedHead);

                table.Append(3, new byte[] { 4, 5, 6 });
                Assert.Equal(3L, table.SealedHead);

                table.Append(4, new byte[] { 7, 8, 9 });
                Assert.Equal(3L, table.SealedHead);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_MultipleRolls_When_SealedHead_Then_AdvancesToEachNewFilesFirstItem()
        {
            const long tinyMaxFileSize = 10;
            var dir = CreateTempDirectory();
            try
            {
                var paths = new FreezerTablePaths(dir, "custom", useCompression: false);
                using var table = FreezerTable<byte[]>.OpenForAppend(paths, new PassthroughByteCodec(), tinyMaxFileSize);

                for (var i = 0; i < 7; i++)
                    table.Append(i, new byte[] { 1, 2, 3 });

                var sealedHead = table.SealedHead;
                Assert.True(sealedHead >= 0 && sealedHead < table.Count);

                for (var i = 0; i < sealedHead; i++)
                    Assert.Equal(new byte[] { 1, 2, 3 }, table.Read(i));

                var before = table.SealedHead;
                table.Append(7, new byte[] { 1 });
                Assert.True(table.SealedHead >= before);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_TableWithVirtualTail_When_SealedHead_Then_IsArchiveGlobalNotPhysical()
        {
            const long tinyMaxFileSize = 10;
            var dir = CreateTempDirectory();
            try
            {
                var paths = new FreezerTablePaths(dir, "custom", useCompression: false);
                new FreezerTableMeta(FreezerTableMeta.SupportedVersion, virtualTail: 100, flushOffset: FreezerIndexEntry.Size)
                    .WriteAtomic(paths.MetaPath);

                using var table = FreezerTable<byte[]>.OpenForAppend(paths, new PassthroughByteCodec(), tinyMaxFileSize);
                Assert.Equal(100L, table.VirtualTail);

                for (var i = 0; i < 4; i++)
                    table.Append(i, new byte[] { 1, 2, 3 });

                Assert.Equal(103L, table.SealedHead);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_ACommittedOpenFileTail_When_ReadSealedChunkWithEndExclusiveBeyondSealedCount_Then_ReadsPastSealedBoundary()
        {
            const int itemCount = 40;
            var dir = CreateTempDirectory();
            try
            {
                var paths = new FreezerTablePaths(dir, "opentail", useCompression: false);
                using var table = FreezerTable<byte[]>.OpenForAppend(
                    paths, new PassthroughByteCodec(), RollingDataFiles.DefaultMaxFileSize);

                for (var i = 0; i < itemCount; i++)
                    table.Append(i, new byte[] { (byte)i, 1, 2, 3, 4 });

                table.SyncIndex();
                table.SyncData();
                table.PersistMeta();

                Assert.Equal(0L, table.SealedHead);

                var window = table.ReadSealedRange(0, maxItems: itemCount, endExclusive: itemCount);

                Assert.Equal(itemCount, window.Count);
                for (var i = 0; i < itemCount; i++)
                {
                    Assert.Equal((long)i, window[i].ItemNumber);
                    Assert.Equal(table.Read(i), window[i].Raw);
                }

                var overshoot = table.ReadSealedRange(0, maxItems: itemCount, endExclusive: itemCount * 1000);

                Assert.Equal(itemCount, overshoot.Count);
                for (var i = 0; i < itemCount; i++)
                    Assert.Equal(table.Read(i), overshoot[i].Raw);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }
    }
}
