using System;
using System.IO;
using System.Linq;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.Freezer.UnitTests
{
    public class FreezerCoordinatorTests
    {
        private static readonly string FixturesDirectory =
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "geth-ancient");

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

        private static FrozenBlockCluster ClusterFor(long blockNumber)
        {
            var header = new byte[] { 0xf9, (byte)blockNumber, 0x01 };
            var hash = Enumerable.Repeat((byte)blockNumber, 32).ToArray();
            var body = new byte[] { 0xc0, (byte)blockNumber };
            var receipts = new byte[] { 0xc0 };
            var bal = new byte[] { 0x00 };
            return new FrozenBlockCluster(header, hash, body, receipts, bal);
        }

        [Fact]
        public void Given_SealedCompressedReceiptsFile_When_DecodeSealedSegment_Then_MatchesPerBlockReadReceiptsAndFileNumberOf()
        {
            var dir = CreateTempDirectory();
            try
            {
                var layout = new FreezerLayout(dir, maxFileSize: 16);
                using var freezer = Freezer.Open(layout, FreezerOpenMode.Append);
                using (var batch = freezer.BeginBatch())
                    for (long b = 0; b < 40; b++) batch.AppendCluster(b, ClusterFor(b));

                var seg = freezer.DecodeSealedSegment("receipts", 0, maxDegreeOfParallelism: 4);

                Assert.NotEmpty(seg);
                foreach (var (blockNumber, decoded) in seg)
                {
                    Assert.Equal(freezer.ReadReceipts(blockNumber), decoded);
                    Assert.Equal((ushort)0, freezer.FileNumberOf("receipts", blockNumber));
                }
                Assert.Equal(0L, seg[0].BlockNumber);
            }
            finally { DeleteDirectory(dir); }
        }

        [Fact]
        public void Given_SealedCompressedReceipts_When_DecodeSealedRangeAcrossFiles_Then_MatchesPerBlockReadReceipts()
        {
            var dir = CreateTempDirectory();
            try
            {
                var layout = new FreezerLayout(dir, maxFileSize: 16);
                using var freezer = Freezer.Open(layout, FreezerOpenMode.Append);
                using (var batch = freezer.BeginBatch())
                    for (long b = 0; b < 40; b++) batch.AppendCluster(b, ClusterFor(b));

                var sealedHead = freezer.SealedHead("receipts");
                Assert.True(sealedHead >= 6, "need enough sealed receipts to span a file boundary");

                var window = freezer.DecodeSealedRange("receipts", startBlock: 4, maxItems: 1000, maxDegreeOfParallelism: 4);

                Assert.Equal((int)(sealedHead - 4), window.Count);
                var crossedFile = false;
                var prevFile = freezer.FileNumberOf("receipts", window[0].BlockNumber);
                for (var k = 0; k < window.Count; k++)
                {
                    Assert.Equal(4L + k, window[k].BlockNumber);
                    Assert.Equal(freezer.ReadReceipts(window[k].BlockNumber), window[k].Decoded);
                    var f = freezer.FileNumberOf("receipts", window[k].BlockNumber);
                    if (f != prevFile) crossedFile = true;
                    prevFile = f;
                }
                Assert.True(crossedFile, "window must span a receipts file boundary");
            }
            finally { DeleteDirectory(dir); }
        }

        [Fact]
        public void Given_RealGethCorpus_When_OpenReadOnly_Then_ItemsAndReadClusterMatch()
        {
            var layout = new FreezerLayout(FixturesDirectory);
            using var freezer = Freezer.Open(layout, FreezerOpenMode.ReadOnly);

            Assert.Equal(2047L, freezer.Items);

            var cluster = freezer.ReadCluster(0);
            Assert.Equal(0xf9, cluster.Header[0]);
            Assert.Equal(32, cluster.Hash.Length);
            Assert.Empty(cluster.Bal);

            var rawBalsItem0 = File.ReadAllBytes(Path.Combine(FixturesDirectory, "bals.0000.cdat")).Take(1).ToArray();
            Assert.Single(rawBalsItem0);
            Assert.Equal(0x00, rawBalsItem0[0]);
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "Freezer append + commit round-trip")]
        public void Given_MultiClusterBatch_When_Commit_Then_AllFiveTablesAdvanceLockstep()
        {
            var dir = CreateTempDirectory();
            try
            {
                var layout = new FreezerLayout(dir);
                using (var freezer = Freezer.Open(layout, FreezerOpenMode.Append))
                {
                    using var batch = freezer.BeginBatch();
                    for (var block = 0L; block < 5; block++)
                        batch.AppendCluster(block, ClusterFor(block));

                    var committedHead = batch.Commit();
                    Assert.Equal(5L, committedHead);
                    Assert.Equal(5L, freezer.Items);
                }

                using var reopened = Freezer.Open(layout, FreezerOpenMode.ReadOnly);
                Assert.Equal(5L, reopened.Items);

                for (var block = 0L; block < 5; block++)
                {
                    var cluster = reopened.ReadCluster(block);
                    Assert.Equal(ClusterFor(block).Header, cluster.Header);
                    Assert.Equal(ClusterFor(block).Hash, cluster.Hash);
                    Assert.Equal(ClusterFor(block).Bal, cluster.Bal);
                }
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_ClusterAtWrongBlockNumber_When_AppendCluster_Then_ThrowsFreezerConsistencyException()
        {
            var dir = CreateTempDirectory();
            try
            {
                var layout = new FreezerLayout(dir);
                using var freezer = Freezer.Open(layout, FreezerOpenMode.Append);
                using var batch = freezer.BeginBatch();

                Assert.Throws<FreezerConsistencyException>(() => batch.AppendCluster(5, ClusterFor(5)));
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_TableGapAfterStart_When_AppendCluster_Then_ThrowsFreezerConsistencyException()
        {
            var dir = CreateTempDirectory();
            try
            {
                var layout = new FreezerLayout(dir);
                using var freezer = Freezer.Open(layout, FreezerOpenMode.Append);
                using (var batch = freezer.BeginBatch())
                {
                    batch.AppendCluster(0, ClusterFor(0));
                    batch.Commit();
                }

                using var next = freezer.BeginBatch();
                Assert.Throws<FreezerConsistencyException>(() => next.AppendCluster(2, ClusterFor(2)));
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_StagedUncommittedAppends_When_Reset_Then_RollsBackToLastCommit()
        {
            var dir = CreateTempDirectory();
            try
            {
                var layout = new FreezerLayout(dir);
                using var freezer = Freezer.Open(layout, FreezerOpenMode.Append);
                using (var batch = freezer.BeginBatch())
                {
                    batch.AppendCluster(0, ClusterFor(0));
                    batch.Commit();

                    batch.AppendCluster(1, ClusterFor(1));
                    batch.AppendCluster(2, ClusterFor(2));
                    batch.Reset();
                }

                Assert.Equal(1L, freezer.Items);

                using var retryBatch = freezer.BeginBatch();
                retryBatch.AppendCluster(1, ClusterFor(1));
                retryBatch.Commit();
                Assert.Equal(2L, freezer.Items);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_BodiesReceiptsSharedTailGroup_When_OpenWithDrift_Then_MaxTailAligned()
        {
            var dir = CreateTempDirectory();
            try
            {
                var layout = new FreezerLayout(dir);

                WriteLockstepTable(dir, layout.Headers, virtualTail: 0, physicalItemCount: 5, physicalStart: 0);
                WriteLockstepTable(dir, layout.Hashes, virtualTail: 0, physicalItemCount: 5, physicalStart: 0);
                WriteLockstepTable(dir, layout.Bals, virtualTail: 0, physicalItemCount: 5, physicalStart: 0);

                WriteLockstepTable(dir, layout.Bodies, virtualTail: 2, physicalItemCount: 3, physicalStart: 2);
                WriteLockstepTable(dir, layout.Receipts, virtualTail: 0, physicalItemCount: 5, physicalStart: 0);

                using var freezer = Freezer.Open(layout, FreezerOpenMode.Append);

                Assert.Equal(5L, freezer.Items);
                var group = freezer.Tails.Single(pair => pair.Value.Members.Contains("bodies"));
                Assert.Contains("receipts", group.Value.Members);
                Assert.Equal(2L, group.Value.VirtualTail);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_TablesWithVirtualTailAboveZero_When_ResetAfterAppend_Then_RollsBackWithoutThrowing()
        {
            var dir = CreateTempDirectory();
            try
            {
                var layout = new FreezerLayout(dir);
                foreach (var config in layout.Tables)
                    WriteLockstepTable(dir, config, virtualTail: 3, physicalItemCount: 2, physicalStart: 3);

                using var freezer = Freezer.Open(layout, FreezerOpenMode.Append);
                Assert.Equal(5L, freezer.Items);

                using var batch = freezer.BeginBatch();
                batch.AppendCluster(5, ClusterFor(5));
                batch.AppendCluster(6, ClusterFor(6));

                var exception = Record.Exception(() => batch.Reset());

                Assert.Null(exception);
                Assert.Equal(5L, freezer.Items);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_TablesWithVirtualTailAboveZero_When_TruncateHeadAboveAllTails_Then_Succeeds()
        {
            var dir = CreateTempDirectory();
            try
            {
                var layout = new FreezerLayout(dir);
                foreach (var config in layout.Tables)
                    WriteLockstepTable(dir, config, virtualTail: 3, physicalItemCount: 5, physicalStart: 3);

                using var freezer = Freezer.Open(layout, FreezerOpenMode.Append);
                Assert.Equal(8L, freezer.Items);

                var exception = Record.Exception(() => freezer.TruncateHead(6));

                Assert.Null(exception);
                Assert.Equal(6L, freezer.Items);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_TruncateHeadBelowMaxTail_When_Called_Then_ThrowsFreezerImmutableException()
        {
            var dir = CreateTempDirectory();
            try
            {
                var layout = new FreezerLayout(dir);

                WriteLockstepTable(dir, layout.Headers, virtualTail: 2, physicalItemCount: 6, physicalStart: 2);
                WriteLockstepTable(dir, layout.Hashes, virtualTail: 2, physicalItemCount: 6, physicalStart: 2);
                WriteLockstepTable(dir, layout.Bals, virtualTail: 2, physicalItemCount: 6, physicalStart: 2);
                WriteLockstepTable(dir, layout.Bodies, virtualTail: 4, physicalItemCount: 4, physicalStart: 4);
                WriteLockstepTable(dir, layout.Receipts, virtualTail: 4, physicalItemCount: 4, physicalStart: 4);

                using var freezer = Freezer.Open(layout, FreezerOpenMode.Append);
                Assert.Equal(8L, freezer.Items);

                Assert.Throws<FreezerImmutableException>(() => freezer.TruncateHead(3));
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_TruncateHeadBelowMaxTail_When_Called_Then_NoTableIsMutated()
        {
            var dir = CreateTempDirectory();
            try
            {
                var layout = new FreezerLayout(dir);

                WriteLockstepTable(dir, layout.Headers, virtualTail: 2, physicalItemCount: 6, physicalStart: 2);
                WriteLockstepTable(dir, layout.Hashes, virtualTail: 2, physicalItemCount: 6, physicalStart: 2);
                WriteLockstepTable(dir, layout.Bals, virtualTail: 2, physicalItemCount: 6, physicalStart: 2);
                WriteLockstepTable(dir, layout.Bodies, virtualTail: 4, physicalItemCount: 4, physicalStart: 4);
                WriteLockstepTable(dir, layout.Receipts, virtualTail: 4, physicalItemCount: 4, physicalStart: 4);

                using var freezer = Freezer.Open(layout, FreezerOpenMode.Append);

                Assert.Throws<FreezerImmutableException>(() => freezer.TruncateHead(3));

                Assert.Equal(8L, freezer.Items);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_NarrowReadAccessors_When_ReadHashBodyReceipts_Then_MatchReadClusterFields()
        {
            var dir = CreateTempDirectory();
            try
            {
                var layout = new FreezerLayout(dir);
                using var freezer = Freezer.Open(layout, FreezerOpenMode.Append);
                using (var batch = freezer.BeginBatch())
                {
                    for (var block = 0L; block < 3; block++)
                        batch.AppendCluster(block, ClusterFor(block));
                    batch.Commit();
                }

                for (var block = 0L; block < 3; block++)
                {
                    var cluster = freezer.ReadCluster(block);
                    Assert.Equal(cluster.Hash, freezer.ReadHash(block));
                    Assert.Equal(cluster.Body, freezer.ReadBody(block));
                    Assert.Equal(cluster.Receipts, freezer.ReadReceipts(block));
                }
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_TablesRollAtDifferentRates_When_SealedHead_Then_ReturnsMinAcrossNamedTables()
        {
            const long tinyMaxFileSize = 40;
            var dir = CreateTempDirectory();
            try
            {
                var layout = new FreezerLayout(dir, tinyMaxFileSize);
                using var freezer = Freezer.Open(layout, FreezerOpenMode.Append);
                using (var batch = freezer.BeginBatch())
                {
                    for (var block = 0L; block < 20; block++)
                        batch.AppendCluster(block, ClusterFor(block));
                    batch.Commit();
                }

                var hashesSealed = freezer.SealedHead("hashes");
                var bodiesSealed = freezer.SealedHead("bodies");
                var receiptsSealed = freezer.SealedHead("receipts");
                var combined = freezer.SealedHead("hashes", "bodies", "receipts");

                Assert.Equal(Math.Min(hashesSealed, Math.Min(bodiesSealed, receiptsSealed)), combined);
                Assert.True(combined <= freezer.Items, "sealed boundary must never exceed the archive head");
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_TablesAllFitOneUnrolledFile_When_CommittedHead_Then_CoversEveryFsyncedItem_UnlikeSealedHead()
        {
            var dir = CreateTempDirectory();
            try
            {
                var layout = new FreezerLayout(dir);
                using var freezer = Freezer.Open(layout, FreezerOpenMode.Append);
                using (var batch = freezer.BeginBatch())
                {
                    for (var block = 0L; block < 20; block++)
                        batch.AppendCluster(block, ClusterFor(block));
                    batch.Commit();
                }

                Assert.Equal(0, freezer.SealedHead("hashes", "bodies", "receipts"));
                Assert.Equal(freezer.Items, freezer.CommittedHead("hashes", "bodies", "receipts"));
                Assert.Equal(20, freezer.CommittedHead("hashes", "bodies", "receipts"));
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_ItemsAppendedButNotCommitted_When_CommittedHead_Then_ExcludesTheUnsyncedTail()
        {
            var dir = CreateTempDirectory();
            try
            {
                var layout = new FreezerLayout(dir);
                using var freezer = Freezer.Open(layout, FreezerOpenMode.Append);
                using (var batch = freezer.BeginBatch())
                {
                    for (var block = 0L; block < 10; block++)
                        batch.AppendCluster(block, ClusterFor(block));
                    batch.Commit();
                }

                var batch2 = freezer.BeginBatch();
                for (var block = 10L; block < 15; block++)
                    batch2.AppendCluster(block, ClusterFor(block));
                Assert.Equal(10, freezer.CommittedHead("hashes", "bodies", "receipts"));
                batch2.Commit();
                Assert.Equal(15, freezer.CommittedHead("hashes", "bodies", "receipts"));
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_NoTableNames_When_SealedHead_Then_Throws()
        {
            var dir = CreateTempDirectory();
            try
            {
                var layout = new FreezerLayout(dir);
                using var freezer = Freezer.Open(layout, FreezerOpenMode.Append);
                Assert.Throws<ArgumentException>(() => freezer.SealedHead());
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        private static void WriteLockstepTable(string dir, FreezerTableConfig config, long virtualTail,
            long physicalItemCount, long physicalStart)
        {
            var paths = new FreezerTablePaths(dir, config.Name, config.UseCompression);
            IItemCodec<byte[]> codec = config.UseCompression
                ? new SnappyItemCodec<byte[]>(new IdentityByteCodec())
                : new IdentityByteCodec();

            using var table = FreezerTable<byte[]>.OpenForAppend(paths, codec, RollingDataFiles.DefaultMaxFileSize);
            for (var i = 0L; i < physicalItemCount; i++)
                table.Append(i, new byte[] { (byte)(physicalStart + i) });

            table.SyncIndex();
            table.SyncData();
            table.PersistMeta();

            if (virtualTail != 0)
            {
                var meta = new FreezerTableMeta(FreezerTableMeta.SupportedVersion, virtualTail,
                    (physicalItemCount + 1) * FreezerIndexEntry.Size);
                meta.WriteAtomic(paths.MetaPath);
            }
        }
    }
}
