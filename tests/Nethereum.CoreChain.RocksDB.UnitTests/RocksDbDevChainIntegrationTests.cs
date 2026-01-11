using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevChain;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbDevChainIntegrationTests : IClassFixture<RocksDbTestFixture>, IDisposable
    {
        private readonly RocksDbTestFixture _fixture;

        public RocksDbDevChainIntegrationTests(RocksDbTestFixture fixture)
        {
            _fixture = fixture;
            _fixture.Reset();
        }

        public void Dispose()
        {
        }

        private DevChainNode CreateNode(DevChainConfig config = null)
        {
            return new DevChainNode(
                config ?? DevChainConfig.Default,
                _fixture.BlockStore,
                _fixture.TransactionStore,
                _fixture.ReceiptStore,
                _fixture.LogStore,
                _fixture.StateStore,
                _fixture.FilterStore);
        }

        [Fact]
        public async Task StartAsync_CreatesGenesisBlock()
        {
            var node = CreateNode();
            await node.StartAsync();

            var blockNumber = await node.GetBlockNumberAsync();
            Assert.Equal(0, blockNumber);

            var genesisBlock = await node.GetBlockByNumberAsync(0);
            Assert.NotNull(genesisBlock);
            Assert.Equal(0, genesisBlock.BlockNumber);
        }

        [Fact]
        public async Task StartAsync_WithPrefundedAccounts_SetsBalance()
        {
            var node = CreateNode();
            var addresses = new[] { "0x1234567890123456789012345678901234567890" };

            await node.StartAsync(addresses);

            var balance = await node.GetBalanceAsync("0x1234567890123456789012345678901234567890");
            Assert.Equal(BigInteger.Parse("10000000000000000000000"), balance);
        }

        [Fact]
        public async Task StartAsync_WithCustomBalance_SetsCorrectBalance()
        {
            var node = CreateNode();
            var addresses = new[] { "0x1234567890123456789012345678901234567890" };
            var customBalance = BigInteger.Parse("1000000000000000000");

            await node.StartAsync(addresses, customBalance);

            var balance = await node.GetBalanceAsync("0x1234567890123456789012345678901234567890");
            Assert.Equal(customBalance, balance);
        }

        [Fact]
        public async Task SetBalance_UpdatesAccountBalance()
        {
            var node = CreateNode();
            await node.StartAsync();

            var address = "0x1234567890123456789012345678901234567890";
            var balance = BigInteger.Parse("5000000000000000000");

            await node.SetBalanceAsync(address, balance);
            var retrieved = await node.GetBalanceAsync(address);

            Assert.Equal(balance, retrieved);
        }

        [Fact]
        public async Task SetNonce_UpdatesAccountNonce()
        {
            var node = CreateNode();
            await node.StartAsync();

            var address = "0x1234567890123456789012345678901234567890";
            var nonce = new BigInteger(10);

            await node.SetNonceAsync(address, nonce);
            var retrieved = await node.GetNonceAsync(address);

            Assert.Equal(nonce, retrieved);
        }

        [Fact]
        public async Task SetCode_StoresAndRetrievesCode()
        {
            var node = CreateNode();
            await node.StartAsync();

            var address = "0x1234567890123456789012345678901234567890";
            var code = new byte[] { 0x60, 0x80, 0x60, 0x40, 0x52 };

            await node.SetCodeAsync(address, code);
            var retrieved = await node.GetCodeAsync(address);

            Assert.Equal(code, retrieved);
        }

        [Fact]
        public async Task SetStorageAt_StoresAndRetrievesStorage()
        {
            var node = CreateNode();
            await node.StartAsync();

            var address = "0x1234567890123456789012345678901234567890";
            var slot = BigInteger.Zero;
            var value = new byte[] { 0x01, 0x02, 0x03 };

            await node.SetStorageAtAsync(address, slot, value);
            var retrieved = await node.GetStorageAtAsync(address, slot);

            Assert.Equal(value, retrieved);
        }

        [Fact]
        public async Task MineBlock_IncreasesBlockNumber()
        {
            var node = CreateNode();
            await node.StartAsync();

            var initialBlockNumber = await node.GetBlockNumberAsync();
            await node.MineBlockAsync();
            var newBlockNumber = await node.GetBlockNumberAsync();

            Assert.Equal(initialBlockNumber + 1, newBlockNumber);
        }

        [Fact]
        public async Task MineBlock_ReturnsBlockHash()
        {
            var node = CreateNode();
            await node.StartAsync();

            var blockHash = await node.MineBlockAsync();

            Assert.NotNull(blockHash);
            Assert.Equal(32, blockHash.Length);
        }

        [Fact]
        public async Task GetBlockByHash_ReturnsCorrectBlock()
        {
            var node = CreateNode();
            await node.StartAsync();

            var blockHash = await node.MineBlockAsync();
            var block = await node.GetBlockByHashAsync(blockHash);

            Assert.NotNull(block);
            Assert.Equal(1, block.BlockNumber);
        }

        [Fact]
        public async Task TakeSnapshot_AllowsRevert()
        {
            var node = CreateNode();
            await node.StartAsync();

            var address = "0x1234567890123456789012345678901234567890";
            await node.SetBalanceAsync(address, 100);

            var snapshot = await node.TakeSnapshotAsync();

            await node.SetBalanceAsync(address, 500);
            Assert.Equal(500, await node.GetBalanceAsync(address));

            await node.RevertToSnapshotAsync(snapshot);
            Assert.Equal(100, await node.GetBalanceAsync(address));
        }

        [Fact]
        public async Task GetPendingBlockContext_ReturnsNextBlockInfo()
        {
            var node = CreateNode();
            await node.StartAsync();

            var context = node.GetPendingBlockContext();

            Assert.Equal(1, context.BlockNumber);
        }

        [Fact]
        public async Task Config_ExposesConfiguration()
        {
            var config = new DevChainConfig { ChainId = 12345 };
            var node = CreateNode(config);
            await node.StartAsync();

            Assert.Equal(12345, node.DevConfig.ChainId);
        }

        [Fact]
        public async Task GetNonce_ReturnsZeroForNonExistentAccount()
        {
            var node = CreateNode();
            await node.StartAsync();

            var nonce = await node.GetNonceAsync("0x0000000000000000000000000000000000000001");

            Assert.Equal(0, nonce);
        }

        [Fact]
        public async Task GetBalance_ReturnsZeroForNonExistentAccount()
        {
            var node = CreateNode();
            await node.StartAsync();

            var balance = await node.GetBalanceAsync("0x0000000000000000000000000000000000000001");

            Assert.Equal(0, balance);
        }

        [Fact]
        public async Task DataPersistence_SurvivesManagerRecreate()
        {
            var address = "0x1234567890123456789012345678901234567890";
            var balance = BigInteger.Parse("5000000000000000000");
            var dbPath = _fixture.DatabasePath;

            var node = CreateNode();
            await node.StartAsync();
            await node.SetBalanceAsync(address, balance);
            await node.MineBlockAsync();

            _fixture.Manager.Dispose();

            var options = new RocksDbStorageOptions { DatabasePath = dbPath };
            using var newManager = new RocksDbManager(options);
            var newBlockStore = new Stores.RocksDbBlockStore(newManager);
            var newStateStore = new Stores.RocksDbStateStore(newManager);

            var retrievedBlock = await newBlockStore.GetByNumberAsync(1);
            Assert.NotNull(retrievedBlock);
            Assert.Equal(1, retrievedBlock.BlockNumber);

            var retrievedBalance = (await newStateStore.GetAccountAsync(address))?.Balance ?? 0;
            Assert.Equal(balance, retrievedBalance);
        }
    }
}
