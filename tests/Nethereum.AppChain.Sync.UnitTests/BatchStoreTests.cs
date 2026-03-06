using System.Numerics;
using Nethereum.AppChain.Sync;
using Xunit;

namespace Nethereum.AppChain.Sync.UnitTests
{
    public class BatchStoreTests
    {
        [Fact]
        public async Task SaveAndGetBatch_ByRange_ReturnsBatch()
        {
            // Arrange
            var store = new InMemoryBatchStore();
            var batch = CreateBatch(0, 99);

            // Act
            await store.SaveBatchAsync(batch);
            var retrieved = await store.GetBatchAsync(0, 99);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(batch.FromBlock, retrieved.FromBlock);
            Assert.Equal(batch.ToBlock, retrieved.ToBlock);
            Assert.Equal(batch.BatchHash, retrieved.BatchHash);
        }

        [Fact]
        public async Task SaveAndGetBatch_ByHash_ReturnsBatch()
        {
            // Arrange
            var store = new InMemoryBatchStore();
            var batch = CreateBatch(0, 99);

            // Act
            await store.SaveBatchAsync(batch);
            var retrieved = await store.GetBatchByHashAsync(batch.BatchHash);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(batch.FromBlock, retrieved.FromBlock);
            Assert.Equal(batch.ToBlock, retrieved.ToBlock);
        }

        [Fact]
        public async Task GetBatchContainingBlock_ReturnsCorrectBatch()
        {
            // Arrange
            var store = new InMemoryBatchStore();
            await store.SaveBatchAsync(CreateBatch(0, 99));
            await store.SaveBatchAsync(CreateBatch(100, 199));
            await store.SaveBatchAsync(CreateBatch(200, 299));

            // Act
            var batch1 = await store.GetBatchContainingBlockAsync(50);
            var batch2 = await store.GetBatchContainingBlockAsync(150);
            var batch3 = await store.GetBatchContainingBlockAsync(250);
            var batchNull = await store.GetBatchContainingBlockAsync(500);

            // Assert
            Assert.NotNull(batch1);
            Assert.Equal(0, batch1.FromBlock);
            Assert.Equal(99, batch1.ToBlock);

            Assert.NotNull(batch2);
            Assert.Equal(100, batch2.FromBlock);

            Assert.NotNull(batch3);
            Assert.Equal(200, batch3.FromBlock);

            Assert.Null(batchNull);
        }

        [Fact]
        public async Task GetLatestBatch_ReturnsHighestToBlock()
        {
            // Arrange
            var store = new InMemoryBatchStore();
            await store.SaveBatchAsync(CreateBatch(0, 99));
            await store.SaveBatchAsync(CreateBatch(100, 199));
            await store.SaveBatchAsync(CreateBatch(200, 299));

            // Act
            var latest = await store.GetLatestBatchAsync();

            // Assert
            Assert.NotNull(latest);
            Assert.Equal(200, latest.FromBlock);
            Assert.Equal(299, latest.ToBlock);
        }

        [Fact]
        public async Task GetBatchesAfter_ReturnsOrderedBatches()
        {
            // Arrange
            var store = new InMemoryBatchStore();
            await store.SaveBatchAsync(CreateBatch(0, 99));
            await store.SaveBatchAsync(CreateBatch(100, 199));
            await store.SaveBatchAsync(CreateBatch(200, 299));
            await store.SaveBatchAsync(CreateBatch(300, 399));

            // Act
            var batches = await store.GetBatchesAfterAsync(100, limit: 2);

            // Assert
            Assert.Equal(2, batches.Count);
            Assert.Equal(100, batches[0].FromBlock);
            Assert.Equal(200, batches[1].FromBlock);
        }

        [Fact]
        public async Task GetPendingBatches_ReturnsOnlyPending()
        {
            // Arrange
            var store = new InMemoryBatchStore();

            var batch1 = CreateBatch(0, 99);
            batch1.Status = BatchStatus.Pending;
            await store.SaveBatchAsync(batch1);

            var batch2 = CreateBatch(100, 199);
            batch2.Status = BatchStatus.Imported;
            await store.SaveBatchAsync(batch2);

            var batch3 = CreateBatch(200, 299);
            batch3.Status = BatchStatus.Pending;
            await store.SaveBatchAsync(batch3);

            // Act
            var pending = await store.GetPendingBatchesAsync();

            // Assert
            Assert.Equal(2, pending.Count);
            Assert.All(pending, b => Assert.Equal(BatchStatus.Pending, b.Status));
        }

        [Fact]
        public async Task UpdateBatchStatus_UpdatesCorrectly()
        {
            // Arrange
            var store = new InMemoryBatchStore();
            var batch = CreateBatch(0, 99);
            batch.Status = BatchStatus.Pending;
            await store.SaveBatchAsync(batch);

            // Act
            await store.UpdateBatchStatusAsync(0, 99, BatchStatus.Imported);
            var updated = await store.GetBatchAsync(0, 99);

            // Assert
            Assert.NotNull(updated);
            Assert.Equal(BatchStatus.Imported, updated.Status);
        }

        [Fact]
        public async Task GetLatestImportedBlock_TracksImportedBatches()
        {
            // Arrange
            var store = new InMemoryBatchStore();

            var batch1 = CreateBatch(0, 99);
            batch1.Status = BatchStatus.Imported;
            await store.SaveBatchAsync(batch1);

            // Act
            var latestAfterFirst = await store.GetLatestImportedBlockAsync();

            var batch2 = CreateBatch(100, 199);
            batch2.Status = BatchStatus.Imported;
            await store.SaveBatchAsync(batch2);

            var latestAfterSecond = await store.GetLatestImportedBlockAsync();

            // Assert
            Assert.Equal(99, latestAfterFirst);
            Assert.Equal(199, latestAfterSecond);
        }

        [Fact]
        public async Task IsBatchImported_ReturnsCorrectStatus()
        {
            // Arrange
            var store = new InMemoryBatchStore();

            var batch1 = CreateBatch(0, 99);
            batch1.Status = BatchStatus.Imported;
            await store.SaveBatchAsync(batch1);

            var batch2 = CreateBatch(100, 199);
            batch2.Status = BatchStatus.Pending;
            await store.SaveBatchAsync(batch2);

            // Act
            var isImported1 = await store.IsBatchImportedAsync(batch1.BatchHash);
            var isImported2 = await store.IsBatchImportedAsync(batch2.BatchHash);
            var isImported3 = await store.IsBatchImportedAsync(new byte[32]);

            // Assert
            Assert.True(isImported1);
            Assert.False(isImported2);
            Assert.False(isImported3);
        }

        private BatchInfo CreateBatch(long fromBlock, long toBlock)
        {
            var hash = new byte[32];
            new Random().NextBytes(hash);

            return new BatchInfo
            {
                ChainId = 420420,
                FromBlock = fromBlock,
                ToBlock = toBlock,
                BatchHash = hash,
                ToBlockStateRoot = new byte[32],
                Status = BatchStatus.Created,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }
    }
}
