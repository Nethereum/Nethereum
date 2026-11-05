using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Trait("Slice", "Flat")]
    public class SnapBootstrapStateWipeFreesDiskTests : IDisposable
    {
        private const int AccountCount = 20000;
        private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"snap_wipe_disk_{Guid.NewGuid():N}");

        public void Dispose()
        {
            try { if (Directory.Exists(_dataDir)) Directory.Delete(_dataDir, recursive: true); } catch { }
        }

        [Fact]
        public async Task Given_ASeededStateOnDisk_When_TheSnapBootstrapStateIsWiped_Then_TheWipedSstFilesAreGoneBeforeTheDatabaseIsClosed()
        {
            await SeedState();
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);
            Assert.True(SstFiles().Sum(f => f.Length) > 500_000, "seeding must leave state SSTs on disk");

            await bundle.ResetSnapBootstrapStateAsync();

            Assert.True(SstFiles().Sum(f => f.Length) < 100_000,
                $"{SstFiles().Count()} SST files / {SstFiles().Sum(f => f.Length)} bytes left after the wipe");
        }

        [Fact]
        public async Task Given_ASeededStateAndChainMetadata_When_TheSnapBootstrapStateIsWiped_Then_TheNonWipedMetadataIsIntactAndTheStateIsEmpty()
        {
            await SeedState();
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);

            await bundle.ResetSnapBootstrapStateAsync();

            Assert.Equal(42UL, bundle.Metadata.GetLastFetchedHeader());
            Assert.Null(await bundle.State.GetAccountAsync(AddressOf(7)));
            await bundle.State.SaveAccountAsync(AddressOf(1), new Account { Balance = 5, Nonce = 1 });
            Assert.Equal(5, (await bundle.State.GetAccountAsync(AddressOf(1))).Balance);
        }

        [Fact]
        public void Given_AColumnFamilyHandleTakenBeforeTheWipe_When_TheColumnFamilyIsWiped_Then_TheOldHandleCanStillBeQueriedAndTheNewOneIsEmpty()
        {
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dataDir });
            var key = new byte[32];
            for (var i = 0; i < 5000; i++)
            {
                BitConverter.GetBytes(i).CopyTo(key, 0);
                manager.Put(RocksDbManager.CF_STATE_ACCOUNTS, key, new byte[200]);
            }
            manager.Flush();
            var before = manager.GetColumnFamily(RocksDbManager.CF_STATE_ACCOUNTS);

            manager.WipeColumnFamily(RocksDbManager.CF_STATE_ACCOUNTS);

            using var lease = manager.Lease();
            Assert.NotNull(lease.Database.GetProperty("rocksdb.total-sst-files-size", before));
            Assert.Equal("0", lease.Database.GetProperty("rocksdb.total-sst-files-size", manager.GetColumnFamily(RocksDbManager.CF_STATE_ACCOUNTS)));
            Assert.Empty(SstFiles());
        }

        [Fact]
        public async Task Given_ADisposeAndAWipeQueuedBehindACompaction_When_TheCompactionEnds_Then_BothFinishWithoutDeadlock()
        {
            var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dataDir });
            var key = new byte[32];
            for (var batch = 0; batch < 12; batch++)
            {
                for (var i = 0; i < 40000; i++)
                {
                    BitConverter.GetBytes(batch * 40000 + i).CopyTo(key, 0);
                    manager.Put(RocksDbManager.CF_STATE_STORAGE, key, new byte[300]);
                }
                manager.Flush();
            }
            var compaction = Task.Run(() => manager.CompactColumnFamily(RocksDbManager.CF_STATE_STORAGE));
            await Task.Delay(200);
            var dispose = Task.Run(manager.Dispose);
            await Task.Delay(200);
            var wipe = Task.Run(() => manager.WipeColumnFamily(RocksDbManager.CF_STATE_ACCOUNTS));

            var all = Task.WhenAll(compaction, wipe, dispose);
            var finished = await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(60)));

            Assert.Same(all, finished);
            var errors = new[] { compaction, wipe, dispose }
                .Where(t => t.IsFaulted).SelectMany(t => t.Exception.InnerExceptions)
                .Where(e => e is not ObjectDisposedException);
            Assert.Empty(errors);
        }

        private async Task SeedState()
        {
            using var seeding = RocksDbChainStoreBundle.Open(_dataDir);
            for (var i = 0; i < AccountCount; i++)
                await seeding.State.SaveAccountAsync(AddressOf(i), new Account { Balance = 100 + i, Nonce = i });
            seeding.Metadata.SetLastFetchedHeader(42);
        }

        private System.Collections.Generic.IEnumerable<FileInfo> SstFiles()
            => new DirectoryInfo(_dataDir).EnumerateFiles("*.sst", SearchOption.AllDirectories);

        private static string AddressOf(int i) => "0x" + (i + 1).ToString("x40");
    }
}
