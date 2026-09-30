using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Stores;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FreezerIndexingWriteStallBackpressureTests
    {
        private sealed class FakeProps
        {
            private readonly Dictionary<(string prop, string cf), string> _values = new();
            public void Set(string property, string cf, string value) => _values[(property, cf ?? "")] = value;
            public string Read(string property, string cf)
                => _values.TryGetValue((property, cf ?? ""), out var v) ? v : "0";
        }

        private static RocksDbWritePressureMonitor MonitorOver(FakeProps freezerProps)
            => new RocksDbWritePressureMonitor(
                Path.GetTempPath(), (_, __) => "0", (_, __) => "0", freezerProps.Read);

        [Fact]
        public void NullFreezerHistorySeam_NeverSignals()
        {
            var monitor = new RocksDbWritePressureMonitor(dataDir: Path.GetTempPath(), readProperty: (_, __) => "999999999999");
            Assert.False(monitor.ShouldPauseFreezerIndexing());
        }

        [Fact]
        public void DebtValve_OnFreezerHistoryCf_PausesThenResumesWithHysteresis()
        {
            var props = new FakeProps();
            var monitor = MonitorOver(props);

            Assert.False(monitor.ShouldPauseFreezerIndexing());

            const long twentyFiveGb = 25L * 1024 * 1024 * 1024;
            props.Set("rocksdb.estimate-pending-compaction-bytes", HistoryColumnFamilies.BlockHashIndex, twentyFiveGb.ToString());
            Assert.True(monitor.ShouldPauseFreezerIndexing());
            Assert.Contains("compaction debt", monitor.DescribeFreezerIndexingBackpressure());

            const long sixteenGb = 16L * 1024 * 1024 * 1024;
            props.Set("rocksdb.estimate-pending-compaction-bytes", HistoryColumnFamilies.BlockHashIndex, sixteenGb.ToString());
            Assert.True(monitor.ShouldPauseFreezerIndexing());

            props.Set("rocksdb.estimate-pending-compaction-bytes", HistoryColumnFamilies.BlockHashIndex, "0");
            Assert.False(monitor.ShouldPauseFreezerIndexing());
        }

        [Fact]
        public void L0Valve_OnLogFilterMapsCf_PausesAndResumesWithHysteresis()
        {
            var props = new FakeProps();
            var monitor = MonitorOver(props);

            var abovePause = (RocksProfiles.LiveIndexL0SlowdownTrigger + 1).ToString();
            var inBand = ((RocksProfiles.LiveIndexL0SlowdownTrigger + RocksProfiles.LiveIndexL0CompactionTrigger) / 2).ToString();
            var belowResume = (RocksProfiles.LiveIndexL0CompactionTrigger - 1).ToString();

            props.Set("rocksdb.num-files-at-level0", HistoryColumnFamilies.LogFilterMaps, abovePause);
            Assert.True(monitor.ShouldPauseFreezerIndexing());
            Assert.Contains("level-0 backlog", monitor.DescribeFreezerIndexingBackpressure());

            props.Set("rocksdb.num-files-at-level0", HistoryColumnFamilies.LogFilterMaps, inBand);
            Assert.True(monitor.ShouldPauseFreezerIndexing());

            props.Set("rocksdb.num-files-at-level0", HistoryColumnFamilies.LogFilterMaps, belowResume);
            Assert.False(monitor.ShouldPauseFreezerIndexing());
        }

        [Fact]
        public void WriteStop_OnTxHashIndexCf_PausesThenResumesWithHysteresis()
        {
            var props = new FakeProps();
            var monitor = MonitorOver(props);

            props.Set("rocksdb.is-write-stopped", HistoryColumnFamilies.TxHashIndex, "1");
            props.Set("rocksdb.num-immutable-mem-table", HistoryColumnFamilies.TxHashIndex, "3");
            Assert.True(monitor.ShouldPauseFreezerIndexing());
            Assert.Contains("WRITE-STOP", monitor.DescribeFreezerIndexingBackpressure());

            props.Set("rocksdb.is-write-stopped", HistoryColumnFamilies.TxHashIndex, "0");
            props.Set("rocksdb.num-immutable-mem-table", HistoryColumnFamilies.TxHashIndex, "2");
            Assert.True(monitor.ShouldPauseFreezerIndexing());

            props.Set("rocksdb.num-immutable-mem-table", HistoryColumnFamilies.TxHashIndex, "1");
            Assert.False(monitor.ShouldPauseFreezerIndexing());
        }

        [Fact]
        public void FreezerIndexingPause_NeverPausesHistoryWrites()
        {
            var props = new FakeProps();
            var monitor = MonitorOver(props);

            const long sixtyOneGb = 61L * 1024 * 1024 * 1024;
            props.Set("rocksdb.estimate-pending-compaction-bytes", HistoryColumnFamilies.BlockHashIndex, sixtyOneGb.ToString());
            props.Set("rocksdb.is-write-stopped", HistoryColumnFamilies.LogFilterMaps, "1");
            props.Set("rocksdb.num-immutable-mem-table", HistoryColumnFamilies.LogFilterMaps, "5");

            Assert.True(monitor.ShouldPauseFreezerIndexing());
            Assert.False(monitor.ShouldPauseHistoryWrites());
        }

        [Fact]
        public void FreezerIndexingSignals_ReadThroughFreezerHistoryScopeDelegate_NotCoreOrHistory()
        {
            var freezer = new FakeProps();
            freezer.Set("rocksdb.num-immutable-mem-table", HistoryColumnFamilies.BlockHashIndex, "2");

            string ThrowingSeam(string property, string cf) =>
                throw new InvalidOperationException($"freezer-indexing CF '{cf}' read through the WRONG delegate");

            var monitor = new RocksDbWritePressureMonitor(
                Path.GetTempPath(), ThrowingSeam, ThrowingSeam, freezer.Read);

            Assert.True(monitor.ShouldPauseFreezerIndexing());
        }
    }
}
