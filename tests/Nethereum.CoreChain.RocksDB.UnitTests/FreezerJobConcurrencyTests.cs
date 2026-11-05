using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Util;
using Xunit;
using static Nethereum.CoreChain.RocksDB.UnitTests.FreezerBlockFactory;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [CollectionDefinition(ConsoleErrorCaptureCollection.Name, DisableParallelization = true)]
    public class ConsoleErrorCaptureCollection
    {
        public const string Name = "ConsoleErrorCapture";
    }

    [Collection(ConsoleErrorCaptureCollection.Name)]
    [Trait("Slice", "Freezer")]
    public class FreezerJobConcurrencyTests : IDisposable
    {
        private const int BlockCount = 30;
        private const ulong PromotionWindow = 5;
        private const string RoundFailed = "round failed";

        private readonly string _root = Path.Combine(Path.GetTempPath(), $"freezerjobconc_{Guid.NewGuid():N}");

        public void Dispose() => DeleteQuietly(_root);

        private async Task AssertEveryBlockResolvesByNumberAsync(RocksDbChainStoreBundle bundle, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var header = await bundle.Blocks.GetByNumberAsync(i);
                Assert.NotNull(header);
                Assert.Equal(i, (int)header.BlockNumber.ToBigInteger());
            }
        }

        [Fact]
        public async Task Given_TheJobRunningWithConcurrentBulkAppendsAndBootRecovery_When_ItSettles_Then_ItemsAreContiguousAndTheCursorNeverRegresses()
        {
            using var capture = new ErrorCapture();
            await FreezerModeSettlesContiguouslyAsync(capture);
            await PromotionModeSettlesWithoutRegressionAsync(capture);
        }

        private async Task FreezerModeSettlesContiguouslyAsync(ErrorCapture capture)
        {
            var root = Path.Combine(_root, "freezer-mode");
            using var bundle = OpenFreezerBundle(root, FreezerOptions(root, backgroundIndexing: true));
            var blocks = MakeChainedBlocks(0, BlockCount);
            foreach (var block in blocks)
                await WriteToHotAsync(bundle, block);
            StampTipHeight(bundle.Rocks, ImmutabilityThreshold + BlockCount - 1);

            bundle.StartBackgroundFreezeIndexing();
            var bulk = Task.Run(() => bundle.PersistBlocksAsync(blocks.GetRange(0, BlockCount / 2)));

            await bulk.WaitAsync(TimeSpan.FromSeconds(60));
            await WaitUntilAsync(() => bundle.FreezerHead == BlockCount, TimeSpan.FromSeconds(30),
                $"the freezer head was not reached: FreezerHead={bundle.FreezerHead}");
            await AssertEveryBlockResolvesByNumberAsync(bundle, BlockCount);
            Assert.DoesNotContain(RoundFailed, capture.Text);
        }

        private async Task PromotionModeSettlesWithoutRegressionAsync(ErrorCapture capture)
        {
            var root = Path.Combine(_root, "promotion-mode");
            var options = new RocksDbStorageOptions { PromotionEnabled = true, BackgroundFreezeIndexing = true };
            var journal = new HistoricalStateOptions { MaxHistoryBlocks = (BigInteger)PromotionWindow };
            using var bundle = RocksDbChainStoreBundle.Open(DataDir(root), journal, false, options, FreezerBlockFactory.Signer);
            await bundle.EnsureConsistentHeadAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(30));
            var metadata = (RocksDbChainMetadataStore)bundle.Metadata;

            for (var i = 1; i <= BlockCount; i++)
                await WriteToHotAsync(bundle, MakeBlock(i));
            metadata.CommitDurableState(BlockCount, Fill32(BlockCount));

            using var stop = new CancellationTokenSource();
            var regressions = new List<string>();
            var sampler = Task.Run(async () =>
            {
                var previous = 0UL;
                while (!stop.IsCancellationRequested)
                {
                    var current = metadata.GetPromotionCursor();
                    if (current < previous) regressions.Add($"{previous}->{current}");
                    previous = current;
                    await Task.Delay(10);
                }
            });
            var recovery = Task.Run(async () =>
            {
                while (!stop.IsCancellationRequested)
                {
                    await bundle.EnsureConsistentHeadAsync(_ => { });
                    await Task.Delay(5);
                }
            });

            bundle.StartBackgroundFreezeIndexing();
            var floor = (ulong)BlockCount - PromotionWindow;
            try
            {
                await WaitUntilAsync(() => metadata.GetPromotionCursor() == floor, TimeSpan.FromSeconds(30),
                    $"the promotion floor was not reached: cursor={metadata.GetPromotionCursor()} floor={floor}");
            }
            finally
            {
                stop.Cancel();
                await Task.WhenAll(sampler, recovery).WaitAsync(TimeSpan.FromSeconds(30));
            }

            Assert.Empty(regressions);
            Assert.DoesNotContain(RoundFailed, capture.Text);
            for (var i = 1; i <= (int)floor; i++)
                Assert.NotNull(await bundle.Blocks.GetByNumberAsync(i));
        }

        [Fact]
        public async Task Given_PromotionModeAndARoundInProgress_When_RecoveryPromotesBetweenTwoOfTheJobsBlocks_Then_TheJobContinuesFromTheNewCursorWithoutFailing()
        {
            const int owed = 500;
            using var capture = new ErrorCapture();
            var root = Path.Combine(_root, "owed-backlog");
            var options = new RocksDbStorageOptions { PromotionEnabled = true, BackgroundFreezeIndexing = true };
            var journal = new HistoricalStateOptions { MaxHistoryBlocks = (BigInteger)PromotionWindow };
            using var bundle = RocksDbChainStoreBundle.Open(DataDir(root), journal, false, options, FreezerBlockFactory.Signer);
            await bundle.EnsureConsistentHeadAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(30));
            var metadata = (RocksDbChainMetadataStore)bundle.Metadata;
            var head = owed + (long)PromotionWindow;
            for (var i = 1; i <= head; i++)
                await WriteToHotAsync(bundle, MakeBareBlock(i));
            metadata.CommitDurableState((ulong)head, Fill32(head));
            var floor = (ulong)head - PromotionWindow;

            bundle.StartBackgroundFreezeIndexing();
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
            while (metadata.GetPromotionCursor() == 0 && DateTime.UtcNow < deadline)
                Thread.SpinWait(100);
            Assert.True(metadata.GetPromotionCursor() > 0, "the job never started promoting");
            using var startsBeforeTheRoundEnds = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await bundle.EnsureConsistentHeadAsync(_ => { }, startsBeforeTheRoundEnds.Token).WaitAsync(TimeSpan.FromSeconds(120));

            await WaitUntilAsync(() => metadata.GetPromotionCursor() == floor, TimeSpan.FromSeconds(120),
                $"the promotion floor was not reached: cursor={metadata.GetPromotionCursor()} floor={floor}");

            Assert.DoesNotContain("freezer-promotion round failed", capture.Text);
            for (var i = 1; i <= (int)floor; i++)
                Assert.NotNull(await bundle.Blocks.GetByNumberAsync(i));
        }

        [Fact]
        public async Task Given_TheSameBlocksFrozenSequentially_When_NoWriterRunsConcurrently_Then_ItemsAreContiguous()
        {
            using var bundle = OpenFreezerBundle(_root, FreezerOptions(_root, backgroundIndexing: false));
            foreach (var block in MakeChainedBlocks(0, BlockCount))
                await WriteToHotAsync(bundle, block);
            StampTipHeight(bundle.Rocks, ImmutabilityThreshold + BlockCount - 1);

            bundle.DriveFreezerPromotionForTests();

            Assert.Equal(BlockCount, bundle.FreezerHead);
            await AssertEveryBlockResolvesByNumberAsync(bundle, BlockCount);
        }
    }
}
