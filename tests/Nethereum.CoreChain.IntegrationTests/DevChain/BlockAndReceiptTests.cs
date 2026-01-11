using System.Numerics;
using Nethereum.CoreChain.IntegrationTests.Contracts;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.DevChain
{
    public class BlockAndReceiptTests : IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _fixture;

        public BlockAndReceiptTests(DevChainNodeFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GenesisBlock_ExistsAfterStart()
        {
            var blockNumber = await _fixture.Node.GetBlockNumberAsync();
            Assert.True(blockNumber >= 0);

            var genesisBlock = await _fixture.Node.GetBlockByNumberAsync(0);
            Assert.NotNull(genesisBlock);
            Assert.Equal(BigInteger.Zero, genesisBlock.BlockNumber);
        }

        [Fact]
        public async Task Block_HasCorrectParentHash()
        {
            // Get current block before mining
            var currentBlockNumber = await _fixture.Node.GetBlockNumberAsync();
            var currentBlockHash = await _fixture.Node.GetBlockHashByNumberAsync(currentBlockNumber);
            Assert.NotNull(currentBlockHash);

            // Mine a new block
            var newBlockHash = await _fixture.Node.MineBlockAsync();

            var newBlock = await _fixture.Node.GetBlockByHashAsync(newBlockHash);
            Assert.NotNull(newBlock);

            // New block's parent should be the previous block
            Assert.Equal(currentBlockHash, newBlock.ParentHash);
        }

        [Fact]
        public async Task Block_ContainsTransactions()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000")); // 0.1 ETH

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            // With AutoMine=true, transaction should be in a block
            var latestBlock = await _fixture.Node.GetLatestBlockAsync();
            Assert.NotNull(latestBlock);

            // TransactionsHash should not be empty trie root if block has transactions
            Assert.NotNull(latestBlock.TransactionsHash);
        }

        [Fact]
        public async Task Block_HasCorrectTimestamp()
        {
            var beforeMine = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await _fixture.Node.MineBlockAsync();
            var afterMine = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var block = await _fixture.Node.GetLatestBlockAsync();
            Assert.NotNull(block);

            // Block timestamp should be within reasonable range
            var timestamp = (long)block.Timestamp;
            Assert.True(timestamp >= beforeMine - 1, $"Timestamp {timestamp} is before expected {beforeMine}");
            Assert.True(timestamp <= afterMine + 1, $"Timestamp {timestamp} is after expected {afterMine}");
        }

        [Fact]
        public async Task Block_HasReceiptsRoot()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("50000000000000000")); // 0.05 ETH

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var block = await _fixture.Node.GetLatestBlockAsync();
            Assert.NotNull(block);
            Assert.NotNull(block.ReceiptHash);
        }

        [Fact]
        public async Task Receipt_HasCorrectGasUsed()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000")); // 0.1 ETH
            var txHash = signedTx.Hash;

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var receiptInfo = await _fixture.Node.GetTransactionReceiptInfoAsync(txHash);
            Assert.NotNull(receiptInfo);
            // Simple ETH transfer uses 21000 gas
            Assert.Equal((BigInteger)21000, receiptInfo.GasUsed);
        }

        [Fact]
        public async Task Receipt_HasCorrectBlockInfo()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000")); // 0.1 ETH
            var txHash = signedTx.Hash;

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var receiptInfo = await _fixture.Node.GetTransactionReceiptInfoAsync(txHash);
            Assert.NotNull(receiptInfo);

            var block = await _fixture.Node.GetLatestBlockAsync();
            Assert.NotNull(block);

            Assert.Equal(block.BlockNumber, receiptInfo.BlockNumber);

            var blockHash = await _fixture.Node.GetBlockHashByNumberAsync(block.BlockNumber);
            Assert.Equal(blockHash, receiptInfo.BlockHash);
        }

        [Fact]
        public async Task Receipt_HasTransactionIndex()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));
            var txHash = signedTx.Hash;

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var receiptInfo = await _fixture.Node.GetTransactionReceiptInfoAsync(txHash);
            Assert.NotNull(receiptInfo);
            Assert.True(receiptInfo.TransactionIndex >= 0);
        }

        [Fact]
        public async Task MineEmptyBlock_IncreasesBlockNumber()
        {
            var initialBlockNumber = await _fixture.Node.GetBlockNumberAsync();

            await _fixture.Node.MineBlockAsync();

            var newBlockNumber = await _fixture.Node.GetBlockNumberAsync();
            Assert.Equal(initialBlockNumber + 1, newBlockNumber);
        }

        [Fact]
        public async Task GetBlockByHash_ReturnsCorrectBlock()
        {
            var blockHash = await _fixture.Node.MineBlockAsync();

            var block = await _fixture.Node.GetBlockByHashAsync(blockHash);

            Assert.NotNull(block);
        }

        [Fact]
        public async Task GetBlockByNumber_ReturnsCorrectBlock()
        {
            await _fixture.Node.MineBlockAsync();

            var blockNumber = await _fixture.Node.GetBlockNumberAsync();
            var block = await _fixture.Node.GetBlockByNumberAsync(blockNumber);

            Assert.NotNull(block);
            Assert.Equal(blockNumber, block.BlockNumber);
        }

        [Fact]
        public async Task Block_HasValidStateRoot()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var block = await _fixture.Node.GetLatestBlockAsync();
            Assert.NotNull(block);
            Assert.NotNull(block.StateRoot);
            Assert.Equal(32, block.StateRoot.Length);
        }
    }
}
