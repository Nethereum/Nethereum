using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class StateWriteStallBackpressureTests
    {
        private sealed class PerCfProps
        {
            private readonly Dictionary<(string Property, string Cf), string> _perCf = new();
            private readonly Dictionary<string, string> _everyCf = new();

            public void Set(string property, string value) => _everyCf[property] = value;

            public void Set(string property, string cf, string value) => _perCf[(property, cf)] = value;

            public string Read(string property, string cf)
            {
                if (_perCf.TryGetValue((property, cf), out var perCf)) return perCf;
                return _everyCf.TryGetValue(property, out var everyCf) ? everyCf : "0";
            }
        }

        private static RocksDbWritePressureMonitor MonitorOver(PerCfProps props)
            => new RocksDbWritePressureMonitor(dataDir: Path.GetTempPath(), readProperty: props.Read);

        [Fact]
        public void Given_AHardWriteStop_When_ItClearsButImmutableMemtablesStayAboveTheResumeDepth_Then_TheStreamStaysPausedUntilTheQueueDrains()
        {
            var props = new PerCfProps();
            var monitor = MonitorOver(props);

            Assert.False(monitor.ShouldPauseStateWrites());

            props.Set("rocksdb.is-write-stopped", "1");
            props.Set("rocksdb.num-immutable-mem-table", "3");
            Assert.True(monitor.ShouldPauseStateWrites());
            Assert.Contains("WRITE-STOP", monitor.DescribeStateBackpressure());

            props.Set("rocksdb.is-write-stopped", "0");
            props.Set("rocksdb.num-immutable-mem-table", "2");
            Assert.True(monitor.ShouldPauseStateWrites());

            props.Set("rocksdb.num-immutable-mem-table", "1");
            Assert.False(monitor.ShouldPauseStateWrites());
        }

        [Fact]
        public void Given_ImmutableMemtablesQueueingWithoutAHardStop_When_TheQueueReachesThePauseDepth_Then_TheStreamPausesAndResumesOnceDrained()
        {
            var props = new PerCfProps();
            var monitor = MonitorOver(props);

            props.Set("rocksdb.num-immutable-mem-table", "1");
            Assert.False(monitor.ShouldPauseStateWrites());

            props.Set("rocksdb.num-immutable-mem-table", "2");
            Assert.True(monitor.ShouldPauseStateWrites());
            Assert.Contains("flush-pipeline saturation", monitor.DescribeStateBackpressure());

            props.Set("rocksdb.num-immutable-mem-table", "0");
            Assert.False(monitor.ShouldPauseStateWrites());
        }

        [Fact]
        public void Given_StateLevelZeroFilesAboveThePauseThreshold_When_PressureIsEvaluated_Then_TheStreamPausesAndResumesOnceLevelZeroClears()
        {
            var props = new PerCfProps();
            var monitor = MonitorOver(props);

            props.Set("rocksdb.num-files-at-level0", "13");
            Assert.True(monitor.ShouldPauseStateWrites());

            props.Set("rocksdb.num-files-at-level0", "0");
            Assert.False(monitor.ShouldPauseStateWrites());
        }

        [Fact]
        public void Given_AUniversalTrieWithFewLevelZeroFilesAndAManyFileBottomLevel_When_PressureIsEvaluated_Then_TheLeafStreamIsNotPaused()
        {
            var props = new PerCfProps();
            var monitor = MonitorOver(props);

            props.Set("rocksdb.num-files-at-level0", RocksDbManager.CF_STATE_TRIE_STORAGE, "3");
            props.Set("rocksdb.num-files-at-level6", RocksDbManager.CF_STATE_TRIE_STORAGE, "60");
            props.Set("rocksdb.num-files-at-level0", RocksDbManager.CF_STATE_TRIE_ACCOUNT, "3");
            props.Set("rocksdb.num-files-at-level6", RocksDbManager.CF_STATE_TRIE_ACCOUNT, "20");

            Assert.False(monitor.ShouldPauseStateWrites());
        }

        [Theory]
        [InlineData("rocksdb.is-write-stopped")]
        [InlineData("rocksdb.num-immutable-mem-table")]
        [InlineData("rocksdb.mem-table-flush-pending")]
        public void Given_AHealthyEmptyRealDatabase_When_AStallPropertyIsReadForEachStateCf_Then_ItParsesAsZero(string property)
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_stallprobe_{Guid.NewGuid():N}");
            try
            {
                using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
                foreach (var cf in new[]
                {
                    RocksDbManager.CF_STATE_ACCOUNTS,
                    RocksDbManager.CF_STATE_STORAGE,
                    RocksDbManager.CF_STATE_TRIE_ACCOUNT,
                    RocksDbManager.CF_STATE_TRIE_STORAGE,
                })
                {
                    var raw = manager.Database.GetProperty(property, manager.GetColumnFamily(cf));
                    Assert.False(string.IsNullOrEmpty(raw),
                        $"RocksDbSharp returned no value for {property} on {cf}");
                    Assert.True(long.TryParse(raw, out var value),
                        $"{property} on {cf} was not integer-parseable: '{raw}'");
                    Assert.Equal(0, value);
                }
            }
            finally
            {
                if (Directory.Exists(dbPath)) { try { Directory.Delete(dbPath, true); } catch { } }
            }
        }

        [Fact]
        public void Given_ARealUniversalTrieCf_When_PendingCompactionBytesIsRead_Then_ItIsZeroWhileTheLevelZeroFileCountStaysReadable()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_universalprobe_{Guid.NewGuid():N}");
            try
            {
                using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
                foreach (var cf in new[] { RocksDbManager.CF_STATE_TRIE_ACCOUNT, RocksDbManager.CF_STATE_TRIE_STORAGE })
                {
                    var raw = manager.Database.GetProperty("rocksdb.estimate-pending-compaction-bytes", manager.GetColumnFamily(cf));
                    Assert.Equal("0", raw);
                    var levelZeroFilesRaw = manager.Database.GetProperty("rocksdb.num-files-at-level0", manager.GetColumnFamily(cf));
                    Assert.True(long.TryParse(levelZeroFilesRaw, out _),
                        $"rocksdb.num-files-at-level0 on {cf} was not integer-parseable: '{levelZeroFilesRaw}'");
                }
            }
            finally
            {
                if (Directory.Exists(dbPath)) { try { Directory.Delete(dbPath, true); } catch { } }
            }
        }

        [Fact]
        public void Given_AHealthyEmptyRealDatabase_When_TheDbWideDelayedWriteRateIsRead_Then_ItParsesAsZero()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_delayprobe_{Guid.NewGuid():N}");
            try
            {
                using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
                var raw = manager.Database.GetProperty("rocksdb.actual-delayed-write-rate");
                Assert.False(string.IsNullOrEmpty(raw));
                Assert.True(ulong.TryParse(raw, out var rate));
                Assert.Equal(0UL, rate);
            }
            finally
            {
                if (Directory.Exists(dbPath)) { try { Directory.Delete(dbPath, true); } catch { } }
            }
        }
    }
}
