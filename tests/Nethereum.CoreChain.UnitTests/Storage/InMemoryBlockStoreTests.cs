using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
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

        private BlockHeader CreateTestHeader(BigInteger number)
        {
            return new BlockHeader
            {
                BlockNumber = number,
                Timestamp = 1234567890,
                GasLimit = 8000000,
                GasUsed = 21000,
                Coinbase = "0x0000000000000000000000000000000000000000"
            };
        }
    }
}
