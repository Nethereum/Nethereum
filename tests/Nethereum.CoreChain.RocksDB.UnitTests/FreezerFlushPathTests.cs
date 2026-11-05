using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Xunit;
using static Nethereum.CoreChain.RocksDB.UnitTests.FreezerBlockFactory;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Trait("Slice", "Freezer")]
    public class FreezerFlushPathTests : IDisposable
    {
        private const string PauseFileName = "freezer-indexing.paused";
        private const int UnindexedFrozenBlocks = 4;
        private const int FlushedBlock = 9;

        private readonly string _root = Path.Combine(Path.GetTempPath(), $"freezerflush_{Guid.NewGuid():N}");

        public void Dispose() => DeleteQuietly(_root);

        private string PauseFile => Path.Combine(DataDir(_root), PauseFileName);

        private async Task ArrangeAgedBlocksAsync(RocksDbChainStoreBundle bundle)
        {
            var blocks = MakeChainedBlocks(0, FlushedBlock + 1);
            bundle.AppendToFreezerWithoutIndexingForTests(blocks.GetRange(0, UnindexedFrozenBlocks));
            for (var i = UnindexedFrozenBlocks; i < blocks.Count; i++)
                await WriteToHotAsync(bundle, blocks[i]);
            StampTipHeight(bundle.Rocks, ImmutabilityThreshold + FlushedBlock - 2);
        }

        private static Task StartFlush(RocksDbChainStoreBundle bundle)
            => Task.Run(() => ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, FlushedBlock, Fill32(FlushedBlock)));

        private void PauseIndexing()
        {
            Directory.CreateDirectory(DataDir(_root));
            File.WriteAllText(PauseFile, "operator pause");
        }

        private void ResumeIndexing()
        {
            try { File.Delete(PauseFile); } catch { }
        }

        [Fact]
        public async Task Given_AFreezerBundleWhoseByHashIndexIsFarBehindAndIndexingPaused_When_ABoundaryFlushAgesABlockIntoTheFreezer_Then_TheFlushReturnsWithoutIndexing()
        {
            var bundle = OpenFreezerBundle(_root, FreezerOptions(_root, backgroundIndexing: false));
            Task flush = null;
            try
            {
                await ArrangeAgedBlocksAsync(bundle);
                PauseIndexing();

                flush = StartFlush(bundle);
                await flush.WaitAsync(TimeSpan.FromSeconds(10));

                Assert.Equal(0, bundle.ByHashIndexedHead);
            }
            finally
            {
                ResumeIndexing();
                if (flush != null)
                {
                    try { await flush.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
                }
                await bundle.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_AFreezerBundleWithTheBackgroundTrailerRunning_When_ABoundaryFlushAgesABlockIntoTheFreezer_Then_TheFlushReturnsAndTheTrailerIndexesTheFrozenBlocks()
        {
            var bundle = OpenFreezerBundle(_root, FreezerOptions(_root, backgroundIndexing: true));
            try
            {
                await ArrangeAgedBlocksAsync(bundle);
                bundle.StartBackgroundFreezeIndexing();

                await StartFlush(bundle).WaitAsync(TimeSpan.FromSeconds(10));

                await WaitUntilAsync(() => bundle.FreezerHead > UnindexedFrozenBlocks, TimeSpan.FromSeconds(30),
                    $"the background freezer job did not freeze the aged block: FreezerHead={bundle.FreezerHead}");
                await WaitUntilAsync(() => bundle.ByHashIndexedHead == bundle.FreezerHead, TimeSpan.FromSeconds(30),
                    "the trailer did not index the frozen blocks");
            }
            finally
            {
                await bundle.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_ABoundaryFlushInFlight_When_TheBundleIsDisposed_Then_TheFlushHasReturnedBeforeAnyStoreCloses()
        {
            var bundle = OpenFreezerBundle(_root, FreezerOptions(_root, backgroundIndexing: false));
            Task flush = null;
            try
            {
                await ArrangeAgedBlocksAsync(bundle);
                PauseIndexing();

                flush = StartFlush(bundle);

                await bundle.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));

                Assert.True(flush.IsCompletedSuccessfully,
                    $"DisposeAsync returned while the flush was still {flush.Status}");
            }
            finally
            {
                ResumeIndexing();
                if (flush != null)
                {
                    try { await flush.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
                }
            }
        }
    }
}
