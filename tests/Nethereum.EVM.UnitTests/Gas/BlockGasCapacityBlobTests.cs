using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Util;
using Nethereum.EVM.Gas;
using Nethereum.EVM.UnitTests;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class BlockGasCapacityBlobTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string RecipientAddress = "0x2222222222222222222222222222222222222222";
        private const string ValidVersionedHash = "0x01" + "cc" + "0000000000000000000000000000000000000000000000000000000000";

        private static List<string> Blobs(int count)
        {
            var hashes = new List<string>();
            for (var i = 0; i < count; i++) hashes.Add(ValidVersionedHash);
            return hashes;
        }

        private static async Task<TransactionExecutionContext> BlobTransactionAsync(
            int blobCount, long blockGasLimit, BlockGasCapacity capacity)
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetBalanceAsync(RecipientAddress, 1);

            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = RecipientAddress,
                Data = null,
                IsContractCreation = false,
                IsType3Transaction = true,
                BlobVersionedHashes = Blobs(blobCount),
                MaxFeePerBlobGas = new EvmUInt256(1_000_000_000),
                GasLimit = 100_000,
                Value = 0,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                BlockGasLimit = blockGasLimit,
                Coinbase = SenderAddress,
                ExecutionState = new ExecutionStateService(node)
            };
            if (capacity != null) ctx.BlockGasCapacity = capacity;
            return ctx;
        }

        private static HardforkConfig Prague() =>
            HardforkConfig.Prague.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());

        [Fact]
        public async Task Given_MoreBlobsThanTheBlockAllows_When_NoBlockGasLimitIsNamed_Then_TheTransactionIsRefused()
        {
            var config = Prague();
            var ctx = await BlobTransactionAsync(blobCount: config.MaxBlobsPerBlock + 1, blockGasLimit: 0, capacity: null);

            var result = await new TransactionExecutor(config).ExecuteAsync(ctx);

            Assert.True(result.IsValidationError, "an over-blobbed transaction must be refused");
            Assert.Equal(TransactionError.Type3TxBlobCountExceeded, result.ErrorCode);
        }

        [Fact]
        public async Task Given_ExactlyTheBlocksBlobAllowance_When_NoBlockGasLimitIsNamed_Then_TheTransactionIsAdmitted()
        {
            var config = Prague();
            var ctx = await BlobTransactionAsync(blobCount: config.MaxBlobsPerBlock, blockGasLimit: 0, capacity: null);

            var result = await new TransactionExecutor(config).ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
        }

        [Fact]
        public async Task Given_TwoBlobTransactionsWhoseCombinedBlobsExceedTheBlock_When_SharingOneCapacity_Then_TheSecondIsRefused()
        {
            var config = Prague();
            var capacity = new BlockGasCapacity();
            var half = config.MaxBlobsPerBlock / 2 + 1;
            var executor = new TransactionExecutor(config);

            var first = await BlobTransactionAsync(half, blockGasLimit: 30_000_000, capacity: capacity);
            var firstResult = await executor.ExecuteAsync(first);
            Assert.False(firstResult.IsValidationError, firstResult.Error);
            capacity.Add(firstResult.GasUsed, firstResult.ExecutionGasUsed, firstResult.StateGasUsed, half, stateGasActive: false);

            var second = await BlobTransactionAsync(half, blockGasLimit: 30_000_000, capacity: capacity);
            var secondResult = await executor.ExecuteAsync(second);

            Assert.True(2 * half > config.MaxBlobsPerBlock, "the pair must overflow the block for this to prove anything");
            Assert.True(secondResult.IsValidationError, "the second transaction must not fit");
            Assert.Equal(TransactionError.Type3TxBlobCountExceeded, secondResult.ErrorCode);
        }
    }
}
