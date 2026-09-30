using Nethereum.EVM.Gas;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    /// <summary>
    /// EIP-8037 §Gas accounting for halts and reverts: <i>"Then, with the child's
    /// <c>state_gas_reservoir</c> merged in, the frame returns state-gas to <c>gas_left</c>, up to
    /// the amount it has outstanding"</i>, by <c>d = min(state_gas_reservoir,
    /// state_gas_from_gas_left)</c>. <i>"This step is needed because a refill may happen in a
    /// different frame than the matching charge."</i>
    /// </summary>
    public class Eip8037ChildReturnReconcileTests
    {
        private static Program Frame(long gasRemaining, long stateGasLeft, long stateGasSpilled)
        {
            var program = new Program(new byte[0])
            {
                GasRemaining = gasRemaining,
                StateGasLeft = stateGasLeft,
                StateGasBaseline = stateGasLeft,
                StateGasSpilled = stateGasSpilled
            };
            return program;
        }

        [Fact]
        public void Given_AParentHoldingBothPools_When_TheChildReturns_Then_GasLeftIsCreditedByTheOutstandingAmount()
        {
            var parent = Frame(gasRemaining: 1_000, stateGasLeft: 700, stateGasSpilled: 300);

            StateGasMeter.ReturnOutstandingStateGasToGasLeft(parent);

            Assert.Equal(1_300, parent.GasRemaining);
            Assert.Equal(400, parent.StateGasLeft);
            Assert.Equal(0, parent.StateGasSpilled);
        }

        [Fact]
        public void Given_AReservoirSmallerThanTheOutstandingAmount_When_TheChildReturns_Then_OnlyTheReservoirIsReturned()
        {
            var parent = Frame(gasRemaining: 1_000, stateGasLeft: 200, stateGasSpilled: 900);

            StateGasMeter.ReturnOutstandingStateGasToGasLeft(parent);

            Assert.Equal(1_200, parent.GasRemaining);
            Assert.Equal(0, parent.StateGasLeft);
            Assert.Equal(700, parent.StateGasSpilled);
        }

        [Fact]
        public void Given_AParentThatBorrowedNothing_When_TheChildReturns_Then_GasLeftIsUnchanged()
        {
            var parent = Frame(gasRemaining: 1_000, stateGasLeft: 700, stateGasSpilled: 0);

            StateGasMeter.ReturnOutstandingStateGasToGasLeft(parent);

            Assert.Equal(1_000, parent.GasRemaining);
            Assert.Equal(700, parent.StateGasLeft);
            Assert.Equal(0, parent.StateGasSpilled);
        }

        [Fact]
        public void Given_AParentWithAnEmptyReservoir_When_TheChildReturns_Then_NothingMoves()
        {
            var parent = Frame(gasRemaining: 1_000, stateGasLeft: 0, stateGasSpilled: 500);

            StateGasMeter.ReturnOutstandingStateGasToGasLeft(parent);

            Assert.Equal(1_000, parent.GasRemaining);
            Assert.Equal(0, parent.StateGasLeft);
            Assert.Equal(500, parent.StateGasSpilled);
        }

        /// <summary>
        /// EIP-8037: <i>"Note: this undoes no state creation, so <c>evm_state_gas_used</c> is
        /// unchanged. It only moves gas between the two pools."</i> The execution-gas total must
        /// fall by exactly what gas_left gained, so the pair the spill created stays consistent.
        /// </summary>
        [Fact]
        public void Given_AReconciledFrame_When_TheGasTotalsAreRead_Then_TheyMovedByTheSameAmountInOppositeDirections()
        {
            var parent = Frame(gasRemaining: 1_000, stateGasLeft: 700, stateGasSpilled: 300);
            parent.TotalGasUsed = 5_000;

            StateGasMeter.ReturnOutstandingStateGasToGasLeft(parent);

            Assert.Equal(1_300, parent.GasRemaining);
            Assert.Equal(4_700, parent.TotalGasUsed);
        }

        [Fact]
        public void Given_AParentThatSpilledAndAChildThatRefilled_When_TheChildIsAbsorbed_Then_TheBorrowingIsRepaid()
        {
            var parent = Frame(gasRemaining: 10_000, stateGasLeft: 100, stateGasSpilled: 0);
            StateGasMeter.ChargeStateGas(parent, 400);
            Assert.Equal(300, parent.StateGasSpilled);

            var child = Frame(gasRemaining: 0, stateGasLeft: StateGasMeter.DrainReservoir(parent), stateGasSpilled: 0);
            StateGasMeter.CreditStateGasRefund(child, 500);

            var gasBeforeReturn = parent.GasRemaining;
            StateGasMeter.AbsorbChild(parent, child);
            StateGasMeter.ReturnOutstandingStateGasToGasLeft(parent);

            Assert.Equal(gasBeforeReturn + 300, parent.GasRemaining);
            Assert.Equal(200, parent.StateGasLeft);
            Assert.Equal(0, parent.StateGasSpilled);
        }
    }
}
