using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class InMemoryBlockStoreTests
    {
        [Fact]
        public async Task SaveAndRetrieveByHash_ShouldWork()
        {
            var store = new InMemoryBlockStore();
            var header = CreateTestHeader(1);
            var blockHash = new byte[] { 0x01, 0x02, 0x03 };

            await store.SaveAsync(header, blockHash);
            var retrieved = await store.GetByHashAsync(blockHash);

            Assert.NotNull(retrieved);
            Assert.Equal(header.BlockNumber, retrieved.BlockNumber);
        }

        [Fact]
        public async Task SaveAndRetrieveByNumber_ShouldWork()
        {
            var store = new InMemoryBlockStore();
            var header = CreateTestHeader(5);
            var blockHash = new byte[] { 0x01, 0x02, 0x03 };

            await store.SaveAsync(header, blockHash);
            var retrieved = await store.GetByNumberAsync(5);

            Assert.NotNull(retrieved);
            Assert.Equal(5, retrieved.BlockNumber);
        }

        [Fact]
        public async Task GetLatest_ShouldReturnHighestBlockNumber()
        {
            var store = new InMemoryBlockStore();

            await store.SaveAsync(CreateTestHeader(1), new byte[] { 0x01 });
            await store.SaveAsync(CreateTestHeader(3), new byte[] { 0x03 });
            await store.SaveAsync(CreateTestHeader(2), new byte[] { 0x02 });

            var latest = await store.GetLatestAsync();

            Assert.NotNull(latest);
            Assert.Equal(3, latest.BlockNumber);
        }

        [Fact]
        public async Task GetHeight_ShouldReturnLatestBlockNumber()
        {
            var store = new InMemoryBlockStore();

            await store.SaveAsync(CreateTestHeader(10), new byte[] { 0x0A });
            var height = await store.GetHeightAsync();

            Assert.Equal(10, height);
        }

        [Fact]
        public async Task Exists_ShouldReturnTrueForExistingBlock()
        {
            var store = new InMemoryBlockStore();
            var blockHash = new byte[] { 0x01, 0x02, 0x03 };

            await store.SaveAsync(CreateTestHeader(1), blockHash);

            Assert.True(await store.ExistsAsync(blockHash));
            Assert.False(await store.ExistsAsync(new byte[] { 0xFF }));
        }

        [Fact]
        public async Task GetHashByNumber_ShouldReturnCorrectHash()
        {
            var store = new InMemoryBlockStore();
            var blockHash = new byte[] { 0xAB, 0xCD, 0xEF };

            await store.SaveAsync(CreateTestHeader(7), blockHash);
            var hash = await store.GetHashByNumberAsync(7);

            Assert.Equal(blockHash, hash);
        }

        [Fact]
        public async Task GetHeightAsync_EmptyStore_ReturnsNegativeOne()
        {
            var store = new InMemoryBlockStore();
            Assert.Equal(-1, await store.GetHeightAsync());
        }

        [Fact]
        public async Task GetLatestAsync_EmptyStore_ReturnsNull()
        {
            var store = new InMemoryBlockStore();
            Assert.Null(await store.GetLatestAsync());
        }

        [Fact]
        public async Task GetByHashAsync_MissingHash_ReturnsNull()
        {
            var store = new InMemoryBlockStore();
            Assert.Null(await store.GetByHashAsync(new byte[] { 0xFF }));
        }

        [Fact]
        public async Task GetByNumberAsync_MissingNumber_ReturnsNull()
        {
            var store = new InMemoryBlockStore();
            Assert.Null(await store.GetByNumberAsync(99));
        }

        [Fact]
        public async Task GetHashByNumberAsync_MissingNumber_ReturnsNull()
        {
            var store = new InMemoryBlockStore();
            Assert.Null(await store.GetHashByNumberAsync(99));
        }

        [Fact]
        public async Task DeleteByNumberAsync_RemovesBlock()
        {
            var store = new InMemoryBlockStore();
            var hash = new byte[] { 0xAA, 0xBB };
            await store.SaveAsync(CreateTestHeader(5), hash);

            await store.DeleteByNumberAsync(5);

            Assert.Null(await store.GetByNumberAsync(5));
            Assert.False(await store.ExistsAsync(hash));
        }

        [Fact]
        public async Task DeleteByNumberAsync_UpdatesHeight_WhenDeletingLatest()
        {
            var store = new InMemoryBlockStore();
            await store.SaveAsync(CreateTestHeader(1), new byte[] { 0x01 });
            await store.SaveAsync(CreateTestHeader(2), new byte[] { 0x02 });
            await store.SaveAsync(CreateTestHeader(3), new byte[] { 0x03 });

            await store.DeleteByNumberAsync(3);

            Assert.Equal(2, await store.GetHeightAsync());
        }

        [Fact]
        public async Task DeleteByNumberAsync_NonLatest_DoesNotAffectHeight()
        {
            var store = new InMemoryBlockStore();
            await store.SaveAsync(CreateTestHeader(1), new byte[] { 0x01 });
            await store.SaveAsync(CreateTestHeader(2), new byte[] { 0x02 });
            await store.SaveAsync(CreateTestHeader(3), new byte[] { 0x03 });

            await store.DeleteByNumberAsync(2);

            Assert.Equal(3, await store.GetHeightAsync());
        }

        [Fact]
        public async Task UpdateBlockHashAsync_ChangesHash()
        {
            var store = new InMemoryBlockStore();
            var oldHash = new byte[] { 0x01 };
            var newHash = new byte[] { 0xFF };
            await store.SaveAsync(CreateTestHeader(1), oldHash);

            await store.UpdateBlockHashAsync(1, newHash);

            Assert.False(await store.ExistsAsync(oldHash));
            Assert.True(await store.ExistsAsync(newHash));
            Assert.NotNull(await store.GetByNumberAsync(1));
        }

        [Fact]
        public void Clear_RemovesEverything()
        {
            var store = new InMemoryBlockStore();
            store.SaveAsync(CreateTestHeader(1), new byte[] { 0x01 }).Wait();
            store.SaveAsync(CreateTestHeader(2), new byte[] { 0x02 }).Wait();

            store.Clear();

            Assert.Equal(-1, store.GetHeightAsync().Result);
            Assert.Null(store.GetByNumberAsync(1).Result);
        }

        [Fact]
        public async Task SaveAsync_OlderBlock_DoesNotDecreaseHeight()
        {
            var store = new InMemoryBlockStore();
            await store.SaveAsync(CreateTestHeader(10), new byte[] { 0x0A });
            await store.SaveAsync(CreateTestHeader(5), new byte[] { 0x05 });

            Assert.Equal(10, await store.GetHeightAsync());
        }

        private BlockHeader CreateTestHeader(BigInteger number)
        {
            return new BlockHeader
            {
                BlockNumber = number,
                Timestamp = 1234567890,
                GasLimit = 8000000,
                GasUsed = 21000,
                Coinbase = AddressUtil.ZERO_ADDRESS
            };
        }
    }
}
