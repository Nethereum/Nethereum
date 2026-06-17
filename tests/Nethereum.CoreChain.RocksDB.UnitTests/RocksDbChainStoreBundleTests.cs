using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbChainStoreBundleTests : IDisposable
    {
        private readonly string _dataDir;

        public RocksDbChainStoreBundleTests()
        {
            _dataDir = Path.Combine(Path.GetTempPath(), $"bundle_test_{Guid.NewGuid():N}");
        }

        public void Dispose()
        {
            if (Directory.Exists(_dataDir))
            {
                try { Directory.Delete(_dataDir, recursive: true); } catch { }
            }
        }

        private static byte[] FillBytes(byte value) => Enumerable.Repeat(value, 32).ToArray();

        [Fact]
        public async Task SaveCheckpointAsync_WritesMetadataRowAndSnapshotDir()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);

            var cp = await bundle.SaveCheckpointAsync(100, FillBytes(0xAA), FillBytes(0xBB));

            Assert.Equal(100UL, cp.BlockNumber);
            Assert.Equal(FillBytes(0xAA), cp.StateRoot);
            Assert.Equal(FillBytes(0xBB), cp.BlockHash);

            var metadataRow = bundle.Metadata.GetCheckpoint(100);
            Assert.NotNull(metadataRow);

            var snapshotDir = bundle.ResolveCheckpointSnapshotPath(100);
            Assert.True(Directory.Exists(snapshotDir));
            Assert.EndsWith(Path.Combine(".cp", "000000000100"), snapshotDir);
        }

        [Fact]
        public async Task SaveCheckpointAsync_SnapshotDirAlreadyExists_RefreshesMetadataIdempotently()
        {
            // After a rewind-without-snapshot-restore that survives a process kill,
            // the on-disk .cp/<block>/ directory may still be present from a prior
            // SaveCheckpointAsync that never completed its metadata write. The next
            // re-execution past the same block-N must NOT throw — it should refresh
            // the metadata row to point at the recomputed state root and treat the
            // existing snapshot dir as canonical (we already paid the cost to create it).
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);
            var snapshotDir = bundle.ResolveCheckpointSnapshotPath(200);
            Directory.CreateDirectory(snapshotDir);

            var cp = await bundle.SaveCheckpointAsync(200, FillBytes(0x11), FillBytes(0x22));

            Assert.Equal(200UL, cp.BlockNumber);
            Assert.Equal(FillBytes(0x11), cp.StateRoot);
            var metadataRow = bundle.Metadata.GetCheckpoint(200);
            Assert.NotNull(metadataRow);
            Assert.Equal(FillBytes(0x11), metadataRow.Value.StateRoot);
            Assert.True(Directory.Exists(snapshotDir));
        }

        [Fact]
        public async Task SaveCheckpointAsync_PairsRowAndSnapshot_AcrossManyCalls()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);

            for (ulong i = 1; i <= 10; i++)
            {
                await bundle.SaveCheckpointAsync(i * 50, FillBytes((byte)i), FillBytes((byte)(i + 0x80)));
            }

            var listed = await bundle.ListCheckpointsAsync();
            Assert.Equal(10, listed.Count);
            foreach (var cp in listed)
            {
                Assert.True(Directory.Exists(bundle.ResolveCheckpointSnapshotPath(cp.BlockNumber)));
                Assert.NotNull(bundle.Metadata.GetCheckpoint(cp.BlockNumber));
            }
        }

        [Fact]
        public async Task ListCheckpointsAsync_FiltersOrphanedMetadataRows()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);
            await bundle.SaveCheckpointAsync(100, FillBytes(0xAA), FillBytes(0xBB));
            await bundle.SaveCheckpointAsync(200, FillBytes(0xCC), FillBytes(0xDD));

            Directory.Delete(bundle.ResolveCheckpointSnapshotPath(100), recursive: true);

            var listed = await bundle.ListCheckpointsAsync();
            Assert.Single(listed);
            Assert.Equal(200UL, listed[0].BlockNumber);
        }

        [Fact]
        public async Task DeleteCheckpointAsync_RemovesBothRowAndSnapshotDir()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);
            await bundle.SaveCheckpointAsync(100, FillBytes(0xAA), FillBytes(0xBB));
            var snapshotDir = bundle.ResolveCheckpointSnapshotPath(100);
            Assert.True(Directory.Exists(snapshotDir));

            await bundle.DeleteCheckpointAsync(100);

            Assert.Null(bundle.Metadata.GetCheckpoint(100));
            Assert.False(Directory.Exists(snapshotDir));
        }

        [Fact]
        public async Task RestoreCheckpointAsync_NoSnapshot_Throws()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => bundle.RestoreCheckpointAsync(999));
        }

        [Fact]
        public async Task ResetStateOnlyAsync_ClearsMetadataAndKeepsCheckpointArchive()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);
            await bundle.SaveCheckpointAsync(100, FillBytes(0xAA), FillBytes(0xBB));
            bundle.Metadata.MarkGenesisLoaded();
            bundle.Metadata.Commit(50, FillBytes(0x99));

            await bundle.ResetStateOnlyAsync();

            Assert.False(bundle.Metadata.IsGenesisLoaded());
            Assert.Equal(0UL, bundle.Metadata.GetLastBlock());
            Assert.Null(bundle.Metadata.GetCheckpoint(100));
            Assert.True(Directory.Exists(bundle.ResolveCheckpointSnapshotPath(100)));
        }

        [Fact]
        public async Task ResetStateOnlyAsync_WipesStateColumnFamilies()
        {
            const string addr = "0x2222222222222222222222222222222222222222";

            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);
            await bundle.State.SaveAccountAsync(addr, new Account { Balance = 500, Nonce = 3 });
            var before = await bundle.State.GetAccountAsync(addr);
            Assert.NotNull(before);
            Assert.Equal(500, before.Balance);

            await bundle.ResetStateOnlyAsync();

            var after = await bundle.State.GetAccountAsync(addr);
            Assert.Null(after);
        }

        [Fact]
        public async Task SaveAndRestoreCheckpoint_RoundTrip_PreservesStateAtCheckpoint()
        {
            const string addr = "0x3333333333333333333333333333333333333333";

            using (var bundle = RocksDbChainStoreBundle.Open(_dataDir))
            {
                await bundle.State.SaveAccountAsync(addr, new Account { Balance = 100, Nonce = 1 });
                await bundle.SaveCheckpointAsync(50, FillBytes(0xAA), FillBytes(0xBB));

                await bundle.State.SaveAccountAsync(addr, new Account { Balance = 999, Nonce = 9 });
                var afterMutation = await bundle.State.GetAccountAsync(addr);
                Assert.Equal(999, afterMutation.Balance);
                Assert.Equal(9, afterMutation.Nonce);
            }

            var snapshotDir = Path.Combine(_dataDir, ".cp", "000000000050");
            Assert.True(Directory.Exists(snapshotDir));
            RocksDbChainStoreBundle.RestoreFromCheckpointDir(snapshotDir, _dataDir);

            using (var bundle = RocksDbChainStoreBundle.Open(_dataDir))
            {
                var restored = await bundle.State.GetAccountAsync(addr);
                Assert.NotNull(restored);
                Assert.Equal(100, restored.Balance);
                Assert.Equal(1, restored.Nonce);
            }
        }
    }
}
