using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using static Nethereum.CoreChain.RocksDB.UnitTests.FreezerBlockFactory;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class CheckpointStagingSweepTests : IDisposable
    {
        private const string StagingName = "000000000042.staging.deadbeef";

        private readonly string _root = Path.Combine(Path.GetTempPath(), $"cpsweep_{Guid.NewGuid():N}");

        public void Dispose() => DeleteQuietly(_root);

        private RocksDbChainStoreBundle OpenBundle(bool freezer)
            => freezer
                ? OpenFreezerBundle(_root, FreezerOptions(_root, backgroundIndexing: false))
                : RocksDbChainStoreBundle.Open(DataDir(_root), null, false, new RocksDbStorageOptions(), FreezerBlockFactory.Signer);

        private string[] CheckpointRoots(bool freezer)
            => freezer
                ? new[]
                {
                    Path.Combine(DataDir(_root), ".cp"),
                    Path.Combine(DataDir(_root), RocksDbChainStoreBundle.FreezerHistorySubDir, ".cp"),
                }
                : new[] { Path.Combine(DataDir(_root), ".cp") };

        private static async Task<ulong[]> ListedAsync(RocksDbChainStoreBundle bundle)
            => (await bundle.ListCheckpointsAsync()).Select(c => c.BlockNumber).OrderBy(n => n).ToArray();

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Given_ALeftoverStagingCheckpointDirectory_When_TheBundleIsReopened_Then_ItIsDeleted(bool freezer)
        {
            using (var first = OpenBundle(freezer)) { }

            var leftovers = CheckpointRoots(freezer).Select(r => Path.Combine(r, StagingName)).ToArray();
            foreach (var leftover in leftovers)
            {
                Directory.CreateDirectory(leftover);
                File.WriteAllText(Path.Combine(leftover, "partial.sst"), "partial");
            }

            using var reopened = OpenBundle(freezer);
            var listed = await ListedAsync(reopened).WaitAsync(TimeSpan.FromSeconds(30));

            Assert.All(leftovers, leftover => Assert.False(Directory.Exists(leftover), $"{leftover} should have been swept"));
            Assert.Empty(listed);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Given_ARealCheckpointDirectory_When_TheBundleIsReopened_Then_ItSurvivesAndIsListed(bool freezer)
        {
            using (var first = OpenBundle(freezer))
            {
                await first.SaveCheckpointAsync(42, Fill32(0x42), Fill32(0x43)).WaitAsync(TimeSpan.FromSeconds(30));
            }

            using var reopened = OpenBundle(freezer);
            var listed = await ListedAsync(reopened).WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(new ulong[] { 42 }, listed);
            foreach (var cpRoot in CheckpointRoots(freezer))
                Assert.True(Directory.Exists(Path.Combine(cpRoot, "000000000042")), $"{cpRoot} should keep the real checkpoint");
        }
    }
}
