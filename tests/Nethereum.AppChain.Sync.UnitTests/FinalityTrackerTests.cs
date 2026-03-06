using System.Numerics;
using Nethereum.AppChain.Sync;
using Xunit;

namespace Nethereum.AppChain.Sync.UnitTests
{
    public class FinalityTrackerTests
    {
        [Fact]
        public async Task MarkAsFinalizedAsync_UpdatesLastFinalizedBlock()
        {
            var tracker = new InMemoryFinalityTracker();

            await tracker.MarkAsFinalizedAsync(10);

            Assert.Equal(10, tracker.LastFinalizedBlock);
            Assert.True(await tracker.IsFinalizedAsync(10));
        }

        [Fact]
        public async Task MarkAsSoftAsync_UpdatesLastSoftBlock()
        {
            var tracker = new InMemoryFinalityTracker();

            await tracker.MarkAsSoftAsync(15);

            Assert.Equal(15, tracker.LastSoftBlock);
            Assert.True(await tracker.IsSoftAsync(15));
        }

        [Fact]
        public async Task IsFinalizedAsync_ReturnsTrueForBlocksBelowLastFinalized()
        {
            var tracker = new InMemoryFinalityTracker();

            await tracker.MarkAsFinalizedAsync(100);

            Assert.True(await tracker.IsFinalizedAsync(50));
            Assert.True(await tracker.IsFinalizedAsync(99));
            Assert.True(await tracker.IsFinalizedAsync(100));
            Assert.False(await tracker.IsFinalizedAsync(101));
        }

        [Fact]
        public async Task IsSoftAsync_ReturnsTrueForBlocksBetweenFinalizedAndSoft()
        {
            var tracker = new InMemoryFinalityTracker();

            await tracker.MarkAsFinalizedAsync(100);
            await tracker.MarkAsSoftAsync(150);

            Assert.False(await tracker.IsSoftAsync(100));
            Assert.True(await tracker.IsSoftAsync(101));
            Assert.True(await tracker.IsSoftAsync(125));
            Assert.True(await tracker.IsSoftAsync(150));
            Assert.False(await tracker.IsSoftAsync(151));
        }

        [Fact]
        public async Task MarkRangeAsFinalizedAsync_FinalizesAllBlocksInRange()
        {
            var tracker = new InMemoryFinalityTracker();

            await tracker.MarkRangeAsFinalizedAsync(0, 99);

            Assert.Equal(99, tracker.LastFinalizedBlock);
            Assert.True(await tracker.IsFinalizedAsync(0));
            Assert.True(await tracker.IsFinalizedAsync(50));
            Assert.True(await tracker.IsFinalizedAsync(99));
            Assert.False(await tracker.IsFinalizedAsync(100));
        }

        [Fact]
        public async Task MarkAsFinalizedAsync_DoesNotDowngrade_ExistingFinalization()
        {
            var tracker = new InMemoryFinalityTracker();

            await tracker.MarkAsFinalizedAsync(100);
            await tracker.MarkAsSoftAsync(100);

            Assert.True(await tracker.IsFinalizedAsync(100));
            Assert.False(await tracker.IsSoftAsync(100));
        }

        [Fact]
        public async Task MultipleBatches_CorrectlyTracksFinality()
        {
            var tracker = new InMemoryFinalityTracker();

            await tracker.MarkRangeAsFinalizedAsync(0, 99);
            await tracker.MarkAsSoftAsync(100);
            await tracker.MarkAsSoftAsync(101);
            await tracker.MarkAsSoftAsync(102);

            Assert.Equal(99, tracker.LastFinalizedBlock);
            Assert.Equal(102, tracker.LastSoftBlock);

            Assert.True(await tracker.IsFinalizedAsync(99));
            Assert.True(await tracker.IsSoftAsync(100));
            Assert.True(await tracker.IsSoftAsync(102));

            await tracker.MarkRangeAsFinalizedAsync(100, 199);

            Assert.Equal(199, tracker.LastFinalizedBlock);
            Assert.True(await tracker.IsFinalizedAsync(100));
            Assert.True(await tracker.IsFinalizedAsync(102));
        }

        [Fact]
        public async Task GetLatestFinalizedBlockAsync_ReturnsCorrectValue()
        {
            var tracker = new InMemoryFinalityTracker();

            Assert.Equal(-1, await tracker.GetLatestFinalizedBlockAsync());

            await tracker.MarkAsFinalizedAsync(50);
            Assert.Equal(50, await tracker.GetLatestFinalizedBlockAsync());

            await tracker.MarkAsFinalizedAsync(100);
            Assert.Equal(100, await tracker.GetLatestFinalizedBlockAsync());
        }

        [Fact]
        public async Task GetLatestSoftBlockAsync_ReturnsCorrectValue()
        {
            var tracker = new InMemoryFinalityTracker();

            Assert.Equal(-1, await tracker.GetLatestSoftBlockAsync());

            await tracker.MarkAsSoftAsync(50);
            Assert.Equal(50, await tracker.GetLatestSoftBlockAsync());

            await tracker.MarkAsSoftAsync(100);
            Assert.Equal(100, await tracker.GetLatestSoftBlockAsync());
        }
    }
}
