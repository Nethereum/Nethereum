using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain;
using Nethereum.DevChain.Storage.Sqlite;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class DevChainBlockAccessListDurabilityTests : IDisposable
    {
        private readonly string _dbPath = Path.Combine(
            Path.GetTempPath(), $"devchain-bal-{Guid.NewGuid():N}.db");

        [Fact]
        public async Task Given_AnAmsterdamDevChain_When_TheNodeIsClosedAndTheDatabaseReopened_Then_TheAccessListIsStillThere()
        {
            byte[] commitment;
            using (var node = await StartAsync("amsterdam"))
            {
                await node.MineBlockAsync();
                commitment = (await node.GetBlockByNumberAsync(1)).BlockAccessListHash;
                Assert.NotNull(commitment);
            }

            var retained = await ReopenAndReadAsync(blockNumber: 1);

            Assert.NotNull(retained);
            Assert.Equal(commitment, new Sha3Keccack().CalculateHash(retained));
        }

        // EIP-7928: the field is "the RLP-encoded BAL or `null` for pre-Amsterdam blocks".
        [Fact]
        public async Task Given_APragueDevChain_When_TheDatabaseIsReopened_Then_NothingWasRetained()
        {
            using (var node = await StartAsync("prague"))
            {
                await node.MineBlockAsync();
                Assert.Null((await node.GetBlockByNumberAsync(1)).BlockAccessListHash);
            }

            Assert.Null(await ReopenAndReadAsync(blockNumber: 1));
        }

        [Fact]
        public async Task Given_AnAmsterdamDevChain_When_AHeightAboveTheHeadIsRequested_Then_NothingIsReturned()
        {
            using (var node = await StartAsync("amsterdam"))
            {
                await node.MineBlockAsync();
            }

            Assert.Null(await ReopenAndReadAsync(blockNumber: 2));
        }

        private async Task<DevChainNode> StartAsync(string hardfork)
        {
            var config = DevChainConfig.Default;
            config.Hardfork = hardfork;
            var node = new DevChainNode(config, _dbPath, persistDb: true);
            await node.StartAsync();
            return node;
        }

        private static async Task<byte[]> ReadAsync(string dbPath, int blockNumber)
        {
            using var manager = new SqliteStorageManager(dbPath, deleteOnDispose: false);
            IBlockStore blockStore = new SqliteBlockStore(manager);
            var store = new SqliteBlockAccessListStore(manager, blockStore);
            return await store.GetByBlockNumberAsync(blockNumber);
        }

        private Task<byte[]> ReopenAndReadAsync(int blockNumber) => ReadAsync(_dbPath, blockNumber);

        public void Dispose()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }
    }
}
