using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.EVM.UnitTests;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    /// <summary>
    /// AMS-GAS-02, the two gas dimensions of <see cref="BlockGasCapacity"/>.
    /// <see cref="BlockGasCapacityBlobTests"/> covers the blob dimension of the
    /// same admission test; this file covers execution gas and state gas.
    ///
    /// <para>EIP-8037 §Transaction validation, item 2, verbatim: <i>"For each gas
    /// dimension, the cumulative gas used of all previous transactions added to the
    /// transaction's contribution does not exceed the block gas limit. Concretely,
    /// <c>min(TX_MAX_GAS_LIMIT, tx.gas) &lt;= execution_gas_available</c> and
    /// <c>tx.gas &lt;= state_gas_available</c>, where <c>execution_gas_available =
    /// block_env.block_gas_limit - block_output.block_execution_gas_used</c> and
    /// <c>state_gas_available = block_env.block_gas_limit -
    /// block_output.block_state_gas_used</c>."</i></para>
    ///
    /// <para>EIP-8037 §Rationale says of EIP-7825, verbatim: <i>"introduces
    /// <c>TX_MAX_GAS_LIMIT</c> (16.7M) as the maximum gas for a single
    /// transaction"</i>.</para>
    ///
    /// <para>THE ASYMMETRY IS THE RULE, not an oversight: the execution operand
    /// is capped at <c>TX_MAX_GAS_LIMIT</c> and the state operand is the raw
    /// declared limit. Reproduced literally in EELS
    /// <c>forks/amsterdam/vm/gas.py:1057-1061</c> as two consecutive
    /// comparisons, one wrapped in <c>min</c> and one not. A later tidy-up that
    /// made the two symmetric — in either direction — is a consensus change,
    /// so it is pinned here from both sides:
    /// <see cref="Given_ATransactionDeclaringMoreGasThanThePerTransactionMaximum_When_TheCappedAmountFitsExecutionGas_Then_ItIsAdmitted"/>
    /// fails if the cap is removed, and
    /// <see cref="Given_ATransactionExceedingRemainingStateGas_When_ExecutionGasHasRoom_Then_ItIsRefused"/>
    /// fails if the cap is added to the state side.</para>
    ///
    /// <para>Asserted at <see cref="TransactionExecutor"/>, the single call site
    /// (<c>TransactionExecutor.ValidateTransaction</c>), which is the level the
    /// rule is stated at. The block-importer view of the same rule lives in
    /// <c>Nethereum.CoreChain.UnitTests.BlockGasCapacityAdmissionTests</c>.</para>
    ///
    /// <para>The block's running totals are primed directly rather than by
    /// executing predecessor transactions: the quantity under test is the
    /// comparison, and a predecessor would fix the totals at whatever it
    /// happened to burn instead of at the boundary each case needs.</para>
    /// </summary>
    public class BlockGasCapacityDimensionTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string RecipientAddress = "0x2222222222222222222222222222222222222222";

        private const long BlockGasLimit = 40_000_000;

        private const long AboveTxMaxGasLimit = 20_000_000;

        private static HardforkConfig Amsterdam() =>
            HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());

        private static HardforkConfig Prague() =>
            HardforkConfig.Prague.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());

        private static BlockGasCapacity CapacityHolding(long executionGasUsed, long stateGasUsed)
        {
            var capacity = new BlockGasCapacity();
            capacity.Add(
                gasUsed: executionGasUsed,
                executionGasUsed: executionGasUsed,
                stateGasUsed: stateGasUsed,
                blobCount: 0,
                stateGasActive: true);
            return capacity;
        }

        private static async Task<TransactionExecutionResult> RunAsync(
            HardforkConfig config, long txGasLimit, BlockGasCapacity capacity,
            long blockGasLimit = BlockGasLimit, ExecutionMode mode = ExecutionMode.Transaction)
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetBalanceAsync(RecipientAddress, 1);

            var ctx = new TransactionExecutionContext
            {
                Mode = mode,
                Sender = SenderAddress,
                To = RecipientAddress,
                Data = null,
                IsContractCreation = false,
                GasLimit = txGasLimit,
                Value = 0,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                BlockGasLimit = blockGasLimit,
                Coinbase = SenderAddress,
                ExecutionState = new ExecutionStateService(node),
                BlockGasCapacity = capacity
            };

            return await new TransactionExecutor(config).ExecuteAsync(ctx);
        }

        private static void AssertRefusedForGasAllowance(TransactionExecutionResult result)
        {
            Assert.True(result.IsValidationError, "the transaction must not be admitted");
            Assert.Equal(TransactionError.GasAllowanceExceeded, result.ErrorCode);
        }

        [Fact]
        public async Task Given_ATransactionFillingExactlyTheRemainingExecutionGas_When_Admitted_Then_ItExecutes()
        {
            var result = await RunAsync(Amsterdam(), txGasLimit: 100_000,
                CapacityHolding(executionGasUsed: BlockGasLimit - 100_000, stateGasUsed: 0));

            Assert.False(result.IsValidationError, result.Error);
            Assert.True(result.Success, result.Error);
        }

        [Fact]
        public async Task Given_ATransactionExceedingRemainingExecutionGasByOne_When_Validated_Then_ItIsRefused()
        {
            var result = await RunAsync(Amsterdam(), txGasLimit: 100_001,
                CapacityHolding(executionGasUsed: BlockGasLimit - 100_000, stateGasUsed: 0));

            AssertRefusedForGasAllowance(result);
        }

        [Fact]
        public async Task Given_ATransactionDeclaringMoreGasThanThePerTransactionMaximum_When_TheCappedAmountFitsExecutionGas_Then_ItIsAdmitted()
        {
            const long executionGasUsed = BlockGasLimit - GasConstants.EIP8037_TX_MAX_GAS_LIMIT;

            Assert.True(AboveTxMaxGasLimit > GasConstants.EIP8037_TX_MAX_GAS_LIMIT,
                "the declared limit must exceed the cap, or the cap is not the quantity under test");
            Assert.True(AboveTxMaxGasLimit <= BlockGasLimit,
                "the state dimension must have room, or the refusal could come from there");

            var result = await RunAsync(Amsterdam(), AboveTxMaxGasLimit,
                CapacityHolding(executionGasUsed, stateGasUsed: 0));

            Assert.False(result.IsValidationError, result.Error);
        }

        [Fact]
        public async Task Given_ATransactionDeclaringMoreGasThanThePerTransactionMaximum_When_TheCappedAmountExceedsExecutionGasByOne_Then_ItIsRefused()
        {
            const long executionGasUsed = BlockGasLimit - GasConstants.EIP8037_TX_MAX_GAS_LIMIT + 1;

            var result = await RunAsync(Amsterdam(), AboveTxMaxGasLimit,
                CapacityHolding(executionGasUsed, stateGasUsed: 0));

            AssertRefusedForGasAllowance(result);
        }

        [Fact]
        public async Task Given_ATransactionDeclaringMoreGasThanThePerTransactionMaximum_When_TheForkPredatesStateGas_Then_TheUncappedLimitIsCharged()
        {
            const long executionGasUsed = BlockGasLimit - GasConstants.EIP8037_TX_MAX_GAS_LIMIT;

            var result = await RunAsync(Prague(), AboveTxMaxGasLimit,
                CapacityHolding(executionGasUsed, stateGasUsed: 0));

            AssertRefusedForGasAllowance(result);
        }

        [Fact]
        public async Task Given_ATransactionExceedingRemainingStateGas_When_ExecutionGasHasRoom_Then_ItIsRefused()
        {
            const long txGasLimit = 2_000_000;
            const long stateGasUsed = BlockGasLimit - 1_000_000;

            Assert.True(txGasLimit <= BlockGasLimit,
                "the execution dimension must have room, or this proves nothing about the state dimension");

            var result = await RunAsync(Amsterdam(), txGasLimit,
                CapacityHolding(executionGasUsed: 0, stateGasUsed: stateGasUsed));

            AssertRefusedForGasAllowance(result);
        }

        [Fact]
        public async Task Given_ATransactionDeclaringMoreGasThanThePerTransactionMaximum_When_OnlyTheCappedAmountRemainsInStateGas_Then_TheRawLimitIsRefused()
        {
            const long stateGasUsed = BlockGasLimit - GasConstants.EIP8037_TX_MAX_GAS_LIMIT;

            Assert.True(AboveTxMaxGasLimit > GasConstants.EIP8037_TX_MAX_GAS_LIMIT,
                "a capped state operand must differ from the raw one, or this case is vacuous");

            var result = await RunAsync(Amsterdam(), AboveTxMaxGasLimit,
                CapacityHolding(executionGasUsed: 0, stateGasUsed: stateGasUsed));

            AssertRefusedForGasAllowance(result);
        }

        [Fact]
        public async Task Given_ATransactionFillingExactlyTheRemainingStateGas_When_Admitted_Then_ItExecutes()
        {
            const long txGasLimit = 1_000_000;

            var result = await RunAsync(Amsterdam(), txGasLimit,
                CapacityHolding(executionGasUsed: 0, stateGasUsed: BlockGasLimit - txGasLimit));

            Assert.False(result.IsValidationError, result.Error);
            Assert.True(result.Success, result.Error);
        }

        [Fact]
        public async Task Given_BothDimensionsHeavilyDrawnButEachWithRoom_When_Validated_Then_TheTransactionIsAdmitted()
        {
            const long drawnPerDimension = 30_000_000;
            const long txGasLimit = 5_000_000;

            Assert.True(2 * drawnPerDimension > BlockGasLimit,
                "the two dimensions must overflow the block when combined, or a single budget would also admit this");

            var result = await RunAsync(Amsterdam(), txGasLimit,
                CapacityHolding(drawnPerDimension, drawnPerDimension));

            Assert.False(result.IsValidationError, result.Error);
            Assert.True(result.Success, result.Error);
        }

        [Fact]
        public async Task Given_AFullyConsumedStateDimension_When_TheForkPredatesStateGas_Then_TheStateCheckRefusesNothing()
        {
            var result = await RunAsync(Prague(), txGasLimit: 1_000_000,
                CapacityHolding(executionGasUsed: 0, stateGasUsed: BlockGasLimit));

            Assert.False(result.IsValidationError, result.Error);
            Assert.True(result.Success, result.Error);
        }

        [Fact]
        public async Task Given_ExhaustedGasDimensions_When_NoBlockGasLimitIsNamed_Then_TheTransactionIsAdmitted()
        {
            var result = await RunAsync(Amsterdam(), txGasLimit: 100_000,
                CapacityHolding(BlockGasLimit, BlockGasLimit), blockGasLimit: 0);

            Assert.False(result.IsValidationError, result.Error);
            Assert.True(result.Success, result.Error);
        }

        [Fact]
        public async Task Given_ExhaustedGasDimensions_When_TheContextIsACall_Then_TheTransactionIsAdmitted()
        {
            var result = await RunAsync(Amsterdam(), txGasLimit: 100_000,
                CapacityHolding(BlockGasLimit, BlockGasLimit),
                blockGasLimit: BlockGasLimit, mode: ExecutionMode.Call);

            Assert.False(result.IsValidationError, result.Error);
            Assert.True(result.Success, result.Error);
        }
    }
}
