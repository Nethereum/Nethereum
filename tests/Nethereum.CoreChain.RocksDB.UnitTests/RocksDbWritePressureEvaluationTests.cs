using System.Collections.Generic;
using System.IO;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Stores;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbWritePressureEvaluationTests
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
        public void Given_AFreshMonitor_When_ShouldYieldToExecutionIsFirstEvaluated_Then_FirstEvaluation_Yields()
        {
            var monitor = MonitorOver(new FakeProps());

            Assert.True(monitor.ShouldYieldToExecution(100, 1_000));
        }

        [Fact]
        public void Given_AYieldingMonitor_When_TheHeadIsWithinTwoOfTheTip_Then_WithinTwo_StopsYielding()
        {
            var monitor = MonitorOver(new FakeProps());
            Assert.True(monitor.ShouldYieldToExecution(100, 1_000));

            Assert.True(monitor.ShouldYieldToExecution(997, 1_000));
            Assert.False(monitor.ShouldYieldToExecution(998, 1_000));
        }

        [Fact]
        public void Given_AYieldingMonitor_When_TheHeadReachesAStaticTip_Then_StaticTipReached_StopsYielding()
        {
            var monitor = MonitorOver(new FakeProps());
            Assert.True(monitor.ShouldYieldToExecution(100, 1_000));

            Assert.False(monitor.ShouldYieldToExecution(1_000, 1_000));
            Assert.False(monitor.ShouldYieldToExecution(1_000, 1_000));
        }

        [Fact]
        public void Given_ACaughtUpMonitor_When_TheTipMovesThirtyThreeAhead_Then_CaughtUpThen33Behind_YieldsAgain()
        {
            var monitor = MonitorOver(new FakeProps());
            Assert.False(monitor.ShouldYieldToExecution(1_000, 1_000));

            Assert.True(monitor.ShouldYieldToExecution(1_000, 1_033));
        }

        [Fact]
        public void Given_ACaughtUpMonitor_When_TheTipMovesThirtyTwoAhead_Then_CaughtUpThen32Behind_StillRuns()
        {
            var monitor = MonitorOver(new FakeProps());
            Assert.False(monitor.ShouldYieldToExecution(1_000, 1_000));

            Assert.False(monitor.ShouldYieldToExecution(1_000, 1_032));
        }

        [Fact]
        public void Given_AFreshMonitor_When_TheTrustedTipIsZero_Then_ZeroTip_NeverYields()
        {
            var monitor = MonitorOver(new FakeProps());

            Assert.False(monitor.ShouldYieldToExecution(0, 0));
            Assert.False(monitor.ShouldYieldToExecution(500, 0));
        }

        [Fact]
        public void Given_APropertyThatIsNotANumber_When_PressureIsEvaluated_Then_ItCountsAsZeroAndDoesNotPause()
        {
            var props = new FakeProps();
            var monitor = MonitorOver(props);
            foreach (var cf in new[] { HistoryColumnFamilies.TxBody, RocksDbManager.CF_STATE_ACCOUNTS })
            {
                props.Set("rocksdb.num-immutable-mem-table", cf, "not-a-number");
                props.Set("rocksdb.is-write-stopped", cf, "n/a");
                props.Set("rocksdb.estimate-pending-compaction-bytes", cf, "");
                props.Set("rocksdb.num-files-at-level0", cf, "x");
            }

            Assert.False(monitor.ShouldPauseHistoryWrites());
            Assert.False(monitor.ShouldPauseStateWrites());
        }

        [Fact]
        public void Given_IsWriteStoppedOnOneOfSeveralColumnFamilies_When_PressureIsEvaluated_Then_TheScopePausesEvenWithZeroImmutableMemtables()
        {
            var props = new FakeProps();
            var monitor = MonitorOver(props);
            props.Set("rocksdb.is-write-stopped", HistoryColumnFamilies.BlockHashIndex, "1");
            props.Set("rocksdb.num-immutable-mem-table", HistoryColumnFamilies.BlockHashIndex, "0");

            Assert.True(monitor.ShouldPauseHistoryWrites());
            Assert.False(monitor.ShouldPauseStateWrites());

            props.Set("rocksdb.is-write-stopped", HistoryColumnFamilies.BlockHashIndex, "0");
            props.Set("rocksdb.is-write-stopped", RocksDbManager.CF_STATE_TRIE_STORAGE, "1");

            Assert.False(monitor.ShouldPauseHistoryWrites());
            Assert.True(monitor.ShouldPauseStateWrites());
        }

        [Fact]
        public void Given_TheMemtableCountDropsToTheResumeThreshold_When_Evaluated_Then_TheStallFlagStaysSetUntilItIsBelowResume()
        {
            var props = new FakeProps();
            var monitor = MonitorOver(props);

            props.Set("rocksdb.num-immutable-mem-table", RocksDbManager.CF_STATE_STORAGE, "3");
            Assert.True(monitor.ShouldPauseStateWrites());

            props.Set("rocksdb.num-immutable-mem-table", RocksDbManager.CF_STATE_STORAGE, "2");
            Assert.True(monitor.ShouldPauseStateWrites());

            props.Set("rocksdb.num-immutable-mem-table", RocksDbManager.CF_STATE_STORAGE, "1");
            Assert.False(monitor.ShouldPauseStateWrites());

            props.Set("rocksdb.num-immutable-mem-table", RocksDbManager.CF_STATE_STORAGE, "2");
            Assert.True(monitor.ShouldPauseStateWrites());
        }
    }
}
