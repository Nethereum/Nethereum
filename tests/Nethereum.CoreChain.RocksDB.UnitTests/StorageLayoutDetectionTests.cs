using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class StorageLayoutDetectionTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"layoutdetect_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        [Fact]
        public void Given_IdentityFileInDataDir_When_Detect_Then_ExistingSingle()
        {
            var dir = Path.Combine(_root, "single");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "IDENTITY"), "marker");

            var layout = RocksDbChainStoreBundle.DetectStorageLayout(dir);

            Assert.Equal(RocksDbChainStoreBundle.StorageLayout.ExistingSingle, layout);
        }

        [Fact]
        public void Given_IdentityInCoreSubdir_When_Detect_Then_ExistingSplit()
        {
            var dir = Path.Combine(_root, "split");
            var coreDir = Path.Combine(dir, RocksDbChainStoreBundle.CoreSubDir);
            Directory.CreateDirectory(coreDir);
            File.WriteAllText(Path.Combine(coreDir, "IDENTITY"), "marker");

            var layout = RocksDbChainStoreBundle.DetectStorageLayout(dir);

            Assert.Equal(RocksDbChainStoreBundle.StorageLayout.ExistingSplit, layout);
        }

        [Fact]
        public void Given_EmptyDir_When_Detect_Then_Fresh()
        {
            var dir = Path.Combine(_root, "fresh");
            Directory.CreateDirectory(dir);

            var layout = RocksDbChainStoreBundle.DetectStorageLayout(dir);

            Assert.Equal(RocksDbChainStoreBundle.StorageLayout.Fresh, layout);
        }

        [Fact]
        public void Given_NonExistentDir_When_Detect_Then_Fresh()
        {
            var dir = Path.Combine(_root, "does-not-exist-yet");

            var layout = RocksDbChainStoreBundle.DetectStorageLayout(dir);

            Assert.Equal(RocksDbChainStoreBundle.StorageLayout.Fresh, layout);
        }

        [Fact]
        public void Given_CoreSubdirWithoutIdentity_When_Detect_Then_Fresh()
        {
            var dir = Path.Combine(_root, "partial-split-init");
            var coreDir = Path.Combine(dir, RocksDbChainStoreBundle.CoreSubDir);
            Directory.CreateDirectory(coreDir);

            var layout = RocksDbChainStoreBundle.DetectStorageLayout(dir);

            Assert.Equal(RocksDbChainStoreBundle.StorageLayout.Fresh, layout);
        }

        [Theory]
        [InlineData(RocksDbChainStoreBundle.StorageLayout.ExistingSingle, true, false)]
        [InlineData(RocksDbChainStoreBundle.StorageLayout.ExistingSingle, false, false)]
        [InlineData(RocksDbChainStoreBundle.StorageLayout.ExistingSplit, true, true)]
        [InlineData(RocksDbChainStoreBundle.StorageLayout.ExistingSplit, false, true)]
        [InlineData(RocksDbChainStoreBundle.StorageLayout.Fresh, true, true)]
        [InlineData(RocksDbChainStoreBundle.StorageLayout.Fresh, false, false)]
        public void ResolveEffectiveSplit_HonoursOnDiskLayoutOverRequest(
            RocksDbChainStoreBundle.StorageLayout layout, bool requestedSplit, bool expectedUseSplit)
        {
            var useSplit = RocksDbChainStoreBundle.ResolveEffectiveSplit(layout, requestedSplit);

            Assert.Equal(expectedUseSplit, useSplit);
        }

        [Fact]
        public void Open_ExistingSingleDbDir_WithSplitRequested_StaysSingle_NoCoreHistoryCreated()
        {
            var dir = Path.Combine(_root, "existing-single");

            using (var seeded = RocksDbChainStoreBundle.Open(dir))
            {
                seeded.Metadata.SetLastFetchedBody(123UL);
            }
            Assert.True(File.Exists(Path.Combine(dir, "IDENTITY")));

            using (var reopened = RocksDbChainStoreBundle.Open(dir, storageOptions: new RocksDbStorageOptions { SplitHistoryStore = true }))
            {
                Assert.False(Directory.Exists(Path.Combine(dir, RocksDbChainStoreBundle.CoreSubDir)));
                Assert.False(Directory.Exists(Path.Combine(dir, RocksDbChainStoreBundle.HistorySubDir)));

                Assert.Equal(123UL, reopened.Metadata.GetLastFetchedBody());
            }
        }

        [Fact]
        public void Open_ExistingSplitDbDir_WithSingleRequested_ForcesSplit_NoDataDirIdentityCreated()
        {
            var dir = Path.Combine(_root, "existing-split");

            using (var seeded = RocksDbChainStoreBundle.Open(dir, storageOptions: new RocksDbStorageOptions { SplitHistoryStore = true }))
            {
            }
            var coreDir = Path.Combine(dir, RocksDbChainStoreBundle.CoreSubDir);
            var historyDir = Path.Combine(dir, RocksDbChainStoreBundle.HistorySubDir);
            Assert.True(File.Exists(Path.Combine(coreDir, "IDENTITY")));

            using (var reopened = RocksDbChainStoreBundle.Open(dir, storageOptions: new RocksDbStorageOptions { SplitHistoryStore = false }))
            {
                Assert.False(File.Exists(Path.Combine(dir, "IDENTITY")));
                Assert.True(Directory.Exists(coreDir));
                Assert.True(Directory.Exists(historyDir));
            }
        }

        private static byte[] FillBytes(byte value) => Enumerable.Repeat(value, 32).ToArray();

        [Fact]
        public async Task InterruptedRestore_MidCommit_WithSplitRequested_RestoresData_NoOrphanedSplitDb()
        {
            var dir = Path.Combine(_root, "mid-restore-split");
            const string addr = "0x7777777777777777777777777777777777777788";
            string snapshotDir;
            using (var bundle = RocksDbChainStoreBundle.Open(dir))
            {
                await bundle.State.SaveAccountAsync(addr, new Account { Balance = 100, Nonce = 1 });
                await bundle.SaveCheckpointAsync(50, FillBytes(0xAA), FillBytes(0xBB));
                await bundle.State.SaveAccountAsync(addr, new Account { Balance = 999, Nonce = 9 });
                snapshotDir = bundle.ResolveCheckpointSnapshotPath(50);
            }

            var newDir = dir + ".restore-new";
            var oldDir = dir + ".restore-old";
            using (var temp = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = snapshotDir }))
                temp.CreateDatabaseCheckpoint(newDir);
            File.WriteAllText(Path.Combine(newDir, ".restore-ready"), "nethereum-checkpoint-restore-v1");
            Directory.Move(dir, oldDir);

            using (var reopened = RocksDbChainStoreBundle.Open(dir, storageOptions: new RocksDbStorageOptions { SplitHistoryStore = true }))
            {
                Assert.False(Directory.Exists(Path.Combine(dir, RocksDbChainStoreBundle.CoreSubDir)));
                Assert.False(Directory.Exists(Path.Combine(dir, RocksDbChainStoreBundle.HistorySubDir)));
                Assert.False(Directory.Exists(newDir));
                Assert.False(Directory.Exists(oldDir));

                var restored = await reopened.State.GetAccountAsync(addr);
                Assert.NotNull(restored);
                Assert.Equal((System.Numerics.BigInteger)100, (System.Numerics.BigInteger)restored.Balance);
            }
        }
    }
}
