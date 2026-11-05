using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using RocksDbSharp;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class StoreFaultTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"store_fault_{Guid.NewGuid():N}");

        public StoreFaultTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        [Fact]
        public void Given_AFlushedColumnFamilyWhoseSstIsCorruptedOnDisk_When_ACompactionAndAWriteSurfaceTheCorruption_Then_TheBundleReportsAFatalStoreFaultNamingCorruption()
        {
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            using var bundle = RocksDbChainStoreBundle.FromManager(manager, _dir, journalOptions: null, ownsManager: false);
            bundle.SetPressureMonitorForTests(new Nethereum.CoreChain.RocksDB.Stores.RocksDbWritePressureMonitor(_dir, (property, cf) => "0"));
            var key = new byte[32];
            for (var generation = 0; generation < 2; generation++)
            {
                for (var i = 0; i < 20000; i++)
                {
                    BitConverter.GetBytes(i).CopyTo(key, 0);
                    manager.Put(RocksDbManager.CF_STATE_TRIE_STORAGE, key, new byte[200]);
                }
                FlushColumnFamily(manager, RocksDbManager.CF_STATE_TRIE_STORAGE);
            }
            Assert.NotEmpty(Directory.GetFiles(_dir, "*.sst"));
            Assert.Null(FaultOf(bundle));

            CorruptEverySst();

            try { manager.CompactColumnFamily(RocksDbManager.CF_STATE_TRIE_STORAGE); } catch (RocksDbException) { }
            for (var attempt = 0; attempt < 50 && FaultOf(bundle) == null; attempt++)
            {
                try
                {
                    using var batch = manager.CreateWriteBatch();
                    batch.Put(key, new byte[] { 1 }, manager.GetColumnFamily(RocksDbManager.CF_METADATA));
                    manager.Write(batch);
                }
                catch (RocksDbException) { }
                System.Threading.Thread.Sleep(100);
            }

            var fault = FaultOf(bundle);
            Assert.NotNull(fault);
            Assert.Contains("Corruption", fault);
        }

        [Fact]
        public void Given_a_real_corruption_background_error_and_no_writes_in_flight_When_the_monitor_polls_Then_the_bundle_reports_a_fatal_store_fault()
        {
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            using var bundle = RocksDbChainStoreBundle.FromManager(manager, _dir, journalOptions: null, ownsManager: false);
            var key = new byte[32];
            for (var generation = 0; generation < 2; generation++)
            {
                for (var i = 0; i < 20000; i++)
                {
                    BitConverter.GetBytes(i).CopyTo(key, 0);
                    manager.Put(RocksDbManager.CF_STATE_TRIE_STORAGE, key, new byte[200]);
                }
                FlushColumnFamily(manager, RocksDbManager.CF_STATE_TRIE_STORAGE);
            }
            Assert.Null(FaultOf(bundle));

            CorruptEverySst();
            try { manager.CompactColumnFamily(RocksDbManager.CF_STATE_TRIE_STORAGE); } catch (RocksDbException) { }

            Assert.Contains("rocksdb.background-errors", FaultOf(bundle));
        }

        [Fact]
        public void Given_AHealthyManager_When_WritesSucceed_Then_NoFatalStoreFaultIsRecorded()
        {
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            using var bundle = RocksDbChainStoreBundle.FromManager(manager, _dir, journalOptions: null, ownsManager: false);
            using (var batch = manager.CreateWriteBatch())
            {
                batch.Put(new byte[] { 1 }, new byte[] { 2 }, manager.GetColumnFamily(RocksDbManager.CF_METADATA));
                manager.Write(batch);
            }

            Assert.Null(FaultOf(bundle));
        }

        private static void FlushColumnFamily(RocksDbManager manager, string columnFamily)
        {
            var options = Native.Instance.rocksdb_flushoptions_create();
            try
            {
                Native.Instance.rocksdb_flush_cf(
                    manager.Database.Handle, options, manager.GetColumnFamily(columnFamily).Handle, out var error);
                if (error != IntPtr.Zero) throw new RocksDbException(error);
            }
            finally
            {
                Native.Instance.rocksdb_flushoptions_destroy(options);
            }
        }

        private static string FaultOf(object bundle) => (bundle as IStoreFaultSource)?.FatalStoreFault;

        private void CorruptEverySst()
        {
            foreach (var sst in Directory.GetFiles(_dir, "*.sst"))
            {
                using var fs = new FileStream(sst, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
                var length = fs.Length;
                var garbage = Enumerable.Repeat((byte)0xA5, 256).ToArray();
                for (long offset = 64; offset + garbage.Length < length - 1024; offset += 4096)
                {
                    fs.Position = offset;
                    fs.Write(garbage, 0, garbage.Length);
                }
            }
        }
    }
}
