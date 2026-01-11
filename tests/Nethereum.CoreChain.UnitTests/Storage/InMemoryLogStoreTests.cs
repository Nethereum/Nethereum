using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class InMemoryLogStoreTests
    {
        [Fact]
        public async Task SaveAndGetLogsByTxHash_ShouldWork()
        {
            var store = new InMemoryLogStore();
            var txHash = new byte[] { 0x01, 0x02, 0x03 };
            var logs = new List<Log>
            {
                new Log { Address = "0x1234", Data = new byte[] { 0xAA } }
            };

            await store.SaveLogsAsync(logs, txHash, new byte[] { 0xFF }, 1, 0);
            var retrieved = await store.GetLogsByTxHashAsync(txHash);

            Assert.Single(retrieved);
            Assert.Equal("0x1234", retrieved[0].Address);
        }

        [Fact]
        public async Task SaveAndGetLogsByBlockHash_ShouldWork()
        {
            var store = new InMemoryLogStore();
            var blockHash = new byte[] { 0xAB, 0xCD };
            var logs = new List<Log>
            {
                new Log { Address = "0x1111" },
                new Log { Address = "0x2222" }
            };

            await store.SaveLogsAsync(logs, new byte[] { 0x01 }, blockHash, 5, 0);
            var retrieved = await store.GetLogsByBlockHashAsync(blockHash);

            Assert.Equal(2, retrieved.Count);
        }

        [Fact]
        public async Task GetLogsByBlockNumber_ShouldWork()
        {
            var store = new InMemoryLogStore();
            var logs = new List<Log> { new Log { Address = "0x1234" } };

            await store.SaveLogsAsync(logs, new byte[] { 0x01 }, new byte[] { 0xFF }, 10, 0);
            var retrieved = await store.GetLogsByBlockNumberAsync(10);

            Assert.Single(retrieved);
        }

        [Fact]
        public async Task GetLogsWithFilter_ByAddress_ShouldWork()
        {
            var store = new InMemoryLogStore();

            await store.SaveLogsAsync(
                new List<Log> { new Log { Address = "0xAAAA" } },
                new byte[] { 0x01 }, new byte[] { 0xFF }, 1, 0);

            await store.SaveLogsAsync(
                new List<Log> { new Log { Address = "0xBBBB" } },
                new byte[] { 0x02 }, new byte[] { 0xFF }, 1, 1);

            var filter = new LogFilter { Addresses = new List<string> { "0xAAAA" } };
            var retrieved = await store.GetLogsAsync(filter);

            Assert.Single(retrieved);
            Assert.Equal("0xAAAA", retrieved[0].Address);
        }

        [Fact]
        public async Task GetLogsWithFilter_ByBlockRange_ShouldWork()
        {
            var store = new InMemoryLogStore();

            await store.SaveLogsAsync(
                new List<Log> { new Log { Address = "0x1111" } },
                new byte[] { 0x01 }, new byte[] { 0x01 }, 5, 0);

            await store.SaveLogsAsync(
                new List<Log> { new Log { Address = "0x2222" } },
                new byte[] { 0x02 }, new byte[] { 0x02 }, 10, 0);

            await store.SaveLogsAsync(
                new List<Log> { new Log { Address = "0x3333" } },
                new byte[] { 0x03 }, new byte[] { 0x03 }, 15, 0);

            var filter = new LogFilter { FromBlock = 6, ToBlock = 12 };
            var retrieved = await store.GetLogsAsync(filter);

            Assert.Single(retrieved);
            Assert.Equal("0x2222", retrieved[0].Address);
        }

        [Fact]
        public async Task FilteredLog_ShouldContainBlockContext()
        {
            var store = new InMemoryLogStore();
            var txHash = new byte[] { 0xAA, 0xBB };
            var blockHash = new byte[] { 0xCC, 0xDD };
            var logs = new List<Log> { new Log { Address = "0x1234", Data = new byte[] { 0x01 } } };

            await store.SaveLogsAsync(logs, txHash, blockHash, 42, 3);
            var retrieved = await store.GetLogsByTxHashAsync(txHash);

            Assert.Single(retrieved);
            Assert.Equal(blockHash, retrieved[0].BlockHash);
            Assert.Equal(42, retrieved[0].BlockNumber);
            Assert.Equal(txHash, retrieved[0].TransactionHash);
            Assert.Equal(3, retrieved[0].TransactionIndex);
            Assert.Equal(0, retrieved[0].LogIndex);
        }
    }
}
