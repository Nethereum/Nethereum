using Nethereum.EVM.Gas;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    /// <summary>
    /// The block-level half of EIP-7778 and EIP-8037. Every other test in this area reads the
    /// TRANSACTION result; nothing asserted what the BLOCK accumulates, and
    /// <see cref="BlockGasCapacity.HeaderGasUsed"/> had no assertion anywhere.
    ///
    /// <para>EIP-8037 §Block-level gas accounting: <i>"The block header <c>gas_used</c> field is
    /// set to: <c>gas_used = max(block_output.block_execution_gas_used,
    /// block_output.block_state_gas_used)</c>"</i>.</para>
    ///
    /// <para>EIP-7778 §Gas Accounting Changes: <i>"Block gas accounting becomes:
    /// <c>block.gas_used += max(tx_gas_used, calldata_floor_gas_cost)</c>"</i> — the refund is
    /// not subtracted, which is why <see cref="BlockGasCapacity.Add"/> takes the per-dimension
    /// totals rather than the sender-facing figure.</para>
    /// </summary>
    public class BlockGasCapacityAccumulationTests
    {
        [Fact]
        [Trait("Category", "EIP7778")]
        public void Given_ATransactionWhoseSenderWasRefunded_When_AddedWithStateGasActive_Then_TheBlockCountsThePreRefundGas()
        {
            var capacity = new BlockGasCapacity();

            capacity.Add(gasUsed: 30_000, executionGasUsed: 41_616, stateGasUsed: 0,
                blobCount: 0, stateGasActive: true);

            Assert.Equal(41_616, capacity.ExecutionGasUsed);
            Assert.Equal(41_616, capacity.HeaderGasUsed);
        }

        [Fact]
        [Trait("Category", "EIP7778")]
        public void Given_TheSameTransaction_When_AddedWithStateGasInactive_Then_TheBlockCountsTheSenderFacingGas()
        {
            var capacity = new BlockGasCapacity();

            capacity.Add(gasUsed: 30_000, executionGasUsed: 41_616, stateGasUsed: 9_000,
                blobCount: 0, stateGasActive: false);

            Assert.Equal(30_000, capacity.ExecutionGasUsed);
            Assert.Equal(0, capacity.StateGasUsed);
            Assert.Equal(30_000, capacity.HeaderGasUsed);
        }

        [Fact]
        [Trait("Category", "EIP8037")]
        public void Given_StateGasExceedsExecutionGas_When_TheHeaderFigureIsRead_Then_ItIsTheLargerOfTheTwo()
        {
            var capacity = new BlockGasCapacity();

            capacity.Add(gasUsed: 0, executionGasUsed: 21_000, stateGasUsed: 183_600,
                blobCount: 0, stateGasActive: true);

            Assert.Equal(183_600, capacity.HeaderGasUsed);
            Assert.NotEqual(capacity.ExecutionGasUsed + capacity.StateGasUsed, capacity.HeaderGasUsed);
        }

        [Fact]
        [Trait("Category", "EIP8037")]
        public void Given_SeveralTransactions_When_Accumulated_Then_EachDimensionSumsIndependently()
        {
            var capacity = new BlockGasCapacity();

            capacity.Add(gasUsed: 0, executionGasUsed: 21_000, stateGasUsed: 1_530,
                blobCount: 0, stateGasActive: true);
            capacity.Add(gasUsed: 0, executionGasUsed: 30_000, stateGasUsed: 183_600,
                blobCount: 0, stateGasActive: true);

            Assert.Equal(51_000, capacity.ExecutionGasUsed);
            Assert.Equal(185_130, capacity.StateGasUsed);
            Assert.Equal(185_130, capacity.HeaderGasUsed);
        }
    }
}
