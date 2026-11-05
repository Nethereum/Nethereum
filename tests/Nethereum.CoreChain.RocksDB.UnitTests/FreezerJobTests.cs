using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Util;
using Xunit;
using static Nethereum.CoreChain.RocksDB.UnitTests.FreezerBlockFactory;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Collection(ConsoleErrorCaptureCollection.Name)]
    [Trait("Slice", "Freezer")]
    public class FreezerJobTests : IDisposable
    {
        private const long TipHeight = ImmutabilityThreshold + 10;

        private readonly string _root = Path.Combine(Path.GetTempPath(), $"freezerjob_{Guid.NewGuid():N}");

        public void Dispose() => DeleteQuietly(_root);

        private RocksDbChainStoreBundle Open() => OpenFreezerBundle(_root, FreezerOptions(_root, backgroundIndexing: true));

        [Fact]
        public async Task Given_AFreezerBundle_When_TheFollowerRunStartsTheBackgroundJobs_Then_AgedBlocksAreFrozenAndEvicted()
        {
            using var bundle = Open();
            foreach (var block in MakeChainedBlocks(0, 6))
                await WriteToHotAsync(bundle, block);
            StampTipHeight(bundle.Rocks, ImmutabilityThreshold + 3);

            bundle.StartBackgroundFreezeIndexing();

            await WaitUntilAsync(() => bundle.FreezerHead == 4, TimeSpan.FromSeconds(15),
                $"the aged blocks were not frozen: FreezerHead={bundle.FreezerHead}");
            await WaitUntilAsync(() => !bundle.HotWindow.ContainsBlock(3), TimeSpan.FromSeconds(15),
                "block 3 was frozen but not evicted from the hot window");
            Assert.True(bundle.HotWindow.ContainsBlock(4));
        }

        private async Task ArrangeHotWindowStartingAtBlockFiveAsync(RocksDbChainStoreBundle bundle)
        {
            foreach (var block in MakeChainedBlocks(5, 5))
                await WriteToHotAsync(bundle, block);
            StampTipHeight(bundle.Rocks, ImmutabilityThreshold + 7);
        }

        [Fact]
        public async Task Given_APromotionThatThrows_When_ABoundaryFlushRuns_Then_TheFlushSucceeds()
        {
            using var bundle = Open();
            await ArrangeHotWindowStartingAtBlockFiveAsync(bundle);
            var flushed = Fill32(9);

            await Task.Run(() => ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, 9, flushed))
                .WaitAsync(TimeSpan.FromSeconds(30));
            await ((IAtomicBlockFlush)bundle).DrainAsync().WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(9UL, bundle.Metadata.GetLastBlock());
            Assert.Equal(flushed, bundle.Metadata.GetLastBlockHash());
        }

        [Fact]
        public async Task Given_AHotWindowStartingAboveTheFreezerHead_When_TheJobRuns_Then_NothingIsFrozenAndNoRoundFails()
        {
            using var capture = new ErrorCapture();
            using var bundle = Open();
            await ArrangeHotWindowStartingAtBlockFiveAsync(bundle);

            bundle.StartBackgroundFreezeIndexing();
            await Task.Delay(TimeSpan.FromSeconds(3));

            Assert.Equal(0, bundle.FreezerHead);
            Assert.True(bundle.HotWindow.ContainsBlock(5));
            Assert.DoesNotContain("round failed", capture.Text);
        }

        [Fact]
        public async Task Given_BlocksBelowAndAboveTheImmutabilityThreshold_When_TheJobRuns_Then_OnlyThoseAtOrBelowTipMinus90000AreFrozen()
        {
            const long tip = ImmutabilityThreshold + 10;
            using var bundle = Open();
            var blocks = MakeChainedBlocks(0, 13);
            bundle.AppendToFreezerWithoutIndexingForTests(blocks.GetRange(0, 8));
            for (var i = 8; i < blocks.Count; i++)
                await WriteToHotAsync(bundle, blocks[i]);
            StampTipHeight(bundle.Rocks, tip);

            bundle.StartBackgroundFreezeIndexing();

            await WaitUntilAsync(() => bundle.FreezerHead >= 11, TimeSpan.FromSeconds(15),
                $"the aged blocks were not frozen: FreezerHead={bundle.FreezerHead}");
            Assert.Equal(tip - ImmutabilityThreshold + 1, bundle.FreezerHead);
        }

        [Fact]
        public async Task Given_AllHotBlocksAboveTheImmutabilityThreshold_When_TheJobRuns_Then_NothingIsFrozen()
        {
            using var bundle = Open();
            foreach (var block in MakeChainedBlocks(11, 5))
                await WriteToHotAsync(bundle, block);
            StampTipHeight(bundle.Rocks, TipHeight);

            bundle.StartBackgroundFreezeIndexing();
            await Task.Delay(TimeSpan.FromSeconds(3));

            Assert.Equal(0, bundle.FreezerHead);
        }

        private const long FarTip = 1_000;
        private const ulong FarBehindHead = 100;
        private const ulong CaughtUpHead = 999;

        private static void ArmBehindTheTip(RocksDbChainStoreBundle bundle)
        {
            bundle.SetYieldToExecution(true);
            bundle.Metadata.SaveHeaderSyncState(HeaderSubchains.OpenTip(HeaderSyncState.Empty, FarTip));
            bundle.Metadata.Commit(FarBehindHead, Fill32((long)FarBehindHead));
        }

        private static void CatchUp(RocksDbChainStoreBundle bundle)
            => bundle.Metadata.Commit(CaughtUpHead, Fill32((long)CaughtUpHead));

        private static async Task WriteAgedBlocksAsync(RocksDbChainStoreBundle bundle)
        {
            foreach (var block in MakeChainedBlocks(0, 6))
                await WriteToHotAsync(bundle, block);
            StampTipHeight(bundle.Rocks, ImmutabilityThreshold + 3);
        }

        private static RocksDbChainStoreBundle OpenPromotionBundle(string root, ulong maxHistoryBlocks)
        {
            var options = new RocksDbStorageOptions { PromotionEnabled = true, BackgroundFreezeIndexing = true };
            var journal = new HistoricalStateOptions { MaxHistoryBlocks = (BigInteger)maxHistoryBlocks };
            return RocksDbChainStoreBundle.Open(DataDir(root), journal, false, options, FreezerBlockFactory.Signer);
        }

        private static async Task WriteExecutedBlocksAsync(RocksDbChainStoreBundle bundle, int count)
        {
            for (var i = 1; i <= count; i++)
                await WriteToHotAsync(bundle, MakeBlock(i));
            ((RocksDbChainMetadataStore)bundle.Metadata).CommitDurableState((ulong)count, Fill32(count));
            bundle.Metadata.Commit((ulong)count, Fill32(count));
        }

        [Fact]
        public async Task Given_AnArmedBundleFarBehindTheTip_When_BlocksAgePastTheThreshold_Then_TheFreezerJobWaitsUntilCaughtUp()
        {
            using var bundle = Open();
            await WriteAgedBlocksAsync(bundle);
            ArmBehindTheTip(bundle);

            bundle.StartBackgroundFreezeIndexing();
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.Equal(0, bundle.FreezerHead);

            CatchUp(bundle);
            await WaitUntilAsync(() => bundle.FreezerHead == 4, TimeSpan.FromSeconds(15),
                $"the job did not freeze after the follower caught up: FreezerHead={bundle.FreezerHead}");
        }

        [Fact]
        public async Task Given_AnArmedBundleYielding_When_BlocksAgePastTheThreshold_Then_TheyStayReadableByNumberAndHashAndFreezeAfterTheResume()
        {
            using var bundle = Open();
            await WriteAgedBlocksAsync(bundle);
            ArmBehindTheTip(bundle);

            bundle.StartBackgroundFreezeIndexing();
            await Task.Delay(TimeSpan.FromSeconds(3));

            Assert.Equal(0, bundle.FreezerHead);
            await AssertAgedBlocksReadableAsync(bundle);

            CatchUp(bundle);
            await WaitUntilAsync(() => bundle.FreezerHead == 4, TimeSpan.FromSeconds(15),
                $"the job did not freeze after the follower caught up: FreezerHead={bundle.FreezerHead}");
            await AssertAgedBlocksReadableAsync(bundle);
        }

        private static async Task AssertAgedBlocksReadableAsync(RocksDbChainStoreBundle bundle)
        {
            for (var i = 0; i < 4; i++)
            {
                var byNumber = await bundle.Blocks.GetByNumberAsync(i);
                Assert.NotNull(byNumber);
                Assert.Equal(i, (int)byNumber.BlockNumber.ToBigInteger());
                var byHash = await bundle.Blocks.GetByHashAsync(Fill32(i));
                Assert.NotNull(byHash);
                Assert.Equal(i, (int)byHash.BlockNumber.ToBigInteger());
            }
        }

        [Fact]
        public async Task Given_PromotionModeArmedAndBehindTheTip_When_BlocksAgePastMaxHistoryBlocks_Then_PromotionWaitsUntilCaughtUp()
        {
            const int executed = 30;
            const ulong window = 5;
            using var bundle = OpenPromotionBundle(_root, window);
            await bundle.EnsureConsistentHeadAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(30));
            var metadata = (RocksDbChainMetadataStore)bundle.Metadata;
            await WriteExecutedBlocksAsync(bundle, executed);
            ArmBehindTheTip(bundle);
            var cursorBefore = metadata.GetPromotionCursor();

            bundle.StartBackgroundFreezeIndexing();
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.Equal(cursorBefore, metadata.GetPromotionCursor());

            CatchUp(bundle);
            var floor = (ulong)executed - window;
            await WaitUntilAsync(() => metadata.GetPromotionCursor() == floor, TimeSpan.FromSeconds(15),
                $"promotion did not run after the follower caught up: cursor={metadata.GetPromotionCursor()} floor={floor}");
        }

        [Fact]
        public async Task Given_PromotionModeOnAChainYoungerThanMaxHistoryBlocks_When_TheJobRuns_Then_NothingIsPromotedAndNoRoundFails()
        {
            const int executed = 20;
            using var capture = new ErrorCapture();
            using var bundle = OpenPromotionBundle(_root, 128);
            await bundle.EnsureConsistentHeadAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(30));
            var metadata = (RocksDbChainMetadataStore)bundle.Metadata;
            await WriteExecutedBlocksAsync(bundle, executed);
            var cursorBefore = metadata.GetPromotionCursor();

            bundle.StartBackgroundFreezeIndexing();
            await Task.Delay(TimeSpan.FromSeconds(3));

            Assert.Equal(cursorBefore, metadata.GetPromotionCursor());
            for (var i = 1; i <= executed; i++)
                Assert.True(bundle.HotWindow.ContainsBlock((ulong)i), $"block {i} left the hot window");
            Assert.DoesNotContain("round failed", capture.Text);
        }

        [Fact]
        public async Task Given_PromotionModeOnAChainOlderThanMaxHistoryBlocks_When_TheJobRuns_Then_BlocksUpToHeadMinusMaxHistoryBlocksArePromoted()
        {
            const int executed = 200;
            const ulong window = 128;
            using var capture = new ErrorCapture();
            using var bundle = OpenPromotionBundle(_root, window);
            await bundle.EnsureConsistentHeadAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(30));
            var metadata = (RocksDbChainMetadataStore)bundle.Metadata;
            await WriteExecutedBlocksAsync(bundle, executed);

            bundle.StartBackgroundFreezeIndexing();

            var floor = (ulong)executed - window;
            await WaitUntilAsync(() => metadata.GetPromotionCursor() == floor, TimeSpan.FromSeconds(15),
                $"promotion did not reach the floor: cursor={metadata.GetPromotionCursor()} floor={floor}");
            Assert.DoesNotContain("round failed", capture.Text);
        }

        [Fact]
        [Trait("Category", "Slow")]
        public async Task Given_TheFirstRoundDone_When_MoreBlocksAgeAfterIt_Then_TheNextRoundFreezesThemWithinOneRecheckInterval()
        {
            using var bundle = Open();
            foreach (var block in MakeChainedBlocks(0, 8))
                await WriteToHotAsync(bundle, block);
            StampTipHeight(bundle.Rocks, ImmutabilityThreshold + 3);

            bundle.StartBackgroundFreezeIndexing();
            await WaitUntilAsync(() => bundle.FreezerHead == 4, TimeSpan.FromSeconds(15),
                $"the first round did not freeze: FreezerHead={bundle.FreezerHead}");

            StampTipHeight(bundle.Rocks, ImmutabilityThreshold + 5);

            await WaitUntilAsync(() => bundle.FreezerHead == 6, TimeSpan.FromSeconds(75),
                $"the next round did not freeze the newly aged blocks: FreezerHead={bundle.FreezerHead}");
        }
    }
}
