using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.Freezer.UnitTests
{
    public class RollingDataFilesTests
    {
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
        public void Given_NewDirectory_When_Constructed_Then_StartsAtFileZeroEmpty()
        {
            var dir = CreateTempDirectory();
            try
            {
                using var files = RollingDataFiles.OpenForAppend(dir, "headers", useCompression: true, maxFileSize: 100);

                Assert.Equal((ushort)0, files.CurrentFileNumber);
                Assert.Equal(0u, files.CurrentFileLength);
                Assert.True(File.Exists(Path.Combine(dir, "headers.0000.cdat")));
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_UncompressedTable_When_Constructed_Then_UsesRdatExtension()
        {
            var dir = CreateTempDirectory();
            try
            {
                using var files = RollingDataFiles.OpenForAppend(dir, "hashes", useCompression: false, maxFileSize: 100);

                Assert.True(File.Exists(Path.Combine(dir, "hashes.0000.rdat")));
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_ItemsWithinCap_When_Append_Then_AppendsToSameFileSequentially()
        {
            var dir = CreateTempDirectory();
            try
            {
                using var files = RollingDataFiles.OpenForAppend(dir, "headers", useCompression: true, maxFileSize: 100);

                var first = files.Append(new byte[] { 1, 2, 3 });
                var second = files.Append(new byte[] { 4, 5 });

                Assert.Equal((ushort)0, first.fileNumber);
                Assert.Equal(0u, first.offset);
                Assert.Equal((ushort)0, second.fileNumber);
                Assert.Equal(3u, second.offset);
                Assert.Equal(5u, files.CurrentFileLength);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_SmallMaxFileSize_When_AppendPastCap_Then_RollsToNextFileNoSplit()
        {
            var dir = CreateTempDirectory();
            try
            {
                using var files = RollingDataFiles.OpenForAppend(dir, "headers", useCompression: true, maxFileSize: 10);

                var first = files.Append(new byte[] { 1, 2, 3, 4, 5, 6 });
                var second = files.Append(new byte[] { 7, 8, 9, 10, 11, 12 });

                Assert.Equal((ushort)0, first.fileNumber);
                Assert.Equal(0u, first.offset);

                Assert.Equal((ushort)1, second.fileNumber);
                Assert.Equal(0u, second.offset);
                Assert.Equal((ushort)1, files.CurrentFileNumber);
                Assert.Equal(6u, files.CurrentFileLength);

                Assert.True(File.Exists(Path.Combine(dir, "headers.0000.cdat")));
                Assert.True(File.Exists(Path.Combine(dir, "headers.0001.cdat")));
                Assert.Equal(6L, new FileInfo(Path.Combine(dir, "headers.0000.cdat")).Length);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_ItemLargerThanRemaining_When_Append_Then_NewFileNotSplitAcrossBoundary()
        {
            var dir = CreateTempDirectory();
            try
            {
                using var files = RollingDataFiles.OpenForAppend(dir, "headers", useCompression: true, maxFileSize: 10);

                var itemA = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
                var itemB = new byte[] { 9, 8, 7, 6, 5 };

                var locationA = files.Append(itemA);
                var locationB = files.Append(itemB);

                Assert.Equal((ushort)0, locationA.fileNumber);
                Assert.Equal(0u, locationA.offset);

                Assert.Equal((ushort)1, locationB.fileNumber);
                Assert.Equal(0u, locationB.offset);

                var readBackA = files.Read(locationA.fileNumber, locationA.offset, (uint)itemA.Length);
                var readBackB = files.Read(locationB.fileNumber, locationB.offset, (uint)itemB.Length);
                Assert.Equal(itemA, readBackA);
                Assert.Equal(itemB, readBackB);

                Assert.Equal((long)itemA.Length, new FileInfo(Path.Combine(dir, "headers.0000.cdat")).Length);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_ItemsAcrossRolledFiles_When_Read_Then_RoundTripsExactBytes()
        {
            var dir = CreateTempDirectory();
            try
            {
                using var files = RollingDataFiles.OpenForAppend(dir, "headers", useCompression: true, maxFileSize: 8);

                var items = new[]
                {
                    new byte[] { 1, 2, 3 },
                    new byte[] { 4, 5, 6, 7 },
                    new byte[] { 8 },
                    new byte[] { 9, 10, 11, 12, 13 },
                    Array.Empty<byte>(),
                    new byte[] { 14 },
                };

                var locations = new (ushort fileNumber, uint offset)[items.Length];
                for (var i = 0; i < items.Length; i++)
                    locations[i] = files.Append(items[i]);

                for (var i = 0; i < items.Length; i++)
                {
                    var readBack = files.Read(locations[i].fileNumber, locations[i].offset, (uint)items[i].Length);
                    Assert.Equal(items[i], readBack);
                }
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_ExistingRolledFiles_When_Reopened_Then_ResumesAtHighestFile()
        {
            var dir = CreateTempDirectory();
            try
            {
                using (var files = RollingDataFiles.OpenForAppend(dir, "headers", useCompression: true, maxFileSize: 10))
                {
                    files.Append(new byte[] { 1, 2, 3, 4, 5, 6 });
                    files.Append(new byte[] { 7, 8, 9, 10, 11, 12 });
                }

                using var reopened = RollingDataFiles.OpenForAppend(dir, "headers", useCompression: true, maxFileSize: 10);

                Assert.Equal((ushort)1, reopened.CurrentFileNumber);
                Assert.Equal(6u, reopened.CurrentFileLength);

                var appended = reopened.Append(new byte[] { 99 });
                Assert.Equal((ushort)1, appended.fileNumber);
                Assert.Equal(6u, appended.offset);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_RolledFiles_When_TruncateHeadTo_Then_LaterFilesRemovedAndCurrentTruncated()
        {
            var dir = CreateTempDirectory();
            try
            {
                using var files = RollingDataFiles.OpenForAppend(dir, "headers", useCompression: true, maxFileSize: 10);

                files.Append(new byte[] { 1, 2, 3, 4, 5, 6 });
                files.Append(new byte[] { 7, 8, 9, 10, 11, 12 });
                files.Append(new byte[] { 13 });

                files.TruncateHeadTo(1, 6);

                Assert.Equal((ushort)1, files.CurrentFileNumber);
                Assert.Equal(6u, files.CurrentFileLength);
                Assert.Equal(6L, new FileInfo(Path.Combine(dir, "headers.0001.cdat")).Length);

                var appended = files.Append(new byte[] { 42 });
                Assert.Equal((ushort)1, appended.fileNumber);
                Assert.Equal(6u, appended.offset);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_OpenFile_When_SyncCurrent_Then_DoesNotThrow()
        {
            var dir = CreateTempDirectory();
            try
            {
                using var files = RollingDataFiles.OpenForAppend(dir, "headers", useCompression: true, maxFileSize: 100);

                files.Append(new byte[] { 1, 2, 3 });

                var exception = Record.Exception(() => files.SyncCurrent());
                Assert.Null(exception);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_OpenFile_When_Disposed_Then_ReleasesFileHandle()
        {
            var dir = CreateTempDirectory();
            try
            {
                var files = RollingDataFiles.OpenForAppend(dir, "headers", useCompression: true, maxFileSize: 100);
                files.Append(new byte[] { 1, 2, 3 });
                files.Dispose();

                var exception = Record.Exception(() => File.Delete(Path.Combine(dir, "headers.0000.cdat")));
                Assert.Null(exception);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_SameFile_When_TwoHandlesOpenConcurrently_Then_NoSharingViolation()
        {
            var dir = CreateTempDirectory();
            try
            {
                using var appendFiles =
                    RollingDataFiles.OpenForAppend(dir, "headers", useCompression: true, maxFileSize: 100);
                appendFiles.Append(new byte[] { 1, 2, 3 });

                var exception = Record.Exception(() =>
                {
                    using var readOnlyFiles = RollingDataFiles.OpenReadOnly(dir, "headers", useCompression: true);
                    var readBack = readOnlyFiles.Read(0, 0, 3);
                    Assert.Equal(new byte[] { 1, 2, 3 }, readBack);
                });

                Assert.Null(exception);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_ReadOnlyTable_When_AppendOrTruncateHeadTo_Then_ThrowsInvalidOperationException()
        {
            var dir = CreateTempDirectory();
            try
            {
                using (var files = RollingDataFiles.OpenForAppend(dir, "headers", useCompression: true, maxFileSize: 100))
                {
                    files.Append(new byte[] { 1, 2, 3 });
                }

                using var readOnlyFiles = RollingDataFiles.OpenReadOnly(dir, "headers", useCompression: true);

                Assert.Throws<InvalidOperationException>(() => readOnlyFiles.Append(new byte[] { 4 }));
                Assert.Throws<InvalidOperationException>(() => readOnlyFiles.TruncateHeadTo(0, 0));
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_ItemExactlyFillsRemaining_When_Append_Then_StaysInSameFile()
        {
            var dir = CreateTempDirectory();
            try
            {
                using var files = RollingDataFiles.OpenForAppend(dir, "headers", useCompression: true, maxFileSize: 10);

                var first = files.Append(new byte[] { 1, 2, 3, 4 });
                var second = files.Append(new byte[] { 5, 6, 7, 8, 9, 10 });

                Assert.Equal((ushort)0, first.fileNumber);
                Assert.Equal((ushort)0, second.fileNumber);
                Assert.Equal((ushort)0, files.CurrentFileNumber);
                Assert.Equal(10u, files.CurrentFileLength);
                Assert.False(File.Exists(Path.Combine(dir, "headers.0001.cdat")));
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_ConcurrentReadsAcrossRolledFiles_When_Parallel_Then_AllCorrect()
        {
            var dir = CreateTempDirectory();
            try
            {
                const uint maxFileSize = 1000;
                using var files = RollingDataFiles.OpenForAppend(dir, "headers", useCompression: true, maxFileSize: maxFileSize);

                var items = new List<byte[]>();
                var locations = new List<(ushort fileNumber, uint offset)>();

                for (var f = 0; f < 4; f++)
                {
                    var filler = new byte[maxFileSize];
                    new Random(f).NextBytes(filler);
                    items.Add(filler);
                    locations.Add(files.Append(filler));
                }

                const int bulkCount = 300;
                for (var i = 0; i < bulkCount; i++)
                {
                    var item = new byte[] { (byte)(i % 256), (byte)((i * 7) % 256), (byte)((i * 13) % 256) };
                    items.Add(item);
                    locations.Add(files.Append(item));
                }

                Assert.Equal((ushort)4, files.CurrentFileNumber);

                var itemsArray = items.ToArray();
                var locationsArray = locations.ToArray();
                var results = new byte[itemsArray.Length][];

                var order = Enumerable.Range(0, itemsArray.Length).ToArray();
                var shuffleRandom = new Random(1234);
                for (var i = order.Length - 1; i > 0; i--)
                {
                    var j = shuffleRandom.Next(i + 1);
                    (order[i], order[j]) = (order[j], order[i]);
                }

                Parallel.ForEach(order, i =>
                {
                    results[i] = files.Read(locationsArray[i].fileNumber, locationsArray[i].offset,
                        (uint)itemsArray[i].Length);
                });

                for (var i = 0; i < itemsArray.Length; i++)
                    Assert.Equal(itemsArray[i], results[i]);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }
    }
}
