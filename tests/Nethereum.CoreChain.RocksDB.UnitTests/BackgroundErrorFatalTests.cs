using System;
using System.IO;
using System.Collections.Generic;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class BackgroundErrorFatalTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"bg_error_{Guid.NewGuid():N}");

        public BackgroundErrorFatalTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        private static RocksDbWritePressureMonitor MonitorReporting(Dictionary<string, string> props)
            => new RocksDbWritePressureMonitor(
                Path.GetTempPath(), (property, cf) => props.TryGetValue(property, out var v) ? v : "0");

        [Fact]
        public void Given_a_rocksdb_background_error_When_the_next_poll_happens_Then_the_bundle_reports_a_fatal_store_fault_that_the_host_turns_into_follower_fatal()
        {
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            using var bundle = RocksDbChainStoreBundle.FromManager(manager, _dir, journalOptions: null, ownsManager: false);
            Assert.Null(((IStoreFaultSource)bundle).FatalStoreFault);

            bundle.SetPressureMonitorForTests(MonitorReporting(new Dictionary<string, string>
            {
                ["rocksdb.background-errors"] = "1",
            }));

            var fault = ((IStoreFaultSource)bundle).FatalStoreFault;
            Assert.NotNull(fault);
            Assert.Contains("rocksdb.background-errors", fault);
        }

        private static RocksDbWritePressureMonitor MonitorReportingWithWriteProbe(
            Dictionary<string, string> props, Func<string> probeWrite)
            => new RocksDbWritePressureMonitor(
                Path.GetTempPath(), (property, cf) => props.TryGetValue(property, out var v) ? v : "0", probeWrite);

        [Fact]
        public void Given_a_background_error_that_stops_writes_When_the_next_poll_happens_Then_the_bundle_reports_a_fatal_store_fault_naming_the_rejected_write()
        {
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            using var bundle = RocksDbChainStoreBundle.FromManager(manager, _dir, journalOptions: null, ownsManager: false);

            bundle.SetPressureMonitorForTests(MonitorReportingWithWriteProbe(
                new Dictionary<string, string> { ["rocksdb.background-errors"] = "1" },
                () => "Corruption: block checksum mismatch"));

            var fault = ((IStoreFaultSource)bundle).FatalStoreFault;
            Assert.NotNull(fault);
            Assert.Contains("rocksdb.background-errors", fault);
            Assert.Contains("Corruption", fault);
        }

        [Fact]
        public void Given_a_recovered_background_error_while_writes_are_accepted_When_the_next_poll_happens_Then_no_fatal_store_fault_is_reported()
        {
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            using var bundle = RocksDbChainStoreBundle.FromManager(manager, _dir, journalOptions: null, ownsManager: false);
            var probes = 0;

            bundle.SetPressureMonitorForTests(MonitorReportingWithWriteProbe(
                new Dictionary<string, string> { ["rocksdb.background-errors"] = "3" },
                () => { probes++; return null; }));

            Assert.Null(((IStoreFaultSource)bundle).FatalStoreFault);
            Assert.True(probes > 0);
        }

        [Fact]
        public void Given_only_retryable_pressure_When_the_next_poll_happens_Then_no_fatal_store_fault_is_reported()
        {
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            using var bundle = RocksDbChainStoreBundle.FromManager(manager, _dir, journalOptions: null, ownsManager: false);

            bundle.SetPressureMonitorForTests(MonitorReporting(new Dictionary<string, string>
            {
                ["rocksdb.is-write-stopped"] = "1",
                ["rocksdb.num-immutable-mem-table"] = "4",
                ["rocksdb.actual-delayed-write-rate"] = "1048576",
                ["rocksdb.num-files-at-level0"] = "30",
                ["rocksdb.background-errors"] = "0",
            }));

            Assert.Null(((IStoreFaultSource)bundle).FatalStoreFault);
        }

        [Fact]
        public void Given_a_busy_status_message_When_the_write_failure_is_classified_Then_it_is_transient_and_a_corruption_message_is_not()
        {
            Assert.True(RocksDbManager.IsTransientFlushStall("Writes have been stopped"));
            Assert.False(RocksDbManager.IsTransientFlushStall("Corruption: block checksum mismatch"));
        }
    }
}
