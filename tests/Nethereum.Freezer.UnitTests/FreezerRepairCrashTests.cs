using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Nethereum.Freezer.UnitTests
{
    public class FreezerRepairCrashTests
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

        private static FrozenBlockCluster ClusterFor(long blockNumber)
        {
            var header = new byte[] { 0xf9, (byte)blockNumber };
            var hash = Enumerable.Repeat((byte)blockNumber, 32).ToArray();
            var body = new byte[] { 0xc0, (byte)blockNumber };
            var receipts = new byte[] { 0xc0 };
            var bal = new byte[] { 0x00 };
            return new FrozenBlockCluster(header, hash, body, receipts, bal);
        }

        private static FreezerTable<byte[]> OpenRawTable(string dir, FreezerTableConfig config)
        {
            var paths = new FreezerTablePaths(dir, config.Name, config.UseCompression);
            IItemCodec<byte[]> codec = config.UseCompression
                ? new SnappyItemCodec<byte[]>(new IdentityByteCodec())
                : new IdentityByteCodec();
            return FreezerTable<byte[]>.OpenForAppend(paths, codec, RollingDataFiles.DefaultMaxFileSize);
        }

        private static void WriteBaselineThenTornCommit(string dir, FreezerLayout layout)
        {
            using (var freezer = Freezer.Open(layout, FreezerOpenMode.Append))
            {
                using var batch = freezer.BeginBatch();
                batch.AppendCluster(0, ClusterFor(0));
                batch.AppendCluster(1, ClusterFor(1));
                batch.Commit();
            }

            var configs = layout.Tables;
            var rawTables = configs.ToDictionary(c => c.Name, c => OpenRawTable(dir, c));
            try
            {
                foreach (var config in configs)
                {
                    var table = rawTables[config.Name];
                    for (var block = 2L; block < 5; block++)
                    {
                        var cluster = ClusterFor(block);
                        var item = config.Name switch
                        {
                            "headers" => cluster.Header,
                            "hashes" => cluster.Hash,
                            "bodies" => cluster.Body,
                            "receipts" => cluster.Receipts,
                            "bals" => cluster.Bal,
                            _ => throw new InvalidOperationException(),
                        };
                        table.Append(table.Count, item);
                    }
                }

                foreach (var table in rawTables.Values)
                {
                    table.SyncIndex();
                    table.SyncData();
                }

                rawTables["headers"].PersistMeta();
                rawTables["hashes"].PersistMeta();
                rawTables["bodies"].PersistMeta();
            }
            finally
            {
                foreach (var table in rawTables.Values)
                    table.Dispose();
            }
        }

        [Fact]
        public void Given_CommitInterruptedAfterSomeTablesMetaPersisted_When_Reopen_Then_MinHeadHealsAll()
        {
            var dir = CreateTempDirectory();
            try
            {
                var layout = new FreezerLayout(dir);
                WriteBaselineThenTornCommit(dir, layout);

                using var healed = Freezer.Open(layout, FreezerOpenMode.Append);

                Assert.Equal(2L, healed.Items);
                Assert.Equal(ClusterFor(0).Header, healed.ReadCluster(0).Header);
                Assert.Equal(ClusterFor(1).Header, healed.ReadCluster(1).Header);
                Assert.Throws<ArgumentOutOfRangeException>(() => healed.ReadCluster(2));

                using var batch = healed.BeginBatch();
                batch.AppendCluster(2, ClusterFor(2));
                batch.Commit();
                Assert.Equal(3L, healed.Items);
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        [Fact]
        public void Given_TornCommit_When_ReopenWithoutMinHeadRepair_Then_TablesDriftDetected()
        {
            var dir = CreateTempDirectory();
            try
            {
                var layout = new FreezerLayout(dir);
                WriteBaselineThenTornCommit(dir, layout);

                var rawTables = layout.Tables.ToDictionary(c => c.Name, c => OpenRawTable(dir, c));
                try
                {
                    Assert.Equal(5L, rawTables["headers"].Count);
                    Assert.Equal(5L, rawTables["hashes"].Count);
                    Assert.Equal(5L, rawTables["bodies"].Count);
                    Assert.Equal(2L, rawTables["receipts"].Count);
                    Assert.Equal(2L, rawTables["bals"].Count);

                    var heights = rawTables.Values.Select(t => t.Count).Distinct().ToList();
                    Assert.True(heights.Count > 1,
                        "per-table Repair alone must leave the five tables at different heights " +
                        "-- if this ever passes with heights.Count == 1, Freezer.Open's cross-table " +
                        "alignment is no longer load-bearing and this twin has gone vacuous.");
                }
                finally
                {
                    foreach (var table in rawTables.Values)
                        table.Dispose();
                }
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }
    }
}
