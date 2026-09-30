using System.Collections.Generic;
using System.IO;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class StateLevelZeroValveColumnFamilyTests
    {
        private sealed class ColumnFamilyProps
        {
            private readonly Dictionary<(string Property, string Cf), string> _values = new();

            public void LevelZeroFiles(string cf, int files) => _values[("rocksdb.num-files-at-level0", cf)] = files.ToString();

            public void DelayedWriteRate(long bytesPerSecond) => _values[("rocksdb.actual-delayed-write-rate", null)] = bytesPerSecond.ToString();

            public string Read(string property, string cf)
                => _values.TryGetValue((property, cf), out var v) ? v : "0";
        }

        private static RocksDbWritePressureMonitor MonitorOver(ColumnFamilyProps props)
            => new RocksDbWritePressureMonitor(dataDir: Path.GetTempPath(), readProperty: props.Read);

        [Fact]
        public void Given_AUniversalTrieColumnFamilyHoldingMoreLevelZeroRunsThanTheLeveledPauseThreshold_When_PressureIsEvaluated_Then_TheLeafStreamKeepsRunning()
        {
            var props = new ColumnFamilyProps();
            var monitor = MonitorOver(props);

            props.LevelZeroFiles(RocksDbManager.CF_STATE_TRIE_STORAGE, 13);
            props.LevelZeroFiles(RocksDbManager.CF_STATE_TRIE_ACCOUNT, 13);

            Assert.False(monitor.ShouldPauseStateWrites());
        }

        [Fact]
        public void Given_ALeveledFlatColumnFamilyAboveTheLevelZeroPauseThreshold_When_ItDrainsBelowResume_Then_TheLeafStreamPausesAndThenResumes()
        {
            var props = new ColumnFamilyProps();
            var monitor = MonitorOver(props);

            props.LevelZeroFiles(RocksDbManager.CF_STATE_STORAGE, 13);
            Assert.True(monitor.ShouldPauseStateWrites());

            props.LevelZeroFiles(RocksDbManager.CF_STATE_STORAGE, 7);
            Assert.True(monitor.ShouldPauseStateWrites());

            props.LevelZeroFiles(RocksDbManager.CF_STATE_STORAGE, 5);
            Assert.False(monitor.ShouldPauseStateWrites());
        }

        [Fact]
        public void Given_AUniversalTrieColumnFamilyAboveTheLeveledEngineThresholdButFarBelowItsOwnSlowdownTrigger_When_PressureIsEvaluated_Then_TheLeafStreamKeepsRunning()
        {
            var props = new ColumnFamilyProps();
            var monitor = MonitorOver(props);

            props.LevelZeroFiles(RocksDbManager.CF_STATE_TRIE_STORAGE, 16);
            props.LevelZeroFiles(RocksDbManager.CF_STATE_TRIE_ACCOUNT, 16);

            Assert.False(monitor.ShouldPauseStateWrites());
        }

        [Fact]
        public void Given_RocksDbAlreadyDelayingWritesBecauseAUniversalTrieReachedItsSlowdownTrigger_When_PressureIsEvaluated_Then_TheEngineValvePauses()
        {
            var props = new ColumnFamilyProps();
            var monitor = MonitorOver(props);

            props.LevelZeroFiles(RocksDbManager.CF_STATE_TRIE_STORAGE, 201);
            props.DelayedWriteRate(16 * 1024 * 1024);

            Assert.True(monitor.ShouldPauseStateWrites());
            Assert.Contains("writes already delayed", monitor.DescribeStateBackpressure());
        }

        [Fact]
        public void Given_ALeveledNonStateColumnFamilyReachingTheEngineLevelZeroThreshold_When_PressureIsEvaluated_Then_TheEngineValvePauses()
        {
            var props = new ColumnFamilyProps();
            var monitor = MonitorOver(props);

            props.LevelZeroFiles(RocksDbManager.CF_BLOCKS, 16);

            Assert.True(monitor.ShouldPauseStateWrites());
            Assert.Contains($"engine pressure: {RocksDbManager.CF_BLOCKS} at 16 level-0 files", monitor.DescribeStateBackpressure());
        }
    }
}
