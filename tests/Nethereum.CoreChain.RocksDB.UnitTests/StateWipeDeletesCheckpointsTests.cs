using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Trait("Slice", "Flat")]
    public class StateWipeDeletesCheckpointsTests : IDisposable
    {
        private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"wipe_cp_{Guid.NewGuid():N}");

        public void Dispose()
        {
            try { if (Directory.Exists(_dataDir)) Directory.Delete(_dataDir, recursive: true); } catch { }
        }

        private static byte[] Fill(byte value) => Enumerable.Repeat(value, 32).ToArray();

        private static async Task<string[]> SaveTwoCheckpoints(RocksDbChainStoreBundle bundle)
        {
            await bundle.SaveCheckpointAsync(100, Fill(0xAA), Fill(0xBB));
            await bundle.SaveCheckpointAsync(200, Fill(0xCC), Fill(0xDD));
            var dirs = new[] { bundle.ResolveCheckpointSnapshotPath(100), bundle.ResolveCheckpointSnapshotPath(200) };
            Assert.All(dirs, d => Assert.True(Directory.Exists(d)));
            Assert.Equal(2, (await bundle.ListCheckpointsAsync()).Count);
            return dirs;
        }

        [Fact]
        public async Task Given_ABundleWithTwoSavedCheckpoints_When_TheSnapBootstrapResetRuns_Then_NoCheckpointIsListedAndTheDirectoriesAreGone()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);
            var dirs = await SaveTwoCheckpoints(bundle);

            await bundle.ResetSnapBootstrapStateAsync();

            Assert.Empty(await bundle.ListCheckpointsAsync());
            Assert.All(dirs, d => Assert.False(Directory.Exists(d)));
        }

        [Fact]
        public async Task Given_ABundleWithTwoSavedCheckpoints_When_TheSnapBootstrapResetRuns_Then_ItReportsHowManyCheckpointsItDeleted()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);
            await SaveTwoCheckpoints(bundle);
            var progress = new System.Collections.Generic.List<string>();

            await bundle.ResetSnapBootstrapStateAsync(progress.Add, default);

            Assert.Contains("checkpoints deleted count=2", progress);
        }

        [Fact]
        public async Task Given_ABundleWithTwoSavedCheckpoints_When_ResetStateOnlyRuns_Then_NoCheckpointIsListedAndTheDirectoriesAreGone()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);
            var dirs = await SaveTwoCheckpoints(bundle);

            await bundle.ResetStateOnlyAsync();

            Assert.Empty(await bundle.ListCheckpointsAsync());
            Assert.All(dirs, d => Assert.False(Directory.Exists(d)));
        }
    }
}
