using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Stores;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class HistoryWriteStallBackpressureTests
    {
        private sealed class FakeProps
        {
            private readonly Dictionary<(string prop, string cf), string> _values = new();
            public void Set(string property, string cf, string value) => _values[(property, cf ?? "")] = value;
            public string Read(string property, string cf)
                => _values.TryGetValue((property, cf ?? ""), out var v) ? v : "0";
        }

        private static RocksDbWritePressureMonitor MonitorOver(FakeProps props)
            => new RocksDbWritePressureMonitor(dataDir: Path.GetTempPath(), readProperty: props.Read);

        [Fact]
        public void WriteStop_OnHistoryCf_PausesThenResumesWithHysteresis()
        {
            var props = new FakeProps();
            var monitor = MonitorOver(props);

            Assert.False(monitor.ShouldPauseHistoryWrites());

            props.Set("rocksdb.is-write-stopped", HistoryColumnFamilies.ReceiptBody, "1");
            props.Set("rocksdb.num-immutable-mem-table", HistoryColumnFamilies.ReceiptBody, "3");
            Assert.True(monitor.ShouldPauseHistoryWrites());
            Assert.Contains("WRITE-STOP", monitor.DescribeHistoryBackpressure());

            props.Set("rocksdb.is-write-stopped", HistoryColumnFamilies.ReceiptBody, "0");
            props.Set("rocksdb.num-immutable-mem-table", HistoryColumnFamilies.ReceiptBody, "2");
            Assert.True(monitor.ShouldPauseHistoryWrites());

            props.Set("rocksdb.num-immutable-mem-table", HistoryColumnFamilies.ReceiptBody, "1");
            Assert.False(monitor.ShouldPauseHistoryWrites());
        }

        [Fact]
        public void ImmutableMemtableSaturation_OnHistoryCf_PausesBeforeHardStop()
        {
            var props = new FakeProps();
            var monitor = MonitorOver(props);

            props.Set("rocksdb.num-immutable-mem-table", HistoryColumnFamilies.TxBody, "1");
            Assert.False(monitor.ShouldPauseHistoryWrites());

            props.Set("rocksdb.num-immutable-mem-table", HistoryColumnFamilies.TxBody, "2");
            Assert.True(monitor.ShouldPauseHistoryWrites());
            Assert.Contains("flush-pipeline saturation", monitor.DescribeHistoryBackpressure());

            props.Set("rocksdb.num-immutable-mem-table", HistoryColumnFamilies.TxBody, "0");
            Assert.False(monitor.ShouldPauseHistoryWrites());
        }

        [Fact]
        public void HistoryValve_IgnoresStateCfMemtableSaturation()
        {
            var props = new FakeProps();
            var monitor = MonitorOver(props);

            props.Set("rocksdb.num-immutable-mem-table", RocksDbManager.CF_STATE_ACCOUNTS, "4");
            props.Set("rocksdb.is-write-stopped", RocksDbManager.CF_STATE_ACCOUNTS, "1");

            Assert.False(monitor.ShouldPauseHistoryWrites());
        }

        [Fact]
        public void ExistingDebtValve_StillEngages_ThroughTheSeam()
        {
            var props = new FakeProps();
            var monitor = MonitorOver(props);

            const long sixtyOneGb = 61L * 1024 * 1024 * 1024;
            props.Set("rocksdb.estimate-pending-compaction-bytes", HistoryColumnFamilies.ReceiptBody, sixtyOneGb.ToString());
            Assert.True(monitor.ShouldPauseHistoryWrites());
            Assert.Contains("compaction debt", monitor.DescribeHistoryBackpressure());

            props.Set("rocksdb.estimate-pending-compaction-bytes", HistoryColumnFamilies.ReceiptBody, "0");
            Assert.False(monitor.ShouldPauseHistoryWrites());
        }

        [Fact]
        public void DebtValve_WatchesAllBulkHistoryCfs_NotJustBodies()
        {
            var props = new FakeProps();
            var monitor = MonitorOver(props);

            const long sixtyOneGb = 61L * 1024 * 1024 * 1024;
            props.Set("rocksdb.estimate-pending-compaction-bytes", HistoryColumnFamilies.TxHashIndex, sixtyOneGb.ToString());
            Assert.True(monitor.ShouldPauseHistoryWrites());
        }

        [Fact]
        public void DebtValve_DeepResume_StaysPausedUntilDrainedWellDown()
        {
            var props = new FakeProps();
            var monitor = MonitorOver(props);
            static string Gb(double n) => ((long)(n * 1024 * 1024 * 1024)).ToString();

            props.Set("rocksdb.estimate-pending-compaction-bytes", HistoryColumnFamilies.ReceiptBody, Gb(61));
            Assert.True(monitor.ShouldPauseHistoryWrites());

            props.Set("rocksdb.estimate-pending-compaction-bytes", HistoryColumnFamilies.ReceiptBody, Gb(30));
            Assert.True(monitor.ShouldPauseHistoryWrites());

            props.Set("rocksdb.estimate-pending-compaction-bytes", HistoryColumnFamilies.ReceiptBody, Gb(15));
            Assert.True(monitor.ShouldPauseHistoryWrites());

            props.Set("rocksdb.estimate-pending-compaction-bytes", HistoryColumnFamilies.ReceiptBody, Gb(5));
            Assert.False(monitor.ShouldPauseHistoryWrites());
        }

        [Fact]
        public void L0Valve_OnHistoryCf_PausesAndResumesWithHysteresis()
        {
            var props = new FakeProps();
            var monitor = MonitorOver(props);

            props.Set("rocksdb.num-files-at-level0", HistoryColumnFamilies.BlockHashIndex, "13");
            Assert.True(monitor.ShouldPauseHistoryWrites());
            Assert.Contains("level-0 backlog", monitor.DescribeHistoryBackpressure());

            props.Set("rocksdb.num-files-at-level0", HistoryColumnFamilies.BlockHashIndex, "8");
            Assert.True(monitor.ShouldPauseHistoryWrites());

            props.Set("rocksdb.num-files-at-level0", HistoryColumnFamilies.BlockHashIndex, "5");
            Assert.False(monitor.ShouldPauseHistoryWrites());
        }

        [Fact]
        public void HistorySignals_ReadThroughHistoryScopeDelegate_NotCore()
        {
            var history = new FakeProps();
            history.Set("rocksdb.num-immutable-mem-table", HistoryColumnFamilies.ReceiptBody, "2");

            var historyCfs = new[]
            {
                HistoryColumnFamilies.TxBody, HistoryColumnFamilies.ReceiptBody,
                HistoryColumnFamilies.BlockHeader, HistoryColumnFamilies.BlockMeta,
                HistoryColumnFamilies.TxHashIndex, HistoryColumnFamilies.BlockHashIndex,
            };
            string Core(string property, string cf)
            {
                if (cf != null && System.Array.IndexOf(historyCfs, cf) >= 0)
                    throw new System.InvalidOperationException($"history CF '{cf}' read through the CORE delegate");
                return "0";
            }

            var monitor = new RocksDbWritePressureMonitor(Path.GetTempPath(), Core, history.Read);
            Assert.True(monitor.ShouldPauseHistoryWrites());
        }

        [Theory]
        [InlineData("rocksdb.is-write-stopped")]
        [InlineData("rocksdb.num-immutable-mem-table")]
        public void RealRocksDb_ReturnsStallProperty_ForHistoryCfs(string property)
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_histstallprobe_{Guid.NewGuid():N}");
            try
            {
                using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
                foreach (var cf in new[] { HistoryColumnFamilies.TxBody, HistoryColumnFamilies.ReceiptBody })
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
    }
}
