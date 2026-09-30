using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.UnitTests;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip8037CalldataFloorGasCapTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string TargetAddress = "0x2222222222222222222222222222222222222222";
        private const long TxMaxGasLimit = 16_777_216;

        private static byte[] NonZeroData(int length)
        {
            var data = new byte[length];
            for (int i = 0; i < length; i++) data[i] = 0x01;
            return data;
        }

        private static async Task<(TransactionExecutionContext ctx, TransactionExecutionResult result)> RunAsync(
            HardforkConfig config, int dataLength, long gasLimit)
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(SenderAddress, BigInteger.Parse("1000000000000000000000000000"));
            var executionState = new ExecutionStateService(node);

            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = TargetAddress,
                Data = NonZeroData(dataLength),
                IsContractCreation = false,
                GasLimit = gasLimit,
                Value = 0,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = executionState
            };

            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);
            return (ctx, result);
        }

        [Fact]
        public async Task Given_CalldataFloorExceedsCap_AtAmsterdam_When_GasLimitMoreThanAffordsIt_Then_StillRejected()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            const long floor = 261_910L * 64 + 15_000;
            Assert.True(floor > TxMaxGasLimit, "test precondition: floor must exceed the cap");

            var (ctx, result) = await RunAsync(config, dataLength: 261_910, gasLimit: floor + 1_000_000);

            Assert.True(result.IsValidationError, "a calldata floor above TX_MAX_GAS_LIMIT must be rejected even when gasLimit affords it");
            Assert.True(ctx.GasLimit.ToLongSafe() > ctx.FloorGas, "test precondition: affordability alone must NOT be what rejects this");
            Assert.Equal(TransactionError.IntrinsicGasTooLow, result.ErrorCode);
        }

        [Fact]
        public async Task Given_CalldataFloorJustUnderCap_AtAmsterdam_When_Validated_Then_Accepted()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            const long floor = 261_909L * 64 + 15_000;
            Assert.True(floor <= TxMaxGasLimit, "test precondition: floor must NOT exceed the cap");

            var (ctx, result) = await RunAsync(config, dataLength: 261_909, gasLimit: TxMaxGasLimit);

            Assert.False(result.IsValidationError, result.Error);
            Assert.Equal(floor, ctx.FloorGas);
        }

        [Fact]
        public async Task Given_GasLimitAboveCap_AtOsaka_When_Validated_Then_RejectedByExistingRawGasLimitCap_NoCrossForkLeak()
        {
            var config = HardforkConfig.Osaka.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            const long overCapGasLimit = 261_910L * 64 + 15_000 + 1_000_000;

            var (_, result) = await RunAsync(config, dataLength: 261_910, gasLimit: overCapGasLimit);

            Assert.True(result.IsValidationError);
            Assert.Equal("GAS_LIMIT_EXCEEDS_MAXIMUM", result.Error);
        }
    }
}
