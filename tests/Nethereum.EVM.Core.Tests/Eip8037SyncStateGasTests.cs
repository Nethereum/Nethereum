using Nethereum.Documentation;
using Nethereum.EVM.Gas;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class Eip8037SyncStateGasTests
    {
        private static Program NewProgram(long gasRemaining, long stateGasLeft)
        {
            var program = new Program(new byte[0])
            {
                GasRemaining = gasRemaining,
                StateGasLeft = stateGasLeft,
                StateGasBaseline = stateGasLeft
            };
            return program;
        }

        [NethereumDocExample(DocSection.EvmSimulator, "state-gas", "EIP-8037: a state-gas charge neither the reservoir nor gas can cover errors the frame and records no spill", Order = 2)]
        [Fact]
        public void Given_InsufficientCombinedGas_WhenChargeStateGasFails_AtEvmSync_Then_NoSpillRecorded()
        {
            var program = NewProgram(gasRemaining: 500, stateGasLeft: 0);

            StateGasMeter.ChargeStateGas(program, 1000);

            Assert.True(program.HasExecutionError);
            Assert.Equal(0, program.GasRemaining);
            Assert.Equal(0, program.StateGasSpilled);
        }

        [Fact]
        public void Given_FailedChargeStateGas_WhenFrameLaterRestored_AtEvmSync_Then_GasRemainingStaysZero_TotalGasUsedNotCorrupted()
        {
            var program = NewProgram(gasRemaining: 500, stateGasLeft: 0);
            program.TotalGasUsed = 12_106;
            StateGasMeter.ChargeStateGas(program, 1000);

            StateGasMeter.RestoreStateGas(program);

            Assert.Equal(0, program.GasRemaining);
            Assert.Equal(12_106, program.TotalGasUsed);
        }

        [NethereumDocExample(DocSection.EvmSimulator, "state-gas", "EIP-8037: a charge beyond the reservoir spills onto execution gas, and a later halt does not return it", Order = 1)]
        [Fact]
        public void Given_LegitimateStateGasSpill_WhenFrameLaterHitsUnrelatedExceptionalHalt_Then_RestoreDoesNotResurrectGasRemaining()
        {
            var program = NewProgram(gasRemaining: 5000, stateGasLeft: 200);
            StateGasMeter.ChargeStateGas(program, 1000);
            Assert.False(program.HasExecutionError);
            Assert.Equal(800, program.StateGasSpilled);

            program.GasRemaining = 0;
            program.MarkExceptionalHalt();

            StateGasMeter.RestoreStateGas(program);

            Assert.Equal(0, program.GasRemaining);
        }
    }
}
