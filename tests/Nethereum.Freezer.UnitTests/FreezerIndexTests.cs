using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.Freezer.UnitTests
{
    public class FreezerIndexTests
    {
        private static readonly string FixturesDirectory =
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "geth-ancient");

        private static string HeadersCidxPath => Path.Combine(FixturesDirectory, "headers.cidx");
        private static string BalsCidxPath => Path.Combine(FixturesDirectory, "bals.cidx");

        private static string CreateTempIndexFile(params FreezerIndexEntry[] entries)
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[FreezerIndexEntry.Size];
                foreach (var entry in entries)
                {
                    entry.WriteTo(buffer);
                    stream.Write(buffer, 0, buffer.Length);
                }
            }
            return path;
        }

        [Fact]
        public void Given_RealHeadersCidx_When_Count_Then_2047Items()
        {
            using var index = FreezerIndex.OpenReadOnly(HeadersCidxPath);

            Assert.Equal(2047L, index.Count);
        }

        [Theory]
        [InlineData(0, (ushort)0, 0u)]
        [InlineData(1, (ushort)0, 199u)]
        [InlineData(2, (ushort)0, 490u)]
        [InlineData(3, (ushort)0, 788u)]
        [InlineData(4, (ushort)0, 1086u)]
        public void Given_RealHeadersCidx_When_EntryAt_Then_MatchesKnownGethBytes(
            long itemNumber, ushort expectedFileNumber, uint expectedOffset)
        {
            using var index = FreezerIndex.OpenReadOnly(HeadersCidxPath);

            var entry = index.EntryAt(itemNumber);

            Assert.Equal(expectedFileNumber, entry.FileNumber);
            Assert.Equal(expectedOffset, entry.Offset);
        }

        [Theory]
        [InlineData(0, (ushort)0, 0u)]
        [InlineData(1, (ushort)0, 1u)]
        [InlineData(2, (ushort)0, 2u)]
        public void Given_RealBalsCidx_When_EntryAt_Then_OneBytePlaceholderItems(
            long itemNumber, ushort expectedFileNumber, uint expectedOffset)
        {
            using var index = FreezerIndex.OpenReadOnly(BalsCidxPath);

            var entry = index.EntryAt(itemNumber);

            Assert.Equal(expectedFileNumber, entry.FileNumber);
            Assert.Equal(expectedOffset, entry.Offset);
        }

        [Fact]
        public void Given_Index_When_RangeOfItem_Then_StartEndFromAdjacentEntries()
        {
            using var index = FreezerIndex.OpenReadOnly(HeadersCidxPath);

            var range = index.RangeOf(0);

            Assert.Equal((ushort)0, range.FileNumber);
            Assert.Equal(0u, range.Start);
            Assert.Equal(199u, range.Length);
        }

        [Fact]
        public void Given_CrossFileItem_When_RangeOf_Then_StartsAtZeroInNextFile()
        {
            var path = CreateTempIndexFile(
                new FreezerIndexEntry(0, 0),
                new FreezerIndexEntry(0, 1000),
                new FreezerIndexEntry(1, 50));
            try
            {
                using var index = FreezerIndex.OpenReadOnly(path);

                var range = index.RangeOf(1);

                Assert.Equal((ushort)1, range.FileNumber);
                Assert.Equal(0u, range.Start);
                Assert.Equal(50u, range.Length);

                var naiveLength = unchecked(50u - 1000u);
                Assert.NotEqual(naiveLength, range.Length);
                Assert.True(naiveLength > 1000u);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Given_ItemNumberOutOfRange_When_EntryAt_Then_ThrowsArgumentOutOfRangeException()
        {
            using var index = FreezerIndex.OpenReadOnly(HeadersCidxPath);

            Assert.Throws<ArgumentOutOfRangeException>(() => index.EntryAt(index.Count + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => index.EntryAt(-1));
        }

        [Fact]
        public void Given_TailEntry_When_EntryAt_Then_Readable()
        {
            using var index = FreezerIndex.OpenReadOnly(HeadersCidxPath);

            var sentinel = index.EntryAt(0);
            var tail = index.EntryAt(index.Count);

            Assert.Equal((ushort)0, sentinel.FileNumber);
            Assert.Equal(0u, sentinel.Offset);
            Assert.True(tail.Offset > 0);
        }

        [Fact]
        public void Given_NewIndexFile_When_Append_Then_CountIncrementsAndReadable()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                using (var index = FreezerIndex.OpenForAppend(path))
                {
                    index.Append(new FreezerIndexEntry(0, 0));
                    index.Append(new FreezerIndexEntry(0, 199));

                    Assert.Equal(1L, index.Count);
                    Assert.Equal((uint)199, index.EntryAt(1).Offset);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Given_IndexWithExtraItems_When_TruncateToItems_Then_FileLengthMatchesItemsPlusSentinel()
        {
            var path = CreateTempIndexFile(
                new FreezerIndexEntry(0, 0),
                new FreezerIndexEntry(0, 100),
                new FreezerIndexEntry(0, 200),
                new FreezerIndexEntry(0, 300));
            try
            {
                using var index = FreezerIndex.OpenForAppend(path);

                index.TruncateToItems(2);

                Assert.Equal(2L, index.Count);
                Assert.Throws<ArgumentOutOfRangeException>(() => index.EntryAt(3));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 0)]
        [InlineData(5, 0)]
        [InlineData(6, 0)]
        [InlineData(12, 1)]
        [InlineData(18, 2)]
        public void Given_FlushOffset_When_DurableItemCount_Then_ComputesItemsMinusOneClampedToZero(
            long flushOffset, long expectedItems)
        {
            using var index = FreezerIndex.OpenReadOnly(HeadersCidxPath);

            Assert.Equal(expectedItems, index.DurableItemCount(flushOffset));
        }

        [Fact]
        public void Given_EmptyIndexFile_When_Count_Then_ZeroNotNegativeOne()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            File.WriteAllBytes(path, Array.Empty<byte>());
            try
            {
                using var index = FreezerIndex.OpenReadOnly(path);

                Assert.Equal(0L, index.Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Given_ASlabOfEntries_When_ReadEntries_Then_MatchesEntryAtOneByOne_AndUsesOneReadNotN()
        {
            const int K = 50;
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                using var index = FreezerIndex.OpenForAppend(path);
                index.Append(new FreezerIndexEntry(0, 0));
                for (var i = 1; i <= K; i++)
                {
                    var fileNumber = (ushort)(i / 10);
                    index.Append(new FreezerIndexEntry(fileNumber, (uint)(i * 37)));
                }

                var readsBeforeReadEntries = index.ReadOperationCount;
                var all = index.ReadEntries(0, K + 1);
                var readsAfterReadEntries = index.ReadOperationCount;

                Assert.Equal(1, readsAfterReadEntries - readsBeforeReadEntries);

                Assert.Equal(K + 1, all.Count);

                var readsBeforeEntryAtLoop = index.ReadOperationCount;
                for (var i = 0; i <= K; i++)
                {
                    var expected = index.EntryAt(i);
                    Assert.Equal(expected.FileNumber, all[i].FileNumber);
                    Assert.Equal(expected.Offset, all[i].Offset);
                }
                var readsAfterEntryAtLoop = index.ReadOperationCount;

                Assert.Equal(K + 1, readsAfterEntryAtLoop - readsBeforeEntryAtLoop);
                Assert.True(
                    readsAfterReadEntries - readsBeforeReadEntries < readsAfterEntryAtLoop - readsBeforeEntryAtLoop,
                    "ReadEntries must use strictly fewer reads than the per-entry EntryAt path");

                var truncated = index.ReadEntries(0, K + 100);
                Assert.Equal(K + 1, truncated.Count);

                var beyondEnd = index.ReadEntries(index.Count + 5, 10);
                Assert.Empty(beyondEnd);

                var mid = index.ReadEntries(10, 5);
                Assert.Equal(5, mid.Count);
                for (var i = 0; i < 5; i++)
                {
                    var expected = index.EntryAt(10 + i);
                    Assert.Equal(expected.FileNumber, mid[i].FileNumber);
                    Assert.Equal(expected.Offset, mid[i].Offset);
                }

                Assert.Throws<ArgumentOutOfRangeException>(() => index.ReadEntries(-1, 10));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Given_ConcurrentEntryAtReads_When_Parallel_Then_AllCorrect()
        {
            var rawBytes = File.ReadAllBytes(HeadersCidxPath);
            var itemCount = rawBytes.Length / FreezerIndexEntry.Size - 1;

            using var index = FreezerIndex.OpenReadOnly(HeadersCidxPath);
            var results = new FreezerIndexEntry[itemCount + 1];

            Parallel.For(0, itemCount + 1, i =>
            {
                results[i] = index.EntryAt(i);
            });

            for (var i = 0; i <= itemCount; i++)
            {
                var expected = FreezerIndexEntry.ReadFrom(
                    rawBytes.AsSpan(i * FreezerIndexEntry.Size, FreezerIndexEntry.Size));

                Assert.Equal(expected.FileNumber, results[i].FileNumber);
                Assert.Equal(expected.Offset, results[i].Offset);
            }
        }
    }
}
