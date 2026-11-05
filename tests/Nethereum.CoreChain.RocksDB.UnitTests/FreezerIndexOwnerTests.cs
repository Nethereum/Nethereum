using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using static Nethereum.CoreChain.RocksDB.UnitTests.FreezerBlockFactory;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Trait("Slice", "Freezer")]
    public class FreezerIndexOwnerTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"freezerindexowner_{Guid.NewGuid():N}");

        public void Dispose() => DeleteQuietly(_root);

        private static async Task FreezeByPromotionAndByBulkAppendAsync(RocksDbChainStoreBundle bundle)
        {
            var blocks = MakeChainedBlocks(0, 12);
            for (var i = 0; i < 6; i++)
                await WriteToHotAsync(bundle, blocks[i]);

            StampTipHeight(bundle.Rocks, ImmutabilityThreshold + 3);
            bundle.DriveFreezerPromotionForTests();
            Assert.Equal(4, bundle.FreezerHead);

            Assert.NotNull(await bundle.Blocks.GetByHashAsync(blocks[5].Hash));

            StampTipHeight(bundle.Rocks, ImmutabilityThreshold + 11);
            await bundle.PersistBlocksAsync(blocks.GetRange(4, 8)).WaitAsync(TimeSpan.FromSeconds(30));
            bundle.DriveFreezerPromotionForTests();
            Assert.Equal(12, bundle.FreezerHead);
        }

        [Fact]
        public async Task Given_BackgroundFreezeIndexingOff_When_BlocksAreFrozenByPromotionAndByBulkAppend_Then_NothingIsIndexedAndFreezingStillAdvances()
        {
            using var bundle = OpenFreezerBundle(_root, FreezerOptions(_root, backgroundIndexing: false));

            await FreezeByPromotionAndByBulkAppendAsync(bundle);

            Assert.Equal(0, bundle.ByHashIndexedHead);
            Assert.Equal(0, bundle.LogIndexRenderedHead);
            Assert.Null(await bundle.Blocks.GetByHashAsync(MakeBlock(0).Hash));
        }

        [Fact]
        public async Task Given_BackgroundFreezeIndexingOn_When_TheTrailerRuns_Then_TheSameBlocksAreIndexed()
        {
            using var bundle = OpenFreezerBundle(_root, FreezerOptions(_root, backgroundIndexing: true));
            bundle.StartBackgroundFreezeIndexing();

            await FreezeByPromotionAndByBulkAppendAsync(bundle);

            await WaitUntilAsync(() => bundle.ByHashIndexedHead == bundle.FreezerHead, TimeSpan.FromSeconds(30),
                "the trailer did not index the frozen blocks");
            Assert.NotNull(await bundle.Blocks.GetByHashAsync(MakeBlock(0).Hash));
        }

        [Fact]
        public async Task Given_ARunningTrailer_When_FinishBulkIndexingRuns_Then_TheTrailerKeepsIndexingBlocksFrozenAfterwards()
        {
            var options = FreezerOptions(_root, backgroundIndexing: true);
            options.FreezerCommitCadenceBlocks = 1;
            using var bundle = OpenFreezerBundle(_root, options);
            bundle.StartBackgroundFreezeIndexing();
            bundle.FinishBulkIndexing();

            bundle.AppendToFreezerWithoutIndexingForTests(MakeChainedBlocks(0, 6));

            Assert.Equal(6, bundle.FreezerHead);
            await WaitUntilAsync(() => bundle.ByHashIndexedHead == bundle.FreezerHead, TimeSpan.FromSeconds(15),
                $"the trailer stopped indexing: ByHashIndexedHead={bundle.ByHashIndexedHead} FreezerHead={bundle.FreezerHead}");
        }

        [Fact]
        public async Task Given_AnOpenFreezerAppendBatch_When_CheckpointBulkRuns_Then_TheBatchIsCommittedAndTheTrailerIndexesIt()
        {
            using var bundle = OpenFreezerBundle(_root, FreezerOptions(_root, backgroundIndexing: true));
            bundle.StartBackgroundFreezeIndexing();
            var blocks = MakeChainedBlocks(0, 5);
            StampTipHeight(bundle.Rocks, ImmutabilityThreshold + 10);

            await bundle.PersistBlocksAsync(blocks).WaitAsync(TimeSpan.FromSeconds(30));
            bundle.CheckpointBulk();

            await WaitUntilAsync(() => bundle.FreezerHead == blocks.Count && bundle.ByHashIndexedHead == bundle.FreezerHead,
                TimeSpan.FromSeconds(15),
                $"the batch was not committed and indexed: ByHashIndexedHead={bundle.ByHashIndexedHead} FreezerHead={bundle.FreezerHead}");
        }
    }
}
