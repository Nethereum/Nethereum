using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;
using Xunit;

namespace Nethereum.AppChain.Anchoring.UnitTests
{
    public class StubChainAnchorable : IChainAnchorable
    {
        private readonly BigInteger _blockNumber;
        private readonly BlockHeader _blockHeader;

        public StubChainAnchorable(BigInteger blockNumber = default)
        {
            _blockNumber = blockNumber == default ? 100 : blockNumber;
            _blockHeader = new BlockHeader
            {
                BlockNumber = _blockNumber,
                StateRoot = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            for (int i = 0; i < 32; i++)
            {
                _blockHeader.StateRoot[i] = (byte)(i + 1);
                _blockHeader.TransactionsHash[i] = (byte)(i + 2);
                _blockHeader.ReceiptHash[i] = (byte)(i + 3);
            }
        }

        public Task<BigInteger> GetBlockNumberAsync() => Task.FromResult(_blockNumber);

        public Task<BlockHeader?> GetBlockByNumberAsync(BigInteger blockNumber)
        {
            if (blockNumber > _blockNumber)
                return Task.FromResult<BlockHeader?>(null);
            return Task.FromResult<BlockHeader?>(_blockHeader);
        }

        public Task<byte[]?> GetBlockHashByNumberAsync(BigInteger blockNumber)
        {
            if (blockNumber > _blockNumber)
                return Task.FromResult<byte[]?>(null);
            return Task.FromResult<byte[]?>(new byte[32]);
        }
    }

    public class StubAnchorService : IAnchorService
    {
        private readonly Dictionary<BigInteger, AnchorInfo> _anchors = new();
        private BigInteger _latestAnchoredBlock = 0;

        public Task<AnchorInfo> AnchorBlockAsync(
            BigInteger blockNumber,
            byte[] stateRoot,
            byte[] transactionsRoot,
            byte[] receiptsRoot)
        {
            var anchor = new AnchorInfo
            {
                BlockNumber = blockNumber,
                StateRoot = stateRoot,
                TransactionsRoot = transactionsRoot,
                ReceiptsRoot = receiptsRoot,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Status = AnchorStatus.Confirmed,
                AnchorTxHash = new byte[32],
                AnchorBlockNumber = 12345
            };

            _anchors[blockNumber] = anchor;
            if (blockNumber > _latestAnchoredBlock)
                _latestAnchoredBlock = blockNumber;

            return Task.FromResult(anchor);
        }

        public Task<AnchorInfo?> GetAnchorAsync(BigInteger blockNumber)
        {
            return Task.FromResult(_anchors.TryGetValue(blockNumber, out var anchor) ? anchor : null);
        }

        public Task<BigInteger> GetLatestAnchoredBlockAsync()
        {
            return Task.FromResult(_latestAnchoredBlock);
        }

        public Task<bool> VerifyAnchorAsync(
            BigInteger blockNumber,
            byte[] stateRoot,
            byte[] transactionsRoot,
            byte[] receiptsRoot)
        {
            if (!_anchors.TryGetValue(blockNumber, out var anchor))
                return Task.FromResult(false);

            return Task.FromResult(
                ByteArraysEqual(anchor.StateRoot, stateRoot) &&
                ByteArraysEqual(anchor.TransactionsRoot, transactionsRoot) &&
                ByteArraysEqual(anchor.ReceiptsRoot, receiptsRoot));
        }

        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }

    public class AnchoringTests
    {
        [Fact]
        public async Task AnchorInfo_DefaultValues_AreCorrect()
        {
            var anchor = new AnchorInfo();

            Assert.Equal(BigInteger.Zero, anchor.BlockNumber);
            Assert.Empty(anchor.StateRoot);
            Assert.Empty(anchor.TransactionsRoot);
            Assert.Empty(anchor.ReceiptsRoot);
            Assert.Equal(AnchorStatus.Pending, anchor.Status);
            Assert.Null(anchor.ErrorMessage);
        }

        [Fact]
        public async Task AnchorConfig_DefaultValues_AreCorrect()
        {
            var config = new AnchorConfig();

            Assert.True(config.Enabled);
            Assert.Equal(100, config.AnchorCadence);
            Assert.Equal(60000, config.AnchorIntervalMs);
            Assert.Equal(3, config.MaxRetries);
            Assert.Equal(5000, config.RetryDelayMs);
        }

        [Fact]
        public async Task StubAnchorService_AnchorBlock_ReturnsConfirmed()
        {
            var service = new StubAnchorService();
            var stateRoot = new byte[32];
            var txRoot = new byte[32];
            var receiptRoot = new byte[32];

            var result = await service.AnchorBlockAsync(100, stateRoot, txRoot, receiptRoot);

            Assert.Equal(AnchorStatus.Confirmed, result.Status);
            Assert.Equal(100, result.BlockNumber);
            Assert.NotNull(result.AnchorTxHash);
        }

        [Fact]
        public async Task StubAnchorService_GetAnchor_ReturnsAnchoredBlock()
        {
            var service = new StubAnchorService();
            var stateRoot = new byte[32];
            var txRoot = new byte[32];
            var receiptRoot = new byte[32];

            await service.AnchorBlockAsync(100, stateRoot, txRoot, receiptRoot);
            var anchor = await service.GetAnchorAsync(100);

            Assert.NotNull(anchor);
            Assert.Equal(100, anchor!.BlockNumber);
        }

        [Fact]
        public async Task StubAnchorService_GetAnchor_ReturnsNullForUnanchoredBlock()
        {
            var service = new StubAnchorService();

            var anchor = await service.GetAnchorAsync(100);

            Assert.Null(anchor);
        }

        [Fact]
        public async Task StubAnchorService_GetLatestAnchoredBlock_ReturnsCorrectValue()
        {
            var service = new StubAnchorService();
            var stateRoot = new byte[32];
            var txRoot = new byte[32];
            var receiptRoot = new byte[32];

            await service.AnchorBlockAsync(100, stateRoot, txRoot, receiptRoot);
            await service.AnchorBlockAsync(200, stateRoot, txRoot, receiptRoot);

            var latest = await service.GetLatestAnchoredBlockAsync();

            Assert.Equal(200, latest);
        }

        [Fact]
        public async Task StubAnchorService_VerifyAnchor_ReturnsTrueForValidAnchor()
        {
            var service = new StubAnchorService();
            var stateRoot = new byte[32];
            var txRoot = new byte[32];
            var receiptRoot = new byte[32];

            await service.AnchorBlockAsync(100, stateRoot, txRoot, receiptRoot);
            var isValid = await service.VerifyAnchorAsync(100, stateRoot, txRoot, receiptRoot);

            Assert.True(isValid);
        }

        [Fact]
        public async Task StubAnchorService_VerifyAnchor_ReturnsFalseForInvalidAnchor()
        {
            var service = new StubAnchorService();
            var stateRoot = new byte[32];
            var txRoot = new byte[32];
            var receiptRoot = new byte[32];

            await service.AnchorBlockAsync(100, stateRoot, txRoot, receiptRoot);

            var differentStateRoot = new byte[32];
            differentStateRoot[0] = 1;
            var isValid = await service.VerifyAnchorAsync(100, differentStateRoot, txRoot, receiptRoot);

            Assert.False(isValid);
        }

        [Fact]
        public async Task StubChainAnchorable_GetBlockNumber_ReturnsConfiguredValue()
        {
            var chain = new StubChainAnchorable(150);

            var blockNumber = await chain.GetBlockNumberAsync();

            Assert.Equal(150, blockNumber);
        }

        [Fact]
        public async Task StubChainAnchorable_GetBlockByNumber_ReturnsBlock()
        {
            var chain = new StubChainAnchorable(100);

            var block = await chain.GetBlockByNumberAsync(100);

            Assert.NotNull(block);
            Assert.Equal(100, block!.BlockNumber);
        }

        [Fact]
        public async Task StubChainAnchorable_GetBlockByNumber_ReturnsNullForFutureBlock()
        {
            var chain = new StubChainAnchorable(100);

            var block = await chain.GetBlockByNumberAsync(200);

            Assert.Null(block);
        }

        [Fact]
        public void EvmAnchorService_WithoutConfig_DoesNotThrow()
        {
            var config = new AnchorConfig { Enabled = false };

            var service = new EvmAnchorService(config);

            Assert.NotNull(service);
        }

        [Fact]
        public async Task EvmAnchorService_AnchorWithoutRpc_ReturnsFailed()
        {
            var config = new AnchorConfig { Enabled = true };
            var service = new EvmAnchorService(config);

            var result = await service.AnchorBlockAsync(100, new byte[32], new byte[32], new byte[32]);

            Assert.Equal(AnchorStatus.Failed, result.Status);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task EvmAnchorService_GetLatestAnchoredBlock_ReturnsZeroWithoutRpc()
        {
            var config = new AnchorConfig { Enabled = true };
            var service = new EvmAnchorService(config);

            var latest = await service.GetLatestAnchoredBlockAsync();

            Assert.Equal(BigInteger.Zero, latest);
        }

        [Fact]
        public async Task EvmAnchorService_GetAnchor_ReturnsNullWithoutRpc()
        {
            var config = new AnchorConfig { Enabled = true };
            var service = new EvmAnchorService(config);

            var anchor = await service.GetAnchorAsync(100);

            Assert.Null(anchor);
        }

        [Fact]
        public async Task EvmAnchorService_VerifyAnchor_ReturnsFalseWithoutRpc()
        {
            var config = new AnchorConfig { Enabled = true };
            var service = new EvmAnchorService(config);

            var isValid = await service.VerifyAnchorAsync(100, new byte[32], new byte[32], new byte[32]);

            Assert.False(isValid);
        }
    }
}
