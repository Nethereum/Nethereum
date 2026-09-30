using System.Collections.Generic;
using System.Text.Json;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class BlockHeaderExtensionsTests
    {
        private static readonly byte[] BlockHash = new byte[32];

        private static BlockHeader PostShanghaiHeader() => new BlockHeader
        {
            Difficulty = new EvmUInt256(0UL),
            WithdrawalsRoot = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 }
        };

        private static BlockHeader PreShanghaiHeader(ulong difficulty) => new BlockHeader
        {
            Difficulty = new EvmUInt256(difficulty),
            WithdrawalsRoot = null
        };

        [Fact]
        public void Given_PostShanghaiHeader_When_ToBlockWithTransactionHashes_Then_WithdrawalsPopulated()
        {
            var withdrawals = new List<Withdrawal>
            {
                new Withdrawal { Index = 7, ValidatorIndex = 11, Address = new byte[20], AmountInGwei = 42 }
            };

            var block = PostShanghaiHeader().ToBlockWithTransactionHashes(BlockHash, null, blockSize: 500, withdrawals);

            Assert.NotNull(block.Withdrawals);
            Assert.Single(block.Withdrawals);
            Assert.Equal(new HexBigInteger(7), block.Withdrawals[0].Index);
            Assert.Equal(new HexBigInteger(11), block.Withdrawals[0].ValidatorIndex);
            Assert.Equal(new HexBigInteger(42), block.Withdrawals[0].Amount);
        }

        [Fact]
        public void Given_PostShanghaiHeader_When_NoWithdrawalsStored_Then_WithdrawalsIsEmptyArray_NotNull()
        {
            var block = PostShanghaiHeader().ToBlockWithTransactionHashes(BlockHash, null, blockSize: 500, withdrawals: null);

            Assert.NotNull(block.Withdrawals);
            Assert.Empty(block.Withdrawals);
        }

        [Fact]
        public void Given_PreShanghaiHeader_When_ToBlockWithTransactionHashes_Then_WithdrawalsFieldStaysNull()
        {
            var block = PreShanghaiHeader(difficulty: 1000).ToBlockWithTransactionHashes(BlockHash, null, blockSize: 500);

            Assert.Null(block.Withdrawals);
        }

        [Theory]
        [InlineData(0UL)]
        [InlineData(1000UL)]
        public void Given_AnyHeader_When_ToBlockWithTransactions_Then_TotalDifficultyNeverFabricated(ulong difficulty)
        {
            var block = PreShanghaiHeader(difficulty).ToBlockWithTransactions(BlockHash, null, blockSize: 500);

            Assert.Null(block.TotalDifficulty);
        }

        [Fact]
        public void Given_ExplicitBlockSize_When_ToBlockWithTransactions_Then_SizeIsCallerSuppliedValue_NotHeaderGuess()
        {
            var block = PreShanghaiHeader(1000).ToBlockWithTransactions(BlockHash, null, blockSize: 12345);

            Assert.Equal(new HexBigInteger(12345), block.Size);
        }

        [Fact]
        public void Given_HeaderAndTxs_When_CalculateFullBlockSize_Then_GrowsWithTransactionCount()
        {
            var header = PreShanghaiHeader(1000);
            var noTxs = BlockHeaderExtensions.CalculateFullBlockSize(header, transactions: null, uncles: null);

            var tx = new LegacyTransaction(
                nonce: new byte[] { 0x01 }, gasPrice: new byte[] { 0x14 }, gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20], value: new byte[] { }, data: new byte[0],
                r: new byte[32], s: new byte[32], v: 27);
            var withTx = BlockHeaderExtensions.CalculateFullBlockSize(
                header, new List<ISignedTransaction> { tx }, uncles: null);

            Assert.True(withTx > noTxs);
        }

        [Fact]
        public void Given_PostShanghaiHeader_When_CalculateFullBlockSize_Then_IncludesWithdrawalsEvenWhenEmpty()
        {
            var header = PostShanghaiHeader();
            var preShanghaiSameShape = PreShanghaiHeader(0);

            var postShanghaiSize = BlockHeaderExtensions.CalculateFullBlockSize(header, null, null, withdrawals: null);
            var preShanghaiSize = BlockHeaderExtensions.CalculateFullBlockSize(preShanghaiSameShape, null, null, withdrawals: null);

            Assert.True(postShanghaiSize > preShanghaiSize);
        }

        [Fact]
        public void Given_HeaderAndTxs_When_CalculateFullBlockSize_Then_EqualsNewBlockMessageEncoderEncodeBlockLength()
        {
            var header = PreShanghaiHeader(1000);
            var tx = new LegacyTransaction(
                nonce: new byte[] { 0x01 }, gasPrice: new byte[] { 0x14 }, gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20], value: new byte[] { }, data: new byte[0],
                r: new byte[32], s: new byte[32], v: 27);
            var transactions = new List<ISignedTransaction> { tx };

            var blockSize = BlockHeaderExtensions.CalculateFullBlockSize(header, transactions, uncles: null);
            var expected = NewBlockMessageEncoder.EncodeBlock(header, transactions, uncles: null, withdrawals: null).Length;

            Assert.Equal(expected, blockSize);
        }

        [Fact]
        public void Given_HeaderWithWithdrawals_When_CalculateFullBlockSize_Then_EqualsNewBlockMessageEncoderEncodeBlockLength()
        {
            var header = PostShanghaiHeader();
            var withdrawals = new List<Withdrawal>
            {
                new Withdrawal { Index = 7, ValidatorIndex = 11, Address = new byte[20], AmountInGwei = 42 }
            };

            var blockSize = BlockHeaderExtensions.CalculateFullBlockSize(header, transactions: null, uncles: null, withdrawals);
            var expected = NewBlockMessageEncoder.EncodeBlock(header, transactions: null, uncles: null, withdrawals).Length;

            Assert.Equal(expected, blockSize);
        }

        [Fact]
        public void Given_NullTotalDifficultyAndParityFields_When_SerializedForTheWire_Then_KeysAreOmitted()
        {
            var block = PreShanghaiHeader(1000).ToBlockWithTransactionHashes(BlockHash, null, blockSize: 500);

            var json = JsonSerializer.Serialize(block, CoreChainJsonContext.Default.BlockWithTransactionHashes);

            Assert.DoesNotContain("\"totalDifficulty\"", json);
            Assert.DoesNotContain("\"author\"", json);
            Assert.DoesNotContain("\"sealFields\"", json);
            Assert.DoesNotContain("\"withdrawals\"", json);
        }

        [Fact]
        public void Given_PostShanghaiHeader_When_SerializedForTheWire_Then_WithdrawalsKeyPresent()
        {
            var block = PostShanghaiHeader().ToBlockWithTransactionHashes(BlockHash, null, blockSize: 500, withdrawals: null);

            var json = JsonSerializer.Serialize(block, CoreChainJsonContext.Default.BlockWithTransactionHashes);

            Assert.Contains("\"withdrawals\":[]", json);
        }

#pragma warning disable CS0618
        [Fact]
        public void Given_610ShapedCall_When_ToBlockWithTransactionHashes_Then_CompilesAndUsesSuppliedSizeAndDifficulty()
        {
            var block = PreShanghaiHeader(1000).ToBlockWithTransactionHashes(BlockHash, new string[0], totalDifficulty: 99, blockSize: 500);

            Assert.Equal(new HexBigInteger(500), block.Size);
            Assert.Equal(new HexBigInteger(99), block.TotalDifficulty);
        }

        [Fact]
        public void Given_610ShapedCall_When_TotalDifficultyOmitted_Then_TotalDifficultyStaysNull()
        {
            var block = PreShanghaiHeader(1000).ToBlockWithTransactionHashes(BlockHash, new string[0]);

            Assert.Null(block.TotalDifficulty);
        }

        [Fact]
        public void Given_610ShapedCall_When_ToBlockWithTransactions_Then_CompilesAndUsesSuppliedSizeAndDifficulty()
        {
            var block = PreShanghaiHeader(1000).ToBlockWithTransactions(BlockHash, new Nethereum.RPC.Eth.DTOs.Transaction[0], totalDifficulty: 99, blockSize: 500);

            Assert.Equal(new HexBigInteger(500), block.Size);
            Assert.Equal(new HexBigInteger(99), block.TotalDifficulty);
        }

        [Fact]
        public void Given_610ShapedCall_When_ToBlockWithTransactionsTotalDifficultyOmitted_Then_TotalDifficultyStaysNull()
        {
            var block = PreShanghaiHeader(1000).ToBlockWithTransactions(BlockHash, new Nethereum.RPC.Eth.DTOs.Transaction[0]);

            Assert.Null(block.TotalDifficulty);
        }
#pragma warning restore CS0618
    }
}
